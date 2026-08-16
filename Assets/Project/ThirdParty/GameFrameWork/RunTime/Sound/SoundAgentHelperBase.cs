using GameFramework.Sound;
using System;
using UnityEngine;
using UnityEngine.Audio;

namespace GameFramework
{
    /// <summary>
    /// 声音代理辅助器基类。
    /// </summary>
    public abstract class SoundAgentHelperBase : MonoBehaviour, ISoundAgentHelper
    {
        public abstract bool IsPlaying { get; }
        public abstract float Length { get; }
        public abstract float Time { get; set; }
        public abstract bool Mute { get; set; }
        public abstract bool Loop { get; set; }
        public abstract int Priority { get; set; }
        public abstract float Volume { get; set; }
        public abstract float Pitch { get; set; }
        public abstract float PanStereo { get; set; }
        public abstract float SpatialBlend { get; set; }
        public abstract float MaxDistance { get; set; }
        public abstract float DopplerLevel { get; set; }

        /// <summary>
        /// 获取或设置声音代理辅助器所在的混音组。
        /// </summary>
        public abstract AudioMixerGroup AudioMixerGroup { get; set; }

        public abstract event EventHandler<ResetSoundAgentEventArgs> ResetSoundAgent;

        public abstract void Play(float fadeInSeconds);
        public abstract void Stop(float fadeOutSeconds);
        public abstract void Pause(float fadeOutSeconds);
        public abstract void Resume(float fadeInSeconds);
        public abstract void Reset();
        public abstract bool SetSoundAsset(object soundAsset);

        /// <summary>
        /// 设置声音绑定的目标（跟随其位置），替换 Refresh 的实体绑定。
        /// </summary>
        public abstract void SetBindingTransform(Transform bindingTransform);

        /// <summary>
        /// 设置声音所在的世界坐标（静止）。
        /// </summary>
        public abstract void SetWorldPosition(Vector3 worldPosition);
    }
}
