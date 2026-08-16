using System;

namespace GameFramework.Sound
{
    internal sealed partial class SoundManager : GameFrameworkModule, ISoundManager
    {
        /// <summary>
        /// 声音代理：单实例播放状态，音量乘法 / 静音 OR 组合，播放完自动释放资源。
        /// </summary>
        private sealed class SoundAgent : ISoundAgent
        {
            private readonly SoundGroup m_SoundGroup;
            private readonly ISoundHelper m_SoundHelper;
            private readonly ISoundAgentHelper m_SoundAgentHelper;
            private int m_SerialId;
            private string m_SoundAssetName;
            private object m_SoundAsset;
            private DateTime m_SetSoundAssetTime;
            private bool m_MuteInSoundGroup;
            private float m_VolumeInSoundGroup;

            public SoundAgent(SoundGroup soundGroup, ISoundHelper soundHelper, ISoundAgentHelper soundAgentHelper)
            {
                if (soundGroup == null)
                {
                    throw new GameFrameworkException("Sound group is invalid.");
                }

                if (soundHelper == null)
                {
                    throw new GameFrameworkException("Sound helper is invalid.");
                }

                if (soundAgentHelper == null)
                {
                    throw new GameFrameworkException("Sound agent helper is invalid.");
                }

                m_SoundGroup = soundGroup;
                m_SoundHelper = soundHelper;
                m_SoundAgentHelper = soundAgentHelper;
                m_SoundAgentHelper.ResetSoundAgent += OnResetSoundAgent;
                m_SerialId = 0;
                m_SoundAssetName = null;
                m_SoundAsset = null;
                Reset();
            }

            public ISoundGroup SoundGroup => m_SoundGroup;

            public int SerialId
            {
                get => m_SerialId;
                set => m_SerialId = value;
            }

            public string SoundAssetName => m_SoundAssetName;

            public bool IsPlaying => m_SoundAgentHelper.IsPlaying;

            public float Length => m_SoundAgentHelper.Length;

            public float Time
            {
                get => m_SoundAgentHelper.Time;
                set => m_SoundAgentHelper.Time = value;
            }

            public bool Mute => m_SoundAgentHelper.Mute;

            public bool MuteInSoundGroup
            {
                get => m_MuteInSoundGroup;
                set
                {
                    m_MuteInSoundGroup = value;
                    RefreshMute();
                }
            }

            public bool Loop
            {
                get => m_SoundAgentHelper.Loop;
                set => m_SoundAgentHelper.Loop = value;
            }

            public int Priority
            {
                get => m_SoundAgentHelper.Priority;
                set => m_SoundAgentHelper.Priority = value;
            }

            public float Volume => m_SoundAgentHelper.Volume;

            public float VolumeInSoundGroup
            {
                get => m_VolumeInSoundGroup;
                set
                {
                    m_VolumeInSoundGroup = value;
                    RefreshVolume();
                }
            }

            public float Pitch
            {
                get => m_SoundAgentHelper.Pitch;
                set => m_SoundAgentHelper.Pitch = value;
            }

            public float PanStereo
            {
                get => m_SoundAgentHelper.PanStereo;
                set => m_SoundAgentHelper.PanStereo = value;
            }

            public float SpatialBlend
            {
                get => m_SoundAgentHelper.SpatialBlend;
                set => m_SoundAgentHelper.SpatialBlend = value;
            }

            public float MaxDistance
            {
                get => m_SoundAgentHelper.MaxDistance;
                set => m_SoundAgentHelper.MaxDistance = value;
            }

            public float DopplerLevel
            {
                get => m_SoundAgentHelper.DopplerLevel;
                set => m_SoundAgentHelper.DopplerLevel = value;
            }

            public ISoundAgentHelper Helper => m_SoundAgentHelper;

            internal DateTime SetSoundAssetTime => m_SetSoundAssetTime;

            public void Play()
            {
                m_SoundAgentHelper.Play(Constant.DefaultFadeInSeconds);
            }

            public void Play(float fadeInSeconds)
            {
                m_SoundAgentHelper.Play(fadeInSeconds);
            }

            public void Stop()
            {
                m_SoundAgentHelper.Stop(Constant.DefaultFadeOutSeconds);
            }

            public void Stop(float fadeOutSeconds)
            {
                m_SoundAgentHelper.Stop(fadeOutSeconds);
            }

            public void Pause()
            {
                m_SoundAgentHelper.Pause(Constant.DefaultFadeOutSeconds);
            }

            public void Pause(float fadeOutSeconds)
            {
                m_SoundAgentHelper.Pause(fadeOutSeconds);
            }

            public void Resume()
            {
                m_SoundAgentHelper.Resume(Constant.DefaultFadeInSeconds);
            }

            public void Resume(float fadeInSeconds)
            {
                m_SoundAgentHelper.Resume(fadeInSeconds);
            }

            public void Reset()
            {
                if (m_SoundAsset != null)
                {
                    m_SoundHelper.ReleaseSoundAsset(m_SoundAsset);
                    m_SoundAsset = null;
                }

                m_SoundAssetName = null;
                m_SetSoundAssetTime = DateTime.MinValue;
                Time = Constant.DefaultTime;
                MuteInSoundGroup = Constant.DefaultMute;
                Loop = Constant.DefaultLoop;
                Priority = Constant.DefaultPriority;
                VolumeInSoundGroup = Constant.DefaultVolume;
                Pitch = Constant.DefaultPitch;
                PanStereo = Constant.DefaultPanStereo;
                SpatialBlend = Constant.DefaultSpatialBlend;
                MaxDistance = Constant.DefaultMaxDistance;
                DopplerLevel = Constant.DefaultDopplerLevel;
                m_SoundAgentHelper.Reset();
            }

            internal bool SetSoundAsset(string soundAssetName, object soundAsset)
            {
                Reset();
                m_SoundAssetName = soundAssetName;
                m_SoundAsset = soundAsset;
                m_SetSoundAssetTime = DateTime.UtcNow;
                return m_SoundAgentHelper.SetSoundAsset(soundAsset);
            }

            internal void RefreshMute()
            {
                m_SoundAgentHelper.Mute = m_SoundGroup.Mute || m_MuteInSoundGroup;
            }

            internal void RefreshVolume()
            {
                m_SoundAgentHelper.Volume = m_SoundGroup.Volume * m_VolumeInSoundGroup;
            }

            private void OnResetSoundAgent(object sender, ResetSoundAgentEventArgs e)
            {
                Reset();
            }
        }
    }
}
