using System.Collections.Generic;
using System.IO;
using System.Text;
using HybridCLR.Editor;
using HybridCLR.Editor.Settings;
using UnityEditor;
using UnityEngine;

namespace GamePlay.Editor.HybridCLR
{
    /// <summary>
    /// 将 HybridCLR 生成的热更 DLL / 补充元数据 DLL 拷贝为 .bytes 到 Bundles 目录。
    /// </summary>
    public static class HybridCLRDllCopyTool
    {
        private const string HotUpdateDestRelativeDir = "Assets/Project/Bundles/HybridCLR/HotUpdate";
        private const string AotMetadataDestRelativeDir = "Assets/Project/Bundles/HybridCLR/AotMetadata";

        [MenuItem("HybridCLR/Project/Copy HotUpdate And AotMetadata Dlls", priority = 200)]
        public static void CopyAll()
        {
            BuildTarget target = EditorUserBuildSettings.activeBuildTarget;
            int hotUpdateCount = CopyHotUpdateDlls(target, out string hotUpdateLog);
            int aotCount = CopyAotMetadataDlls(target, out string aotLog);

            AssetDatabase.Refresh();

            var sb = new StringBuilder();
            sb.AppendLine($"[HybridCLRDllCopy] target={target}");
            sb.AppendLine(hotUpdateLog);
            sb.AppendLine(aotLog);
            sb.AppendLine($"done. HotUpdate={hotUpdateCount}, AotMetadata={aotCount}");

            if (hotUpdateCount <= 0 || aotCount <= 0)
            {
                Debug.LogError(sb.ToString());
                EditorUtility.DisplayDialog(
                    "HybridCLR Copy Dlls",
                    "拷贝未完全成功，请查看 Console。\n需先执行 Generate/All 或 CompileDll。",
                    "OK");
                return;
            }

            Debug.Log(sb.ToString());
            EditorUtility.DisplayDialog(
                "HybridCLR Copy Dlls",
                $"拷贝完成\nHotUpdate: {hotUpdateCount}\nAotMetadata: {aotCount}",
                "OK");
        }

        [MenuItem("HybridCLR/Project/Copy HotUpdate Dlls Only", priority = 201)]
        public static void CopyHotUpdateOnly()
        {
            BuildTarget target = EditorUserBuildSettings.activeBuildTarget;
            int count = CopyHotUpdateDlls(target, out string log);
            AssetDatabase.Refresh();
            if (count <= 0)
            {
                Debug.LogError(log);
                return;
            }

            Debug.Log(log);
        }

        [MenuItem("HybridCLR/Project/Copy AotMetadata Dlls Only", priority = 202)]
        public static void CopyAotMetadataOnly()
        {
            BuildTarget target = EditorUserBuildSettings.activeBuildTarget;
            int count = CopyAotMetadataDlls(target, out string log);
            AssetDatabase.Refresh();
            if (count <= 0)
            {
                Debug.LogError(log);
                return;
            }

            Debug.Log(log);
        }

        private static int CopyHotUpdateDlls(BuildTarget target, out string log)
        {
            string srcDir = ResolveAbsoluteDir(SettingsUtil.GetHotUpdateDllsOutputDirByTarget(target));
            string destDir = ResolveAbsoluteDir(HotUpdateDestRelativeDir);
            Directory.CreateDirectory(destDir);

            var sb = new StringBuilder();
            sb.AppendLine($"HotUpdate src: {srcDir}");
            sb.AppendLine($"HotUpdate dest: {destDir}");

            if (!Directory.Exists(srcDir))
            {
                sb.AppendLine("source dir missing. Run HybridCLR/CompileDll or Generate/All first.");
                log = sb.ToString();
                return 0;
            }

            IReadOnlyList<string> dllFiles = SettingsUtil.HotUpdateAssemblyFilesExcludePreserved;
            int copied = 0;
            foreach (string dllFile in dllFiles)
            {
                if (TryCopyAsBytes(srcDir, destDir, dllFile, sb))
                    copied++;
            }

            log = sb.ToString();
            return copied;
        }

        private static int CopyAotMetadataDlls(BuildTarget target, out string log)
        {
            string srcDir = ResolveAbsoluteDir(SettingsUtil.GetAssembliesPostIl2CppStripDir(target));
            string destDir = ResolveAbsoluteDir(AotMetadataDestRelativeDir);
            Directory.CreateDirectory(destDir);

            var sb = new StringBuilder();
            sb.AppendLine($"AotMetadata src: {srcDir}");
            sb.AppendLine($"AotMetadata dest: {destDir}");

            if (!Directory.Exists(srcDir))
            {
                sb.AppendLine("source dir missing. Run HybridCLR/Generate/All first.");
                log = sb.ToString();
                return 0;
            }

            List<string> dllFiles = CollectAotDllFileNames();
            if (dllFiles.Count == 0)
            {
                sb.AppendLine(
                    "no AOT dll list. Fill HybridCLR Settings patchAOTAssemblies " +
                    "or ensure AOTGenericReferences.PatchedAOTAssemblyList is generated.");
                log = sb.ToString();
                return 0;
            }

            int copied = 0;
            foreach (string dllFile in dllFiles)
            {
                if (TryCopyAsBytes(srcDir, destDir, dllFile, sb))
                    copied++;
            }

            log = sb.ToString();
            return copied;
        }

        private static List<string> CollectAotDllFileNames()
        {
            var result = new List<string>();
            string[] configured = HybridCLRSettings.Instance.patchAOTAssemblies;
            if (configured != null && configured.Length > 0)
            {
                foreach (string name in configured)
                    result.Add(NormalizeDllFileName(name));
                return result;
            }

            IReadOnlyList<string> fromGenerate = AOTGenericReferences.PatchedAOTAssemblyList;
            if (fromGenerate != null)
            {
                foreach (string name in fromGenerate)
                    result.Add(NormalizeDllFileName(name));
            }

            return result;
        }

        private static string NormalizeDllFileName(string assemblyName)
        {
            if (string.IsNullOrEmpty(assemblyName))
                return assemblyName;
            return assemblyName.EndsWith(".dll") ? assemblyName : assemblyName + ".dll";
        }

        private static bool TryCopyAsBytes(string srcDir, string destDir, string dllFileName, StringBuilder log)
        {
            string srcPath = Path.Combine(srcDir, dllFileName);
            if (!File.Exists(srcPath))
            {
                log.AppendLine($"missing: {srcPath}");
                return false;
            }

            string destPath = Path.Combine(destDir, dllFileName + ".bytes");
            File.Copy(srcPath, destPath, true);
            log.AppendLine($"copied: {dllFileName} -> {destPath}");
            return true;
        }

        private static string ResolveAbsoluteDir(string path)
        {
            if (Path.IsPathRooted(path))
                return path;
            return Path.GetFullPath(Path.Combine(SettingsUtil.ProjectDir, path));
        }
    }
}
