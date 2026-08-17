using UnityEngine;
using System.Collections;
namespace Rostam
{
    public class MoveMaterial : MonoBehaviour
    {

        public Vector2 delta;
        Vector2 m_InitSize;
        // Use this for initialization
        void Start()
        {
            Material mat = GetComponent<Renderer>().material;
            m_InitSize = mat.mainTextureScale;
        }

        // Update is called once per frame
        void Update()
        {
            Material mat = GetComponent<Renderer>().material;
            mat.mainTextureOffset += Time.deltaTime * delta;
            //mat.mainTextureScale = m_InitSize + .02f*Mathf.Sin(Time.time) * Vector2.one;
        }
    }
}