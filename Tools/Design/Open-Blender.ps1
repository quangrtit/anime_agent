[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$blender = Join-Path $repoRoot '.tools\Blender\blender-5.2.2-windows-x64\blender.exe'

if (-not (Test-Path -LiteralPath $blender)) {
    throw "Blender 5.2.2 LTS is not installed at $blender"
}

Start-Process -FilePath $blender -WorkingDirectory $repoRoot

