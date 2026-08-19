using VContainer;
using VContainer.Unity;

/// <summary>
/// UI 层 DI 入口（子 Scope）：注册 Presenter。
/// 延迟 Build，等 HybridCLR 之后先建 Game Scope，再挂 Presenter。
/// 池化 UIFormLogic 不走 Inject，只 Resolve。
/// </summary>
public sealed class UiLifetimeScope : LifetimeScope
{
    protected override void Awake()
    {
        autoRun = false;
        base.Awake();
    }

    protected override LifetimeScope FindParent()
    {
        var game = Find<GameLifetimeScope>();
        return game != null ? game : Find<AppLifetimeScope>();
    }

    protected override void Configure(IContainerBuilder builder)
    {
    }
}
