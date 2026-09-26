using System;

namespace AnimeAssistant.Domain
{
    public enum GiftKind
    {
        Tea,
        Flowers,
        Letter,
        Charm,
        Chocolate
    }

    /// <summary>
    /// "Quà từ thế giới kia" — a deterministic gift of the day, with holiday
    /// overrides (Valentine / White Day chocolate) and the confession rule.
    /// </summary>
    public static class GiftCalendar
    {
        public const int ConfessionDay = 100;

        private static readonly GiftKind[] DailyRotation =
        {
            GiftKind.Tea, GiftKind.Letter, GiftKind.Flowers,
            GiftKind.Tea, GiftKind.Charm, GiftKind.Flowers, GiftKind.Letter
        };

        public static GiftKind GetGift(DateTime localDate)
        {
            if (localDate.Month == 2 && localDate.Day == 14)
            {
                return GiftKind.Chocolate; // Valentine — handmade chocolate
            }
            if (localDate.Month == 3 && localDate.Day == 14)
            {
                return GiftKind.Chocolate; // White Day — return gift
            }

            return DailyRotation[localDate.DayOfYear % DailyRotation.Length];
        }

        public static string GiftName(GiftKind gift)
        {
            switch (gift)
            {
                case GiftKind.Flowers: return "một bó hoa thế giới bên kia";
                case GiftKind.Letter: return "một lá thư viết tay";
                case GiftKind.Charm: return "một bùa may mắn";
                case GiftKind.Chocolate: return "sô-cô-la tay em làm";
                default: return "ly trà ấm";
            }
        }

        public static bool SavesDesktopFile(GiftKind gift)
        {
            return gift == GiftKind.Charm || gift == GiftKind.Chocolate;
        }

        /// <summary>Lời tỏ tình — the special milestone day, once ever.</summary>
        public static bool IsConfessionDay(int daysTogether, int alreadyConfessedDay)
        {
            return daysTogether == ConfessionDay && alreadyConfessedDay != ConfessionDay;
        }
    }
}
