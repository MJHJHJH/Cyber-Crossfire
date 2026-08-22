using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace CommandoRobot
{
    public class EnemyBossA : EnemyCharacter
    {
        public override void HandleDeath()
        {
            PlayDeathSound();

            GameControl.m_Current.m_EnemyKilledCount++;
            StartCoroutine(Co_HandleDeath());
        }

        IEnumerator Co_HandleDeath()
        {
            float delay = .2f;
            for (int i = 0; i < 10; i++)
            {
                GameObject obj = Instantiate(m_DeathParticle);

                obj.transform.position = transform.position + new Vector3(0, 2, 0) + 4 * Random.insideUnitSphere;
                Destroy(obj, 5);
                yield return new WaitForSeconds(delay);
                delay -= .01f;
            }

            yield return new WaitForSeconds(.5f);

            GameObject obj1 = Instantiate(m_DeathParticle);
            obj1.transform.position = transform.position;
            Destroy(obj1, 6);

            DropItem(m_ItemDropCount);
            CameraControl.m_Current.StartShake(.6f, .2f);
            //DropItem(20);

            Destroy(gameObject);
        }

    }
}
