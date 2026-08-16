namespace GameFramework.Sound
{
    /// <summary>
    /// 播放声音时加载依赖资源事件。
    /// </summary>
    public sealed class PlaySoundDependencyAssetEventArgs : GameFrameworkEventArgs
    {
        public PlaySoundDependencyAssetEventArgs()
        {
            SerialId = 0;
            SoundAssetName = null;
            SoundGroupName = null;
            PlaySoundParams = null;
            DependencyAssetName = null;
            LoadedCount = 0;
            TotalCount = 0;
            UserData = null;
        }

        public int SerialId { get; private set; }
        public string SoundAssetName { get; private set; }
        public string SoundGroupName { get; private set; }
        public PlaySoundParams PlaySoundParams { get; private set; }
        public string DependencyAssetName { get; private set; }
        public int LoadedCount { get; private set; }
        public int TotalCount { get; private set; }
        public object UserData { get; private set; }

        public static PlaySoundDependencyAssetEventArgs Create(int serialId, string soundAssetName, string soundGroupName, PlaySoundParams playSoundParams, string dependencyAssetName, int loadedCount, int totalCount, object userData)
        {
            PlaySoundDependencyAssetEventArgs playSoundDependencyAssetEventArgs = ReferencePool.Acquire<PlaySoundDependencyAssetEventArgs>();
            playSoundDependencyAssetEventArgs.SerialId = serialId;
            playSoundDependencyAssetEventArgs.SoundAssetName = soundAssetName;
            playSoundDependencyAssetEventArgs.SoundGroupName = soundGroupName;
            playSoundDependencyAssetEventArgs.PlaySoundParams = playSoundParams;
            playSoundDependencyAssetEventArgs.DependencyAssetName = dependencyAssetName;
            playSoundDependencyAssetEventArgs.LoadedCount = loadedCount;
            playSoundDependencyAssetEventArgs.TotalCount = totalCount;
            playSoundDependencyAssetEventArgs.UserData = userData;
            return playSoundDependencyAssetEventArgs;
        }

        public override void Clear()
        {
            SerialId = 0;
            SoundAssetName = null;
            SoundGroupName = null;
            PlaySoundParams = null;
            DependencyAssetName = null;
            LoadedCount = 0;
            TotalCount = 0;
            UserData = null;
        }
    }
}
