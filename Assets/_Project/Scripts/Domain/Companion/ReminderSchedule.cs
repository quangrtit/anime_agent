using System;
using System.Collections.Generic;
using System.Globalization;

namespace AnimeAssistant.Domain
{
    /// <summary>One reminder the companion keeps for the user. Kinds are mutually exclusive.</summary>
    [Serializable]
    public sealed class ReminderEntry
    {
        public string text = "";
        public string dailyTime = "";
        public int repeatMinutes;
        public string onceLocalTime = "";
        public bool enabled = true;
        public string lastFiredKey = "";
    }

    /// <summary>
    /// Pure due-time evaluation for reminder entries. Firing bookkeeping lives
    /// in the entry itself so the presenter can persist it between sessions.
    /// </summary>
    public static class ReminderSchedule
    {
        public static bool TryGetDue(ReminderEntry entry, DateTime localNow, out string fireKey)
        {
            fireKey = "";
            if (entry == null || !entry.enabled || string.IsNullOrWhiteSpace(entry.text))
            {
                return false;
            }

            if (!string.IsNullOrWhiteSpace(entry.dailyTime) &&
                TimeSpan.TryParseExact(entry.dailyTime, "hh\\:mm", CultureInfo.InvariantCulture,
                    out var dailyTime))
            {
                var dueAt = localNow.Date + dailyTime;
                if (localNow < dueAt)
                {
                    return false;
                }

                fireKey = $"daily:{entry.text.GetHashCode():x8}:{dueAt:yyyyMMdd}";
                return entry.lastFiredKey != fireKey;
            }

            if (entry.repeatMinutes > 0)
            {
                // Interval reminders repeat through the whole day; the key rolls
                // over each day so they re-arm overnight.
                var slot = (int)(localNow.TimeOfDay.TotalMinutes / entry.repeatMinutes);
                fireKey = $"repeat:{entry.text.GetHashCode():x8}:{localNow:yyyyMMdd}:{slot}";
                return entry.lastFiredKey != fireKey;
            }

            if (!string.IsNullOrWhiteSpace(entry.onceLocalTime) &&
                DateTime.TryParseExact(entry.onceLocalTime, "yyyy-MM-dd HH:mm",
                    CultureInfo.InvariantCulture, DateTimeStyles.None, out var onceAt))
            {
                if (localNow < onceAt)
                {
                    return false;
                }

                fireKey = "once";
                return entry.lastFiredKey != fireKey;
            }

            return false;
        }

        public static List<ReminderEntry> FindDue(IEnumerable<ReminderEntry> entries, DateTime localNow)
        {
            var due = new List<ReminderEntry>();
            if (entries == null)
            {
                return due;
            }

            foreach (var entry in entries)
            {
                if (TryGetDue(entry, localNow, out var fireKey))
                {
                    entry.lastFiredKey = fireKey;
                    due.Add(entry);
                }
            }

            return due;
        }
    }
}
