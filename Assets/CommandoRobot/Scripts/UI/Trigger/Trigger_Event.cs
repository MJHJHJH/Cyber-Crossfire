using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
namespace CommandoRobot
{
    public class Trigger_Event : MonoBehaviour
    {
        public UnityEvent Event_OnEnter;
        void OnTriggerEnter(Collider coll)
        {
            if (coll.gameObject.tag == "Player")
            {
                print("trigger");
                Event_OnEnter.Invoke();
                //GetComponent<Collider>().enabled = false;
                gameObject.SetActive(false);
            }

        }
    }
}