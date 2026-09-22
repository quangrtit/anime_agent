[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$assetsRoot = Join-Path $repoRoot 'Assets'
$utf8WithoutBom = [System.Text.UTF8Encoding]::new($false)

function Get-DeterministicGuid([string]$relativePath) {
    $normalizedPath = $relativePath.Replace('\', '/').ToLowerInvariant()
    $bytes = [System.Text.Encoding]::UTF8.GetBytes("anime-assistant:$normalizedPath")
    $sha256 = [System.Security.Cryptography.SHA256]::Create()
    try {
        $hash = $sha256.ComputeHash($bytes)
    } finally {
        $sha256.Dispose()
    }
    return ([System.BitConverter]::ToString($hash).Replace('-', '').Substring(0, 32).ToLowerInvariant())
}

function Write-MetaFile([string]$assetPath, [bool]$isDirectory) {
    $metaPath = "$assetPath.meta"
    if (Test-Path -LiteralPath $metaPath) { return }

    $relativePath = $assetPath.Substring($repoRoot.Length + 1)
    $guid = Get-DeterministicGuid $relativePath

    if ($isDirectory) {
        $content = @"
fileFormatVersion: 2
guid: $guid
folderAsset: yes
DefaultImporter:
  externalObjects: {}
  userData:
  assetBundleName:
  assetBundleVariant:
"@
    } elseif ([System.IO.Path]::GetExtension($assetPath) -eq '.cs') {
        $content = @"
fileFormatVersion: 2
guid: $guid
MonoImporter:
  externalObjects: {}
  serializedVersion: 2
  defaultReferences: []
  executionOrder: 0
  icon: {instanceID: 0}
  userData:
  assetBundleName:
  assetBundleVariant:
"@
    } else {
        $content = @"
fileFormatVersion: 2
guid: $guid
DefaultImporter:
  externalObjects: {}
  userData:
  assetBundleName:
  assetBundleVariant:
"@
    }

    [System.IO.File]::WriteAllText($metaPath, $content.TrimStart() + [Environment]::NewLine, $utf8WithoutBom)
}

Get-ChildItem -LiteralPath $assetsRoot -Recurse -Directory |
    Where-Object { $_.FullName -notlike '*\TutorialInfo*' } |
    ForEach-Object { Write-MetaFile $_.FullName $true }

Get-ChildItem -LiteralPath $assetsRoot -Recurse -File |
    Where-Object { $_.Extension -ne '.meta' -and $_.FullName -notlike '*\TutorialInfo\*' -and $_.Name -ne 'Readme.asset' } |
    ForEach-Object { Write-MetaFile $_.FullName $false }

Write-Host 'Missing Unity .meta files generated deterministically.'
