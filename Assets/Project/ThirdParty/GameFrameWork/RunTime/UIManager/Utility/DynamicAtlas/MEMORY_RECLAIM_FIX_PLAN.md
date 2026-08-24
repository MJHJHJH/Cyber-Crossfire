# 全局资源回收 —— 修改方案（README 6.2-1）

> 目标：为 DynamicAtlasManager 补齐"页面可销毁、脏缓存有界、像素可清理"的完整回收能力，
> 同时保证与现有引用计数 / 脏缓存复活 / 页索引机制不冲突。
> 状态：方案待评审，未实施。

---

## 1. 问题拆解

| 子问题 | 现状 | 影响 |
|---|---|---|
| 页面永不销毁 | `AtlasPage.Destroy()` 仅用于 PackFailed 回滚；`_groups` 列表只增不减 | 模块切换后旧 UI 图集页（4~64MB/页）永久占内存 |
| 脏缓存无上限 | `_dirtyCache` 只增不减（复活命中/区域覆盖/Debug 才移除） | 一次性 location 多时字典无限膨胀（条目虽小，但无界） |
| 像素永久保留 | Release 后矩形像素保留（供复活），页内死区只增不减 | 页利用率下降 → 被迫开新页 → 内存进一步增长 |

**关键交互（方案必须处理的约束）**：

1. **页索引一致性**：`Release`/`DebugClearFreeAreas`/`GetPageCount`/`TryGetPageTexture` 都依赖 `AtlasPage.PageIndex == _groups 列表索引`，且 `_entries`/`_dirtyCache` 都存有 `PageIndex`——回收页面后必须保持索引连续并同步修正引用；
2. **复活机制**：像素保留是"零拷贝复活"的基础——清像素 = 放弃复活优化，必须是**显式**操作，不能混入常规释放路径；
3. **OnDisable 不释放绑定（6.2-3）**：组件禁用不释放 RefCount，空页检测不到——本方案对"面板级关闭（OnDestroy）"有效，对"列表项禁用"无效（见 7 节，不扩大范围）。

---

## 2. 方案总览

```
┌─────────────────────────────────────────────────────────────┐
│ ① 显式清理 API（Reset）          —— 整体卸载/重建            │
│ ② 空页自动回收（CollectEmptyPages）—— 模块切换后整页归还     │
│ ③ 脏缓存容量上限 + 驱逐（LRU）    —— 字典有界                │
│ ④ 空闲像素清理（ClearIdlePixels）—— 显式内存整理（牺牲复活） │
│ ⑤ 组合入口（TrimMemory）+ 调用挂点                           │
└─────────────────────────────────────────────────────────────┘
```

---

## 3. 详细设计

### 3.1 API 设计（DynamicAtlasManager 新增）

```csharp
/// <summary>脏缓存容量上限（默认 DynamicAtlasConfig.DEFAULT_DIRTY_CACHE_LIMIT = 512）。</summary>
public int DirtyCacheLimit { get; set; }

// 统计（供调试/监控）
public int TotalPageCount { get; }       // 全部分组页数
public int DirtyCacheCount { get; }      // 脏缓存条数

/// <summary>销毁全部图集页与条目、清空脏缓存。调用前必须确保无 UI 组件正在显示图集 Sprite。</summary>
public void Reset();

/// <summary>回收所有"无活跃条目"的空页（含中间页，自动保持页索引连续并修正引用）。返回回收页数。</summary>
public int CollectEmptyPages();

/// <summary>整理：回收空页；clearIdlePixels=true 时额外清空闲像素并失效对应脏缓存。返回回收页数。</summary>
public int TrimMemory(bool clearIdlePixels = false);

/// <summary>将所有页的空闲区像素清为透明并失效对应脏缓存（显式放弃复活优化换内存）。</summary>
public void ClearIdlePixels();
```

### 3.2 空页回收（核心）

**活跃计数**：`AtlasPage` 增加 `ActiveCount`（internal set），在 `CreateEntry` 时 +1、`Release` 移除条目时 -1。`ActiveCount == 0` 即空页。

```csharp
// AtlasPage.cs
public int PageIndex { get; internal set; }   // 由 readonly 改为可写（页回收时 swap 需要）
public int ActiveCount { get; internal set; } // 该页活跃条目数
```

**回收实现**（Manager）：

