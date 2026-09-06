using System.Collections;
using UnityEngine;

namespace CommandoRobot
{
    /// <summary>
    /// 自动拾取物：先自由掉落落地，延迟后再自动飞向玩家，靠近即拾取。
    /// 复用 Pickup 的物品类型/数量/拾取回调（含 2D 拾取音效），仅改变“如何被拾取”的方式。
    /// </summary>
    public class AutoPickup : Pickup
    {
        [Header("Auto Pickup")]
        [Tooltip("先让物体自由掉落多久再开始飞向玩家(秒)，掉落物落地后再起飞")]
        public float m_FlyDelay = 1f;

        [Tooltip("飞向玩家的移动速度(米/秒)")]
        public float m_FlySpeed = 12f;

        [Tooltip("吸附触发范围：玩家进入该距离内才会飞向玩家(米)")]
        public float m_AttractRadius = 8f;

        private bool m_Flying;
        private bool m_Collected;

        // 隐藏基类 Start：不走“玩家靠近 4m 才触发”的旧逻辑，改为延迟后自动飞行
        private new void Start()
        {
            StartCoroutine(Co_AutoPick());
        }

        // 隐藏基类 Update：由协程全权接管，避免基类距离检测造成双重触发
        private new void Update()
        {
        }

        private IEnumerator Co_AutoPick()
        {
            if (m_FlyDelay > 0f)
                yield return new WaitForSeconds(m_FlyDelay);

            // 脚本接管位置：关闭物理，避免重力/碰撞干扰飞行轨迹
            Rigidbody body = GetComponent<Rigidbody>();
            if (body != null)
            {
                body.isKinematic = true;
                body.velocity = Vector3.zero;
            }

            Collider col = GetComponent<Collider>();
            if (col != null)
                col.enabled = false;

            m_Flying = true;

            while (!m_Collected)
            {
                PlayerCharacter player = PlayerCharacter.m_Current;

                // 容错：玩家为空 / 已死亡 / 未激活（角色隐藏或场景销毁）→ 停止并回收自身
                if (player == null || player.m_IsDead || !player.gameObject.activeInHierarchy)
                {
                    Destroy(gameObject);
                    yield break;
                }

                // 容错：游戏已结束 / 场景已销毁 → 停止回收，避免残留飞行
                GameControl gc = GameControl.m_Current;
                if (gc == null || !gc.IsGamePlaying)
                {
                    Destroy(gameObject);
                    yield break;
                }

                // 落地后原地等待，玩家进入吸附范围才飞向玩家
                if (Vector3.Distance(transform.position, player.transform.position) > m_AttractRadius)
                {
                    yield return null;
                    continue;
                }

                Vector3 target = player.transform.position + new Vector3(0f, 1.5f, 0f);
                Vector3 offset = target - transform.position;
                float step = m_FlySpeed * Time.deltaTime;

                if (offset.magnitude <= step)
                {
                    transform.position = target;
                    Collect(player);
                    yield break;
                }

                transform.position += offset.normalized * step;
                yield return null;
            }
        }

        private void Collect(PlayerCharacter player)
        {
            m_Collected = true;
            m_TouchedPlayer = player;
            HandlePickup();

            if (m_ScoreParticle != null)
            {
                GameObject obj = Instantiate(m_ScoreParticle);
                obj.transform.position = transform.position;
                Destroy(obj, 1f);
            }

            Destroy(gameObject);
        }
    }
}
