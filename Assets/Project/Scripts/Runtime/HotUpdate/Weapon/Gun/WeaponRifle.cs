namespace CommandoRobot
{
    /// <summary>步枪。开火抖动暂关。</summary>
    public class WeaponRifle : WeaponBase
    {
        protected override void OnFireFeedback()
        {
            // 开火抖动暂关（原：Range(0.16f, 0.08f, 0.15f, 34f, 44f)）
        }
    }
}
