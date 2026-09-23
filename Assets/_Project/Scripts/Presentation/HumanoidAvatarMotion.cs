using System;
using System.Collections.Generic;
using AnimeAssistant.Domain;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.InputSystem;
using UnityEngine.Playables;
using UniVRM10;

namespace AnimeAssistant.Presentation
{
    public enum AvatarIdleBehaviour
    {
        Idle,
        Celebration,
        LookAround,
        Wave,
        HairAdjust,
        Sit,
        Stretch,
        Wander,
        ShortHop
    }

    /// <summary>
    /// Model-independent motion and face layer for a Humanoid VRM avatar.
    /// Human-muscle animation keeps the pose portable across VRoid rigs while
    /// the model's own spring-bone components remain responsible for hair.
    /// </summary>
    [DefaultExecutionOrder(10000)]
    [DisallowMultipleComponent]
    public sealed class HumanoidAvatarMotion : MonoBehaviour
    {
        [Header("Optional same-rig native clips")]
        [SerializeField] private AnimationClip nativeIdleClip;
        [SerializeField] private AnimationClip nativeIdleVariantClip;
        [SerializeField] private AnimationClip nativeWalkClip;
        [SerializeField] private AnimationClip nativeRunClip;
        [SerializeField] private AnimationClip nativeInteractClip;
        [SerializeField] private AnimationClip nativeJumpClip;
        [SerializeField] private AnimationClip nativeCelebrationClip;
        [SerializeField] private AnimationClip nativeStretchClip;

        private readonly List<BlendShapeBinding> blinkBindings = new List<BlendShapeBinding>();
        private readonly List<BlendShapeBinding> smileBindings = new List<BlendShapeBinding>();
        private readonly List<BlendShapeBinding> mouthOpenBindings = new List<BlendShapeBinding>();
        private readonly Dictionary<string, int> muscleIndices = new Dictionary<string, int>();

        private GreyboxSummonController summonController;
        private Animator animator;
        private Vrm10Instance vrmInstance;
        private HumanPoseHandler poseHandler;
        private HumanPose pose;
        private float[] neutralMuscles;
        private Vector3 neutralBodyPosition;
        private Quaternion neutralBodyRotation;
        private Transform leftEye;
        private Transform rightEye;
        private Quaternion leftEyeNeutral;
        private Quaternion rightEyeNeutral;
        private float blinkCountdown = 1.6f;
        private float blinkElapsed = -1f;
        private float activeElapsed;
        private float behaviourElapsed;
        private float behaviourDuration;
        private float nextBehaviourDelay = 1.1f;
        private uint randomState = 0x6D2B79F5u;
        private Vector3 roamingOffset;
        private Vector3 roamStartOffset;
        private Vector3 roamTargetOffset;
        private AvatarIdleBehaviour currentBehaviour = AvatarIdleBehaviour.Idle;
        private AvatarIdleBehaviour previousBehaviour = AvatarIdleBehaviour.Idle;
        private bool hasRoamedSinceSummon;
        private bool roamUsesRun;
        private SummonState previousState = SummonState.DoorClosed;
        private bool initialized;
        private PlayableGraph nativeGraph;
        private AnimationMixerPlayable nativeMixer;
        private AnimationClipPlayable nativeCurrentPlayable;
        private AnimationClipPlayable nativeNextPlayable;
        private AnimationClip nativeCurrentClip;
        private int nativeCurrentPort;
        private float nativeBlendElapsed;
        private const float NativeBlendDuration = 0.28f;

        public AvatarIdleBehaviour CurrentBehaviour => currentBehaviour;
        public bool IsRoamRunning => currentBehaviour == AvatarIdleBehaviour.Wander && roamUsesRun;
        public static int BehaviourTemplateCount => 7;

        private void Start()
        {
            InitializeIfNeeded();
        }

        private void OnDestroy()
        {
            poseHandler?.Dispose();
            if (nativeGraph.IsValid())
            {
                nativeGraph.Destroy();
            }
        }

        public void ConfigureNativeClips(AnimationClip idle, AnimationClip idleVariant,
            AnimationClip walk, AnimationClip run, AnimationClip interact, AnimationClip jump,
            AnimationClip celebration, AnimationClip stretch)
        {
            nativeIdleClip = idle;
            nativeIdleVariantClip = idleVariant;
            nativeWalkClip = walk;
            nativeRunClip = run;
            nativeInteractClip = interact;
            nativeJumpClip = jump;
            nativeCelebrationClip = celebration;
            nativeStretchClip = stretch;
        }

