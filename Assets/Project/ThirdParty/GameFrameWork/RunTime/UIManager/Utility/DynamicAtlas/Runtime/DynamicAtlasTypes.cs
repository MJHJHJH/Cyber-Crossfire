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
        SourceIsAtlasPage
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

        public static TextureFormat PageTextureFormat
        {
            get
            {
#if UNITY_ANDROID && !UNITY_EDITOR && DYNAMIC_ATLAS_ANDROID_FORMAT
                return TextureFormat.RGBA32;
#elif UNITY_IOS && !UNITY_EDITOR && DYNAMIC_ATLAS_IOS_FORMAT
                return TextureFormat.RGBA32;
#else
                // ARGB32 语义：Unity 常用 RGBA32 作为可 CopyTexture 的图集页格式
                return TextureFormat.RGBA32;
#endif
            }
        }

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
