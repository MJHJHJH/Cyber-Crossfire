using System.Collections;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using GamePlay.Data;
using UnityEngine;


namespace CommandoRobot
{
    public class PlayerCharacter : CharacterBase
    {
        public static PlayerCharacter m_Current;

        //-------------------------------
        [HideInInspector]
        public bool m_InControl = false;
        [HideInInspector]
        public bool m_CanShoot = false;

        public Transform[] m_WeaponHands;

        Vector3 m_DashDirection;
        public AnimationCurve m_DashCurve;
        public GameObject m_DashParticle;

        bool m_Input_Fire;


        public GameObject m_GrenadePrefab1;
        public int m_GrenadeCount = 3;
        public float m_MaxThrowDistance = 20f;

        [HideInInspector]
        public PlayerPowers m_PlayerPowers;

        void Awake()
        {
            m_Current = this;
            m_PlayerPowers = GetComponent<PlayerPowers>();
            m_CharMovement = GetComponent<CharacterMovement>();
            m_DamageControl = GetComponent<DamageControl>();
            m_CharAnimator = GetComponentInChildren<Animator>();
            m_CharBody = GetComponentInChildren<CharacterBody>();
        }

        void OnDestroy()
        {
            if (m_Current == this)
                m_Current = null;
        }

        void Start()
        {
            m_DamageControl.OnDamaged.AddListener(HandleDamage);
            m_InControl = true;
            m_GrenadeCount = 3;
            InitDefaultWeaponAsync().Forget();
        }

        async UniTaskVoid InitDefaultWeaponAsync()
        {
            await EquipWeaponAsync((WeaponId)PlayerSave.SelectedWeapon);
            EnableDefaultWeaponInfiniteAmmo();
        }

        void EnableDefaultWeaponInfiniteAmmo()
        {
            if (!IsWeaponReady)
                return;

            WeaponConfig cfg = m_CurrentWeapon.Config;
            cfg.InfiniteAmmo = true;
            m_CurrentWeapon.Config = cfg;
        }

        async UniTask EquipWeaponAsync(WeaponId weaponId)
        {
            if (GameControl.m_Current == null)
            {
                Debug.LogError("[PlayerCharacter] GameControl missing.");
                return;
            }

            CancellationToken ct = this.GetCancellationTokenOnDestroy();
            GameObject prefab = await GameControl.m_Current.GetGunPrefabAsync(weaponId, ct);
            if (prefab == null || ct.IsCancellationRequested)
                return;

            await SetWeaponAsync(prefab, ct);
        }

        // Update is called once per frame
        void Update()
        {
            FindBestTarget();
            UpdateControls();
            UpdateAnimatorData();
            UpdateRotation();
            CheckWeaponAmmo();
            CheckDeath();

            if (m_WeaponPowerType > 0)
            {
                if (m_WeaponPowerAmmo <= 0)
                {
                    m_WeaponPowerType = 0;
                    m_WeaponPowerAmmo = 0;
                }
            }
        }

        public void CheckWeaponAmmo()
        {
            if (!IsWeaponReady)
                return;

            WeaponId selected = (WeaponId)PlayerSave.SelectedWeapon;
            if (m_CurrentWeapon.WeaponId != selected && m_CurrentWeapon.AmmoCount <= 0)
                SwitchToDefaultWeaponAsync(selected).Forget();
        }

        async UniTaskVoid SwitchToDefaultWeaponAsync(WeaponId selected)
        {
            await EquipWeaponAsync(selected);
            EnableDefaultWeaponInfiniteAmmo();
        }

        public void UpdateControls()
        {
            m_Input_Fire = false;

            // 仅游戏正常运行（进行中且未暂停）时监听移动/开火/投掷；暂停界面打开时冻结角色控制
            GameControl gc = GameControl.m_Current;
            if (gc != null && !gc.IsGamePlaying)
            {
                m_MovementInput = Vector3.zero;
                if (IsWeaponReady)
                    m_CurrentWeapon.Input_FireHold = false;
                return;
            }

            if (!m_InControl) return;

            m_MovementInput = InputControl.m_Main.m_Movement;
            if (m_MovementInput.magnitude > .1f)
                m_CharMovement.AddMovement(m_MovementInput);

            m_Input_Fire = InputControl.m_Main.m_Fire;
            if (IsWeaponReady)
                m_CurrentWeapon.Input_FireHold = InputControl.m_Main.m_Fire;

            if (m_GrenadeCount > 0 && InputControl.m_Main.m_Grenade)
            {
                ThrowGrenade();
            }
        }

