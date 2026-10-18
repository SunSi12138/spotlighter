# 签名与分发

[返回项目首页](../README.md) · [开发与验证](development.md)

以下命令在仓库根目录执行。

## 正式签名流程

当前构建为 **未签名**。仓库没有签名私钥，未创建自签名证书，也未更改任何电脑的证书信任或安全设置。

1. 获取受 Windows 信任的代码签名证书，并通过颁发机构/硬件令牌工具使私钥可由 Windows 证书存储使用。微软 Artifact Signing 是另一条正式签名路线，需要单独配置账号、身份验证和官方签名客户端。
2. 先完成发布和验证，然后在有证书的电脑上运行：

```powershell
Get-ChildItem Cert:/CurrentUser/My -CodeSigningCert | Select-Object Subject, Thumbprint, NotAfter
./scripts/Sign-Release.ps1 -CertificateThumbprint '这里填写40位证书指纹'
```

脚本使用 Windows SDK SignTool：SHA-256 文件摘要、RFC 3161 时间戳，再以 Authenticode 策略验证所有签名和时间戳，输出最终 SHA-256。机器证书存储可加 `-MachineStore`，也支持显式 `-FilePath`、`-SignToolPath`。脚本不导出私钥，不接收或打印密码。

3. 分发刚签名的同一个 EXE；签名后不要再次构建或改写文件。签名后运行 `./scripts/Package-Release.ps1` 打包，脚本会校验保留签名后的 EXE 原始字节。
4. 正式签名可以显示可信发布者，但 SmartScreen 还检查信誉；新程序即使签名有效也可能出现提示。自签名不能自动在其他电脑受信任。Microsoft Store 分发由商店处理签名和信誉要求。详见[微软签名选项](https://learn.microsoft.com/en-us/windows/apps/package-and-deploy/code-signing-options)、[SmartScreen 信誉](https://learn.microsoft.com/en-us/windows/apps/package-and-deploy/smartscreen-reputation)和 [SignTool](https://learn.microsoft.com/en-us/windows/win32/seccrypto/signtool)。

签名脚本已通过语法检查；尚无正式证书，因此未执行实际签名与时间戳服务调用。CI 默认发布未签名构建。
