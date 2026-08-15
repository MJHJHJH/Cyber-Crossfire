using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace CommandoRobot
{
    public class FadeControl : MonoBehaviour
    {
        public static FadeControl m_Current;
        public Image m_FadeImage;

        void Awake()
        {
            m_Current = this;
        }
        // Start is called before the first frame update
        void Start()
        {
            m_FadeImage.gameObject.SetActive(false);

            StartFadeIn();
        }

        // Update is called once per frame
        void Update()
        {

        }

        public void StartFadeOut()
        {
            StartCoroutine(Co_StartFadeOut());
        }

        public void StartFadeIn()
        {
            StartCoroutine(Co_StartFadeIn());
        }

        IEnumerator Co_StartFadeOut()
        {
            m_FadeImage.gameObject.SetActive(true);
            float fade = 0;
            while (fade < 1f)
            {
                fade += .5f * Time.deltaTime;
                Color c = Color.black;
                c.a = fade;
                m_FadeImage.color = c;
                yield return null;
            }

            m_FadeImage.color = Color.black;
        }
        IEnumerator Co_StartFadeIn()
        {
            m_FadeImage.gameObject.SetActive(true);
            m_FadeImage.color = Color.black;
            float fade = 1;
            while (fade > 0f)
            {
                fade -= .5f * Time.deltaTime;
                Color c = Color.black;
                c.a = fade;
                m_FadeImage.color = c;
                yield return null;
            }

            m_FadeImage.gameObject.SetActive(false);
        }

    }
}