using System;
using System.Collections.Generic;

namespace GameFramework.Sound
{
    /// <summary>
    /// 声音管理器：分组、Agent 池、优先级抢占、音量/静音组合、加载跟踪与自动释放。
    /// </summary>
    internal sealed partial class SoundManager : GameFrameworkModule, ISoundManager
    {
        private readonly Dictionary<string, SoundGroup> m_SoundGroups;
        private readonly List<int> m_SoundsBeingLoaded;
        private readonly HashSet<int> m_SoundsToReleaseOnLoad;
        private readonly LoadAudioClipCallbacks m_LoadAudioClipCallbacks;
        private ISoundAssetLoader m_ResourcesLoader;
        private ISoundAssetLoader m_YooAssetLoader;
        private ISoundHelper m_SoundHelper;
        private int m_Serial;
        private EventHandler<PlaySoundSuccessEventArgs> m_PlaySoundSuccessEventHandler;
        private EventHandler<PlaySoundFailureEventArgs> m_PlaySoundFailureEventHandler;
        private EventHandler<PlaySoundUpdateEventArgs> m_PlaySoundUpdateEventHandler;
        private EventHandler<PlaySoundDependencyAssetEventArgs> m_PlaySoundDependencyAssetEventHandler;

        public SoundManager()
        {
            m_SoundGroups = new Dictionary<string, SoundGroup>(StringComparer.Ordinal);
            m_SoundsBeingLoaded = new List<int>();
            m_SoundsToReleaseOnLoad = new HashSet<int>();
            m_LoadAudioClipCallbacks = new LoadAudioClipCallbacks(LoadAudioClipSuccessCallback, LoadAudioClipFailureCallback, LoadAudioClipUpdateCallback, LoadAudioClipDependencyAssetCallback);
            m_ResourcesLoader = null;
            m_YooAssetLoader = null;
            m_SoundHelper = null;
            m_Serial = 0;
            m_PlaySoundSuccessEventHandler = null;
            m_PlaySoundFailureEventHandler = null;
            m_PlaySoundUpdateEventHandler = null;
            m_PlaySoundDependencyAssetEventHandler = null;
        }

        public int SoundGroupCount => m_SoundGroups.Count;

        public event EventHandler<PlaySoundSuccessEventArgs> PlaySoundSuccess
        {
            add => m_PlaySoundSuccessEventHandler += value;
            remove => m_PlaySoundSuccessEventHandler -= value;
        }

        public event EventHandler<PlaySoundFailureEventArgs> PlaySoundFailure
        {
            add => m_PlaySoundFailureEventHandler += value;
            remove => m_PlaySoundFailureEventHandler -= value;
        }

        public event EventHandler<PlaySoundUpdateEventArgs> PlaySoundUpdate
        {
            add => m_PlaySoundUpdateEventHandler += value;
            remove => m_PlaySoundUpdateEventHandler -= value;
        }

        public event EventHandler<PlaySoundDependencyAssetEventArgs> PlaySoundDependencyAsset
        {
            add => m_PlaySoundDependencyAssetEventHandler += value;
            remove => m_PlaySoundDependencyAssetEventHandler -= value;
        }

        internal override void Update(float elapseSeconds, float realElapseSeconds)
        {
        }

        internal override void Shutdown()
        {
            StopAllLoadedSounds();
            m_SoundGroups.Clear();
            m_SoundsBeingLoaded.Clear();
            m_SoundsToReleaseOnLoad.Clear();
        }

        public void SetResourcesLoader(ISoundAssetLoader soundAssetLoader)
        {
            if (soundAssetLoader == null)
            {
                throw new GameFrameworkException("Resources sound asset loader is invalid.");
            }

            m_ResourcesLoader = soundAssetLoader;
        }

        public void SetYooAssetLoader(ISoundAssetLoader soundAssetLoader)
        {
            if (soundAssetLoader == null)
            {
                throw new GameFrameworkException("YooAsset sound asset loader is invalid.");
            }

            m_YooAssetLoader = soundAssetLoader;
        }

        public void SetSoundHelper(ISoundHelper soundHelper)
        {
            if (soundHelper == null)
            {
                throw new GameFrameworkException("Sound helper is invalid.");
            }

            m_SoundHelper = soundHelper;
        }