```csharp
public int CollectEmptyPages()
{
    int collected = 0;
    foreach (KeyValuePair<int, List<AtlasPage>> pair in _groups)
    {
        int groupSize = pair.Key;
        List<AtlasPage> pages = pair.Value;
        if (pages == null || pages.Count == 0)
            continue;

        for (int i = pages.Count - 1; i >= 0; i--)
        {
            if (pages[i].ActiveCount > 0)
                continue;

            // 页即将销毁：其上的脏缓存（像素已不存在）必须先失效
            RemoveDirtyForPage(groupSize, i);

            // 非尾页回收：把尾页移到空位，保持索引连续（低频操作，全量修正可接受）
            int last = pages.Count - 1;
            if (i != last)
            {
                AtlasPage moved = pages[last];
                pages[i] = moved;
                moved.PageIndex = i;
                FixPageIndexReferences(groupSize, last, i); // _entries + _dirtyCache
            }

            pages[i].Destroy();
            pages.RemoveAt(last);
            collected++;
        }
    }
    return collected;
}

/// <summary>页移动后修正所有引用旧索引的条目与脏缓存（O(entries + dirty)，回收低频可接受）。</summary>
private void FixPageIndexReferences(int groupSize, int fromIndex, int toIndex)
{
    foreach (KeyValuePair<string, AtlasEntry> pair in _entries)
    {
        if (pair.Value.GroupSize == groupSize && pair.Value.PageIndex == fromIndex)
            pair.Value.PageIndex = toIndex;
    }
    foreach (KeyValuePair<string, DirtyCacheEntry> pair in _dirtyCache)
    {
        if (pair.Value.GroupSize == groupSize && pair.Value.PageIndex == fromIndex)
            pair.Value.PageIndex = toIndex;
    }
}
```

**为什么必须做索引修正**：被 swap 到尾部的页可能仍有活跃条目（其 `_entries.PageIndex`）与脏缓存（`_dirtyCache.PageIndex`），不修正则后续 `Release`/复活全部错乱——这是本方案最易错点，验收标准单独覆盖。

**安全性论证**：

- 只回收 `ActiveCount == 0` 的页 → `_entries` 中不存在指向该页的活跃条目 → `Release` 的 `pages[entry.PageIndex]` 永远有效；
- 复活（`TryReviveFromDirtyCache`）会经 `CreateEntry` 使 `ActiveCount++` → 复活与回收不竞争（回收只发生在显式调用时，且只收无活跃条目的页）；
- 页销毁前先 `RemoveDirtyForPage` → 不会有脏缓存指向已销毁页的像素。

### 3.3 脏缓存上限 + 驱逐

`DirtyCacheEntry` 增加写入帧号：

```csharp
private sealed class DirtyCacheEntry
{
    // ...existing...
    public int Frame; // 写入时的 Time.frameCount，用于驱逐
}
```

`Release` 写入脏缓存后检查上限，超出时驱逐最老条目至低水位（上限的 3/4）：

```csharp
// Release() 中写入 _dirtyCache 之后：
if (_dirtyCache.Count > _dirtyCacheLimit)
    EvictOldestDirty();

private void EvictOldestDirty()
{
    int watermark = _dirtyCacheLimit * 3 / 4; // 低水位，避免抖动
    while (_dirtyCache.Count > watermark)
    {
        string oldestKey = null;
        int oldestFrame = int.MaxValue;
        foreach (KeyValuePair<string, DirtyCacheEntry> pair in _dirtyCache)
        {
            if (pair.Value.Frame < oldestFrame)
            {
                oldestFrame = pair.Value.Frame;
                oldestKey = pair.Key;
            }
        }
        if (oldestKey == null)
            break;
        _dirtyCache.Remove(oldestKey); // 像素仍保留在页上，仅放弃"复活"能力——正确性不受影响
    }
}
```

**安全性论证**：脏缓存是纯性能优化（复活免拷贝）。驱逐只影响"下次插入是否免拷贝"，不影响正确性（未命中 → 走正常插入，区域被覆盖时像素本就要重写）。

### 3.4 空闲像素清理（显式内存整理）

```csharp
public void ClearIdlePixels()
{
    foreach (KeyValuePair<int, List<AtlasPage>> pair in _groups)
    {
        int groupSize = pair.Key;
        List<AtlasPage> pages = pair.Value;
        if (pages == null)
            continue;

        for (int i = 0; i < pages.Count; i++)
        {
            if (pages[i] == null)
                continue;
            pages[i].ClearIdlePixels();      // 复用现有 Debug 逻辑，模板改为透明
            RemoveDirtyForPage(groupSize, i); // 像素被清 → 脏缓存失效
        }
    }
}
```

`AtlasPage` 将 `DebugClearFreeAreas` 的拷贝逻辑抽为 `ClearIdlePixels()`（透明模板），`DebugClearFreeAreas`（灰底）保留为纯调试工具。

**语义**：清空闲像素 = 显式放弃这些区域的复活能力。与 `CollectEmptyPages` 的区别——清像素不清页（页内活跃条目不受影响），用于"页仍被占用但死区多"的整理。

### 3.5 Reset（整体卸载）

```csharp
public void Reset()
{
    foreach (KeyValuePair<int, List<AtlasPage>> pair in _groups)
    {
        List<AtlasPage> pages = pair.Value;
        if (pages == null)
            continue;
        for (int i = 0; i < pages.Count; i++)
            pages[i]?.Destroy();
        pages.Clear();
    }
    _entries.Clear();
    _dirtyCache.Clear();
}
```

