using System;
using Cysharp.Threading.Tasks;
using CommandoRobot.ScriptableObjects;
using GamePlay;
using GamePlay.Data;
using R3;
using UI.Mvp;
using UnityEngine;
using GameFramework;

public sealed class MainHUDPresenter : PanelPresenter<IMainHUDView>
{
    private readonly GameplayData _gameplayData;
    private IDisposable _saveSubscription;

    public MainHUDPresenter(GameplayData gameplayData)
    {
        _gameplayData = gameplayData;
    }

    protected override void OnAttach()
    {
        PlayerSave.EnsureLoaded();
        View.ShowMainPanel();
        Refresh();
        _saveSubscription = PlayerSave.Changed.Subscribe(_ => Refresh());
    }

    protected override void OnDetach()
    {
        _saveSubscription?.Dispose();
        _saveSubscription = null;
        base.OnDetach();
    }

    public void OnPlayClick()
    {
        if (!IsAttached)
            return;

        View.ShowLevelPanel();
    }

    public void OnArmoryClick()
    {
        if (!IsAttached)
            return;

        if (GameFrameWork.UI == null)
        {
            Debug.LogError("[MainHUD] UI component is missing.");
            return;
        }

        GameFrameWork.UI.OpenAsync(UIPanelIds.ShopPanel).Forget();
    }

    public void OnBackClick()
    {
        if (!IsAttached)
            return;

        View.ShowMainPanel();
    }

    public void OnLevelClick(int index)
    {
        if (!IsAttached)
            return;

        _gameplayData.LevelNumber = index;
        if (!LevelSceneLocations.TryGet(index, out string location))
        {
            Debug.LogError($"[MainHUD] Invalid level index: {index}");
            return;
        }

        ProcedureNavigator.EnterBattle(location);
    }

    public void OnExitClick()
    {
        Application.Quit();
    }

    private void Refresh()
    {
        View.SetCoinText(PlayerSave.Coin);
    }
}