        public bool HasSoundGroup(string soundGroupName)
        {
            if (string.IsNullOrEmpty(soundGroupName))
            {
                throw new GameFrameworkException("Sound group name is invalid.");
            }

            return m_SoundGroups.ContainsKey(soundGroupName);
        }

        public ISoundGroup GetSoundGroup(string soundGroupName)
        {
            if (string.IsNullOrEmpty(soundGroupName))
            {
                throw new GameFrameworkException("Sound group name is invalid.");
            }

            return m_SoundGroups.TryGetValue(soundGroupName, out SoundGroup soundGroup) ? soundGroup : null;
        }

        public ISoundGroup[] GetAllSoundGroups()
        {
            int index = 0;
            ISoundGroup[] results = new ISoundGroup[m_SoundGroups.Count];
            foreach (KeyValuePair<string, SoundGroup> soundGroup in m_SoundGroups)
            {
                results[index++] = soundGroup.Value;
            }

            return results;
        }

        public bool AddSoundGroup(string soundGroupName, bool soundGroupAvoidBeingReplacedBySamePriority, bool soundGroupMute, float soundGroupVolume, ISoundGroupHelper soundGroupHelper)
        {
            if (string.IsNullOrEmpty(soundGroupName))
            {
                throw new GameFrameworkException("Sound group name is invalid.");
            }

            if (soundGroupHelper == null)
            {
                throw new GameFrameworkException("Sound group helper is invalid.");
            }

            if (HasSoundGroup(soundGroupName))
            {
                return false;
            }

            m_SoundGroups.Add(soundGroupName, new SoundGroup(soundGroupName, soundGroupHelper)
            {
                AvoidBeingReplacedBySamePriority = soundGroupAvoidBeingReplacedBySamePriority,
                Mute = soundGroupMute,
                Volume = soundGroupVolume
            });

            return true;
        }

        public void AddSoundAgentHelper(string soundGroupName, ISoundAgentHelper soundAgentHelper)
        {
            if (m_SoundHelper == null)
            {
                throw new GameFrameworkException("You must set sound helper first.");
            }

            SoundGroup soundGroup = (SoundGroup)GetSoundGroup(soundGroupName);
            if (soundGroup == null)
            {
                throw new GameFrameworkException(Utility.Text.Format("Sound group '{0}' is not exist.", soundGroupName));
            }

            soundGroup.AddSoundAgentHelper(m_SoundHelper, soundAgentHelper);
        }

        public bool IsLoadingSound(int serialId)
        {
            return m_SoundsBeingLoaded.Contains(serialId);
        }

        public int PlaySound(string soundAssetName, string soundGroupName, SoundLoadKind loadKind = SoundLoadKind.YooAsset)
        {
            return PlaySound(soundAssetName, soundGroupName, Constant.DefaultPriority, null, null, loadKind);
        }

        public int PlaySound(string soundAssetName, string soundGroupName, int priority, SoundLoadKind loadKind = SoundLoadKind.YooAsset)
        {
            return PlaySound(soundAssetName, soundGroupName, priority, null, null, loadKind);
        }

        public int PlaySound(string soundAssetName, string soundGroupName, PlaySoundParams playSoundParams, SoundLoadKind loadKind = SoundLoadKind.YooAsset)
        {
            return PlaySound(soundAssetName, soundGroupName, Constant.DefaultPriority, playSoundParams, null, loadKind);
        }

        public int PlaySound(string soundAssetName, string soundGroupName, PlaySoundParams playSoundParams, object userData, SoundLoadKind loadKind = SoundLoadKind.YooAsset)
        {
            return PlaySound(soundAssetName, soundGroupName, Constant.DefaultPriority, playSoundParams, userData, loadKind);
        }

        public int PlaySound(string soundAssetName, string soundGroupName, object userData, SoundLoadKind loadKind = SoundLoadKind.YooAsset)
        {
            return PlaySound(soundAssetName, soundGroupName, Constant.DefaultPriority, null, userData, loadKind);
        }

