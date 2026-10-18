# 开发与验证

[返回项目首页](../README.md) · [源码模块索引](../Spotlighter/README.md)

命令均在仓库根目录执行。

## 当前渲染方案

`CompositionOverlay.cs` 在 CPU 上生成低分辨率的预乘 alpha 遮罩，只在设置或显示配置变化时重新计算。通过 WARP 软件设备上传并保留位图，鼠标移动时仅修改一个 DirectComposition visual 的位置并提交事务；没有每帧桌面捕获、全屏 CPU 像素改写或自定义 GPU 着色器。Windows 的 DWM 仍负责桌面合成，这不意味着整台电脑完全不使用 GPU。

- 缓存位图覆盖两倍虚拟桌面的宽和高，以四分之一的空间分辨率存储，再由系统插值放大。实际 4K 桌面对应 1920×1080 缓存。
- 默认路径分别缓存中性暗幕和满强度的暖/冷滤色位图，由 Windows 自带 `IDCompositionEffectGroup` 的浮点透明度调节滤色强度；暗幕位于滤色层上方，两层跟随同一个根 visual 移动。同侧冷暖变化只更新透明度，不计算或上传位图；跨越原色中点时更新固定滤色，照明范围、亮度、柔和度或环境亮度变化时重建两份缓存。比旧版多一份滤色缓存及其交换链，用来减少拖动时的 CPU 重建；没有新增桌面采集、自定义着色器或运行时依赖。
- 这避免了低强度冷暖分别量化各通道导致的额外色相抖动：例如 RGB(27,30,35) 的中心背景在暖值 0.4 附近，旧的预乘 RGBA 取整会让红蓝差值来回变化。保留原色附近的平滑非对称曲线，先缓存固定颜色再混合强度；使用单个动态颜色矩阵仍可能在系统效果缓存阶段再次量化，因此没有采用该方案。最终显示仍受输出位深、系统色彩管理和显示器影响。[微软效果组说明](https://learn.microsoft.com/en-us/windows/win32/api/dcomp/nn-dcomp-idcompositioneffectgroup)。
- 只在亮部过渡区域对暗幕的灰度 alpha 加入减弱的固定抗色带抖动；环境亮度底色、最暗尾部和滤色 RGB 不加入随机量，避免暗部彩色斑点。抖动逐渐淡入，幅度比最初版本降低一半，不依赖时间、不使用重复贴图，只在缓存生成时计算；静止时像素完全相同。仍受 8 位输出限制，不能承诺所有显示器上完全无色带。
- 小台灯缓存 48 个方向。灯泡和光晕使用与桌面相同的非对称冷暖曲线，灯罩保持原有黄铜颜色；改变冷暖后按需更新当前方向，后续方向懒加载，避免每次拖动滑条重绘全部 48 帧。熄灯时保留无光晕的灯具。
- 方向缓存保持 240×190 设计尺寸，仅在方向或冷暖变化时对当前帧做预乘 alpha 插值，输出到随屏幕高度变化的小位图；4K 直接复制原像素，不增加静止时更新或随分辨率增长的 48 帧缓存。
- 鼠标停止后停止提交图层更新；启用期间以 60 Hz 检查位置，关闭时降到 4 Hz。
- DirectComposition 初始化或运行失败时，自动回退到单个较大的 Win32 保留位图。兼容路径的内存和设置变更成本更高，最多支持 1600 万像素的虚拟桌面；4K 缓存像素约 127 MiB。
- 兼容路径仍用 8 位 Win32 位图直接滤色，也使用固定抗色带抖动，但没有独立的浮点透明度滤色层；极细微冷暖仍可能出现取整台阶。默认路径只使用原有 DirectComposition 接口，失败时自动回退。
- 已取消曾经的四块外围窗口拼接，避免窗口几何更新不同步产生的接缝。外围颜色明确来自黑色像素，不再使用主题相关的 `SS_BLACKRECT`。[微软对静态控件颜色的说明](https://learn.microsoft.com/en-us/windows/win32/controls/static-control-styles)。
- 设置面板只在操作时重绘小窗口，使用 Windows 自带 GDI+，没有引入浏览器、WinUI 或大型 UI 框架。
- 首次操作说明用原生黑色半透明窗口覆盖虚拟桌面，小尺寸说明卡按主屏 DPI 居中并缩放以适应高度；没有整屏 CPU 位图、截图或动画。显示说明时暂停灯光，关闭后释放说明窗口并恢复原来的开关状态。已读标记单独保存到 `%LOCALAPPDATA%/Spotlighter/welcome.version`，恢复灯光默认值不清除它。首次升级到带说明的版本也会显示一次。
- 环境亮度只在面板交互时映射：`ambient = 0.3 × position³`，绘制滑块用立方根逆映射。方向键和滚轮每步移动 1% 行程；数值显示四位小数。持久化仍保存实际亮度，默认 0.01 和 0～0.3 的范围不变，因此旧设置不会被二次映射。此曲线不增加渲染阶段的逐像素计算。

在当前“保留桌面原内容，只调暗和滤色”的需求下，不需要 WGC 或 Desktop Duplication。此前的桌面采集与纸面着色已移除；采集会增加延迟、纹理资源及自捕获排除问题。也没有加入 DXR 或光追。

## 发布体积与便携打包

Native AOT 原本就隐含 Trim；Release 现明确设置 `PublishTrimmed=true`、`TrimMode=full`、`OptimizationPreference=Size`，并保持不变区域性模式。发布版关闭运行时堆栈、调试器/事件/指标等支持，精简系统异常文案，不生成或拷贝调试符号。SDK 对 Native AOT 的这些功能已有静态裁剪支持，详见[微软体积优化说明](https://learn.microsoft.com/en-us/dotnet/core/deploying/native-aot/optimizing)。

2026-10-19 含首次操作说明版本的本地文件大小（后续版本可能变化）：

| 文件 | 大小 |
| --- | --- |
| 优化前 EXE | 1,864,704 字节（1.78 MiB） |
| 当前 EXE | 1,528,320 字节（1.46 MiB，比优化前减少约 18.0%） |
| ZIP 便携包 | 765,971 字节（748 KiB） |

ZIP 只含 `Spotlighter.exe`，不含 PDB、SDK、源码或额外运行时。此前约 9.5 MiB 的 PDB 是调试符号，不是必需运行文件；旧本地符号备份在被 Git 忽略的 `artifacts/symbols/`。打包使用 ZIP 的 SmallestSize 压缩，并验证包内 EXE 的 SHA-256 与输入完全相同。CI 也生成此便携包；这里只比较文件体积，没有追加运行帧率测试。

```powershell
./scripts/Package-Release.ps1
```

输出为 `artifacts/Spotlighter-win-x64.zip`，脚本需要 PowerShell 7.2+；使用者解压运行 EXE 不需要 PowerShell。

体积优化的代价是发布版异常信息更简短，不含运行时堆栈。需要诊断时可另行发布完整符号版本：

```powershell
dotnet publish ./Spotlighter/Spotlighter.csproj -c Release -o ./artifacts/diagnostic -p:StackTraceSupport=true -p:UseSystemResourceKeys=false -p:DebuggerSupport=true -p:DebugType=portable -p:DebugSymbols=true -p:CopyOutputSymbolsToPublishDirectory=true
```

## 构建与检查

需要 Windows、.NET SDK 10、MSVC Build Tools 和 Windows SDK。推荐使用与 CI 相同的一键流程（PowerShell 7.2+）：

```powershell
./scripts/Build-Release.ps1
```

它发布 EXE，执行有 60 秒超时保护的自检，然后打包并校验 ZIP；不会执行性能测试或启用桌面覆盖层。也可分别执行：

```powershell
dotnet publish ./Spotlighter/Spotlighter.csproj -c Release -r win-x64 --self-contained true -p:PublishAot=true -o ./artifacts/publish
Start-Process ./artifacts/publish/Spotlighter.exe -ArgumentList '--self-test' -Wait -PassThru
```

`--self-test` 不打开桌面覆盖层，检查衰减、默认值、参数边界、冷暖滤光的预乘 alpha、原色附近的非线性连续性、柔边与环境反射冷暖方向、单击/双击判定、台灯发光冷暖和台灯图标；另检查环境亮度三次方曲线、固定滤色配合浮点透明度的连续扫值、环境区像素均匀性、灰度抖动边界与确定性，以及各分辨率下的灯具缩放。当前 Native AOT 发布和自检通过。

其他可选入口：

```powershell
# 启用灯光并显示调光面板
./artifacts/publish/Spotlighter.exe --settings
# 只启用灯光
./artifacts/publish/Spotlighter.exe --active
# 不显示窗口，输出同一绘制代码生成的面板 PNG
./artifacts/publish/Spotlighter.exe --ui-preview ./artifacts/settings-preview.png
# 手动显示完整操作说明（先退出已运行实例）
./artifacts/publish/Spotlighter.exe --guide
# 只输出说明卡 PNG，不显示遮罩、不写已读标记
./artifacts/publish/Spotlighter.exe --guide-preview ./artifacts/welcome-preview.png
# 重新生成源码中的多尺寸台灯图标
./artifacts/publish/Spotlighter.exe --export-icon ./Spotlighter/Assets/Spotlighter.ico
# 仅手动需要时运行；会显示覆盖层，运行前退出托盘实例
./artifacts/publish/Spotlighter.exe --integration-test ./artifacts/integration.txt
./artifacts/publish/Spotlighter.exe --profile cpu ./artifacts/profile.txt 3840 2160
```

集成入口在 Present 前读取本程序提交的暗幕位图，检查 alpha、移动稳定性，以及连续调整冷暖时没有暗幕重建或上传。旧版在 Present 后读取已经轮转的下一帧缓冲区，可能把旧帧误判为当前帧，此处已修正。读回只在这个手动入口中启用，正常运行不会同步读回。

集成验证还会短暂创建自己的 RGB(27,30,35) 色块窗口，在 DWM 提交完成后只采样该色块覆盖的一个屏幕点，比较冷暖扫值的实际合成结果与浮点参考。已知色块不可见或读像素失败时标为 UNVERIFIED；本程序不采集其他应用的画面。它仍不能证明物理显示无闪烁、HDR 校准准确或输入延迟。无交互的 PNG 预览已检查，但完整桌面上的灯具双击、面板拖动、跨应用点击、输入、滚动和拖动仍须手工复核。旧版输入曾经手工确认正常；这不替代当前版本的复核。

操作说明的已读状态读写、版本处理、与恢复默认相互独立，以及 1080p/4K/带鱼屏/高 DPI 下的布局边界由自检覆盖；说明卡 PNG 已检查。按钮、Esc、首次显示、从托盘重开和显示器变化时重建的实际桌面交互仍需手工复核。

## 限制与待验证

- 保留通用便携模式，不处理开始菜单、通知中心等更高层级系统浮层，也不支持 UAC 安全桌面。
- 内容不被截图和重绘，系统光标仍由 Windows 绘制。台灯自身可见实体是刻意保留的点击区域，其余覆盖层保持穿透。
- HDR 色彩准确性、跨适配器多屏、负坐标、混合 DPI、显示热插拔、睡眠恢复和远程驱动差异未完成对应物理环境验证。显示变化时重建遮罩并关闭可能越界的面板。
- 浮动面板按打开位置的显示器 DPI 缩放并限制在工作区内；跨 DPI 拖动后可关闭并重新打开重新适配。
- 多屏时只在主显示器放置一盏灯，避免落入虚拟桌面的显示器间隙；分辨率缩放、带鱼屏宽度无关和负坐标布局通过合成数据自检，尚未实际切换物理显示模式或验证混合 DPI。
- 旧分块暗幕的灰边与接缝来源已移除；新版本是否在本机完全消除动态闪条仍需实际观察，不能仅凭缓存像素检查宣称通过。
