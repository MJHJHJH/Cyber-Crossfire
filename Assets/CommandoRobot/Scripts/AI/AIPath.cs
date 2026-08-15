using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace CommandoRobot
{
    public class AIPath : MonoBehaviour
    {
        public AIPoint[] m_Points;

        public void FindPoints()
        {
            m_Points = GetComponentsInChildren<AIPoint>();

        }

        void OnDrawGizmos()
        {
            if (m_Points != null && m_Points.Length > 0)
            {
                //Gizmos.DrawLine(transform.position, m_Points[0].transform.position);
                Gizmos.color = Color.red;
                for (int i = 0; i < m_Points.Length - 1; i++)
                {
                    Gizmos.DrawLine(m_Points[i].transform.position, m_Points[i + 1].transform.position);
                }

                for (int i = 0; i < m_Points.Length; i++)
                {
                    Gizmos.DrawSphere(m_Points[i].transform.position, .4f);
                }
            }
        }
    }
}