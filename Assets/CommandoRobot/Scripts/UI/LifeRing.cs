using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
namespace CommandoRobot
{
    public class LifeRing : MonoBehaviour
    {
        [HideInInspector]
        public DamageControl m_DamageControl;
        public Image m_MainRingImage;
        // Start is called before the first frame update
        void Start()
        {
            if (transform.parent != null)
            {
                m_DamageControl = transform.parent.GetComponent<DamageControl>();
            }
        }

        // Update is called once per frame
        void Update()
        {
            if (m_DamageControl != null)
            {
                float amount = m_DamageControl.Damage / m_DamageControl.MaxDamage;
                m_MainRingImage.fillAmount = Mathf.Lerp(m_MainRingImage.fillAmount, amount, 10 * Time.deltaTime);
            }
        }
    }
}