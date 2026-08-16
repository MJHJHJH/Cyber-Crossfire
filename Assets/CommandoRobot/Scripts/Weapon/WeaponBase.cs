using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
namespace CommandoRobot
{
    public class WeaponBase : MonoBehaviour
    {
        [Header("Data")]
        public int WpnNumber = 0;
        public string WeaponID = "Weapon";
        public string Title = "SMG 1";
        public Sprite WeaponIcon;
        public bool OneHanded = true;
        [Header("FireMode")]
        public bool AutoFire = true;
        public float FireDelay = 0.2f;
        public float RecoilSpeed = 5;
        public float RecoilAngle = 20;
        [Header("Ammo")]
        public int InitAmmo = 80;
        public int MaxAmmo = 80;
        public int AddAmmo = 10;
        public int PowerWeaponMaxAmmo = 80;
        public bool InfiniteAmmo = false;
        [Header("Projectile")]

        public float ProjectileDamage = 1;
        public float ProjectileSpeed = 200;
        public float ProjectileRange = 100;
        //public int ProjectileCount = 1;
        public GameObject ProjectilePrefab;

        [Space]
        public GameObject WeaponModelPrefab;
        public GameObject EffectPrefab;
        [Space]
        [HideInInspector]
        public bool WeaponEnable = true;
        [HideInInspector]
        public int AmmoCount = 50;

        public Transform m_ParticlePoint;
        [HideInInspector]
        public GameObject m_Owner;
        [HideInInspector]
        public CharacterBase m_OwnerCharacter;

        [HideInInspector]
        public PlayerControl Owner;

        [HideInInspector]
        public float FireDelayTimer = 0;
        [HideInInspector]
        public float RecoilTimer = 0;

        [HideInInspector]
        public WeaponModel WeaponModel;

        [HideInInspector]
        public bool Input_FireHold = false;
        [HideInInspector]
        public Vector3 Forward;

        [HideInInspector]
        public int m_PowerType = 0; //0=normal , 1=speed , 2=multi bullet

        [HideInInspector]
        public int m_PowerAmmo = 0;
        void Start()
        {

            if (m_ParticlePoint == null)
                m_ParticlePoint = transform;

            AmmoCount = InitAmmo;
        }

        // Update is called once per frame
        void Update()
        {

            FireDelayTimer -= Time.deltaTime;
            if (FireDelayTimer <= 0)
                FireDelayTimer = 0;

            RecoilTimer -= RecoilSpeed * Time.deltaTime;
            if (RecoilTimer <= 0)
                RecoilTimer = 0;

            if (Input_FireHold)
            {
                if (FireDelayTimer == 0)
                {
                    if (AmmoCount > 0 || InfiniteAmmo)
                    {
                        if (Owner == PlayerControl.m_Main)
                        {
                            CameraControl.m_Current.StartShake(.2f, 1f);
                        }

                        if (m_OwnerCharacter != null && m_OwnerCharacter.m_WeaponPowerType == 2)
                        {
                            FireWeaponPowered();
                        }
                        else
                        {
                            FireWeapon();
                        }

                        AmmoCount -= 1;

                        if (m_OwnerCharacter != null && m_OwnerCharacter.m_WeaponPowerType > 0)
                        {
                            m_OwnerCharacter.m_WeaponPowerAmmo--;
                        }

                        RecoilTimer = 1;
                    }
                    else
                    {
                        //SoundGallery.PlaySound("EmptyFire1");
                    }


                    FireDelayTimer = FireDelay;
                    if (m_OwnerCharacter != null && m_OwnerCharacter.m_WeaponPowerType == 1)
                    {
                        FireDelayTimer = .5f * FireDelay;
                    }

                }
            }

            Input_FireHold = false;
        }

        public virtual void FireWeapon()
        {
            if (ProjectilePrefab != null)
            {
                m_OwnerCharacter.PlayFireAnimation();
                CreateProjectile(0);
            }

            CreateParticle();
        }

        public virtual void FireWeaponPowered()
        {
            if (ProjectilePrefab != null)
            {
                for (int i = -1; i < 2; i++)
                {
                    CreateProjectile(i * 10);
                }
            }

            CreateParticle();
        }

        public virtual void CreateProjectile(float deltaAngle)
        {
            GameObject obj = Instantiate(ProjectilePrefab);
            if (WeaponModel != null)
                obj.transform.position = WeaponModel.m_FirePoint.position;
            else
                obj.transform.position = transform.position;

            obj.transform.forward = Quaternion.Euler(0, deltaAngle, 0) * m_OwnerCharacter.m_CharBody.m_UpperAimBase.forward;

            ProjectileBase projectile = obj.GetComponent<ProjectileBase>();
            projectile.m_Speed = ProjectileSpeed;
            projectile.m_Creator = m_Owner;
            projectile.m_Damage = ProjectileDamage;
            projectile.m_Range = ProjectileRange;
            Destroy(obj, 5);
        }

        public virtual void CreateParticle()
        {
            if (EffectPrefab != null)
            {
                GameObject obj;
                obj = Instantiate(EffectPrefab);
                if (WeaponModel != null)
                    obj.transform.SetParent(WeaponModel.m_FirePoint, false);
                else
                    obj.transform.SetParent(transform, false);

                //obj.transform.position = WeaponModel.m_FirePoint.position;
                //obj.transform.forward = WeaponModel.m_FirePoint.forward;
                Destroy(obj, 3);
            }
        }
        public void AddModel()
        {
            WeaponModel = Instantiate(WeaponModelPrefab).GetComponent<WeaponModel>();
        }
    }


}