**调用纪律**：Reset 语义是"全部放弃"——调用前必须确保没有 UI 组件正在显示图集 Sprite（面板全部关闭/卸载）。异步加载进行中调用也安全：pending 回调执行时 `Insert` 会走正常路径重建（脏缓存已清 → 重新拷贝），自洽。

### 3.6 配置

```csharp
// DynamicAtlasConfig
public const int DEFAULT_DIRTY_CACHE_LIMIT = 512;
```

### 3.7 调用挂点（应用层集成，实施时定位）

| 挂点 | 调用 | 理由 |
|---|---|---|
| 场景切换（Procedure 切换，旧 UI 全部关闭后） | `TrimMemory(true)` | 换模块后旧图复活的概率低，连像素一起清，内存收益最大 |
| `Application.lowMemory`（移动端内存告警） | `TrimMemory(true)` | 低内存兜底 |
| UI 系统整体卸载/重建（切账号、回滚） | `Reset()` | 全清 |
| 面板级开/关 | **不调用** | 面板关闭走 OnDestroy 已释放条目；开开关关依赖复活优化，不应清像素 |

> 实施注意：挂点代码在热更层（Procedure）或 AOT 层（UIManager）由实施时确认；`Application.lowMemory` 订阅建议放 AOT 侧。

---

## 4. 改动文件清单

| 文件 | 动作 | 内容 |
|---|---|---|
| `DynamicAtlasTypes.cs` | 修改 | `DEFAULT_DIRTY_CACHE_LIMIT` 配置 |
| `DynamicAtlasManager.cs` | 修改 | `DirtyCacheEntry.Frame`；`ActiveCount` 维护；`Reset/CollectEmptyPages/TrimMemory/ClearIdlePixels/EvictOldestDirty/FixPageIndexReferences`；统计属性 |
| `AtlasPage.cs` | 修改 | `PageIndex` 可写；`ActiveCount`；`ClearIdlePixels()`（抽自 Debug 逻辑） |
| `DynamicAtlas/README.md` | 修改 | 6.2-1 标注方案与状态；新增"回收 API"章节 |
| 场景切换/内存告警代码 | 修改（实施时定位） | 挂点调用 |

---

## 5. 验收标准

1. **空页回收**：构造整页条目 → 全部 Release → `CollectEmptyPages()` 回收该页（`GetPageCount` 下降、Profiler 确认纹理销毁）；
2. **中间页回收索引正确**：3 页各放条目 → 释放中间页全部条目 → 回收后尾页索引变为 1，其余条目的 `Release`、`TryGetPageTexture`、脏缓存复活均正常（本方案最易错点，必须覆盖）；
3. **脏缓存驱逐**：设置 `DirtyCacheLimit = 8` → 释放 20 个不同 location → 字典收敛到 6（低水位）；最早条目复活转为正常插入（不崩溃、像素正确）；
4. **ClearIdlePixels**：释放条目 → 清空闲像素 → 同 key 再插入走正常拷贝（非复活），图像正确；
5. **Reset**：调用后页数/条目/脏缓存归零，重新插入一切正常；
6. **无回归**：不调用任何新 API 时行为与现状完全一致；
7. 场景切换挂点接入后，Profiler 观察切换后页数回落到基线。

---

## 6. 风险与边界

| 风险 | 说明与对策 |
|---|---|
| swap 索引修正遗漏 | 本方案最易错点——`FixPageIndexReferences` 必须同时覆盖 `_entries` 与 `_dirtyCache`；验收 2 专门覆盖 |
| Reset 时组件仍显示 | 文档纪律 + Debug 断言（可选）；Reset 后组件下次 OnEnable 会经 `_boundKey` 失效走 Reload 自愈，但显示瞬间可能白图 |
| 清像素破坏复活 | 设计使然（显式操作）；挂点选择（场景切换/低内存）已把损失降到最低 |
| 驱逐抖动 | 低水位（3/4 上限）避免边界反复驱逐 |
| OnDisable 不释放（6.2-3） | 空页回收对"面板关闭（OnDestroy）"生效；列表项禁用场景条目常驻，页不空——如需彻底解决需另行处理 6.2-3（延迟释放），不在本方案范围 |

---

## 7. 取舍说明

| 取舍点 | 选择 | 理由 |
|---|---|---|
| 中间空页回收 vs 仅尾页回收 | 中间页也回收（swap + 索引修正） | 模块页常在列表中间，仅尾页回收覆盖不了典型切场景场景；swap 是全量修正，低频调用成本可接受 |
| 驱逐脏缓存是否清像素 | 只删条目不清像素 | 驱逐是纯字典有界化；像素回收由空页回收/清空闲像素承担，职责分离 |
| TrimMemory 默认是否清像素 | `TrimMemory(false)` 显式传参 | 调用方决定是否牺牲复活优化；挂点推荐 true |
| 自动定时回收 | 不做 | 回收时机与 UI 生命周期强相关，显式调用更可控；避免"回收后立刻复活重拷"的抖动 |
