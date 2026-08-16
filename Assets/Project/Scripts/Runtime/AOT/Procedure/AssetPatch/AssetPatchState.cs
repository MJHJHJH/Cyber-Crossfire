using GameFramework.Fsm;

namespace GamePlay.AssetPatch
{
    public abstract class AssetPatchState : FsmState<AssetPatchContext>
    {
        public void Goto<TState>(IFsm<AssetPatchContext> fsm) where TState : FsmState<AssetPatchContext>
        {
            ChangeState<TState>(fsm);
        }
    }
}
