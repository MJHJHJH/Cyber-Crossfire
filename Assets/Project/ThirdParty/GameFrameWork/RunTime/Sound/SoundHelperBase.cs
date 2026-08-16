using GameFramework.Sound;

namespace GameFramework
{
    /// <summary>
    /// 声音辅助器基类（纯 C#，无需挂载物体）。
    /// </summary>
    public abstract class SoundHelperBase : ISoundHelper
    {
        /// <summary>
        /// 释放声音资源。
        /// </summary>
        public abstract void ReleaseSoundAsset(object soundAsset);
    }
}
