using GameFramework.Event;

namespace GameFramework
{
    /// <summary>
    /// 播放声音更新事件（经 EventComponent 转发的运行时包装）。
    /// </summary>
    public sealed class PlaySoundUpdateEventArgs : GameEventArgs
    {
        public static readonly int EventId = typeof(PlaySoundUpdateEventArgs).GetHashCode();

        public override int Id => EventId;

        public int SerialId => _args.SerialId;
        public string SoundAssetName => _args.SoundAssetName;
        public string SoundGroupName => _args.SoundGroupName;
        public GameFramework.Sound.PlaySoundParams PlaySoundParams => _args.PlaySoundParams;
        public float Progress => _args.Progress;
        public object UserData => _args.UserData;

        private GameFramework.Sound.PlaySoundUpdateEventArgs _args;

        public PlaySoundUpdateEventArgs()
        {
            _args = null;
        }

        public static PlaySoundUpdateEventArgs Create(GameFramework.Sound.PlaySoundUpdateEventArgs args)
        {
            PlaySoundUpdateEventArgs playSoundUpdateEventArgs = ReferencePool.Acquire<PlaySoundUpdateEventArgs>();
            playSoundUpdateEventArgs._args = args;
            return playSoundUpdateEventArgs;
        }

        public override void Clear()
        {
            _args = null;
        }
    }
}
