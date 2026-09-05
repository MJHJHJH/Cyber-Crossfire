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
            CameraControl.m_Current.StartShake(.2f, .1f);
        }

        void LateUpdate()
        {
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
            Vector3 end = transform.position + 20 * m_CharBody.m_UpperAimBase.forward;
            if (m_BestTargetObject != null)
            {
                end = m_BestTargetObject.transform.position;
            }
            GameObject obj = Instantiate(m_GrenadePrefab1);
            obj.transform.position = transform.position;
            PlayerGrenade g = obj.GetComponent<PlayerGrenade>();
            g.m_StartPosition = start;
            g.m_TargetPosition = end;
            //Destroy(obj, 3);
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