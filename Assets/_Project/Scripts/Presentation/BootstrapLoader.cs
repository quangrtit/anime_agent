using System;
using System.IO;
using System.Threading.Tasks;
using UniGLTF;
using UniHumanoid;
using UniVRM10;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AnimeAssistant.Presentation
{
    [DefaultExecutionOrder(-1000)]
    public sealed class BootstrapLoader : MonoBehaviour
    {
        private const string PrototypeSceneName = "DesktopPetPrototype";

        private void Awake()
        {
            if (FindFirstObjectByType<RuntimeAvatarSlot>() == null)
            {
                var avatarSlot = new GameObject("Runtime Avatar Slot");
                DontDestroyOnLoad(avatarSlot);
                avatarSlot.AddComponent<RuntimeAvatarSlot>();
            }

            if (FindFirstObjectByType<RuntimeMotionLibrary>() == null)
            {
                var motionLibrary = new GameObject("Runtime Motion Library");
                DontDestroyOnLoad(motionLibrary);
                motionLibrary.AddComponent<RuntimeMotionLibrary>();
            }

            SceneManager.LoadScene(PrototypeSceneName, LoadSceneMode.Single);
        }
    }

    /// <summary>
    /// Loads a portable glTF motion library and retargets its legacy clips through
    /// Unity's HumanPose system. Avatar files stay independent from animation data.
    /// </summary>
    [DefaultExecutionOrder(-940)]
    [DisallowMultipleComponent]
    public sealed class RuntimeMotionLibrary : MonoBehaviour
    {
        private const string AnimationDirectoryName = "Animations";
        private const string VendorDirectoryName = "Quaternius";
        private const string LibraryFileName = "AnimationLibrary_Godot_Standard.gltf";

        private static readonly (string Name, HumanBodyBones Bone)[] BoneMap =
        {
            ("DEF-hips", HumanBodyBones.Hips),
            ("DEF-spine.001", HumanBodyBones.Spine),
            ("DEF-spine.002", HumanBodyBones.Chest),
            ("DEF-spine.003", HumanBodyBones.UpperChest),
            ("DEF-neck", HumanBodyBones.Neck),
            ("DEF-head", HumanBodyBones.Head),
            ("DEF-shoulder.L", HumanBodyBones.LeftShoulder),
            ("DEF-upper_arm.L", HumanBodyBones.LeftUpperArm),
            ("DEF-forearm.L", HumanBodyBones.LeftLowerArm),
            ("DEF-hand.L", HumanBodyBones.LeftHand),
            ("DEF-shoulder.R", HumanBodyBones.RightShoulder),
            ("DEF-upper_arm.R", HumanBodyBones.RightUpperArm),
            ("DEF-forearm.R", HumanBodyBones.RightLowerArm),
            ("DEF-hand.R", HumanBodyBones.RightHand),
            ("DEF-thigh.L", HumanBodyBones.LeftUpperLeg),
            ("DEF-shin.L", HumanBodyBones.LeftLowerLeg),
            ("DEF-foot.L", HumanBodyBones.LeftFoot),
            ("DEF-toe.L", HumanBodyBones.LeftToes),
            ("DEF-thigh.R", HumanBodyBones.RightUpperLeg),
            ("DEF-shin.R", HumanBodyBones.RightLowerLeg),
            ("DEF-foot.R", HumanBodyBones.RightFoot),
            ("DEF-toe.R", HumanBodyBones.RightToes),
            ("DEF-thumb.01.L", HumanBodyBones.LeftThumbProximal),
            ("DEF-thumb.02.L", HumanBodyBones.LeftThumbIntermediate),
            ("DEF-thumb.03.L", HumanBodyBones.LeftThumbDistal),
            ("DEF-f_index.01.L", HumanBodyBones.LeftIndexProximal),
            ("DEF-f_index.02.L", HumanBodyBones.LeftIndexIntermediate),
            ("DEF-f_index.03.L", HumanBodyBones.LeftIndexDistal),
            ("DEF-f_middle.01.L", HumanBodyBones.LeftMiddleProximal),
            ("DEF-f_middle.02.L", HumanBodyBones.LeftMiddleIntermediate),
            ("DEF-f_middle.03.L", HumanBodyBones.LeftMiddleDistal),
            ("DEF-f_ring.01.L", HumanBodyBones.LeftRingProximal),
            ("DEF-f_ring.02.L", HumanBodyBones.LeftRingIntermediate),
            ("DEF-f_ring.03.L", HumanBodyBones.LeftRingDistal),
            ("DEF-f_pinky.01.L", HumanBodyBones.LeftLittleProximal),
            ("DEF-f_pinky.02.L", HumanBodyBones.LeftLittleIntermediate),
            ("DEF-f_pinky.03.L", HumanBodyBones.LeftLittleDistal),
            ("DEF-thumb.01.R", HumanBodyBones.RightThumbProximal),
            ("DEF-thumb.02.R", HumanBodyBones.RightThumbIntermediate),
            ("DEF-thumb.03.R", HumanBodyBones.RightThumbDistal),
            ("DEF-f_index.01.R", HumanBodyBones.RightIndexProximal),
            ("DEF-f_index.02.R", HumanBodyBones.RightIndexIntermediate),
            ("DEF-f_index.03.R", HumanBodyBones.RightIndexDistal),
            ("DEF-f_middle.01.R", HumanBodyBones.RightMiddleProximal),
            ("DEF-f_middle.02.R", HumanBodyBones.RightMiddleIntermediate),
            ("DEF-f_middle.03.R", HumanBodyBones.RightMiddleDistal),
            ("DEF-f_ring.01.R", HumanBodyBones.RightRingProximal),
            ("DEF-f_ring.02.R", HumanBodyBones.RightRingIntermediate),
            ("DEF-f_ring.03.R", HumanBodyBones.RightRingDistal),
            ("DEF-f_pinky.01.R", HumanBodyBones.RightLittleProximal),
            ("DEF-f_pinky.02.R", HumanBodyBones.RightLittleIntermediate),
            ("DEF-f_pinky.03.R", HumanBodyBones.RightLittleDistal),
        };

        private RuntimeGltfInstance sourceInstance;
        private Animation sourceAnimation;
        private Avatar sourceAvatar;
        private HumanPoseHandler sourcePoseHandler;
        private HumanPose sourcePose;
        private Vector3 sourceNeutralBodyPosition;
        private string currentClip;
        private bool sampleConfirmed;

        public static RuntimeMotionLibrary Instance { get; private set; }
        public bool IsReady => sourcePoseHandler != null && sourceAnimation != null;

        private async void Awake()
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            try
            {
                await LoadLibraryAsync();
            }
            catch (Exception exception)
            {
                Debug.LogError($"[MotionLibrary] External motion loading failed; procedural fallback remains active. {exception}");
            }
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }

            sourcePoseHandler?.Dispose();
            if (sourceAvatar != null)
            {
                Destroy(sourceAvatar);
            }
        }

        public bool TrySample(string clipName, Vector3 targetNeutralBodyPosition,
            Quaternion targetNeutralBodyRotation, ref HumanPose targetPose)
        {
            if (!IsReady || string.IsNullOrEmpty(clipName) || sourceAnimation.GetClip(clipName) == null)
            {
                return false;
            }

            if (!string.Equals(currentClip, clipName, StringComparison.Ordinal))
            {
                sourceAnimation.CrossFade(clipName, 0.28f, PlayMode.StopAll);
                currentClip = clipName;
            }

            sourcePoseHandler.GetHumanPose(ref sourcePose);
            targetPose = sourcePose;
            var verticalMotion = Mathf.Clamp(sourcePose.bodyPosition.y - sourceNeutralBodyPosition.y, -0.32f, 0.14f);
            targetPose.bodyPosition = targetNeutralBodyPosition + Vector3.up * verticalMotion;
            targetPose.bodyRotation = targetNeutralBodyRotation;
            if (!sampleConfirmed)
            {
                sampleConfirmed = true;
                Debug.Log($"[MotionLibrary] Retargeting clip '{clipName}' onto the active VRM.");
            }
            return true;
        }

        private async Task LoadLibraryAsync()
        {
            var playerDirectory = Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath;
            var libraryPath = Path.Combine(playerDirectory, AnimationDirectoryName,
                VendorDirectoryName, LibraryFileName);
            if (!File.Exists(libraryPath))
            {
                Debug.LogWarning($"[MotionLibrary] No external library found at '{libraryPath}'.");
                return;
            }

            sourceInstance = await GltfUtility.LoadAsync(libraryPath, new RuntimeOnlyAwaitCaller());
            if (sourceInstance == null)
            {
                throw new InvalidOperationException("UniGLTF returned no motion source instance.");
            }

            sourceInstance.transform.SetParent(transform, false);
            sourceInstance.gameObject.name = "QuaterniusMotionSource";
            foreach (var renderer in sourceInstance.GetComponentsInChildren<Renderer>(true))
            {
                renderer.enabled = false;
            }

            sourceAnimation = sourceInstance.GetComponent<Animation>();
            if (sourceAnimation == null || sourceInstance.AnimationClips.Count == 0)
            {
                throw new InvalidOperationException("The glTF library contains no playable clips.");
            }

            foreach (var clip in sourceInstance.AnimationClips)
            {
                clip.wrapMode = clip.name.IndexOf("Loop", StringComparison.OrdinalIgnoreCase) >= 0
                    ? WrapMode.Loop
                    : WrapMode.ClampForever;
            }

            var transforms = sourceInstance.GetComponentsInChildren<Transform>(true);
            var humanoidMap = new System.Collections.Generic.List<(Transform, HumanBodyBones)>();
            foreach (var entry in BoneMap)
            {
                Transform match = null;
                foreach (var candidate in transforms)
                {
                    if (candidate.name == entry.Name)
                    {
                        match = candidate;
                        break;
                    }
                }

                if (match != null)
                {
                    humanoidMap.Add((match, entry.Bone));
                }
            }

            sourceAvatar = HumanoidLoader.BuildHumanAvatarFromMap(sourceInstance.transform, humanoidMap);
            if (sourceAvatar == null || !sourceAvatar.isValid || !sourceAvatar.isHuman)
            {
                throw new InvalidOperationException("The external animation skeleton could not be mapped as Humanoid.");
            }

            sourcePoseHandler = new HumanPoseHandler(sourceAvatar, sourceInstance.transform);
            sourcePose = new HumanPose();
            sourcePoseHandler.GetHumanPose(ref sourcePose);
            sourceNeutralBodyPosition = sourcePose.bodyPosition;
            sourceAnimation.Play("Idle_Loop");
            currentClip = "Idle_Loop";
            Debug.Log($"[MotionLibrary] Loaded {sourceInstance.AnimationClips.Count} CC0 Humanoid clips from Quaternius. " +
                      "Avatar-independent retargeting is active.");
        }
    }

    /// <summary>
    /// Loads the selected VRM from the Characters directory next to the Windows
    /// executable. The scene avatar remains as an embedded, always-available
    /// fallback, so changing characters never requires rebuilding the player.
    /// </summary>
    [DefaultExecutionOrder(-950)]
    [DisallowMultipleComponent]
    public sealed class RuntimeAvatarSlot : MonoBehaviour
    {
        private const string PrototypeSceneName = "DesktopPetPrototype";
        private const string CharacterDirectoryName = "Characters";
        private const string SelectionFileName = "active_character.txt";
        private const string VisualSlotName = "AvatarVisualFacing";
        private const string EmbeddedAvatarName = "UnityChanAvatar";
        private const float TargetAvatarHeight = 2.05f;

        private bool loadInProgress;

        private void Awake()
        {
            DontDestroyOnLoad(gameObject);
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        private async void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name != PrototypeSceneName || loadInProgress)
            {
                return;
            }

            loadInProgress = true;
            try
            {
                await LoadSelectedAvatarAsync();
            }
            catch (Exception exception)
            {
                Debug.LogError($"[AvatarSlot] Custom avatar failed; using embedded Unity-Chan. {exception}");
            }
            finally
            {
                loadInProgress = false;
            }
        }

        private static async Task LoadSelectedAvatarAsync()
        {
            var charactersDirectory = GetCharactersDirectory();
            var selectedPath = ResolveSelectedPath(charactersDirectory);
            if (string.IsNullOrEmpty(selectedPath))
            {
                Debug.Log($"[AvatarSlot] No custom VRM selected. Put a .vrm file in '{charactersDirectory}' " +
                          $"and write its filename to {SelectionFileName}. Using embedded Unity-Chan.");
                return;
            }

            var visualSlot = GameObject.Find(VisualSlotName);
            var embeddedAvatar = FindEmbeddedAvatar(visualSlot);
            if (visualSlot == null)
            {
                throw new InvalidOperationException($"Scene is missing {VisualSlotName}.");
            }

            var instance = await Vrm10.LoadPathAsync(
                selectedPath,
                canLoadVrm0X: true,
                showMeshes: true,
                awaitCaller: new RuntimeOnlyAwaitCaller());
            if (instance == null)
            {
                throw new InvalidOperationException("UniVRM returned no avatar instance.");
            }

            var loadedAvatar = instance.gameObject;
            loadedAvatar.name = $"RuntimeAvatar_{Path.GetFileNameWithoutExtension(selectedPath)}";
            loadedAvatar.transform.SetParent(visualSlot.transform, false);
            loadedAvatar.transform.localPosition = Vector3.zero;
            loadedAvatar.transform.localRotation = Quaternion.identity;
            loadedAvatar.transform.localScale = Vector3.one;

            var animator = loadedAvatar.GetComponentInChildren<Animator>(true);
            if (animator == null || animator.avatar == null || !animator.avatar.isHuman)
            {
                Destroy(loadedAvatar);
                throw new InvalidOperationException("The selected VRM does not contain a valid Humanoid avatar.");
            }

            FitAvatarToSlot(loadedAvatar);
            loadedAvatar.AddComponent<HumanoidAvatarMotion>();
            if (embeddedAvatar != null)
            {
                embeddedAvatar.SetActive(false);
            }

            var bounds = CalculateBounds(loadedAvatar);
            Debug.Log($"[AvatarSlot] Loaded '{Path.GetFileName(selectedPath)}' as the active avatar. " +
                      $"Height={bounds.size.y:F2}m, Renderers={loadedAvatar.GetComponentsInChildren<Renderer>(true).Length}, " +
                      "Humanoid=True. The embedded Unity-Chan remains available as fallback.");
        }

        private static string GetCharactersDirectory()
        {
            var playerDirectory = Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath;
            return Path.Combine(playerDirectory, CharacterDirectoryName);
        }

        private static string ResolveSelectedPath(string charactersDirectory)
        {
            var arguments = Environment.GetCommandLineArgs();
            for (var i = 0; i < arguments.Length - 1; i++)
            {
                if (string.Equals(arguments[i], "--vrm", StringComparison.OrdinalIgnoreCase))
                {
                    return ValidateVrmPath(arguments[i + 1]);
                }
            }

            var selectionPath = Path.Combine(charactersDirectory, SelectionFileName);
            if (!File.Exists(selectionPath))
            {
                return null;
            }

            // Only a filename is accepted here. This prevents a portable config
            // from unexpectedly loading files outside its Characters directory.
            var selectedName = File.ReadAllText(selectionPath).Trim();
            if (string.IsNullOrWhiteSpace(selectedName) ||
                string.Equals(selectedName, "embedded", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            if (selectedName != Path.GetFileName(selectedName))
            {
                throw new InvalidDataException($"{SelectionFileName} must contain one .vrm filename only.");
            }

            return ValidateVrmPath(Path.Combine(charactersDirectory, selectedName));
        }

        private static GameObject FindEmbeddedAvatar(GameObject visualSlot)
        {
            var named = GameObject.Find(EmbeddedAvatarName) ?? GameObject.Find("MichanAvatar");
            if (named != null)
            {
                return named;
            }

            if (visualSlot == null)
            {
                return null;
            }

            foreach (Transform child in visualSlot.transform)
            {
                if (!child.name.StartsWith("RuntimeAvatar_", StringComparison.Ordinal))
                {
                    return child.gameObject;
                }
            }

            return null;
        }

        private static string ValidateVrmPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path) ||
                !string.Equals(Path.GetExtension(path), ".vrm", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException("Selected character must be a .vrm file.");
            }

            var fullPath = Path.GetFullPath(path);
            if (!File.Exists(fullPath))
            {
                throw new FileNotFoundException("Selected VRM was not found.", fullPath);
            }

            return fullPath;
        }

        private static void FitAvatarToSlot(GameObject avatar)
        {
            var initialBounds = CalculateBounds(avatar);
            if (initialBounds.size.y <= 0.01f)
            {
                throw new InvalidOperationException("The selected VRM has no renderable bounds.");
            }

            avatar.transform.localScale = Vector3.one * (TargetAvatarHeight / initialBounds.size.y);
            var fittedBounds = CalculateBounds(avatar);
            avatar.transform.position += new Vector3(
                -fittedBounds.center.x + avatar.transform.parent.position.x,
                0.02f - fittedBounds.min.y + avatar.transform.parent.position.y,
                -fittedBounds.center.z + avatar.transform.parent.position.z);
        }

        private static Bounds CalculateBounds(GameObject root)
        {
            var renderers = root.GetComponentsInChildren<Renderer>(true);
            var hasBounds = false;
            var combined = new Bounds(root.transform.position, Vector3.zero);
            foreach (var renderer in renderers)
            {
                if (renderer is ParticleSystemRenderer || renderer.bounds.size.sqrMagnitude <= 0.000001f)
                {
                    continue;
                }

                if (!hasBounds)
                {
                    combined = renderer.bounds;
                    hasBounds = true;
                }
                else
                {
                    combined.Encapsulate(renderer.bounds);
                }
            }

            return combined;
        }
    }
}
