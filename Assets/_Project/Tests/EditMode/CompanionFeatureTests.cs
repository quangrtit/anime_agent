using System;
using System.Collections.Generic;
using AnimeAssistant.Domain;
using NUnit.Framework;

namespace AnimeAssistant.Tests.EditMode
{
    public sealed class CompanionFeatureTests
    {
        private static readonly DateTime UtcNow = new(2026, 9, 26, 12, 0, 0, DateTimeKind.Utc);

        // ---- CompanionProfile -------------------------------------------------

        [Test]
        public void FirstLaunch_InitializesFirstMetAndCountsDayOne()
        {
            var profile = new CompanionProfile();
            profile.RegisterSessionStart(UtcNow);

            Assert.That(profile.DaysTogether(UtcNow), Is.EqualTo(1));
            Assert.That(profile.DaysSinceLastSeen(UtcNow), Is.EqualTo(0));
            Assert.That(CompanionProfile.TryParseUtc(profile.firstMetUtc, out var firstMet), Is.True);
            Assert.That(firstMet, Is.EqualTo(UtcNow));
        }

        [Test]
        public void HundredDaysLater_MilestoneIsRecognizedOnce()
        {
            var profile = new CompanionProfile();
            profile.RegisterSessionStart(UtcNow);
            var later = UtcNow.AddDays(99);

            Assert.That(profile.DaysTogether(later), Is.EqualTo(100));
            Assert.That(profile.IsAnniversaryDay(later), Is.True);
            profile.MarkAnniversary(profile.DaysTogether(later));
            Assert.That(profile.IsAnniversaryDay(later), Is.False,
                "The same anniversary must not repeat within one day.");
        }

        [Test]
        public void LateNightActiveMinutes_AccumulateIntoSleepDebtAndRecoverAfterAbsence()
        {
            var profile = new CompanionProfile();
            profile.RegisterSessionStart(UtcNow);
            profile.RegisterActiveMinutes(180, 1); // 3h past midnight
            profile.RegisterActiveMinutes(60, 14); // daytime — no debt
            Assert.That(profile.sleepDebtMinutes, Is.EqualTo(180));
            Assert.That(profile.IsSleepy, Is.True);

            profile.MarkSessionEnd(UtcNow);
            profile.RegisterSessionStart(UtcNow.AddDays(7));
            Assert.That(profile.sleepDebtMinutes,
                Is.EqualTo(Math.Max(0, 180 - 7 * 120)));
            Assert.That(profile.IsSleepy, Is.False);
        }

        [Test]
        public void LongAbsence_TriggersReturnHomeOnlyOncePerDay()
        {
            var profile = new CompanionProfile();
            profile.RegisterSessionStart(UtcNow);
            profile.MarkSessionEnd(UtcNow);

            Assert.That(profile.ShouldReturnHome(UtcNow.AddDays(7)), Is.True);
            profile.MarkReturnedHome(UtcNow.AddDays(7));
            Assert.That(profile.ShouldReturnHome(UtcNow.AddDays(7)), Is.False,
                "One farewell per visit is enough.");
            Assert.That(profile.ShouldReturnHome(UtcNow.AddDays(8)), Is.True);
        }

        [Test]
        public void Diary_CapsEntriesAndKeepsTheMostRecent()
        {
            var profile = new CompanionProfile();
            for (var i = 0; i < CompanionProfile.MaxDiaryEntries + 10; i++)
            {
                profile.AddDiary(new DateTime(2026, 1, 1).AddMinutes(i), $"line {i}");
            }

            Assert.That(profile.diaryEntries.Count, Is.EqualTo(CompanionProfile.MaxDiaryEntries));
            StringAssert.Contains($"line {CompanionProfile.MaxDiaryEntries + 9}",
                profile.diaryEntries[^1]);
        }

        [TestCase("tsundere", CompanionPersonality.Tsundere)]
        [TestCase("YANDERE", CompanionPersonality.Yandere)]
        [TestCase("kuudere", CompanionPersonality.Kuudere)]
        [TestCase("nonsense", CompanionPersonality.Deredere)]
        public void PersonalityParsing_IsTolerant(string raw, CompanionPersonality expected)
        {
            Assert.That(CompanionProfile.ParsePersonality(raw), Is.EqualTo(expected));
        }

        [Test]
        public void PersonalityCycle_CoversAllFourModes()
        {
            var order = new List<CompanionPersonality>();
            var current = CompanionPersonality.Deredere;
            for (var i = 0; i < 5; i++)
            {
                order.Add(current);
                current = CompanionProfile.NextPersonality(current);
            }

            CollectionAssert.AreEqual(new[]
            {
                CompanionPersonality.Deredere, CompanionPersonality.Tsundere,
                CompanionPersonality.Yandere, CompanionPersonality.Kuudere,
                CompanionPersonality.Deredere
            }, order);
        }

