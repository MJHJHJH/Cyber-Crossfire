using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace CommandoRobot.ScriptableObjects
{
    [CreateAssetMenu(fileName = "RewardData", menuName = "CustomObjects/RewardData", order = 1)]
    public class VideoRewardData : ScriptableObject
    {

        public string m_TitleEnglish = "coin";
        public string m_Type = "coin";
        public int m_Number = 0;
        public int m_Count = 10;
        public Sprite m_Icon;
        public bool m_Aquired = false;
        // Start is called before the first frame update
        void Start()
        {

        }

        // Update is called once per frame
        void Update()
        {

        }
    }
}