        public int PlaySound(string soundAssetName, string soundGroupName, int priority, PlaySoundParams playSoundParams, object userData, SoundLoadKind loadKind = SoundLoadKind.YooAsset)
        {
            ISoundAssetLoader soundAssetLoader = ResolveLoader(loadKind);
            if (soundAssetLoader == null)
            {
                throw new GameFrameworkException(Utility.Text.Format("Sound asset loader for kind '{0}' is invalid.", loadKind.ToString()));
            }

            if (m_SoundHelper == null)
            {
                throw new GameFrameworkException("You must set sound helper first.");
            }

            if (playSoundParams == null)
            {
                playSoundParams = PlaySoundParams.Create();
            }

            int serialId = ++m_Serial;
            PlaySoundErrorCode? errorCode = null;
            string errorMessage = null;
            SoundGroup soundGroup = (SoundGroup)GetSoundGroup(soundGroupName);
            if (soundGroup == null)
            {
                errorCode = PlaySoundErrorCode.SoundGroupNotExist;
                errorMessage = Utility.Text.Format("Sound group '{0}' is not exist.", soundGroupName);
            }
            else if (soundGroup.SoundAgentCount <= 0)
            {
                errorCode = PlaySoundErrorCode.SoundGroupHasNoAgent;
                errorMessage = Utility.Text.Format("Sound group '{0}' is have no sound agent.", soundGroupName);
            }

            if (errorCode.HasValue)
            {
                if (m_PlaySoundFailureEventHandler != null)
                {
                    PlaySoundFailureEventArgs playSoundFailureEventArgs = PlaySoundFailureEventArgs.Create(serialId, soundAssetName, soundGroupName, playSoundParams, errorCode.Value, errorMessage, userData);
                    m_PlaySoundFailureEventHandler(this, playSoundFailureEventArgs);
                    ReferencePool.Release(playSoundFailureEventArgs);

                    if (playSoundParams.Referenced)
                    {
                        ReferencePool.Release(playSoundParams);
                    }

                    return serialId;
                }

                throw new GameFrameworkException(errorMessage);
            }

            m_SoundsBeingLoaded.Add(serialId);
            soundAssetLoader.LoadAudioClip(soundAssetName, priority, m_LoadAudioClipCallbacks, PlaySoundInfo.Create(serialId, soundGroup, playSoundParams, userData));
            return serialId;
        }

        private ISoundAssetLoader ResolveLoader(SoundLoadKind loadKind)
        {
            switch (loadKind)
            {
                case SoundLoadKind.YooAsset:
                    return m_YooAssetLoader;
                case SoundLoadKind.Resources:
                    return m_ResourcesLoader;
                default:
                    throw new GameFrameworkException(Utility.Text.Format("Sound load kind '{0}' is invalid.", loadKind.ToString()));
            }
        }

        public bool StopSound(int serialId)
        {
            return StopSound(serialId, Constant.DefaultFadeOutSeconds);
        }

        public bool StopSound(int serialId, float fadeOutSeconds)
        {
            if (IsLoadingSound(serialId))
            {
                m_SoundsToReleaseOnLoad.Add(serialId);
                m_SoundsBeingLoaded.Remove(serialId);
                return true;
            }

            foreach (KeyValuePair<string, SoundGroup> soundGroup in m_SoundGroups)
            {
                if (soundGroup.Value.StopSound(serialId, fadeOutSeconds))
                {
                    return true;
                }
            }

            return false;
        }

        public bool StopSound(string soundAssetName)
        {
            return StopSound(soundAssetName, Constant.DefaultFadeOutSeconds);
        }

        /// <summary>
        /// 按资源名停止所有匹配的声音（遍历各组 Agent，比对 SoundAssetName）。
        /// </summary>
        public bool StopSound(string soundAssetName, float fadeOutSeconds)
        {
            if (string.IsNullOrEmpty(soundAssetName))
            {
                return false;
            }

            bool stopped = false;
            foreach (KeyValuePair<string, SoundGroup> soundGroup in m_SoundGroups)
            {
                if (soundGroup.Value.StopSound(soundAssetName, fadeOutSeconds))
                {
                    stopped = true;
                }
            }

            return stopped;
        }

        public void StopAllLoadedSounds()
        {
            StopAllLoadedSounds(Constant.DefaultFadeOutSeconds);
        }

