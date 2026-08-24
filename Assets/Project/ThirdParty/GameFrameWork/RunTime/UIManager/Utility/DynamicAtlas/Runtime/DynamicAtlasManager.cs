using System.Collections.Generic;
using UnityEngine;

namespace DynamicAtlas
{
    public sealed class DynamicAtlasManager
    {
        private sealed class DirtyCacheEntry
        {
            public string Key;
            public string Location;
            public int GroupSize;
            public int PageIndex;
            public RectInt Rect;
            public Vector4 Border;
            public Vector2 Pivot;
            public float PixelsPerUnit;
            /// <summary>写入时的 Time.frameCount，用于超限驱逐（近似 LRU）。</summary>
            public int Frame;
        }
        /// <summary>软释放队列项：条目引用归零后延迟销毁的 Sprite。</summary>
        private sealed class PendingSprite
        {
            public Sprite Sprite;
            public int ReleaseFrame;
        }

        private readonly struct SpriteCreateParams
        {
            public readonly Vector4 Border;
            public readonly Vector2 Pivot;
            public readonly float PixelsPerUnit;

            public SpriteCreateParams(Vector4 border, Vector2 pivot, float pixelsPerUnit)
            {
                Border = border;
                Pivot = pivot;
                PixelsPerUnit = pixelsPerUnit;
            }
        }

        public static DynamicAtlasManager Instance { get; } = new DynamicAtlasManager();

        private readonly Dictionary<int, List<AtlasPage>> _groups = new Dictionary<int, List<AtlasPage>>();
        private readonly Dictionary<string, AtlasEntry> _entries = new Dictionary<string, AtlasEntry>();
        private readonly Dictionary<string, DirtyCacheEntry> _dirtyCache = new Dictionary<string, DirtyCacheEntry>();
        private readonly int _padding;
        private readonly float _pixelsPerUnit;
        private readonly Vector2 _pivot;
        private int _dirtyCacheLimit = DynamicAtlasConfig.DEFAULT_DIRTY_CACHE_LIMIT;
        private readonly List<PendingSprite> _pendingDestroySprites = new List<PendingSprite>();
        private int _softReleaseFrames = DynamicAtlasConfig.DEFAULT_SOFT_RELEASE_FRAMES;

        /// <summary>脏缓存条目数上限，超出后驱逐最老条目至低水位。下限 64 防抖。</summary>
        public int DirtyCacheLimit
        {
            get => _dirtyCacheLimit;
            set => _dirtyCacheLimit = Mathf.Max(64, value);
        }

        /// <summary>软释放延迟销毁帧数：Release 归零后 Sprite 延迟 N 帧销毁，给"误释放后重新绑定"留自愈窗口。0 = 立即销毁（旧行为）。</summary>
        public int SoftReleaseFrames
        {
            get => _softReleaseFrames;
            set => _softReleaseFrames = Mathf.Max(0, value);
        }
        /// <summary>当前全部图集页总数（所有分组）。</summary>
        public int TotalPageCount
        {
            get
            {
                int count = 0;
                foreach (KeyValuePair<int, List<AtlasPage>> pair in _groups)
                {
                    if (pair.Value != null)
                        count += pair.Value.Count;
                }
                return count;
            }
        }

        /// <summary>当前脏缓存条数。</summary>
        public int DirtyCacheCount => _dirtyCache.Count;

        private DynamicAtlasManager()
        {
            _padding = DynamicAtlasConfig.DEFAULT_PADDING;
            _pixelsPerUnit = DynamicAtlasConfig.DEFAULT_PIXELS_PER_UNIT;
            _pivot = DynamicAtlasConfig.DefaultPivot;

            for (int i = 0; i < DynamicAtlasConfig.ValidGroupSizes.Length; i++)
                _groups[DynamicAtlasConfig.ValidGroupSizes[i]] = new List<AtlasPage>();
        }

        /// <summary>
        /// 条目字典 key = Location + 分组尺寸，同资源可分别插入 1024/2048/4096。
        /// </summary>
        public static string MakeEntryKey(string location, int groupSize)
        {
            return location + "@" + groupSize;
        }

