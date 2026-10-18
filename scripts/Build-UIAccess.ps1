#Requires -Version 7.2
[CmdletBinding()]
param(
    [ValidatePattern('^[0-9A-Fa-f]{40}$')][string]$CertificateThumbprint,
    [string]$OutputDirectory = (Join-Path $PSScriptRoot '..\artifacts\uiaccess\publish'),
    [string]$PackagePath
)
$ErrorActionPreference = 'Stop'
if (-not $IsWindows) { throw 'UIAccess publishing requires Windows, MSVC Build Tools and Windows SDK.' }
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$project = Join-Path $root 'Spotlighter\Spotlighter.csproj'
$manifest = Join-Path $root 'Spotlighter\app.uiaccess.manifest'
$output = [IO.Path]::GetFullPath($OutputDirectory)
$exe = Join-Path $output 'Spotlighter.exe'
if (-not $PackagePath) {
    $suffix = if ($CertificateThumbprint) { 'win-x64' } else { 'unsigned-candidate' }
    $PackagePath = Join-Path $root "artifacts\Spotlighter-UIAccess-$suffix.zip"
}
$package = [IO.Path]::GetFullPath($PackagePath)
if (-not $output.StartsWith((Join-Path $root 'artifacts')+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)) {
    throw 'OutputDirectory must be inside the repository artifacts folder.'
}
if ($package -eq $exe -or $package.StartsWith($output+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)) {
    throw 'PackagePath must be outside OutputDirectory.'
}
New-Item -ItemType Directory -Path $output -Force | Out-Null
& dotnet publish $project -c Release -r win-x64 --self-contained true -p:PublishAot=true `
    "-p:ApplicationManifest=$manifest" '-p:Version=1.0.0-uiaccess.1' -o $output
if ($LASTEXITCODE -ne 0) { throw "UIAccess Native AOT publish failed: $LASTEXITCODE" }
if (-not (Test-Path -LiteralPath $exe)) { throw 'UIAccess executable is missing.' }

$sdkBin = Join-Path ${env:ProgramFiles(x86)} 'Windows Kits\10\bin'
$mt = Get-ChildItem -LiteralPath $sdkBin -Directory | Sort-Object Name -Descending |
    ForEach-Object { Join-Path $_.FullName 'x64\mt.exe' } | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
if (-not $mt) { throw 'Windows SDK Manifest Tool (mt.exe) was not found.' }
$embedded = Join-Path $output 'embedded-uiaccess.manifest'
& $mt "-inputresource:$exe;#1" "-out:$embedded"
if ($LASTEXITCODE -ne 0 -or -not (Test-Path -LiteralPath $embedded)) { throw 'Could not inspect the executable manifest.' }
if (-not (Select-String -LiteralPath $embedded -Pattern 'uiAccess="true"' -Quiet)) { throw 'The published executable does not contain uiAccess=true.' }
Remove-Item -LiteralPath $embedded -Force

$signed = [bool]$CertificateThumbprint
if ($signed) {
    & (Join-Path $PSScriptRoot 'Sign-Release.ps1') -CertificateThumbprint $CertificateThumbprint -FilePath $exe
    if ($LASTEXITCODE -ne 0) { throw 'Signing the UIAccess executable failed.' }
    $test = Start-Process -FilePath $exe -ArgumentList '--self-test' -WindowStyle Hidden -PassThru
    if (-not $test.WaitForExit(60000)) { $test.Kill(); throw 'Signed UIAccess self-test timed out.' }
    if ($test.ExitCode -ne 0) { throw "Signed UIAccess self-test failed: $($test.ExitCode)" }
}

$packageDirectory = [IO.Path]::GetDirectoryName($package)
[IO.Directory]::CreateDirectory($packageDirectory) | Out-Null
$temporary = $package+'.'+[Guid]::NewGuid().ToString('N')+'.tmp'
$archive = [IO.Compression.ZipFile]::Open($temporary,[IO.Compression.ZipArchiveMode]::Create)
try {
    [IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive,$exe,'Spotlighter.exe',[IO.Compression.CompressionLevel]::SmallestSize) | Out-Null
    if ($signed) {
        [IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive,(Join-Path $PSScriptRoot 'Install-UIAccess.ps1'),'Install-UIAccess.ps1',[IO.Compression.CompressionLevel]::SmallestSize) | Out-Null
    }
    else {
        $note = @'
UNSIGNED UIACCESS BUILD CANDIDATE — NOT READY TO RUN

This executable requests UIAccess but has no trusted signature. Windows will not grant UIAccess to it. Do not run this candidate as a system-overlay build.

To create an installable package, rebuild with scripts/Build-UIAccess.ps1 -CertificateThumbprint <trusted-code-signing-certificate-thumbprint>. The signed executable must then be installed under Program Files using Install-UIAccess.ps1 from an elevated PowerShell window.
'@
        $entry = $archive.CreateEntry('SIGNING-REQUIRED.txt',[IO.Compression.CompressionLevel]::SmallestSize)
        $writer = [IO.StreamWriter]::new($entry.Open(),[Text.UTF8Encoding]::new($false))
        try { $writer.Write($note) } finally { $writer.Dispose() }
    }
}
finally { $archive.Dispose() }
Move-Item -LiteralPath $temporary -Destination $package -Force
Get-FileHash -LiteralPath $exe -Algorithm SHA256
[PSCustomObject]@{Path=$package;Signed=$signed;ExeBytes=(Get-Item -LiteralPath $exe).Length;ZipBytes=(Get-Item -LiteralPath $package).Length}
