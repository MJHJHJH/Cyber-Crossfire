using GameFramework.Sound;
using System;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;
using YooAsset;

namespace GameFramework
{
    /// <summary>
    /// 声音组件：持有 ISoundManager，注入资源加载器与 Helper，创建组 / Agent 层级，
    /// 提供按 location 与按音效 ID 播放、大类 mixer 路由与 SetGroupVolume。
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Game Framework/Sound")]
    public sealed partial class SoundComponent : GameFrameworkComponent
    {
        public const int DefaultSoundAgentCount = 8;

        private const float DefaultVolume = 1f;
        private const float DefaultPitch = 1f;
        private const float DefaultPanStereo = 0f;
        private const float DefaultSpatialBlend = 0f;
        private const float DefaultMaxDistance = 100f;
        private const float DefaultDopplerLevel = 1f;

        private ISoundManager m_SoundManager;
        private EventComponent m_EventComponent;
        private SoundAssetLoaderBase m_ResourcesLoader;
        private SoundAssetLoaderBase m_YooAssetLoader;
        private ISoundConfigTable m_SoundConfigTable;
        private AudioListener m_AudioListener;

        [SerializeField]
        private bool m_EnablePlaySoundUpdateEvent = false;

        [SerializeField]
        private bool m_EnablePlaySoundDependencyAssetEvent = false;

        [SerializeField]
        private Transform m_InstanceRoot = null;

        [SerializeField]
        private AudioMixer m_AudioMixer = null;

        private ISoundGroupConfigTable m_SoundGroupConfigTable;

        public ISoundManager SoundManager => m_SoundManager;

        public bool IsReady => m_SoundManager != null && m_ResourcesLoader != null;

        public AudioMixer AudioMixer => m_AudioMixer;

        public bool EnablePlaySoundUpdateEvent
        {
            get => m_EnablePlaySoundUpdateEvent;
            set => m_EnablePlaySoundUpdateEvent = value;
        }

        public bool EnablePlaySoundDependencyAssetEvent
        {
            get => m_EnablePlaySoundDependencyAssetEvent;
            set => m_EnablePlaySoundDependencyAssetEvent = value;
        }

        protected override void Awake()
        {
            base.Awake();

            m_SoundManager = GameFrameworkEntry.GetModule<ISoundManager>();
            if (m_SoundManager == null)
            {
                Debug.LogError("Sound manager is invalid.");
                return;
            }

            m_SoundManager.PlaySoundSuccess += OnPlaySoundSuccess;
            m_SoundManager.PlaySoundFailure += OnPlaySoundFailure;
            m_SoundManager.PlaySoundUpdate += OnPlaySoundUpdate;
            m_SoundManager.PlaySoundDependencyAsset += OnPlaySoundDependencyAsset;

            RefreshAudioListener();
            SceneManager.sceneLoaded += OnSceneLoaded;
            SceneManager.sceneUnloaded += OnSceneUnloaded;
        }

        private void Start()
        {
            m_EventComponent = GameEntry.GetComponent<EventComponent>();
            SetupAssetLoader();
            SetupSoundManager();
            SetupSoundGroups();
        }

        private void Update()
        {
            m_ResourcesLoader?.Update(Time.deltaTime);
            m_YooAssetLoader?.Update(Time.deltaTime);
        }

        private void OnDestroy()
        {
            if (m_SoundManager != null)
            {
                m_SoundManager.PlaySoundSuccess -= OnPlaySoundSuccess;
                m_SoundManager.PlaySoundFailure -= OnPlaySoundFailure;
                m_SoundManager.PlaySoundUpdate -= OnPlaySoundUpdate;
                m_SoundManager.PlaySoundDependencyAsset -= OnPlaySoundDependencyAsset;
            }

            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneUnloaded -= OnSceneUnloaded;
        }

