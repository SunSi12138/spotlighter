# Spotlighter

一款轻量、便携的 Windows 夜间阅读台灯覆盖层，光照跟随鼠标，周围桌面柔和变暗。

Lightweight Windows desk-lamp overlay for night reading, with adjustable light and a click-through dimming layer.

适用于 Windows 10/11 x64。原生 Win32、.NET 10 Native AOT，便携单文件，无依赖。

## 使用

解压便携包，运行 `Spotlighter.exe`。程序启动后在主显示器顶部显示小台灯，并在系统托盘显示台灯图标。

第一次启动会显示全屏操作说明：介绍台灯开关、调光和托盘退出。点击“知道了，开始使用”或按 Esc/Enter 收起，之后不再自动弹出；可从托盘右键菜单的“操作说明…”重新查看。查看说明时灯光暂停，关闭后恢复。

| 操作 | 功能 |
| --- | --- |
| 单击桌面小台灯 | 开灯或关灯 |
| 双击桌面小台灯 | 打开或收起调光面板 |
| 双击托盘图标 | 开灯或关灯 |
| 右键托盘图标 | 开关、调光、退出 |

调光面板可调整光源范围、照明亮度、边缘柔和度、环境亮度、光色冷暖；即时预览，自动保存，支持恢复默认。鼠标停在面板或灯具上时，光照位置固定，便于比较调整效果。

- 冷暖范围为 **-1 到 +1**，负值偏冷，正值偏暖，0 保留原色。灯泡和光晕也随之变化。
- 默认合成路径分开缓存暗幕和固定滤色，用浮点透明度调整冷暖，避免拖动时反复量化各个颜色通道。同侧微调冷暖不重建遮罩；切换冷、暖方向时才更新滤色缓存。
- 光照渐变加入低幅度、位置固定的灰度抗色带处理，环境亮度区域保持均匀；没有彩色随机噪点，也不会逐帧重算像素。
- 默认环境亮度为 **0.01**；调为 0 时，光照范围外为黑色。
- 环境亮度滑条采用三次方映射，低亮度段占据更多行程，拖动、方向键和滚轮都能细调；已有设置的实际亮度保持不变。
- 台灯按显示器高度保持相同比例：4K 为设计尺寸，1080p 减半，带鱼屏不会因为更宽而放大。
- 面板支持拖动；Tab/上下键选择滑条，左右键或滚轮微调，Home/End 调到端点，Esc/Enter 收起。
- 设置保存在 `%LOCALAPPDATA%/Spotlighter/settings.ini`。没有全局 Ctrl 快捷键。

覆盖层设计为点击穿透，只有灯具实体和调光面板接受点击。开始菜单、通知中心等系统浮层和 UAC 安全桌面不在覆盖范围内。当前便携包尚未正式签名，Windows 可能提示未知发布者。

## 构建

需要 Windows、.NET SDK 10、Visual Studio Build Tools 的 C++ 构建工具及 Windows SDK；构建脚本需要 PowerShell 7.2+。

在仓库根目录运行：

```powershell
./scripts/Build-Release.ps1
```

脚本完成 Native AOT 发布、自检及便携打包，输出：

- `artifacts/publish/Spotlighter.exe`
- `artifacts/Spotlighter-win-x64.zip`

构建时请先退出正在使用该输出目录的程序；也可用 `-OutputDirectory ./artifacts/staging` 发布到其他目录。GitHub Actions 执行相同流程，成功后可下载构建附件。

## 项目结构

```text
Spotlighter/             原生桌面程序、图标与内置自检
docs/                    开发、签名和历史性能记录
scripts/                 构建、自检、打包与正式签名
.github/workflows/       Windows Native AOT 自动构建
artifacts/               本地输出与报告（不提交）
```

- [源码模块索引](Spotlighter/README.md)
- [开发与验证](docs/development.md)
- [签名与分发](docs/signing.md)
- [覆盖系统界面的要求](docs/system-overlay.md)
- [已有性能记录及测量边界](docs/performance.md)

程序使用 CPU 生成并缓存光照遮罩，鼠标移动时只移动系统合成图层；不截取桌面，不逐帧重写全屏像素。显示合成仍由 Windows DWM 完成。多屏、HDR、混合 DPI 和物理交互的验证范围见开发文档。

## License

Spotlighter is distributed under the [MIT License](LICENSE).
