using System;
using UnityEngine;

namespace UI.SafeArea
{
    /// <summary>
    /// 全局安全区提供者（静态服务）：
    /// 集中读取 <see cref="Screen.safeArea"/> 与 <see cref="Screen.cutouts"/>（可选合并），
    /// 缓存 + 脏检查，产出归一化 insets（比例），任何 CanvasScaler 模式下均可按比例折算。
    ///
    /// 编辑器预览统一使用 Unity Device Simulator（真实模拟 Screen.safeArea/cutouts），
    /// 本类不提供内置模拟。
    /// </summary>
    public static class SafeAreaProvider
    {
        /// <summary>有效安全区矩形（物理像素）。</summary>
        public static Rect SafeAreaRect { get; private set; }

        /// <summary>当前屏幕尺寸（物理像素）。</summary>
        public static Vector2 ScreenSize { get; private set; }

        /// <summary>
        /// 归一化 insets：x=Left, y=Bottom, z=Right, w=Top（0~1 比例，相对屏幕宽/高）。
        /// 无刘海时为全 0。这是折算到任意画布坐标系的唯一依据。
        /// </summary>
        public static Vector4 NormalizedInsets { get; private set; }

        /// <summary>安全区变化事件（方向/分辨率/开孔变化时触发），供代码驱动的动态布局订阅。</summary>
        public static event Action Changed;

        /// <summary>是否把 <see cref="Screen.cutouts"/>（Android 多开孔/药丸屏）并入有效安全区。默认 true。</summary>
        public static bool IncludeCutouts { get; set; } = true;

        /// <summary>上次 Refresh 时的快照（脏检查用）。</summary>
        static Rect s_CachedSafeArea;
        static Vector2 s_CachedScreenSize;
        static bool s_HasCache;

        /// <summary>cutout 缓存：仅在屏幕尺寸变化时重新读取 Screen.cutouts，避免每帧分配。</summary>
        static Vector2 s_CutoutFetchScreenSize;
        static Rect s_CutoutUnion = new Rect(0, 0, 0, 0);

        /// <summary>强制刷新（重新读取屏幕状态并触发 <see cref="Changed"/>）。</summary>
        public static void Refresh()
        {
            RefreshInternal();
            s_HasCache = true;
        }

        /// <summary>
        /// 脏检查 + 按需刷新：屏幕尺寸/安全区变化时重算，返回是否发生刷新。
        /// 每帧调用成本仅为两次缓存比较。
        /// </summary>
        public static bool RefreshIfDirty()
        {
            if (!s_HasCache)
            {
                Refresh();
                return true;
            }

            if (IsCurrent())
                return false;

            Refresh();
            return true;
        }

        static bool IsCurrent()
        {
            if (s_CachedScreenSize.x != Screen.width || s_CachedScreenSize.y != Screen.height)
                return false;

            return s_CachedSafeArea == GetCurrentSafeArea();
        }

        static void RefreshInternal()
        {
            ScreenSize = new Vector2(Screen.width, Screen.height);
            SafeAreaRect = GetCurrentSafeArea();

            // 极小/最小化窗口下 Screen 尺寸可能为 0，避免 NaN 污染 offset。
            NormalizedInsets = ScreenSize.x <= 0f || ScreenSize.y <= 0f
                ? Vector4.zero
                : new Vector4(
                    SafeAreaRect.xMin / ScreenSize.x,
                    SafeAreaRect.yMin / ScreenSize.y,
                    (ScreenSize.x - SafeAreaRect.xMax) / ScreenSize.x,
                    (ScreenSize.y - SafeAreaRect.yMax) / ScreenSize.y);

            s_CachedScreenSize = ScreenSize;
            s_CachedSafeArea = SafeAreaRect;

            try
            {
                Changed?.Invoke();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[SafeAreaProvider] Changed handler exception: {e.Message}");
            }
        }

        static Rect GetCurrentSafeArea()
        {
            Rect safe = Screen.safeArea;

            if (IncludeCutouts)
                safe = UnionCutouts(safe);

            return safe;
        }

        /// <summary>
        /// 把 Screen.cutouts 并入安全区。cutouts 读取结果按屏幕尺寸缓存，
        /// 避免每帧数组分配；开孔变化总是伴随尺寸/安全区变化。
        /// </summary>
        static Rect UnionCutouts(Rect safe)
        {
            Vector2 size = new Vector2(Screen.width, Screen.height);

            if (s_CutoutFetchScreenSize != size)
            {
                Rect union = new Rect(0, 0, 0, 0);
                Rect[] cutouts = Screen.cutouts;
                if (cutouts != null && cutouts.Length > 0)
                {
                    union = cutouts[0];
                    for (int i = 1; i < cutouts.Length; i++)
                        union = Union(union, cutouts[i]);
                }

                s_CutoutUnion = union;
                s_CutoutFetchScreenSize = size;
            }

            return s_CutoutUnion.width <= 0f || s_CutoutUnion.height <= 0f
                ? safe
                : Union(safe, s_CutoutUnion);
        }

        static Rect Union(Rect a, Rect b)
        {
            float xMin = Mathf.Min(a.xMin, b.xMin);
            float yMin = Mathf.Min(a.yMin, b.yMin);
            float xMax = Mathf.Max(a.xMax, b.xMax);
            float yMax = Mathf.Max(a.yMax, b.yMax);
            return Rect.MinMaxRect(xMin, yMin, xMax, yMax);
        }
    }
}
