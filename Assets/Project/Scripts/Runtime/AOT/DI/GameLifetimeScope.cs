using UnityEngine;
using VContainer;
using VContainer.Unity;

/// <summary>
/// 热更领域 Scope：HybridCLR 之后由 App.CreateChild 创建，
/// 注册 GameplayData 等热更类型。Ui Scope 向其解析领域依赖。
/// </summary>
[DefaultExecutionOrder(-5050)]
public sealed class GameLifetimeScope : LifetimeScope
{
    protected override LifetimeScope FindParent()
    {
        return Find<AppLifetimeScope>();
    }

    protected override void Configure(IContainerBuilder builder)
    {
    }
}
