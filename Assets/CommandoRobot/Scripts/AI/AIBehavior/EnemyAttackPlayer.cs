using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace CommandoRobot
{
    public class EnemyAttackPlayer : MonoBehaviour
    {
        [HideInInspector]
        public bool m_AttackEnabled = false;
        public float m_MaxAttackRange = 10;
        public Coroutine m_AttackCoroutine;

        public float m_AttackDuration = 5;
        public float m_AttackWaitDuration = 5;

        public bool m_CanAttack = true;
        // Start is called before the first frame update
        void Start()
        {
            m_AttackEnabled = false;
            StartCoroutine(Co_ShootLoop());
        }

        // Update is called once per frame
        void Update()
        {
            if (m_AttackEnabled)
            {
                EnemyCharacter enemyCharacter = GetComponent<EnemyCharacter>();

                Vector3 TargetPosition = PlayerCharacter.m_Current.transform.position;
                Vector3 toTarget = TargetPosition - transform.position;
                toTarget.y = 0;

                if (toTarget.magnitude > m_MaxAttackRange)
                {
                    enemyCharacter.m_CharMovement.m_FaceTowardsMovement = true;
                    CharacterBase characterBase = GetComponent<CharacterBase>();
                    characterBase.m_MovementInput = toTarget.normalized;
                }
                else
                {
                    enemyCharacter.m_CharMovement.m_FaceTowardsMovement = false;
                    enemyCharacter.m_RotationTargetPosition = TargetPosition;
                    if (enemyCharacter.m_CurrentWeapon != null)
                    {
                        if (m_CanAttack)
                        {
                            enemyCharacter.m_CurrentWeapon.Input_FireHold = true;
                        }
                        else
                        {
                            enemyCharacter.m_CurrentWeapon.Input_FireHold = false;
                        }
                    }
                }

            }
        }

        public void StartAttack()
        {
            m_AttackEnabled = true;
            if (m_AttackCoroutine != null)
            {
                StopCoroutine(m_AttackCoroutine);
                m_AttackCoroutine = null;
            }
        }

        public void StopAttack()
        {
            EnemyCharacter enemyCharacter = GetComponent<EnemyCharacter>();
            if (enemyCharacter != null && enemyCharacter.m_CurrentWeapon != null)
            {
                enemyCharacter.m_CurrentWeapon.Input_FireHold = true;
            }
            m_AttackEnabled = false;
            if (m_AttackCoroutine != null)
            {
                StopCoroutine(m_AttackCoroutine);
                m_AttackCoroutine = null;
            }
        }

        IEnumerator Co_ShootLoop()
        {
            while (true)
            {
                yield return new WaitForSeconds(1);

                m_CanAttack = true;

                yield return new WaitForSeconds(m_AttackDuration);

                m_CanAttack = false;

                yield return new WaitForSeconds(m_AttackWaitDuration);
            }
        }
    }
}