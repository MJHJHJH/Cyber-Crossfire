using CommandoRobot.ScriptableObjects;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace GamePlay
{
    /// <summary>
    /// HybridCLR 之后装配热更 DI：Game Scope 注册领域实例，Ui Scope 注册 Presenter。
    /// </summary>
    public static class GameDiInstaller
    {
        public static void EnsureInitialized()
        {
            var app = LifetimeScope.Find<AppLifetimeScope>() as AppLifetimeScope;
            var ui = LifetimeScope.Find<UiLifetimeScope>() as UiLifetimeScope;
            if (app == null || ui == null)
            {
                Debug.LogError("[GameDI] AppLifetimeScope or UiLifetimeScope is missing.");
                return;
            }

            if (app.Container == null)
            {
                Debug.LogError("[GameDI] AppLifetimeScope is not built.");
                return;
            }

            EnsureGameScope(app);
            EnsureUiScope(ui);
        }

        private static void EnsureGameScope(AppLifetimeScope app)
        {
            if (LifetimeScope.Find<GameLifetimeScope>() != null)
                return;

            var gameplay = app.GameplayDataAsset as GameplayData;
            if (gameplay == null)
            {
                Debug.LogError("[GameDI] Assign GameplayData on AppLifetimeScope.");
                return;
            }

            app.CreateChild<GameLifetimeScope>(builder =>
            {
                builder.RegisterInstance(gameplay);
            }, "GameLifetimeScope");
        }

        private static void EnsureUiScope(UiLifetimeScope ui)
        {
            if (ui.Container != null)
                return;

            if (LifetimeScope.Find<GameLifetimeScope>()?.Container == null)
            {
                Debug.LogError("[GameDI] GameLifetimeScope is not built; skip Ui Scope.");
                return;
            }

            using (LifetimeScope.Enqueue(InstallPresenters))
            {
                ui.Build();
            }
        }

        private static void InstallPresenters(IContainerBuilder builder)
        {
            builder.Register<MainHUDPresenter>(Lifetime.Transient);
            builder.Register<ShopPanelViewModel>(Lifetime.Transient);
            builder.Register<GameUI_PCPresenter>(Lifetime.Transient);
            builder.Register<PauseUiPresenter>(Lifetime.Transient);
            builder.Register<WinUiPresenter>(Lifetime.Transient);
            builder.Register<LoseUiPresenter>(Lifetime.Transient);
            builder.Register<Message1Presenter>(Lifetime.Transient);
            builder.Register<GemRewardUIPresenter>(Lifetime.Transient);
        }
    }
}
