namespace CommandoRobot
{
    /// <summary>霰弹枪：扇形多弹道。开火抖动暂关。</summary>
    public class WeaponShotgun : WeaponBase
    {
        protected override void OnFireFeedback()
        {
            // 开火抖动暂关（原：Range(0.24f, 0.12f, 0.22f, 28f, 38f)）
        }

        protected override void OnFire()
        {
            if (ProjectilePrefab != null)
            {
                for (int i = -2; i < 3; i++)
                    CreateProjectile(i * 3f);
            }

            CreateParticle();
        }

        protected override void OnFirePowered()
        {
            if (ProjectilePrefab != null)
            {
                for (int i = -4; i < 5; i++)
                    CreateProjectile(i * 4f);
            }

            CreateParticle();
        }
    }
}
