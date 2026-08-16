using System;
using System.Collections.Generic;

namespace GameFramework.Sound
{
    internal sealed partial class SoundManager : GameFrameworkModule, ISoundManager
    {
        /// <summary>
        /// 声音组。
        /// </summary>
        private sealed class SoundGroup : ISoundGroup
        {
            private readonly string m_Name;
            private readonly ISoundGroupHelper m_SoundGroupHelper;
            private readonly List<SoundAgent> m_SoundAgents;
            private bool m_AvoidBeingReplacedBySamePriority;
            private bool m_Mute;
            private float m_Volume;

            public SoundGroup(string name, ISoundGroupHelper soundGroupHelper)
            {
                if (string.IsNullOrEmpty(name))
                {
                    throw new GameFrameworkException("Sound group name is invalid.");
                }

                if (soundGroupHelper == null)
                {
                    throw new GameFrameworkException("Sound group helper is invalid.");
                }

                m_Name = name;
                m_SoundGroupHelper = soundGroupHelper;
                m_SoundAgents = new List<SoundAgent>();
            }

            public string Name => m_Name;

            public int SoundAgentCount => m_SoundAgents.Count;

            public bool AvoidBeingReplacedBySamePriority
            {
                get => m_AvoidBeingReplacedBySamePriority;
                set => m_AvoidBeingReplacedBySamePriority = value;
            }

            public bool Mute
            {
                get => m_Mute;
                set
                {
                    m_Mute = value;
                    foreach (SoundAgent soundAgent in m_SoundAgents)
                    {
                        soundAgent.RefreshMute();
                    }
                }
            }

            public float Volume
            {
                get => m_Volume;
                set
                {
                    m_Volume = value;
                    foreach (SoundAgent soundAgent in m_SoundAgents)
                    {
                        soundAgent.RefreshVolume();
                    }
                }
            }

            public ISoundGroupHelper Helper => m_SoundGroupHelper;

            public void AddSoundAgentHelper(ISoundHelper soundHelper, ISoundAgentHelper soundAgentHelper)
            {
                m_SoundAgents.Add(new SoundAgent(this, soundHelper, soundAgentHelper));
            }

            public ISoundAgent PlaySound(string soundAssetName, int serialId, object soundAsset, PlaySoundParams playSoundParams, out PlaySoundErrorCode? errorCode)
            {
                errorCode = null;
                SoundAgent candidateAgent = null;
                foreach (SoundAgent soundAgent in m_SoundAgents)
                {
                    if (!soundAgent.IsPlaying)
                    {
                        candidateAgent = soundAgent;
                        break;
                    }

                    if (soundAgent.Priority < playSoundParams.Priority)
                    {
                        if (candidateAgent == null || soundAgent.Priority < candidateAgent.Priority)
                        {
                            candidateAgent = soundAgent;
                        }
                    }
                    else if (!m_AvoidBeingReplacedBySamePriority && soundAgent.Priority == playSoundParams.Priority)
                    {
                        if (candidateAgent == null || soundAgent.SetSoundAssetTime < candidateAgent.SetSoundAssetTime)
                        {
                            candidateAgent = soundAgent;
                        }
                    }
                }

                if (candidateAgent == null)
                {
                    errorCode = PlaySoundErrorCode.IgnoredDueToLowPriority;
                    return null;
                }

                if (!candidateAgent.SetSoundAsset(soundAssetName, soundAsset))
                {
                    errorCode = PlaySoundErrorCode.SetSoundAssetFailure;
                    return null;
                }

                candidateAgent.SerialId = serialId;
                candidateAgent.Time = playSoundParams.Time;
                candidateAgent.MuteInSoundGroup = playSoundParams.MuteInSoundGroup;
                candidateAgent.Loop = playSoundParams.Loop;
                candidateAgent.Priority = playSoundParams.Priority;
                candidateAgent.VolumeInSoundGroup = playSoundParams.VolumeInSoundGroup;
                candidateAgent.Pitch = playSoundParams.Pitch;
                candidateAgent.PanStereo = playSoundParams.PanStereo;
                candidateAgent.SpatialBlend = playSoundParams.SpatialBlend;
                candidateAgent.MaxDistance = playSoundParams.MaxDistance;
                candidateAgent.DopplerLevel = playSoundParams.DopplerLevel;
                candidateAgent.Play(playSoundParams.FadeInSeconds);
                return candidateAgent;
            }

            public bool StopSound(int serialId, float fadeOutSeconds)
            {
                foreach (SoundAgent soundAgent in m_SoundAgents)
                {
                    if (soundAgent.SerialId != serialId)
                    {
                        continue;
                    }

                    soundAgent.Stop(fadeOutSeconds);
                    return true;
                }

                return false;
            }

            /// <summary>
            /// 按资源名停止：匹配所有正在播放该 name 的 Agent（BGM 单例场景等价于停止那一条）。
            /// </summary>
            public bool StopSound(string soundAssetName, float fadeOutSeconds)
            {
                bool stopped = false;
                foreach (SoundAgent soundAgent in m_SoundAgents)
                {
                    if (!string.Equals(soundAgent.SoundAssetName, soundAssetName, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    soundAgent.Stop(fadeOutSeconds);
                    stopped = true;
                }

                return stopped;
            }

            public bool PauseSound(int serialId, float fadeOutSeconds)
            {
                foreach (SoundAgent soundAgent in m_SoundAgents)
                {
                    if (soundAgent.SerialId != serialId)
                    {
                        continue;
                    }

                    soundAgent.Pause(fadeOutSeconds);
                    return true;
                }

                return false;
            }

            public bool ResumeSound(int serialId, float fadeInSeconds)
            {
                foreach (SoundAgent soundAgent in m_SoundAgents)
                {
                    if (soundAgent.SerialId != serialId)
                    {
                        continue;
                    }

                    soundAgent.Resume(fadeInSeconds);
                    return true;
                }

                return false;
            }

            public void StopAllLoadedSounds()
            {
                foreach (SoundAgent soundAgent in m_SoundAgents)
                {
                    if (soundAgent.IsPlaying)
                    {
                        soundAgent.Stop();
                    }
                }
            }

            public void StopAllLoadedSounds(float fadeOutSeconds)
            {
                foreach (SoundAgent soundAgent in m_SoundAgents)
                {
                    if (soundAgent.IsPlaying)
                    {
                        soundAgent.Stop(fadeOutSeconds);
                    }
                }
            }
        }
    }
}
