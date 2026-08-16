using DynamicAtlas.Packing;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace DynamicAtlas
{
    internal sealed class AtlasPage
    {
        private readonly int _padding;

        public int PageIndex { get; }
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
            var freeRects = new List<RectInt>();
            Packer.CopyFreeRectangles(freeRects);
            Texture2D grayStamp = DynamicAtlasGpuFill.GetGrayStamp(Size);
            for (int i = 0; i < freeRects.Count; i++)
                DynamicAtlasGpuFill.CopyRect(grayStamp, Texture, freeRects[i]);
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
