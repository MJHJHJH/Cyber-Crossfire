using GameFramework.Event;

namespace GameFramework
{
    /// <summary>
    /// 播放声音成功事件（经 EventComponent 转发的运行时包装）。
    /// </summary>
    public sealed class PlaySoundSuccessEventArgs : GameEventArgs
    {
        public static readonly int EventId = typeof(PlaySoundSuccessEventArgs).GetHashCode();

        public override int Id => EventId;

        public int SerialId => _args.SerialId;
        public string SoundAssetName => _args.SoundAssetName;
        public GameFramework.Sound.ISoundAgent SoundAgent => _args.SoundAgent;
        public float Duration => _args.Duration;
        public object UserData => _args.UserData;

        private GameFramework.Sound.PlaySoundSuccessEventArgs _args;

        public PlaySoundSuccessEventArgs()
        {
            _args = null;
        }

        public static PlaySoundSuccessEventArgs Create(GameFramework.Sound.PlaySoundSuccessEventArgs args)
        {
            PlaySoundSuccessEventArgs playSoundSuccessEventArgs = ReferencePool.Acquire<PlaySoundSuccessEventArgs>();
            playSoundSuccessEventArgs._args = args;
            return playSoundSuccessEventArgs;
        }

        public override void Clear()
        {
            _args = null;
        }
    }
}
