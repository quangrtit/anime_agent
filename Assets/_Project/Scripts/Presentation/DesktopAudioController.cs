using AnimeAssistant.Domain;
using UnityEngine;

namespace AnimeAssistant.Presentation
{
    /// <summary>
    /// Lightweight 2D sound layer for the desktop pet. Clips live in Resources so
    /// the same audio works with the embedded avatar and any runtime-loaded VRM.
    /// </summary>
    [DefaultExecutionOrder(9000)]
    public sealed class DesktopAudioController : MonoBehaviour
    {
        private const string AudioRoot = "Audio/DesktopAssistant/";

        private AudioSource interfaceSource;
        private AudioSource worldSource;
        private AudioSource voiceSource;
        private AudioSource footstepSource;
        private GreyboxSummonController controller;
        private HumanoidAvatarMotion motion;
        private SummonState previousState = SummonState.DoorClosed;
        private AvatarIdleBehaviour previousBehaviour = AvatarIdleBehaviour.Idle;
        private float footstepCountdown;
        private int footstepIndex;
        private int voiceIndex;
        private int lastChatterIndex = -1;
        private float chatterCountdown;

        private AudioClip buttonPress;
        private AudioClip buttonElectric;
        private AudioClip doorOpen;
        private AudioClip doorClose;
        private AudioClip doorLatch;
        private AudioClip portalChime;
        private AudioClip portalImpact;
        private AudioClip[] footsteps;
        private AudioClip[] cloth;
        private AudioClip[] jumpVoices;
        private AudioClip[] cheerVoices;
        private AudioClip[] chatterVoices;