        public static string MakeEntryKey(string location, DynamicAtlasGroup group)
        {
            return MakeEntryKey(location, DynamicAtlasConfig.ToSize(group));
        }

        public AtlasInsertResult Insert(string location, Texture texture, DynamicAtlasGroup group)
        {
            if (texture == null)
                return AtlasInsertResult.Fail(AtlasInsertFailReason.NullTexture);

            var sourceRect = new RectInt(0, 0, texture.width, texture.height);
            return Insert(location, texture, sourceRect, DefaultSpriteParams(), group);
        }

        public AtlasInsertResult Insert(string location, Sprite sprite, DynamicAtlasGroup group)
        {
            if (sprite == null || sprite.texture == null)
                return AtlasInsertResult.Fail(AtlasInsertFailReason.NullTexture);

            Rect tr = sprite.textureRect;
            var sourceRect = TextureRectToRectInt(tr, sprite.texture.width, sprite.texture.height);

            // Sprite.pivot 为像素；Sprite.Create 需要归一化 pivot
            float w = Mathf.Max(1f, sourceRect.width);
            float h = Mathf.Max(1f, sourceRect.height);
            var pivot = new Vector2(sprite.pivot.x / w, sprite.pivot.y / h);
            var createParams = new SpriteCreateParams(sprite.border, pivot, sprite.pixelsPerUnit);

            AtlasInsertResult result = Insert(location, sprite.texture, sourceRect, createParams, group);
            if (result.Success && result.Entry?.Sprite != null)
                WarnIfSpriteMetaMismatch(sprite, result.Entry);

            return result;
        }

        /// <summary>
        /// 与 Unity 常用写法一致：(int)截断，并 Clamp 到纹理范围，避免越界 / 丢边。
        /// </summary>
        private static RectInt TextureRectToRectInt(Rect textureRect, int texWidth, int texHeight)
        {
            int x = Mathf.Clamp((int)textureRect.x, 0, Mathf.Max(0, texWidth - 1));
            int y = Mathf.Clamp((int)textureRect.y, 0, Mathf.Max(0, texHeight - 1));
            int w = Mathf.Max(1, (int)textureRect.width);
            int h = Mathf.Max(1, (int)textureRect.height);

            if (x + w > texWidth)
                w = texWidth - x;
            if (y + h > texHeight)
                h = texHeight - y;

            return new RectInt(x, y, Mathf.Max(1, w), Mathf.Max(1, h));
        }

        private static void WarnIfSpriteMetaMismatch(Sprite source, AtlasEntry entry)
        {
            Sprite atlasSprite = entry.Sprite;
            Vector2 srcSize = source.textureRect.size;
            Vector2 atlasSize = atlasSprite.rect.size;

            if (Mathf.Abs(srcSize.x - atlasSize.x) > 0.5f || Mathf.Abs(srcSize.y - atlasSize.y) > 0.5f)
            {
                Debug.LogWarning(
                    $"[DynamicAtlas] Sprite 尺寸不一致 location={entry.Location}, source={srcSize}, atlas={atlasSize}");
            }

            if (Mathf.Abs(source.pixelsPerUnit - atlasSprite.pixelsPerUnit) > 0.01f)
            {
                Debug.LogWarning(
                    $"[DynamicAtlas] PPU 不一致 location={entry.Location}, source={source.pixelsPerUnit}, atlas={atlasSprite.pixelsPerUnit}");
            }

            if ((source.border - atlasSprite.border).sqrMagnitude > 0.01f)
            {
                Debug.LogWarning(
                    $"[DynamicAtlas] Border 不一致 location={entry.Location}, source={source.border}, atlas={atlasSprite.border}");
            }
        }

        public AtlasInsertResult Insert(string location, Texture texture, RectInt sourceRect, DynamicAtlasGroup group)
        {
            return Insert(location, texture, sourceRect, DefaultSpriteParams(), group);
        }

