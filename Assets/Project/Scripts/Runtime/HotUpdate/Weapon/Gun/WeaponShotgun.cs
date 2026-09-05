namespace CommandoRobot
{
    /// <summary>霰弹枪：扇形多弹道。</summary>
    public class WeaponShotgun : WeaponBase
    {
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