        /// <summary>
        /// 按 location 播放声音。
        /// </summary>
        public int PlaySound(string soundAssetName, string soundGroupName, SoundLoadKind loadKind = SoundLoadKind.YooAsset)
        {
            return PlaySound(soundAssetName, soundGroupName, DefaultVolume, null, default, null, loadKind);
        }

        /// <summary>
        /// 按 location 播放声音，可绑定目标跟随或指定世界坐标。
        /// </summary>
        public int PlaySound(string soundAssetName, string soundGroupName, float volume, Transform bindingTransform = null, Vector3 worldPosition = default, object userData = null, SoundLoadKind loadKind = SoundLoadKind.YooAsset)
        {
            if (m_SoundManager == null)
            {
                return -1;
            }

            EnsureSoundGroup(soundGroupName, DefaultSoundAgentCount);

            PlaySoundParams playSoundParams = PlaySoundParams.Create();
            playSoundParams.VolumeInSoundGroup = volume;
            return m_SoundManager.PlaySound(soundAssetName, soundGroupName, playSoundParams, new PlaySoundInfo(bindingTransform, worldPosition, userData), loadKind);
        }

        /// <summary>
        /// 按 location 播放声音（完整参数版本）。
        /// </summary>
        public int PlaySound(string soundAssetName, string soundGroupName, int priority, PlaySoundParams playSoundParams, Transform bindingTransform = null, Vector3 worldPosition = default, object userData = null, SoundLoadKind loadKind = SoundLoadKind.YooAsset)
        {
            if (m_SoundManager == null)
            {
                return -1;
            }

            EnsureSoundGroup(soundGroupName, DefaultSoundAgentCount);

            return m_SoundManager.PlaySound(soundAssetName, soundGroupName, priority, playSoundParams, new PlaySoundInfo(bindingTransform, worldPosition, userData), loadKind);
        }

        /// <summary>
        /// 按音效配表 ID 播放声音，组不存在时按 D8 兜底自动创建。
        /// </summary>
        public int PlaySound(int soundId, Transform bindingTransform = null, object userData = null, SoundLoadKind loadKind = SoundLoadKind.YooAsset)
        {
            if (!TryGetSoundConfig(soundId, out SoundConfig soundConfig))
            {
                FireSoundTableNotFound(soundId);
                return -1;
            }

            string groupName = string.IsNullOrEmpty(soundConfig.Group) ? Constant.DefaultSoundGroupName : soundConfig.Group;
            EnsureSoundGroup(groupName, DefaultSoundAgentCount);
            return PlaySound(soundConfig.Location, groupName, soundConfig.Priority, CreatePlaySoundParams(soundConfig), bindingTransform, default, userData, loadKind);
        }

        /// <summary>
        /// 按音效配表 ID 在世界坐标位置播放声音（3D 音效用，3D 参数取自配表，不跟随任何物体）。
        /// 注意：worldPosition 无默认值，避免与 bindingTransform 重载在省略第二参数时产生调用歧义。
        /// </summary>
        public int PlaySound(int soundId, Vector3 worldPosition, object userData = null, SoundLoadKind loadKind = SoundLoadKind.YooAsset)
        {
            if (!TryGetSoundConfig(soundId, out SoundConfig soundConfig))
            {
                FireSoundTableNotFound(soundId);
                return -1;
            }

            string groupName = string.IsNullOrEmpty(soundConfig.Group) ? Constant.DefaultSoundGroupName : soundConfig.Group;
            EnsureSoundGroup(groupName, DefaultSoundAgentCount);
            return PlaySound(soundConfig.Location, groupName, soundConfig.Priority, CreatePlaySoundParams(soundConfig), null, worldPosition, userData, loadKind);
        }

        public bool StopSound(int serialId)
        {
            return m_SoundManager != null && m_SoundManager.StopSound(serialId);
        }

        public bool StopSound(int serialId, float fadeOutSeconds)
        {
            return m_SoundManager != null && m_SoundManager.StopSound(serialId, fadeOutSeconds);
        }

