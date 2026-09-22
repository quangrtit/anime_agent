[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$tools = @(
    @{ Name = 'Unity'; Path = Join-Path $repoRoot '.tools\Unity\Hub\Editor\6000.0.75f1\Editor\Unity.exe' },
    @{ Name = 'Blender'; Path = Join-Path $repoRoot '.tools\Blender\blender-5.2.2-windows-x64\blender.exe' },
    @{ Name = 'Git LFS'; Path = Join-Path $repoRoot '.packages\tools\git-lfs-v3.8.0\git-lfs-3.8.0\git-lfs.exe' }
)

$missing = @()
foreach ($tool in $tools) {
    if (Test-Path -LiteralPath $tool.Path) {
        Write-Host "[OK] $($tool.Name): $($tool.Path)"
    } else {
        Write-Warning "[MISSING] $($tool.Name): $($tool.Path)"
        $missing += $tool.Name
    }
}

if ($missing.Count -gt 0) {
    throw "Missing design tools: $($missing -join ', ')"
}

