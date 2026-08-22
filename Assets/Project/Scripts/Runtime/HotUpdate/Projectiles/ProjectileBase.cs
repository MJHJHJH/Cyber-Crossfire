using System.Collections;
using System.Collections.Generic;
using GameFramework;
using GamePlay;
using UnityEngine;

namespace CommandoRobot
{
    public class ProjectileBase : MonoBehaviour
    {
        public GameObject m_HitParticle;
        [HideInInspector]
        public GameObject m_Creator;

        public float m_Speed = 10;
        public float m_Damage = 1;
        public float m_Radius = .2f;
        public float m_Range = 10;
        public float m_TurnSpeed = 0;
        Vector3 m_StartPosition;
        public bool m_IsEnemyTeam = true;

        public GameObject m_DetachObject;
        // Use this for initialization
        void Start()
        {
            m_StartPosition = transform.position;
        }

        void Update()
        {
            if (m_TurnSpeed != 0)
            {
                transform.forward = Quaternion.Euler(0, Time.deltaTime * m_TurnSpeed, 0) * transform.forward;
            }
            transform.position += m_Speed * Time.deltaTime * transform.forward;

            if (Vector3.Distance(m_StartPosition, transform.position) >= m_Range)
            {
                CreateHitParticle();
                Destroy(gameObject);
                return;
            }

            CheckHit();
        }

        public void CheckHit()
        {
            Collider[] hits = Physics.OverlapSphere(transform.position, m_Radius);
            foreach (Collider col in hits)
            {
                if (col.gameObject == m_Creator)
                    continue;

                if (col.gameObject.tag == "Player" && m_IsEnemyTeam)
                {
                    DamageControl d = col.gameObject.GetComponent<DamageControl>();
                    if (d != null)
                    {
                        d.ApplyDamage(m_Damage, transform.forward, 1);
                    }

                    // Player Projectile Hit：敌方子弹命中玩家时播放；子弹销毁本身不播音效
                    GameFrameWork.Sound?.PlaySound(SoundIds.PlayerHit, col.transform);

                    CreateHitParticle();
                    Destroy(gameObject);
                }
                else if (col.gameObject.tag == "Block")
                {
                    DamageControl d = col.gameObject.GetComponent<DamageControl>();
                    if (d != null)
                    {
                        d.ApplyDamage(m_Damage, transform.forward, 1);
                    }
                    CreateHitParticle();
                    DetachOnHit();
                    Destroy(gameObject);
                }
                else if (col.gameObject.tag == "Enemy" && !m_IsEnemyTeam)
                {
                    DamageControl d = col.gameObject.GetComponent<DamageControl>();
                    if (d != null)
                    {
                        d.ApplyDamage(m_Damage, transform.forward, 1);
                    }
                    CreateHitParticle();
                    DetachOnHit();
                    Destroy(gameObject);
                }

            }
        }

        public void DetachOnHit()
        {
            if (m_DetachObject != null)
                m_DetachObject.transform.SetParent(null, true);
        }
        public void CreateHitParticle()
        {
            if (m_HitParticle != null)
            {
                GameObject obj = Instantiate(m_HitParticle);
                obj.transform.position = transform.position;
                Destroy(obj, 3);
            }
        }
    }
}