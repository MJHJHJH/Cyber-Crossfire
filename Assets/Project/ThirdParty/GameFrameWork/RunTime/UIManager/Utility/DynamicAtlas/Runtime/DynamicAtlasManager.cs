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

            int srcW = sourceRect.width;
            int srcH = sourceRect.height;

            if (srcW > DynamicAtlasConfig.MAX_PAGE_SIZE || srcH > DynamicAtlasConfig.MAX_PAGE_SIZE)
                return AtlasInsertResult.Fail(AtlasInsertFailReason.ExceedsMaxPageSize);

            if (srcW > groupSize || srcH > groupSize)
                return AtlasInsertResult.Fail(AtlasInsertFailReason.ExceedsGroupSize);

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

            entry.RefCount--;
            if (entry.RefCount > 0)
                return true;

            if (_groups.TryGetValue(entry.GroupSize, out List<AtlasPage> pages) &&
                entry.PageIndex >= 0 && entry.PageIndex < pages.Count)
            {
                pages[entry.PageIndex].FreeRect(entry.Rect);
            }

            if (entry.Sprite != null)
                Object.Destroy(entry.Sprite);

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
                PixelsPerUnit = entry.PixelsPerUnit
            };

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
