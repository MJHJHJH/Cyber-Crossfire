using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace CommandoRobot.ScriptableObjects
{
    [CreateAssetMenu(fileName = "Contents", menuName = "CustomObjects/Contents", order = 1)]
    public class Contents : ScriptableObject
    {
        public GameObject[] m_PlayerWeapons;

    }
}
