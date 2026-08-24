# CopyTexture 跨格式/压缩纹理无防护、失败静默 —— 修改方案

> 对应 README 缺陷清单 6.1-2 / 6.4，目标：**让图源格式受控（治本）、让失败显式可观测（治标）、让约定可校验（防回归）**。
> 状态：方案待评审，未实施。

---

## 1. 问题回顾

| 问题 | 现状 | 后果 |
|---|---|---|
| 跨格式拷贝 | `AtlasPage.TryCopyFrom` 只检查 `copyTextureSupport != None`，不检查源纹理格式 | 压缩图源（ETC2/ASTC/BC，移动端默认导入格式）→ RGBA32 页，CopyTexture 多数平台失败 |
| 压缩块对齐 | 无对齐检查 | 即使同压缩格式，sprite 区域非 4 倍数也会失败 |
| 失败静默 | `Graphics.CopyTexture` 无返回值，`TryCopyFrom` 失败仅返回 false | UI 静默空白，控制台报错不显眼，难以定位 |

**根本约束（为什么图源必须 RGBA32）**：
- CopyTexture 要求源与目标**同格式**；
- 压缩纹理拷贝区域必须 **4×4 块对齐**，动态装箱的 sprite 无法保证 → 图集页只能 RGBA32；
- 因此图源也必须全平台 RGBA32（普通资源照常按平台压缩，图源目录是刻意例外）。

---

## 2. 方案总览（四层）

```
┌────────────────────────────────────────────────────────────┐
│ L1 图源格式治理（编辑器，治本）                              │
│    AssetPostprocessor 导入强制 + 存量 Reimport + 构建校验    │
├────────────────────────────────────────────────────────────┤
│ L2 运行时格式防御（兜底热更/动态资源）                       │
│    Insert 入口格式校验 + 新增 UnsupportedFormat 失败原因     │
├────────────────────────────────────────────────────────────┤
│ L3 失败可观测性                                             │
│    Insert 全部失败路径统一 Debug.LogError（带全上下文）      │
├────────────────────────────────────────────────────────────┤
│ L4 配置清理                                                 │
│    PageTextureFormat 条件编译宏残留与误导注释清理            │
└────────────────────────────────────────────────────────────┘
```

---

## 3. 详细设计

### L1-1 目录约定（先决条件）

新增独立图源目录（建议）：`Assets/Project/Art/Sprites/DynamicAtlas/`

- 只对该目录强制 RGBA32，不影响其它 UI 资源（普通资源照常按平台压缩）；
- 该目录需加入 DefaultPackage 收集（`AddressByFileName`，与 `DynamicAtlasLocation` 约定一致）；
- 大图/背景图**不进**该目录（RGBA32 是 4B/px，收窄范围控制包体）。

### L1-2 导入强制（AssetPostprocessor）

新文件：`Assets/Project/Scripts/Editor/DynamicAtlas/DynamicAtlasTexturePostprocessor.cs`

```csharp
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace DynamicAtlas.Editor
{
    public static class DynamicAtlasTextureSettings
    {
        public const string SourceRoot = "Assets/Project/Art/Sprites/DynamicAtlas";
    }

    /// <summary>图源目录内资源导入/重导时强制全平台 RGBA32。</summary>
    public class DynamicAtlasTexturePostprocessor : AssetPostprocessor
    {
        private void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(DynamicAtlasTextureSettings.SourceRoot))
                return;

            var ti = (TextureImporter)assetImporter;
            ti.textureType = TextureImporterType.Sprite;
            ti.mipmapEnabled = false;   // 页无 mipmap，源有 mip 也只拷 mip0，纯浪费
            ti.textureCompression = TextureImporterCompression.Uncompressed;

            ForcePlatform(ti, "");        // Default（编辑器/PC 也须一致，否则编辑器 Play 同样跨格式失败）
            ForcePlatform(ti, "Android"); // 平台覆盖必须显式压制，否则构建走 ETC2
            ForcePlatform(ti, "iPhone");  // 否则构建走 ASTC
        }

        private static void ForcePlatform(TextureImporter ti, string platform)
        {
            // 读原设置再改，避免 SetPlatformTextureSettings 清掉 maxTextureSize 等其它配置
            TextureImporterPlatformSettings s = ti.GetPlatformTextureSettings(platform);
            s.overridden = true;
            s.format = TextureImporterFormat.RGBA32;
            ti.SetPlatformTextureSettings(s);
        }
    }

    public static class DynamicAtlasTextureMenu
    {
        /// <summary>存量资源一次性归位（postprocessor 只对新导入/重导生效）。</summary>
        [MenuItem("Tools/DynamicAtlas/Reimport Source Textures")]
        public static void ReimportSources()
        {
            string[] guids = AssetDatabase.FindAssets(
                "t:Texture2D", new[] { DynamicAtlasTextureSettings.SourceRoot });
            for (int i = 0; i < guids.Length; i++)
                AssetDatabase.ImportAsset(AssetDatabase.GUIDToAssetPath(guids[i]));
            Debug.Log($"[DynamicAtlas] Reimported {guids.Length} source textures.");
        }
    }
}
#endif
```