        private AtlasInsertResult Insert(
            string location,
            Texture texture,
            RectInt sourceRect,
            SpriteCreateParams createParams,
            DynamicAtlasGroup group)
        {
            if (texture == null)
                return AtlasInsertResult.Fail(AtlasInsertFailReason.NullTexture);

            DrainPendingDestroy();

            if (string.IsNullOrEmpty(location))
                return AtlasInsertResult.Fail(AtlasInsertFailReason.EmptyKey);

            int groupSize = DynamicAtlasConfig.ToSize(group);
            if (!DynamicAtlasConfig.IsValidGroupSize(groupSize))
                groupSize = DynamicAtlasConfig.DEFAULT_GROUP_SIZE;

            string key = MakeEntryKey(location, groupSize);
            if (_entries.TryGetValue(key, out AtlasEntry existing))
            {
                existing.RefCount++;
                return AtlasInsertResult.Ok(existing);
            }

            // 入口限制：禁止把动态图集页纹理再当源合入（会 CopyTexture 自拷贝并污染图集）
            if (IsAtlasPageTexture(texture))
            {
                Debug.LogWarning(
                    $"[DynamicAtlas] 插入拒绝：源已是图集页 texture={texture.name}, location={location}, group={groupSize}");
                return AtlasInsertResult.Fail(AtlasInsertFailReason.SourceIsAtlasPage);
            }

            // 格式限制：CopyTexture 要求源与图集页（RGBA32）同格式，压缩纹理另有 4×4 块对齐约束，
            // 故图源只允许 RGBA32/ARGB32（RenderTexture 无压缩放行）；否则跨格式拷贝会静默失败。
            if (!IsSupportedSourceTexture(texture))
            {
                Debug.LogError(
                    $"[DynamicAtlas] 插入拒绝：图源格式不支持 CopyTexture。location={location}, " +
                    $"texture={texture.name}, format={GetSourceFormatName(texture)}。图源须为 RGBA32/ARGB32 " +
                    $"（图源目录由 DynamicAtlasTexturePostprocessor 强制），或改用 RenderTexture 源。");
                return AtlasInsertResult.Fail(AtlasInsertFailReason.UnsupportedFormat);
            }

            int srcW = sourceRect.width;
            int srcH = sourceRect.height;

            if (srcW > DynamicAtlasConfig.MAX_PAGE_SIZE || srcH > DynamicAtlasConfig.MAX_PAGE_SIZE)
            {
                Debug.LogError(
                    $"[DynamicAtlas] 插入拒绝：尺寸超过最大页 {DynamicAtlasConfig.MAX_PAGE_SIZE}。" +
                    $"location={location}, texture={texture.name}, size={srcW}x{srcH}");
                return AtlasInsertResult.Fail(AtlasInsertFailReason.ExceedsMaxPageSize);
            }

            if (srcW > groupSize || srcH > groupSize)
            {
                Debug.LogError(
                    $"[DynamicAtlas] 插入拒绝：尺寸超过分组 {groupSize}。location={location}, " +
                    $"texture={texture.name}, size={srcW}x{srcH}。请选择更大的 DynamicAtlasGroup。");
                return AtlasInsertResult.Fail(AtlasInsertFailReason.ExceedsGroupSize);
            }

            // 脏缓存命中：尝试零拷贝复活；失败则移除缓存并走正常插入
            if (TryReviveFromDirtyCache(key, location, groupSize, srcW, srcH, out AtlasEntry revived))
                return AtlasInsertResult.Ok(revived);

            if (!_groups.TryGetValue(groupSize, out List<AtlasPage> pages))
            {
                pages = new List<AtlasPage>();
                _groups[groupSize] = pages;
            }

            RectInt destRect = default;
            AtlasPage targetPage = null;

            for (int i = 0; i < pages.Count; i++)
            {
                if (pages[i].TryPack(srcW, srcH, out destRect))
                {
                    targetPage = pages[i];
                    break;
                }
            }

            if (targetPage == null)
            {
                var newPage = new AtlasPage(pages.Count, groupSize, _padding);
                if (!newPage.TryPack(srcW, srcH, out destRect))
                {
                    newPage.Destroy();
                    Debug.LogError(
                        $"[DynamicAtlas] 装箱失败（新页无法容纳）。location={location}, texture={texture.name}, " +
                        $"size={srcW}x{srcH}, group={groupSize}, pageIndex={newPage.PageIndex}");
                    return AtlasInsertResult.Fail(AtlasInsertFailReason.PackFailed);
                }

                pages.Add(newPage);
                targetPage = newPage;
            }

            // 装箱占用相交 → 脏缓存失活（在清边/拷贝之前）
            InvalidateDirtyIntersecting(groupSize, targetPage.PageIndex, destRect);

            if (!targetPage.TryCopyFrom(texture, sourceRect, destRect))
            {
                targetPage.FreeRect(destRect);
                Debug.LogError(
                    $"[DynamicAtlas] CopyTexture 失败。location={location}, texture={texture.name}, " +
                    $"format={GetSourceFormatName(texture)}, srcRect={sourceRect}, destRect={destRect}, " +
                    $"copyTextureSupport={SystemInfo.copyTextureSupport}");
                return AtlasInsertResult.Fail(AtlasInsertFailReason.CopyFailed);
            }

            AtlasEntry entry = CreateEntry(key, location, groupSize, targetPage, destRect, createParams);
            _entries[key] = entry;
            // Debug.Log($"[DynamicAtlas] 插入成功 location={location}, group={groupSize}, page={targetPage.PageIndex}, rect={destRect}");
            return AtlasInsertResult.Ok(entry);
        }