        public static DesktopAudioController Instance { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void CreateRuntimeAudio()
        {
            if (Instance != null)
            {
                return;
            }

            var host = new GameObject("Desktop Assistant Audio");
            DontDestroyOnLoad(host);
            host.AddComponent<DesktopAudioController>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            interfaceSource = CreateSource("Interface Audio");
            worldSource = CreateSource("World Audio");
            voiceSource = CreateSource("Avatar Voice");
            footstepSource = CreateSource("Footsteps");
            LoadClips();
            FindSceneActors();
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private void Update()
        {
            if (controller == null || motion == null)
            {
                FindSceneActors();
            }

            if (controller == null)
            {
                return;
            }

            var state = controller.State;
            if (state != previousState)
            {
                PlayStateTransition(state);
                previousState = state;
                footstepCountdown = 0.04f;
                chatterCountdown = state == SummonState.AvatarActive
                    ? Random.Range(6f, 10f)
                    : 0f;
            }

            if (motion != null && motion.CurrentBehaviour != previousBehaviour)
            {
                PlayBehaviour(motion.CurrentBehaviour);
                previousBehaviour = motion.CurrentBehaviour;
                footstepCountdown = 0.04f;
            }

            UpdateFootsteps(state);
            UpdateChatter(state);
        }

        public void PlayButtonPress()
        {
            Play(interfaceSource, buttonPress, 0.82f);
            Play(interfaceSource, buttonElectric, 0.28f);
        }

        private void PlayStateTransition(SummonState state)
        {
            switch (state)
            {
                case SummonState.DoorOpening:
                    Play(worldSource, doorOpen, 0.62f);
                    Play(interfaceSource, buttonElectric, 0.22f);
                    break;
                case SummonState.AvatarExiting:
                    Play(worldSource, portalImpact, 0.48f);
                    PlayVoice(cheerVoices, 0.32f);
                    break;
                case SummonState.AvatarActive:
                    Play(worldSource, portalChime, 0.28f);
                    break;
                case SummonState.AvatarReturning:
                    PlayRandom(worldSource, cloth, 0.24f);
                    PlayVoice(cheerVoices, 0.20f);
                    break;
                case SummonState.AvatarEntering:
                    Play(worldSource, portalImpact, 0.30f);
                    break;
                case SummonState.DoorClosing:
                    Play(worldSource, doorClose, 0.60f);
                    break;
                case SummonState.DoorClosed:
                    Play(worldSource, doorLatch, 0.58f);
                    break;
            }
        }

        private void PlayBehaviour(AvatarIdleBehaviour behaviour)
        {
            switch (behaviour)
            {
                case AvatarIdleBehaviour.Wave:
                    PlayRandom(worldSource, cloth, 0.20f);
                    PlayVoice(cheerVoices, 0.18f);
                    break;
                case AvatarIdleBehaviour.HairAdjust:
                case AvatarIdleBehaviour.LookAround:
                    PlayRandom(worldSource, cloth, 0.14f);
                    break;
                case AvatarIdleBehaviour.Sit:
                    PlayRandom(worldSource, cloth, 0.24f);
                    break;
                case AvatarIdleBehaviour.Stretch:
                case AvatarIdleBehaviour.ShortHop:
                    PlayRandom(worldSource, cloth, 0.18f);
                    PlayVoice(jumpVoices, 0.26f);
                    break;
            }
        }

        private void UpdateFootsteps(SummonState state)
        {
            var scriptedTravel = state == SummonState.AvatarExiting ||
                                 state == SummonState.AvatarReturning ||
                                 state == SummonState.AvatarEntering;
            var roaming = state == SummonState.AvatarActive && motion != null &&
                          motion.CurrentBehaviour == AvatarIdleBehaviour.Wander;
            if (!scriptedTravel && !roaming)
            {
                footstepCountdown = 0f;
                return;
            }

            footstepCountdown -= Time.unscaledDeltaTime;
            if (footstepCountdown > 0f || footsteps.Length == 0)
            {
                return;
            }

            var running = roaming && motion.IsRoamRunning;
            footstepCountdown = running ? 0.28f : 0.43f;
            var clip = footsteps[footstepIndex++ % footsteps.Length];
            Play(footstepSource, clip, running ? 0.25f : 0.19f);
        }

        private void UpdateChatter(SummonState state)
        {
            var config = DesktopExperienceConfig.Current;
            if (!config.chatterEnabled || state != SummonState.AvatarActive ||
                chatterVoices == null || chatterVoices.Length == 0)
            {
                return;
            }

            // Let spoken lines land while the character is calm; travel, hops and
            // celebration already have their own action sounds.
            if (voiceSource.isPlaying || motion == null ||
                motion.CurrentBehaviour == AvatarIdleBehaviour.Wander ||
                motion.CurrentBehaviour == AvatarIdleBehaviour.ShortHop ||
                motion.CurrentBehaviour == AvatarIdleBehaviour.Celebration)
            {
                return;
            }

            chatterCountdown -= Time.unscaledDeltaTime;
            if (chatterCountdown > 0f)
            {
                return;
            }

            var index = Random.Range(0, chatterVoices.Length);
            if (chatterVoices.Length > 1 && index == lastChatterIndex)
            {
                index = (index + 1) % chatterVoices.Length;
            }
            lastChatterIndex = index;
            Play(voiceSource, chatterVoices[index], config.chatterVolume, true);
            Debug.Log($"[DesktopAudio] Chatter '{chatterVoices[index].name}'.");
            chatterCountdown = Random.Range(config.chatterMinSeconds, config.chatterMaxSeconds);
        }

        private void FindSceneActors()
        {
            controller = FindFirstObjectByType<GreyboxSummonController>();
            motion = FindFirstObjectByType<HumanoidAvatarMotion>();
            previousState = controller != null ? controller.State : SummonState.DoorClosed;
            previousBehaviour = motion != null ? motion.CurrentBehaviour : AvatarIdleBehaviour.Idle;
        }

        private AudioSource CreateSource(string sourceName)
        {
            var sourceObject = new GameObject(sourceName);
            sourceObject.transform.SetParent(transform, false);
            var source = sourceObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = false;
            source.spatialBlend = 0f;
            source.ignoreListenerPause = true;
            return source;
        }

        private void LoadClips()
        {
            buttonPress = Load("button_press");
            buttonElectric = Load("button_electric");
            doorOpen = Load("door_open");
            doorClose = Load("door_close");
            doorLatch = Load("door_latch");
            portalChime = Load("portal_chime");
            portalImpact = Load("portal_impact");
            footsteps = LoadMany("footstep_1", "footstep_2", "footstep_3", "footstep_4");
            cloth = LoadMany("cloth_1", "cloth_2");
            jumpVoices = LoadMany("voice_jump_1", "voice_jump_2");
            cheerVoices = LoadMany("voice_cheer_1", "voice_cheer_2");
            chatterVoices = LoadMany(
                "voice_chatter_vi_1", "voice_chatter_vi_2",
                "voice_chatter_vi_3", "voice_chatter_vi_4",
                "voice_chatter_jp_1", "voice_chatter_jp_2",
                "voice_chatter_jp_3", "voice_chatter_jp_4");
        }

        private static AudioClip Load(string clipName)
        {
            var clip = Resources.Load<AudioClip>(AudioRoot + clipName);
            if (clip == null)
            {
                Debug.LogWarning($"[DesktopAudio] Missing clip '{clipName}'.");
            }
            return clip;
        }

        private static AudioClip[] LoadMany(params string[] names)
        {
            var result = new AudioClip[names.Length];
            for (var index = 0; index < names.Length; index++)
            {
                result[index] = Load(names[index]);
            }
            return result;
        }

        private void PlayVoice(AudioClip[] clips, float volume)
        {
            if (clips == null || clips.Length == 0)
            {
                return;
            }
            Play(voiceSource, clips[voiceIndex++ % clips.Length], volume, true);
        }

        private void PlayRandom(AudioSource source, AudioClip[] clips, float volume)
        {
            if (clips == null || clips.Length == 0)
            {
                return;
            }
            var index = (footstepIndex + voiceIndex) % clips.Length;
            Play(source, clips[index], volume);
        }

        private static void Play(AudioSource source, AudioClip clip, float volume, bool voice = false)
        {
            if (source == null || clip == null)
            {
                return;
            }
            var config = DesktopExperienceConfig.Current;
            var category = voice ? config.voiceVolume : config.effectsVolume;
            source.PlayOneShot(clip, Mathf.Clamp01(volume * category * config.masterVolume));
        }
    }
}
