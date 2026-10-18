#Requires -Version 7.2
[CmdletBinding()]
param(
    [string]$OutputDirectory = (Join-Path $PSScriptRoot '..\artifacts\publish'),
    [string]$PackagePath = (Join-Path $PSScriptRoot '..\artifacts\Spotlighter-win-x64.zip')
)
$ErrorActionPreference = 'Stop'
if (-not $IsWindows) { throw 'Native AOT publishing requires Windows, MSVC Build Tools and Windows SDK.' }
$project = Join-Path $PSScriptRoot '..\Spotlighter\Spotlighter.csproj'
$output = [IO.Path]::GetFullPath($OutputDirectory)
& dotnet publish $project -c Release -r win-x64 --self-contained true -p:PublishAot=true -o $output
if ($LASTEXITCODE -ne 0) { throw "Native AOT publish failed: $LASTEXITCODE" }
$executable = Join-Path $output 'Spotlighter.exe'
if (-not (Test-Path -LiteralPath $executable)) { throw 'Published executable is missing.' }
$test = Start-Process -FilePath $executable -ArgumentList '--self-test' -WindowStyle Hidden -PassThru
if (-not $test.WaitForExit(60000)) {
    $test.Kill()
    throw 'Native AOT self-test timed out after 60 seconds.'
}
if ($test.ExitCode -ne 0) { throw "Native AOT self-test failed: $($test.ExitCode)" }
Write-Output 'Native AOT self-test passed.'
& (Join-Path $PSScriptRoot 'Package-Release.ps1') -FilePath $executable -OutputPath $PackagePath