        /// <summary>
        /// 按资源名停止声音（BGM 单例场景可直接传 location 关闭）。
        /// </summary>
        public bool StopSound(string location, float fadeOutSeconds = 0f)
        {
            return m_SoundManager != null && m_SoundManager.StopSound(location, fadeOutSeconds);
        }

        /// <summary>
        /// 按音效配表 ID 停止声音（查表转 location 后停止，BGM 单例场景可直接传音效 ID 关闭）。
        /// </summary>
        public bool StopSoundById(int soundId, float fadeOutSeconds = 0f)
        {
            if (m_SoundManager == null || !TryGetSoundConfig(soundId, out SoundConfig soundConfig))
            {
                return false;
            }

            return m_SoundManager.StopSound(soundConfig.Location, fadeOutSeconds);
        }

        public void StopAllLoadedSounds()
        {
            m_SoundManager?.StopAllLoadedSounds();
        }

        public void StopAllLoadingSounds()
        {
            m_SoundManager?.StopAllLoadingSounds();
        }

        public void PauseSound(int serialId)
        {
            m_SoundManager?.PauseSound(serialId);
        }

        public void PauseSound(int serialId, float fadeOutSeconds)
        {
            m_SoundManager?.PauseSound(serialId, fadeOutSeconds);
        }

        public void ResumeSound(int serialId)
        {
            m_SoundManager?.ResumeSound(serialId);
        }

        public void ResumeSound(int serialId, float fadeInSeconds)
        {
            m_SoundManager?.ResumeSound(serialId, fadeInSeconds);
        }

        /// <summary>
        /// 组不存在时自动创建组与 Agent（配表 Group 为空或未在 Inspector 配置时兜底）。
        /// </summary>
        public bool EnsureSoundGroup(string groupName, int agentCount)
        {
            if (m_SoundManager == null)
            {
                return false;
            }

            if (m_SoundManager.HasSoundGroup(groupName))
            {
                return true;
            }

            return AddSoundGroup(groupName, false, false, 1f, agentCount);
        }

        /// <summary>
        /// 增加声音组。
        /// </summary>
        public bool AddSoundGroup(string groupName, bool avoidBeingReplacedBySamePriority, bool mute, float volume, int agentCount)
        {
            if (m_SoundManager == null)
            {
                return false;
            }

            if (m_SoundManager.HasSoundGroup(groupName))
            {
                return false;
            }

            if (!m_SoundManager.AddSoundGroup(groupName, avoidBeingReplacedBySamePriority, mute, volume, CreateSoundGroupHelper(groupName)))
            {
                return false;
            }

            for (int i = 0; i < agentCount; i++)
            {
                m_SoundManager.AddSoundAgentHelper(groupName, CreateSoundAgentHelper(groupName));
            }

            return true;
        }

        /// <summary>
        /// 大类音量（0~1），映射到 mixer 暴露参数 "{大类}Volume" 的 dB 值；作为设置界面整类音量入口。
        /// </summary>
        public bool SetGroupVolume(string groupName, float linearVolume)
        {
            if (m_AudioMixer == null)
            {
                Debug.LogWarning("AudioMixer is not set. SetGroupVolume is ignored.");
                return false;
            }

            float dB = Mathf.Log10(Mathf.Clamp(linearVolume, 0.0001f, 1f)) * 20f;
            string parameterName = Utility.Text.Format("{0}Volume", groupName);
            if (!m_AudioMixer.SetFloat(parameterName, dB))
            {
                Debug.LogWarningFormat("AudioMixer has no exposed parameter '{0}'.", parameterName);
                return false;
            }

            return true;
        }

        public void ReleaseAudioClip(object soundAsset)
        {
            m_ResourcesLoader?.ReleaseAudioClip(soundAsset);
            m_YooAssetLoader?.ReleaseAudioClip(soundAsset);
        }

