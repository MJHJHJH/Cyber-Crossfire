using System;
using GamePlay.Data;
using R3;
using UnityEngine;

namespace CommandoRobot
{
    [Serializable]
    public struct WeaponPreviewEntry
    {
        public WeaponId WeaponId;
        public GameObject Model;
    }

    /// <summary>
    /// MainMenu 展示用：按存档选中武器切换 gunpoint 下的预摆模型。
    /// 通过 PlayerSave.Changed 与商城 UI 解耦。
    /// </summary>
    public class MainMenuWeaponPreview : MonoBehaviour
    {
        [SerializeField] WeaponPreviewEntry[] _weaponModels;

        IDisposable _subscription;

        void OnEnable()
        {
            PlayerSave.EnsureLoaded();
            Apply(PlayerSave.SelectedWeapon);
            _subscription = PlayerSave.Changed.Subscribe(_ => Apply(PlayerSave.SelectedWeapon));
        }

        void OnDisable()
        {
            _subscription?.Dispose();
            _subscription = null;
        }

        void Apply(int selected)
        {
            if (_weaponModels == null)
                return;

            for (int i = 0; i < _weaponModels.Length; i++)
            {
                GameObject model = _weaponModels[i].Model;
                if (model == null)
                    continue;

                model.SetActive((int)_weaponModels[i].WeaponId == selected);
            }
        }
    }
}
