using GameFramework;

namespace GamePlay
{
    /// <summary>
    /// 基于 <see cref="ISound"/> 配表的音效配置 Provider（SoundComponent 按 ID 播放用）。
    /// </summary>
    public sealed class SoundConfigProvider : ISoundConfigTable
    {
        public bool TryGet(int soundId, out SoundConfig config)
        {
            config = default;
            if (GameFrameWork.DataTable == null ||
                !GameFrameWork.DataTable.TryGetTable(out ISound table) ||
                !table.TryGet(soundId, out Sound_Record record))
            {
                return false;
            }

            config = new SoundConfig(
                record.Id,
                record.Location,
                record.Group,
                record.Loop,
                record.Volume,
                record.Priority,
                record.Mute,
                record.FadeInSeconds,
                record.Pitch,
                record.PanStereo,
                record.SpatialBlend,
                record.MaxDistance,
                record.DopplerLevel);
            return true;
        }
    }
}
