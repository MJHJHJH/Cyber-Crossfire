using UnityEngine;

namespace DynamicAtlas
{
    public enum DynamicAtlasGroup
    {
        Size1024 = 1024,
        Size2048 = 2048,
        Size4096 = 4096
    }

    public enum AtlasInsertFailReason
    {
        None = 0,
        EmptyKey,
        NullTexture,
        ExceedsGroupSize,
        ExceedsMaxPageSize,
        PackFailed,
        CopyFailed,
        /// <summary>源纹理已是动态图集页，禁止再作为合图输入以免自拷贝污染。</summary>
        SourceIsAtlasPage,
        /// <summary>图源纹理格式不支持 CopyTexture（须 RGBA32/ARGB32，RenderTexture 放行）。</summary>
        UnsupportedFormat
    }

    public struct AtlasInsertResult
    {
        public bool Success;
        public AtlasInsertFailReason FailReason;
        public AtlasEntry Entry;

        public static AtlasInsertResult Ok(AtlasEntry entry)
        {
            return new AtlasInsertResult
            {
                Success = true,
                FailReason = AtlasInsertFailReason.None,
                Entry = entry
            };
        }

        public static AtlasInsertResult Fail(AtlasInsertFailReason reason)
        {
            return new AtlasInsertResult
            {
                Success = false,
                FailReason = reason,
                Entry = null
            };
        }
    }

    public sealed class AtlasEntry
    {
        /// <summary>复合 key：Location@GroupSize，同 Location 可在不同分组各有一份。</summary>
        public string Key;
        public string Location;
        public int GroupSize;
        public int PageIndex;
        public RectInt Rect;
        public Texture2D PageTexture;
        public Sprite Sprite;
        public int RefCount;
        /// <summary>九宫格边距（像素），供 Sliced/Tiled 使用。</summary>
        public Vector4 Border;
        public Vector2 Pivot;
        public float PixelsPerUnit;
    }

    public static class DynamicAtlasConfig
    {
        public const int DEFAULT_GROUP_SIZE = 1024;
        public const int MAX_PAGE_SIZE = 4096;
        public const int DEFAULT_PADDING = 2;
        public const float DEFAULT_PIXELS_PER_UNIT = 100f;
        /// <summary>脏缓存条目数上限，超出后驱逐最老条目（低水位 = 上限的 3/4）。</summary>
        public const int DEFAULT_DIRTY_CACHE_LIMIT = 512;
        /// <summary>软释放延迟销毁帧数：Release 引用归零后 Sprite 延迟 N 帧销毁，保护"误释放仍显示"场景。</summary>
        public const int DEFAULT_SOFT_RELEASE_FRAMES = 3;

        /// <summary>
        /// 新建图集页底色；Debug 清空空闲区时恢复为此色。
        /// </summary>
        public static readonly Color32 InitialFillColor = new Color32(128, 128, 128, 255);

        public static readonly Color32 TransparentColor = new Color32(0, 0, 0, 0);

        public static readonly Vector2 DefaultPivot = new Vector2(0.5f, 0.5f);

        public static readonly int[] ValidGroupSizes =
        {
            (int)DynamicAtlasGroup.Size1024,
            (int)DynamicAtlasGroup.Size2048,
            (int)DynamicAtlasGroup.Size4096
        };

        /// <summary>
        /// 图集页纹理格式，固定 RGBA32。
        /// 约束链：CopyTexture 要求源与目标同格式；压缩纹理（ETC2/ASTC/BC）的拷贝区域
        /// 必须 4×4 块对齐，而动态装箱的 Sprite 区域无法保证 → 图集页只能用无压缩 RGBA32；
        /// 因此图源也必须全平台 RGBA32（由编辑器 DynamicAtlasTexturePostprocessor 强制）。
        /// </summary>
        public static readonly TextureFormat PageTextureFormat = TextureFormat.RGBA32;

        public static int ToSize(DynamicAtlasGroup group)
        {
            return (int)group;
        }

        public static bool IsValidGroupSize(int size)
        {
            return size == 1024 || size == 2048 || size == 4096;
        }
    }
}
