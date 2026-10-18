# 源码模块索引

[使用说明](../README.md) · [构建、渲染与验证](../docs/development.md)

这是一个单项目的 Windows x64 Native AOT 程序，源码保持在同一个命名空间中，无第三方运行时依赖。

| 模块 | 职责 |
| --- | --- |
| Program.cs / FrameClock.cs | Win32 消息循环、托盘、开关、鼠标跟随与调度 |
| CompositionOverlay.cs | CPU 遮罩缓存、WARP 上传、DirectComposition 图层移动 |
| DesktopSurface.cs / IDesktopOverlay.cs | 保留位图兼容回退与统一覆盖层接口 |
| LightMask.cs / LightSettings.cs | 光照衰减、冷暖滤色、参数边界与设置保存 |
| LampOverlay.cs / LampClickGesture.cs | 小台灯窗口、命中测试、单击与双击判定 |
| LampPainter.cs / LampLayout.cs / LampIcon.cs | 灯具绘制、按屏幕高度缩放、桌面与托盘图标 |
| SettingsWindow.cs / UiCanvas.cs | 半透明调光面板与 GDI+ 绘制 |
| Native.cs / OverlayBitmap.cs | Win32 互操作、保留位图及窗口生命周期 |
| SelfTest.cs / IntegrationTest.cs | 无窗口自检与可选集成验证 |
| FrameMetrics.cs / CompositionReadback.cs | 手动诊断和可选测量，正常渲染不执行同步读回 |
| Assets/Spotlighter.ico | 程序的多尺寸台灯图标 |
| Spotlighter.csproj / app.manifest | Native AOT、裁剪配置、DPI 感知与普通用户权限 |

项目不捕获应用画面，不使用自定义 GPU 着色器、光线追踪或全局键盘钩子。历史性能及其局限见[已有性能记录](../docs/performance.md)，发布签名见[签名与分发](../docs/signing.md)。