        private void LateUpdate()
        {
            if (!InitializeIfNeeded())
            {
                return;
            }

            var state = summonController != null ? summonController.State : SummonState.DoorClosed;
            if (state != previousState)
            {
                activeElapsed = 0f;
                if (state == SummonState.AvatarActive)
                {
                    roamingOffset = Vector3.zero;
                    roamStartOffset = Vector3.zero;
                    roamTargetOffset = Vector3.zero;
                    hasRoamedSinceSummon = false;
                }
                previousState = state;
            }

            if (state == SummonState.AvatarActive)
            {
                activeElapsed += Time.unscaledDeltaTime;
            }

            UpdateBehaviourScheduler(state, Time.unscaledDeltaTime);

            var locomoting = state == SummonState.AvatarExiting ||
                             state == SummonState.AvatarReturning ||
                             state == SummonState.AvatarEntering;
            var celebrating = state == SummonState.AvatarActive && activeElapsed < 2.4f;
            var phase = Time.unscaledTime * (locomoting ? 4.6f : 1.15f);
            if (nativeGraph.IsValid())
            {
                UpdateNativeAnimation(ResolveNativeClip(state, celebrating), Time.unscaledDeltaTime);
            }
            else
            {
                var externalClip = ResolveExternalClip(state, celebrating);
                if (RuntimeMotionLibrary.Instance == null ||
                    !RuntimeMotionLibrary.Instance.TrySample(externalClip, neutralBodyPosition,
                        neutralBodyRotation, ref pose))
                {
                    ApplyBodyPose(locomoting, celebrating, phase, activeElapsed);
                }
                else
                {
                    poseHandler.SetHumanPose(ref pose);
                }
            }
            ApplyActiveMotionOffset(state, celebrating);
            UpdateBlink(Time.unscaledDeltaTime);
            ApplyExpressions(state, activeElapsed);
            ApplyEyeLook();
        }

        /// <summary>Applies a representative relaxed pose for editor evidence captures.</summary>
        public void ApplyEditorPreviewPose()
        {
            if (!InitializeIfNeeded())
            {
                return;
            }

            ApplyBodyPose(false, true, 0.65f, 1.1f);
            SetBlinkWeight(0f);
        }

        private bool InitializeIfNeeded()
        {
            if (initialized)
            {
                return poseHandler != null;
            }

            initialized = true;
            summonController = FindFirstObjectByType<GreyboxSummonController>();
            animator = GetComponentInChildren<Animator>(true);
            vrmInstance = GetComponent<Vrm10Instance>() ?? GetComponentInChildren<Vrm10Instance>(true);
            if (animator == null || animator.avatar == null || !animator.avatar.isHuman)
            {
                Debug.LogError("HumanoidAvatarMotion requires a valid Humanoid Animator.", this);
                return false;
            }

            poseHandler = new HumanPoseHandler(animator.avatar, animator.transform);
            pose = new HumanPose();
            poseHandler.GetHumanPose(ref pose);
            neutralMuscles = (float[])pose.muscles.Clone();
            neutralBodyPosition = pose.bodyPosition;
            neutralBodyRotation = pose.bodyRotation;
            for (var i = 0; i < HumanTrait.MuscleName.Length; i++)
            {
                muscleIndices[HumanTrait.MuscleName[i]] = i;
            }

            leftEye = animator.GetBoneTransform(HumanBodyBones.LeftEye);
            rightEye = animator.GetBoneTransform(HumanBodyBones.RightEye);
            if (leftEye != null)
            {
                leftEyeNeutral = leftEye.localRotation;
            }

            if (rightEye != null)
            {
                rightEyeNeutral = rightEye.localRotation;
            }

            CacheBlinkBlendShapes();
            InitializeNativeAnimation();
            return true;
        }

        private void InitializeNativeAnimation()
        {
            if (!Application.isPlaying || nativeIdleClip == null || animator == null || nativeGraph.IsValid())
            {
                return;
            }

            nativeGraph = PlayableGraph.Create($"{name} Native Humanoid Motion");
            nativeGraph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
            nativeMixer = AnimationMixerPlayable.Create(nativeGraph, 2);
            var output = AnimationPlayableOutput.Create(nativeGraph, "Avatar Motion", animator);
            output.SetSourcePlayable(nativeMixer);
            nativeGraph.Play();
            StartNativeClip(nativeIdleClip, true);
            Debug.Log($"[AvatarMotion] Using same-rig native animation clips for '{name}'.", this);
        }

