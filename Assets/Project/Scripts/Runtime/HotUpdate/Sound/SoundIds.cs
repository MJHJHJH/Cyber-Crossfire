namespace GamePlay
{
    /// <summary>
    /// 音效配表 ID 常量。武器开火音效由 TbWeapon.fire_sound 引用，勿在此硬编码枪种 ID。
    /// </summary>
    public static class SoundIds
    {
        public const int HomeBgm = 1001;
        public const int BattleBgm = 1002;

        /// <summary>敌方死亡（Enemy Explosion 1）。</summary>
        public const int EnemyExplosion = 2004;
        public const int EnemyHit = 2006; // 预留：敌方受击（当前子弹命中不播）

        /// <summary>敌方子弹命中玩家（配表 Player Projectile Hit）。</summary>
        public const int PlayerHit = 2102;
        public const int PlayerDead = 2103;

        public const float BgmFadeSeconds = 1f;
    }
}