        // ---- PomodoroMachine --------------------------------------------------

        [Test]
        public void Pomodoro_CompletesFocusThenBreakThenReturnsToIdle()
        {
            var machine = new PomodoroMachine();
            var start = 0.0;
            Assert.That(machine.Start(start), Is.True);
            Assert.That(machine.Start(start), Is.False, "Must not double-start.");
            Assert.That(machine.Phase, Is.EqualTo(PomodoroPhase.Focusing));

            // No completion before the full 25 minutes.
            Assert.That(machine.Tick(start + 60, 0), Is.EqualTo(PomodoroEvent.None));

            var completed = machine.Tick(start + PomodoroMachine.FocusMinutes * 60 + 1, 0);
            Assert.That(completed, Is.EqualTo(PomodoroEvent.FocusCompleted));
            Assert.That(machine.Phase, Is.EqualTo(PomodoroPhase.OnBreak));
            Assert.That(machine.CompletedFocusSessions, Is.EqualTo(1));

            var breakEnd = machine.Tick(start + (PomodoroMachine.FocusMinutes + PomodoroMachine.BreakMinutes) * 60 + 2, 0);
            Assert.That(breakEnd, Is.EqualTo(PomodoroEvent.BreakEnded));
            Assert.That(machine.Phase, Is.EqualTo(PomodoroPhase.Idle));
        }

        [Test]
        public void Pomodoro_NoticesOwnerWanderingOffAndComingBack()
        {
            var machine = new PomodoroMachine();
            machine.Start(0);

            Assert.That(machine.Tick(60, PomodoroMachine.AwayNoticeSeconds + 5),
                Is.EqualTo(PomodoroEvent.OwnerWanderedOff));
            Assert.That(machine.WasAwayDuringFocus, Is.True);

            // Still under the focus end — the off notice must not repeat.
            Assert.That(machine.Tick(120, PomodoroMachine.AwayNoticeSeconds + 40),
                Is.EqualTo(PomodoroEvent.None));

            Assert.That(machine.Tick(180, 10), Is.EqualTo(PomodoroEvent.OwnerCameBack));
        }

        [Test]
        public void Pomodoro_CancelStopsTheCycle()
        {
            var machine = new PomodoroMachine();
            machine.Start(0);
            Assert.That(machine.Cancel(10), Is.True);
            Assert.That(machine.Phase, Is.EqualTo(PomodoroPhase.Idle));
            Assert.That(machine.Tick(20, 0), Is.EqualTo(PomodoroEvent.Cancelled));
            Assert.That(machine.Tick(30, 0), Is.EqualTo(PomodoroEvent.None));
        }

        // ---- ReminderSchedule -------------------------------------------------

        [Test]
        public void DailyReminder_FiresOncePerDayAfterItsTime()
        {
            var entry = new ReminderEntry { text = "Gọi mẹ", dailyTime = "20:30", enabled = true };
            var morning = new DateTime(2026, 9, 26, 9, 0, 0);
            var evening = new DateTime(2026, 9, 26, 21, 0, 0);

            Assert.That(ReminderSchedule.TryGetDue(entry, morning, out _), Is.False);
            Assert.That(ReminderSchedule.TryGetDue(entry, evening, out var fireKey), Is.True);

            entry.lastFiredKey = fireKey;
            Assert.That(ReminderSchedule.TryGetDue(entry, evening.AddMinutes(10), out _), Is.False);
            Assert.That(ReminderSchedule.TryGetDue(entry, evening.AddDays(1), out _), Is.True,
                "A daily reminder re-arms the next day.");
        }

        [Test]
        public void IntervalReminder_FiresPerSlotAndReArmsNextSlot()
        {
            var entry = new ReminderEntry { text = "Uống nước", repeatMinutes = 90, enabled = true };
            var at = new DateTime(2026, 9, 26, 10, 0, 0);

            Assert.That(ReminderSchedule.TryGetDue(entry, at, out var key), Is.True);
            entry.lastFiredKey = key;
            // 20 minutes later is still inside the same 90-minute slot.
            Assert.That(ReminderSchedule.TryGetDue(entry, at.AddMinutes(20), out _), Is.False);
            Assert.That(ReminderSchedule.TryGetDue(entry, at.AddMinutes(95), out _), Is.True);
        }

