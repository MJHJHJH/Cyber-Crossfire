using System.Collections.Generic;
using GameFramework;

namespace GamePlay
{
    /// <summary>
    /// 基于 <see cref="ISoundGroup"/> 配表的声音组配置 Provider（SoundComponent 初始化声音组与大类 mixer 路由用）。
    /// </summary>
    public sealed class SoundGroupConfigProvider : ISoundGroupConfigTable
    {
        public IReadOnlyList<SoundGroupConfig> All
        {
            get
            {
                List<SoundGroupConfig> results = new List<SoundGroupConfig>();
                if (GameFrameWork.DataTable == null || !GameFrameWork.DataTable.TryGetTable(out ISoundGroup table))
                {
                    return results;
                }

                foreach (SoundGroup_Record record in table.All)
                {
                    results.Add(new SoundGroupConfig(
                        record.Id,
                        record.Name,
                        record.MixerGroupPath,
                        record.AvoidBeingReplacedBySamePriority,
                        record.Mute,
                        record.Volume,
                        record.AgentCount));
                }

                return results;
            }
        }
    }
}
