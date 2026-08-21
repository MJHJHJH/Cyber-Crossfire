using System;

namespace GamePlay.Data
{
    [Serializable]
    public sealed class PlayerSaveData
    {
        public int Coin;
        public int LevelUnlocked;
        public int SelectedWeapon;
        public bool[] WeaponsUnlocked;
    }
}
