namespace GameFramework.Sound
{
    /// <summary>
    /// 声音相关常量。
    /// </summary>
    public static class Constant
    {
        /// <summary>
        /// 默认兜底声音组名（配表 Group 为空时使用，Agent 不走 mixer）。
        /// </summary>
        public const string DefaultSoundGroupName = "Default";

        internal const float DefaultTime = 0f;
        internal const bool DefaultMute = false;
        internal const bool DefaultLoop = false;
        internal const int DefaultPriority = 0;
        internal const float DefaultVolume = 1f;
        internal const float DefaultFadeInSeconds = 0f;
        internal const float DefaultFadeOutSeconds = 0f;
        internal const float DefaultPitch = 1f;
        internal const float DefaultPanStereo = 0f;
        internal const float DefaultSpatialBlend = 0f;
        internal const float DefaultMaxDistance = 100f;
        internal const float DefaultDopplerLevel = 1f;
    }
}
