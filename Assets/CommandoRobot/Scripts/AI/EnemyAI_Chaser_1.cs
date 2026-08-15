using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace CommandoRobot
{
    public class EnemyAI_Chaser_1 : AIControlBase
    {
        // Start is called before the first frame update
        void Start()
        {

        }

        // Update is called once per frame
        void Update()
        {
            CheckAlert();

            if (m_IsAlerted)
            {
                EnemyCharacter enemyCharacter = GetComponent<EnemyCharacter>();
                Vector3 TargetPosition = PlayerCharacter.m_Current.transform.position;
                Vector3 toTarget = TargetPosition - transform.position;
                toTarget.y = 0;


                enemyCharacter.m_CharMovement.m_FaceTowardsMovement = true;
                CharacterBase characterBase = GetComponent<CharacterBase>();
                characterBase.m_MovementInput = toTarget.normalized;
            }
        }

        void OnCollisionEnter(Collision collision)
        {
            if (collision.gameObject == PlayerCharacter.m_Current.gameObject)
            {
                EnemyCharacter enemyCharacter = GetComponent<EnemyCharacter>();
                enemyCharacter.m_DamageControl.ApplyDamage(10, Vector3.forward, 1);
                PlayerCharacter.m_Current.m_DamageControl.ApplyDamage(2, Vector3.forward, 1);
                GetComponent<Collider>().enabled = false;
            }
        }
    }
}