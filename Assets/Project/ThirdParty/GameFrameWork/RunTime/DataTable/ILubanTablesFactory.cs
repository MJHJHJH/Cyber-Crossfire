using System.Threading;
using Cysharp.Threading.Tasks;

namespace GameFramework
{
    /// <summary>
    /// 由 GamePlay 实现：预加载全部表并注册到 <see cref="LubanTableRegistry"/>。
    /// </summary>
    public interface ILubanTablesFactory
    {
        UniTask<LubanTableRegistry> CreateAsync(
            LubanDataMode mode,
            ILubanDataLoader loader,
            CancellationToken cancellationToken = default);
    }
}
