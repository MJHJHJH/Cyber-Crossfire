using System.Collections.Generic;
using GameFramework;
using GamePlay;
using R3;
using UnityEngine;

namespace GamePlay.Data
{
    /// <summary>
    /// 玩家存档门面：业务字段读写 + 关键操作立即落盘。
    /// </summary>
    public static class PlayerSave
    {
        public const string DataKey = "player";
        private const int FallbackWeaponCount = 6;

        private static PlayerSaveData _data;
        private static bool _loaded;
        private static readonly Subject<Unit> s_Changed = new();

        public static Observable<Unit> Changed => s_Changed;

        public static int Coin => EnsureLoaded().Coin;

        public static int LevelUnlocked => EnsureLoaded().LevelUnlocked;

        public static int SelectedWeapon => EnsureLoaded().SelectedWeapon;

        public static bool[] WeaponsUnlocked => EnsureLoaded().WeaponsUnlocked;

        public static PlayerSaveData EnsureLoaded()
        {
            if (_loaded && _data != null)
                return _data;

            if (GameFrameWork.Data == null)
            {
                Debug.LogError("[PlayerSave] GameFrameWork.Data is missing.");
                _data = CreateDefault();
                _loaded = true;
                return _data;
            }

            if (!GameFrameWork.Data.IsLoaded)
                GameFrameWork.Data.Load();

            bool createdNew = false;
            if (GameFrameWork.Data.TryGetObject(DataKey, out PlayerSaveData saved) && saved != null)
                _data = saved;
            else
            {
                _data = CreateDefault();
                createdNew = true;
            }

            bool normalized = NormalizeWeapons(_data);
            _loaded = true;
            // 仅新建或结构对齐时标脏；平时读档不写盘
            if (createdNew || normalized)
                MarkDirty();
            return _data;
        }

        /// <summary>将内存存档刷到磁盘。仅在离开战斗、进出流程、退出等关键节点调用。</summary>
        public static void Save()
        {
            MarkDirty();
            GameFrameWork.Data?.Save();
        }

        public static void Reset()
        {
            _data = CreateDefault();
            _loaded = true;
            Save();
        }

        public static void AddCoin(int amount)
        {
            PlayerSaveData data = EnsureLoaded();
            if (amount == 0)
                return;

            data.Coin += amount;
            if (data.Coin < 0)
                data.Coin = 0;

            MarkDirty();
        }

        public static bool TryUnlockWeapon(int index)
        {
            PlayerSaveData data = EnsureLoaded();
            if (!IsValidWeaponIndex(index))
                return false;

            if (data.WeaponsUnlocked[index])
            {
                data.SelectedWeapon = index;
                MarkDirty();
                return true;
            }

            if (!TryGetWeaponPrice(index, out int price))
                return false;

            if (data.Coin < price)
                return false;

            data.Coin -= price;
            data.WeaponsUnlocked[index] = true;
            data.SelectedWeapon = index;
            MarkDirty();
            return true;
        }

        public static void SelectWeapon(int index)
        {
            PlayerSaveData data = EnsureLoaded();
            if (!IsValidWeaponIndex(index) || !data.WeaponsUnlocked[index])
                return;

            data.SelectedWeapon = index;
            MarkDirty();
        }

        public static bool IsWeaponUnlocked(int index)
        {
            PlayerSaveData data = EnsureLoaded();
            return IsValidWeaponIndex(index) && data.WeaponsUnlocked[index];
        }

        public static int GetWeaponPrice(int index)
        {
            return TryGetWeaponPrice(index, out int price) ? price : 0;
        }

        public static void SetLevelUnlocked(int level)
        {
            // 当前不做关卡锁定；保留 API 与字段以兼容存档结构
            PlayerSaveData data = EnsureLoaded();
            if (level < 0)
                level = 0;

            if (level <= data.LevelUnlocked)
                return;

            data.LevelUnlocked = level;
            MarkDirty();
        }

        public static bool CanEnterLevel(int index)
        {
            return index >= 0;
        }

        private static void MarkDirty()
        {
            if (_data == null)
                return;

            if (GameFrameWork.Data != null)
                GameFrameWork.Data.SetObject(DataKey, _data);

            s_Changed.OnNext(Unit.Default);
        }

        private static PlayerSaveData CreateDefault()
        {
            PlayerSaveData data = new PlayerSaveData
            {
                Coin = 0,
                LevelUnlocked = 0,
                SelectedWeapon = 0,
                WeaponsUnlocked = new bool[GetWeaponCount()]
            };
            if (data.WeaponsUnlocked.Length > 0)
                data.WeaponsUnlocked[0] = true;
            return data;
        }

        /// <returns>是否改动了结构/默认槽，需要标脏。</returns>
        private static bool NormalizeWeapons(PlayerSaveData data)
        {
            bool changed = false;
            int count = GetWeaponCount();
            if (data.WeaponsUnlocked == null || data.WeaponsUnlocked.Length != count)
            {
                bool[] next = new bool[count];
                if (data.WeaponsUnlocked != null)
                {
                    int copy = Mathf.Min(data.WeaponsUnlocked.Length, count);
                    for (int i = 0; i < copy; i++)
                        next[i] = data.WeaponsUnlocked[i];
                }

                if (count > 0)
                    next[0] = true;
                data.WeaponsUnlocked = next;
                changed = true;
            }
            else if (count > 0 && !data.WeaponsUnlocked[0])
            {
                data.WeaponsUnlocked[0] = true;
                changed = true;
            }

            if (data.SelectedWeapon < 0 || data.SelectedWeapon >= count || !data.WeaponsUnlocked[data.SelectedWeapon])
            {
                data.SelectedWeapon = 0;
                changed = true;
            }

            return changed;
        }

        private static int GetWeaponCount()
        {
            if (GameFrameWork.DataTable != null &&
                GameFrameWork.DataTable.TryGetTable(out IWeapon table) &&
                table != null)
            {
                int shopCount = 0;
                IReadOnlyList<Weapon_Record> all = table.All;
                for (int i = 0; i < all.Count; i++)
                {
                    if (all[i].InShop)
                        shopCount++;
                }

                if (shopCount > 0)
                    return shopCount;
            }

            return FallbackWeaponCount;
        }

        private static bool TryGetWeaponPrice(int index, out int price)
        {
            price = 0;
            if (GameFrameWork.DataTable == null ||
                !GameFrameWork.DataTable.TryGetTable(out IWeapon table) ||
                table == null)
            {
                return false;
            }

            if (!table.TryGet(index, out Weapon_Record record) || !record.InShop)
                return false;

            price = record.Price;
            return true;
        }

        private static bool IsValidWeaponIndex(int index)
        {
            PlayerSaveData data = EnsureLoaded();
            return index >= 0 && data.WeaponsUnlocked != null && index < data.WeaponsUnlocked.Length;
        }
    }
}
