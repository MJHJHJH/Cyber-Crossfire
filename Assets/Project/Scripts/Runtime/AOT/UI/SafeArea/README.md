# UI SafeArea 适配（SafeAreaFitter + SafeAreaProvider）

框架级安全区适配方案，弥补框架缺失的刘海屏/圆角屏支持。
组件全部位于 **GamePlay.AOT** 程序集（`Assets/Project/Scripts/Runtime/AOT/UI/SafeArea`），
HotUpdate 程序集与 prefab 均可直接使用（符合 HybridCLR 最佳实践：prefab 上的 MonoBehaviour 放 AOT）。

## 文件说明

| 文件 | 作用 |
| --- | --- |
| `SafeAreaProvider.cs` | 全局静态服务：集中读取 `Screen.safeArea` ∪ `Screen.cutouts`（可选），缓存 + 脏检查，输出归一化 insets；`Changed` 事件供代码动态布局订阅 |
| `SafeAreaFitter.cs` | 核心组件：挂在任意 RectTransform，按 insets 折算到画布坐标系并叠加到 offsetMin/offsetMax；锚点保持、逐边控制、Expand 背景扩展、Padding 边距；Scene Gizmo 预览 |
| `SafeAreaDebug.cs` | 运行时调试工具：屏幕左上角实时显示安全区数据，红色遮罩 = 非安全区（刘海/挖孔/手势条），绿色描边 = 安全区；真机/模拟器验证用 |
| `Editor/SafeAreaEditor.cs` | 编辑器工具：Scene 视图安全区参考线（运行态显示真实安全区）、Hierarchy 右键 `UI/Safe Area Fitter` 快捷添加组件 |

## 快速使用

1. 打开面板 prefab（如 `MainHUD.prefab`），在需要避让刘海的节点上添加 `Safe Area Fitter`：
   - **内容节点**：挂组件并缩进（推荐；有全屏背景的面板挂内容容器，背景图锚定面板根保持铺满）；
   - **面板根节点**：纯内容面板可直接挂根节点；
   - **全屏背景图**：挂 `SafeAreaFitter` 并勾选 **Expand**，反向扩展到刘海区；
   - **全屏遮罩**（如 FadeScreen）：不挂。
2. Inspector 配置：
   - `Edges`：参与缩进的边。**内容贴哪条屏幕边就勾哪条**（顶栏勾 Top、底部按钮勾 Bottom、角落元素勾相邻两条、全屏内容勾 All）；
   - `Expand`：背景铺满模式；
   - `Padding`：安全区基础上每边额外留白（canvas 单位，x=Left y=Bottom z=Right w=Top）。
3. 编辑器预览/测试：使用 **Unity Device Simulator**（已安装 `com.unity.device-simulator.devices`）：
   - Window → General → Device Simulator → 选 iPhone 14 Pro / Pixel 7 → 切横屏 → Play；
   - Device Simulator 真实模拟 `Screen.safeArea`/`Screen.cutouts`，组件自动适配，无需任何配置；
   - 运行时可开 `Tools/Safe Area/Scene Overlay` 在 Scene 视图查看真实安全区参考线。

## 设计要点

- **锚点保持**：不修改 anchorMin/Max，只在基线 offset 上叠加增量，与美术锚点布局完全兼容；
  PC / 无刘海设备自动零偏移。
- **坐标系折算**：`Screen.safeArea` 是物理像素，组件按“归一化比例 × 画布 rect 尺寸”折算，
  对 `ScaleWithScreenSize` / `ConstantPixelSize` / `ScreenSpaceCamera` 全部成立（线性映射）。
- **动态响应**：方向/分辨率/开孔变化由 Update 脏检查（两次缓存比较）驱动；
  `OnRectTransformDimensionsChange` 覆盖画布与布局驱动的尺寸变化。
- **对象池安全**：基线 offset 只采集一次，面板复用（UIForm 对象池）重复应用同一基线。
- **编辑器安全**：编辑态不写 rect（避免污染 prefab 序列化），仅运行态绘制 Gizmo。

## 代码 API（动态布局）

```csharp
// 订阅安全区变化（横竖屏切换时动态重排 HUD 元素）
SafeAreaProvider.Changed += OnSafeAreaChanged;
void OnSafeAreaChanged()
{
    Vector4 insets = SafeAreaProvider.NormalizedInsets; // x=Left y=Bottom z=Right w=Top（0~1）
    // 折算到目标画布：insets.x * canvasRect.rect.width 等
}

// 手动触发一次适配（代码改锚点后）
fitter.ApplyNow();
```

## 注意事项

- 挂 `SafeAreaFitter` 的节点不要再挂会驱动 offset 的 LayoutGroup（会互相覆盖）；
  确有需要时用 `ApplyNow()` 在布局完成后手动调用。
- `SafeAreaFitter` 与 `SafeAreaProvider` 均标注 `[Preserve]`，防止 IL2CPP 裁剪
  （组件仅被 prefab 引用、无代码引用时裁剪风险）。
- 新增了 AOT 程序集脚本：主包需重新构建（AOT 程序集变更），
  HotUpdate 热更包不受影响。
- 组件会查找最近的上级 Canvas 作为折算基准，请确保挂在 Canvas 层级之下。
- 发布前移除场景中的 `SafeAreaDebug` 组件（或关闭其显示开关）。
