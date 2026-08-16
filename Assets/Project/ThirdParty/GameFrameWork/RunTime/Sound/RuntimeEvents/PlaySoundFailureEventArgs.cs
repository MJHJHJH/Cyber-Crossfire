using GameFramework.Event;

namespace GameFramework
{
    /// <summary>
    /// 播放声音失败事件（经 EventComponent 转发的运行时包装）。
    /// </summary>
    public sealed class PlaySoundFailureEventArgs : GameEventArgs
    {
        public static readonly int EventId = typeof(PlaySoundFailureEventArgs).GetHashCode();

        public override int Id => EventId;

        public int SerialId => _args.SerialId;
        public string SoundAssetName => _args.SoundAssetName;
        public string SoundGroupName => _args.SoundGroupName;
        public GameFramework.Sound.PlaySoundParams PlaySoundParams => _args.PlaySoundParams;
        public GameFramework.Sound.PlaySoundErrorCode ErrorCode => _args.ErrorCode;
        public string ErrorMessage => _args.ErrorMessage;
        public object UserData => _args.UserData;

        private GameFramework.Sound.PlaySoundFailureEventArgs _args;

        public PlaySoundFailureEventArgs()
        {
            _args = null;
        }

        public static PlaySoundFailureEventArgs Create(GameFramework.Sound.PlaySoundFailureEventArgs args)
        {
            PlaySoundFailureEventArgs playSoundFailureEventArgs = ReferencePool.Acquire<PlaySoundFailureEventArgs>();
            playSoundFailureEventArgs._args = args;
            return playSoundFailureEventArgs;
        }

        public override void Clear()
        {
            _args = null;
        }
    }
}
