namespace CommandoRobot
{
    /// <summary>弩。开火抖动暂关。</summary>
    public class WeaponCrossbow : WeaponBase
    {
        protected override void OnFireFeedback()
        {
            // 开火抖动暂关（原：Range(0.16f, 0.07f, 0.14f, 34f, 44f)）
        }
    }
}
