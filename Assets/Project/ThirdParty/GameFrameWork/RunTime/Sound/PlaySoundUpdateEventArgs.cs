namespace GameFramework.Sound
{
    /// <summary>
    /// 播放声音更新事件。
    /// </summary>
    public sealed class PlaySoundUpdateEventArgs : GameFrameworkEventArgs
    {
        public PlaySoundUpdateEventArgs()
        {
            SerialId = 0;
            SoundAssetName = null;
            SoundGroupName = null;
            PlaySoundParams = null;
            Progress = 0f;
            UserData = null;
        }

        public int SerialId { get; private set; }
        public string SoundAssetName { get; private set; }
        public string SoundGroupName { get; private set; }
        public PlaySoundParams PlaySoundParams { get; private set; }
        public float Progress { get; private set; }
        public object UserData { get; private set; }

        public static PlaySoundUpdateEventArgs Create(int serialId, string soundAssetName, string soundGroupName, PlaySoundParams playSoundParams, float progress, object userData)
        {
            PlaySoundUpdateEventArgs playSoundUpdateEventArgs = ReferencePool.Acquire<PlaySoundUpdateEventArgs>();
            playSoundUpdateEventArgs.SerialId = serialId;
            playSoundUpdateEventArgs.SoundAssetName = soundAssetName;
            playSoundUpdateEventArgs.SoundGroupName = soundGroupName;
            playSoundUpdateEventArgs.PlaySoundParams = playSoundParams;
            playSoundUpdateEventArgs.Progress = progress;
            playSoundUpdateEventArgs.UserData = userData;
            return playSoundUpdateEventArgs;
        }

        public override void Clear()
        {
            SerialId = 0;
            SoundAssetName = null;
            SoundGroupName = null;
            PlaySoundParams = null;
            Progress = 0f;
            UserData = null;
        }
    }
}
