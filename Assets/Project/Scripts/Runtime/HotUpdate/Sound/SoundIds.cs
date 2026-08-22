namespace GamePlay
{
    /// <summary>
    /// 音效配表 ID 常量。
    /// 战斗 SFX 硬编码仅为临时验证；武器用哪条音效后续改由 TbWeapon 等业务表引用，勿在业务表未接入前扩散魔法数字。
    /// </summary>
    public static class SoundIds
    {
        public const int HomeBgm = 1001;
        public const int BattleBgm = 1002;

        /// <summary>敌方发射（Enemy 02  Projectile Launch）。</summary>
        public const int EnemyFire = 2002;

        /// <summary>敌方死亡（Enemy Explosion 1）。</summary>
        public const int EnemyExplosion = 2004;
        public const int EnemyHit = 2006; // 预留：敌方受击（当前子弹命中不播）

        public const int PlayerFire = 2101;
        /// <summary>敌方子弹命中玩家（配表 Player Projectile Hit）。</summary>
        public const int PlayerHit = 2102;
        public const int PlayerDead = 2103;

        public const float BgmFadeSeconds = 1f;
    }
}
