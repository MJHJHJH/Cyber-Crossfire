using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace CommandoRobot
{
    public class PlayerPowers : MonoBehaviour
    {
        [HideInInspector]
        public bool m_HavePower = false;
        [HideInInspector]
        public int m_PowerNum = 0;
        [HideInInspector]
        public bool m_HasTimer = false;
        [HideInInspector]
        public float m_Timer = 0;
        [HideInInspector]
        public int m_AmmoCount = 0;
        // Start is called before the first frame update
        void Start()
        {
            m_PowerNum = -1;
            m_HavePower = false;
        }

        // Update is called once per frame
        void Update()
        {
            if (m_HavePower)
            {
                if (m_HasTimer)
                {
                    m_Timer -= Time.deltaTime;
                    if (m_Timer <= 0)
                    {
                        m_HavePower = false;
                        m_PowerNum = -1;
                    }
                }
                else
                {
                    if (m_AmmoCount <= 0)
                    {
                        m_HavePower = false;
                        m_PowerNum = -1;
                    }
                }
            }
        }

        public void SetNewPower(int num)
        {
            m_HavePower = true;
            switch (num)
            {
                case 0:
                    m_HasTimer = false;
                    m_AmmoCount = 3;
                    m_PowerNum = 0;
                    break;
            }
        }
    }
}
