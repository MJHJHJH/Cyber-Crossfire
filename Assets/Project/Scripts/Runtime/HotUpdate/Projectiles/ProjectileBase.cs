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

        [HideInInspector]
        public float m_SafetyLifetime = 5f;
        [HideInInspector]
        public bool m_IsRecycled = false;

        float m_TimeAlive = 0f;

        void Update()
        {
            if (m_TurnSpeed != 0)
            {
                transform.forward = Quaternion.Euler(0, Time.deltaTime * m_TurnSpeed, 0) * transform.forward;
            }
            transform.position += m_Speed * Time.deltaTime * transform.forward;

            m_TimeAlive += Time.deltaTime;

            if (Vector3.Distance(m_StartPosition, transform.position) >= m_Range)
            {
                CreateHitParticle();
                Recycle();
                return;
            }

            CheckHit();

            if (m_TimeAlive >= m_SafetyLifetime)
            {
                Recycle();
            }
        }

        /// <summary>
        /// 出池重置：由 BulletPool.SpawnBullet 在返回调用方前调用。
        /// Start() 对池化对象只执行一次，运行态字段必须在每次出池时重新初始化。
        /// </summary>
        public void ResetForSpawn(Vector3 startPosition)
        {
            m_StartPosition = startPosition;
            m_TimeAlive = 0f;
            m_IsRecycled = false;
            m_Creator = null;

            // 复用实例的粒子子物体（particle-1）不会因 SetActive(true) 自动重播
            // （PlayOnAwake 仅首次激活触发），出池时手动重置并重播；
            // 已被 DetachOnHit 分离过的实例没有粒子子物体，自然跳过。
            ParticleSystem particle = GetComponentInChildren<ParticleSystem>(true);
            if (particle != null)
            {
                particle.Clear(true);
                particle.time = 0f;
                particle.Play(true);
            }
        }

        /// <summary>
        /// 入池清理：由 BulletPool.RecycleBullet 调用（幂等）。
        /// 已分离的脱离物（DetachOnHit 后留在世界）若为粒子系统——prefab 配置为
        /// looping + stopAction=None，永不自行销毁——回收时销毁，避免场景永久残留；
        /// 未分离的脱离物随子弹回池复用（下次出池由模板按子节点名重新挂接）。
        /// </summary>
        public void OnRecycle()
        {
            if (m_IsRecycled)
                return;

            m_IsRecycled = true;
            m_Creator = null;

            if (m_DetachObject != null)
            {
                if (m_DetachObject.transform.parent == null &&
                    m_DetachObject.GetComponent<ParticleSystem>() != null)
                {
                    Destroy(m_DetachObject);
                }

                m_DetachObject = null;
            }
        }

        void Recycle()
        {
            if (m_IsRecycled)
                return;

            BulletPool.RecycleBullet(gameObject);
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
                    Recycle();
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
                    Recycle();
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
                    Recycle();
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
