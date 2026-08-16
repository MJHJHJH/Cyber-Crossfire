using UnityEngine;
using VContainer;
using VContainer.Unity;

/// <summary>
/// UI 层 DI 入口（子 Scope）：注册 Presenter / ViewModel。
/// 父为 <see cref="AppLifetimeScope"/>（领域 Model / Service）；池化 UIFormLogic 不走 Inject，只 Resolve 中介。
/// </summary>
public sealed class UiLifetimeScope : LifetimeScope
{
    protected override void Awake()
    {
        base.Awake();
    }

    protected override LifetimeScope FindParent()
    {
        return Find<AppLifetimeScope>();
    }

    protected override void Configure(IContainerBuilder builder)
    {
       
    }
}
