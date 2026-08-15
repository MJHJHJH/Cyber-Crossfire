using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace CommandoRobot
{
    public class StateSystem : MonoBehaviour
    {
        [HideInInspector]
        public int CurrentState = 0;
        [HideInInspector]
        public float StateTimer = 0;
        [HideInInspector]
        public float StateStartTime = 0;
        // Start is called before the first frame update
        void Start()
        {
            StateTimer = 0;
            //CurrentState = 0;
            StateStartTime = Time.time;
        }

        // Update is called once per frame
        void Update()
        {
            StateTimer += Time.deltaTime;
        }

        public void StartState(int newState)
        {
            CurrentState = newState;
            StateTimer = 0;
            StateStartTime = Time.time;
        }
    }
}
