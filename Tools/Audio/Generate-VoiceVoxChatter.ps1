[CmdletBinding()]
param(
    [int]$StartIndex = 0,
    [int]$EndIndex = 7
)

$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$outputDirectory = Join-Path $repoRoot 'Assets\_Project\Resources\Audio\DesktopAssistant'
$logPath = Join-Path $repoRoot 'Logs\voicevox-generation.log'

$lines = @(
    @{ Name = 'voice_chatter_vi_1.mp3'; Speaker = 56; TextBase64 = '44Gt44GI44CB5LuK5pel44KC44KI44GP6aCR5by144Gj44Gf44Gt44CC5bCR44GX44GP44KJ44GE5LyR44KT44Gn44KC44GE44GE44KT44Gg44KI44CC' },
    @{ Name = 'voice_chatter_vi_2.mp3'; Speaker = 56; TextBase64 = '44KG44Gj44GP44KK5q2p44GP44GT44Go44Gv44CB5b6M44KN44Gr5LiL44GM44KL44GT44Go44GY44KD44Gq44GE44KI44CC44GC44GN44KJ44KB44Gq44GR44KM44Gw5aSn5LiI5aSr44CC' },
    @{ Name = 'voice_chatter_vi_3.mp3'; Speaker = 56; TextBase64 = '5a6M55Kn44GY44KD44Gq44GP44Gm44KC44GE44GE44Gu44CC56yR6aGU44Gr44Gq44KM44KL44GT44Go44GM5LiA44Gk44GC44KM44Gw44CB44Gd44KM44Gn57Sg5pW144Gq5LiA5pel44Gg44KI44CC' },
    @{ Name = 'voice_chatter_vi_4.mp3'; Speaker = 56; TextBase64 = '55ay44KM44Gf44KJ44CB44KG44Gj44GP44KK5rex5ZG85ZC444GX44Gm44Gt44CC44KP44Gf44GX44Gv44GT44GT44Gr44GE44KL44KI44CC' },
    @{ Name = 'voice_chatter_jp_1.mp3'; Speaker = 57; TextBase64 = '5LuK5pel44KC5LiA57eS44Gr44CB44Gu44KT44Gz44KK6aCR5by144KN44GG44Gt44CC' },
    @{ Name = 'voice_chatter_jp_2.mp3'; Speaker = 56; TextBase64 = '5bCP44GV44Gq5LiA5q2p44Gn44KC44CB5YmN44Gr6YCy44KB44Gw57Sg5pW144Gg44KI44CC' },
    @{ Name = 'voice_chatter_jp_3.mp3'; Speaker = 57; TextBase64 = '44KJ44KT44CB44KJ44KJ44KT44CC44G144G144Gj44CB5LuK5pel44KC44GE44GE5pel44Gr44Gq44KK44G+44GZ44KI44GG44Gr44CC' },
    @{ Name = 'voice_chatter_jp_4.mp3'; Speaker = 57; TextBase64 = '44GC44Gq44Gf44GM56yR44GG44Go44CB44KP44Gf44GX44KC44GG44KM44GX44GP44Gq44KL44Gu44CC44Gg44GL44KJ44CB56yR44Gj44Gm77yf' }
)

function Get-LineText($line) {
    return [Text.Encoding]::UTF8.GetString([Convert]::FromBase64String($line.TextBase64))
}

function Request-Synthesis($line) {
    $spokenText = Get-LineText $line
    for ($attempt = 0; $attempt -lt 20; $attempt++) {
        $responseText = (curl.exe -G --silent --show-error `
            'https://api.tts.quest/v3/voicevox/synthesis' `
            --data-urlencode "speaker=$($line.Speaker)" `
            --data-urlencode "text=$spokenText") -join "`n"
        try {
            $response = $responseText | ConvertFrom-Json
        }
        catch {
            $response = $null
        }
        if ($response -and $response.success) {
            return $response
        }

        $retrySeconds = if ($response -and $response.retryAfter) {
            [Math]::Min(60, [int]$response.retryAfter + 1)
        }
        else {
            10
        }
        Start-Sleep -Seconds $retrySeconds
    }
    throw "VOICEVOX request failed for $($line.Name)."
}

New-Item -ItemType Directory -Force -Path (Split-Path $logPath) | Out-Null
for ($index = $StartIndex; $index -le [Math]::Min($EndIndex, $lines.Count - 1); $index++) {
    $line = $lines[$index]
    $response = Request-Synthesis $line
    $ready = $false
    for ($poll = 0; $poll -lt 90; $poll++) {
        Start-Sleep -Seconds 2
        $statusText = (curl.exe -L --fail --silent --show-error $response.audioStatusUrl) -join "`n"
        $status = $statusText | ConvertFrom-Json
        if ($status.isAudioError) {
            throw "VOICEVOX synthesis failed for $($line.Name): $($status.status)"
        }
        if ($status.isAudioReady) {
            $ready = $true
            break
        }
    }
    if (-not $ready) {
        throw "VOICEVOX synthesis timed out for $($line.Name)."
    }

    $target = Join-Path $outputDirectory $line.Name
    curl.exe -L --fail --silent --show-error $response.mp3DownloadUrl -o $target
    if ($LASTEXITCODE -ne 0 -or (Get-Item $target).Length -lt 1000) {
        throw "VOICEVOX download failed for $($line.Name)."
    }
    "$(Get-Date -Format o)|$($line.Name)|$($response.speakerName)|$((Get-Item $target).Length)" |
        Add-Content -LiteralPath $logPath -Encoding UTF8
}
