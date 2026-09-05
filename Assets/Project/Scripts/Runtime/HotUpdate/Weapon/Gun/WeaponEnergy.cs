namespace CommandoRobot
{
    /// <summary>能量枪。开火抖动暂关。</summary>
    public class WeaponEnergy : WeaponBase
    {
        protected override void OnFireFeedback()
        {
            // 开火抖动暂关（原：Range(0.22f, 0.11f, 0.2f, 30f, 40f)）
        }
    }
}
