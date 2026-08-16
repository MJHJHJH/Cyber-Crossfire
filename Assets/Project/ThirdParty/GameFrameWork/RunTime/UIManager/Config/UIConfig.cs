using UnityEngine;
using UnityEngine.UI;

namespace GameFramework
{
    /// <summary>
    /// 界面组打开策略：单例界面再次打开时的栈处理方式。
    /// </summary>
    public enum UIGroupOpenMode
    {
        /// <summary>
        /// 关闭目标之上的界面，使目标成为栈顶；已是栈顶则直接返回。
        /// </summary>
        ClearToTop = 0,

        /// <summary>
        /// 将已有单例 Refocus 到栈顶，不关闭中间界面。
        /// </summary>
        Normal = 1,
    }

    /// <summary>
    /// UI 相关配置定义（代码/配置表驱动，不依赖 Inspector 序列化业务组列表）。
    /// </summary>
    public static class UIConfig
    {
        /// <summary>
        /// 界面组配置条目。
        /// </summary>
        public sealed class UIGroupConfig
        {
            public string Name = "Default";
            public int Depth;
            public UIGroupOpenMode OpenMode = UIGroupOpenMode.ClearToTop;
        }

        /// <summary>
        /// Overlay Canvas 运行时配置。
        /// </summary>
        public struct OverlayCanvasConfig
        {
            public int SortingOrder;
            public bool ApplyScaler;
            public CanvasScaler.ScaleMode ScaleMode;
            public Vector2 ReferenceResolution;
            public float MatchWidthOrHeight;
            public bool RaycasterEnabled;

            public static OverlayCanvasConfig Default => new OverlayCanvasConfig
            {
                SortingOrder = 0,
                ApplyScaler = true,
                ScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize,
                ReferenceResolution = new Vector2(1920f, 1080f),
                MatchWidthOrHeight = 0.5f,
                RaycasterEnabled = true
            };
        }

        /// <summary>
        /// Camera Canvas 运行时配置（不改动 UICamera 引用）。
        /// </summary>
        public struct CameraCanvasConfig
        {
            public int SortingOrder;
            public float PlaneDistance;
            public bool ApplyScaler;
            public CanvasScaler.ScaleMode ScaleMode;
            public Vector2 ReferenceResolution;
            public float MatchWidthOrHeight;
            public bool RaycasterEnabled;

            public static CameraCanvasConfig Default => new CameraCanvasConfig
            {
                SortingOrder = 100,
                PlaneDistance = 100f,
                ApplyScaler = true,
                ScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize,
                ReferenceResolution = new Vector2(1920f, 1080f),
                MatchWidthOrHeight = 0.5f,
                RaycasterEnabled = true
            };
        }

        /// <summary>
        /// 对象池相关参数设置
        /// </summary>
        public struct UIObjectPoolConfig
        {
            public float InstanceAutoReleaseInterval; //每隔多少秒尝试一次自动 Release()
            public int InstanceCapacity; //	池内对象数量上限；超了就裁
            public float InstanceExpireTime; //未使用（Unspawn 后）超过多少秒算过期，可被裁
            public int InstancePriority; //这个对象池相对其它池的优先级

            public static UIObjectPoolConfig Default => new UIObjectPoolConfig
            {
                InstanceAutoReleaseInterval = 10,
                InstanceCapacity = 10,
                InstanceExpireTime = 10,
                InstancePriority = 0
            };
        }
    }
}
