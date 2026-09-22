[CmdletBinding()]
param(
    [switch]$Force
)

$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$downloadsRoot = Join-Path $repoRoot '.downloads'
$packageRoot = Join-Path $repoRoot '.packages'
$upmRoot = Join-Path $packageRoot 'upm'
$toolsRoot = Join-Path $packageRoot 'tools'

New-Item -ItemType Directory -Force -Path $downloadsRoot, $upmRoot, $toolsRoot | Out-Null

$uniVrmVersion = 'v0.131.2'
$uniVrmCommit = 'a4711bbf8c4d10659d3e5568c2e3d7d595005e51'
$uniVrmPath = Join-Path $upmRoot 'UniVRM-v0.131.2'

if ($Force -and (Test-Path -LiteralPath $uniVrmPath)) {
    $resolvedUniVrmPath = (Resolve-Path -LiteralPath $uniVrmPath).Path
    if (-not $resolvedUniVrmPath.StartsWith($packageRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to remove unexpected path: $resolvedUniVrmPath"
    }
    Remove-Item -LiteralPath $resolvedUniVrmPath -Recurse -Force
}

if (-not (Test-Path -LiteralPath (Join-Path $uniVrmPath '.git'))) {
    & git clone --depth 1 --branch $uniVrmVersion --single-branch `
        https://github.com/vrm-c/UniVRM.git $uniVrmPath
    if ($LASTEXITCODE -ne 0) { throw 'Failed to clone UniVRM.' }
}

$actualUniVrmCommit = (& git -C $uniVrmPath rev-parse HEAD).Trim()
if ($actualUniVrmCommit -ne $uniVrmCommit) {
    throw "UniVRM commit mismatch. Expected $uniVrmCommit, got $actualUniVrmCommit."
}

$gitLfsVersion = '3.8.0'
$gitLfsArchiveName = "git-lfs-windows-amd64-v$gitLfsVersion.zip"
$gitLfsArchive = Join-Path $downloadsRoot $gitLfsArchiveName
$gitLfsExpectedSha256 = 'B62E7B8CEDDEE635F691233D77DE8EAA4B213E9209E0173811D8CFA77F7882C1'
$gitLfsUrl = "https://github.com/git-lfs/git-lfs/releases/download/v$gitLfsVersion/$gitLfsArchiveName"
$gitLfsDestination = Join-Path $toolsRoot "git-lfs-v$gitLfsVersion"
$gitLfsExecutable = Join-Path $gitLfsDestination "git-lfs-$gitLfsVersion\git-lfs.exe"

if (-not (Test-Path -LiteralPath $gitLfsArchive)) {
    Invoke-WebRequest -UseBasicParsing -Uri $gitLfsUrl -OutFile $gitLfsArchive
}

$actualGitLfsSha256 = (Get-FileHash -LiteralPath $gitLfsArchive -Algorithm SHA256).Hash
if ($actualGitLfsSha256 -ne $gitLfsExpectedSha256) {
    throw "Git LFS archive checksum mismatch. Expected $gitLfsExpectedSha256, got $actualGitLfsSha256."
}

if (-not (Test-Path -LiteralPath $gitLfsExecutable)) {
    Expand-Archive -LiteralPath $gitLfsArchive -DestinationPath $gitLfsDestination -Force
}

$gitDirectory = Join-Path $repoRoot '.git'
if (Test-Path -LiteralPath $gitDirectory) {
    $gitLfsForConfig = $gitLfsExecutable.Replace('\', '/')
    & git -C $repoRoot config --local filter.lfs.clean "`"$gitLfsForConfig`" clean -- %f"
    & git -C $repoRoot config --local filter.lfs.smudge "`"$gitLfsForConfig`" smudge -- %f"
    & git -C $repoRoot config --local filter.lfs.process "`"$gitLfsForConfig`" filter-process"
    & git -C $repoRoot config --local filter.lfs.required true
    if ($LASTEXITCODE -ne 0) { throw 'Failed to configure repository-local Git LFS filters.' }
}

Write-Host "UniVRM $uniVrmVersion restored at $uniVrmPath"
Write-Host "Git LFS $gitLfsVersion restored at $gitLfsExecutable"
Write-Host 'Unity registry packages will restore into Library/PackageCache when the Editor opens.'
