using UnityEngine;

namespace GameFramework
{
    /// <summary>
    /// 场景 3D 音源：挂到 GameObject 上，配置音效 ID，启用即播放并跟随自身 Transform。
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Game Framework/Sound Source")]
    public sealed class SoundSourceBehaviour : MonoBehaviour
    {
        [SerializeField]
        private int m_SoundId;

        [SerializeField]
        private bool m_PlayOnEnable = true;

        public int SoundId
        {
            get => m_SoundId;
            set => m_SoundId = value;
        }

        private void OnEnable()
        {
            if (m_PlayOnEnable)
            {
                Play();
            }
        }

        /// <summary>
        /// 按配表 ID 播放并绑定自身 Transform，返回声音序列编号。
        /// </summary>
        public int Play()
        {
            SoundComponent soundComponent = GameEntry.GetComponent<SoundComponent>();
            if (soundComponent == null || !soundComponent.IsReady)
            {
                Debug.LogWarning("SoundComponent is not ready.");
                return -1;
            }

            if (m_SoundId <= 0)
            {
                Debug.LogWarningFormat("SoundSourceBehaviour sound id '{0}' is invalid.", m_SoundId);
                return -1;
            }

            return soundComponent.PlaySound(m_SoundId, transform);
        }
    }
}
