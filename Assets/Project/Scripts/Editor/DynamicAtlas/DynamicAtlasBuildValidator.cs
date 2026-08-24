#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace DynamicAtlas.Editor
{
    /// <summary>
    /// 构建前校验：图源目录内所有纹理在 Default/Android/iPhone 平台的最终导入格式必须为 RGBA32，
    /// 违规直接中断构建——把运行时 CopyTexture 跨格式失败（静默白图）前置为构建失败。
    /// </summary>
    public class DynamicAtlasBuildValidator : IPreprocessBuildWithReport
    {
        public int callbackOrder => 0;

        public void OnPreprocessBuild(BuildReport report)
        {
            string[] guids = AssetDatabase.FindAssets(
                "t:Texture2D", new[] { DynamicAtlasTextureSettings.SourceRoot });
            if (guids.Length == 0)
                return;

            var violations = new List<string>();
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var ti = AssetImporter.GetAtPath(path) as TextureImporter;
                if (ti == null)
                    continue;

                if (!IsRgba32(ti, "") || !IsRgba32(ti, "Android") || !IsRgba32(ti, "iPhone"))
                    violations.Add(path);
            }

            if (violations.Count > 0)
            {
                string list = string.Join("\n  ", violations);
                throw new BuildFailedException(
                    $"[DynamicAtlas] 图源未按约定导入为 RGBA32（共 {violations.Count} 个）：\n  {list}\n" +
                    "图源目录由 DynamicAtlasTexturePostprocessor 强制 RGBA32，请修正导入设置后重试。");
            }
        }

        /// <summary>平台覆盖生效时看 format；未覆盖时回退看默认压缩设置。</summary>
        private static bool IsRgba32(TextureImporter ti, string platform)
        {
            TextureImporterPlatformSettings settings = ti.GetPlatformTextureSettings(platform);
            if (settings.overridden)
                return settings.format == TextureImporterFormat.RGBA32;
            return ti.textureCompression == TextureImporterCompression.Uncompressed;
        }
    }
}
#endif
