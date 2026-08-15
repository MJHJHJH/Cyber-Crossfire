using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace CommandoRobot
{
    public class EnemyAI_Tank_A : AIControlBase
    {
        public WeaponBase m_Weapon;
        public GameObject m_BulletPrefab1;
        public Transform m_FirePoint;
        // Start is called before the first frame update
        void Start()
        {
            m_Weapon.m_Owner = gameObject;
            m_Weapon.m_OwnerCharacter = GetComponent<EnemyCharacter>();
        }

        // Update is called once per frame
        void Update()
        {
            CheckAlert();

            if (m_IsAlerted)
            {

                GetComponent<EnemyCharacter>().FaceTowardsPosition(PlayerCharacter.m_Current.transform.position);
            }
        }

        public override void StartAlert()
        {
            base.StartAlert();

            StartCoroutine(Co_AttackLoop());
        }

        IEnumerator Co_AttackLoop()
        {
            yield return new WaitForSeconds(1);
            while (true)
            {
                //m_Weapon.FireWeapon();
                GetComponent<CharacterBase>().m_CharAnimator.Play("anim-fire");
                ShootBullets();
                yield return new WaitForSeconds(2);
            }
        }

        public void ShootBullets()
        {
            for (int i = -1; i < 2; i++)
            {
                GameObject obj = Instantiate(m_BulletPrefab1);
                obj.transform.position = m_FirePoint.position;
                obj.transform.forward = Quaternion.Euler(0, i * 10, 0) * GetComponent<CharacterBase>().m_CharBody.m_RotationBase.forward;
                ProjectileBase projectile = obj.GetComponent<ProjectileBase>();
                projectile.m_Creator = gameObject;
                projectile.m_Speed = 6;
                projectile.m_Damage = 1;
                projectile.m_Range = 50;
                Destroy(obj, 10);
            }
        }
    }
}