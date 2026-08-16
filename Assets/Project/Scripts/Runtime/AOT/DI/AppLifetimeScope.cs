using UnityEngine;
using VContainer;
using VContainer.Unity;

/// <summary>
/// 应用 / 数据层 DI 入口（父 Scope）：注册 Model、领域 Service。
/// UI Scope 应作为其子 Scope，向父解析领域依赖。
/// </summary>
[DefaultExecutionOrder(-5100)]
public sealed class AppLifetimeScope : LifetimeScope
{
    protected override void Awake()
    {
        base.Awake();
    }

    protected override void Configure(IContainerBuilder builder)
    {

    }
}