        public void StopAllLoadedSounds(float fadeOutSeconds)
        {
            foreach (KeyValuePair<string, SoundGroup> soundGroup in m_SoundGroups)
            {
                soundGroup.Value.StopAllLoadedSounds(fadeOutSeconds);
            }
        }

        public void StopAllLoadingSounds()
        {
            foreach (int serialId in m_SoundsBeingLoaded)
            {
                m_SoundsToReleaseOnLoad.Add(serialId);
            }
        }

        public void PauseSound(int serialId)
        {
            PauseSound(serialId, Constant.DefaultFadeOutSeconds);
        }

        public void PauseSound(int serialId, float fadeOutSeconds)
        {
            foreach (KeyValuePair<string, SoundGroup> soundGroup in m_SoundGroups)
            {
                if (soundGroup.Value.PauseSound(serialId, fadeOutSeconds))
                {
                    return;
                }
            }

            throw new GameFrameworkException(Utility.Text.Format("Can not find sound '{0}'.", serialId.ToString()));
        }

        public void ResumeSound(int serialId)
        {
            ResumeSound(serialId, Constant.DefaultFadeInSeconds);
        }

        public void ResumeSound(int serialId, float fadeInSeconds)
        {
            foreach (KeyValuePair<string, SoundGroup> soundGroup in m_SoundGroups)
            {
                if (soundGroup.Value.ResumeSound(serialId, fadeInSeconds))
                {
                    return;
                }
            }

            throw new GameFrameworkException(Utility.Text.Format("Can not find sound '{0}'.", serialId.ToString()));
        }

        private void LoadAudioClipSuccessCallback(string soundAssetName, object soundAsset, float duration, object userData)
        {
            PlaySoundInfo playSoundInfo = (PlaySoundInfo)userData;
            if (playSoundInfo == null)
            {
                throw new GameFrameworkException("Play sound info is invalid.");
            }

            if (m_SoundsToReleaseOnLoad.Contains(playSoundInfo.SerialId))
            {
                m_SoundsToReleaseOnLoad.Remove(playSoundInfo.SerialId);
                if (playSoundInfo.PlaySoundParams.Referenced)
                {
                    ReferencePool.Release(playSoundInfo.PlaySoundParams);
                }

                ReferencePool.Release(playSoundInfo);
                m_SoundHelper.ReleaseSoundAsset(soundAsset);
                return;
            }

            m_SoundsBeingLoaded.Remove(playSoundInfo.SerialId);

            PlaySoundErrorCode? errorCode = null;
            ISoundAgent soundAgent = playSoundInfo.SoundGroup.PlaySound(soundAssetName, playSoundInfo.SerialId, soundAsset, playSoundInfo.PlaySoundParams, out errorCode);
            if (soundAgent != null)
            {
                if (m_PlaySoundSuccessEventHandler != null)
                {
                    PlaySoundSuccessEventArgs playSoundSuccessEventArgs = PlaySoundSuccessEventArgs.Create(playSoundInfo.SerialId, soundAssetName, soundAgent, duration, playSoundInfo.UserData);
                    m_PlaySoundSuccessEventHandler(this, playSoundSuccessEventArgs);
                    ReferencePool.Release(playSoundSuccessEventArgs);
                }

                if (playSoundInfo.PlaySoundParams.Referenced)
                {
                    ReferencePool.Release(playSoundInfo.PlaySoundParams);
                }

                ReferencePool.Release(playSoundInfo);
                return;
            }

            m_SoundsToReleaseOnLoad.Remove(playSoundInfo.SerialId);
            m_SoundHelper.ReleaseSoundAsset(soundAsset);
            string errorMessage = Utility.Text.Format("Sound group '{0}' play sound '{1}' failure.", playSoundInfo.SoundGroup.Name, soundAssetName);
            if (m_PlaySoundFailureEventHandler != null)
            {
                PlaySoundFailureEventArgs playSoundFailureEventArgs = PlaySoundFailureEventArgs.Create(playSoundInfo.SerialId, soundAssetName, playSoundInfo.SoundGroup.Name, playSoundInfo.PlaySoundParams, errorCode.Value, errorMessage, playSoundInfo.UserData);
                m_PlaySoundFailureEventHandler(this, playSoundFailureEventArgs);
                ReferencePool.Release(playSoundFailureEventArgs);

                if (playSoundInfo.PlaySoundParams.Referenced)
                {
                    ReferencePool.Release(playSoundInfo.PlaySoundParams);
                }

                ReferencePool.Release(playSoundInfo);
                return;
            }

            if (playSoundInfo.PlaySoundParams.Referenced)
            {
                ReferencePool.Release(playSoundInfo.PlaySoundParams);
            }

            ReferencePool.Release(playSoundInfo);
            throw new GameFrameworkException(errorMessage);
        }

