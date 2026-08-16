namespace GameFramework
{
    /// <summary>
    /// 默认声音辅助器：资源释放委托给 SoundComponent 持有的资源加载器。
    /// </summary>
    public class DefaultSoundHelper : SoundHelperBase
    {
        public override void ReleaseSoundAsset(object soundAsset)
        {
            SoundComponent soundComponent = GameEntry.GetComponent<SoundComponent>();
            if (soundComponent == null)
            {
                return;
            }

            soundComponent.ReleaseAudioClip(soundAsset);
        }
    }
}
