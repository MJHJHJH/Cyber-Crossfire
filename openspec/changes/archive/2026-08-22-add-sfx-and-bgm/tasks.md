## 1. Luban 源表与导出

- [x] 1.1 确认 `Tools/DataTablesTool/DataTables/Datas` 中 Sound / SoundGroup Excel 源表可用
- [x] 1.2 修订 Sound 表：1001 location=`HomeMusic`、1002 location=`BGM_Battle`，二者 loop=true 且 fade_in_seconds=1；修正玩家 SFX 路径；保留敌方 2001–2007；删除无文件占位行
- [x] 1.3 核对 SoundGroup（Music/SFX/Ambient/UI）与 Mixer 路径、AgentCount，仅在不足时微调
- [x] 1.4 运行 `gen_all` 导出 json/bin，核对 Music location 为文件名、fade_in=1

## 2. 资源可加载性

- [x] 2.1 检查 YooAsset Collector：Music 的 address 须为文件名 `HomeMusic` / `BGM_Battle`；Characters SFX 可加载；未收录则补规则
- [x] 2.2 Play Mode 用 `PlaySound(id)` 抽查 Music 与若干 SFX（需 Unity 内手动，并入 4.4）

## 3. BGM 流程接入

- [x] 3.1 增加集中 SoundId 常量（HomeBgm=1001、BattleBgm=1002、敌方开火测试=2001 等）
- [x] 3.2 `ProcedureMain` 就绪播主页 BGM；离开时 `StopSoundById(..., 1f)`
- [x] 3.3 `ProcedureBattle` 就绪：先对旧 BGM `StopSoundById(..., 1f)`，再播战斗 BGM；离开同样 1s 淡出

## 4. 战斗 SFX 测试硬编码

- [x] 4.1 `WeaponBase` 成功开火：玩家用玩家发射 ID；敌方统一写死 2001
- [x] 4.2 子弹命中路径临时播放命中 SFX
- [x] 4.3 `HandleDeath`（或等价死亡点）临时播放死亡/爆炸 SFX
- [x] 4.4 验收：主页 BGM、进战斗 1s 淡出/淡入切曲、开火/命中/死亡有 SFX

## 5. 收尾说明

- [x] 5.1 在代码或 tasks 备注中标明：武器用音后续进 TbWeapon，本轮硬编码为临时验证
