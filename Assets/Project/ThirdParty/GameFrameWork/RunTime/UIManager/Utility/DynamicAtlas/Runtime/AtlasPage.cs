using DynamicAtlas.Packing;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace DynamicAtlas
{
    internal sealed class AtlasPage
    {
        private readonly int _padding;

        /// <summary>页索引，恒等于所属分组列表中的下标；空页回收 swap 时由 Manager 更新。</summary>
        public int PageIndex { get; internal set; }
        /// <summary>本页活跃条目数（CreateEntry +1 / Release 归零条目 -1），为 0 即空页、可回收。</summary>
        public int ActiveCount { get; internal set; }
        public int Size { get; }
        public Texture2D Texture { get; }
        public RectanglePacker Packer { get; }

        public AtlasPage(int pageIndex, int size, int padding)
        {
            PageIndex = pageIndex;
            Size = size;
            _padding = Mathf.Max(0, padding);
            Texture = new Texture2D(size, size, DynamicAtlasConfig.PageTextureFormat, false, false)
            {
                name = $"DynamicAtlas_{size}_{pageIndex}",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            // 不整页 Copy、不 Apply；写入仅在 Insert/Debug 时按需 CopyTexture

            Packer = new RectanglePacker(size, size, padding);
        }

        public bool TryPack(int width, int height, out RectInt rect)
        {
            rect = default;
            if (!Packer.TryPack(width, height, out IntegerRectangle packed) || packed == null)
                return false;

            rect = new RectInt(packed.X, packed.Y, packed.Width, packed.Height);
            return true;
        }

        public bool TryCopyFrom(Texture source, RectInt sourceRect, RectInt destRect)
        {
            if (source == null)
                return false;

            if (sourceRect.width != destRect.width || sourceRect.height != destRect.height)
                return false;

            if (destRect.xMax > Size || destRect.yMax > Size || destRect.x < 0 || destRect.y < 0)
                return false;

            if (SystemInfo.copyTextureSupport == CopyTextureSupport.None)
                return false;

            // 禁止同源同目标（例如把图集 Sprite 再当源插入）
            if (ReferenceEquals(source, Texture))
            {
                Debug.LogWarning(
                    $"[DynamicAtlas] CopyTexture 拒绝：源与目标为同一图集页 page={PageIndex}, size={Size}");
                return false;
            }

            // Insert 前：透明模板 CopyTexture 覆盖 destRect + padding
            ClearRectWithPadding(destRect);

            Graphics.CopyTexture(
                source, 0, 0,
                sourceRect.x, sourceRect.y, sourceRect.width, sourceRect.height,
                Texture, 0, 0,
                destRect.x, destRect.y);
            return true;
        }

        public void FreeRect(RectInt rect)
        {
            Packer.FreeRectangle(rect.x, rect.y, rect.width, rect.height);
        }

        public bool TryReclaim(RectInt rect)
        {
            return Packer.TryReclaimRectangle(rect.x, rect.y, rect.width, rect.height);
        }

        /// <summary>
        /// Debug：空闲区用灰底模板 CopyTexture 覆盖。不参与正常 Release/Insert。
        /// </summary>
        public void DebugClearFreeAreas()
        {
            ClearFreeAreasWith(DynamicAtlasGpuFill.GetGrayStamp(Size));
        }

        /// <summary>
        /// 内存整理：空闲区像素清为透明并交由 Manager 失效对应脏缓存（显式放弃复活优化）。
        /// </summary>
        public void ClearIdlePixels()
        {
            ClearFreeAreasWith(DynamicAtlasGpuFill.GetTransparentStamp(Size));
        }

        private void ClearFreeAreasWith(Texture2D stamp)
        {
            var freeRects = new List<RectInt>();
            Packer.CopyFreeRectangles(freeRects);
            for (int i = 0; i < freeRects.Count; i++)
                DynamicAtlasGpuFill.CopyRect(stamp, Texture, freeRects[i]);
        }

        public void Destroy()
        {
            if (Texture != null)
                Object.Destroy(Texture);
        }

        private void ClearRectWithPadding(RectInt destRect)
        {
            int xMin = Mathf.Max(0, destRect.x - _padding);
            int yMin = Mathf.Max(0, destRect.y - _padding);
            int xMax = Mathf.Min(Size, destRect.xMax + _padding);
            int yMax = Mathf.Min(Size, destRect.yMax + _padding);

            var clearRect = new RectInt(xMin, yMin, xMax - xMin, yMax - yMin);
            Texture2D transparentStamp = DynamicAtlasGpuFill.GetTransparentStamp(Size);
            DynamicAtlasGpuFill.CopyRect(transparentStamp, Texture, clearRect);
        }
    }
}
