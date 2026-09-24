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
        private sealed class ChatterLine
        {
            public ChatterLine(string clipName, string language, string spokenText)
            {
                ClipName = clipName;
                Language = language;
                SpokenText = spokenText;
            }

            public string ClipName { get; }
            public string Language { get; }
            public string SpokenText { get; }
        }

        // Audio and displayed text deliberately share one language. This record is
        // ready for future interactive dialogue without a separate subtitle index.
        private static readonly ChatterLine[] ChatterLines =
        {
            new ChatterLine("voice_chatter_vi_1", "ja-JP",
                "ねえ、今日もよく頑張ったね。少しくらい休んでもいいんだよ。"),
            new ChatterLine("voice_chatter_vi_2", "ja-JP",
                "ゆっくり歩くことは、後ろに下がることじゃないよ。諦めなければ大丈夫。"),
            new ChatterLine("voice_chatter_vi_3", "ja-JP",
                "完璧じゃなくてもいいの。笑顔になれることが一つあれば、それで素敵な一日だよ。"),
            new ChatterLine("voice_chatter_vi_4", "ja-JP",
                "疲れたら、ゆっくり深呼吸してね。私はここにいるよ。"),
            new ChatterLine("voice_chatter_jp_1", "ja-JP", "今日も一緒に、のんびり頑張ろうね。"),
            new ChatterLine("voice_chatter_jp_2", "ja-JP", "小さな一歩でも、前に進めば素敵だよ。"),
            new ChatterLine("voice_chatter_jp_3", "ja-JP",
                "らん、ららん。ふふっ、今日もいい日になりますように。"),
            new ChatterLine("voice_chatter_jp_4", "ja-JP",
                "あなたが笑うと、私も嬉しくなるの。だから、笑って？"),
            new ChatterLine("voice_chatter_jp_5", "ja-JP",
                "無理しすぎないでね。あなたのペースで大丈夫だよ。"),
            new ChatterLine("voice_chatter_jp_6", "ja-JP", "窓の外を見て。空がとってもきれいだよ。"),
            new ChatterLine("voice_chatter_jp_7", "ja-JP",
                "集中できていて偉いね。あと少し、一緒に頑張ろう。"),
            new ChatterLine("voice_chatter_jp_8", "ja-JP", "お水、ちゃんと飲んだ？休憩も忘れないでね。"),
            new ChatterLine("voice_chatter_jp_9", "ja-JP",
                "今日できたことを一つ、思い出してみよう。それだけでも十分だよ。"),
            new ChatterLine("voice_chatter_jp_10", "ja-JP",
                "ねえ、少しお話ししない？あなたのこと、もっと知りたいな。"),
            new ChatterLine("voice_chatter_jp_11", "ja-JP",
                "おかえり。会えて嬉しいよ。今日もそばにいるね。"),
            new ChatterLine("voice_chatter_jp_12", "ja-JP",
                "大丈夫。うまくいかない日だって、明日につながっているよ。")
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
        private float subtitleRevealSeconds;
        private Font bubbleFont;
        private Texture2D activeBubbleTexture;
        private Texture2D bubbleTailTexture;
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
        private AudioClip avatarClickReaction;

        public static DesktopAudioController Instance { get; private set; }
        public static int ChatterLineCount => ChatterLines.Length;

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
            if (activeBubbleTexture != null)
            {
                Destroy(activeBubbleTexture);
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

        public void PlayAvatarClickReaction()
        {
            if (avatarClickReaction == null)
            {
                return;
            }

            voiceSource.Stop();
            Play(voiceSource, avatarClickReaction, 0.92f, true);
            ShowSpeechBubble("Ư… ư… đau em!", avatarClickReaction);
            chatterCountdown = Random.Range(
                DesktopExperienceConfig.Current.chatterMinSeconds,
                DesktopExperienceConfig.Current.chatterMaxSeconds);
            Debug.Log("[DesktopAudio] Vietnamese click reaction: Ư… ư… đau em!");
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
            avatarClickReaction = Load("voice_click_hurt_vi");
            var clipNames = new string[ChatterLines.Length];
            for (var index = 0; index < ChatterLines.Length; index++)
            {
                clipNames[index] = ChatterLines[index].ClipName;
            }
            chatterVoices = LoadMany(clipNames);
        }

        private void ShowSpeechBubble(int chatterIndex, AudioClip clip)
        {
            if (!DesktopExperienceConfig.Current.speechBubbleEnabled ||
                chatterIndex < 0 || chatterIndex >= ChatterLines.Length)
            {
                return;
            }

            var line = ChatterLines[chatterIndex];
            ShowSpeechBubble(line.SpokenText, clip);
            Debug.Log($"[DesktopAudio] {line.Language}: {line.SpokenText}");
        }

        private void ShowSpeechBubble(string displayedText, AudioClip clip)
        {
            if (!DesktopExperienceConfig.Current.speechBubbleEnabled ||
                string.IsNullOrWhiteSpace(displayedText))
            {
                return;
            }

            activeSubtitle = displayedText;
            subtitleStartedAt = Time.unscaledTime;
            subtitleRevealSeconds = clip != null && clip.length > 0.1f
                ? Mathf.Max(0.1f, clip.length * 0.92f)
                : activeSubtitle.Length /
                  DesktopExperienceConfig.Current.speechTextCharactersPerSecond;
            subtitleHideAt = subtitleStartedAt + Mathf.Max(
                clip != null ? clip.length + 1f : 0f, subtitleRevealSeconds + 0.8f);
            if (activeBubbleTexture != null)
            {
                Destroy(activeBubbleTexture);
            }
            var cloudSeed = StableTextHash(activeSubtitle);
            activeBubbleTexture = CreateSmoothBubbleTexture(320, 180, cloudSeed, 5);
        }

        private void CreateSpeechBubbleAssets()
        {
            bubbleFont = Font.CreateDynamicFontFromOSFont(
                new[] { "Yu Gothic UI", "Meiryo UI", "Segoe UI" }, 16);
            bubbleTailTexture = CreateBubbleTailTexture(28, 18, 3);
            bubbleTextStyle = new GUIStyle
            {
                font = bubbleFont,
                fontSize = 12,
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
            bubbleTextStyle.fontSize = Mathf.Max(6, Mathf.RoundToInt(12f * scale));
            var fullContent = new GUIContent(activeSubtitle);
            var longestLineWidth = 0f;
            foreach (var line in activeSubtitle.Split('\n'))
            {
                longestLineWidth = Mathf.Max(longestLineWidth,
                    bubbleTextStyle.CalcSize(new GUIContent(line)).x);
            }
            var textWidth = Mathf.Clamp(longestLineWidth, 105f * scale, 220f * scale);
            var horizontalPadding = 32f * scale;
            var verticalPadding = 23f * scale;
            var bubbleWidth = textWidth + horizontalPadding * 2f;
            var textHeight = bubbleTextStyle.CalcHeight(fullContent, textWidth);
            var bubbleHeight = textHeight + verticalPadding * 2f;
            var preferredY = head.y - bubbleHeight - 20f * scale;
            var bubbleAbove = preferredY >= 12f;
            var bubbleY = bubbleAbove ? preferredY : head.y + 20f * scale;
            var bubbleX = Mathf.Clamp(head.x - bubbleWidth * 0.5f, 12f, Screen.width - bubbleWidth - 12f);
            bubbleY = Mathf.Clamp(bubbleY, 12f, Screen.height - bubbleHeight - 12f);
            var bubbleRect = new Rect(bubbleX, bubbleY, bubbleWidth, bubbleHeight);
            var bubbleTexture = activeBubbleTexture;
            if (bubbleTexture == null)
            {
                return;
            }

            var previousColor = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.24f);
            GUI.DrawTexture(new Rect(bubbleRect.x + 3f * scale, bubbleRect.y + 4f * scale,
                bubbleRect.width, bubbleRect.height), bubbleTexture, ScaleMode.StretchToFill, true);
            GUI.color = Color.white;
            GUI.DrawTexture(bubbleRect, bubbleTexture, ScaleMode.StretchToFill, true);

            var tailWidth = 28f * scale;
            var tailHeight = 18f * scale;
            var tailX = Mathf.Clamp(head.x - tailWidth * 0.5f,
                bubbleRect.x + 20f * scale, bubbleRect.xMax - 48f * scale);
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
                Mathf.FloorToInt(activeSubtitle.Length *
                    Mathf.Clamp01(elapsed / subtitleRevealSeconds)), 0, activeSubtitle.Length);
            var visibleText = activeSubtitle.Substring(0, visibleCharacters);
            if (visibleCharacters < activeSubtitle.Length && Mathf.FloorToInt(elapsed * 4f) % 2 == 0)
            {
                visibleText += "▌";
            }
            var textRect = new Rect(
                bubbleRect.x + horizontalPadding, bubbleRect.y + verticalPadding,
                textWidth, textHeight);
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

        private static Texture2D CreateSmoothBubbleTexture(
            int width, int height, int seed, int border)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                name = $"Anime Smooth Speech Bubble {seed}",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.DontSave
            };
            var pixels = new Color32[width * height];
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    var outer = IsInsideSmoothBubble(x, y, width, height, seed, 0);
                    var inner = IsInsideSmoothBubble(x, y, width, height, seed, border);
                    pixels[y * width + x] = !outer
                        ? new Color32(0, 0, 0, 0)
                        : inner ? new Color32(255, 255, 255, 250) : new Color32(24, 20, 30, 255);
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return texture;
        }

        private static bool IsInsideSmoothBubble(
            int x, int y, int width, int height, int seed, int inset)
        {
            var centerX = width * 0.5f;
            var centerY = height * 0.5f;
            var radiusX = width * 0.455f - inset;
            var radiusY = height * 0.405f - inset;
            var normalizedX = (x - centerX) / radiusX;
            var normalizedY = (y - centerY) / radiusY;
            var angle = Mathf.Atan2(normalizedY, normalizedX);
            var phaseA = Hash01(seed) * Mathf.PI * 2f;
            var phaseB = Hash01(seed + 911) * Mathf.PI * 2f;
            var softVariation = Mathf.Sin(angle * 3f + phaseA) * 0.018f +
                                Mathf.Sin(angle * 5f + phaseB) * 0.009f;
            var radialDistance = Mathf.Sqrt(
                normalizedX * normalizedX + normalizedY * normalizedY);
            return radialDistance <= 1f + softVariation;
        }

        private static int StableTextHash(string text)
        {
            unchecked
            {
                var hash = 23;
                for (var index = 0; index < text.Length; index++)
                {
                    hash = hash * 31 + text[index];
                }
                return hash;
            }
        }

        private static float Hash01(int value)
        {
            var hash = unchecked((uint)value * 747796405u + 2891336453u);
            hash = ((hash >> ((int)(hash >> 28) + 4)) ^ hash) * 277803737u;
            hash = (hash >> 22) ^ hash;
            return (hash & 0x00ffffffu) / 16777215f;
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
