using System;

namespace UI.Mvvm.Demo
{
    /// <summary>
    /// Demo 领域 Model：计数数据与变更通知；纯 C#，无 Unity / 资源依赖。
    /// </summary>
    public interface IMvvmDemoModel
    {
        int Count { get; }

        event Action Changed;

        void Increment();

        void Reset();
    }
}
