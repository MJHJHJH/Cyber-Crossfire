using System.IO;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace DynamicAtlas
{
    /// <summary>
    /// Location 约定：与 YooAsset 地址一致，为本项目资源文件名（无扩展名）。
    /// 动态图集图源须收集进 DefaultPackage（见 <see cref="DynamicAtlasLoader.DefaultPackageName"/>）。
    /// </summary>
    public static class DynamicAtlasLocation
    {
        /// <summary>
        /// 从 Sprite 解析 Location。
        /// Editor：优先资源路径文件名；Runtime：回退 texture.name（需与 Address 文件名约定一致）。
        /// </summary>
        public static bool TryResolveFromSprite(Sprite sprite, out string location)
        {
            location = null;
            if (sprite == null)
                return false;

#if UNITY_EDITOR
            string path = AssetDatabase.GetAssetPath(sprite);
            if (!string.IsNullOrEmpty(path))
            {
                location = Path.GetFileNameWithoutExtension(path);
                if (!string.IsNullOrEmpty(location))
                    return true;
            }
#endif
            if (sprite.texture != null && !string.IsNullOrEmpty(sprite.texture.name))
            {
                location = sprite.texture.name;
                return true;
            }

            if (!string.IsNullOrEmpty(sprite.name))
            {
                location = sprite.name;
                return true;
            }

            return false;
        }

        public static bool TryResolveFromTexture(Texture texture, out string location)
        {
            location = null;
            if (texture == null)
                return false;

#if UNITY_EDITOR
            string path = AssetDatabase.GetAssetPath(texture);
            if (!string.IsNullOrEmpty(path))
            {
                location = Path.GetFileNameWithoutExtension(path);
                if (!string.IsNullOrEmpty(location))
                    return true;
            }
#endif
            if (!string.IsNullOrEmpty(texture.name))
            {
                location = texture.name;
                return true;
            }

            return false;
        }
    }
}