        public bool TryGet(string location, DynamicAtlasGroup group, out AtlasEntry entry)
        {
            return _entries.TryGetValue(MakeEntryKey(location, group), out entry);
        }

        /// <summary>按复合 key（Location@GroupSize）查询，通常传入 AtlasEntry.Key。</summary>
        public bool TryGet(string key, out AtlasEntry entry)
        {
            return _entries.TryGetValue(key, out entry);
        }

        /// <summary>判断纹理是否为任一动态图集页（禁止再作为 Insert 源）。</summary>
        public bool IsAtlasPageTexture(Texture texture)
        {
            if (texture == null)
                return false;

            foreach (KeyValuePair<int, List<AtlasPage>> pair in _groups)
            {
                List<AtlasPage> pages = pair.Value;
                if (pages == null)
                    continue;

                for (int i = 0; i < pages.Count; i++)
                {
                    AtlasPage page = pages[i];
                    if (page != null && ReferenceEquals(page.Texture, texture))
                        return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 图源格式是否可直接 CopyTexture 到图集页（RGBA32）。
        /// Texture2D 仅允许 RGBA32/ARGB32；RenderTexture 无压缩、无块对齐问题，放行。
        /// </summary>
        private static bool IsSupportedSourceTexture(Texture texture)
        {
            if (texture is RenderTexture)
                return true;

            if (texture is Texture2D tex2d)
                return tex2d.format == TextureFormat.RGBA32 || tex2d.format == TextureFormat.ARGB32;

            return false;
        }

        /// <summary>日志用：返回源纹理可读的格式描述。</summary>
        private static string GetSourceFormatName(Texture texture)
        {
            if (texture is Texture2D tex2d)
                return tex2d.format.ToString();
            if (texture is RenderTexture rt)
                return rt.format.ToString();
            return texture != null ? texture.GetType().Name : "null";
        }

        /// <summary>通过图集 Sprite 反查条目（用于复制后的引用恢复）。</summary>
        public bool TryGetEntryBySprite(Sprite sprite, out AtlasEntry entry)
        {
            entry = null;
            if (sprite == null)
                return false;

            foreach (KeyValuePair<string, AtlasEntry> pair in _entries)
            {
                AtlasEntry candidate = pair.Value;
                if (candidate != null && ReferenceEquals(candidate.Sprite, sprite))
                {
                    entry = candidate;
                    return true;
                }
            }

            return false;
        }

        public int GetPageCount(DynamicAtlasGroup group)
        {
            int groupSize = DynamicAtlasConfig.ToSize(group);
            if (!_groups.TryGetValue(groupSize, out List<AtlasPage> pages) || pages == null)
                return 0;
            return pages.Count;
        }

        public bool TryGetPageTexture(DynamicAtlasGroup group, int pageIndex, out Texture2D texture)
        {
            texture = null;
            int groupSize = DynamicAtlasConfig.ToSize(group);
            if (!_groups.TryGetValue(groupSize, out List<AtlasPage> pages) || pages == null)
                return false;
            if (pageIndex < 0 || pageIndex >= pages.Count)
                return false;

            texture = pages[pageIndex].Texture;
            return texture != null;
        }

        public bool Release(string key)
        {
            if (string.IsNullOrEmpty(key) || !_entries.TryGetValue(key, out AtlasEntry entry))
                return false;

            DrainPendingDestroy();

            entry.RefCount--;
            if (entry.RefCount > 0)
                return true;

            if (_groups.TryGetValue(entry.GroupSize, out List<AtlasPage> pages) &&
                entry.PageIndex >= 0 && entry.PageIndex < pages.Count)
            {
                AtlasPage page = pages[entry.PageIndex];
                page.FreeRect(entry.Rect);
                page.ActiveCount--;
            }

            // 软释放：引用归零不立即销毁 Sprite，延迟 SoftReleaseFrames 帧（操作驱动）后销毁，
            // 给"外部误释放但组件仍显示/即将重新绑定"留自愈窗口；0 = 立即销毁（旧行为）。
            if (entry.Sprite != null)
            {
                if (_softReleaseFrames <= 0)
                Object.Destroy(entry.Sprite);
                else
                    _pendingDestroySprites.Add(new PendingSprite
                    {
                        Sprite = entry.Sprite,
                        ReleaseFrame = Time.frameCount
                    });
            }

            _entries.Remove(key);

            // 像素保留：写入脏缓存，供同 Location+同分组再次插入时零拷贝复活
            _dirtyCache[key] = new DirtyCacheEntry
            {
                Key = key,
                Location = entry.Location,
                GroupSize = entry.GroupSize,
                PageIndex = entry.PageIndex,
                Rect = entry.Rect,
                Border = entry.Border,
                Pivot = entry.Pivot,
                PixelsPerUnit = entry.PixelsPerUnit,
                Frame = Time.frameCount
            };

            // 脏缓存有界：超出上限驱逐最老条目（只放弃复活能力，不影响正确性）
            if (_dirtyCache.Count > _dirtyCacheLimit)
                EvictOldestDirty();

            // Debug.Log($"[DynamicAtlas] 释放移入空闲列表 location={entry.Location}, group={entry.GroupSize}, page={entry.PageIndex}, rect={entry.Rect}");
            return true;
        }

        public bool Release(string location, DynamicAtlasGroup group)
        {
            return Release(MakeEntryKey(location, group));
        }

        /// <summary>
        /// Debug：将所有图集页上空闲列表区域恢复为初始灰底。
        /// 会移除对应页上的脏缓存（像素已被覆盖）。
        /// </summary>
        public void DebugClearFreeAreas()
        {
            foreach (KeyValuePair<int, List<AtlasPage>> pair in _groups)
            {
                int groupSize = pair.Key;
                List<AtlasPage> pages = pair.Value;
                if (pages == null)
                    continue;

                for (int i = 0; i < pages.Count; i++)
                {
                    pages[i]?.DebugClearFreeAreas();
                    RemoveDirtyForPage(groupSize, i);
                }
            }
        }

        /// <summary>
        /// 销毁全部图集页与条目、清空脏缓存。
        /// 调用纪律：必须确保无 UI 组件正在显示图集 Sprite（面板全部关闭/卸载）。
        /// 进行中的异步加载回调会走正常插入路径重建，自洽。
        /// </summary>
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

            // 全清语义：待销毁 Sprite 立即销毁，不留延迟窗口
            for (int i = 0; i < _pendingDestroySprites.Count; i++)
            {
                if (_pendingDestroySprites[i].Sprite != null)
                    Object.Destroy(_pendingDestroySprites[i].Sprite);
            }
            _pendingDestroySprites.Clear();

            _entries.Clear();
            _dirtyCache.Clear();
        }

        /// <summary>
        /// 回收所有"无活跃条目"的空页（含中间页）。
        /// 非尾页回收时把尾页 swap 到空位，保持 PageIndex == 列表索引，
        /// 并修正 _entries/_dirtyCache 中指向被移动页的索引引用。返回回收页数。
        /// </summary>
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

                    // 页即将销毁：其上的脏缓存（像素将不存在）必须先失效
                    RemoveDirtyForPage(groupSize, i);

                    // 非尾页回收：尾页移到空位，保持索引连续（低频操作，全量修正可接受）
                    int last = pages.Count - 1;
                    if (i != last)
                    {
                        AtlasPage moved = pages[last];
                        pages[i] = moved;
                        moved.PageIndex = i;
                        FixPageIndexReferences(groupSize, last, i);
                    }

                    pages[i].Destroy();
                    pages.RemoveAt(last);
                    collected++;
                }
            }
            return collected;
        }