        private void StartNativeClip(AnimationClip clip, bool immediate = false)
        {
            if (clip == null || !nativeGraph.IsValid() || clip == nativeCurrentClip)
            {
                return;
            }

            var nextPort = nativeCurrentClip == null ? 0 : 1 - nativeCurrentPort;
            if (nativeMixer.GetInput(nextPort).IsValid())
            {
                nativeGraph.Disconnect(nativeMixer, nextPort);
            }

            nativeNextPlayable = AnimationClipPlayable.Create(nativeGraph, clip);
            nativeNextPlayable.SetApplyFootIK(true);
            // Looping clips must remain alive for the whole route. Limiting the
            // playable to one clip length caused a long walk to freeze on its
            // final airborne/contact pose while the root kept travelling.
            nativeNextPlayable.SetDuration(clip.isLooping ? double.PositiveInfinity : clip.length);
            nativeGraph.Connect(nativeNextPlayable, 0, nativeMixer, nextPort);
            nativeMixer.SetInputWeight(nextPort, immediate ? 1f : 0f);
            nativeBlendElapsed = immediate ? NativeBlendDuration : 0f;

            if (immediate || nativeCurrentClip == null)
            {
                if (nativeCurrentPlayable.IsValid())
                {
                    nativeGraph.Disconnect(nativeMixer, nativeCurrentPort);
                    nativeCurrentPlayable.Destroy();
                }

                nativeCurrentPlayable = nativeNextPlayable;
                nativeCurrentPort = nextPort;
                nativeCurrentClip = clip;
                return;
            }

            nativeCurrentClip = clip;
        }

        private void UpdateNativeAnimation(AnimationClip desired, float deltaSeconds)
        {
            if (desired != null && desired != nativeCurrentClip)
            {
                StartNativeClip(desired);
            }

            if (!nativeNextPlayable.IsValid() || nativeNextPlayable.Equals(nativeCurrentPlayable))
            {
                return;
            }

            nativeBlendElapsed += deltaSeconds;
            var t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(nativeBlendElapsed / NativeBlendDuration));
            var nextPort = 1 - nativeCurrentPort;
            nativeMixer.SetInputWeight(nativeCurrentPort, 1f - t);
            nativeMixer.SetInputWeight(nextPort, t);
            if (t < 1f)
            {
                return;
            }

            nativeGraph.Disconnect(nativeMixer, nativeCurrentPort);
            if (nativeCurrentPlayable.IsValid())
            {
                nativeCurrentPlayable.Destroy();
            }

            nativeCurrentPlayable = nativeNextPlayable;
            nativeCurrentPort = nextPort;
        }

        private AnimationClip ResolveNativeClip(SummonState state, bool celebrating)
        {
            if (state == SummonState.AvatarExiting || state == SummonState.AvatarReturning ||
                state == SummonState.AvatarEntering || currentBehaviour == AvatarIdleBehaviour.Wander)
            {
                if (currentBehaviour == AvatarIdleBehaviour.Wander && roamUsesRun)
                {
                    return nativeRunClip ?? nativeWalkClip ?? nativeIdleClip;
                }

                return nativeWalkClip ?? nativeIdleClip;
            }

            if (celebrating || currentBehaviour == AvatarIdleBehaviour.Celebration)
            {
                return nativeCelebrationClip ?? nativeInteractClip ?? nativeIdleClip;
            }

            switch (currentBehaviour)
            {
                case AvatarIdleBehaviour.Wave:
                case AvatarIdleBehaviour.HairAdjust:
                    return nativeInteractClip ?? nativeIdleClip;
                case AvatarIdleBehaviour.Stretch:
                    return nativeStretchClip ?? nativeIdleVariantClip ?? nativeIdleClip;
                case AvatarIdleBehaviour.ShortHop:
                    return nativeJumpClip ?? nativeIdleClip;
                case AvatarIdleBehaviour.LookAround:
                case AvatarIdleBehaviour.Sit:
                    return nativeIdleVariantClip ?? nativeIdleClip;
                default:
                    return nativeIdleClip;
            }
        }

