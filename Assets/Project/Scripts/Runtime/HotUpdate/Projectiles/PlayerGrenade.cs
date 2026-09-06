using System.Collections;
using GameFramework;
using UnityEngine;
namespace CommandoRobot
{
    public class PlayerGrenade : MonoBehaviour
    {
        [HideInInspector]
        public Vector3 m_StartPosition;
        [HideInInspector]
        public Vector3 m_TargetPosition;

        [Header("抛物线")]
        [Tooltip("基准最大高度(米)，近距离投掷的基础弧高")]
        public float m_ArcHeight = 3f;
        [Tooltip("高度随距离增长系数：峰值高度 = clamp(基准 + 距离*系数, 下限, 上限)，越远抛得越高")]
        public float m_HeightPerMeter = 0.15f;
        public float m_MinPeakHeight = 1f;
        public float m_MaxPeakHeight = 30f;
        [Tooltip("水平飞行速度(米/秒)，决定飞行时长 = 距离/速度；越近越快、越远越久")]
        public float m_ThrowSpeed = 16f;
        [Tooltip("飞行时长上限(秒)：距离过远时封顶该时长，等效给远距离保底速度，避免慢慢悠悠落地")]
        public float m_MaxFlightTime = 1f;
        [Tooltip("到达最高点的时间比例(0.51~0.95)。0.5 为完全对称；>0.5 时上升占时长更多(慢)、下落更短更快")]
        [Range(0.51f, 0.95f)]
        public float m_PeakTime = 0.6f;

        public GameObject m_ExplodeParticle;

        float m_PeakHeight;
        float m_Duration;

        // Start is called before the first frame update
        void Start()
        {
            // 依据落点距离计算本次实际峰值高度（越远越高，受上下限约束）
            Vector3 offset = m_TargetPosition - m_StartPosition;
            offset.y = 0;
            float distance = Mathf.Max(offset.magnitude, 0.01f);

            m_PeakHeight = Mathf.Clamp(m_ArcHeight + distance * m_HeightPerMeter,
                m_MinPeakHeight, m_MaxPeakHeight);

            // 水平等速：飞行时长随距离增大而增大（近距离不再慢动作）；
            // 超过上限后时长封顶，等效远距离保底速度，不会出现非常缓慢的下落
            float flight = distance / Mathf.Max(m_ThrowSpeed, 0.01f);
            m_Duration = Mathf.Clamp(flight, 0.05f, m_MaxFlightTime);
            GameFrameWork.Sound?.PlaySound(2207, m_StartPosition);
            StartCoroutine(Co_Move());
        }

        IEnumerator Co_Move()
        {
            float elapsed = 0;
            while (elapsed < m_Duration)
            {
                // 安全保护：玩法结束/场景退出（GameControl 失效或进入胜负结算）时直接回收，
                // 避免协程继续访问已失效的引用或让手雷滞留场景
                GameControl gc = GameControl.m_Current;
                if (gc == null || gc.m_GameState != GameControl.State_Gameplay)
                {
                    Destroy(gameObject);
                    yield break;
                }

                // 水平等速推进：总时长 = 距离/速度
                elapsed += Time.deltaTime;
                UpdatePosition(Mathf.Min(elapsed / m_Duration, 1f));
                yield return null;
            }

            transform.position = m_TargetPosition;
            //explode
            // 安全保护：场景退出瞬间 CameraControl 可能已被销毁，直接回收
            if (CameraControl.m_Current == null || m_ExplodeParticle == null)
            {
                Destroy(gameObject);
                yield break;
            }
            CameraControl.m_Current.PlayShake(
                CameraShakePreset.Fire,
                ShakeSettings.Position(0.28f, 0.3f, 30f));
            GameObject obj = Instantiate(m_ExplodeParticle);
            obj.transform.position = transform.position;
            GameFrameWork.Sound?.PlaySound(2212, m_TargetPosition);
            Destroy(obj, 3);
            Destroy(gameObject);
        }

        void UpdatePosition(float t)
        {
            // 起点→落点线性插值 + 平滑抛物线高度（顶点时间后移：上升慢、下落快）
            Vector3 pos = Vector3.Lerp(m_StartPosition, m_TargetPosition, t);
            pos.y += m_PeakHeight * ArcHeightWeight(t);
            transform.position = pos;
        }

        /// <summary>
        /// 两段正弦拼成的平滑弧线高度权重：0→1(顶点)→0。
        /// 顶点位于 m_PeakTime；两侧顶点处切线为 0（圆润），落地处速度有限（无跳变）。
        /// </summary>
        float ArcHeightWeight(float t)
        {
            float tp = Mathf.Clamp(m_PeakTime, 0.51f, 0.95f);
            if (t < tp)
                return Mathf.Sin((t / tp) * Mathf.PI * 0.5f);
            float r = (t - tp) / (1f - tp); // 下降段 0→1
            return Mathf.Cos(r * Mathf.PI * 0.5f);
        }
    }
}
