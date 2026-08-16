namespace GameFramework.Sound
{
    /// <summary>
    /// 重置声音代理事件。
    /// </summary>
    public sealed class ResetSoundAgentEventArgs : GameFrameworkEventArgs
    {
        public ResetSoundAgentEventArgs()
        {
        }

        public static ResetSoundAgentEventArgs Create()
        {
            return ReferencePool.Acquire<ResetSoundAgentEventArgs>();
        }

        public override void Clear()
        {
        }
    }
}