        private void SetupAssetLoader()
        {
            if (m_ResourcesLoader != null)
            {
                return;
            }

            // Resources 加载器始终可用，YooAsset 未就绪时也可播放内置音效。
            m_ResourcesLoader = new ResourcesSoundAssetLoader();
        }

        /// <summary>
        /// 注入 YooAsset 包并启用 YooAsset 加载（资源补丁完成后由 ProcedureAssetInit 调用）。
        /// </summary>
        public void SetYooAssetPackage(ResourcePackage package)
        {
            if (package == null)
            {
                Debug.LogWarning("SetYooAssetPackage ignored: package is null.");
                return;
            }

            m_YooAssetLoader = new YooAssetSoundAssetLoader(package);
            m_SoundManager?.SetYooAssetLoader(m_YooAssetLoader);
        }

        private void SetupSoundManager()
        {
            if (m_SoundManager == null || m_ResourcesLoader == null)
            {
                return;
            }

            m_SoundManager.SetResourcesLoader(m_ResourcesLoader);
            if (m_YooAssetLoader != null)
            {
                m_SoundManager.SetYooAssetLoader(m_YooAssetLoader);
            }

            m_SoundManager.SetSoundHelper(CreateSoundHelper());
        }

        private void SetupSoundGroups()
        {
            if (m_SoundGroupConfigTable == null)
            {
                return;
            }

            foreach (SoundGroupConfig soundGroup in m_SoundGroupConfigTable.All)
            {
                if (string.IsNullOrEmpty(soundGroup.Name))
                {
                    continue;
                }

                if (!AddSoundGroup(soundGroup.Name, soundGroup.AvoidBeingReplacedBySamePriority, soundGroup.Mute, soundGroup.Volume, soundGroup.AgentCount))
                {
                    Debug.LogWarningFormat("Sound group '{0}' already exists.", soundGroup.Name);
                }
            }
        }

        /// <summary>
        /// 配表加载完成后由业务侧调用，按 SoundGroup 表初始化声音组与大类 mixer 路由。
        /// </summary>
        public void InitSoundGroupsFromTable()
        {
            SetupSoundGroups();
        }

        private ISoundHelper CreateSoundHelper()
        {
            return new DefaultSoundHelper();
        }

        private ISoundGroupHelper CreateSoundGroupHelper(string groupName)
        {
            GetOrCreateGroupRoot(groupName);
            return new DefaultSoundGroupHelper();
        }

        private ISoundAgentHelper CreateSoundAgentHelper(string groupName)
        {
            Transform groupRoot = GetOrCreateGroupRoot(groupName);
            GameObject helper = new GameObject("Sound Agent Helper", typeof(DefaultSoundAgentHelper));
            helper.transform.SetParent(groupRoot, false);
            SoundAgentHelperBase agentHelper = helper.GetComponent<DefaultSoundAgentHelper>();
            agentHelper.AudioMixerGroup = ResolveMixerGroup(groupName);
            return agentHelper;
        }

        /// <summary>
        /// 运行时实例根：首次访问时创建，命名 instance 并挂在本组件物体下。
        /// </summary>
        private Transform EnsureInstanceRoot()
        {
            if (m_InstanceRoot != null)
            {
                return m_InstanceRoot;
            }

            GameObject instanceRoot = new GameObject("Instance");
            instanceRoot.transform.SetParent(transform, false);
            m_InstanceRoot = instanceRoot.transform;
            return m_InstanceRoot;
        }

        /// <summary>
        /// 组父节点：同组 Helper 集中在 instance/{组名} 下，避免散列。
        /// </summary>
        private Transform GetOrCreateGroupRoot(string groupName)
        {
            Transform instanceRoot = EnsureInstanceRoot();
            Transform groupRoot = instanceRoot.Find(groupName);
            if (groupRoot != null)
            {
                return groupRoot;
            }

            GameObject groupRootGo = new GameObject(groupName);
            groupRootGo.transform.SetParent(instanceRoot, false);
            return groupRootGo.transform;
        }

