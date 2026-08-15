using System.Collections;
using System.Collections.Generic;
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
        void Awake()
        {
            m_CharMovement = GetComponent<CharacterMovement>();
            m_DamageControl = GetComponent<DamageControl>();
            m_CharAnimator = GetComponentInChildren<Animator>();
            m_CharBody = GetComponentInChildren<CharacterBody>();
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

        public void UpdateAnimatorData()
        {
            if (m_CharAnimator == null) return;

            float runSpeed = Mathf.Clamp(m_CharMovement.m_CurrentHorVelocity.magnitude / m_CharMovement.m_MaxSpeed, 0, 1);
            m_CharAnimator.SetFloat("RunBlend", runSpeed);

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

            if (m_CurrentWeapon != null)
            {
                float recoil = -m_CurrentWeapon.RecoilAngle * m_CurrentWeapon.RecoilTimer;
                m_CharBody.m_RecoilBones[0].position += recoil * m_CharBody.m_UpperAimBase.forward;

                if (m_CharBody.m_RecoilBones[1] == null)
                    return;
                m_CharBody.m_RecoilBones[1].position += recoil * m_CharBody.m_UpperAimBase.forward;
            }
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
            GameObject obj = Instantiate(m_DeathParticle);
            obj.transform.position = transform.position + new Vector3(0, 1, 0);
            Destroy(obj, 3);
            gameObject.SetActive(false);
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
            if (!prefab)
                return;

            if (m_CurrentWeapon != null)
            {
                if (m_CurrentWeapon.WeaponModel != null)
                {
                    Destroy(m_CurrentWeapon.WeaponModel.gameObject);
                }
                Destroy(m_CurrentWeapon.gameObject);

                m_CurrentWeapon = null;
                m_WeaponNum = 1;
            }

            WeaponBase wpn = Instantiate(prefab).GetComponent<WeaponBase>();
            m_CurrentWeapon = wpn;
            m_CurrentWeapon.AddModel();
            m_CurrentWeapon.WeaponModel.transform.SetParent(m_CharBody.m_GunPoints[0], false);
            //m_CurrentWeapon.WeaponModel.transform.localScale =.01f* Vector3.one;
            //m_CurrentWeapon.WeaponModel.transform.localRotation = Quaternion.Euler(-90, 0,-180);
            m_CurrentWeapon.m_Owner = gameObject;
            m_CurrentWeapon.m_OwnerCharacter = this;
        }

        public virtual void FireWeapon()
        {
            if (m_CurrentWeapon != null)
            {
                m_CurrentWeapon.Input_FireHold = true;
            }
        }
    }
}