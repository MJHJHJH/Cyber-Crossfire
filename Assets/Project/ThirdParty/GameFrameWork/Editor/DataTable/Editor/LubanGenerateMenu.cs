using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace GameFramework.Editor
{
    /// <summary>
    /// 框架层菜单：调用 Tools/DataTablesTool 全量导出到 Assets/DataTable（生成目录）。
    /// </summary>
    public static class LubanGenerateMenu
    {
        private const string MENU_PATH = "Tools/Luban/Generate All Tables";
        private const int TIMEOUT_MS = 300000;

        [MenuItem(MENU_PATH, priority = 100)]
        public static void GenerateAllTables()
        {
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string batPath = Path.Combine(projectRoot, "Tools", "DataTablesTool", "DataTables", "gen_all.bat");
            if (!File.Exists(batPath))
            {
                EditorUtility.DisplayDialog("Luban", "找不到导出脚本:\n" + batPath, "OK");
                return;
            }

            if (!EditorUtility.DisplayDialog(
                    "Luban",
                    "将执行全量导出：\nJson / Bin 代码与数据 + Adapters\n输出到 Assets/DataTable\n\n"
                    + "注意：正式源表目录是\nTools/DataTablesTool/DataTables/Datas/\n"
                    + "请编辑 Datas 下的 #Sound.xlsx / #Weapon.xlsx 等；\n"
                    + "Datas_Gen 是临时目录，改它不会进导出结果。\n\n"
                    + "可能需要数秒到数十秒，期间请等待。",
                    "开始",
                    "取消"))
                return;

            try
            {
                EditorUtility.DisplayProgressBar("Luban", "正在导出配表...", 0.35f);
                string log = RunBat(batPath, Path.GetDirectoryName(batPath));
                EditorUtility.ClearProgressBar();

                AssetDatabase.Refresh();
                UnityEngine.Debug.Log("[Luban] Generate All Tables finished.\n" + log);
                EditorUtility.DisplayDialog("Luban", "导出完成，已 Refresh。\n详情见 Console。", "OK");
            }
            catch (Exception e)
            {
                EditorUtility.ClearProgressBar();
                UnityEngine.Debug.LogError("[Luban] Generate All Tables failed: " + e);
                EditorUtility.DisplayDialog("Luban", "导出失败，详见 Console。", "OK");
            }
        }

        private static string RunBat(string batPath, string workingDirectory)
        {
            var psi = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = "/c \"\"" + batPath + "\"\"",
                WorkingDirectory = workingDirectory,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.GetEncoding(0),
                StandardErrorEncoding = Encoding.GetEncoding(0),
            };
            psi.EnvironmentVariables["LUBAN_NOPAUSE"] = "1";

            using (var process = new Process { StartInfo = psi })
            {
                var output = new StringBuilder(4096);
                process.OutputDataReceived += (_, args) =>
                {
                    if (!string.IsNullOrEmpty(args.Data))
                        output.AppendLine(args.Data);
                };
                process.ErrorDataReceived += (_, args) =>
                {
                    if (!string.IsNullOrEmpty(args.Data))
                        output.AppendLine(args.Data);
                };

                if (!process.Start())
                    throw new InvalidOperationException("无法启动 gen_all.bat。");

                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                if (!process.WaitForExit(TIMEOUT_MS))
                {
                    try
                    {
                        process.Kill();
                    }
                    catch
                    {
                        // ignored
                    }

                    throw new TimeoutException("导出超时（>" + (TIMEOUT_MS / 1000) + "s）。");
                }

                process.WaitForExit();

                if (process.ExitCode != 0)
                {
                    throw new InvalidOperationException(
                        "gen_all.bat exit=" + process.ExitCode + "\n" + output);
                }

                return output.ToString();
            }
        }
    }
}
