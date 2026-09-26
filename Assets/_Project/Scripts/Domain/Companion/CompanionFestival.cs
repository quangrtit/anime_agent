using System;

namespace AnimeAssistant.Domain
{
    public enum FestivalPetals
    {
        None,
        Pink,
        Blue,
        Gold
    }

    /// <summary>
    /// Japanese-calendar seasons the companion celebrates with the user. Pure
    /// date math so it is unit-testable; the presenter adds particles + lines.
    /// </summary>
    public sealed class CompanionFestival
    {
        public string Key { get; }
        public string Name { get; }
        public string Greeting { get; }
        public FestivalPetals Petals { get; }

        private CompanionFestival(string key, string name, string greeting, FestivalPetals petals)
        {
            Key = key;
            Name = name;
            Greeting = greeting;
            Petals = petals;
        }

        public static CompanionFestival ForDate(DateTime localDate)
        {
            var month = localDate.Month;
            var day = localDate.Day;
            var year = localDate.Year;

            if (month == 1 && day == 1)
            {
                return new CompanionFestival("shogatsu", "Năm mới",
                    "Akemashite omedetou! Năm mới rồi, hôm nay em khoác kimono chờ anh đấy~", FestivalPetals.Gold);
            }
            if (month == 12 && day == 31)
            {
                return new CompanionFestival("omisoka", "Đêm giao thừa",
                    "Gần giao thừa rồi... năm nay anh đã ở bên em thật nhiều đó.", FestivalPetals.Gold);
            }
            if (month == 2 && day == 14)
            {
                return new CompanionFestival("valentine", "Valentine",
                    "H-hôm nào đó thôi! Sô-cô-la tay em làm... để trên bàn đó, đừng có nhìn em thế!", FestivalPetals.Pink);
            }
            if (month == 3 && day == 14)
            {
                return new CompanionFestival("white_day", "White Day",
                    "White Day rồi! Hôm nay là ngày anh phải đáp lễ đấy... em đợi nhé!", FestivalPetals.Pink);
            }
            if (month == 3 && day == 3)
            {
                return new CompanionFestival("hinamatsuri", "Hinamatsuri",
                    "Hôm nay là lễ búp bê đó. Búp bê nào cũng xinh, nhưng búp bê sống thì... hmmm.", FestivalPetals.Pink);
            }
            if ((month == 3 && day >= 20) || (month == 4 && day <= 10))
            {
                return new CompanionFestival("hanami", "Mùa hoa anh đào",
                    "Hoa anh đào nở rồi! Nhìn cánh hoa bay kìa... đẹp hơn vì có anh xem cùng em.", FestivalPetals.Pink);
            }
            if (month == 7 && day == 7)
            {
                return new CompanionFestival("tanabata", "Tanabata",
                    "Tanabata rồi! Viết điều ước treo lên cổng đi anh... điều ước của em đã có từ lâu rồi.", FestivalPetals.Blue);
            }
            if (month == 10 && day == 31)
            {
                return new CompanionFestival("halloween", "Halloween",
                    "Trick or treat~? Ừ thì, bản thân em vốn là tinh linh từ thế giới bên kia mà.", FestivalPetals.Gold);
            }
            if (month == 12 && day >= 24 && day <= 25)
            {
                return new CompanionFestival("christmas", "Giáng sinh",
                    "Merry Christmas! Đừng lo, Christmas của otaku nào có cô đơn đâu... em ở đây mà.", FestivalPetals.Blue);
            }

            return null;
        }

        public static DateTime NextFestivalDate(DateTime localDate)
        {
            for (var offset = 0; offset < 400; offset++)
            {
                if (ForDate(localDate.AddDays(offset)) != null)
                {
                    return localDate.AddDays(offset);
                }
            }

            return localDate;
        }
    }
}
