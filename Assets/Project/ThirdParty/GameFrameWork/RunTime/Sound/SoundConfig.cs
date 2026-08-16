using System.Collections.Generic;

namespace GameFramework
{
    /// <summary>
    /// 音效配置（配表 → 播放参数），供 SoundComponent 按 ID 播放。
    /// </summary>
    public readonly struct SoundConfig
    {
        public int Id { get; }
        public string Location { get; }
        public string Group { get; }
        public bool Loop { get; }
        public float Volume { get; }
        public int Priority { get; }
        public bool Mute { get; }
        public float FadeInSeconds { get; }
        public float Pitch { get; }
        public float PanStereo { get; }
        public float SpatialBlend { get; }
        public float MaxDistance { get; }
        public float DopplerLevel { get; }

        public SoundConfig(
            int id,
            string location,
            string group,
            bool loop,
            float volume,
            int priority,
            bool mute,
            float fadeInSeconds,
            float pitch,
            float panStereo,
            float spatialBlend,
            float maxDistance,
            float dopplerLevel)
        {
            Id = id;
            Location = location;
            Group = group;
            Loop = loop;
            Volume = volume;
            Priority = priority;
            Mute = mute;
            FadeInSeconds = fadeInSeconds;
            Pitch = pitch;
            PanStereo = panStereo;
            SpatialBlend = spatialBlend;
            MaxDistance = maxDistance;
            DopplerLevel = dopplerLevel;
        }
    }

    /// <summary>
    /// 音效配表查询接口：由业务侧（GamePlay）实现，经 SetSoundConfigTable 注入。
    /// </summary>
    public interface ISoundConfigTable
    {
        bool TryGet(int soundId, out SoundConfig config);
    }

    /// <summary>
    /// 声音组配置（配表 → 组参数），供 SoundComponent 初始化声音组与大类 mixer 路由。
    /// </summary>
    public readonly struct SoundGroupConfig
    {
        public int Id { get; }
        public string Name { get; }
        public string MixerGroupPath { get; }
        public bool AvoidBeingReplacedBySamePriority { get; }
        public bool Mute { get; }
        public float Volume { get; }
        public int AgentCount { get; }

        public SoundGroupConfig(
            int id,
            string name,
            string mixerGroupPath,
            bool avoidBeingReplacedBySamePriority,
            bool mute,
            float volume,
            int agentCount)
        {
            Id = id;
            Name = name;
            MixerGroupPath = mixerGroupPath;
            AvoidBeingReplacedBySamePriority = avoidBeingReplacedBySamePriority;
            Mute = mute;
            Volume = volume;
            AgentCount = agentCount;
        }
    }

    /// <summary>
    /// 声音组配表查询接口：由业务侧（GamePlay）实现，经 SetSoundGroupConfigTable 注入。
    /// </summary>
    public interface ISoundGroupConfigTable
    {
        IReadOnlyList<SoundGroupConfig> All { get; }
    }
}
