using System.Collections;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using GameFramework;
using GamePlay;
using UnityEngine;
using UnityEngine.UIElements;
namespace CommandoRobot
{
    public class CharacterBase : MonoBehaviour
    {
        [HideInInspector]
        public CharacterMovement m_CharMovement;
        [HideInInspector]
        public DamageControl m_DamageControl;
        [HideInInspector]
        public Animator m_CharAnimator;
        [HideInInspector]
        public CharacterBody m_CharBody;

        [HideInInspector]
        public bool m_IsDead = false;
        [HideInInspector]
        public Vector3 m_MovementInput;

        [HideInInspector]
        public TargetObject m_BestTargetObject;

        [HideInInspector]
        public Vector3 m_RotationTargetPosition;

        public GameObject m_DeathParticle;

        public GameObject m_WeaponPrefab;

        [HideInInspector]
        public WeaponBase m_CurrentWeapon;
        [HideInInspector]
        public WeaponBase[] m_Weapons;

        [HideInInspector]
        public int m_WeaponPowerAmmo = 0;
        [HideInInspector]
        public int m_WeaponPowerType = 0;

        [HideInInspector]
        public int m_WeaponNum = 0;

        private int _setWeaponVersion;

        /// <summary>当前武器已实例化且表驱动资源加载完成，可供开火/轮询使用。</summary>
        public bool IsWeaponReady => m_CurrentWeapon != null && m_CurrentWeapon.AssetsReady;

        bool _animatorParamsCached;
        bool _hasRunBlend;
        int _runBlendHash;
        static readonly int AnimFireHash = Animator.StringToHash("anim-fire");

        void Awake()
        {
            m_CharMovement = GetComponent<CharacterMovement>();
            m_DamageControl = GetComponent<DamageControl>();
            m_CharAnimator = GetComponentInChildren<Animator>();
            m_CharBody = GetComponentInChildren<CharacterBody>();
        }

        public void PlayFireAnimation()
        {
            if (m_CharAnimator == null) return;
            if (!m_CharAnimator.HasState(0, AnimFireHash)) return;
            m_CharAnimator.Play(AnimFireHash, 0, 0f);
        }
        // Start is called before the first frame update
        void Start()
        {
            m_MovementInput = Vector3.zero;
        }

        // Update is called once per frame
        void Update()
        {
            m_CharMovement.AddMovement(m_MovementInput);

            UpdateAnimatorData();
            UpdateRotation();

            CheckDeath();
        }

        void CacheAnimatorParams()
        {
            if (_animatorParamsCached) return;
            _animatorParamsCached = true;
            _runBlendHash = Animator.StringToHash("RunBlend");
            if (m_CharAnimator == null) return;

            AnimatorControllerParameter[] parameters = m_CharAnimator.parameters;
            for (int i = 0; i < parameters.Length; i++)
            {
                if (parameters[i].nameHash == _runBlendHash)
                {
                    _hasRunBlend = true;
                    break;
                }
            }
        }

        public void UpdateAnimatorData()
        {
            if (m_CharAnimator == null) return;

            CacheAnimatorParams();
            if (_hasRunBlend)
            {
                float runSpeed = Mathf.Clamp(m_CharMovement.m_CurrentHorVelocity.magnitude / m_CharMovement.m_MaxSpeed, 0, 1);
                m_CharAnimator.SetFloat(_runBlendHash, runSpeed);
            }

            //if (m_CurrentWeapon != null)
            //{
            //    if (m_CurrentWeapon.OneHanded)
            //    {
            //        m_CharAnimator.SetInteger("WeaponType", 1);

            //    }
            //    else
            //    {
            //        m_CharAnimator.SetInteger("WeaponType", 2);

            //    }

            //}
            //else
            //{
            //    m_CharAnimator.SetInteger("WeaponType", 0);
            //}

            UpdateRecoilTransforms();
        }

        public void UpdateRecoilTransforms()
        {
            if (m_CharBody.m_RecoilBones == null || m_CharBody.m_RecoilBones.Length == 0 || m_CharBody.m_RecoilBones[0] == null)
                return;

            if (!IsWeaponReady)
                return;

            float recoil = -m_CurrentWeapon.Config.RecoilAngle * m_CurrentWeapon.RecoilTimer;
            m_CharBody.m_RecoilBones[0].position += recoil * m_CharBody.m_UpperAimBase.forward;

            if (m_CharBody.m_RecoilBones[1] == null)
                return;
            m_CharBody.m_RecoilBones[1].position += recoil * m_CharBody.m_UpperAimBase.forward;
        }
        public void CheckDeath()
        {
            if (m_IsDead) return;

            if (transform.position.y <= -4.2f)
            {
                m_IsDead = true;
                HandleDeath();
            }

            if (m_DamageControl.Damage <= 0)
            {
                //die
                m_IsDead = true;
                HandleDeath();
            }

        }

        public virtual void HandleDeath()
        {
            PlayDeathSound();

            if (m_DeathParticle != null)
            {
                GameObject obj = Instantiate(m_DeathParticle);
                obj.transform.position = transform.position + new Vector3(0, 1, 0);
                Destroy(obj, 3);
            }

            gameObject.SetActive(false);
        }

        /// <summary>
        /// 临时验证：硬编码 SoundId；后续改由角色/业务表配置。
        /// 敌方死亡勿绑定 Transform（对象会立即 Destroy，会导致音效被截断）。
        /// </summary>
        protected void PlayDeathSound()
        {
            if (this is PlayerCharacter)
                GameFrameWork.Sound?.PlaySound(SoundIds.PlayerDead);
            else
                GameFrameWork.Sound?.PlaySound(SoundIds.EnemyExplosion);
        }

