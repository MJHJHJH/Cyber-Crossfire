using GameFramework;
using GameFramework.Event;
using UnityEngine;

namespace GamePlay.AssetPatch
{
    /// <summary>
    /// 临时订阅补丁事件并打日志，验证数据层；正式 Patch UI 另 change。
    /// 挂到任意常驻物体即可（如 GameFrameWork 根节点）。
    /// </summary>
    public sealed class AssetPatchDebugLogger : MonoBehaviour
    {
        private void OnEnable()
        {
            if (GameFrameWork.Event == null)
                return;

            GameFrameWork.Event.Subscribe(AssetPatchStepsChangeEventArgs.EventId, OnSteps);
            GameFrameWork.Event.Subscribe(AssetPatchInitializeFailedEventArgs.EventId, OnInitFailed);
            GameFrameWork.Event.Subscribe(AssetPatchVersionRequestFailedEventArgs.EventId, OnVersionFailed);
            GameFrameWork.Event.Subscribe(AssetPatchManifestUpdateFailedEventArgs.EventId, OnManifestFailed);
            GameFrameWork.Event.Subscribe(AssetPatchFoundUpdateFilesEventArgs.EventId, OnFound);
            GameFrameWork.Event.Subscribe(AssetPatchDownloadUpdateEventArgs.EventId, OnDownloadUpdate);
            GameFrameWork.Event.Subscribe(AssetPatchDownloadFailedEventArgs.EventId, OnDownloadFailed);
        }

        private void OnDisable()
        {
            if (GameFrameWork.Event == null)
                return;

            GameFrameWork.Event.Unsubscribe(AssetPatchStepsChangeEventArgs.EventId, OnSteps);
            GameFrameWork.Event.Unsubscribe(AssetPatchInitializeFailedEventArgs.EventId, OnInitFailed);
            GameFrameWork.Event.Unsubscribe(AssetPatchVersionRequestFailedEventArgs.EventId, OnVersionFailed);
            GameFrameWork.Event.Unsubscribe(AssetPatchManifestUpdateFailedEventArgs.EventId, OnManifestFailed);
            GameFrameWork.Event.Unsubscribe(AssetPatchFoundUpdateFilesEventArgs.EventId, OnFound);
            GameFrameWork.Event.Unsubscribe(AssetPatchDownloadUpdateEventArgs.EventId, OnDownloadUpdate);
            GameFrameWork.Event.Unsubscribe(AssetPatchDownloadFailedEventArgs.EventId, OnDownloadFailed);
        }

        private static void OnSteps(object sender, GameEventArgs e)
        {
            var args = (AssetPatchStepsChangeEventArgs)e;
            Debug.Log($"[AssetPatch] {args.Tips}");
        }

        private static void OnInitFailed(object sender, GameEventArgs e)
        {
            var args = (AssetPatchInitializeFailedEventArgs)e;
            Debug.LogWarning($"[AssetPatch][{args.PackageName}] Initialize failed: {args.Error}");
        }

        private static void OnVersionFailed(object sender, GameEventArgs e)
        {
            var args = (AssetPatchVersionRequestFailedEventArgs)e;
            Debug.LogWarning($"[AssetPatch][{args.PackageName}] Version request failed: {args.Error}");
        }

        private static void OnManifestFailed(object sender, GameEventArgs e)
        {
            var args = (AssetPatchManifestUpdateFailedEventArgs)e;
            Debug.LogWarning($"[AssetPatch][{args.PackageName}] Manifest update failed: {args.Error}");
        }

        private static void OnFound(object sender, GameEventArgs e)
        {
            var args = (AssetPatchFoundUpdateFilesEventArgs)e;
            Debug.Log(
                $"[AssetPatch][{args.PackageName}] Found update files: {args.TotalCount}, {args.TotalBytes} bytes");
        }

        private static void OnDownloadUpdate(object sender, GameEventArgs e)
        {
            var args = (AssetPatchDownloadUpdateEventArgs)e;
            Debug.Log(
                $"[AssetPatch][{args.PackageName}] Download {args.CurrentDownloadCount}/{args.TotalDownloadCount} " +
                $"{args.CurrentDownloadBytes}/{args.TotalDownloadBytes}");
        }

        private static void OnDownloadFailed(object sender, GameEventArgs e)
        {
            var args = (AssetPatchDownloadFailedEventArgs)e;
            Debug.LogWarning($"[AssetPatch][{args.PackageName}] Download failed: {args.FileName} {args.Error}");
        }
    }
}
