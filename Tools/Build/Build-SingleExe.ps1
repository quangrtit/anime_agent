[CmdletBinding()]
param(
    [switch]$SkipUnityBuild,
    [string]$UnityEditorPath = $env:ANIME_ASSISTANT_UNITY_EDITOR
)

$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$windowsBuild = Join-Path $repoRoot 'Builds\Windows'

if (-not $SkipUnityBuild) {
    & (Join-Path $PSScriptRoot 'Build-Windows.ps1') -UnityEditorPath $UnityEditorPath
}

$playerExe = Join-Path $windowsBuild 'AnimeAssistant.exe'
if (-not (Test-Path -LiteralPath $playerExe)) {
    throw "Windows player is missing: $playerExe"
}

$packagingDirectory = Join-Path $repoRoot 'Builds\Packaging'
$payloadPath = Join-Path $packagingDirectory 'AnimeAssistantRuntime.zip'
$publishDirectory = Join-Path $packagingDirectory 'LauncherPublish'
$finalDirectory = Join-Path $repoRoot 'Builds\SingleExe'
New-Item -ItemType Directory -Force -Path $packagingDirectory, $publishDirectory, $finalDirectory | Out-Null

Compress-Archive -Path (Join-Path $windowsBuild '*') -DestinationPath $payloadPath `
    -CompressionLevel Optimal -Force

$project = Join-Path $repoRoot 'Tools\PortableLauncher\AnimeAssistant.PortableLauncher.csproj'
dotnet publish $project -c Release -r win-x64 --self-contained true -o $publishDirectory
if ($LASTEXITCODE -ne 0) {
    throw "Portable launcher publish failed with exit code $LASTEXITCODE."
}

$publishedExe = Join-Path $publishDirectory 'AnimeAssistant-Portable.exe'
$finalExe = Join-Path $finalDirectory 'AnimeAssistant-Portable.exe'
Copy-Item -LiteralPath $publishedExe -Destination $finalExe -Force
Copy-Item -LiteralPath (Join-Path $repoRoot 'RuntimeContent\Config\desktop_layout.json') `
    -Destination (Join-Path $finalDirectory 'desktop_layout.json') -Force

Write-Host "Single-file launcher: $finalExe"
Write-Host "Editable config: $(Join-Path $finalDirectory 'desktop_layout.json')"
