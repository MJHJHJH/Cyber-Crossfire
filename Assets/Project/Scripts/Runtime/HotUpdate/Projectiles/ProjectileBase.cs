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
            ResetVisualEffects();
        }

        /// <summary>
        /// 清理并重播拖尾/粒子。池化瞬移前若不清理，World 空间粒子与 TrailRenderer
        /// 会把旧落点与新出生点连成非子弹轨迹的假拖尾。
        /// </summary>
        public void ResetVisualEffects()
        {
            TrailRenderer[] trails = GetComponentsInChildren<TrailRenderer>(true);
            for (int i = 0; i < trails.Length; i++)
            {
                trails[i].Clear();
            }

            // 复用实例的粒子（如 particle-1：World + Prewarm + Looping）不会因 SetActive 自动正确复位
            ParticleSystem[] particles = GetComponentsInChildren<ParticleSystem>(true);
            for (int i = 0; i < particles.Length; i++)
            {
                ParticleSystem particle = particles[i];
                particle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                particle.Clear(true);
                particle.time = 0f;
                if (particle.gameObject.activeInHierarchy)
                {
                    particle.Play(true);
                }
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

            // 入池前清掉 World 粒子与 Trail 点，避免下次出池前残留可见
            TrailRenderer[] trails = GetComponentsInChildren<TrailRenderer>(true);
            for (int i = 0; i < trails.Length; i++)
            {
                trails[i].Clear();
            }

            ParticleSystem[] particles = GetComponentsInChildren<ParticleSystem>(true);
            for (int i = 0; i < particles.Length; i++)
            {
                particles[i].Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                particles[i].Clear(true);
            }

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