        private void LoadAudioClipFailureCallback(string soundAssetName, LoadAudioClipErrorCode errorCode, string errorMessage, object userData)
        {
            PlaySoundInfo playSoundInfo = (PlaySoundInfo)userData;
            if (playSoundInfo == null)
            {
                throw new GameFrameworkException("Play sound info is invalid.");
            }

            if (m_SoundsToReleaseOnLoad.Contains(playSoundInfo.SerialId))
            {
                m_SoundsToReleaseOnLoad.Remove(playSoundInfo.SerialId);
                if (playSoundInfo.PlaySoundParams.Referenced)
                {
                    ReferencePool.Release(playSoundInfo.PlaySoundParams);
                }

                return;
            }

            m_SoundsBeingLoaded.Remove(playSoundInfo.SerialId);
            string appendErrorMessage = Utility.Text.Format("Load sound failure, asset name '{0}', error code '{1}', error message '{2}'.", soundAssetName, errorCode.ToString(), errorMessage);
            if (m_PlaySoundFailureEventHandler != null)
            {
                PlaySoundFailureEventArgs playSoundFailureEventArgs = PlaySoundFailureEventArgs.Create(playSoundInfo.SerialId, soundAssetName, playSoundInfo.SoundGroup.Name, playSoundInfo.PlaySoundParams, PlaySoundErrorCode.LoadAssetFailure, appendErrorMessage, playSoundInfo.UserData);
                m_PlaySoundFailureEventHandler(this, playSoundFailureEventArgs);
                ReferencePool.Release(playSoundFailureEventArgs);

                if (playSoundInfo.PlaySoundParams.Referenced)
                {
                    ReferencePool.Release(playSoundInfo.PlaySoundParams);
                }

                return;
            }

            throw new GameFrameworkException(appendErrorMessage);
        }

        private void LoadAudioClipUpdateCallback(string soundAssetName, float progress, object userData)
        {
            PlaySoundInfo playSoundInfo = (PlaySoundInfo)userData;
            if (playSoundInfo == null)
            {
                throw new GameFrameworkException("Play sound info is invalid.");
            }

            if (m_PlaySoundUpdateEventHandler != null)
            {
                PlaySoundUpdateEventArgs playSoundUpdateEventArgs = PlaySoundUpdateEventArgs.Create(playSoundInfo.SerialId, soundAssetName, playSoundInfo.SoundGroup.Name, playSoundInfo.PlaySoundParams, progress, playSoundInfo.UserData);
                m_PlaySoundUpdateEventHandler(this, playSoundUpdateEventArgs);
                ReferencePool.Release(playSoundUpdateEventArgs);
            }
        }

        private void LoadAudioClipDependencyAssetCallback(string soundAssetName, string dependencyAssetName, int loadedCount, int totalCount, object userData)
        {
            PlaySoundInfo playSoundInfo = (PlaySoundInfo)userData;
            if (playSoundInfo == null)
            {
                throw new GameFrameworkException("Play sound info is invalid.");
            }

            if (m_PlaySoundDependencyAssetEventHandler != null)
            {
                PlaySoundDependencyAssetEventArgs playSoundDependencyAssetEventArgs = PlaySoundDependencyAssetEventArgs.Create(playSoundInfo.SerialId, soundAssetName, playSoundInfo.SoundGroup.Name, playSoundInfo.PlaySoundParams, dependencyAssetName, loadedCount, totalCount, playSoundInfo.UserData);
                m_PlaySoundDependencyAssetEventHandler(this, playSoundDependencyAssetEventArgs);
                ReferencePool.Release(playSoundDependencyAssetEventArgs);
            }
        }
    }
}
