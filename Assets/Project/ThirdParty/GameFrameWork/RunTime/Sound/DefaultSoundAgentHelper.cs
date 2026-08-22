using GameFramework.Sound;
using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Audio;

namespace GameFramework
{
    /// <summary>
    /// 默认声音代理辅助器：封装 AudioSource，支持淡变、3D 绑定与自然结束检测。
    /// </summary>
    public class DefaultSoundAgentHelper : SoundAgentHelperBase
    {
        private AudioSource m_AudioSource;
        private AudioClip m_AudioClip;
        private AudioMixerGroup m_CachedMixerGroup;
        private float m_Volume = 1f;
        private bool m_IsPlayFinished;
        private bool m_IsPaused;
        private bool m_IsFadingOut;
        private bool m_HasBindingTransform;
        private Transform m_BindingTransform;
        private bool m_HasWorldPosition;
        private Vector3 m_WorldPosition;
        private Coroutine m_FadeCoroutine;

        protected virtual void Awake()
        {
            m_AudioSource = GetComponent<AudioSource>();
            if (m_AudioSource == null)
            {
                m_AudioSource = gameObject.AddComponent<AudioSource>();
                m_AudioSource.playOnAwake = false;
                m_AudioSource.rolloffMode = AudioRolloffMode.Custom;
                m_AudioSource.SetCustomCurve(AudioSourceCurveType.CustomRolloff, AnimationCurve.Linear(0f, 1f, 1f, 0f));
            }
        }

        protected virtual void Update()
        {
            if (m_HasBindingTransform)
            {
                if (m_BindingTransform == null)
                {
                    m_IsPlayFinished = true;
                    OnResetSoundAgent(this, ResetSoundAgentEventArgs.Create());
                    return;
                }

                transform.position = m_BindingTransform.position;
            }
            else if (m_HasWorldPosition)
            {
                transform.position = m_WorldPosition;
            }

            if (m_AudioClip != null && !m_IsPlayFinished && !m_IsPaused && !m_IsFadingOut && !m_AudioSource.isPlaying)
            {
                m_IsPlayFinished = true;
                OnResetSoundAgent(this, ResetSoundAgentEventArgs.Create());
            }
        }

        public override bool IsPlaying => m_AudioSource.isPlaying;

        public override float Length => m_AudioClip != null ? m_AudioClip.length : 0f;

        public override float Time
        {
            get => m_AudioSource.time;
            set => m_AudioSource.time = value;
        }

        public override bool Mute
        {
            get => m_AudioSource.mute;
            set => m_AudioSource.mute = value;
        }

        public override bool Loop
        {
            get => m_AudioSource.loop;
            set => m_AudioSource.loop = value;
        }

        public override int Priority
        {
            get => m_AudioSource.priority;
            set => m_AudioSource.priority = value;
        }

        public override float Volume
        {
            get => m_Volume;
            set
            {
                m_Volume = value;
                m_AudioSource.volume = value;
            }
        }

        public override float Pitch
        {
            get => m_AudioSource.pitch;
            set => m_AudioSource.pitch = value;
        }

        public override float PanStereo
        {
            get => m_AudioSource.panStereo;
            set => m_AudioSource.panStereo = value;
        }

        public override float SpatialBlend
        {
            get => m_AudioSource.spatialBlend;
            set => m_AudioSource.spatialBlend = value;
        }

        public override float MaxDistance
        {
            get => m_AudioSource.maxDistance;
            set => m_AudioSource.maxDistance = value;
        }

        public override float DopplerLevel
        {
            get => m_AudioSource.dopplerLevel;
            set => m_AudioSource.dopplerLevel = value;
        }

        public override AudioMixerGroup AudioMixerGroup
        {
            get => m_AudioSource.outputAudioMixerGroup;
            set
            {
                m_CachedMixerGroup = value;
                m_AudioSource.outputAudioMixerGroup = value;
            }
        }

        public override event EventHandler<ResetSoundAgentEventArgs> ResetSoundAgent;

        public override void Play(float fadeInSeconds)
        {
            StopAllCoroutines();
            m_IsPlayFinished = false;
            m_IsPaused = false;
            m_IsFadingOut = false;

            if (fadeInSeconds <= 0f)
            {
                m_AudioSource.volume = m_Volume;
                m_AudioSource.Play();
                return;
            }

            m_AudioSource.volume = 0f;
            m_AudioSource.Play();
            m_FadeCoroutine = StartCoroutine(FadeToVolume(m_Volume, fadeInSeconds));
        }

