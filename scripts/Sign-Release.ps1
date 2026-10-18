[CmdletBinding()]
param(
    [Parameter(Mandatory)][ValidatePattern('^[0-9A-Fa-f]{40}$')][string]$CertificateThumbprint,
    [string]$FilePath = (Join-Path $PSScriptRoot '..\artifacts\publish\Spotlighter.exe'),
    [string]$SignToolPath,
    [switch]$MachineStore,
    [string]$TimestampServer = 'https://timestamp.digicert.com'
)
$ErrorActionPreference = 'Stop'
$target = (Resolve-Path -LiteralPath $FilePath).Path
$store = if ($MachineStore) { 'LocalMachine' } else { 'CurrentUser' }
$certificate = Get-Item -LiteralPath "Cert:\$store\My\$CertificateThumbprint"
if (-not $certificate.HasPrivateKey) { throw 'The signing certificate has no available private key.' }
if ($certificate.NotAfter -le (Get-Date)) { throw 'The signing certificate has expired.' }
if ($certificate.Subject -eq $certificate.Issuer) { throw 'Use a publicly trusted code-signing certificate. Self-signing does not establish trust on other computers.' }
if (-not ($certificate.EnhancedKeyUsageList | Where-Object { $_.ObjectId.Value -eq '1.3.6.1.5.5.7.3.3' })) { throw 'The certificate is not valid for code signing.' }
if (-not $SignToolPath) {
    $found = Get-Command signtool.exe -ErrorAction SilentlyContinue
    if ($found) { $SignToolPath = $found.Source }
    else {
        $sdkBin = Join-Path ${env:ProgramFiles(x86)} 'Windows Kits\10\bin'
        $candidate = Get-ChildItem -LiteralPath $sdkBin -Directory | Sort-Object Name -Descending | ForEach-Object { Join-Path $_.FullName 'x64\signtool.exe' } | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
        if (-not $candidate) { throw 'Windows SDK SignTool was not found. Pass -SignToolPath.' }
        $SignToolPath = $candidate
    }
}
$arguments = @('sign','/sha1',$CertificateThumbprint,'/s','My','/fd','SHA256','/tr',$TimestampServer,'/td','SHA256')
if ($MachineStore) { $arguments += '/sm' }
$arguments += $target
& $SignToolPath @arguments
if ($LASTEXITCODE -ne 0) { throw "Signing failed: $LASTEXITCODE" }
& $SignToolPath verify /pa /all /v $target
if ($LASTEXITCODE -ne 0) { throw "Signature verification failed: $LASTEXITCODE" }
$signature = Get-AuthenticodeSignature -LiteralPath $target
if ($signature.Status -ne 'Valid' -or -not $signature.TimeStamperCertificate) { throw "Signature or timestamp is invalid: $($signature.Status)" }
Get-FileHash -LiteralPath $target -Algorithm SHA256
Write-Output 'Signed and timestamped. Publish this exact executable without rebuilding it. SmartScreen reputation is a separate Windows check.'