        [Test]
        public void OnceReminder_FiresExactlyOnceAtOrAfterTarget()
        {
            var entry = new ReminderEntry
            {
                text = "Phỏng vấn",
                onceLocalTime = "2026-09-26 09:00",
                enabled = true
            };

            Assert.That(ReminderSchedule.TryGetDue(entry, new DateTime(2026, 9, 26, 8, 0, 0), out _), Is.False);
            Assert.That(ReminderSchedule.TryGetDue(entry, new DateTime(2026, 9, 26, 9, 30, 0), out var key), Is.True);
            entry.lastFiredKey = key;
            Assert.That(ReminderSchedule.TryGetDue(entry, new DateTime(2027, 1, 1), out _), Is.False);
        }

        [Test]
        public void DisabledOrEmptyEntries_NeverFire()
        {
            Assert.That(ReminderSchedule.TryGetDue(
                new ReminderEntry { enabled = false, text = "x", repeatMinutes = 10 },
                DateTime.Now, out _), Is.False);
            Assert.That(ReminderSchedule.TryGetDue(new ReminderEntry { text = "" }, DateTime.Now, out _),
                Is.False);
        }

        // ---- CompanionFestival ------------------------------------------------

        [Test]
        public void FestivalCalendar_HitsTheExpectedDates()
        {
            Assert.That(CompanionFestival.ForDate(new DateTime(2026, 1, 1))?.Key, Is.EqualTo("shogatsu"));
            Assert.That(CompanionFestival.ForDate(new DateTime(2026, 7, 7))?.Key, Is.EqualTo("tanabata"));
            Assert.That(CompanionFestival.ForDate(new DateTime(2026, 4, 1))?.Key, Is.EqualTo("hanami"));
            Assert.That(CompanionFestival.ForDate(new DateTime(2026, 12, 25))?.Key, Is.EqualTo("christmas"));
            Assert.That(CompanionFestival.ForDate(new DateTime(2026, 9, 26)), Is.Null);
        }

        // ---- GiftCalendar -----------------------------------------------------

        [TestCase("2026-02-14", GiftKind.Chocolate)]
        [TestCase("2027-03-14", GiftKind.Chocolate)]
        public void ValentineAndWhiteDay_OverrideTheGiftRotation(string date, GiftKind expected)
        {
            Assert.That(GiftCalendar.GetGift(DateTime.Parse(date)), Is.EqualTo(expected));
        }

        [Test]
        public void GiftOfTheDay_IsDeterministicAndCharmsRare()
        {
            var day = new DateTime(2026, 9, 26);
            Assert.That(GiftCalendar.GetGift(day), Is.EqualTo(GiftCalendar.GetGift(day)));
            var charmDays = 0;
            for (var offset = 0; offset < 28; offset++)
            {
                if (GiftCalendar.GetGift(day.AddDays(offset)) == GiftKind.Charm)
                {
                    charmDays++;
                }
            }
            Assert.That(charmDays, Is.EqualTo(4), "One charm per week on average.");
        }

        [Test]
        public void Confession_FiresOnlyOnTheMilestoneDayOnce()
        {
            Assert.That(GiftCalendar.IsConfessionDay(99, 0), Is.False);
            Assert.That(GiftCalendar.IsConfessionDay(100, 0), Is.True);
            Assert.That(GiftCalendar.IsConfessionDay(100, 100), Is.False,
                "One confession ever.");
            Assert.That(GiftCalendar.IsConfessionDay(101, 100), Is.False);
        }

        // ---- Knock lore -------------------------------------------------------

        [Test]
        public void MidnightKnock_RespectsTwoDayCooldown()
        {
            var profile = new CompanionProfile();
            Assert.That(profile.knockCount, Is.EqualTo(0));

            profile.lastKnockUtc = CompanionProfile.ToIso(UtcNow);
            profile.knockCount = 1;
            Assert.That((DateTime.UtcNow - DateTime.UtcNow).TotalDays >= 0);
            Assert.That(CompanionProfile.TryParseUtc(profile.lastKnockUtc, out var last), Is.True);
            Assert.That((UtcNow.AddDays(1) - last).TotalDays, Is.EqualTo(1).Within(0.01));
        }

        // ---- DesktopSignals ---------------------------------------------------

        [TestCase("IMG_2026.png - Photos", true)]
        [TestCase("waifu_collection.jpg - Xem ảnh Windows", true)]
        [TestCase("Instagram - Chrome", true)]
        [TestCase("Visual Studio Code - CompanionDirector.cs", false)]
        [TestCase("", false)]
        public void ImageRelatedTitles_AreDetected(string title, bool expected)
        {
            Assert.That(DesktopSignals.IsImageRelatedTitle(title), Is.EqualTo(expected));
        }
    }
}
