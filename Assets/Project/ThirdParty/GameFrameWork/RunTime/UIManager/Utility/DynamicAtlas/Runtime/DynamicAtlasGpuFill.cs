using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace DynamicAtlas
{
    /// <summary>
    /// 纯色模板：用 1x1 + Blit 在 GPU 上拉伸生成，之后对图集的清除一律 CopyTexture。
    /// 注意：CopyTexture 本身不能缩放，只能按像素拷贝；拉伸只发生在模板生成阶段。
    /// </summary>
    internal static class DynamicAtlasGpuFill
    {
        private static readonly Dictionary<int, Texture2D> s_transparentBySize = new Dictionary<int, Texture2D>();
        private static readonly Dictionary<int, Texture2D> s_grayBySize = new Dictionary<int, Texture2D>();
        private static Texture2D s_pixelTransparent;
        private static Texture2D s_pixelGray;

        public static bool CopyRect(Texture2D stamp, Texture2D dest, RectInt destRect)
        {
            if (stamp == null || dest == null)
                return false;
            if (ReferenceEquals(stamp, dest))
                return false;
            if (destRect.width <= 0 || destRect.height <= 0)
                return false;
            if (destRect.x < 0 || destRect.y < 0 ||
                destRect.xMax > dest.width || destRect.yMax > dest.height)
                return false;
            if (destRect.width > stamp.width || destRect.height > stamp.height)
                return false;
            if (SystemInfo.copyTextureSupport == CopyTextureSupport.None)
                return false;

            Graphics.CopyTexture(
                stamp, 0, 0,
                0, 0, destRect.width, destRect.height,
                dest, 0, 0,
                destRect.x, destRect.y);
            return true;
        }

        public static Texture2D GetTransparentStamp(int pageSize)
        {
            return GetOrCreateStamp(pageSize, DynamicAtlasConfig.TransparentColor, s_transparentBySize, ref s_pixelTransparent);
        }

        public static Texture2D GetGrayStamp(int pageSize)
        {
            return GetOrCreateStamp(pageSize, DynamicAtlasConfig.InitialFillColor, s_grayBySize, ref s_pixelGray);
        }

        private static Texture2D GetOrCreateStamp(
            int pageSize,
            Color32 color,
            Dictionary<int, Texture2D> cache,
            ref Texture2D pixelSource)
        {
            if (cache.TryGetValue(pageSize, out Texture2D existing) && existing != null)
                return existing;

            if (pixelSource == null)
                pixelSource = CreateSolidPixel(color);

            Texture2D stamp = CreateStampByGpuBlit(pageSize, pixelSource);
            cache[pageSize] = stamp;
            return stamp;
        }

        private static Texture2D CreateSolidPixel(Color32 color)
        {
            var tex = new Texture2D(1, 1, DynamicAtlasConfig.PageTextureFormat, false, false)
            {
                name = $"DynamicAtlas_Pixel_{color.r}_{color.g}_{color.b}_{color.a}",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };
            tex.SetPixels32(new[] { color });
            tex.Apply(false, true);
            return tex;
        }

        private static Texture2D CreateStampByGpuBlit(int size, Texture2D pixelSource)
        {
            RenderTexture rt = RenderTexture.GetTemporary(
                size, size, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            rt.filterMode = FilterMode.Point;

            var prev = RenderTexture.active;
            Graphics.Blit(pixelSource, rt);

            var stamp = new Texture2D(size, size, DynamicAtlasConfig.PageTextureFormat, false, false)
            {
                name = $"DynamicAtlas_Stamp_{size}_{pixelSource.name}",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };


            // RT -> Texture2D：优先 GPU CopyTexture（勿再 Apply，以免用空 CPU 缓冲覆盖 GPU）
            if (SystemInfo.copyTextureSupport != CopyTextureSupport.None)
            {
                Graphics.CopyTexture(rt, stamp);
            }
            else
            {
                RenderTexture.active = rt;
                stamp.ReadPixels(new Rect(0, 0, size, size), 0, 0);
                stamp.Apply(false, true);
            }

            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(rt);
            return stamp;
        }
    }
}
