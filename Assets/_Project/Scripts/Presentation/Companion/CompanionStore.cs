using System;
using System.Collections.Generic;
using System.IO;
using AnimeAssistant.Domain;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace AnimeAssistant.Presentation
{
    /// <summary>
    /// File-backed persistence for the companion systems: profile (memory),
    /// reminders and hot-reloadable settings. Everything lives under
    /// Application.persistentDataPath/Companion so it works identically in the
    /// Editor and in the portable player build.
    /// </summary>
    public static class CompanionStore
    {
        private const string DirectoryName = "Companion";
        private const string ProfileFileName = "companion_profile.json";
        private const string RemindersFileName = "reminders.json";
        private const string SettingsFileName = "companion_settings.json";

        public static string RootDirectory =>
            Path.Combine(Application.persistentDataPath, DirectoryName);

        public static string ProfilePath => Path.Combine(RootDirectory, ProfileFileName);
        public static string RemindersPath => Path.Combine(RootDirectory, RemindersFileName);
        public static string SettingsPath => Path.Combine(RootDirectory, SettingsFileName);

        public static CompanionProfile LoadProfile()
        {
            var profile = new CompanionProfile();
            try
            {
                EnsureDirectory();
                if (File.Exists(ProfilePath))
                {
                    JsonUtility.FromJsonOverwrite(File.ReadAllText(ProfilePath), profile);
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[Companion] Could not read profile: {exception.Message}");
            }

            profile.diaryEntries ??= new List<string>();
            return profile;
        }

        public static void SaveProfile(CompanionProfile profile)
        {
            try
            {
                EnsureDirectory();
                File.WriteAllText(ProfilePath, JsonUtility.ToJson(profile, true));
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[Companion] Could not save profile: {exception.Message}");
            }
        }

        public static List<ReminderEntry> LoadReminders()
        {
            var reminders = new List<ReminderEntry>();
            try
            {
                EnsureDirectory();
                if (!File.Exists(RemindersPath))
                {
                    File.WriteAllText(RemindersPath, JsonUtility.ToJson(CreateDefaultReminders(), true));
                    Debug.Log("[Companion] Wrote default reminders.json — edit it to add your own reminders.");
                    return reminders;
                }

                var wrapper = JsonUtility.FromJson<ReminderListWrapper>(File.ReadAllText(RemindersPath));
                if (wrapper?.entries != null)
                {
                    reminders.AddRange(wrapper.entries);
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[Companion] Could not read reminders: {exception.Message}");
            }

            return reminders;
        }

        public static void SaveReminders(List<ReminderEntry> reminders)
        {
            try
            {
                EnsureDirectory();
                File.WriteAllText(RemindersPath,
                    JsonUtility.ToJson(new ReminderListWrapper { entries = reminders }, true));
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[Companion] Could not save reminders: {exception.Message}");
            }
        }

        public static CompanionSettings LoadSettings()
        {
            var settings = new CompanionSettings();
            try
            {
                EnsureDirectory();
                if (File.Exists(SettingsPath))
                {
                    JsonUtility.FromJsonOverwrite(File.ReadAllText(SettingsPath), settings);
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[Companion] Could not read settings: {exception.Message}");
            }

            settings.Normalize();
            return settings;
        }

        public static DateTime GetUtcLastWrite(string path)
        {
            try
            {
                return File.Exists(path) ? File.GetLastWriteTimeUtc(path) : DateTime.MinValue;
            }
            catch
            {
                return DateTime.MinValue;
            }
        }

        /// <summary>Writes the farewell letter ("Hồi Quy") to the user's Desktop\Airi folder.</summary>
        public static string WriteFarewellLetter(string content)
        {
            const string fileName = "Thu tu Airi.txt";
            try
            {
                var directory = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "Airi");
                Directory.CreateDirectory(directory);
                var path = Path.Combine(directory, fileName);
                File.WriteAllText(path, content);
                return path;
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[Companion] Could not write farewell letter: {exception.Message}");
                return null;
            }
        }

        private static void EnsureDirectory()
        {
            Directory.CreateDirectory(RootDirectory);
        }

        private static List<ReminderEntry> CreateDefaultReminders()
        {
            return new List<ReminderEntry>
            {
                new ReminderEntry
                {
                    text = "Uống nước đi anh, ngồi mãi vậy khổ lắm~",
                    repeatMinutes = 90,
                    enabled = true
                },
                new ReminderEntry
                {
                    text = "Gọi điện cho mẹ đi anh nha...",
                    dailyTime = "20:30",
                    enabled = false
                },
                new ReminderEntry
                {
                    text = "Ví dụ nhắc một lần: lịch phỏng vấn 9h sáng mai",
                    onceLocalTime = "2026-12-31 09:00",
                    enabled = false
                }
            };
        }

        [Serializable]
        private sealed class ReminderListWrapper
        {
            public List<ReminderEntry> entries;
        }
    }
}