        /// <summary>整理：回收空页；clearIdlePixels=true 时额外清空闲像素并失效对应脏缓存。返回回收页数。</summary>
        public int TrimMemory(bool clearIdlePixels = false)
        {
            DrainPendingDestroy();
            int collected = CollectEmptyPages();
            if (clearIdlePixels)
                ClearIdlePixels();
            return collected;
        }

        /// <summary>
        /// 内存整理：所有页的空闲区像素清为透明并失效对应脏缓存。
        /// 显式放弃这些区域的"零拷贝复活"优化以换取内存，由调用方权衡。
        /// </summary>
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
                    pages[i]?.ClearIdlePixels();
                    RemoveDirtyForPage(groupSize, i);
                }
            }
        }

        /// <summary>页移动后修正 _entries/_dirtyCache 中引用旧索引的条目（O(n)，回收低频可接受）。</summary>
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

        /// <summary>驱逐最老脏缓存条目至低水位（上限的 3/4），避免边界反复驱逐。</summary>
        private void EvictOldestDirty()
        {
            int watermark = Mathf.Max(16, _dirtyCacheLimit * 3 / 4);
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

                // 像素仍保留在页上，仅放弃"复活"能力——正确性不受影响
                _dirtyCache.Remove(oldestKey);
            }
        }

        /// <summary>操作驱动：销毁到期（超过 SoftReleaseFrames 帧）的软释放 Sprite。零开销快速返回。</summary>
        private void DrainPendingDestroy()
        {
            if (_pendingDestroySprites.Count == 0)
                return;

            int threshold = Time.frameCount - _softReleaseFrames;
            for (int i = _pendingDestroySprites.Count - 1; i >= 0; i--)
            {
                PendingSprite pending = _pendingDestroySprites[i];
                if (pending.ReleaseFrame > threshold)
                    continue;

                _pendingDestroySprites.RemoveAt(i);
                if (pending.Sprite != null)
                    Object.Destroy(pending.Sprite);
            }
        }

        /// <summary>条目是否仍被持有（登记在字典且 RefCount &gt; 0）。供外部调用者在 Release 前自查。</summary>
        public bool IsInUse(string key)
        {
            return !string.IsNullOrEmpty(key)
                && _entries.TryGetValue(key, out AtlasEntry entry)
                && entry != null
                && entry.RefCount > 0;
        }

        /// <summary>条目当前引用计数；key 不存在或已释放返回 0。</summary>
        public int GetRefCount(string key)
        {
            if (string.IsNullOrEmpty(key) || !_entries.TryGetValue(key, out AtlasEntry entry) || entry == null)
                return 0;
            return entry.RefCount;
        }

        private bool TryReviveFromDirtyCache(
            string key,
            string location,
            int groupSize,
            int srcW,
            int srcH,
            out AtlasEntry entry)
        {
            entry = null;
            if (!_dirtyCache.TryGetValue(key, out DirtyCacheEntry dirty))
                return false;

            // 尺寸或分组不一致，无法免拷贝复活
            if (dirty.GroupSize != groupSize ||
                dirty.Rect.width != srcW ||
                dirty.Rect.height != srcH)
            {
                _dirtyCache.Remove(key);
                return false;
            }

            if (!_groups.TryGetValue(groupSize, out List<AtlasPage> pages) ||
                dirty.PageIndex < 0 ||
                dirty.PageIndex >= pages.Count)
            {
                _dirtyCache.Remove(key);
                return false;
            }

            AtlasPage page = pages[dirty.PageIndex];
            if (page == null || !page.TryReclaim(dirty.Rect))
            {
                _dirtyCache.Remove(key);
                return false;
            }

            _dirtyCache.Remove(key);
            var createParams = new SpriteCreateParams(dirty.Border, dirty.Pivot, dirty.PixelsPerUnit);
            entry = CreateEntry(key, location, groupSize, page, dirty.Rect, createParams);
            _entries[key] = entry;
            Debug.Log($"[DynamicAtlas] 复活成功 location={location}, group={groupSize}, page={page.PageIndex}, rect={dirty.Rect}");
            return true;
        }

        private SpriteCreateParams DefaultSpriteParams()
        {
            return new SpriteCreateParams(Vector4.zero, _pivot, _pixelsPerUnit);
        }

        private AtlasEntry CreateEntry(
            string key,
            string location,
            int groupSize,
            AtlasPage page,
            RectInt destRect,
            SpriteCreateParams createParams)
        {
            float ppu = createParams.PixelsPerUnit > 0f
                ? createParams.PixelsPerUnit
                : _pixelsPerUnit;

            // 项目约定：动态图集源 Sprite 均为 FullRect
            Sprite atlasSprite = Sprite.Create(
                page.Texture,
                new Rect(destRect.x, destRect.y, destRect.width, destRect.height),
                createParams.Pivot,
                ppu,
                0,
                SpriteMeshType.FullRect,
                createParams.Border);

            page.ActiveCount++;

            return new AtlasEntry
            {
                Key = key,
                Location = location,
                GroupSize = groupSize,
                PageIndex = page.PageIndex,
                Rect = destRect,
                PageTexture = page.Texture,
                Sprite = atlasSprite,
                RefCount = 1,
                Border = createParams.Border,
                Pivot = createParams.Pivot,
                PixelsPerUnit = ppu
            };
        }

        private void InvalidateDirtyIntersecting(int groupSize, int pageIndex, RectInt occupied)
        {
            if (_dirtyCache.Count == 0)
                return;

            var toRemove = new List<string>();
            foreach (KeyValuePair<string, DirtyCacheEntry> pair in _dirtyCache)
            {
                DirtyCacheEntry dirty = pair.Value;
                if (dirty.GroupSize != groupSize || dirty.PageIndex != pageIndex)
                    continue;

                if (RectsIntersect(dirty.Rect, occupied))
                    toRemove.Add(pair.Key);
            }

            for (int i = 0; i < toRemove.Count; i++)
                _dirtyCache.Remove(toRemove[i]);
        }

        private void RemoveDirtyForPage(int groupSize, int pageIndex)
        {
            if (_dirtyCache.Count == 0)
                return;

            var toRemove = new List<string>();
            foreach (KeyValuePair<string, DirtyCacheEntry> pair in _dirtyCache)
            {
                DirtyCacheEntry dirty = pair.Value;
                if (dirty.GroupSize == groupSize && dirty.PageIndex == pageIndex)
                    toRemove.Add(pair.Key);
            }

            for (int i = 0; i < toRemove.Count; i++)
                _dirtyCache.Remove(toRemove[i]);
        }

        private static bool RectsIntersect(RectInt a, RectInt b)
        {
            return a.x < b.xMax && a.xMax > b.x && a.y < b.yMax && a.yMax > b.y;
        }
    }
}
