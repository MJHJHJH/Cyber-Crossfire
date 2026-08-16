namespace GameFramework.Sound
{
    /// <summary>
    /// 播放声音成功事件。
    /// </summary>
    public sealed class PlaySoundSuccessEventArgs : GameFrameworkEventArgs
    {
        public PlaySoundSuccessEventArgs()
        {
            SerialId = 0;
            SoundAssetName = null;
            SoundAgent = null;
            Duration = 0f;
            UserData = null;
        }

        public int SerialId { get; private set; }
        public string SoundAssetName { get; private set; }
        public ISoundAgent SoundAgent { get; private set; }
        public float Duration { get; private set; }
        public object UserData { get; private set; }

        public static PlaySoundSuccessEventArgs Create(int serialId, string soundAssetName, ISoundAgent soundAgent, float duration, object userData)
        {
            PlaySoundSuccessEventArgs playSoundSuccessEventArgs = ReferencePool.Acquire<PlaySoundSuccessEventArgs>();
            playSoundSuccessEventArgs.SerialId = serialId;
            playSoundSuccessEventArgs.SoundAssetName = soundAssetName;
            playSoundSuccessEventArgs.SoundAgent = soundAgent;
            playSoundSuccessEventArgs.Duration = duration;
            playSoundSuccessEventArgs.UserData = userData;
            return playSoundSuccessEventArgs;
        }

        public override void Clear()
        {
            SerialId = 0;
            SoundAssetName = null;
            SoundAgent = null;
            Duration = 0f;
            UserData = null;
        }
    }
}