设计要点：

- **无条件覆盖**（而非仅提示）：目录内图源没有理由例外，确定性优先；例外资源走 L2 的转换入口显式处理；
- 编辑器中 RGBA32 内存上升是固有代价（编辑器 Play 模式同样需要 CopyTexture 可用），目录收窄后通常可接受；
- `OnPreprocessTexture` 只对"导入/重导"生效，存量资源必须执行一次菜单 Reimport。

### L1-3 构建校验（最后防线，只查不改）

新文件：`Assets/Project/Scripts/Editor/DynamicAtlas/DynamicAtlasBuildValidator.cs`

```csharp
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace DynamicAtlas.Editor
{
    /// <summary>构建前校验图源目录最终平台格式，违规直接中断构建。</summary>
    public class DynamicAtlasBuildValidator : IPreprocessBuildWithReport
    {
        public int callbackOrder => 0;

        public void OnPreprocessBuild(BuildReport report)
        {
            string[] guids = AssetDatabase.FindAssets(
                "t:Texture2D", new[] { DynamicAtlasTextureSettings.SourceRoot });

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var ti = (TextureImporter)AssetImporter.GetAtPath(path);
                if (!IsRgba32(ti, "") || !IsRgba32(ti, "Android") || !IsRgba32(ti, "iPhone"))
                    throw new BuildFailedException(
                        $"[DynamicAtlas] 图源未按约定导入为 RGBA32: {path}，请执行 " +
                        "Tools/DynamicAtlas/Reimport Source Textures 后重试。");
            }
        }

        private static bool IsRgba32(TextureImporter ti, string platform)
        {
            TextureImporterPlatformSettings s = ti.GetPlatformTextureSettings(platform);
            if (s.overridden)
                return s.format == TextureImporterFormat.RGBA32;
            return ti.textureCompression == TextureImporterCompression.Uncompressed;
        }
    }
}
#endif
```

> 实现时验证点：`TextureImporterPlatformSettings` 对 `name=""`（Default）的 overridden 语义、以及 Uncompressed 时 format 的取值（Unity 可能返回 Automatic/16bit），以实际 API 行为为准微调判定逻辑。

### L2 运行时格式防御（兜底）

**新增失败原因**（`DynamicAtlasTypes.cs`）：

```csharp
public enum AtlasInsertFailReason
{
    // ...existing...
    /// <summary>图源纹理格式不支持 CopyTexture（须 RGBA32/ARGB32，或 RenderTexture）。</summary>
    UnsupportedFormat
}
```

**Insert 入口校验**（`DynamicAtlasManager.cs` 核心 Insert，置于 `IsAtlasPageTexture` 检查之后）：

```csharp
if (!IsSupportedSourceTexture(texture))
{
    Debug.LogError($"[DynamicAtlas] 插入拒绝：图源格式不支持 CopyTexture。"
                 + $"location={location}, texture={texture.name}, format={GetFormatName(texture)}。"
                 + "图源须为 RGBA32/ARGB32（RenderTexture 放行）；检查导入设置或走转换路径。");
    return AtlasInsertResult.Fail(AtlasInsertFailReason.UnsupportedFormat);
}

private static bool IsSupportedSourceTexture(Texture texture)
{
    // RenderTexture 无压缩、无块对齐问题，CopyTexture 支持，直接放行
    if (texture is RenderTexture)
        return true;
    return texture is Texture2D tex2d
        && (tex2d.format == TextureFormat.RGBA32 || tex2d.format == TextureFormat.ARGB32);
}
```

- 该检查覆盖 `Insert(sprite)` 路径（检查的是 `sprite.texture`）；
- 静态图集 sprite（texture 为图集纹理，通常压缩）会被显式拒绝——这正是期望行为（静态图集图不应进动态图集），调用方 `DynamicAtlasImage.ApplySourceSprite` 已有失败告警处理；
- 压缩纹理的块对齐检查**不需要单独做**：既然压缩格式在入口被拒绝，对齐问题不会到达拷贝层。

### L3 失败可观测性

`DynamicAtlasManager.cs` 中 Insert 的失败路径统一补日志（当前仅静默返回枚举）：

```csharp
if (!targetPage.TryCopyFrom(texture, sourceRect, destRect))
{
    targetPage.FreeRect(destRect);
    Debug.LogError($"[DynamicAtlas] CopyTexture 失败：location={location}, texture={texture.name}, "
                 + $"format={texture.format}, srcRect={sourceRect}, destRect={destRect}, "
                 + $"copyTextureSupport={SystemInfo.copyTextureSupport}");
    return AtlasInsertResult.Fail(AtlasInsertFailReason.CopyFailed);
}
```

