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
        private static readonly string[] ChatterSubtitles =
        {
            "Này, hôm nay bạn đã cố gắng nhiều rồi.\nNghỉ một chút cũng không sao đâu.",
            "Đi chậm không có nghĩa là đi lùi.\nChỉ cần đừng bỏ cuộc nhé!",
            "Một ngày đẹp không cần hoàn hảo.\nChỉ cần có điều khiến bạn mỉm cười.",
            "Nếu thấy mệt, hãy hít một hơi thật sâu.\nMình vẫn ở đây mà.",
            "Hôm nay chúng ta cùng thong thả cố gắng nhé!",
            "Dù chỉ là một bước nhỏ, tiến về phía trước đã rất tuyệt rồi.",
            "♪ La la... Mong hôm nay cũng là một ngày thật đẹp! ♪",
            "Khi bạn cười, mình cũng thấy vui.\nVậy nên... cười lên nhé?"
        };

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
        private string activeSubtitle;
        private float subtitleStartedAt;
        private float subtitleHideAt;
        private float subtitleCharactersPerSecond;
        private Font bubbleFont;
        private Texture2D bubbleTexture;
        private Texture2D bubbleTailTexture;
        private GUIStyle bubbleFrameStyle;
        private GUIStyle bubbleTextStyle;

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
        public static int ChatterLineCount => ChatterSubtitles.Length;

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
            CreateSpeechBubbleAssets();
            FindSceneActors();
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
            if (bubbleTexture != null)
            {
                Destroy(bubbleTexture);
            }
            if (bubbleTailTexture != null)
            {
                Destroy(bubbleTailTexture);
            }
            if (bubbleFont != null)
            {
                Destroy(bubbleFont);
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
                if (state != SummonState.AvatarActive)
                {
                    activeSubtitle = null;
                }
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
            ShowSpeechBubble(index, chatterVoices[index]);
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

        private void ShowSpeechBubble(int chatterIndex, AudioClip clip)
        {
            if (!DesktopExperienceConfig.Current.speechBubbleEnabled ||
                chatterIndex < 0 || chatterIndex >= ChatterSubtitles.Length)
            {
                return;
            }

            activeSubtitle = ChatterSubtitles[chatterIndex];
            subtitleStartedAt = Time.unscaledTime;
            var revealSeconds = Mathf.Clamp(clip.length * 0.72f, 1.4f, 4.8f);
            subtitleCharactersPerSecond = Mathf.Max(
                DesktopExperienceConfig.Current.speechTextCharactersPerSecond,
                activeSubtitle.Length / revealSeconds);
            subtitleHideAt = subtitleStartedAt + Mathf.Max(clip.length + 1.25f, revealSeconds + 1f);
        }

        private void CreateSpeechBubbleAssets()
        {
            bubbleFont = Font.CreateDynamicFontFromOSFont(
                new[] { "Yu Gothic UI", "Meiryo UI", "Segoe UI" }, 22);
            bubbleTexture = CreateRoundedBubbleTexture(64, 64, 15, 3);
            bubbleTailTexture = CreateBubbleTailTexture(40, 28, 3);
            bubbleFrameStyle = new GUIStyle
            {
                normal = { background = bubbleTexture },
                border = new RectOffset(18, 18, 18, 18),
                padding = new RectOffset(22, 22, 15, 17)
            };
            bubbleTextStyle = new GUIStyle
            {
                font = bubbleFont,
                fontSize = 20,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                wordWrap = true,
                richText = false,
                normal = { textColor = new Color(0.07f, 0.065f, 0.09f, 1f) }
            };
        }

        private void OnGUI()
        {
            var config = DesktopExperienceConfig.Current;
            if (!config.speechBubbleEnabled || string.IsNullOrEmpty(activeSubtitle) ||
                Time.unscaledTime >= subtitleHideAt || controller == null ||
                controller.State != SummonState.AvatarActive || motion == null ||
                !TryGetAvatarHeadScreenPoint(out var head))
            {
                return;
            }

            GUI.depth = -1000;
            var scale = config.speechBubbleScale;
            var bubbleWidth = 360f * scale;
            bubbleTextStyle.fontSize = Mathf.RoundToInt(20f * scale);
            var fullContent = new GUIContent(activeSubtitle);
            var textHeight = bubbleTextStyle.CalcHeight(fullContent, bubbleWidth - 44f * scale);
            var bubbleHeight = Mathf.Clamp(textHeight + 34f * scale, 82f * scale, 156f * scale);
            var preferredY = head.y - bubbleHeight - 30f * scale;
            var bubbleAbove = preferredY >= 12f;
            var bubbleY = bubbleAbove ? preferredY : head.y + 30f * scale;
            var bubbleX = Mathf.Clamp(head.x - bubbleWidth * 0.5f, 12f, Screen.width - bubbleWidth - 12f);
            bubbleY = Mathf.Clamp(bubbleY, 12f, Screen.height - bubbleHeight - 12f);
            var bubbleRect = new Rect(bubbleX, bubbleY, bubbleWidth, bubbleHeight);

            var previousColor = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.24f);
            GUI.Box(new Rect(bubbleRect.x + 5f, bubbleRect.y + 6f,
                bubbleRect.width, bubbleRect.height), GUIContent.none, bubbleFrameStyle);
            GUI.color = Color.white;
            GUI.Box(bubbleRect, GUIContent.none, bubbleFrameStyle);

            var tailWidth = 40f * scale;
            var tailHeight = 28f * scale;
            var tailX = Mathf.Clamp(head.x - tailWidth * 0.5f,
                bubbleRect.x + 24f * scale, bubbleRect.xMax - 64f * scale);
            var tailRect = bubbleAbove
                ? new Rect(tailX, bubbleRect.yMax - 3f, tailWidth, tailHeight)
                : new Rect(tailX, bubbleRect.y - tailHeight + 3f, tailWidth, tailHeight);
            if (bubbleAbove)
            {
                GUI.DrawTextureWithTexCoords(tailRect, bubbleTailTexture,
                    new Rect(0f, 1f, 1f, -1f), true);
            }
            else
            {
                GUI.DrawTexture(tailRect, bubbleTailTexture, ScaleMode.StretchToFill, true);
            }

            var elapsed = Mathf.Max(0f, Time.unscaledTime - subtitleStartedAt);
            var visibleCharacters = Mathf.Clamp(
                Mathf.FloorToInt(elapsed * subtitleCharactersPerSecond), 0, activeSubtitle.Length);
            var visibleText = activeSubtitle.Substring(0, visibleCharacters);
            if (visibleCharacters < activeSubtitle.Length && Mathf.FloorToInt(elapsed * 4f) % 2 == 0)
            {
                visibleText += "▌";
            }
            var textRect = new Rect(
                bubbleRect.x + 22f * scale, bubbleRect.y + 13f * scale,
                bubbleRect.width - 44f * scale, bubbleRect.height - 28f * scale);
            GUI.Label(textRect, visibleText, bubbleTextStyle);
            GUI.color = previousColor;
        }

        private bool TryGetAvatarHeadScreenPoint(out Vector2 point)
        {
            point = default;
            var camera = Camera.main;
            if (camera == null)
            {
                return false;
            }

            var found = false;
            var avatarBounds = default(Bounds);
            foreach (var renderer in motion.GetComponentsInChildren<Renderer>(true))
            {
                if (!renderer.enabled || !renderer.gameObject.activeInHierarchy ||
                    renderer is ParticleSystemRenderer)
                {
                    continue;
                }
                if (!found)
                {
                    avatarBounds = renderer.bounds;
                    found = true;
                }
                else
                {
                    avatarBounds.Encapsulate(renderer.bounds);
                }
            }
            if (!found)
            {
                return false;
            }

            var world = new Vector3(avatarBounds.center.x,
                avatarBounds.max.y + avatarBounds.size.y * 0.08f, avatarBounds.center.z);
            var screen = camera.WorldToScreenPoint(world);
            if (screen.z <= 0f)
            {
                return false;
            }
            point = new Vector2(screen.x, Screen.height - screen.y);
            return true;
        }

        private static Texture2D CreateRoundedBubbleTexture(int width, int height, int radius, int border)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                name = "Anime Speech Bubble",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.DontSave
            };
            var pixels = new Color32[width * height];
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    var outer = IsInsideRoundedRect(x, y, width, height, radius, 0);
                    var inner = IsInsideRoundedRect(x, y, width, height, radius, border);
                    pixels[y * width + x] = !outer
                        ? new Color32(0, 0, 0, 0)
                        : inner ? new Color32(255, 255, 255, 250) : new Color32(24, 20, 30, 255);
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return texture;
        }

        private static bool IsInsideRoundedRect(int x, int y, int width, int height, int radius, int inset)
        {
            var left = inset;
            var right = width - 1 - inset;
            var bottom = inset;
            var top = height - 1 - inset;
            if (x < left || x > right || y < bottom || y > top)
            {
                return false;
            }
            var cornerRadius = Mathf.Max(1, radius - inset);
            var centerX = Mathf.Clamp(x, left + cornerRadius, right - cornerRadius);
            var centerY = Mathf.Clamp(y, bottom + cornerRadius, top - cornerRadius);
            var dx = x - centerX;
            var dy = y - centerY;
            return dx * dx + dy * dy <= cornerRadius * cornerRadius;
        }

        private static Texture2D CreateBubbleTailTexture(int width, int height, int border)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                name = "Anime Speech Bubble Tail",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.DontSave
            };
            var pixels = new Color32[width * height];
            for (var y = 0; y < height; y++)
            {
                var normalized = y / (float)(height - 1);
                var halfWidth = (1f - normalized) * width * 0.5f;
                var distance = 0f;
                for (var x = 0; x < width; x++)
                {
                    distance = Mathf.Abs(x - (width - 1) * 0.5f);
                    var inside = distance <= halfWidth;
                    var innerHalfWidth = Mathf.Max(0f, halfWidth - border);
                    var inner = y < height - border && distance <= innerHalfWidth;
                    pixels[y * width + x] = !inside
                        ? new Color32(0, 0, 0, 0)
                        : inner ? new Color32(255, 255, 255, 250) : new Color32(24, 20, 30, 255);
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return texture;
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