        public void HandleDamage()
        {
            CameraControl.m_Current.PlayShake(
                CameraShakePreset.Hit,
                ShakeSettings.Position(0.32f, 0.48f, 38f));
        }

        void LateUpdate()
        {
            // 暂停/停止界面打开或胜负结算时冻结瞄准旋转，避免角色仍跟随鼠标/目标转动
            GameControl gc = GameControl.m_Current;
            if (gc != null && !gc.IsGamePlaying)
            {
                m_MovementInput = Vector3.zero;
                UpdateRecoilTransforms();
                return;
            }

            if (InputControl.m_Main.m_MouseAim)
            {
                Vector3 targetPos = InputControl.m_Main.m_WorldAimPosition;
                Vector3 targetDir = targetPos - transform.position;
                targetDir.y = 0;
                m_CharBody.m_UpperAimBase.rotation = Quaternion.LookRotation(targetDir);
            }
            else
            {
                if (m_BestTargetObject != null)
                {
                    Vector3 targetPos = m_BestTargetObject.m_TargetCenter.position;
                    Vector3 targetDir = targetPos - transform.position;
                    targetDir.y = 0;
                    m_CharBody.m_UpperAimBase.rotation = Quaternion.LookRotation(targetDir);
                }
                else
                {
                    m_CharBody.m_UpperAimBase.rotation = m_CharBody.m_RotationBase.rotation;
                }
            }

            m_MovementInput = Vector3.zero;
            UpdateRecoilTransforms();
        }
        public void CheckMelleeAttack()
        {
            Collider[] colls = Physics.OverlapSphere(transform.position + 2 * m_CharBody.m_UpperAimBase.forward, 1);
            foreach (Collider col in colls)
            {
                if (col.gameObject.tag == "Enemy")
                {
                    DamageControl d = col.gameObject.GetComponent<DamageControl>();
                    if (d != null)
                    {
                        Vector3 dir = col.gameObject.transform.position - transform.position;
                        dir.Normalize();
                        d.ApplyDamage(5, dir, 1);
                    }
                }
                else if (col.gameObject.tag == "Block")
                {
                    //Rigidbody rb = col.gameObject.GetComponent<Rigidbody>();
                    //if (rb != null)
                    //{
                    //    Vector3 dir = col.gameObject.transform.position - transform.position;
                    //    dir.Normalize();
                    //    rb.AddForceAtPosition(3000 * dir, col.gameObject.transform.position);
                    //}

                    DamageControl d = col.gameObject.GetComponent<DamageControl>();
                    if (d != null)
                    {
                        //float lerp = Vector3.Distance(col.bounds.center, transform.position) / Radius;
                        d.ApplyDamage(1, transform.forward, 1);
                    }
                }
            }
        }

        public void AddAmmo(int count)
        {

        }


        public void ThrowGrenade()
        {
            m_GrenadeCount--;

            Vector3 start = transform.position;
            Vector3 end = ComputeGrenadeTarget(start);
            GameObject obj = Instantiate(m_GrenadePrefab1);
            obj.transform.position = start;
            PlayerGrenade g = obj.GetComponent<PlayerGrenade>();
            g.m_StartPosition = start;
            g.m_TargetPosition = end;
            //Destroy(obj, 3);
        }

