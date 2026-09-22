ANIME DESKTOP ASSISTANT - WINDOWS PORTABLE

Run:
  Double-click AnimeAssistant.exe.

Controls:
  Left-click the door: summon/return the active character.
  Right-click the door or character: exit.
  Ctrl+Shift+Q: exit from anywhere.

Change character (no rebuild required):
  1. Close AnimeAssistant.
  2. Copy a Humanoid VRM 0.x/1.0 file into the Characters folder.
  3. Put only its filename in Characters\active_character.txt.
  4. Start AnimeAssistant.exe again.

Write "embedded" to active_character.txt to use the built-in Unity-Chan model.
If a selected VRM cannot be loaded, embedded Unity-Chan is used. You are
responsible for the license of custom VRM files you add.

Layout and movement (no rebuild required):
  Edit Config\desktop_layout.json before starting the app. It controls door
  margins/scale, roaming screen range, perspective depth, walk/run speed,
  door-collapse timing, and the recall-button size.

Animations\Quaternius contains 46 CC0 Humanoid clips shared by every VRM.
Keep the .gltf and .bin files together. If removed, procedural motion is used.
The embedded Unity-Chan uses her own same-rig walk, idle, greeting, jump,
stretch, and celebration clips instead, which avoids cross-skeleton retargeting.

Unity-Chan notice:
  © Unity Technologies Japan/UCL
  License files: ThirdPartyLicenses\CHAR-004

Deployment:
  Keep AnimeAssistant.exe, AnimeAssistant_Data, UnityPlayer.dll,
  UnityCrashHandler64.exe, Characters, and Animations together.

Supported target:
  Windows 10/11 x64 with a DirectX 11-capable GPU.
