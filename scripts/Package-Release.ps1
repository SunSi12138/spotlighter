#Requires -Version 7.2
[CmdletBinding()]
param(
    [string]$FilePath = (Join-Path $PSScriptRoot '..\artifacts\publish\Spotlighter.exe'),
    [string]$OutputPath = (Join-Path $PSScriptRoot '..\artifacts\Spotlighter-win-x64.zip')
)
$ErrorActionPreference = 'Stop'
$source = (Resolve-Path -LiteralPath $FilePath).Path
$output = [IO.Path]::GetFullPath($OutputPath)
if ($source -eq $output) { throw 'Archive destination must differ from the executable.' }
$folder = [IO.Path]::GetDirectoryName($output)
[IO.Directory]::CreateDirectory($folder) | Out-Null
$temporary = $output + '.' + [Guid]::NewGuid().ToString('N') + '.tmp'
try {
    $archive = [IO.Compression.ZipFile]::Open($temporary, [IO.Compression.ZipArchiveMode]::Create)
    try {
        [IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive, $source, 'Spotlighter.exe', [IO.Compression.CompressionLevel]::SmallestSize) | Out-Null
    } finally { $archive.Dispose() }
    # Verify the exact executable bytes, including any Authenticode signature.
    $archive = [IO.Compression.ZipFile]::OpenRead($temporary)
    try {
        if ($archive.Entries.Count -ne 1 -or $archive.Entries[0].FullName -ne 'Spotlighter.exe') { throw 'Unexpected archive contents.' }
        $stream = $archive.Entries[0].Open()
        $sha = [Security.Cryptography.SHA256]::Create()
        try { $packedHash = [Convert]::ToHexString($sha.ComputeHash($stream)) }
        finally { $sha.Dispose(); $stream.Dispose() }
        if ($packedHash -ne (Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash) { throw 'Archive executable hash mismatch.' }
    } finally { $archive.Dispose() }
    Move-Item -LiteralPath $temporary -Destination $output -Force
} finally {
    if (Test-Path -LiteralPath $temporary) { Remove-Item -LiteralPath $temporary }
}
[PSCustomObject]@{
    Path = $output
    ExeBytes = (Get-Item -LiteralPath $source).Length
    ZipBytes = (Get-Item -LiteralPath $output).Length
    SHA256 = (Get-FileHash -LiteralPath $output -Algorithm SHA256).Hash
}
