using GameFramework.Fsm;

namespace GamePlay.AssetPatch
{
    public sealed class PatchDoneState : AssetPatchState
    {
        protected override void OnEnter(IFsm<AssetPatchContext> fsm)
        {
            base.OnEnter(fsm);
            AssetPatchContext ctx = fsm.Owner;
            ctx.Fire(AssetPatchStepsChangeEventArgs.Create(
                "全部资源初始化完成！", ctx.DefaultPackageName));
            ctx.OnSucceeded?.Invoke();
        }
    }
}
