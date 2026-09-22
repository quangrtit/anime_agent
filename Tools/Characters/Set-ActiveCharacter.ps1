[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$VrmPath,

    [string]$PlayerDirectory
)

$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
if ([string]::IsNullOrWhiteSpace($PlayerDirectory)) {
    $PlayerDirectory = Join-Path $repoRoot 'Builds\Windows'
}

$source = (Resolve-Path -LiteralPath $VrmPath).Path
if ([IO.Path]::GetExtension($source) -ine '.vrm') {
    throw "The selected file must use the .vrm extension: $source"
}

$player = [IO.Path]::GetFullPath($PlayerDirectory)
if (-not (Test-Path -LiteralPath (Join-Path $player 'AnimeAssistant.exe'))) {
    throw "AnimeAssistant.exe was not found in: $player"
}

$characters = Join-Path $player 'Characters'
New-Item -ItemType Directory -Force -Path $characters | Out-Null
$destination = Join-Path $characters ([IO.Path]::GetFileName($source))
Copy-Item -LiteralPath $source -Destination $destination -Force
[IO.File]::WriteAllText((Join-Path $characters 'active_character.txt'),
    [IO.Path]::GetFileName($destination), [Text.UTF8Encoding]::new($false))

Write-Host "Active character: $destination"
Write-Host 'Restart AnimeAssistant.exe to load the new avatar.'
