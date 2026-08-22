## Context

项目已具备完整声音运行时：Luban `TbSound` / `TbSoundGroup`、`SoundConfigProvider` / `SoundGroupConfigProvider`、`SoundComponent.PlaySound(int soundId)`、YooAsset 加载与 AudioMixer 分组（Music / SFX / Ambient / UI）。

当前缺口主要在数据与触发：

- `tbsound.json` 中敌方 SFX（2001–2007）路径基本正确；玩家条目 location 与磁盘文件不一致，且含不存在资源名
- Music / Ambient 资源（如 `HomeMusic`、`BGM_Battle`）未入表
- 业务几乎无 `PlaySound` 调用（`WeaponBase` 仅残留旧 `SoundGallery` 注释）
- Luban 源表目录为 `Tools/DataTablesTool/DataTables/Datas`，需改 Excel 后走 `gen_all` 导出

利益相关：策划/程序通过配表维护音效字典；玩法后续再在武器表引用 SoundId。

## Goals / Non-Goals

**Goals:**

- 以 Luban 源表为唯一数据源，对齐并补齐本轮 Sound / SoundGroup 数据后导出
- TbSound 仅作为音效字典（id、location、group、loop、音量等），不描述「谁使用」
- 主页进入后播 Home BGM；战斗进入后切战斗 BGM；流程离开时正确停止/切换
- 战斗关键事件（开火/命中/死亡等）能通过 `PlaySound(id)` 播出 SFX，测试期允许代码写死 ID
- location 可被 YooAsset 加载

**Non-Goals:**

- 不在本轮修改 `TbWeapon` 增加 `fireSoundId` 等字段（后续变更）
- 不做 UI 点击音、不强制接入 Ambient
- 不重做 Sound 框架 / Mixer 拓扑（复用现有）
- 不引入 Wwise/FMOD

## Decisions

### D1. Sound 表只管字典；业务引用后置

- **选择**：TbSound / TbSoundGroup 只描述音频资源与播放参数；武器用哪条音效后续进武器表
- **理由**：与用户确认的分层一致，避免本轮改武器表结构拖慢听感验证
- **备选**：本轮直接给 TbWeapon 加 SoundId → 拒绝，范围过大

### D2. 测试期事件点硬编码 SoundId

- **选择**：`WeaponBase` / `ProjectileBase` / `CharacterBase`（及 BGM 流程）直接 `PlaySound(常量ID)`
- **理由**：快速验证链路；常量集中便于后续替换为读武器表
- **备选**：Prefab 挂 `SoundSourceBehaviour` 全覆盖 → 开火等逻辑事件仍需代码调用，仅作补充

### D3. ID 分段约定

| 分段 | 用途 |
|------|------|
| 1xxx | Music（主页 / 战斗 BGM） |
| 20xx | 敌方 SFX |
| 21xx | 玩家 SFX |
| 3xxx | Ambient（本轮可不填） |

- **理由**：与现有 2001–2106 习惯兼容，便于常量与后续配表引用
- **备选**：按资源目录连续编号 → 可读性差，拒绝

### D4. BGM 由 Procedure 驱动，切换淡入淡出 1s

- **选择**：`ProcedureMain` 场景就绪后播主页 Music ID；`ProcedureBattle` 就绪后停旧 BGM 并播战斗 Music ID；`OnLeave` 停止本流程持有的 BGM
- **淡入淡出**：BGM 切换与停止统一使用 **1 秒**——播新曲依赖表字段 `fade_in_seconds = 1`；停旧曲调用 `StopSoundById(..., fadeOutSeconds: 1f)`（或等价 Stop）
- **理由**：BGM 与场景流程生命周期一致；Music 组 AgentCount=2 支持交叠切换
- **备选**：场景 Prefab 挂 SoundSourceBehaviour → 流程切换时卸载时机难控；0s 硬切 → 已否决

### D5. location 约定（按资源类型）

- **Music（本轮已确认）**：`HomeMusic` / `BGM_Battle` 的 YooAsset location **与文件名完全一致**（无目录前缀、无扩展名）→ 表内 location 即为 `HomeMusic`、`BGM_Battle`
- **角色 SFX**：沿用现有敌方写法，路径型 location（如 `Audio/Characters/Player/Player Projectile`，无后缀）
- **理由**：Music 与 Collector/寻址约定按文件名；SFX 已有路径型条目可复用
- **备选**：Music 也写成 `Audio/Musics/...` → 与已确认寻址不一致，拒绝

### D6. SoundGroup 本轮以核对为主

- **选择**：保留现有 Music / SFX / Ambient / UI 四组；仅在 Mixer 路径或 AgentCount 不足时微调
- **理由**：分组已覆盖本轮需求，避免无意义改表

### D7. 建议本轮 Sound 条目（实现时以 Excel 为准）

| ID | location | group | loop | fade_in_seconds |
|----|----------|-------|------|-----------------|
| 1001 | `HomeMusic` | Music | true | 1 |
| 1002 | `BGM_Battle` | Music | true | 1 |
| 2001–2007 | 现有敌方路径 | SFX | false | 0 |
| 2101 | `Audio/Characters/Player/Player Projectile` | SFX | false | 0 |
| 2102 | `Audio/Characters/Player/Player Projectile Hit` | SFX | false | 0 |
| 2103 | `Audio/Characters/Player/Player Dead` | SFX | false | 0 |

- 删除或修正表中无对应文件的玩家占位行（Dodge / Explosion / Hit1–3 等）

### D8. 测试期敌方发射音统一写死

- **选择**：敌方多种发射音（2001–2003）测试期 **统一硬编码 2001**；表内可保留 2002/2003 供后续按敌人/武器配置引用
- **理由**：本轮只验证链路，不做按敌类型分支
- **备选**：按敌人类型选 2001–2003 → 延后

## Risks / Trade-offs

- **[Risk] Datas Excel 未纳入仓库或本机缺失** → 先确认 `Tools/DataTablesTool/DataTables/Datas` 存在再改；禁止只改 json 产物冒充源表
- **[Risk] YooAsset Collector 未按「文件名」为 Music 生成 address** → 核对 Collector 规则，确保 address 为 `HomeMusic` / `BGM_Battle`
- **[Risk] 硬编码 ID 散落多处** → 集中到少量常量类（如 `SoundIds`），后续接武器表时一处替换
- **[Risk] 连射导致 SFX Agent 抢占** → SFX AgentCount=8；必要时提高优先级或 Agent 数
- **[Risk] 1s 淡出未完成就切场景** → Procedure 离开时仍发起 1s fade stop；可接受短暂重叠
- **[Trade-off] 测试硬编码 vs 最终武器表驱动** → 接受短期重复劳动，换本轮可听验证

## Migration Plan

1. 改 Luban Sound / SoundGroup 源表 → `gen_all` 导出
2. 核对生成 json/bin：Music location 为文件名；BGM `fade_in_seconds=1`
3. 确认/补 YooAsset 收录后进 Play Mode 用 `PlaySound(id)` 抽查
4. 接入 Procedure BGM（停旧曲 fadeOut=1）与战斗事件硬编码（敌方开火固定 2001）
5. 回滚：还原源表与生成物、还原 Procedure/战斗脚本即可；无存档格式变更

## Open Questions

- （无）用户已确认：Music location=文件名；BGM 淡入淡出 1s；敌方发射测试统一 2001
