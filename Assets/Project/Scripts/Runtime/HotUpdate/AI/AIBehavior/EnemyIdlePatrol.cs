using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace CommandoRobot
{
    public class EnemyIdlePatrol : MonoBehaviour
    {
        public AIPath m_Path;
        private int m_NextWaypointIndex = 0;
        [HideInInspector]
        public Vector3 m_NextPointPosition;
        [HideInInspector]
        public bool m_FollowPath = false;
        [HideInInspector]
        public bool m_IsWating = false;
        [HideInInspector]
        public CharacterMovement m_Movement;
        // Start is called before the first frame update
        void Start()
        {
            m_Movement = GetComponent<CharacterMovement>();

            if (m_Path == null)
            {
                m_Path = GetComponentInChildren<AIPath>();
            }

            if (m_Path != null)
            {
                m_Path.transform.SetParent(null);
                m_NextWaypointIndex = 0;
                m_NextPointPosition = m_Path.m_Points[m_NextWaypointIndex].transform.position;
                m_Movement.SetAutoMovement(m_NextPointPosition);
                m_FollowPath = true;
                m_Movement.m_FaceTowardsMovement = true;
            }
            else
            {
                m_FollowPath = false;
            }

        }

        // Update is called once per frame
        void Update()
        {
            if (m_FollowPath)
            {
                if (!m_IsWating)
                {
                    if (ReachedTarget(m_NextPointPosition))
                    {
                        m_Movement.StopAutoMovement();
                        m_IsWating = true;
                        Invoke("ContinueAfterWait", 2);

                    }
                }
            }
        }

        public bool ReachedTarget(Vector3 target)
        {
            Vector3 dir;
            dir = target - transform.position;
            dir.y = 0;
            if (dir.magnitude <= .5f)
            {
                return true;
            }
            return false;
        }

        public void ContinueAfterWait()
        {
            if (!m_FollowPath)
                return;

            m_IsWating = false;
            m_NextWaypointIndex++;
            if (m_NextWaypointIndex > m_Path.m_Points.Length - 1)
            {
                m_NextWaypointIndex = 0;
            }
            m_NextPointPosition = m_Path.m_Points[m_NextWaypointIndex].transform.position;
            m_Movement.SetAutoMovement(m_NextPointPosition);
        }
        public void StopPatrol()
        {
            m_FollowPath = false;
            m_Movement.StopAutoMovement();
        }

        void OnDrawGizmos()
        {
            if (m_Path != null)
            {
                Gizmos.DrawLine(transform.position, m_Path.m_Points[0].transform.position);
            }

        }

    }
}