using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace CommandoRobot
{
    public class LevelBase : MonoBehaviour
    {
        public Transform m_PlayerStartPoint;
        public Transform m_GeometryBase;
        // Start is called before the first frame update
        void Start()
        {

        }

        // Update is called once per frame
        void Update()
        {

        }

        public Transform FindGeometryBase()
        {
            Transform geometryBase = transform.Find("LevelGeometry");
            return geometryBase;
        }
    }
}