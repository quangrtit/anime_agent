[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$unity = Join-Path $repoRoot '.tools\Unity\Hub\Editor\6000.0.75f1\Editor\Unity.exe'

if (-not (Test-Path -LiteralPath $unity)) {
    throw "Unity 6000.0.75f1 is not installed at $unity"
}

Start-Process -FilePath $unity -ArgumentList @('-projectPath', $repoRoot) -WorkingDirectory $repoRoot

