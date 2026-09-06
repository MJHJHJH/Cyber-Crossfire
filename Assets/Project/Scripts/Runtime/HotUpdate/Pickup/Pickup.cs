using System.Collections;
using System.Collections.Generic;
using GameFramework;
using UnityEngine;

namespace CommandoRobot
{
    public class Pickup : MonoBehaviour
    {
        public GameObject m_ScoreParticle;
        [Tooltip("拾取成功音效的 sound 表 ID；>0 时以 2D 播放，0/未填则不播")]
        public int m_PickupSoundId;
        bool picked = false;

        [HideInInspector]
        public PlayerCharacter m_TouchedPlayer;

        [Space]
        public string m_ItemType = "Cash";
        public int m_ItemCount = 1;

        public bool m_CanPick = false;

        public string[] m_WeaponNames;
        // Start is called before the first frame update
        void Start()
        {
            m_CanPick = false;
            Invoke("AllowPick", .5f);

            if (m_ItemType == "Weapon")
            {
                int rand = Random.Range(0, 6);
                m_ItemType = m_WeaponNames[rand];
            }
        }

        // Update is called once per frame
        void Update()
        {
            //m_Base.localRotation = Quaternion.Euler(0, Time.deltaTime * 100, 0) * m_Base.localRotation;
            if (!picked && m_CanPick)
            {
                if (Vector3.Distance(transform.position, PlayerCharacter.m_Current.transform.position) <= 4f)
                {
                    m_TouchedPlayer = PlayerCharacter.m_Current;
                    picked = true;
                    Collider m_PhysCollider = GetComponent<Collider>();
                    if (m_PhysCollider != null)
                    {
                        m_PhysCollider.enabled = false;
                    }

                    Rigidbody rigidbody = GetComponent<Rigidbody>();
                    if (rigidbody != null)
                    {
                        rigidbody.isKinematic = true;
                    }
                    StartCoroutine(Co_HandlePick());
                }
            }

        }


        public virtual void HandlePickup()
        {
            m_TouchedPlayer.HandlePickup(m_ItemType, m_ItemCount);
            PlayPickupSound();
        }

        /// <summary>
        /// 拾取成功音效：以 2D 方式播放（不绑定位置/Transform），未配置则不播。
        /// </summary>
        protected void PlayPickupSound()
        {
            if (m_PickupSoundId > 0)
                GameFrameWork.Sound?.PlaySound(m_PickupSoundId);
        }

        IEnumerator Co_HandlePick()
        {
            Vector3 startPos = transform.position;
            float lerp = 0;
            while (lerp <= 1)
            {
                transform.position = Vector3.Lerp(startPos, m_TouchedPlayer.transform.position + new Vector3(0, 1.5f, 0), lerp);
                lerp += 10 * Time.deltaTime;
                yield return null;
            }
            transform.position = m_TouchedPlayer.transform.position + new Vector3(0, 1.5f, 0);
            HandlePickup();

            GameObject obj = Instantiate(m_ScoreParticle);
            obj.transform.position = transform.position;
            Destroy(obj, 1);

            Destroy(gameObject);

        }

        private void AllowPick()
        {
            m_CanPick = true;
        }
    }
}
