using System;
using System.Collections.Generic;

namespace AnimeAssistant.Domain
{
    public enum CompanionPersonality
    {
        Deredere,
        Tsundere,
        Yandere,
        Kuudere
    }

    /// <summary>
    /// Persistent "shared life" state between the user and the desktop companion:
    /// first meeting, days together, biological sleep debt, diary lines and the
    /// active personality. Serialized by the presentation layer as plain JSON.
    /// </summary>
    [Serializable]
    public sealed class CompanionProfile
    {
        public const int MaxDiaryEntries = 80;
        public const int MaxSleepDebtMinutes = 600;
        public const int SleepyDebtMinutes = 150;
        public const int ReturnHomeAfterDays = 7;

        public string personality = "deredere";
        public string firstMetUtc = "";
        public string lastSeenUtc = "";
        public double totalActiveMinutes;
        public int sleepDebtMinutes;
        public int completedPomodoros;
        public int lastAnniversaryDay;
        public string lastFestivalKey = "";
        public string lastReturnHomeUtc = "";
        public string lastGiftKey = "";
        public int confessionDay;
        public int knockCount;
        public string lastKnockUtc = "";
        public bool knockLoreTold;
        public List<string> diaryEntries = new List<string>();

        public CompanionPersonality Personality => ParsePersonality(personality);

        public static CompanionPersonality ParsePersonality(string value)
        {
            switch ((value ?? "").Trim().ToLowerInvariant())
            {
                case "tsundere": return CompanionPersonality.Tsundere;
                case "yandere": return CompanionPersonality.Yandere;
                case "kuudere": return CompanionPersonality.Kuudere;
                default: return CompanionPersonality.Deredere;
            }
        }

        public static string PersonalityName(CompanionPersonality personality)
        {
            switch (personality)
            {
                case CompanionPersonality.Tsundere: return "tsundere";
                case CompanionPersonality.Yandere: return "yandere";
                case CompanionPersonality.Kuudere: return "kuudere";
                default: return "deredere";
            }
        }

        public static CompanionPersonality NextPersonality(CompanionPersonality personality)
        {
            return personality switch
            {
                CompanionPersonality.Deredere => CompanionPersonality.Tsundere,
                CompanionPersonality.Tsundere => CompanionPersonality.Yandere,
                CompanionPersonality.Yandere => CompanionPersonality.Kuudere,
                _ => CompanionPersonality.Deredere
            };
        }

        public static bool TryParseUtc(string value, out DateTime utc)
        {
            utc = DateTime.MinValue;
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            return DateTime.TryParse(value, null,
                System.Globalization.DateTimeStyles.RoundtripKind, out utc) && utc != DateTime.MinValue;
        }

        /// <summary>Creates the first-meeting anchor on first launch and recovers sleep debt after long absences.</summary>
        public void RegisterSessionStart(DateTime nowUtc)
        {
            if (!TryParseUtc(firstMetUtc, out _))
            {
                firstMetUtc = ToIso(nowUtc);
            }

            var daysAway = 0;
            if (TryParseUtc(lastSeenUtc, out var lastSeen))
            {
                daysAway = Math.Max(0, (int)(nowUtc - lastSeen).TotalDays);
                if (daysAway >= 6)
                {
                    // A real night of sleep (or a longer break) repays the debt.
                    sleepDebtMinutes = Math.Max(0, sleepDebtMinutes - daysAway * 120);
                }
            }
        }

        public void MarkSessionEnd(DateTime nowUtc)
        {
            lastSeenUtc = ToIso(nowUtc);
        }

        public int DaysTogether(DateTime nowUtc)
        {
            if (!TryParseUtc(firstMetUtc, out var firstMet))
            {
                return 1;
            }

            return Math.Max(1, (int)(nowUtc.Date - firstMet.Date).TotalDays + 1);
        }

        public int DaysSinceLastSeen(DateTime nowUtc)
        {
            if (!TryParseUtc(lastSeenUtc, out var lastSeen))
            {
                return 0;
            }

            return Math.Max(0, (int)(nowUtc.Date - lastSeen.Date).TotalDays);
        }

        public bool ShouldReturnHome(DateTime nowUtc)
        {
            if (!TryParseUtc(lastReturnHomeUtc, out var lastReturn) ||
                (nowUtc - lastReturn).TotalDays >= 1.0)
            {
                return DaysSinceLastSeen(nowUtc) >= ReturnHomeAfterDays;
            }

            return false;
        }

        public void MarkReturnedHome(DateTime nowUtc)
        {
            lastReturnHomeUtc = ToIso(nowUtc);
        }

        /// <summary>Adds a chunk of active companionship time; late-night minutes become sleep debt.</summary>
        public void RegisterActiveMinutes(int minutes, int localHourOfDay)
        {
            if (minutes <= 0)
            {
                return;
            }

            totalActiveMinutes += minutes;
            if (localHourOfDay >= 23 || localHourOfDay < 5)
            {
                sleepDebtMinutes = Math.Min(MaxSleepDebtMinutes, sleepDebtMinutes + minutes);
            }
        }

        public bool IsSleepy => sleepDebtMinutes >= SleepyDebtMinutes;

        public bool IsAnniversaryDay(DateTime nowUtc)
        {
            var days = DaysTogether(nowUtc);
            return IsMilestoneDay(days) && days != lastAnniversaryDay;
        }

        public void MarkAnniversary(int days)
        {
            lastAnniversaryDay = days;
        }

        public static bool IsMilestoneDay(int days)
        {
            return days == 1 || days == 3 || days == 7 || days == 30 || days == 100 ||
                   days == 200 || days == 300 || days == 365 || days == 500 ||
                   days == 730 || days == 1000 || days % 365 == 0 && days > 0;
        }

        public void AddDiary(DateTime localNow, string line)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                return;
            }

            diaryEntries.Add($"{localNow:yyyy-MM-dd HH:mm} — {line.Trim()}");
            while (diaryEntries.Count > MaxDiaryEntries)
            {
                diaryEntries.RemoveAt(0);
            }
        }

        public bool HasDiaryEntries => diaryEntries != null && diaryEntries.Count > 0;

        public string RandomDiaryEntry(Random random)
        {
            if (!HasDiaryEntries)
            {
                return "";
            }

            return diaryEntries[random.Next(diaryEntries.Count)];
        }

        public static string ToIso(DateTime utc)
        {
            return utc.ToString("o", System.Globalization.CultureInfo.InvariantCulture);
        }
    }
}
