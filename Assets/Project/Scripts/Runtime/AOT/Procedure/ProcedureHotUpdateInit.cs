using System;
using System.Linq;
using System.Reflection;
using System.Threading;
using Cysharp.Threading.Tasks;
using GameFramework;
using GameFramework.Procedure;
using HybridCLR;
using UnityEngine;
using YooAsset;
using ProcedureOwner = GameFramework.Fsm.IFsm<GameFramework.Procedure.IProcedureManager>;

namespace GamePlay
{
    /// <summary>
    /// 热更初始化流程：加载 HotUpdate 程序集与 AOT 补充元数据，解析热更主流程入口后进入配表流程。
    /// </summary>
    public sealed class ProcedureHotUpdateInit : ProcedureBase
    {
        public const string HotUpdateAssemblyName = "HotUpdate";
        public const string HotUpdateMainLocation = "HotUpdate.dll";
        public const string HotUpdateMainTypeName = "GamePlay.ProcedureMain";
        public const string AotMetadataAssetDir = "Assets/Project/Bundles/HybridCLR/AotMetadata";
        public const string HybridClrPackageName = "DefaultRawPackage";

        private static Type s_MainProcedureType;

        /// <summary>热更主流程类型（已注册进 Procedure Fsm，后续流程据此切换）。</summary>
        public static Type MainProcedureType => s_MainProcedureType;

        private CancellationTokenSource m_Cts;

        protected override void OnEnter(ProcedureOwner procedureOwner)
        {
            base.OnEnter(procedureOwner);

            m_Cts = new CancellationTokenSource();
            EnterAsync(procedureOwner, m_Cts.Token).Forget();
        }

        protected override void OnLeave(ProcedureOwner procedureOwner, bool isShutdown)
        {
            if (m_Cts != null)
            {
                m_Cts.Cancel();
                m_Cts.Dispose();
                m_Cts = null;
            }

            base.OnLeave(procedureOwner, isShutdown);
        }

        private async UniTaskVoid EnterAsync(ProcedureOwner procedureOwner, CancellationToken cancellationToken)
        {
            try
            {
                Assembly hotUpdateAssembly = await LoadHotUpdateAssemblyAsync(cancellationToken);
                if (hotUpdateAssembly == null)
                    return;

#if !UNITY_EDITOR
                await LoadAotMetadataAsync(cancellationToken);
#endif

                s_MainProcedureType = ResolveMainProcedureType(hotUpdateAssembly);
                if (s_MainProcedureType == null)
                    return;

                if (!RegisterMainProcedure())
                    return;

                ChangeState<ProcedureDataTableInit>(procedureOwner);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ProcedureHotUpdateInit] {ex}");
            }
        }

        /// <summary>反射创建热更主流程实例并注册进 Procedure Fsm，使其可被 ChangeState 切换。</summary>
        private static bool RegisterMainProcedure()
        {
            object instance = Activator.CreateInstance(s_MainProcedureType);
            if (instance == null)
            {
                Debug.LogError(
                    $"[ProcedureHotUpdateInit] Create instance of '{HotUpdateMainTypeName}' failed.");
                return false;
            }

            ProcedureBase procedure = instance as ProcedureBase;
            if (procedure == null)
            {
                Debug.LogError(
                    $"[ProcedureHotUpdateInit] '{HotUpdateMainTypeName}' is not a ProcedureBase.");
                return false;
            }

            ProcedureComponent procedureComponent = GameFrameWork.Procedure;
            if (procedureComponent == null)
            {
                Debug.LogError("[ProcedureHotUpdateInit] ProcedureComponent is missing.");
                return false;
            }

            procedureComponent.AddProcedure(procedure);
            Debug.Log(
                $"[ProcedureHotUpdateInit] Registered hot update procedure: {s_MainProcedureType.FullName}");
            return true;
        }

