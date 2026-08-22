## Why

项目已有 Luban `TbSound` / `TbSoundGroup` 与 `SoundComponent.PlaySound(int)` 管线，但配表与 `Assets/Audio` 资源未对齐，业务层也几乎未触发播放，主页与战斗听感缺失。需要先把战斗 SFX 与主页/战斗 BGM 配进音效表并接到可验证的播放路径。

## What Changes

- 修订 Luban 源表 `Sound` / `SoundGroup` 数据并导出，使 `TbSound` location 与 `Assets/Audio` 下真实资源一致
- 补齐本轮音频条目：敌方/玩家战斗 SFX、主页 BGM、战斗 BGM（可选 Ambient 不在本轮必做范围）
- 在主页流程与战斗流程中按 Music ID 播放/切换 BGM
- 在开火、命中、死亡等关键事件点临时硬编码 SoundId，验证 SFX 可播（武器用哪条音效后续进武器表，本轮不改 `TbWeapon` 结构）
- 确认 YooAsset 可加载 `Assets/Audio` 对应 location（若 Collector 未收录则补收录）

## Capabilities

### New Capabilities

- `game-audio`: 音效字典配表、分组路由，以及主页/战斗 BGM 与战斗 SFX 的按 ID 播放行为

### Modified Capabilities

- （无）现有 `openspec/specs/` 中无音效相关需求需改写

## Impact

- Luban：`Tools/DataTablesTool/DataTables/Datas` 中 Sound / SoundGroup 源表；生成物 `Assets/DataTable/Data/{Json,Bin}` 与 Core（仅数据变更时通常无需改 Adapter 结构）
- 运行时：`ProcedureMain` / `ProcedureBattle`（BGM）；`WeaponBase` / `ProjectileBase` / `CharacterBase` 等战斗事件点（测试期硬编码 SFX）
- 资源：`Assets/Audio/**`；YooAsset `BundleCollectorSetting`（若需）
- 已有框架复用：`SoundComponent`、`SoundConfigProvider`、`SoundGroupConfigProvider`、AudioMixer 分组（Music/SFX/Ambient/UI）
- 明确不做：武器表挂 SoundId（后续变更）；UI 点击音；Ambient 全量接入（非本轮必做）
