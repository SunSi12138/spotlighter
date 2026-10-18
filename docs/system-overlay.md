# 覆盖 Windows 系统界面需要什么

[返回项目首页](../README.md) · [正式签名流程](signing.md)

当前版本仍为普通用户权限的便携版：`asInvoker`、`uiAccess=false`。首次操作说明是程序自己的普通桌面遮罩，并不意味着已经具备系统浮层覆盖权限。

如果要尝试覆盖开始菜单、通知中心等更高层级界面，常规路线是另外提供经过正式签名的 UIAccess 安装版：

1. 将该安装版的清单改为 `uiAccess=true`；普通便携版保留现有清单。
2. 用目标电脑信任的 Authenticode 代码签名证书签署最终 EXE，签名后打包，不能再修改文件。
3. 通过安装器以管理员权限安装到受保护目录，例如 `Program Files/Spotlighter`，并保证普通用户不能改写安装文件。
4. 从该安装位置启动，检查系统实际授予的 UIAccess 状态，再逐项测试 Windows 10/11 的开始菜单、任务栏浮层、通知中心、音量浮层和点击穿透；不能只靠清单推断通过。

仓库提供独立清单 `Spotlighter/app.uiaccess.manifest` 和 `scripts/Build-UIAccess.ps1`。未提供可信代码签名证书指纹时，脚本只生成明确标注的 `Spotlighter-UIAccess-unsigned-candidate.zip`，它用于签名前准备，Windows 不会授予 UIAccess。拿到证书后，可用 `-CertificateThumbprint` 构建、签署、验证，再生成含管理员安装脚本的独立 ZIP：

```powershell
./scripts/Build-UIAccess.ps1 -CertificateThumbprint '40位证书指纹'
```

已签名包解压后，在管理员 PowerShell 中运行 `Install-UIAccess.ps1`；它会拒绝无效或不受本机信任的签名，并把程序安装到 `%ProgramFiles%\Spotlighter\UIAccess`。灯光普通版和 UIAccess 版使用同一个单实例锁，测试前先退出另一版。

正式受信任签名可以面向其他电脑分发，不要求每台电脑手工导入本地自签名证书。自签名本身不能提供这种通用信任。签名和 UIAccess 也不能保证永远盖过其他 UIAccess 程序的最上层窗口。

UIAccess 不等于普通管理员权限，也不意味着可以把桌面窗口画到 UAC 安全桌面、锁屏或登录界面。这里不以关闭安全桌面、放宽 UIAccess 安装目录策略等方式绕过系统保护。正式签名也不能承诺立即消除 SmartScreen 信誉提示。

这条路线会增加证书或签名服务、安装器、安装权限和多系统验证的工作。当前没有可用的通用代码签名证书；没有执行安装、证书信任变更或系统策略变更，也没有声称任何系统浮层已通过覆盖验证。微软明确不建议只为了显示在新版 Windows 界面上方而使用 UIAccess；这一实验版不保证所有系统弹窗都能被覆盖。

依据：[微软 UIAccess 安全要求](https://learn.microsoft.com/en-us/windows/win32/winauto/uiauto-securityoverview)、[应用清单](https://learn.microsoft.com/en-us/windows/win32/sbscs/application-manifests)、[安全安装目录策略](https://learn.microsoft.com/en-us/windows/security/application-security/application-control/user-account-control/settings-and-configuration)。
