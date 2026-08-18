using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace CommandoRobot
{
    public class AIControlBase : MonoBehaviour
    {
        [Header("Alert Properties")]
        public bool m_AlertAtStart;
        public bool m_AlertByPlayerInRange;
        public bool m_AlertByPlayerInSight;
        public bool m_LoseAlertByRange;
        public float m_AlertRange = 10.0f;
        public float m_LoseAlertRange = 15.0f;
        [Header("Other")]
        [HideInInspector]
        public bool m_IsAlerted = false;

        // Start is called before the first frame update
        void Start()
        {

        }

        // Update is called once per frame
        void Update()
        {

            CheckAlert();
        }

        public virtual void CheckAlert()
        {
            if (PlayerCharacter.m_Current == null)
                return;

            Vector3 dir = PlayerCharacter.m_Current.transform.position - transform.position;
            dir.y = 0;

            float distanceToPlayer = dir.magnitude;
            if (!m_IsAlerted)
            {
                if (m_AlertByPlayerInRange)
                {
                    if (distanceToPlayer <= m_AlertRange)
                    {
                        StartAlert();
                    }
                }
                else if (m_AlertByPlayerInSight)
                {
                    if (distanceToPlayer <= m_AlertRange)
                    {
                        StartAlert();
                    }
                }
                else
                {

                }
            }
            else
            {
                if (m_LoseAlertByRange)
                {
                    if (distanceToPlayer > m_LoseAlertRange)
                    {
                        EndAlert();
                    }
                }


            }
        }

        public virtual void StartAlert()
        {
            m_IsAlerted = true;
            EnemyIdlePatrol patrol = GetComponent<EnemyIdlePatrol>();
            if (patrol != null)
            {
                patrol.StopPatrol();
            }

            EnemyAttackPlayer attackPlayer = GetComponent<EnemyAttackPlayer>();
            if (attackPlayer != null)
            {
                attackPlayer.StartAttack();
            }
        }

        public virtual void UpdateAlert()
        {
            if (!m_IsAlerted)
                return;


        }

        public virtual void EndAlert()
        {
            m_IsAlerted = false;

            EnemyAttackPlayer attackPlayer = GetComponent<EnemyAttackPlayer>();
            if (attackPlayer != null)
            {
                attackPlayer.StopAttack();
            }
        }

        private void OnDrawGizmosSelected()
        {
            if (m_AlertByPlayerInRange)
            {
                Gizmos.DrawWireSphere(transform.position, m_AlertRange);
            }
        }

    }
}
