using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace CommandoRobot
{
    public class DestroyObject : MonoBehaviour
    {
        public float m_Delay = .5f;
        // Start is called before the first frame update
        void Start()
        {
            Destroy(gameObject, m_Delay);
        }

        // Update is called once per frame
        void Update()
        {

        }
    }
}