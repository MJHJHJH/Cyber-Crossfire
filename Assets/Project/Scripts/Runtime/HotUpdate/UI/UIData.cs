using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace CommandoRobot
{
    [CreateAssetMenu(fileName = "UIData", menuName = "CustomObjects/UIData", order = 1)]
    public class UIData : ScriptableObject
    {
        public GameObject[] m_UIPrefabs;
        public Dictionary<string, GameObject> m_UIPrefabList;

    }
}