同一批处理：`PackFailed`、`ExceedsGroupSize`、`ExceedsMaxPageSize` 失败路径也补 LogError（带 location 与尺寸），使所有失败可 grep、可定位。`AtlasPage.TryCopyFrom` 保持内部静默（日志统一在 Manager 层，避免重复输出）。

> 说明：`Graphics.CopyTexture` 无返回值，无法直接探测失败；L2 前置静态检查通过后，拷贝失败概率已降至最低，L3 日志保证一旦发生即可见。可选增强（不推荐默认做）：DEVELOPMENT_BUILD 下拷贝后读目标区域一像素与源比对，成本高、收益低。

### L4 配置清理（`DynamicAtlasTypes.cs`）

```csharp
// 现状：属性 + 两个从未定义的条件编译宏（DYNAMIC_ATLAS_ANDROID_FORMAT / DYNAMIC_ATLAS_IOS_FORMAT）
// 恒返回 RGBA32；注释 "ARGB32 语义" 与实现不符，误导。
// 改为：
public static readonly TextureFormat PageTextureFormat = TextureFormat.RGBA32;
```

- 删除死宏与误导注释，明确"页格式 RGBA32 是 CopyTexture 同格式约束的硬性选择"；
- 若将来要支持压缩页，必须先解决块对齐装箱（README 6.4 已说明不成立），此处不预留扩展。

---

## 4. 改动文件清单

| 文件 | 动作 | 内容 |
|---|---|---|
| `Assets/Project/Scripts/Editor/DynamicAtlas/DynamicAtlasTexturePostprocessor.cs` | 新增 | 导入强制 + Reimport 菜单 |
| `Assets/Project/Scripts/Editor/DynamicAtlas/DynamicAtlasBuildValidator.cs` | 新增 | 构建校验 |
| `DynamicAtlasTypes.cs` | 修改 | `UnsupportedFormat` 枚举；`PageTextureFormat` 清理 |
| `DynamicAtlasManager.cs` | 修改 | Insert 格式校验；失败路径日志 |
| `DynamicAtlas/README.md` | 修改 | 6.1-2 标注方案与状态；补充格式约定章节 |
| YooAsset 收集器配置 | 手动 | 图源目录加入 DefaultPackage（`AddressByFileName`） |

---

## 5. 验收标准

1. 图源目录内导入任意 png（默认压缩导入）→ 导入设置自动变为 RGBA32（Default/Android/iPhone 三平台一致），mipmap 关闭；
2. 手动把图源改回 ETC2 → 重导被强制回 RGBA32；构建时若有漏网，构建被 `BuildFailedException` 中断并提示 Reimport 菜单；
3. 运行时 Insert 压缩纹理 → 返回 `UnsupportedFormat` + LogError，不静默、不白图；`DynamicAtlasImage` 收到失败告警；
4. 正常 RGBA32 图源：合图、脏缓存复活、释放全流程行为不变；
5. 任意平台构建通过校验；
6. 编辑器菜单 Tools/DynamicAtlas/Reimport Source Textures 可一键归位存量资源。

---

## 6. 顺带项（可选，不扩大主范围）

- **0 尺寸 rect 校验**（README 6.3-7）：核心 Insert 入口对 `sourceRect.width/height < 1` 直接返回 `EmptyKey` 类失败——一行检查，顺手可加；
- **mipmap 说明**：图源关闭 mipmap 属正常 UI 取舍（Simple 缩放用 mip0 + Bilinear），在 README 格式约定章节注明即可。

---

## 7. 实施顺序

1. **L2 + L3**（运行时防御与日志）——独立可测，先落地；
2. **L4**（配置清理）；
3. **L1-2**（postprocessor + Reimport 菜单）——执行一次存量 Reimport；
4. **L1-3**（构建校验）；
5. 收集器配置 + README 更新；
6. 按第 5 节验收清单逐项验证。

---

## 8. 方案取舍说明

| 取舍点 | 选择 | 理由 |
|---|---|---|
| 导入时强制 vs 构建前临时改 | 导入时强制 | TextureImporter 设置改动必须 Reimport 才生效，构建前临时改会导致每次构建全量重导、极慢且易残留脏状态 |
| 无条件覆盖 vs 条件豁免 | 无条件覆盖 | 目录收窄后无例外需求；确定性优先；例外走显式转换入口 |
| 运行时转换路径（RT Blit） | 本期不做 | 作为未来"例外资源"扩展；当前先让失败显式化，比悄悄转换更利于暴露问题 |
| CPU 回退（GetPixels/SetPixels） | 不做 | 破坏页纹理"无 Apply"设计，大页整页上传卡顿 |
| 压缩图集页 | 不做 | 块对齐约束下不成立（README 6.4 已论证） |