        /// <summary>
        /// 计算手雷落点：鼠标瞄准时抛向鼠标点击的世界位置；
        /// 超出最大投掷距离则沿“玩家→点击点”的方向截取到最大距离处。
        /// 非鼠标瞄准（手柄/移动端）时保持原有逻辑：优先抛向锁定目标，否则沿瞄准方向最大距离。
        /// </summary>
        Vector3 ComputeGrenadeTarget(Vector3 start)
        {
            InputControl input = InputControl.m_Main;
            if (input != null && input.m_MouseAim)
            {
                Vector3 toTarget = input.m_WorldAimPosition - start;
                toTarget.y = 0;
                float dist = toTarget.magnitude;
                if (dist > 0.001f)
                {
                    Vector3 dir = toTarget / dist;
                    dist = Mathf.Min(dist, m_MaxThrowDistance);
                    Vector3 end = start + dir * dist;
                    end.y = start.y; // 与起点同高，保证抛物线高度曲线一致
                    return end;
                }
                // 点击点与玩家几乎重合：退化为瞄准方向
            }

            if (m_BestTargetObject != null)
                return m_BestTargetObject.transform.position;
            return start + m_MaxThrowDistance * m_CharBody.m_UpperAimBase.forward;
        }

        public void HandlePickup(string itemType, int count)
        {
            if (itemType == "Coin")
            {
                PlayerSave.AddCoin(5);
            }
            else if (itemType == "WeaponShotgun")
            {
                GameControl.m_Current.ShowMessage("Shotgun");
                if (m_CurrentWeapon != null && m_CurrentWeapon.WeaponId == WeaponId.Shotgun)
                    m_CurrentWeapon.AmmoCount += 40;
                else
                    EquipWeaponAsync(WeaponId.Shotgun).Forget();
            }
            else if (itemType == "WeaponRPG")
            {
                GameControl.m_Current.ShowMessage("Rocket Launcher");
                if (m_CurrentWeapon != null && m_CurrentWeapon.WeaponId == WeaponId.RPG)
                    m_CurrentWeapon.AmmoCount += 10;
                else
                    EquipWeaponAsync(WeaponId.RPG).Forget();
            }
            else if (itemType == "WeaponEnergy")
            {
                GameControl.m_Current.ShowMessage("Energy Rifle");
                if (m_CurrentWeapon != null && m_CurrentWeapon.WeaponId == WeaponId.Energy)
                    m_CurrentWeapon.AmmoCount += 10;
                else
                    EquipWeaponAsync(WeaponId.Energy).Forget();
            }
            else if (itemType == "WeaponSniper")
            {
                GameControl.m_Current.ShowMessage("Sniper Rifle");
                if (m_CurrentWeapon != null && m_CurrentWeapon.WeaponId == WeaponId.Sniper)
                    m_CurrentWeapon.AmmoCount += 10;
                else
                    EquipWeaponAsync(WeaponId.Sniper).Forget();
            }
            else if (itemType == "WeaponCrossbow")
            {
                GameControl.m_Current.ShowMessage("Crossbow");
                if (m_CurrentWeapon != null && m_CurrentWeapon.WeaponId == WeaponId.Crossbow)
                    m_CurrentWeapon.AmmoCount += 10;
                else
                    EquipWeaponAsync(WeaponId.Crossbow).Forget();
            }
            else if (itemType == "WeaponGrenade")
            {
                GameControl.m_Current.ShowMessage("Grenade");
                m_GrenadeCount += 3;
            }
            else if (itemType == "WeaponPower1")
            {
                GameControl.m_Current.ShowMessage("Weapon Powerup");
                m_WeaponPowerType = 1;
                m_WeaponPowerAmmo = 50;
            }
            else if (itemType == "WeaponPower2")
            {
                GameControl.m_Current.ShowMessage("Weapon Powerup");
                m_WeaponPowerType = 2;
                m_WeaponPowerAmmo = 50;
            }
            else if (itemType == "Health")
            {
                m_DamageControl.AddHealth(count);
            }
        }

        void OnCollisionEnter(Collision collision)
        {
            if (collision.gameObject.tag == "DamageHit")
            {
                Vector3 dir = collision.contacts[0].normal;
                dir.y = 0;
                dir.Normalize();
                m_DamageControl.ApplyDamage(4, dir, 1);
                GetComponent<Rigidbody>().AddForce(10 * dir, ForceMode.Impulse);
            }
        }

    }
}