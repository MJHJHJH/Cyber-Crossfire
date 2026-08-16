using GameFramework.Event;

namespace GameFramework
{
    /// <summary>
    /// 播放声音时加载依赖资源事件（经 EventComponent 转发的运行时包装）。
    /// </summary>
    public sealed class PlaySoundDependencyAssetEventArgs : GameEventArgs
    {
        public static readonly int EventId = typeof(PlaySoundDependencyAssetEventArgs).GetHashCode();

        public override int Id => EventId;

        public int SerialId => _args.SerialId;
        public string SoundAssetName => _args.SoundAssetName;
        public string SoundGroupName => _args.SoundGroupName;
        public GameFramework.Sound.PlaySoundParams PlaySoundParams => _args.PlaySoundParams;
        public string DependencyAssetName => _args.DependencyAssetName;
        public int LoadedCount => _args.LoadedCount;
        public int TotalCount => _args.TotalCount;
        public object UserData => _args.UserData;

        private GameFramework.Sound.PlaySoundDependencyAssetEventArgs _args;

        public PlaySoundDependencyAssetEventArgs()
        {
            _args = null;
        }

        public static PlaySoundDependencyAssetEventArgs Create(GameFramework.Sound.PlaySoundDependencyAssetEventArgs args)
        {
            PlaySoundDependencyAssetEventArgs playSoundDependencyAssetEventArgs = ReferencePool.Acquire<PlaySoundDependencyAssetEventArgs>();
            playSoundDependencyAssetEventArgs._args = args;
            return playSoundDependencyAssetEventArgs;
        }

        public override void Clear()
        {
            _args = null;
        }
    }
}
