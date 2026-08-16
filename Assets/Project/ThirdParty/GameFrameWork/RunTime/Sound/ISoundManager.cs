using System;

namespace GameFramework.Sound
{
    /// <summary>
    /// 声音管理器接口。
    /// </summary>
    public interface ISoundManager
    {
        /// <summary>
        /// 获取声音组数量。
        /// </summary>
        int SoundGroupCount { get; }

        /// <summary>
        /// 播放声音成功事件。
        /// </summary>
        event EventHandler<PlaySoundSuccessEventArgs> PlaySoundSuccess;

        /// <summary>
        /// 播放声音失败事件。
        /// </summary>
        event EventHandler<PlaySoundFailureEventArgs> PlaySoundFailure;

        /// <summary>
        /// 播放声音更新事件。
        /// </summary>
        event EventHandler<PlaySoundUpdateEventArgs> PlaySoundUpdate;

        /// <summary>
        /// 播放声音时加载依赖资源事件。
        /// </summary>
        event EventHandler<PlaySoundDependencyAssetEventArgs> PlaySoundDependencyAsset;

        /// <summary>
        /// 设置 Resources 声音资源加载器。
        /// </summary>
        /// <param name="soundAssetLoader">声音资源加载器。</param>
        void SetResourcesLoader(ISoundAssetLoader soundAssetLoader);

        /// <summary>
        /// 设置 YooAsset 声音资源加载器。
        /// </summary>
        /// <param name="soundAssetLoader">声音资源加载器。</param>
        void SetYooAssetLoader(ISoundAssetLoader soundAssetLoader);

        /// <summary>
        /// 设置声音辅助器。
        /// </summary>
        /// <param name="soundHelper">声音辅助器。</param>
        void SetSoundHelper(ISoundHelper soundHelper);

        /// <summary>
        /// 是否存在指定声音组。
        /// </summary>
        bool HasSoundGroup(string soundGroupName);

        /// <summary>
        /// 获取指定声音组。
        /// </summary>
        ISoundGroup GetSoundGroup(string soundGroupName);

        /// <summary>
        /// 获取所有声音组。
        /// </summary>
        ISoundGroup[] GetAllSoundGroups();

        /// <summary>
        /// 增加声音组。
        /// </summary>
        bool AddSoundGroup(string soundGroupName, bool soundGroupAvoidBeingReplacedBySamePriority, bool soundGroupMute, float soundGroupVolume, ISoundGroupHelper soundGroupHelper);

        /// <summary>
        /// 增加声音代理辅助器。
        /// </summary>
        void AddSoundAgentHelper(string soundGroupName, ISoundAgentHelper soundAgentHelper);

        /// <summary>
        /// 是否正在加载声音。
        /// </summary>
        bool IsLoadingSound(int serialId);

        /// <summary>
        /// 播放声音。
        /// </summary>
        /// <param name="soundAssetName">声音资源名称。</param>
        /// <param name="soundGroupName">声音组名称。</param>
        /// <param name="loadKind">资源加载方式，默认 YooAsset。</param>
        /// <returns>声音的序列编号。</returns>
        int PlaySound(string soundAssetName, string soundGroupName, SoundLoadKind loadKind = SoundLoadKind.YooAsset);

        /// <summary>
        /// 播放声音。
        /// </summary>
        int PlaySound(string soundAssetName, string soundGroupName, int priority, SoundLoadKind loadKind = SoundLoadKind.YooAsset);

        /// <summary>
        /// 播放声音。
        /// </summary>
        int PlaySound(string soundAssetName, string soundGroupName, PlaySoundParams playSoundParams, SoundLoadKind loadKind = SoundLoadKind.YooAsset);

        /// <summary>
        /// 播放声音。
        /// </summary>
        int PlaySound(string soundAssetName, string soundGroupName, PlaySoundParams playSoundParams, object userData, SoundLoadKind loadKind = SoundLoadKind.YooAsset);

        /// <summary>
        /// 播放声音。
        /// </summary>
        int PlaySound(string soundAssetName, string soundGroupName, object userData, SoundLoadKind loadKind = SoundLoadKind.YooAsset);

        /// <summary>
        /// 播放声音。
        /// </summary>
        int PlaySound(string soundAssetName, string soundGroupName, int priority, PlaySoundParams playSoundParams, object userData, SoundLoadKind loadKind = SoundLoadKind.YooAsset);

        /// <summary>
        /// 停止播放声音。
        /// </summary>
        bool StopSound(int serialId);

        /// <summary>
        /// 停止播放声音。
        /// </summary>
        bool StopSound(int serialId, float fadeOutSeconds);

        /// <summary>
        /// 按资源名停止播放声音（停止所有匹配该 name 的声音）。
        /// </summary>
        bool StopSound(string soundAssetName);

        /// <summary>
        /// 按资源名停止播放声音（停止所有匹配该 name 的声音）。
        /// </summary>
        bool StopSound(string soundAssetName, float fadeOutSeconds);

        /// <summary>
        /// 停止所有已加载的声音。
        /// </summary>
        void StopAllLoadedSounds();

        /// <summary>
        /// 停止所有已加载的声音。
        /// </summary>
        void StopAllLoadedSounds(float fadeOutSeconds);

        /// <summary>
        /// 停止所有正在加载的声音。
        /// </summary>
        void StopAllLoadingSounds();

        /// <summary>
        /// 暂停播放声音。
        /// </summary>
        void PauseSound(int serialId);

        /// <summary>
        /// 暂停播放声音。
        /// </summary>
        void PauseSound(int serialId, float fadeOutSeconds);

        /// <summary>
        /// 恢复播放声音。
        /// </summary>
        void ResumeSound(int serialId);

        /// <summary>
        /// 恢复播放声音。
        /// </summary>
        void ResumeSound(int serialId, float fadeInSeconds);
    }
}
