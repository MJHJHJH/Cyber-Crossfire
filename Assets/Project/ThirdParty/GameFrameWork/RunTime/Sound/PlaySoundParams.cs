namespace GameFramework.Sound
{
    /// <summary>
    /// 播放声音参数。
    /// </summary>
    public sealed class PlaySoundParams : IReference
    {
        private bool m_Referenced;
        private float m_Time;
        private bool m_MuteInSoundGroup;
        private bool m_Loop;
        private int m_Priority;
        private float m_VolumeInSoundGroup;
        private float m_FadeInSeconds;
        private float m_Pitch;
        private float m_PanStereo;
        private float m_SpatialBlend;
        private float m_MaxDistance;
        private float m_DopplerLevel;

        public PlaySoundParams()
        {
            m_Referenced = false;
            m_Time = Constant.DefaultTime;
            m_MuteInSoundGroup = Constant.DefaultMute;
            m_Loop = Constant.DefaultLoop;
            m_Priority = Constant.DefaultPriority;
            m_VolumeInSoundGroup = Constant.DefaultVolume;
            m_FadeInSeconds = Constant.DefaultFadeInSeconds;
            m_Pitch = Constant.DefaultPitch;
            m_PanStereo = Constant.DefaultPanStereo;
            m_SpatialBlend = Constant.DefaultSpatialBlend;
            m_MaxDistance = Constant.DefaultMaxDistance;
            m_DopplerLevel = Constant.DefaultDopplerLevel;
        }

        public float Time { get => m_Time; set => m_Time = value; }
        public bool MuteInSoundGroup { get => m_MuteInSoundGroup; set => m_MuteInSoundGroup = value; }
        public bool Loop { get => m_Loop; set => m_Loop = value; }
        public int Priority { get => m_Priority; set => m_Priority = value; }
        public float VolumeInSoundGroup { get => m_VolumeInSoundGroup; set => m_VolumeInSoundGroup = value; }
        public float FadeInSeconds { get => m_FadeInSeconds; set => m_FadeInSeconds = value; }
        public float Pitch { get => m_Pitch; set => m_Pitch = value; }
        public float PanStereo { get => m_PanStereo; set => m_PanStereo = value; }
        public float SpatialBlend { get => m_SpatialBlend; set => m_SpatialBlend = value; }
        public float MaxDistance { get => m_MaxDistance; set => m_MaxDistance = value; }
        public float DopplerLevel { get => m_DopplerLevel; set => m_DopplerLevel = value; }

        internal bool Referenced => m_Referenced;

        /// <summary>
        /// 创建播放声音参数。
        /// </summary>
        public static PlaySoundParams Create()
        {
            PlaySoundParams playSoundParams = ReferencePool.Acquire<PlaySoundParams>();
            playSoundParams.m_Referenced = true;
            return playSoundParams;
        }

        /// <summary>
        /// 清理播放声音参数。
        /// </summary>
        public void Clear()
        {
            m_Time = Constant.DefaultTime;
            m_MuteInSoundGroup = Constant.DefaultMute;
            m_Loop = Constant.DefaultLoop;
            m_Priority = Constant.DefaultPriority;
            m_VolumeInSoundGroup = Constant.DefaultVolume;
            m_FadeInSeconds = Constant.DefaultFadeInSeconds;
            m_Pitch = Constant.DefaultPitch;
            m_PanStereo = Constant.DefaultPanStereo;
            m_SpatialBlend = Constant.DefaultSpatialBlend;
            m_MaxDistance = Constant.DefaultMaxDistance;
            m_DopplerLevel = Constant.DefaultDopplerLevel;
        }
    }
}
