using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace CommandoRobot
{
    public class HitGlowControl : MonoBehaviour
    {
        [HideInInspector]
        public Renderer m_MainRenderer;
        [HideInInspector]
        public Material m_OriginalMat;
        public Material m_GlowMaterial;
        // Start is called before the first frame update
        void Start()
        {
            m_MainRenderer = GetComponent<Renderer>();
            m_OriginalMat = m_MainRenderer.material;
        }

        // Update is called once per frame
        void Update()
        {

        }

        public void SetOriginal()
        {
            m_MainRenderer.material = m_OriginalMat;
        }

        public void SetGlow()
        {
            m_MainRenderer.material = m_GlowMaterial;
        }
    }
}