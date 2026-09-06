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
        [Tooltip("销毁音效的 sound 表 ID；>0 时子弹销毁(回收)位置播放，0/未填则不播")]
        public int m_DestroySoundId;
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
            // 安全保护：仅“出池激活且未回收”的子弹才推进。
            // 场景切换强制回收、命中回收后组件被禁用(enabled=false)或对象 inactive，
            // 同帧窗口内的 Update 一律直接返回，避免已回池实例继续移动/二次回收。
            if (m_IsRecycled || !isActiveAndEnabled)
                return;

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

            // 销毁时在销毁位置播放配置的销毁音效（未填则跳过）；
            // 场景切换的强制回收走 BulletPool.RecycleBullet 不经此路径，避免卸载时误播
            if (m_DestroySoundId > 0)
                GameFrameWork.Sound?.PlaySound(m_DestroySoundId, transform.position);

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

                // 爆炸粒子继承子弹阵营：玩家子弹的爆炸不伤玩家，敌人子弹的爆炸不伤敌人
                Explosion explosion = obj.GetComponent<Explosion>();
                if (explosion != null)
                    explosion.m_IsEnemyTeam = m_IsEnemyTeam;

                Destroy(obj, 3);
            }
        }
    }
}
