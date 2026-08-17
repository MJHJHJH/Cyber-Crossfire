using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace CommandoRobot
{
    public class EnemyAI_Boss_A : AIControlBase
    {

        public GameObject m_BulletPrefab1;

        public Transform[] m_FirePoints;
        [HideInInspector]
        public int m_AttackLevel = 0;
        // Start is called before the first frame update
        void Start()
        {
            //m_DamageControl = GetComponent<DamageControl>();
            //InitPosition = transform.position;
            //m_DamageControl.m_NoDamage = true;
            //transform.position = InitPosition + new Vector3(0, 0, 20);

            m_AttackLevel = 0;
        }

        // Update is called once per frame
        void Update()
        {
            DamageControl damageControl = GetComponent<DamageControl>();
            float damage = damageControl.Damage / damageControl.MaxDamage;
            if (damage > .6f)
            {
                m_AttackLevel = 0;
            }
            else if (damage > .3f)
            {
                m_AttackLevel = 1;
            }
            else
            {
                m_AttackLevel = 2;
            }

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
            while (true)
            {
                switch (m_AttackLevel)
                {
                    case 0:
                        yield return new WaitForSeconds(1f);
                        ShootRingBullet(2);
                        yield return new WaitForSeconds(2f);
                        for (int i = 0; i < 5; i++)
                        {
                            ShootBullet();
                            yield return new WaitForSeconds(.3f);
                        }
                        yield return new WaitForSeconds(2f);
                        break;

                    case 1:
                        yield return new WaitForSeconds(1f);
                        ShootRingBullet(3);
                        yield return new WaitForSeconds(.7f);
                        ShootRingBullet(3);
                        yield return new WaitForSeconds(2f);
                        for (int i = 0; i < 10; i++)
                        {
                            ShootBullet();
                            yield return new WaitForSeconds(.2f);
                        }
                        yield return new WaitForSeconds(1f);
                        break;

                    case 2:
                        yield return new WaitForSeconds(1f);
                        ShootRingBullet(4);
                        yield return new WaitForSeconds(.6f);
                        ShootRingBullet(4);
                        yield return new WaitForSeconds(.6f);
                        ShootRingBullet(4);
                        yield return new WaitForSeconds(2f);
                        for (int i = 0; i < 15; i++)
                        {
                            ShootBullet();
                            yield return new WaitForSeconds(.3f);
                        }
                        yield return new WaitForSeconds(1f);
                        break;
                }
            }
            //yield return null;
        }
        IEnumerator Co_MoveLoop()
        {
            //EnemyMovement movement = GetComponent<EnemyMovement>();
            //StateSystem stateSys = GetComponent<StateSystem>();

            //movement.m_FaceMoveDirection = false;
            //m_FacePlayer = true;

            //Vector3[] points = new Vector3[4];
            //points[0] = InitPosition + new Vector3(-3, 0, 0);
            //points[1] = InitPosition + new Vector3(3, 0, 0);
            //points[2] = InitPosition + new Vector3(-3, 0, 2);
            //points[3] = InitPosition + new Vector3(3, 0, 2);
            //int pointNum = 0;
            //while (true)
            //{
            //    //movement.m_FaceMoveDirection = true;
            //    //m_FacePlayer = false;
            //    movement.SetMoveTargetPosition(points[pointNum]);
            //    while (!movement.m_ReachedTargetPosition)
            //    {
            //        yield return null;
            //    }

            //    pointNum = Random.Range(0, 4);

            //    yield return new WaitForSeconds(1);

            //}
            yield return new WaitForSeconds(1);
        }



        public void ShootBullet()
        {
            Vector3 dir;
            GameObject obj;
            Vector3 pos = Vector3.zero;
            obj = Instantiate(m_BulletPrefab1);
            pos = m_FirePoints[1].position;
            pos.y = 1;
            obj.transform.position = pos;
            dir = PlayerCharacter.m_Current.transform.position - m_FirePoints[1].position;
            dir.y = 0;
            obj.transform.forward = Quaternion.Euler(0, -00, 0) * dir;
            ProjectileBase projectile = obj.GetComponent<ProjectileBase>();
            projectile.m_Creator = gameObject;
            projectile.m_Speed = 10;
            projectile.m_Damage = 1;
            projectile.m_Range = 50;
            Destroy(obj, 10);

            obj = Instantiate(m_BulletPrefab1);
            pos = m_FirePoints[2].position;
            pos.y = 1;
            obj.transform.position = pos;
            dir = PlayerCharacter.m_Current.transform.position - m_FirePoints[2].position;
            dir.y = 0;
            obj.transform.forward = Quaternion.Euler(0, 00, 0) * dir;
            projectile = obj.GetComponent<ProjectileBase>();
            projectile.m_Creator = gameObject;
            projectile.m_Speed = 10;
            projectile.m_Damage = 1;
            projectile.m_Range = 50;
            Destroy(obj, 10);

        }

        public void ShootRingBullet(int halfCount)
        {
            for (int i = -halfCount; i <= halfCount; i++)
            {
                GameObject obj = Instantiate(m_BulletPrefab1);
                Vector3 pos = m_FirePoints[0].position;
                pos.y = 1;
                obj.transform.position = pos;
                obj.transform.forward = Quaternion.Euler(0, i * 20, 0) * GetComponent<CharacterBase>().m_CharBody.m_RotationBase.forward;
                ProjectileBase projectile = obj.GetComponent<ProjectileBase>();
                projectile.m_Creator = gameObject;
                projectile.m_Speed = 8;
                projectile.m_Damage = 1;
                projectile.m_Range = 50;
                Destroy(obj, 10);
            }
        }



    }
}
