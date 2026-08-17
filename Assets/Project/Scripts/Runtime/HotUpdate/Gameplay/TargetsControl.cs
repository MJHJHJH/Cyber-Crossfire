using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace CommandoRobot
{
    public class TargetsControl : MonoBehaviour
    {
        public static TargetsControl m_Main;
        [HideInInspector]
        public List<TargetObject> m_Targets;

        void Awake()
        {
            m_Main = this;
            m_Targets = new List<TargetObject>();
        }
        // Start is called before the first frame update
        void Start()
        {

        }

        // Update is called once per frame
        void Update()
        {

        }

        public void AddTarget(TargetObject obj)
        {
            m_Targets.Add(obj);
        }

        public void RemoveTarget(TargetObject obj)
        {
            m_Targets.Remove(obj);
        }
    }
}
