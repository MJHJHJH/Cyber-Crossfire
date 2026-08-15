using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace CommandoRobot
{
    public class RandomShake : MonoBehaviour
    {
        // Start is called before the first frame update
        void Start()
        {

        }

        // Update is called once per frame
        void Update()
        {
            Vector3 shake = Vector3.zero;
            shake.x = .2f * Mathf.Cos(Time.time);
            shake.y = .2f * Mathf.Sin(1.5f * Time.time + .6f);
            shake.z = .1f * Mathf.Cos(2 * Time.time + 1);

            transform.localPosition = shake;
        }
    }
}