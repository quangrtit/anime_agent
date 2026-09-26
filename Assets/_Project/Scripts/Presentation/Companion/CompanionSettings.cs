using System;
using UnityEngine;

namespace AnimeAssistant.Presentation
{
    /// <summary>
    /// User-tunable switches for the companion features. Lives in its own JSON
    /// file (companion_settings.json) and is hot-reloaded so users can tweak
    /// behaviour without restarting the overlay.
    /// </summary>
    [Serializable]
    public sealed class CompanionSettings
    {
        public bool enabled = true;
        public bool jealousyEnabled = true;
        public bool tabSnoopingEnabled = true;
        public bool sleepClockEnabled = true;
        public bool pomodoroEnabled = true;
        public bool remindersEnabled = true;
        public bool diaryEnabled = true;
        public bool festivalsEnabled = true;
        public bool danceEnabled = true;
        public bool isekaiGiftsEnabled = true;
        public bool midnightKnocksEnabled = true;
        public float jealousyCooldownMinutes = 12f;
        public float snoopingCooldownMinutes = 45f;
        public int waterReminderMinutes = 90;
        public int returnHomeAfterDays = AnimeAssistant.Domain.CompanionProfile.ReturnHomeAfterDays;

        // Demo/testing aids: force a specific gift ("tea|flowers|letter|charm|chocolate")
        // or fire a midnight knock on the next active tick.
        public string forceGift = "";
        public bool forceKnockNow;

        public void Normalize()
        {
            jealousyCooldownMinutes = Mathf.Clamp(jealousyCooldownMinutes, 1f, 720f);
            snoopingCooldownMinutes = Mathf.Clamp(snoopingCooldownMinutes, 5f, 720f);
            waterReminderMinutes = Mathf.Clamp(waterReminderMinutes, 15, 480);
            returnHomeAfterDays = Mathf.Clamp(returnHomeAfterDays, 2, 365);
            forceGift = forceGift == null ? "" : forceGift.Trim().ToLowerInvariant();
        }
    }
}