        private string ResolveExternalClip(SummonState state, bool celebrating)
        {
            if (state == SummonState.AvatarExiting || state == SummonState.AvatarReturning ||
                state == SummonState.AvatarEntering)
            {
                return "Walk_Formal_Loop";
            }

            if (celebrating)
            {
                return "Idle_Talking_Loop";
            }

            switch (currentBehaviour)
            {
                case AvatarIdleBehaviour.Celebration:
                    return "Dance_Loop";
                case AvatarIdleBehaviour.LookAround:
                    return "Idle_Talking_Loop";
                case AvatarIdleBehaviour.Wave:
                case AvatarIdleBehaviour.HairAdjust:
                    return "Interact";
                case AvatarIdleBehaviour.Sit:
                    return "Sitting_Idle_Loop";
                case AvatarIdleBehaviour.Stretch:
                    return "Spell_Simple_Idle_Loop";
                case AvatarIdleBehaviour.Wander:
                    return "Walk_Formal_Loop";
                case AvatarIdleBehaviour.ShortHop:
                    return "Jump_Loop";
                default:
                    return "Idle_Loop";
            }
        }

        private void ApplyBodyPose(bool locomoting, bool celebrating, float phase, float celebrationTime)
        {
            Array.Copy(neutralMuscles, pose.muscles, neutralMuscles.Length);
            pose.bodyPosition = neutralBodyPosition;

            // A relaxed silhouette replaces the import T-pose. Values are Humanoid
            // muscles, so they remain consistent even when the source rig differs.
            SetMuscle("Left Shoulder Down-Up", -0.12f);
            SetMuscle("Right Shoulder Down-Up", -0.12f);
            SetMuscle("Left Arm Down-Up", -0.78f);
            SetMuscle("Right Arm Down-Up", -0.78f);
            SetMuscle("Left Forearm Stretch", 0.08f);
            SetMuscle("Right Forearm Stretch", 0.08f);

            var breath = Mathf.Sin(phase) * 0.035f;
            SetMuscle("Spine Front-Back", -0.02f + breath * 0.35f);
            SetMuscle("Chest Front-Back", 0.04f + breath);
            SetMuscle("UpperChest Front-Back", 0.025f + breath * 0.45f);
            SetMuscle("Neck Left-Right", Mathf.Sin(phase * 0.47f) * 0.008f);
            SetMuscle("Head Left-Right", Mathf.Sin(phase * 0.42f + 0.4f) * 0.01f);

            if (celebrating)
            {
                var greeting = Mathf.Sin(celebrationTime * 5.2f);
                var settle = Mathf.Sin(Mathf.Clamp01(celebrationTime / 2.4f) * Mathf.PI);
                pose.bodyPosition = neutralBodyPosition + Vector3.up * (settle * 0.012f);
                SetMuscle("Left Arm Down-Up", -0.72f);
                SetMuscle("Left Forearm Stretch", 0.04f);
                SetMuscle("Right Shoulder Down-Up", 0.30f);
                SetMuscle("Right Arm Down-Up", -0.12f);
                SetMuscle("Right Arm Front-Back", -0.16f);
                SetMuscle("Right Forearm Stretch", -0.62f);
                SetMuscle("Right Hand In-Out", greeting * 0.32f);
                SetMuscle("Spine Left-Right", greeting * 0.018f);
                SetMuscle("Head Left-Right", 0f);
            }
            else if (locomoting)
            {
                var stride = Mathf.Sin(phase);
                var opposite = Mathf.Sin(phase + Mathf.PI);
                var stepLift = Mathf.Pow(Mathf.Abs(stride), 1.5f);
                pose.bodyPosition = neutralBodyPosition + Vector3.up * (stepLift * 0.014f);
                SetMuscle("Left Upper Leg Front-Back", stride * 0.32f);
                SetMuscle("Right Upper Leg Front-Back", opposite * 0.32f);
                SetMuscle("Left Upper Leg In-Out", 0.025f);
                SetMuscle("Right Upper Leg In-Out", -0.025f);
                SetMuscle("Left Lower Leg Stretch", Mathf.Max(0f, -stride) * 0.36f);
                SetMuscle("Right Lower Leg Stretch", Mathf.Max(0f, -opposite) * 0.36f);
                SetMuscle("Left Arm Front-Back", opposite * 0.18f);
                SetMuscle("Right Arm Front-Back", stride * 0.18f);
                SetMuscle("Left Foot Up-Down", -stride * 0.09f);
                SetMuscle("Right Foot Up-Down", -opposite * 0.09f);
                SetMuscle("Spine Left-Right", stride * 0.018f);
            }
            else
            {
                ApplyIdleBehaviour(phase);
            }

            poseHandler.SetHumanPose(ref pose);
        }

