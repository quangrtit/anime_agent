[CmdletBinding()]
param(
    [string]$PythonCommand = 'python'
)

$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$output = Join-Path $repoRoot `
    'Assets\_Project\Resources\Audio\DesktopAssistant\voice_click_hurt_vi.mp3'
$text = [Text.Encoding]::UTF8.GetString(
    [Convert]::FromBase64String('xq8uLi4gxrAuLi4gxJFhdSBlbSE='))

& $PythonCommand -m edge_tts `
    --voice 'vi-VN-HoaiMyNeural' `
    --rate=-16% `
    --pitch=+32Hz `
    --volume=+4% `
    --text $text `
    --write-media $output

if ($LASTEXITCODE -ne 0 -or -not (Test-Path -LiteralPath $output) -or
    (Get-Item -LiteralPath $output).Length -lt 1000) {
    throw 'Vietnamese click-reaction voice generation failed.'
}

Write-Host "Generated $output with vi-VN-HoaiMyNeural."
