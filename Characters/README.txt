CUSTOM VRM CHARACTERS

The Windows player reads custom avatars from the Characters directory beside
AnimeAssistant.exe. Put a VRM 0.x or VRM 1.0 file there, then write that file's
name into active_character.txt and restart the app.

Recommended command from the repository root:

  powershell -NoProfile -ExecutionPolicy Bypass -File .\Tools\Characters\Set-ActiveCharacter.ps1 -VrmPath "C:\path\character.vrm"

The loader validates the Humanoid rig, scales the model to the scene, preserves
VRM expressions and spring bones, and falls back to embedded Unity-Chan if
loading fails. Write "embedded" to active_character.txt to select that built-in
character explicitly. Only use avatars whose licenses permit your intended
use/distribution.
