#Requires -Version 7.2
[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$identity = [Security.Principal.WindowsIdentity]::GetCurrent()
$principal = [Security.Principal.WindowsPrincipal]::new($identity)
if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    throw 'Run this installer from an elevated PowerShell window.'
}
$source = Join-Path $PSScriptRoot 'Spotlighter.exe'
if (-not (Test-Path -LiteralPath $source -PathType Leaf)) { throw "Spotlighter.exe is missing next to this installer: $source" }
$signature = Get-AuthenticodeSignature -LiteralPath $source
if ($signature.Status -ne [Management.Automation.SignatureStatus]::Valid -or -not $signature.SignerCertificate) {
    throw "Spotlighter.exe must have a valid signature trusted by this computer. Current status: $($signature.Status)"
}
if (-not ($signature.SignerCertificate.EnhancedKeyUsageList | Where-Object { $_.ObjectId.Value -eq '1.3.6.1.5.5.7.3.3' })) {
    throw 'The signer certificate is not a code-signing certificate.'
}
$destination = [IO.Path]::GetFullPath((Join-Path ${env:ProgramFiles} 'Spotlighter\UIAccess'))
New-Item -ItemType Directory -Path $destination -Force | Out-Null
$installed = Join-Path $destination 'Spotlighter.exe'
Copy-Item -LiteralPath $source -Destination $installed -Force
$installedSignature = Get-AuthenticodeSignature -LiteralPath $installed
if ($installedSignature.Status -ne [Management.Automation.SignatureStatus]::Valid) {
    Remove-Item -LiteralPath $installed -Force
    throw "Signature validation failed after installation: $($installedSignature.Status)"
}
Write-Output "Installed signed UIAccess build to $installed"
Write-Output 'Launch Spotlighter.exe from this protected directory. Do not move it back to Downloads or another user-writable folder.'
