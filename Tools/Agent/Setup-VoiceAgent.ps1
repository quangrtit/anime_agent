[CmdletBinding()]
param(
    [switch]$SkipDownload,
    [switch]$SkipPublish
)

$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$modelName = 'sherpa-onnx-zipformer-vi-int8-2025-04-20'
$downloadUrl = "https://github.com/k2-fsa/sherpa-onnx/releases/download/asr-models/$modelName.tar.bz2"
$downloadDirectory = Join-Path $repoRoot '.downloads\voice-agent'
$archivePath = Join-Path $downloadDirectory "$modelName.tar.bz2"
$agentDirectory = Join-Path $repoRoot 'RuntimeContent\Agent'
$modelRoot = Join-Path $agentDirectory 'Models'
$modelDirectory = Join-Path $modelRoot $modelName
$requiredFiles = @(
    'tokens.txt',
    'encoder-epoch-12-avg-8.int8.onnx',
    'decoder-epoch-12-avg-8.onnx',
    'joiner-epoch-12-avg-8.int8.onnx'
)
$ttsModelName = 'vits-piper-vi_VN-vais1000-medium'
$ttsDownloadUrl = "https://github.com/k2-fsa/sherpa-onnx/releases/download/tts-models/$ttsModelName.tar.bz2"
$ttsArchivePath = Join-Path $downloadDirectory "$ttsModelName.tar.bz2"
$ttsModelDirectory = Join-Path $modelRoot $ttsModelName
$ttsRequiredPaths = @('vi_VN-vais1000-medium.onnx', 'tokens.txt', 'espeak-ng-data')

$modelReady = $true
foreach ($file in $requiredFiles) {
    if (-not (Test-Path -LiteralPath (Join-Path $modelDirectory $file))) {
        $modelReady = $false
        break
    }
}

$ttsReady = $true
foreach ($path in $ttsRequiredPaths) {
    if (-not (Test-Path -LiteralPath (Join-Path $ttsModelDirectory $path))) {
        $ttsReady = $false
        break
    }
}

if (-not $ttsReady) {
    if ($SkipDownload) {
        throw "Vietnamese female TTS model is missing at $ttsModelDirectory"
    }
    New-Item -ItemType Directory -Force -Path $downloadDirectory, $modelRoot | Out-Null
    if (-not (Test-Path -LiteralPath $ttsArchivePath)) {
        Write-Host 'Downloading the local Vietnamese female TTS model...'
        $partialTtsArchive = "$ttsArchivePath.partial"
        Invoke-WebRequest -Uri $ttsDownloadUrl -OutFile $partialTtsArchive
        Move-Item -LiteralPath $partialTtsArchive -Destination $ttsArchivePath -Force
    }
    Write-Host 'Extracting the Vietnamese female TTS model...'
    & tar.exe -xf $ttsArchivePath -C $modelRoot
    if ($LASTEXITCODE -ne 0) {
        throw "Could not extract $ttsArchivePath"
    }
}

foreach ($path in $ttsRequiredPaths) {
    if (-not (Test-Path -LiteralPath (Join-Path $ttsModelDirectory $path))) {
        throw "TTS setup is incomplete: missing $path"
    }
}

if (-not $modelReady) {
    if ($SkipDownload) {
        throw "Vietnamese ASR model is missing at $modelDirectory"
    }
    New-Item -ItemType Directory -Force -Path $downloadDirectory, $modelRoot | Out-Null
    if (-not (Test-Path -LiteralPath $archivePath)) {
        Write-Host "Downloading the Apache-2.0 Vietnamese ASR model (about 60 MB)..."
        $partialArchive = "$archivePath.partial"
        Invoke-WebRequest -Uri $downloadUrl -OutFile $partialArchive
        Move-Item -LiteralPath $partialArchive -Destination $archivePath -Force
    }
    Write-Host 'Extracting the Vietnamese ASR model...'
    & tar.exe -xf $archivePath -C $modelRoot
    if ($LASTEXITCODE -ne 0) {
        throw "Could not extract $archivePath"
    }
}

foreach ($file in $requiredFiles) {
    if (-not (Test-Path -LiteralPath (Join-Path $modelDirectory $file))) {
        throw "ASR setup is incomplete: missing $file"
    }
}

if (-not $SkipPublish) {
    New-Item -ItemType Directory -Force -Path $agentDirectory | Out-Null
    Write-Host 'Publishing the lightweight Windows voice-agent sidecar...'
    & dotnet publish (Join-Path $repoRoot 'Tools\AgentHost\AnimeAssistant.AgentHost.csproj') `
        -c Release -r win-x64 --self-contained true -o $agentDirectory
    if ($LASTEXITCODE -ne 0) {
        throw 'Voice-agent publish failed.'
    }
}

Write-Host "Voice agent is ready at $agentDirectory"
