using System.IO;
using UnityEditor;
using UnityEngine;
using YooAsset;

namespace GamePlay.Editor.YooAsset
{
    public static class YooAssetCacheCleaner
    {
        private const string MenuPath = "YooAsset/Clear Editor Cache";

        [MenuItem(MenuPath, false, 200)]
        public static void ClearEditorCache()
        {
            if (EditorApplication.isPlaying)
            {
                EditorUtility.DisplayDialog("YooAsset", "请先退出 Play 模式再清理缓存。", "OK");
                return;
            }

            string cacheRoot = GetEditorCacheRoot();
            if (!Directory.Exists(cacheRoot))
            {
                EditorUtility.DisplayDialog("YooAsset", $"缓存目录不存在：\n{cacheRoot}", "OK");
                return;
            }

            if (!EditorUtility.DisplayDialog(
                    "YooAsset",
                    $"将删除编辑器缓存目录：\n{cacheRoot}\n\n确定继续？",
                    "删除",
                    "取消"))
            {
                return;
            }

            try
            {
                Directory.Delete(cacheRoot, true);
                Debug.Log($"[YooAssetCacheCleaner] deleted: {cacheRoot}");
                EditorUtility.DisplayDialog("YooAsset", $"已清理缓存：\n{cacheRoot}", "OK");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[YooAssetCacheCleaner] {ex}");
                EditorUtility.DisplayDialog("YooAsset", $"清理失败，请查看 Console。\n{ex.Message}", "OK");
            }
        }

        [MenuItem(MenuPath, true)]
        private static bool ValidateClearEditorCache()
        {
            return !EditorApplication.isPlaying;
        }

        private static string GetEditorCacheRoot()
        {
            string projectRoot = Path.GetDirectoryName(Application.dataPath);
            string folderName = YooAssetConfiguration.GetYooFolderName();
            if (string.IsNullOrEmpty(folderName))
                folderName = "yoo";
            return Path.GetFullPath(Path.Combine(projectRoot, "Library", folderName));
        }
    }
}