        private static async UniTask<Assembly> LoadHotUpdateAssemblyAsync(CancellationToken cancellationToken)
        {
#if UNITY_EDITOR
            // Editor 下热更程序集已由 Unity 编译进域内，无需加载
            Assembly hotUpdate = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(a => a.GetName().Name == HotUpdateAssemblyName);
            if (hotUpdate == null)
            {
                Debug.LogError(
                    $"[ProcedureHotUpdateInit] Assembly '{HotUpdateAssemblyName}' not found in current domain.");
                return null;
            }

            return hotUpdate;
#else
            if (!YooAssets.TryGetPackage(HybridClrPackageName, out ResourcePackage package) || package == null)
            {
                Debug.LogError($"[ProcedureHotUpdateInit] Package '{HybridClrPackageName}' is missing.");
                return null;
            }

            byte[] dllBytes = await LoadRawBytesAsync(package, HotUpdateMainLocation, cancellationToken);
            if (dllBytes == null)
                return null;

            return Assembly.Load(dllBytes);
#endif
        }

#if !UNITY_EDITOR
        private static async UniTask LoadAotMetadataAsync(CancellationToken cancellationToken)
        {
            if (!YooAssets.TryGetPackage(HybridClrPackageName, out ResourcePackage package) || package == null)
                return;

            AssetInfo[] infos = package.GetAllAssetInfos();
            foreach (AssetInfo info in infos)
            {
                if (!info.IsValid || string.IsNullOrEmpty(info.AssetPath))
                    continue;
                if (!info.AssetPath.StartsWith(AotMetadataAssetDir, StringComparison.Ordinal))
                    continue;

                byte[] metadataBytes = await LoadRawBytesAsync(package, info.Address, cancellationToken);
                if (cancellationToken.IsCancellationRequested)
                    return;
                if (metadataBytes == null)
                {
                    Debug.LogWarning($"[ProcedureHotUpdateInit] Aot metadata '{info.Address}' is empty.");
                    continue;
                }

                LoadImageErrorCode errorCode =
                    RuntimeApi.LoadMetadataForAOTAssembly(metadataBytes, HomologousImageMode.SuperSet);
                if (errorCode == LoadImageErrorCode.OK)
                    Debug.Log($"[ProcedureHotUpdateInit] LoadMetadataForAOTAssembly: {info.Address}");
                else
                    Debug.LogError(
                        $"[ProcedureHotUpdateInit] LoadMetadataForAOTAssembly '{info.Address}' failed: {errorCode}");
            }
        }

        private static async UniTask<byte[]> LoadRawBytesAsync(
            ResourcePackage package, string location, CancellationToken cancellationToken)
        {
            AssetHandle handle = package.LoadAssetAsync<RawFileObject>(location);
            try
            {
                await handle.ToUniTask(cancellationToken: cancellationToken);
                if (cancellationToken.IsCancellationRequested)
                    return null;

                if (handle.Status != EOperationStatus.Succeeded)
                {
                    Debug.LogError(
                        $"[ProcedureHotUpdateInit] Load '{location}' failed: {handle.Error}");
                    return null;
                }

                RawFileObject raw = handle.GetAssetObject<RawFileObject>();
                byte[] bytes = raw?.GetBytes();
                if (bytes == null || bytes.Length == 0)
                {
                    Debug.LogError($"[ProcedureHotUpdateInit] '{location}' raw data is empty.");
                    return null;
                }

                return bytes;
            }
            finally
            {
                handle.Release();
            }
        }
#endif

        private static Type ResolveMainProcedureType(Assembly hotUpdateAssembly)
        {
            Type mainType = hotUpdateAssembly.GetType(HotUpdateMainTypeName);
            if (mainType == null)
            {
                Debug.LogError(
                    $"[ProcedureHotUpdateInit] Type '{HotUpdateMainTypeName}' not found in '{HotUpdateAssemblyName}'.");
                return null;
            }

            return mainType;
        }
    }
}