        private void ApplyIdleBehaviour(float phase)
        {
            var t = behaviourDuration > 0f ? Mathf.Clamp01(behaviourElapsed / behaviourDuration) : 0f;
            var pulse = Mathf.Sin(t * Mathf.PI);
            switch (currentBehaviour)
            {
                case AvatarIdleBehaviour.LookAround:
                    SetMuscle("Neck Left-Right", Mathf.Sin(t * Mathf.PI * 2f) * 0.07f);
                    SetMuscle("Head Left-Right", Mathf.Sin(t * Mathf.PI * 2f) * 0.16f);
                    SetMuscle("Head Up-Down", pulse * -0.08f);
                    break;
                case AvatarIdleBehaviour.Wave:
                    SetMuscle("Right Shoulder Down-Up", 0.48f);
                    SetMuscle("Right Arm Down-Up", 0.28f);
                    SetMuscle("Right Arm Front-Back", -0.18f);
                    SetMuscle("Right Forearm Stretch", -0.68f);
                    SetMuscle("Right Hand In-Out", Mathf.Sin(t * Mathf.PI * 7f) * 0.55f);
                    SetMuscle("Head Left-Right", -0.12f);
                    break;
                case AvatarIdleBehaviour.HairAdjust:
                    SetMuscle("Right Shoulder Down-Up", 0.34f);
                    SetMuscle("Right Arm Down-Up", 0.02f);
                    SetMuscle("Right Arm Front-Back", -0.32f);
                    SetMuscle("Right Forearm Stretch", -0.76f);
                    SetMuscle("Right Hand Down-Up", 0.22f);
                    SetMuscle("Head Left-Right", 0.03f * pulse);
                    break;
                case AvatarIdleBehaviour.Sit:
                    pose.bodyPosition = neutralBodyPosition + Vector3.down * (0.25f * pulse);
                    SetMuscle("Left Upper Leg Front-Back", -0.62f * pulse);
                    SetMuscle("Right Upper Leg Front-Back", -0.62f * pulse);
                    SetMuscle("Left Lower Leg Stretch", -0.58f * pulse);
                    SetMuscle("Right Lower Leg Stretch", -0.58f * pulse);
                    SetMuscle("Spine Front-Back", 0.16f * pulse);
                    SetMuscle("Left Arm Front-Back", -0.14f * pulse);
                    SetMuscle("Right Arm Front-Back", -0.14f * pulse);
                    break;
                case AvatarIdleBehaviour.Stretch:
                    SetMuscle("Left Shoulder Down-Up", 0.62f * pulse);
                    SetMuscle("Right Shoulder Down-Up", 0.62f * pulse);
                    SetMuscle("Left Arm Down-Up", Mathf.Lerp(-0.78f, 0.55f, pulse));
                    SetMuscle("Right Arm Down-Up", Mathf.Lerp(-0.78f, 0.55f, pulse));
                    SetMuscle("Left Forearm Stretch", -0.12f * pulse);
                    SetMuscle("Right Forearm Stretch", -0.12f * pulse);
                    SetMuscle("Spine Front-Back", -0.18f * pulse);
                    pose.bodyPosition = neutralBodyPosition + Vector3.up * (0.035f * pulse);
                    break;
                case AvatarIdleBehaviour.Wander:
                {
                    var stride = Mathf.Sin(t * Mathf.PI * 6f);
                    SetMuscle("Left Upper Leg Front-Back", stride * 0.32f);
                    SetMuscle("Right Upper Leg Front-Back", -stride * 0.32f);
                    SetMuscle("Left Lower Leg Stretch", Mathf.Max(0f, -stride) * 0.35f);
                    SetMuscle("Right Lower Leg Stretch", Mathf.Max(0f, stride) * 0.35f);
                    SetMuscle("Left Arm Front-Back", -stride * 0.22f);
                    SetMuscle("Right Arm Front-Back", stride * 0.22f);
                    pose.bodyPosition = neutralBodyPosition + Vector3.up * (Mathf.Abs(stride) * 0.02f);
                    break;
                }
                case AvatarIdleBehaviour.ShortHop:
                {
                    var hop = Mathf.Max(0f, Mathf.Sin(t * Mathf.PI * 3f));
                    pose.bodyPosition = neutralBodyPosition + Vector3.up * (hop * hop * 0.09f);
                    SetMuscle("Left Upper Leg Front-Back", hop * 0.18f);
                    SetMuscle("Right Upper Leg Front-Back", hop * 0.18f);
                    SetMuscle("Left Lower Leg Stretch", -hop * 0.28f);
                    SetMuscle("Right Lower Leg Stretch", -hop * 0.28f);
                    SetMuscle("Left Arm Down-Up", -0.72f);
                    SetMuscle("Right Arm Down-Up", -0.72f);
                    break;
                }
                default:
                    SetMuscle("Left Arm Front-Back", Mathf.Sin(phase * 0.61f) * 0.025f);
                    SetMuscle("Right Arm Front-Back", -Mathf.Sin(phase * 0.61f) * 0.025f);
                    break;
            }
        }

