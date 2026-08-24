using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace CommandoRobot
{
    public class EnemyAI_RingShooter : AIControlBase
    {
        public GameObject m_BulletPrefab1;
        // Start is called before the first frame update
        void Start()
        {

        }

        // Update is called once per frame
        void Update()
        {
            CheckAlert();

            if (m_IsAlerted && PlayerCharacter.m_Current != null)
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
                GetComponent<CharacterBase>().PlayFireAnimation();
                ShootRingBullet();
                yield return new WaitForSeconds(3);
            }
        }

        public void ShootRingBullet()
        {
            for (int i = 0; i < 8; i++)
            {
                GameObject obj = BulletPool.SpawnBullet(m_BulletPrefab1,
                    transform.position + new Vector3(0, 1, 0),
                    Quaternion.Euler(0, i * 45, 0) * Vector3.forward,
                    10f);
                if (obj == null)
                    continue;

                ProjectileBase projectile = obj.GetComponent<ProjectileBase>();
                projectile.m_Creator = gameObject;
                projectile.m_Speed = 6;
                projectile.m_Damage = 1;
                projectile.m_Range = 50;
            }
        }
    }
}
