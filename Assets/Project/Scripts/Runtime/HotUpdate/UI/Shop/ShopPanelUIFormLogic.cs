using GameFramework;
using R3;
using UI.Mvvm;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 武器商店 View：R3 绑定；业务由 <see cref="ShopPanelViewModel"/> 处理。
/// </summary>
public sealed class ShopPanelUIFormLogic : MvvmUIFormLogic<ShopPanelViewModel>
{
    private static readonly Color LockedWeaponColor = new Color(0.3f, 0.3f, 0.3f, 0.7f);

    [SerializeField] private Image[] m_WeaponButtons;
    [SerializeField] private Image[] m_WeaponImages;
    [SerializeField] private Text[] m_WeaponPrices;

    protected override void OnBind(ShopPanelViewModel viewModel)
    {
        viewModel.Initialize();

        AddBinding(viewModel.WeaponSlots.Subscribe(slots =>
        {
            if (slots == null)
                return;

            for (int i = 0; i < slots.Length; i++)
                ApplyWeaponSlot(i, slots[i]);
        }));

        AddBinding(viewModel.CloseRequested.Subscribe(_ => RequestClose()));
    }

    public void BtnWeapon(int num) => ViewModel?.SelectWeapon(num);

    public void BtnBack() => ViewModel?.RequestClose();

    private void ApplyWeaponSlot(int index, WeaponSlotUiState state)
    {
        if (index >= 0 && m_WeaponButtons != null && index < m_WeaponButtons.Length && m_WeaponButtons[index] != null)
            m_WeaponButtons[index].color = state.Selected ? Color.green : Color.white;

        if (index >= 0 && m_WeaponImages != null && index < m_WeaponImages.Length && m_WeaponImages[index] != null)
            m_WeaponImages[index].color = state.Unlocked ? Color.white : LockedWeaponColor;

        if (index >= 0 && m_WeaponPrices != null && index < m_WeaponPrices.Length && m_WeaponPrices[index] != null)
        {
            m_WeaponPrices[index].gameObject.SetActive(!state.Unlocked);
            if (!state.Unlocked)
                m_WeaponPrices[index].text = state.Price.ToString();
        }
    }

    private void RequestClose()
    {
        if (UIForm != null)
            GameFrameWork.UI?.Close(UIForm);
    }
}
