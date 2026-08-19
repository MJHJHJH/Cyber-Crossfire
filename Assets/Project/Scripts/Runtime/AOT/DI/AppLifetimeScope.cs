using UnityEngine;
using VContainer;
using VContainer.Unity;

/// <summary>
/// 应用层 DI 入口（父 Scope）。热更类型不能在此 Register；
/// DataStorage / GameplayData 仅作场景引用袋，由热更在 HybridCLR 之后注入 Game Scope。
/// </summary>
[DefaultExecutionOrder(-5100)]
public sealed class AppLifetimeScope : LifetimeScope
{
    [SerializeField] private ScriptableObject _dataStorage;
    [SerializeField] private ScriptableObject _gameplayData;

    public ScriptableObject DataStorageAsset => _dataStorage;

    public ScriptableObject GameplayDataAsset => _gameplayData;

    protected override void Configure(IContainerBuilder builder)
    {
    }
}
