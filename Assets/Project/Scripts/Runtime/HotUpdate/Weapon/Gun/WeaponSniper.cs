namespace CommandoRobot
{
    /// <summary>狙击枪。开火抖动暂关。</summary>
    public class WeaponSniper : WeaponBase
    {
        protected override void OnFireFeedback()
        {
            // 开火抖动暂关（原：Range(0.3f, 0.18f, 0.34f, 22f, 32f)）
        }
    }
}
