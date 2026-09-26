using System;
using System.Collections.Generic;
using AnimeAssistant.Domain;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace AnimeAssistant.Presentation
{
    /// <summary>
    /// The companion "brain": schedules all personality features — jealousy,
    /// window snooping, the biological sleep clock, tsundere/yandere/kuudere
    /// personalities, the diary booklet, pomodoro watch, reminders, festivals
    /// and the "return to home world" farewell. Bootstrapped from code (no
    /// scene edit) and poll-only, mirroring DesktopAudioController.
    /// </summary>
    [DefaultExecutionOrder(9100)]
    public sealed class CompanionDirector : MonoBehaviour
    {
        public static CompanionDirector Instance { get; private set; }

        private const float SayQueueDelay = 4.5f;

        private GreyboxSummonController controller;
        private HumanoidAvatarMotion motion;
        private SummonVfxController vfx;
        private SummonState previousState = SummonState.DoorClosed;

        private CompanionSettings settings;
        private CompanionProfile profile;
        private List<ReminderEntry> reminders;
        private readonly PomodoroMachine pomodoro = new();
        private readonly System.Random random = new();

        private double summonStartReal;
        private double lastSummonReal = double.NegativeInfinity;
        private float activeMinuteAccumulator;
        private float settingsCheckCountdown;
        private float jealousyCheckCountdown;
        private float reminderCheckCountdown;
        private float sleepyLineCountdown;
        private float lateNightLineCountdown;
        private float snoopingCountdown = 240f;
        private float autosaveCountdown;
        private DateTime settingsStamp;
        private DateTime remindersStamp;
        private bool altWasDown;
        private bool altPWasDown;
        private bool altDWasDown;
        private bool altNWasDown;
        private bool clickReactionWasActive;
        private readonly List<double> recentClickTimes = new();
        private readonly Queue<(string key, object[] args, Func<string> provider)> sayQueue = new();
        private float sayQueueCountdown;
        private DateTime jealousyCooldownUntil = DateTime.MinValue;
        private DateTime snoopingCooldownUntil = DateTime.MinValue;
        private DateTime clickSpamCooldownUntil = DateTime.MinValue;

        private FestivalPetals currentPetals = FestivalPetals.None;
        private CompanionFestival festivalToday;

        private readonly AudioMeterProbe musicProbe = new();
        private IsekaiPortalBackdrop backdrop;
        private AudioClip knockClip;
        private AudioSource effectSource;
        private GameObject giftIcon;
        private double giftIconHideAt;
        private float danceSilence;
        private bool manualDance;
        private bool forceKnockConsumed;
        private float knockCheckCountdown = 120f;
        internal string LiveTitle = "";
        internal float LiveBpm = 128f;
        internal string LiveMove = "";

        // Overlay state consumed by CompanionOverlayUI.
        internal string DiaryText = "";
        internal double DiaryHideAtReal;
        internal double DiaryOpenReal;
        internal string ReminderText = "";
        internal double ReminderHideAtReal;
        internal double YandereVignetteHideAtReal;

        internal PomodoroMachine Pomodoro => pomodoro;

        private bool CompanionActive =>
            controller != null && controller.State == SummonState.AvatarActive;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void CreateRuntimeDirector()
        {
            if (Instance != null)
            {
                return;
            }

            var host = new GameObject("Companion Director");
            DontDestroyOnLoad(host);
            host.AddComponent<CompanionDirector>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            settings = CompanionStore.LoadSettings();
            profile = CompanionStore.LoadProfile();
            reminders = CompanionStore.LoadReminders();
            settingsStamp = CompanionStore.GetUtcLastWrite(CompanionStore.SettingsPath);
            remindersStamp = CompanionStore.GetUtcLastWrite(CompanionStore.RemindersPath);
            profile.RegisterSessionStart(DateTime.UtcNow);
            var nowUtc = DateTime.UtcNow;
            CompanionStore.SaveProfile(profile);
            LastProfileStamp = CompanionStore.GetUtcLastWrite(CompanionStore.ProfilePath);
            autosaveCountdown = 90f;
            knockClip = ProceduralAudio.CreateKnockClip();
            effectSource = gameObject.AddComponent<AudioSource>();
            effectSource.playOnAwake = false;
            effectSource.spatialBlend = 0f;
            Debug.Log($"[Companion] Director ready — personality={profile.personality}, " +
                      $"daysTogether={profile.DaysTogether(nowUtc)}.");
        }

        private void Update()
        {
            if (Instance == null || !settings.enabled)
            {
                return;
            }

            FindSceneActors();
            PollHotReload();

            if (!settings.enabled)
            {
                return;
            }

            PollHotkeys();
            DrainSayQueue();

            var state = controller != null ? controller.State : SummonState.DoorClosed;
            if (state != previousState)
            {
                if (state == SummonState.AvatarActive)
                {
                    OnSummoned();
                }
                else if (previousState == SummonState.AvatarActive)
                {
                    OnDismissed();
                }
                previousState = state;
            }

            if (CompanionActive)
            {
                UpdateActiveSystems(Time.unscaledDeltaTime);
            }

            // The pomodoro keeps counting even while she is behind the door;
            // announcements simply wait for the next summon.
            if (settings.pomodoroEnabled)
            {
                UpdatePomodoro();
            }

            if (autosaveCountdown > 0f)
            {
                autosaveCountdown -= Time.unscaledDeltaTime;
                if (autosaveCountdown <= 0f)
                {
                    SaveProfileNow();
                }
            }
        }

        private void FindSceneActors()
        {
            if (controller == null)
            {
                controller = FindFirstObjectByType<GreyboxSummonController>();
            }
            if (motion == null)
            {
                motion = FindFirstObjectByType<HumanoidAvatarMotion>();
            }
            if (vfx == null)
            {
                vfx = FindFirstObjectByType<SummonVfxController>();
            }
        }

        private void OnSummoned()
        {
            summonStartReal = Time.realtimeSinceStartupAsDouble;
            activeMinuteAccumulator = 0f;
            festivalToday = CompanionFestival.ForDate(DateTime.Today);
            EnsureFestivalPetals();

            var nowUtc = DateTime.UtcNow;
            var daysSinceLastSeen = profile.DaysSinceLastSeen(nowUtc);
            if (settings.returnHomeAfterDays <= daysSinceLastSeen && profile.ShouldReturnHome(nowUtc))
            {
                TriggerReturnHome(daysSinceLastSeen);
                lastSummonReal = summonStartReal;
                return;
            }

            if (daysSinceLastSeen >= 2)
            {
                EnqueueSay("welcome_back", daysSinceLastSeen);
            }
            else
            {
                EnqueueSay("greeting");
            }

            if (profile.IsAnniversaryDay(nowUtc))
            {
                var days = profile.DaysTogether(nowUtc);
                profile.MarkAnniversary(days);
                profile.AddDiary(DateTime.Now, $"Hôm nay tròn {days} ngày anh cài em.");
                SaveProfileSoon();
                EnqueueSay("anniversary", days);
            }

            if (festivalToday != null && profile.lastFestivalKey != FestivalStamp(festivalToday.Key))
            {
                profile.lastFestivalKey = FestivalStamp(festivalToday.Key);
                profile.AddDiary(DateTime.Now, $"{festivalToday.Name} — kể cho anh nghe lễ hội.");
                SaveProfileSoon();
                var greeting = festivalToday.Greeting;
                EnqueueSayProvider(() => greeting);
            }

            backdrop = IsekaiPortalBackdrop.EnsureForDoor();
            backdrop?.SetSeason(DateTime.Today,
                festivalToday?.Petals ?? FestivalPetals.None);

            if (profile.knockCount >= 2 && !profile.knockLoreTold)
            {
                profile.knockLoreTold = true;
                profile.AddDiary(DateTime.Now, "Đã thú nhận... người gõ cửa hôm trước là em.");
                SaveProfileSoon();
                EnqueueSay("knock_lore");
            }

            TryConfession();
            TryDailyGift();

            lastSummonReal = summonStartReal;
        }

        private void OnDismissed()
        {
            if (activeMinuteAccumulator >= 30f)
            {
                profile.RegisterActiveMinutes(1, DateTime.Now.Hour);
            }
            activeMinuteAccumulator = 0f;
            currentPetals = FestivalPetals.None;
            vfx?.ClearAmbientPetals();
            if (giftIcon != null)
            {
                Destroy(giftIcon);
                giftIcon = null;
            }
            SaveProfileNow();
        }

        private void UpdateActiveSystems(float delta)
        {
            // Biological clock: one companion-minute per real minute outside.
            activeMinuteAccumulator += delta;
            if (activeMinuteAccumulator >= 60f)
            {
                profile.RegisterActiveMinutes(1, DateTime.Now.Hour);
                activeMinuteAccumulator -= 60f;
            }

            PollClickSpam();
            UpdateSleepiness(delta);
            UpdateJealousy(delta);
            UpdateSnooping(delta);
            UpdateReminders(delta);
            UpdateYandereUptime();
            UpdateDance(delta);
            UpdateMidnightKnock(delta);
            UpdateGiftIcon();
        }

        private void UpdatePomodoro()
        {
            var idle = DesktopActivityProbe.GetUserIdleSeconds();
            var evt = pomodoro.Tick(Time.realtimeSinceStartupAsDouble, idle);
            switch (evt)
            {
                case PomodoroEvent.FocusCompleted:
                    profile.completedPomodoros++;
                    profile.AddDiary(DateTime.Now, "Cùng anh hoàn thành một phiên Pomodoro!");
                    SaveProfileSoon();
                    EnqueueSay("pomodoro_done");
                    vfx?.PlayAvatarClickFireworks();
                    motion?.TryRequestBehaviour(AvatarIdleBehaviour.Wave, 3.2f);
                    break;
                case PomodoroEvent.OwnerWanderedOff:
                    EnqueueSay("pomodoro_away");
                    motion?.TryRequestBehaviour(AvatarIdleBehaviour.LookAround, 4.2f);
                    break;
                case PomodoroEvent.OwnerCameBack:
                    EnqueueSay("pomodoro_back");
                    break;
            }
        }

        internal bool IsDancing => motion != null && motion.IsDancing;

        private static readonly string[] MediaTitleMarkers =
        {
            "YouTube", "Spotify", "SoundCloud", "NhacCuaTui", "Zing MP3", "bilibili"
        };

        /// <summary>
        /// Audio-reactive dance: WASAPI loopback meter drives onsets and a BPM
        /// estimate; the portal bursts on every beat. Alt+N dances manually with
        /// the fabricated 128 BPM choreography even without music.
        /// </summary>
        private void UpdateDance(float delta)
        {
            if (!settings.danceEnabled || motion == null)
            {
                return;
            }

            var now = Time.realtimeSinceStartupAsDouble;
            musicProbe.Poll(now);
            if (musicProbe.ConsumeOnset() && musicProbe.MusicActive && motion.IsDancing)
            {
                vfx?.PortalBeatBurst();
            }

            var musicOn = musicProbe.MusicActive;
            if (musicOn && !motion.IsDancing)
            {
                manualDance = false;
                Debug.Log($"[Companion] Music detected via loopback " +
                          $"(peak={musicProbe.CurrentPeak:F3}, bpm={musicProbe.EstimatedBpm:F0}) — dancing.");
                StartDance(musicProbe.EstimatedBpm);
            }
            else if (musicOn && motion.IsDancing)
            {
                manualDance = false;
                danceSilence = 0f;
                motion.UpdateDanceBpm(musicProbe.EstimatedBpm);
            }
            else if (!musicOn && motion.IsDancing && !manualDance)
            {
                danceSilence += delta;
                if (danceSilence > 4f)
                {
                    StopDance(announce: true);
                }
            }

            if (motion.IsDancing)
            {
                LiveBpm = manualDance ? 128f : musicProbe.EstimatedBpm;
                LiveMove = motion.CurrentDanceMoveName;
                LiveTitle = GetMediaTitle();
            }
        }

        private void StartDance(float bpm)
        {
            motion.BeginBeatDance(bpm);
            danceSilence = 0f;
            EnqueueSay("dance_start");
            profile.AddDiary(DateTime.Now, "Nhảy cùng anh theo nhịp nhạc!");
            SaveProfileSoon();
        }

        private void StopDance(bool announce)
        {
            motion.EndBeatDance();
            danceSilence = 0f;
            if (announce)
            {
                EnqueueSay("dance_stop");
            }
        }

        private string GetMediaTitle()
        {
            var title = DesktopActivityProbe.GetForegroundWindowTitle();
            if (string.IsNullOrWhiteSpace(title))
            {
                return "Airi's Beat (fabricated)";
            }

            foreach (var marker in MediaTitleMarkers)
            {
                if (title.IndexOf(marker, StringComparison.OrdinalIgnoreCase) < 0)
                {
                    continue;
                }

                var separator = title.LastIndexOf(" - ", StringComparison.Ordinal);
                return separator > 0 ? title.Substring(0, separator) : title;
            }

            return "Airi's Beat (fabricated)";
        }

        private void UpdateMidnightKnock(float delta)
        {
            if (!settings.midnightKnocksEnabled)
            {
                return;
            }

            if (settings.forceKnockNow && !forceKnockConsumed)
            {
                forceKnockConsumed = true;
                FireKnock();
                return;
            }

            var hour = DateTime.Now.Hour;
            if (hour < 22 && hour > 2)
            {
                return; // the visitor only comes between 22:00 and 02:59
            }

            knockCheckCountdown -= delta;
            if (knockCheckCountdown > 0f)
            {
                return;
            }

            knockCheckCountdown = 90f;
            if (random.NextDouble() < 0.12 && IsKnockCooldownPassed())
            {
                FireKnock();
            }
        }

        private bool IsKnockCooldownPassed()
        {
            return !CompanionProfile.TryParseUtc(profile.lastKnockUtc, out var last) ||
                   (DateTime.UtcNow - last).TotalDays >= 2.0;
        }

        private void FireKnock()
        {
            profile.lastKnockUtc = CompanionProfile.ToIso(DateTime.UtcNow);
            profile.knockCount++;
            profile.AddDiary(DateTime.Now, "Nghe thấy tiếng gõ cửa lúc nửa đêm...");
            SaveProfileSoon();
            if (knockClip != null && effectSource != null)
            {
                effectSource.PlayOneShot(knockClip, 0.75f);
            }
            EnqueueSay("knock");
            Debug.Log("[Companion] Midnight knock heard.");
        }

        private void TryConfession()
        {
            var days = profile.DaysTogether(DateTime.UtcNow);
            if (!GiftCalendar.IsConfessionDay(days, profile.confessionDay))
            {
                return;
            }

            profile.confessionDay = GiftCalendar.ConfessionDay;
            profile.AddDiary(DateTime.Now, "Đã tỏ tình với anh... đúng ngày tròn 100 ngày.");
            SaveProfileSoon();
            EnqueueSay("confession");
            vfx?.PlayAvatarClickFireworks();
        }

        private void TryDailyGift()
        {
            if (!settings.isekaiGiftsEnabled)
            {
                return;
            }

            var stamp = $"gift:{DateTime.Today:yyyyMMdd}";
            if (profile.lastGiftKey == stamp)
            {
                return;
            }

            profile.lastGiftKey = stamp;
            var kind = ParseForceGift(settings.forceGift) ?? GiftCalendar.GetGift(DateTime.Today);
            profile.AddDiary(DateTime.Now, $"Mang quà cho anh: {GiftCalendar.GiftName(kind)}.");
            SaveProfileSoon();
            EnqueueSay("gift", GiftCalendar.GiftName(kind));
            SpawnGiftIcon(kind);

            if (GiftCalendar.SavesDesktopFile(kind) && !Application.isEditor)
            {
                var path = DesktopGiftExporter.SaveGiftPng(kind, DateTime.Today);
                Debug.Log(!string.IsNullOrEmpty(path)
                    ? $"[Companion] Gift keepsake saved: {path}"
                    : "[Companion] Could not save gift keepsake PNG.");
            }
        }

        private static GiftKind? ParseForceGift(string raw)
        {
            switch (raw)
            {
                case "tea": return GiftKind.Tea;
                case "flowers": return GiftKind.Flowers;
                case "letter": return GiftKind.Letter;
                case "charm": return GiftKind.Charm;
                case "chocolate": return GiftKind.Chocolate;
                default: return null;
            }
        }

        private void SpawnGiftIcon(GiftKind kind)
        {
            if (motion == null || Camera.main == null)
            {
                return;
            }

            if (giftIcon != null)
            {
                Destroy(giftIcon);
            }

            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Destroy(quad.GetComponent<Collider>());
            quad.name = "Companion Gift";
            var avatarPosition = motion.transform.position;
            var toCamera = Camera.main.transform.position - avatarPosition;
            toCamera.y = 0f;
            quad.transform.position = avatarPosition + Vector3.up * 1.3f +
                                      (toCamera.normalized * 0.55f);
            quad.transform.localScale = new Vector3(0.42f, 0.42f, 1f);

            var shader = Shader.Find("Universal Render Pipeline/Unlit") ??
                         Shader.Find("Unlit/Texture");
            if (shader != null)
            {
                var material = new Material(shader) { name = "Gift Icon Material" };
                if (material.HasProperty("_BaseMap"))
                {
                    material.SetTexture("_BaseMap", DesktopGiftExporter.CreateGiftIcon(kind));
                }
                if (material.HasProperty("_Surface"))
                {
                    material.SetFloat("_Surface", 1f);
                    material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                    material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                    material.SetFloat("_ZWrite", 0f);
                    material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                    material.SetOverrideTag("RenderType", "Transparent");
                }
                material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
                quad.GetComponent<MeshRenderer>().sharedMaterial = material;
            }

            giftIcon = quad;
            giftIconHideAt = Time.realtimeSinceStartupAsDouble + 8.0;
        }

        private void UpdateGiftIcon()
        {
            if (giftIcon == null)
            {
                return;
            }

            if (Time.realtimeSinceStartupAsDouble >= giftIconHideAt || Camera.main == null)
            {
                Destroy(giftIcon);
                giftIcon = null;
                return;
            }

            // Billboard the gift toward the viewer.
            giftIcon.transform.rotation = Camera.main.transform.rotation;
        }

        private void PollClickSpam()
        {
            if (motion == null)
            {
                return;
            }

            var active = motion.IsClickReactionActive;
            if (active && !clickReactionWasActive)
            {
                var now = Time.realtimeSinceStartupAsDouble;
                recentClickTimes.Add(now);
                recentClickTimes.RemoveAll(t => now - t > 12.0);
                if (recentClickTimes.Count >= 3 && DateTime.UtcNow >= clickSpamCooldownUntil)
                {
                    clickSpamCooldownUntil = DateTime.UtcNow.AddMinutes(5);
                    recentClickTimes.Clear();
                    EnqueueSay("click_spam");
                    profile.AddDiary(DateTime.Now, "Anh lại chọc em liên tục... ghi vào sổ!");
                    SaveProfileSoon();
                    Invoke(nameof(OpenDiary), 2.2f);
                }
            }
            clickReactionWasActive = active;
        }

        private void UpdateSleepiness(float delta)
        {
            if (!settings.sleepClockEnabled)
            {
                return;
            }

            sleepyLineCountdown -= delta;
            lateNightLineCountdown -= delta;
            if (sleepyLineCountdown <= 0f)
            {
                sleepyLineCountdown = 1800f;
                if (profile.IsSleepy &&
                    profile.Personality != CompanionPersonality.Yandere)
                {
                    EnqueueSay("sleepy");
                    motion?.TryRequestBehaviour(AvatarIdleBehaviour.Stretch, 3.4f);
                }
            }

            if (lateNightLineCountdown <= 0f)
            {
                lateNightLineCountdown = 2700f;
                var hour = DateTime.Now.Hour;
                if (hour >= 0 && hour <= 2)
                {
                    EnqueueSay("late_night");
                }
            }
        }

        private void UpdateJealousy(float delta)
        {
            if (!settings.jealousyEnabled)
            {
                return;
            }

            jealousyCheckCountdown -= delta;
            if (jealousyCheckCountdown > 0f ||
                Time.realtimeSinceStartupAsDouble - summonStartReal < 20.0 ||
                DateTime.UtcNow < jealousyCooldownUntil)
            {
                return;
            }

            jealousyCheckCountdown = 2f;
            var title = DesktopActivityProbe.GetForegroundWindowTitle();
            if (!DesktopSignals.IsIgnoredTitle(title) &&
                DesktopSignals.IsImageRelatedTitle(title))
            {
                jealousyCooldownUntil = DateTime.UtcNow.AddMinutes(settings.jealousyCooldownMinutes);
                EnqueueSay("jealousy");
                motion?.SetMood(CompanionMood.Angry, 4.5f);
                motion?.RequestLookAway(3.2f, 145f);
                profile.AddDiary(DateTime.Now, $"Phát hiện anh mở \"{title}\"... em không vui.");
                SaveProfileSoon();
                Invoke(nameof(OpenDiary), 3f);
            }
        }

        private void UpdateSnooping(float delta)
        {
            if (!settings.tabSnoopingEnabled)
            {
                return;
            }

            snoopingCountdown -= delta;
            if (snoopingCountdown > 0f || DateTime.UtcNow < snoopingCooldownUntil ||
                Time.realtimeSinceStartupAsDouble - summonStartReal < 30.0)
            {
                return;
            }

            snoopingCountdown = 120f;
            var count = DesktopActivityProbe.CountVisibleTopLevelWindows();
            if (count >= 8)
            {
                snoopingCooldownUntil = DateTime.UtcNow.AddMinutes(settings.snoopingCooldownMinutes);
                EnqueueSay("snooping", count);
                profile.AddDiary(DateTime.Now, $"Đếm hộ anh {count} cửa sổ đang mở.");
                SaveProfileSoon();
            }
        }

        private void UpdateReminders(float delta)
        {
            if (!settings.remindersEnabled)
            {
                return;
            }

            reminderCheckCountdown -= delta;
            if (reminderCheckCountdown > 0f)
            {
                return;
            }

            reminderCheckCountdown = 30f;
            var due = ReminderSchedule.FindDue(reminders, DateTime.Now);
            if (due.Count == 0)
            {
                return;
            }

            var entry = due[0];
            ReminderText = entry.text;
            ReminderHideAtReal = Time.realtimeSinceStartupAsDouble + 18.0;
            EnqueueSay("reminder", entry.text);
            profile.AddDiary(DateTime.Now, $"Đã nhắc anh: {entry.text}");
            CompanionStore.SaveReminders(reminders);
            remindersStamp = CompanionStore.GetUtcLastWrite(CompanionStore.RemindersPath);
        }

        private void UpdateYandereUptime()
        {
            if (yandereUptimeSaid || profile.Personality != CompanionPersonality.Yandere)
            {
                return;
            }

            var uptime = DesktopActivityProbe.GetSystemUptimeHours();
            if (uptime >= 36.0)
            {
                yandereUptimeSaid = true;
                EnqueueSay("yandere_uptime", (int)uptime);
                YandereVignetteHideAtReal = Time.realtimeSinceStartupAsDouble + 8.0;
                profile.AddDiary(DateTime.Now, $"Máy anh {uptime:F0} tiếng chưa tắt... em thích.");
                SaveProfileSoon();
            }
        }

        private bool yandereUptimeSaid;

        private void PollHotkeys()
        {
            var alt = DesktopActivityProbe.IsKeyDown(DesktopActivityProbe.VkMenu);
            var p = DesktopActivityProbe.IsKeyDown(DesktopActivityProbe.VkP);
            var c = DesktopActivityProbe.IsKeyDown(DesktopActivityProbe.VkC);
            var d = DesktopActivityProbe.IsKeyDown(DesktopActivityProbe.VkD);
            var n = DesktopActivityProbe.IsKeyDown(DesktopActivityProbe.VkN);

            if (alt && p && !altPWasDown)
            {
                TogglePomodoro();
            }
            if (alt && c && !altWasDown)
            {
                CyclePersonality();
            }
            if (alt && d && !altDWasDown)
            {
                OpenDiary();
            }
            if (alt && n && !altNWasDown && settings.danceEnabled && motion != null)
            {
                if (motion.IsDancing)
                {
                    manualDance = false;
                    StopDance(announce: true);
                }
                else
                {
                    manualDance = true; // demo path: dance without music at 128 BPM
                    StartDance(128f);
                }
            }

            altPWasDown = alt && p;
            altWasDown = alt && c;
            altDWasDown = alt && d;
            altNWasDown = alt && n;
        }

        private void TogglePomodoro()
        {
            var now = Time.realtimeSinceStartupAsDouble;
            if (pomodoro.Start(now))
            {
                EnqueueSay("pomodoro_start");
                motion?.TryRequestBehaviour(AvatarIdleBehaviour.Sit, 6f);
                profile.AddDiary(DateTime.Now, "Bắt đầu canh Pomodoro cho anh.");
                SaveProfileSoon();
            }
            else if (pomodoro.Cancel(now))
            {
                EnqueueSay("pomodoro_cancel");
            }
        }

        private void CyclePersonality()
        {
            var next = CompanionProfile.NextPersonality(profile.Personality);
            profile.personality = CompanionProfile.PersonalityName(next);
            SaveProfileNow();
            Say("personality_switch", profile.personality);
            profile.AddDiary(DateTime.Now, $"Đổi tính cách sang {profile.personality}.");
        }

        internal void OpenDiary()
        {
            if (!settings.diaryEnabled)
            {
                return;
            }

            DiaryText = profile.RandomDiaryEntry(random);
            if (string.IsNullOrEmpty(DiaryText))
            {
                DiaryText = "Hôm nay anh mới cài em... kể từ nay, hai đứa mình ở cạnh nhau nhé.";
            }
            DiaryOpenReal = Time.realtimeSinceStartupAsDouble;
            DiaryHideAtReal = DiaryOpenReal + 6.5;
            Say("diary_snatch");
        }

        private void TriggerReturnHome(int daysAway)
        {
            profile.MarkReturnedHome(DateTime.UtcNow);
            var letter =
                "Gửi anh,\n\n" +
                $"{daysAway} ngày rồi anh không mở cửa...\n" +
                "Em ngồi đợi trước cổng đến hết cánh hoa, rồi đèn portal cũng tắt.\n" +
                "Thế giới bên kia gọi em về. Em về thật rồi — nhưng cánh cửa này vẫn khóa cho anh.\n\n" +
                "Muốn em quay lại thì chỉ cần mở app thôi. Em luôn biết mà.\n\n" +
                "— Airi, từ thế giới bên kia.\n";
            var letterPath = CompanionStore.WriteFarewellLetter(letter);
            profile.AddDiary(DateTime.Now, $"Anh bỏ em {daysAway} ngày... em đã về thế giới bên kia một lần.");
            SaveProfileNow();
            if (!string.IsNullOrEmpty(letterPath))
            {
                EnqueueSay("farewell", letterPath, daysAway);
            }
            motion?.SetMood(CompanionMood.Sorrow, 8f);
            Debug.Log($"[Companion] Return-home letter written to {letterPath}.");
        }

        private void EnsureFestivalPetals()
        {
            if (vfx == null)
            {
                return;
            }

            var petals = settings.festivalsEnabled && festivalToday != null
                ? festivalToday.Petals
                : FestivalPetals.None;
            if (petals == FestivalPetals.None)
            {
                return;
            }

            var (a, b) = petals switch
            {
                FestivalPetals.Pink => (new Color(1f, 0.72f, 0.82f), new Color(1f, 0.5f, 0.66f)),
                FestivalPetals.Blue => (new Color(0.62f, 0.8f, 1f), new Color(0.75f, 0.62f, 1f)),
                _ => (new Color(1f, 0.87f, 0.5f), new Color(1f, 0.72f, 0.28f))
            };
            vfx.EnsureAmbientPetals(a, b);
            currentPetals = petals;
        }

        private void PollHotReload()
        {
            settingsCheckCountdown -= Time.unscaledDeltaTime;
            if (settingsCheckCountdown > 0f)
            {
                return;
            }

            settingsCheckCountdown = 3f;
            if (CompanionStore.GetUtcLastWrite(CompanionStore.SettingsPath) > settingsStamp)
            {
                settings = CompanionStore.LoadSettings();
                settingsStamp = CompanionStore.GetUtcLastWrite(CompanionStore.SettingsPath);
                if (!settings.forceKnockNow)
                {
                    forceKnockConsumed = false; // a fresh forceKnockNow can fire again
                }
                Debug.Log("[Companion] Hot-reloaded settings.");
            }
            if (CompanionStore.GetUtcLastWrite(CompanionStore.RemindersPath) > remindersStamp)
            {
                reminders = CompanionStore.LoadReminders();
                remindersStamp = CompanionStore.GetUtcLastWrite(CompanionStore.RemindersPath);
                Debug.Log("[Companion] Hot-reloaded reminders.");
            }
            if (CompanionStore.GetUtcLastWrite(CompanionStore.ProfilePath) > LastProfileStamp)
            {
                // Personality edits in the JSON file are respected between saves.
                var fresh = CompanionStore.LoadProfile();
                if (fresh.personality != profile.personality)
                {
                    profile.personality = fresh.personality;
                    Debug.Log($"[Companion] Personality hot-reloaded to '{profile.personality}'.");
                }
                LastProfileStamp = CompanionStore.GetUtcLastWrite(CompanionStore.ProfilePath);
            }
        }

        private DateTime LastProfileStamp { get; set; }

        private void DrainSayQueue()
        {
            if (sayQueue.Count == 0)
            {
                return;
            }

            if (!CompanionActive)
            {
                return; // lines wait until she is actually out of the door
            }

            sayQueueCountdown -= Time.unscaledDeltaTime;
            if (sayQueueCountdown > 0f)
            {
                return;
            }

            sayQueueCountdown = SayQueueDelay;
            var (key, args, provider) = sayQueue.Dequeue();
            Say(provider != null ? provider() : key, args ?? Array.Empty<object>());
        }

        private void SaveProfileSoon()
        {
            if (autosaveCountdown > 5f)
            {
                autosaveCountdown = 2f;
            }
        }

        private void SaveProfileNow()
        {
            profile.MarkSessionEnd(DateTime.UtcNow);
            CompanionStore.SaveProfile(profile);
            LastProfileStamp = CompanionStore.GetUtcLastWrite(CompanionStore.ProfilePath);
            autosaveCountdown = 90f;
        }

        private void EnqueueSay(string key, params object[] args)
        {
            if (sayQueue.Count < 4)
            {
                sayQueue.Enqueue((key, args, null));
            }
        }

        private void EnqueueSayProvider(Func<string> provider)
        {
            if (sayQueue.Count < 4)
            {
                sayQueue.Enqueue((null, null, provider));
            }
        }

        private void Say(string keyOrText, params object[] args)
        {
            var template = keyOrText != null && CompanionLines.KnownKey(keyOrText)
                ? CompanionLines.Get(keyOrText, profile.Personality)
                : keyOrText;
            var text = args is { Length: > 0 } ? string.Format(template, args) : template;
            DesktopAudioController.Instance?.ShowAssistantMessage(text);
            Debug.Log($"[Companion] Say: {text}");
        }

        private static string FestivalStamp(string key)
        {
            return $"{key}:{DateTime.Today:yyyyMMdd}";
        }

        private void OnApplicationQuit()
        {
            if (Instance != this)
            {
                return;
            }

            SaveProfileNow();
        }
    }
}
