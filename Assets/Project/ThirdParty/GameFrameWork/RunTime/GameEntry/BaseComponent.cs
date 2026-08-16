//------------------------------------------------------------
// Slim BaseComponent — Module driver + runtime settings
// Reference: Refresh BaseComponent (Helpers / EditorResource omitted)
//------------------------------------------------------------

using System.Collections.Generic;
using UnityEngine;
using YooAsset;

namespace GameFramework
{
    /// <summary>
    /// 基础组件：驱动 <see cref="GameFrameworkEntry"/> 轮询与关闭，并提供常用运行参数。
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Game Framework/Base")]
    public sealed class BaseComponent : GameFrameworkComponent
    {
        private float m_GameSpeedBeforePause = 1f;

        [SerializeField]
        private int m_FrameRate = 30;

        [SerializeField]
        private float m_GameSpeed = 1f;

        [SerializeField]
        private bool m_RunInBackground = true;

        [SerializeField]
        private bool m_NeverSleep = true;

        [SerializeField]
        private EPlayMode m_YooPlayMode = EPlayMode.EditorSimulateMode;

        [SerializeField]
        private string m_YooPackageName = "DefaultPackage";

        [SerializeField]
        private YooPackageCatalog m_YooPackageCatalog;

        [SerializeField]
        private string m_HostServerIP = "http://127.0.0.1";

        [SerializeField]
        private string m_FallbackHostServerIP = "http://127.0.0.1";

        [SerializeField]
        private string m_HostAppVersion = "v1.0";

        public int FrameRate
        {
            get => m_FrameRate;
            set => Application.targetFrameRate = m_FrameRate = value;
        }

        public float GameSpeed
        {
            get => m_GameSpeed;
            set => Time.timeScale = m_GameSpeed = value >= 0f ? value : 0f;
        }

        public bool IsGamePaused => m_GameSpeed <= 0f;

        public bool IsNormalGameSpeed => m_GameSpeed == 1f;

        public bool RunInBackground
        {
            get => m_RunInBackground;
            set => Application.runInBackground = m_RunInBackground = value;
        }

        public bool NeverSleep
        {
            get => m_NeverSleep;
            set
            {
                m_NeverSleep = value;
                Screen.sleepTimeout = value ? SleepTimeout.NeverSleep : SleepTimeout.SystemSetting;
            }
        }

        public EPlayMode YooPlayMode => m_YooPlayMode;

        public string YooPackageName =>
            string.IsNullOrEmpty(m_YooPackageName) ? "DefaultPackage" : m_YooPackageName;

        public YooPackageCatalog YooPackageCatalog => m_YooPackageCatalog;

        /// <summary>
        /// 解析启动包队列：优先 Catalog；否则回退为单一 <see cref="YooPackageName"/>。
        /// </summary>
        public void ResolveYooPackageQueue(List<string> packageNames, out string defaultPackageName)
        {
            packageNames.Clear();
            if (m_YooPackageCatalog != null &&
                m_YooPackageCatalog.TryBuildQueue(packageNames, out defaultPackageName))
                return;

            defaultPackageName = YooPackageName;
            packageNames.Add(defaultPackageName);
        }

        public string HostServerIP => m_HostServerIP;

        public string FallbackHostServerIP => m_FallbackHostServerIP;

        public string HostAppVersion =>
            string.IsNullOrEmpty(m_HostAppVersion) ? "v1.0" : m_HostAppVersion;

        protected override void Awake()
        {
            base.Awake();

            Application.targetFrameRate = m_FrameRate;
            Time.timeScale = m_GameSpeed;
            Application.runInBackground = m_RunInBackground;
            Screen.sleepTimeout = m_NeverSleep ? SleepTimeout.NeverSleep : SleepTimeout.SystemSetting;

            Application.lowMemory += OnLowMemory;
        }

        private void Update()
        {
            GameFrameworkEntry.Update(Time.deltaTime, Time.unscaledDeltaTime);
        }

        private void OnApplicationQuit()
        {
            Application.lowMemory -= OnLowMemory;
            StopAllCoroutines();
        }

        private void OnDestroy()
        {
            Application.lowMemory -= OnLowMemory;
            GameFrameworkEntry.Shutdown();
        }

        public void PauseGame()
        {
            if (IsGamePaused)
                return;

            m_GameSpeedBeforePause = GameSpeed;
            GameSpeed = 0f;
        }

        public void ResumeGame()
        {
            if (!IsGamePaused)
                return;

            GameSpeed = m_GameSpeedBeforePause;
        }

        public void ResetNormalGameSpeed()
        {
            if (IsNormalGameSpeed)
                return;

            GameSpeed = 1f;
        }

        internal void Shutdown()
        {
            Destroy(gameObject);
        }

        private void OnLowMemory()
        {
            ObjectPoolComponent objectPoolComponent = GameEntry.GetComponent<ObjectPoolComponent>();
            objectPoolComponent?.ReleaseAllUnused();
        }
    }
}