        private void UpdateBehaviourScheduler(SummonState state, float deltaSeconds)
        {
            if (state != SummonState.AvatarActive)
            {
                currentBehaviour = AvatarIdleBehaviour.Idle;
                behaviourElapsed = 0f;
                nextBehaviourDelay = 1.1f;
                if (summonController != null)
                {
                    summonController.SetActiveMotionYaw(0f);
                }
                return;
            }

            if (activeElapsed < 2.4f)
            {
                currentBehaviour = AvatarIdleBehaviour.Celebration;
                return;
            }

            if (currentBehaviour == AvatarIdleBehaviour.Celebration)
            {
                currentBehaviour = AvatarIdleBehaviour.Idle;
                nextBehaviourDelay = 0.75f;
            }

            if (currentBehaviour == AvatarIdleBehaviour.Idle)
            {
                nextBehaviourDelay -= deltaSeconds;
                if (nextBehaviourDelay <= 0f)
                {
                    BeginNextBehaviour();
                }
                return;
            }

            behaviourElapsed += deltaSeconds;
            if (behaviourElapsed >= behaviourDuration)
            {
                if (currentBehaviour == AvatarIdleBehaviour.Wander)
                {
                    roamingOffset = roamTargetOffset;
                    hasRoamedSinceSummon = true;
                }
                previousBehaviour = currentBehaviour;
                currentBehaviour = AvatarIdleBehaviour.Idle;
                behaviourElapsed = 0f;
                nextBehaviourDelay = Mathf.Lerp(0.8f, 2.2f, NextRandom01());
            }
        }

        private void BeginNextBehaviour()
        {
            var roll = NextRandom01() * 8.1f;
            var selected = !hasRoamedSinceSummon ? AvatarIdleBehaviour.Wander :
                roll < 1.45f ? AvatarIdleBehaviour.LookAround :
                roll < 2.9f ? AvatarIdleBehaviour.Wave :
                roll < 4.05f ? AvatarIdleBehaviour.HairAdjust :
                roll < 5.15f ? AvatarIdleBehaviour.Sit :
                roll < 6.5f ? AvatarIdleBehaviour.Stretch : AvatarIdleBehaviour.Wander;
            if (selected == previousBehaviour)
            {
                selected = selected == AvatarIdleBehaviour.ShortHop
                    ? AvatarIdleBehaviour.LookAround
                    : (AvatarIdleBehaviour)((int)selected + 1);
            }

            currentBehaviour = selected;
            behaviourElapsed = 0f;
            if (selected == AvatarIdleBehaviour.Wander)
            {
                roamStartOffset = roamingOffset;
                roamTargetOffset = ChooseScreenRoamingTarget();
                var travelDistance = Vector3.Distance(roamTargetOffset, roamStartOffset);
                roamUsesRun = travelDistance > 2.8f && NextRandom01() > 0.34f;
            }
            behaviourDuration = selected switch
            {
                AvatarIdleBehaviour.LookAround => 3.2f,
                AvatarIdleBehaviour.Wave => 2.8f,
                AvatarIdleBehaviour.HairAdjust => 2.7f,
                AvatarIdleBehaviour.Sit => 4.6f,
                AvatarIdleBehaviour.Stretch => 3.4f,
                AvatarIdleBehaviour.Wander => Mathf.Clamp(
                    Vector3.Distance(roamTargetOffset, roamStartOffset) /
                    (roamUsesRun
                        ? DesktopExperienceConfig.Current.runWorldUnitsPerSecond
                        : DesktopExperienceConfig.Current.walkWorldUnitsPerSecond),
                    3.2f, 8.5f),
                AvatarIdleBehaviour.ShortHop => 2.2f,
                _ => 2f
            };
        }