        private AudioMixerGroup ResolveMixerGroup(string groupName)
        {
            if (m_AudioMixer == null)
            {
                return null;
            }

            // 默认兜底组不属于任何大类，不走 mixer，直通 AudioListener。
            if (string.Equals(groupName, Constant.DefaultSoundGroupName, StringComparison.Ordinal))
            {
                return null;
            }

            string mixerGroupPath = GetMixerGroupPath(groupName);
            AudioMixerGroup[] groups = m_AudioMixer.FindMatchingGroups(mixerGroupPath);
            if (groups == null || groups.Length == 0)
            {
                Debug.LogWarningFormat("Mixer group '{0}' for category '{1}' not found, fallback to Master.", mixerGroupPath, groupName);
                AudioMixerGroup[] masterGroups = m_AudioMixer.FindMatchingGroups("Master");
                return masterGroups != null && masterGroups.Length > 0 ? masterGroups[0] : null;
            }

            return groups[0];
        }

        private string GetMixerGroupPath(string groupName)
        {
            if (m_SoundGroupConfigTable != null)
            {
                foreach (SoundGroupConfig soundGroup in m_SoundGroupConfigTable.All)
                {
                    if (string.Equals(soundGroup.Name, groupName, StringComparison.Ordinal))
                    {
                        return soundGroup.MixerGroupPath;
                    }
                }
            }

            return Utility.Text.Format("Master/{0}", groupName);
        }

        /// <summary>
        /// 注入音效配表查询器（业务侧实现，如 GamePlay.SoundConfigProvider）。
        /// </summary>
        public void SetSoundConfigTable(ISoundConfigTable soundConfigTable)
        {
            m_SoundConfigTable = soundConfigTable ?? throw new GameFrameworkException("Sound config table is invalid.");
        }

        /// <summary>
        /// 注入声音组配表查询器（业务侧实现，如 GamePlay.SoundGroupConfigProvider）。
        /// </summary>
        public void SetSoundGroupConfigTable(ISoundGroupConfigTable soundGroupConfigTable)
        {
            m_SoundGroupConfigTable = soundGroupConfigTable ?? throw new GameFrameworkException("Sound group config table is invalid.");
            SetupSoundGroups();
        }

        private bool TryGetSoundConfig(int soundId, out SoundConfig soundConfig)
        {
            soundConfig = default;
            if (m_SoundConfigTable == null)
            {
                Debug.LogWarning("Sound config table is not set. Call SetSoundConfigTable first.");
                return false;
            }

            return m_SoundConfigTable.TryGet(soundId, out soundConfig);
        }

        private static PlaySoundParams CreatePlaySoundParams(SoundConfig soundConfig)
        {
            PlaySoundParams playSoundParams = PlaySoundParams.Create();
            playSoundParams.Loop = soundConfig.Loop;
            playSoundParams.VolumeInSoundGroup = soundConfig.Volume;
            playSoundParams.Priority = soundConfig.Priority;
            playSoundParams.MuteInSoundGroup = soundConfig.Mute;
            playSoundParams.FadeInSeconds = soundConfig.FadeInSeconds;
            playSoundParams.Pitch = soundConfig.Pitch;
            playSoundParams.PanStereo = soundConfig.PanStereo;
            playSoundParams.SpatialBlend = soundConfig.SpatialBlend;
            playSoundParams.MaxDistance = soundConfig.MaxDistance;
            playSoundParams.DopplerLevel = soundConfig.DopplerLevel;
            return playSoundParams;
        }

