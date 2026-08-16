namespace GameFramework.Sound
{
    internal sealed partial class SoundManager : GameFrameworkModule, ISoundManager
    {
        private sealed class PlaySoundInfo : IReference
        {
            private int m_SerialId;
            private SoundGroup m_SoundGroup;
            private PlaySoundParams m_PlaySoundParams;
            private object m_UserData;

            public PlaySoundInfo()
            {
                m_SerialId = 0;
                m_SoundGroup = null;
                m_PlaySoundParams = null;
                m_UserData = null;
            }

            public int SerialId => m_SerialId;
            public SoundGroup SoundGroup => m_SoundGroup;
            public PlaySoundParams PlaySoundParams => m_PlaySoundParams;
            public object UserData => m_UserData;

            public static PlaySoundInfo Create(int serialId, SoundGroup soundGroup, PlaySoundParams playSoundParams, object userData)
            {
                PlaySoundInfo playSoundInfo = ReferencePool.Acquire<PlaySoundInfo>();
                playSoundInfo.m_SerialId = serialId;
                playSoundInfo.m_SoundGroup = soundGroup;
                playSoundInfo.m_PlaySoundParams = playSoundParams;
                playSoundInfo.m_UserData = userData;
                return playSoundInfo;
            }

            public void Clear()
            {
                m_SerialId = 0;
                m_SoundGroup = null;
                m_PlaySoundParams = null;
                m_UserData = null;
            }
        }
    }
}