        private Vector3 ChooseScreenRoamingTarget()
        {
            var camera = Camera.main;
            if (camera == null || summonController == null || Screen.width <= 0)
            {
                return new Vector3(Mathf.Lerp(-3.8f, 1.1f, NextRandom01()), 0f,
                    Mathf.Lerp(-0.18f, 0.18f, NextRandom01()));
            }

            var anchor = summonController.AvatarActivePosition;
            var screenAnchor = camera.WorldToScreenPoint(anchor);
            if (screenAnchor.z <= 0f)
            {
                return roamingOffset;
            }

            // Leave a small safe edge for the avatar silhouette and keep the
            // far-right door area available for the return choreography.
            var config = DesktopExperienceConfig.Current;
            var targetViewportX = Mathf.Lerp(config.roamMinViewportX, config.roamMaxViewportX, NextRandom01());
            var targetDepthOffset = Mathf.Lerp(config.roamNearDepth, config.roamFarDepth, NextRandom01());
            var targetZ = anchor.z + targetDepthOffset;
            var ray = camera.ViewportPointToRay(new Vector3(targetViewportX, 0.5f, 0f));
            if (Mathf.Abs(ray.direction.z) < 0.0001f)
            {
                return roamingOffset;
            }

            var rayDistance = (targetZ - ray.origin.z) / ray.direction.z;
            var targetWorld = ray.GetPoint(rayDistance);
            return new Vector3(targetWorld.x - anchor.x, 0f, targetDepthOffset);
        }

        private void ApplyActiveMotionOffset(SummonState state, bool celebrating)
        {
            if (summonController == null)
            {
                return;
            }

            if (state != SummonState.AvatarActive)
            {
                summonController.SetActiveMotionOffset(Vector3.zero);
                summonController.SetActiveMotionYaw(0f);
                return;
            }

            if (celebrating)
            {
                summonController.SetActiveMotionOffset(Vector3.zero);
                summonController.SetActiveMotionYaw(0f);
                return;
            }

            var t = behaviourDuration > 0f ? Mathf.Clamp01(behaviourElapsed / behaviourDuration) : 0f;
            if (currentBehaviour == AvatarIdleBehaviour.Wander)
            {
                // Constant root speed lets each repeated walk/run cycle cover an
                // equal piece of the route instead of accelerating like a glide.
                var travelT = t;
                summonController.SetActiveMotionOffset(Vector3.Lerp(roamStartOffset, roamTargetOffset, travelT));
                var direction = roamTargetOffset - roamStartOffset;
                direction.y = 0f;
                var travelYaw = direction.sqrMagnitude > 0.0001f
                    ? Mathf.Atan2(-direction.x, -direction.z) * Mathf.Rad2Deg
                    : 0f;
                summonController.SetActiveMotionYaw(travelYaw * Mathf.Sin(t * Mathf.PI));
            }
            else if (currentBehaviour == AvatarIdleBehaviour.ShortHop)
            {
                var hop = Mathf.Max(0f, Mathf.Sin(t * Mathf.PI * 3f));
                summonController.SetActiveMotionOffset(roamingOffset + Vector3.up * (hop * hop * 0.025f));
                summonController.SetActiveMotionYaw(0f);
            }
            else
            {
                var idleBob = Mathf.Sin(Time.unscaledTime * 1.8f) * 0.008f;
                summonController.SetActiveMotionOffset(roamingOffset + Vector3.up * idleBob);
                summonController.SetActiveMotionYaw(0f);
            }
        }

        private float NextRandom01()
        {
            randomState ^= randomState << 13;
            randomState ^= randomState >> 17;
            randomState ^= randomState << 5;
            return (randomState & 0x00FFFFFFu) / 16777216f;
        }

        private void SetMuscle(string muscleName, float value)
        {
            if (muscleIndices.TryGetValue(muscleName, out var index))
            {
                pose.muscles[index] = Mathf.Clamp(value, -1f, 1f);
            }
        }

