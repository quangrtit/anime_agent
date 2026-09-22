RUNTIME HUMANOID MOTIONS

The portable player loads its shared animation library from:

  Animations\Quaternius\AnimationLibrary_Godot_Standard.gltf

The accompanying .bin file must remain beside the .gltf file. These animations
are retargeted through Unity HumanPose, so the same library works with every
valid Humanoid VRM selected through the Characters folder.

The current CC0 library contains 46 clips, including idle, talking, formal walk,
normal walk, jog, sprint, jump, sitting, interaction, dance and other actions.
If the library is missing or invalid, the project-authored procedural movement
layer remains available as an automatic fallback.
