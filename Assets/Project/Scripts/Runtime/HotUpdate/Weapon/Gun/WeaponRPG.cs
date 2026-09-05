namespace CommandoRobot
{
    /// <summary>火箭筒。开火抖动暂关。</summary>
    public class WeaponRPG : WeaponBase
    {
        protected override void OnFireFeedback()
        {
            // 开火抖动暂关（原：Range(0.28f, 0.15f, 0.28f, 24f, 34f)）
        }
    }
}