        public override void Stop(float fadeOutSeconds)
        {
            StopAllCoroutines();

            if (m_IsPlayFinished || fadeOutSeconds <= 0f || !m_AudioSource.isPlaying)
            {
                m_AudioSource.Stop();
                m_IsPaused = false;
                m_IsFadingOut = false;
                m_IsPlayFinished = true;
                OnResetSoundAgent(this, ResetSoundAgentEventArgs.Create());
                return;
            }

            m_IsFadingOut = true;
            m_FadeCoroutine = StartCoroutine(FadeOutAndStop(fadeOutSeconds));
        }

        public override void Pause(float fadeOutSeconds)
        {
            StopAllCoroutines();

            if (m_IsPlayFinished)
            {
                return;
            }

            if (fadeOutSeconds <= 0f || !m_AudioSource.isPlaying)
            {
                m_AudioSource.Pause();
                m_IsPaused = true;
                return;
            }

            m_FadeCoroutine = StartCoroutine(FadeOutThenPause(fadeOutSeconds));
        }

        public override void Resume(float fadeInSeconds)
        {
            StopAllCoroutines();

            if (m_IsPlayFinished)
            {
                return;
            }

            m_AudioSource.UnPause();
            m_IsPaused = false;

            if (fadeInSeconds > 0f)
            {
                m_AudioSource.volume = 0f;
                m_FadeCoroutine = StartCoroutine(FadeToVolume(m_Volume, fadeInSeconds));
            }
            else
            {
                m_AudioSource.volume = m_Volume;
            }
        }

        public override void Reset()
        {
            StopAllCoroutines();
            m_AudioClip = null;
            m_Volume = 1f;
            m_IsPlayFinished = false;
            m_IsPaused = false;
            m_IsFadingOut = false;
            m_HasBindingTransform = false;
            m_BindingTransform = null;
            m_HasWorldPosition = false;
            m_FadeCoroutine = null;

            if (m_AudioSource != null)
            {
                m_AudioSource.Stop();
                m_AudioSource.clip = null;
                m_AudioSource.mute = false;
                m_AudioSource.loop = false;
                m_AudioSource.priority = 128;
                m_AudioSource.volume = 1f;
                m_AudioSource.pitch = 1f;
                m_AudioSource.panStereo = 0f;
                m_AudioSource.spatialBlend = 0f;
                m_AudioSource.maxDistance = 100f;
                m_AudioSource.dopplerLevel = 1f;
                m_AudioSource.outputAudioMixerGroup = m_CachedMixerGroup;
            }

            transform.position = Vector3.zero;
        }

        public override bool SetSoundAsset(object soundAsset)
        {
            AudioClip audioClip = soundAsset as AudioClip;
            if (audioClip == null)
            {
                return false;
            }

            m_AudioClip = audioClip;
            m_AudioSource.clip = audioClip;
            m_IsPlayFinished = false;
            return true;
        }

        public override void SetBindingTransform(Transform bindingTransform)
        {
            m_BindingTransform = bindingTransform;
            m_HasBindingTransform = bindingTransform != null;
            m_HasWorldPosition = false;
        }

        public override void SetWorldPosition(Vector3 worldPosition)
        {
            m_WorldPosition = worldPosition;
            m_HasWorldPosition = true;
            m_HasBindingTransform = false;
            m_BindingTransform = null;
        }

        private IEnumerator FadeToVolume(float targetVolume, float duration)
        {
            float startVolume = m_AudioSource.volume;
            float elapsedTime = 0f;
            while (elapsedTime < duration)
            {
                elapsedTime += UnityEngine.Time.deltaTime;
                m_AudioSource.volume = Mathf.Lerp(startVolume, targetVolume, elapsedTime / duration);
                yield return null;
            }

            m_AudioSource.volume = targetVolume;
            m_FadeCoroutine = null;
        }

        private IEnumerator FadeOutAndStop(float fadeOutSeconds)
        {
            yield return StartCoroutine(FadeToVolume(0f, fadeOutSeconds));
            m_AudioSource.Stop();
            m_IsFadingOut = false;
            m_IsPaused = false;
            m_IsPlayFinished = true;
            OnResetSoundAgent(this, ResetSoundAgentEventArgs.Create());
        }

        private IEnumerator FadeOutThenPause(float fadeOutSeconds)
        {
            yield return StartCoroutine(FadeToVolume(0f, fadeOutSeconds));
            m_AudioSource.Pause();
            m_IsPaused = true;
        }

        private void OnResetSoundAgent(object sender, ResetSoundAgentEventArgs e)
        {
            ResetSoundAgent?.Invoke(sender, e);
            ReferencePool.Release(e);
        }
    }
}