        public void UpdateRotation()
        {
            if (m_CharMovement.m_FaceTowardsMovement)
            {
                if (m_CharMovement.m_Movement.magnitude > .1f)
                {
                    FaceTowardsDirection(m_CharMovement.m_Movement);
                }
            }
            else
            {
                Vector3 dir = m_RotationTargetPosition - transform.position;
                dir.y = 0;
                dir.Normalize();
                FaceTowardsDirection(dir);
            }

            //m_CharBody.m_UpperAimBase.localRotation = Quaternion.identity;
        }

        public void FindBestTarget()
        {
            //check last target
            //if (m_BestTargetObject != null)
            //{

            //}
            //else
            {
                //find target
                List<TargetObject> targets = TargetsControl.m_Main.m_Targets;

                TargetObject tempTarget = null;

                float minAngle = 40;
                float minDistance = 25;
                foreach (TargetObject target in targets)
                {
                    if (target == null)
                        continue;

                    Vector3 targetPos = target.m_TargetCenter.position;
                    Vector3 dir = targetPos - transform.position;
                    dir.y = 0;
                    float delta = Vector3.Angle(m_CharBody.m_RotationBase.forward, dir);
                    float distance = dir.magnitude;

                    if (distance > 25)
                        continue;

                    if (delta < minAngle)
                    {
                        tempTarget = target;
                        minAngle = delta;
                        minDistance = distance;
                    }

                    //if (distance < minDistance)
                    //{
                    //    tempTarget = target;
                    //    minAngle = delta;
                    //    minDistance = distance;
                    //}
                }

                //if (tempTarget != null)
                {
                    m_BestTargetObject = tempTarget;
                }
            }
        }

        public void AimTowardsPosition(Vector3 pos)
        {
            Vector3 dir = pos - transform.position;
            dir.y = 0;
            dir.Normalize();
            if (m_CharBody.m_UpperAimBase != null)
            {
                m_CharBody.m_UpperAimBase.forward = dir;
            }
        }

        public void FaceTowardsPosition(Vector3 targetPosition)
        {
            Vector3 dir = targetPosition - transform.position;
            dir.y = 0;
            dir.Normalize();
            m_CharBody.m_RotationBase.rotation = Quaternion.Lerp(m_CharBody.m_RotationBase.rotation, Quaternion.LookRotation(dir), 10 * Time.deltaTime);
        }

        public void FaceTowardsDirection(Vector3 targetDirection)
        {
            Vector3 faceDirection = targetDirection;
            faceDirection.y = 0;
            faceDirection.Normalize();
            m_CharBody.m_RotationBase.rotation = Quaternion.Lerp(m_CharBody.m_RotationBase.rotation, Quaternion.LookRotation(faceDirection), 10 * Time.deltaTime);
        }

        public void SetWeapon(GameObject prefab)
        {
            SetWeaponAsync(prefab, this.GetCancellationTokenOnDestroy()).Forget();
        }

        public async UniTask SetWeaponAsync(GameObject prefab, CancellationToken cancellationToken = default)
        {
            if (!prefab)
                return;

            int version = ++_setWeaponVersion;
            DestroyCurrentWeapon();

            WeaponBase wpn = Instantiate(prefab).GetComponent<WeaponBase>();
            m_CurrentWeapon = wpn;
            wpn.m_Owner = gameObject;
            wpn.m_OwnerCharacter = this;
            wpn.Owner = this is PlayerCharacter ? PlayerControl.m_Main : null;

            try
            {
                await wpn.ApplyTableConfigAsync(cancellationToken);
            }
            catch (System.OperationCanceledException)
            {
                if (m_CurrentWeapon == wpn)
                    DestroyCurrentWeapon();
                else
                    DestroyWeaponInstance(wpn);
                return;
            }
            catch (System.Exception)
            {
                if (m_CurrentWeapon == wpn)
                    DestroyCurrentWeapon();
                else
                    DestroyWeaponInstance(wpn);
                throw;
            }

            // 已过期（被更新的换枪覆盖）或取消：若仍持有本实例则销毁，避免泄漏
            if (cancellationToken.IsCancellationRequested || version != _setWeaponVersion)
            {
                if (m_CurrentWeapon == wpn)
                    DestroyCurrentWeapon();
                else
                    DestroyWeaponInstance(wpn);
                return;
            }

            if (m_CurrentWeapon != wpn)
                return;

            wpn.AddModel();
            if (wpn.WeaponModel != null && m_CharBody != null && m_CharBody.m_GunPoints != null && m_CharBody.m_GunPoints.Length > 0)
                wpn.WeaponModel.transform.SetParent(m_CharBody.m_GunPoints[0], false);
        }

        private void DestroyCurrentWeapon()
        {
            if (m_CurrentWeapon == null)
                return;

            DestroyWeaponInstance(m_CurrentWeapon);
            m_CurrentWeapon = null;
            m_WeaponNum = 1;
        }

        private static void DestroyWeaponInstance(WeaponBase weapon)
        {
            if (weapon == null)
                return;

            if (weapon.WeaponModel != null)
                Destroy(weapon.WeaponModel.gameObject);

            Destroy(weapon.gameObject);
        }

        public virtual void FireWeapon()
        {
            if (!IsWeaponReady)
                return;

            m_CurrentWeapon.Input_FireHold = true;
        }
    }
}