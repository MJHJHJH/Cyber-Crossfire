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
    /// 热更初始化流程：加载 HotUpdate 程序集与 AOT 补充元数据，扫描注册热更流程后进入配表流程。
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

                s_MainProcedureType = ResolveProcedureType(hotUpdateAssembly, HotUpdateMainTypeName);
                if (s_MainProcedureType == null)
                    return;

                if (!RegisterHotUpdateProcedures(hotUpdateAssembly))
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

        /// <summary>扫描热更程序集中所有非抽象 ProcedureBase 子类并注册进 Procedure Fsm。</summary>
        private static bool RegisterHotUpdateProcedures(Assembly hotUpdateAssembly)
        {
            ProcedureComponent procedureComponent = GameFrameWork.Procedure;
            if (procedureComponent == null)
            {
                Debug.LogError("[ProcedureHotUpdateInit] ProcedureComponent is missing.");
                return false;
            }

            Type procedureBaseType = typeof(ProcedureBase);
            Type[] types;
            try
            {
                types = hotUpdateAssembly.GetExportedTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                types = ex.Types.Where(t => t != null).ToArray();
            }

            int registered = 0;
            for (int i = 0; i < types.Length; i++)
            {
                Type type = types[i];
                if (type == null || !type.IsClass || type.IsAbstract || type.IsGenericTypeDefinition)
                    continue;
                if (!procedureBaseType.IsAssignableFrom(type))
                    continue;

                if (!RegisterProcedure(procedureComponent, type))
                    return false;
                registered++;
            }

            if (registered == 0)
            {
                Debug.LogError(
                    $"[ProcedureHotUpdateInit] No ProcedureBase found in '{HotUpdateAssemblyName}'.");
                return false;
            }

            if (!procedureComponent.HasProcedure(s_MainProcedureType))
            {
                Debug.LogError(
                    $"[ProcedureHotUpdateInit] Main procedure '{HotUpdateMainTypeName}' was not registered.");
                return false;
            }

            return true;
        }

        private static bool RegisterProcedure(ProcedureComponent procedureComponent, Type procedureType)
        {
            object instance = Activator.CreateInstance(procedureType);
            ProcedureBase procedure = instance as ProcedureBase;
            if (procedure == null)
            {
                Debug.LogError(
                    $"[ProcedureHotUpdateInit] Create instance of '{procedureType.FullName}' failed.");
                return false;
            }

            procedureComponent.AddProcedure(procedure);
            Debug.Log(
                $"[ProcedureHotUpdateInit] Registered hot update procedure: {procedureType.FullName}");
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

        private static Type ResolveProcedureType(Assembly hotUpdateAssembly, string typeName)
        {
            Type procedureType = hotUpdateAssembly.GetType(typeName);
            if (procedureType == null)
            {
                Debug.LogError(
                    $"[ProcedureHotUpdateInit] Type '{typeName}' not found in '{HotUpdateAssemblyName}'.");
                return null;
            }

            return procedureType;
        }
    }
}