        private void CacheBlinkBlendShapes()
        {
            foreach (var renderer in GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                var mesh = renderer.sharedMesh;
                if (mesh == null)
                {
                    continue;
                }

                for (var i = 0; i < mesh.blendShapeCount; i++)
                {
                    var shapeName = mesh.GetBlendShapeName(i);
                    if (shapeName.IndexOf("blink", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        shapeName.IndexOf("eye_close", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        shapeName.IndexOf("eye close", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        shapeName.IndexOf("eye_def_c", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        blinkBindings.Add(new BlendShapeBinding(renderer, i));
                    }

                    if (shapeName.IndexOf("smile1", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        smileBindings.Add(new BlendShapeBinding(renderer, i));
                    }

                    if (shapeName.EndsWith("MTH_A", StringComparison.OrdinalIgnoreCase) ||
                        shapeName.Equals("MTH_A", StringComparison.OrdinalIgnoreCase))
                    {
                        mouthOpenBindings.Add(new BlendShapeBinding(renderer, i));
                    }
                }
            }
        }

        private void UpdateBlink(float deltaSeconds)
        {
            blinkCountdown -= deltaSeconds;
            if (blinkElapsed < 0f && blinkCountdown <= 0f)
            {
                blinkElapsed = 0f;
                blinkCountdown = 2.4f + Mathf.Repeat(Time.unscaledTime * 0.39f, 1.9f);
            }

            var weight = 0f;
            if (blinkElapsed >= 0f)
            {
                blinkElapsed += deltaSeconds;
                var normalized = blinkElapsed / 0.18f;
                weight = Mathf.Sin(Mathf.Clamp01(normalized) * Mathf.PI) * 100f;
                if (normalized >= 1f)
                {
                    blinkElapsed = -1f;
                    weight = 0f;
                }
            }

            SetBlinkWeight(weight);
        }

        private void SetBlinkWeight(float weight)
        {
            if (vrmInstance != null && Application.isPlaying)
            {
                vrmInstance.Runtime.Expression.SetWeight(ExpressionKey.Blink, weight / 100f);
                return;
            }

            foreach (var binding in blinkBindings)
            {
                binding.Renderer.SetBlendShapeWeight(binding.Index, weight);
            }
        }

        private void ApplyExpressions(SummonState state, float elapsed)
        {
            if (!Application.isPlaying)
            {
                return;
            }

            var active = state == SummonState.AvatarActive;
            var celebrating = active && elapsed < 2.4f;
            var playful = currentBehaviour == AvatarIdleBehaviour.Wave ||
                          currentBehaviour == AvatarIdleBehaviour.Stretch ||
                          currentBehaviour == AvatarIdleBehaviour.ShortHop;
            var happy = active ? (celebrating ? 0.68f : playful ? 0.52f : 0.24f) : 0f;
            var laugh = celebrating
                ? Mathf.Clamp01(Mathf.Sin(elapsed * 5.2f) * 0.5f + 0.5f) * 0.22f
                : playful ? Mathf.Clamp01(Mathf.Sin(behaviourElapsed * 4.5f)) * 0.18f : 0f;
            var surprised = state == SummonState.AvatarExiting ? 0.22f : 0f;
            if (vrmInstance != null)
            {
                var expression = vrmInstance.Runtime.Expression;
                expression.SetWeight(ExpressionKey.Happy, happy);
                expression.SetWeight(ExpressionKey.Relaxed, active && !celebrating ? 0.22f : 0f);
                expression.SetWeight(ExpressionKey.Surprised, surprised);
                expression.SetWeight(ExpressionKey.Aa, laugh);
                return;
            }

            SetBlendShapeWeights(smileBindings, happy * 100f);
            SetBlendShapeWeights(mouthOpenBindings, laugh * 100f);
        }

        private static void SetBlendShapeWeights(IEnumerable<BlendShapeBinding> bindings, float weight)
        {
            foreach (var binding in bindings)
            {
                binding.Renderer.SetBlendShapeWeight(binding.Index, weight);
            }
        }

        private void ApplyEyeLook()
        {
            if (Mouse.current == null || Screen.width <= 0 || Screen.height <= 0)
            {
                return;
            }

            var pointer = Mouse.current.position.ReadValue();
            var horizontal = Mathf.Clamp(((pointer.x / Screen.width) - 0.5f) * 12f, -6f, 6f);
            var vertical = Mathf.Clamp(((pointer.y / Screen.height) - 0.5f) * -8f, -4f, 4f);
            var look = Quaternion.Euler(vertical, horizontal, 0f);
            if (leftEye != null)
            {
                leftEye.localRotation = leftEyeNeutral * look;
            }

            if (rightEye != null)
            {
                rightEye.localRotation = rightEyeNeutral * look;
            }
        }

        private readonly struct BlendShapeBinding
        {
            public BlendShapeBinding(SkinnedMeshRenderer renderer, int index)
            {
                Renderer = renderer;
                Index = index;
            }

            public SkinnedMeshRenderer Renderer { get; }
            public int Index { get; }
        }
    }
}
