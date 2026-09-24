[CmdletBinding()]
param(
    [string]$UnityEditorPath = $env:ANIME_ASSISTANT_UNITY_EDITOR
)

$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$expectedVersion = '6000.0.75f1'

if ([string]::IsNullOrWhiteSpace($UnityEditorPath)) {
    $candidates = @(
        (Join-Path $repoRoot ".tools\Unity\Hub\Editor\$expectedVersion\Editor\Unity.exe"),
        (Join-Path $repoRoot ".packages\editors\$expectedVersion\Editor\Unity.exe")
    )
    foreach ($candidate in $candidates) {
        if (Test-Path -LiteralPath $candidate) {
            $UnityEditorPath = $candidate
            break
        }
    }
}

if ([string]::IsNullOrWhiteSpace($UnityEditorPath) -or -not (Test-Path -LiteralPath $UnityEditorPath)) {
    throw "Unity $expectedVersion was not found. Pass -UnityEditorPath or set ANIME_ASSISTANT_UNITY_EDITOR."
}

$logDirectory = Join-Path $repoRoot 'Logs'
New-Item -ItemType Directory -Force -Path $logDirectory | Out-Null
$logPath = Join-Path $logDirectory 'windows-build.log'

$unityArguments = @(
    '-batchmode',
    '-nographics',
    '-quit',
    '-projectPath', $repoRoot,
    '-executeMethod', 'AnimeAssistant.Editor.WindowsBuild.Build',
    '-logFile', $logPath
)
$process = Start-Process -FilePath $UnityEditorPath -ArgumentList $unityArguments `
    -Wait -PassThru -WindowStyle Hidden

if ($process.ExitCode -ne 0) {
    throw "Unity build failed with exit code $($process.ExitCode). See $logPath"
}

$playerDirectory = Join-Path $repoRoot 'Builds\Windows'
$runtimeContent = Join-Path $repoRoot 'RuntimeContent'
if (Test-Path -LiteralPath $runtimeContent) {
    Copy-Item -Path (Join-Path $runtimeContent '*') -Destination $playerDirectory -Recurse -Force
}

$thirdPartyDirectory = Join-Path $playerDirectory 'ThirdPartyLicenses'
foreach ($assetId in @('CHAR-003', 'CHAR-004', 'ANIM-001', 'AUDIO-001', 'AUDIO-002', 'AUDIO-003', 'AUDIO-004')) {
    $licenseSource = Join-Path $repoRoot "LICENSES\$assetId"
    if (Test-Path -LiteralPath $licenseSource) {
        $licenseDestination = Join-Path $thirdPartyDirectory $assetId
        New-Item -ItemType Directory -Force -Path $licenseDestination | Out-Null
        Copy-Item -Path (Join-Path $licenseSource '*') -Destination $licenseDestination -Recurse -Force
    }
}

$portableZip = Join-Path $repoRoot 'Builds\AnimeAssistant-Windows-x64-Portable.zip'
Compress-Archive -Path (Join-Path $playerDirectory '*') -DestinationPath $portableZip `
    -CompressionLevel Optimal -Force

Write-Host "Windows build created under $playerDirectory"
Write-Host "Portable archive created at $portableZip"