        private void FireSoundTableNotFound(int soundId)
        {
            string errorMessage = Utility.Text.Format("Sound table has no entry with id '{0}'.", soundId.ToString());
            Debug.LogWarning(errorMessage);
            if (m_EventComponent == null)
            {
                return;
            }

            GameFramework.Sound.PlaySoundFailureEventArgs failureEventArgs = GameFramework.Sound.PlaySoundFailureEventArgs.Create(
                0, null, Constant.DefaultSoundGroupName, null, PlaySoundErrorCode.SoundTableNotFound, errorMessage, null);
            m_EventComponent.Fire(this, GameFramework.PlaySoundFailureEventArgs.Create(failureEventArgs));
        }

        private void OnPlaySoundSuccess(object sender, GameFramework.Sound.PlaySoundSuccessEventArgs e)
        {
            if (e.UserData is PlaySoundInfo playSoundInfo && e.SoundAgent.Helper is SoundAgentHelperBase agentHelper)
            {
                // 绑定目标与世界坐标二选一：优先跟随 Transform，否则使用世界坐标。
                // 若两者都调用，SetWorldPosition 会清空绑定状态，导致 3D 音效被钉在世界原点。
                if (playSoundInfo.BindingTransform != null)
                {
                    agentHelper.SetBindingTransform(playSoundInfo.BindingTransform);
                }
                else
                {
                    agentHelper.SetWorldPosition(playSoundInfo.WorldPosition);
                }
            }

            if (m_EventComponent != null)
            {
                m_EventComponent.Fire(this, GameFramework.PlaySoundSuccessEventArgs.Create(e));
            }
        }

        private void OnPlaySoundFailure(object sender, GameFramework.Sound.PlaySoundFailureEventArgs e)
        {
            if (e.ErrorCode == PlaySoundErrorCode.IgnoredDueToLowPriority)
            {
                Debug.LogFormat("Play sound '{0}' ignored due to low priority.", e.SoundAssetName);
            }
            else
            {
                Debug.LogWarningFormat("Play sound '{0}' failure, error code '{1}', message '{2}'.", e.SoundAssetName, e.ErrorCode, e.ErrorMessage);
            }

            if (m_EventComponent != null)
            {
                m_EventComponent.Fire(this, GameFramework.PlaySoundFailureEventArgs.Create(e));
            }
        }

        private void OnPlaySoundUpdate(object sender, GameFramework.Sound.PlaySoundUpdateEventArgs e)
        {
            if (m_EnablePlaySoundUpdateEvent && m_EventComponent != null)
            {
                m_EventComponent.Fire(this, GameFramework.PlaySoundUpdateEventArgs.Create(e));
            }
        }

        private void OnPlaySoundDependencyAsset(object sender, GameFramework.Sound.PlaySoundDependencyAssetEventArgs e)
        {
            if (m_EnablePlaySoundDependencyAssetEvent && m_EventComponent != null)
            {
                m_EventComponent.Fire(this, GameFramework.PlaySoundDependencyAssetEventArgs.Create(e));
            }
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            RefreshAudioListener();
        }

        private void OnSceneUnloaded(Scene scene)
        {
            RefreshAudioListener();
        }

        private void RefreshAudioListener()
        {
            AudioListener own = GetComponent<AudioListener>();
            AudioListener[] all = FindObjectsOfType<AudioListener>();

            // 场景自带的监听器（排除自建）
            AudioListener sceneListener = null;
            int sceneCount = 0;
            foreach (AudioListener listener in all)
            {
                if (listener == own)
                {
                    continue;
                }

                sceneListener = listener;
                sceneCount++;
            }

            if (sceneCount > 1)
            {
                Debug.LogWarning("Multiple AudioListener found in scene, only the first one is kept.");
            }

            if (sceneListener != null)
            {
                // 场景自带监听器：关闭自建，避免双监听器并存
                if (own != null)
                {
                    own.enabled = false;
                }

                m_AudioListener = sceneListener;
                return;
            }

            // 场景无监听器：启用自建（不存在则创建）
            if (own == null)
            {
                own = gameObject.AddComponent<AudioListener>();
            }

            own.enabled = true;
            m_AudioListener = own;
        }
    }
}
