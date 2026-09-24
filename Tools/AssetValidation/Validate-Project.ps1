[CmdletBinding()]
param(
    [switch]$RequireRestoredDependencies
)

$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$errors = [System.Collections.Generic.List[string]]::new()

$requiredPaths = @(
    'Assets/_Project/Scenes/Bootstrap.unity',
    'Assets/_Project/Scenes/DesktopPetPrototype.unity',
    'Assets/_Project/Scripts/Domain/AnimeAssistant.Domain.asmdef',
    'Assets/_Project/Scripts/Presentation/AnimeAssistant.Presentation.asmdef',
    'Assets/_Project/Scripts/Platform/AnimeAssistant.Platform.asmdef',
    'Assets/_Project/Tests/EditMode/AnimeAssistant.Tests.EditMode.asmdef',
    'Packages/manifest.json',
    'ProjectSettings/ProjectVersion.txt',
    'THIRD_PARTY_ASSETS.md',
    'RuntimeContent/Characters/2.vrm',
    'RuntimeContent/Characters/active_character.txt',
    'RuntimeContent/Config/desktop_layout.json',
    'RuntimeContent/Animations/Quaternius/AnimationLibrary_Godot_Standard.gltf',
    'RuntimeContent/Animations/Quaternius/AnimationLibrary_Godot_Standard.bin',
    'LICENSES/CHAR-003/LICENSE.txt',
    'Assets/ThirdParty/UnityChan/Models/unitychan.fbx',
    'Assets/ThirdParty/UnityChan/Animations/unitychan_WALK00_F.fbx',
    'LICENSES/CHAR-004/LICENSE.txt',
    'LICENSES/CHAR-004/UCL2.02_TERMS_EN.pdf',
    'LICENSES/ANIM-001/LICENSE.txt',
    'Assets/_Project/Resources/Audio/DesktopAssistant/button_press.ogg',
    'Assets/_Project/Resources/Audio/DesktopAssistant/door_open.ogg',
    'Assets/_Project/Resources/Audio/DesktopAssistant/voice_jump_1.wav',
    'Assets/_Project/Resources/Audio/DesktopAssistant/voice_chatter_vi_1.mp3',
    'Assets/_Project/Resources/Audio/DesktopAssistant/voice_chatter_jp_3.mp3',
    'Assets/_Project/Scripts/Presentation/DesktopAudioController.cs',
    'LICENSES/AUDIO-001/LICENSE.txt',
    'LICENSES/AUDIO-002/LICENSE.txt',
    'LICENSES/AUDIO-003/LICENSE.txt',
    'LICENSES/AUDIO-004/LICENSE.txt',
    'Tools/Build/Build-SingleExe.ps1',
    'Tools/PortableLauncher/AnimeAssistant.PortableLauncher.csproj'
)

foreach ($relativePath in $requiredPaths) {
    if (-not (Test-Path -LiteralPath (Join-Path $repoRoot $relativePath))) {
        $errors.Add("Missing required path: $relativePath")
    }
}

$pinnedRuntimeHashes = @{
    'RuntimeContent/Characters/2.vrm' = '2CC406BEA05F18CF1B6A195B15FB511E5C44B7AF18364E3D043ABF562738F927'
    'Assets/ThirdParty/UnityChan/Models/unitychan.fbx' = 'BF5A467E8A3922D06D4808BBAEF5CD9C95655574E63B0FD676996BADDC2221F9'
    'RuntimeContent/Animations/Quaternius/AnimationLibrary_Godot_Standard.gltf' = '0FF075C7AD6855C5C2C37A171592EE8F0D6AB2F58259E2BE77A9B63DD8027765'
    'RuntimeContent/Animations/Quaternius/AnimationLibrary_Godot_Standard.bin' = '6E65377D81558333C4093DBB144A48FD19019343D82B1A3A7992A98EC0E0543C'
}

$activeCharacter = (Get-Content -Raw -LiteralPath (Join-Path $repoRoot 'RuntimeContent/Characters/active_character.txt')).Trim()
if ($activeCharacter -ne 'embedded') {
    $errors.Add('The production default must select the embedded same-rig Unity-Chan avatar.')
}
foreach ($entry in $pinnedRuntimeHashes.GetEnumerator()) {
    $assetPath = Join-Path $repoRoot $entry.Key
    if ((Test-Path -LiteralPath $assetPath) -and
        (Get-FileHash -Algorithm SHA256 -LiteralPath $assetPath).Hash -ne $entry.Value) {
        $errors.Add("Runtime asset digest mismatch: $($entry.Key)")
    }
}

$manifestPath = Join-Path $repoRoot 'Packages/manifest.json'
$manifest = Get-Content -Raw -LiteralPath $manifestPath | ConvertFrom-Json
$requiredPackages = @(
    'com.unity.render-pipelines.universal',
    'com.unity.test-framework',
    'com.vrmc.gltf',
    'com.vrmc.univrm',
    'com.vrmc.vrm'
)

foreach ($packageName in $requiredPackages) {
    if ($null -eq $manifest.dependencies.$packageName) {
        $errors.Add("Missing pinned package: $packageName")
    }
}

if ($RequireRestoredDependencies) {
    foreach ($packageName in @('com.vrmc.gltf', 'com.vrmc.univrm', 'com.vrmc.vrm')) {
        $packageReference = [string]$manifest.dependencies.$packageName
        if (-not $packageReference.StartsWith('file:')) {
            $errors.Add("Expected project-local package reference for $packageName")
            continue
        }

        $relativePackagePath = $packageReference.Substring(5).Replace('/', [System.IO.Path]::DirectorySeparatorChar)
        $resolvedPackagePath = [System.IO.Path]::GetFullPath((Join-Path (Join-Path $repoRoot 'Packages') $relativePackagePath))
        if (-not (Test-Path -LiteralPath (Join-Path $resolvedPackagePath 'package.json'))) {
            $errors.Add("Restored package is missing: $packageName at $resolvedPackagePath")
        }
    }
}

$thirdPartyRoot = Join-Path $repoRoot 'Assets/ThirdParty'
$thirdPartyFiles = @(Get-ChildItem -LiteralPath $thirdPartyRoot -Recurse -File -ErrorAction SilentlyContinue |
    Where-Object { $_.Name -ne '.gitkeep' -and $_.Extension -ne '.meta' })
if ($thirdPartyFiles.Count -gt 0) {
    $assetManifest = Get-Content -Raw -LiteralPath (Join-Path $repoRoot 'THIRD_PARTY_ASSETS.md')
    if ($assetManifest -match 'No third-party content assets in M0') {
        $errors.Add('Third-party files exist but THIRD_PARTY_ASSETS.md still declares none.')
    }
}

if ($errors.Count -gt 0) {
    $errors | ForEach-Object { Write-Error $_ }
    exit 1
}

Write-Host "Project validation passed ($($requiredPaths.Count) required paths, $($requiredPackages.Count) pinned packages)."
