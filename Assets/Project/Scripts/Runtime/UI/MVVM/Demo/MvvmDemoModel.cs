using System;

namespace UI.Mvvm.Demo
{
    /// <summary>
    /// <see cref="IMvvmDemoModel"/> 默认实现。
    /// </summary>
    public sealed class MvvmDemoModel : IMvvmDemoModel
    {
        public int Count { get; private set; }

        public event Action Changed;

        public void Increment()
        {
            Count++;
            Changed?.Invoke();
        }

        public void Reset()
        {
            Count = 0;
            Changed?.Invoke();
        }
    }
}
