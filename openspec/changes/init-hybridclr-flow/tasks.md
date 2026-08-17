---
change: init-hybridclr-flow
schema: spec-driven
---

# Tasks

- [x] 新建 `Assets/Project/DataTable/GamePlay.DataTable.asmdef`，包裹 Luban 表类与 Adapters
- [x] 新建 `Assets/Project/Scripts/Runtime/AOT/GamePlay.AOT.asmdef`，包裹 AOT 运行时代码，引用 GameFrameWork.RunTime / YooAsset / UniTask / R3 / VContainer / TMPro / UnityEngine.UI / HybridCLR.Runtime / GamePlay.DataTable
- [x] 更新 `HotUpdate.asmdef` 引用列表，追加 GamePlay.AOT、GamePlay.DataTable 与 UnityEngine.UI（GUID 形式保持一致）
- [x] 热更版 `ProcedureMain` 改造为 `ProcedureBase` 子类（`namespace GamePlay`），`OnEnter` 内调用 `ProcedureSceneSwitch.SwitchAsync` 加载场景组，`OnLeave` 取消
- [x] 新增 `Assets/Project/Scripts/Runtime/AOT/Procedure/ProcedureHotUpdateInit.cs`：Editor 直接取 HotUpdate 程序集；真机 YooAsset 加载 `HotUpdate.dll.bytes` + 遍历 AotMetadata 加载元数据 + 反射创建热更 `ProcedureMain` 实例并注册进 Procedure Fsm；完成后切到 DataTableInit
- [x] 修改 `ProcedureDataTableInit.cs`：配表加载完成后 `ChangeState(procedureOwner, hotMainType)` 切换到已注册的热更流程
- [x] 修改 `ProcedureAssetInit.OnPatchSucceeded`：补丁完成后切到 `ProcedureHotUpdateInit` 而非直接进 DataTableInit
- [x] `GameFrameWork.RunTime` 增加动态注册能力：`Fsm.AddState` / `IProcedureManager.AddProcedure` / `ProcedureManager.AddProcedure` / `ProcedureComponent.AddProcedure`
- [ ] 编辑器手动配置：GameFrameWork 场景的 ProcedureComponent 流程列表中移除已不存在的 AOT `GamePlay.ProcedureMain`、添加 `GamePlay.ProcedureHotUpdateInit`
- [ ] Unity 中验证编译通过、Editor 模拟模式下启动链路走通、热更主流程正常加载场景组
