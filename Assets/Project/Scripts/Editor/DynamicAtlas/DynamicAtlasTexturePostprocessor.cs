#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace DynamicAtlas.Editor
{
    /// <summary>
    /// 动态图集图源目录约定。
    /// 约束链：CopyTexture 要求源与图集页（RGBA32）同格式；压缩纹理（ETC2/ASTC/BC）的拷贝区域
    /// 必须 4×4 块对齐，而动态装箱的 Sprite 区域无法保证 → 图集页只能用无压缩 RGBA32，
    /// 因此图源目录内的纹理在所有平台刻意放弃平台压缩（ETC2/ASTC/BC），统一 RGBA32。
    /// </summary>
    public static class DynamicAtlasTextureSettings
    {
        /// <summary>动态图集图源根目录（约定，须加入 DefaultPackage 收集，AddressByFileName）。</summary>
        public const string SourceRoot = "Assets/Project/Art/Sprites/DynamicAtlas";
    }

    /// <summary>
    /// 图源目录内资源导入/重导时强制全平台 RGBA32（Default/Android/iPhone 平台覆盖一起压制，
    /// 否则移动端构建会走 ETC2/ASTC 默认压缩，运行时跨格式拷贝失败）。
    /// 注意：postprocessor 只在导入流程执行，存量资源需手动 Reimport 一次生效。
    /// </summary>
    public class DynamicAtlasTexturePostprocessor : AssetPostprocessor
    {
        private void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(DynamicAtlasTextureSettings.SourceRoot))
                return;

            var ti = (TextureImporter)assetImporter;
            ti.textureType = TextureImporterType.Sprite;
            ti.mipmapEnabled = false; // 图集页无 mipmap，源有 mip 也只拷 mip0，纯浪费
            ti.textureCompression = TextureImporterCompression.Uncompressed;

            ForcePlatform(ti, ""); // Default：编辑器/PC 也须一致，否则编辑器 Play 同样跨格式失败
            ForcePlatform(ti, "Android"); // 平台覆盖必须显式压制，否则构建走 ETC2
            ForcePlatform(ti, "iPhone"); // 否则构建走 ASTC
        }

        private static void ForcePlatform(TextureImporter ti, string platform)
        {
            // 读原设置再改，避免 SetPlatformTextureSettings 清掉 maxTextureSize 等其它配置
            TextureImporterPlatformSettings settings = ti.GetPlatformTextureSettings(platform);
            settings.overridden = true;
            settings.format = TextureImporterFormat.RGBA32;
            ti.SetPlatformTextureSettings(settings);
        }
    }
}
#endif
