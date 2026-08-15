using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace CommandoRobot
{
    public class CharacterBody : MonoBehaviour
    {

        public Transform m_RotationBase;
        public Transform m_UpperAimBase;
        public Transform[] m_GunPoints;
        public Transform[] m_RecoilBones;
        public bool m_AnimatedUpperBody = true;
        // Start is called before the first frame update
        void Start()
        {
            //AvatarMask mask = new AvatarMask();
            //mask.
        }

        // Update is called once per frame
        void Update()
        {

        }
    }
}