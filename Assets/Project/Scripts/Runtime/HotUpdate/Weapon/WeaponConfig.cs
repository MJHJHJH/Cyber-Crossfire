using GamePlay;

namespace CommandoRobot
{
    /// <summary>
    /// 武器静态配置（表驱动）。仅保存数值与资源 location，不负责加载实例。
    /// </summary>
    public struct WeaponConfig
    {
        public WeaponId WeaponId;
        public int Price;
        public string Title;
        public string WeaponKey;
        public bool OneHanded;
        public bool AutoFire;
        public float FireDelay;
        public float RecoilSpeed;
        public float RecoilAngle;
        public int InitAmmo;
        public int MaxAmmo;
        public int AddAmmo;
        public int PowerWeaponMaxAmmo;
        public bool InfiniteAmmo;
        public float ProjectileDamage;
        public float ProjectileSpeed;
        public float ProjectileRange;
        public string ProjectilePrefabLocation;
        public string WeaponModelPrefabLocation;
        public string EffectPrefabLocation;
        public string WeaponIconLocation;
        public string GunPrefabLocation;
        /// <summary>开火音效 TbSound.id，0 表示不播放。</summary>
        public int FireSound;
        public bool InShop;

        public static WeaponConfig FromRecord(Weapon_Record record)
        {
            return new WeaponConfig
            {
                WeaponId = (WeaponId)record.Id,
                Price = record.Price,
                Title = record.Name,
                WeaponKey = record.WeaponKey,
                OneHanded = record.OneHanded,
                AutoFire = record.AutoFire,
                FireDelay = record.FireDelay,
                RecoilSpeed = record.RecoilSpeed,
                RecoilAngle = record.RecoilAngle,
                InitAmmo = record.InitAmmo,
                MaxAmmo = record.MaxAmmo,
                AddAmmo = record.AddAmmo,
                PowerWeaponMaxAmmo = record.PowerWeaponMaxAmmo,
                InfiniteAmmo = record.InfiniteAmmo,
                ProjectileDamage = record.ProjectileDamage,
                ProjectileSpeed = record.ProjectileSpeed,
                ProjectileRange = record.ProjectileRange,
                ProjectilePrefabLocation = record.ProjectilePrefab,
                WeaponModelPrefabLocation = record.WeaponModelPrefab,
                EffectPrefabLocation = record.EffectPrefab,
                WeaponIconLocation = record.WeaponIcon,
                GunPrefabLocation = record.GunPrefab,
                FireSound = record.FireSound,
                InShop = record.InShop,
            };
        }
    }
}
