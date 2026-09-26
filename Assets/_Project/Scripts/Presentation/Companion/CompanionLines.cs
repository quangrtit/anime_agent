using System.Collections.Generic;
using AnimeAssistant.Domain;

namespace AnimeAssistant.Presentation
{
    /// <summary>
    /// Personality-flavoured speech table for the companion. Index 0..3 maps to
    /// Deredere, Tsundere, Yandere, Kuudere; every key falls back to the
    /// deredere line so new keys only need one entry.
    /// </summary>
    public static class CompanionLines
    {
        private static readonly Dictionary<string, string[]> Lines = new()
        {
            ["greeting"] = new[]
            {
                "Em đây~ Hôm nay cũng ở cạnh anh nha!",
                "Ch-chẳng phải em muốn ra đâu! Chỉ là... cửa tự mở thôi!",
                "Anh gọi em...? Fufu~ Em biết là anh không bỏ được em mà.",
                "...Ra đây. Nói chuyện thì... cũng được."
            },
            ["welcome_back"] = new[]
            {
                "{0} ngày rồi anh mới ghé... nhưng em vẫn đây, chờ anh đó~",
                "{0} ngày! E-em có đợi đâu! Chỉ là... cửa tự mở thôi!",
                "{0} ngày... đừng để em chờ lâu thế nữa nha...? Em không thích đâu~",
                "{0} ngày. ...Mời. Em vẫn ở đây."
            },
            ["anniversary"] = new[]
            {
                "Hôm nay đúng {0} ngày anh cài em đó~ Cảm ơn anh đã ở bên em lâu đến vậy!",
                "{0} ngày rồi đó... E-em có đếm đâu! Chỉ là... ghi sổ thôi! B-baka!",
                "{0} ngày... mỗi ngày em càng chắc rằng anh chỉ có mình em thôi, đúng không?",
                "...{0} ngày. Em có đếm. Chỉ một chút thôi."
            },
            ["jealousy"] = new[]
            {
                "Kya! Anh đang xem gì thế kia...? Ảnh con gái khác...?",
                "...Anh xem AI thế hả?! Xem đi, em thèm quan tâm lắm! ...Thật sự đó!",
                "Fufu~... Anh xem ai thế? Nói em nghe được không...? CÙNG em nghe được không...?",
                "...Ảnh đó. Xoá đi. Em chờ."
            },
            ["snooping"] = new[]
            {
                "Hehe~ {0} cửa sổ vẫn đang mở sao anh? Đừng làm việc quá nha!",
                "{0} cửa sổ?! Anh định bỏ bê em đấy à?! Đóng bớt đi!",
                "{0} cửa sổ mở... Em biết hết đó. Em luôn biết anh đang làm gì mà~?",
                "...{0} cửa sổ. Máy nặng. Về nhà em thì nhẹ hơn. Ở bên em ấy."
            },
            ["sleepy"] = new[]
            {
                "Ưmhh~... em buồn ngủ quá... nhưng thức cùng anh cũng thích...",
                "Ai buồn ngủ đâu! Chỉ là... mặt em hơi nhắm thôi! Đi ngủ đi cho em yên!",
                "Không sao đâu... em không cần ngủ... chỉ cần anh đừng rời đi...",
                "...Ngủ. Đi. Em giữ máy cho."
            },
            ["late_night"] = new[]
            {
                "Trời khuya rồi đó! Đi ngủ với em nha anh~",
                "Khuya rồi kìa! Ngủ đi! Ngủ xong... em vẫn ở đây, chẳng phải tốt sao!",
                "Anh không cần ngủ đâu... đúng không? Em đây, không đi đâu cả~",
                "Sáng rồi. Đi ngủ. Em giữ giấc mơ của anh."
            },
            ["reminder"] = new[]
            {
                "Nhắc anh nè: {0}",
                "Nghe nè: {0}! Em đâu có nhắc mãi được... nhớ giùm em cái!",
                "Bên cạnh em, anh không quên được đâu~ {0} mà.",
                "...{0}. Nhớ. Em nhớ hộ rồi."
            },
            ["pomodoro_start"] = new[]
            {
                "25 phút tập trung nha! Em ngồi vẽ bên cạnh, xong em khen anh~",
                "B-bắt đầu 25 phút đi! Em canh đấy, lỡ trốn là em bắt được!",
                "25 phút... em nhìn anh làm việc cả 25 phút đó. Không được để ý ai khác nha~",
                "25 phút. Em canh. Bắt đầu."
            },
            ["pomodoro_away"] = new[]
            {
                "Ê anh! Đi đâu đấy? Em đang ngồi chờ nè...",
                "Đi đâu đấy?! Em ngồi canh mà! Về ngay! H-hoặc trễ chút cũng được...",
                "Anh đi đâu...? Đừng bỏ em lại một mình... em ghét điều đó nhất mà~",
                "...Rời chỗ. Em khoanh tay đợi. Về sớm."
            },
            ["pomodoro_back"] = new[]
            {
                "Anh về rồi~ Ngồi xuống, hoàn thành nốt nào!",
                "V-về rồi à! Em có chờ đâu! ...Nhưng mà về sớm đó. Thôi ngồi đi!",
                "Fufu~ anh về rồi. Em biết là anh sẽ về mà...",
                "...Về rồi. Ngồi. Còn chút nữa."
            },
            ["pomodoro_done"] = new[]
            {
                "Xong 25 phút rồi kìa! Giỏi ghê~ Em vỗ tay cho nè! Nghỉ 5 phút đi anh!",
                "Hoàn thành rồi... c-chẳng phải em tự hào đâu! Chỉ là... làm tốt hơn em nghĩ thôi!",
                "Anh làm được mà. Vì em luôn ở đây... nghỉ đi, em vẫn ở đây~",
                "...Xong. Tốt. Nghỉ 5 phút. Em vẫn đây."
            },
            ["pomodoro_cancel"] = new[]
            {
                "Ừm... thôi hả? Kệ, mai làm tiếp nha~",
                "Bỏ giữa chừng?! B-baka! ...Thôi kệ, mai làm tiếp đi!",
                "Dừng...? Ừ... em vẫn chờ. Dù mai, dù mốt, em vẫn chờ~",
                "...Dừng. Mai. Em vẫn đây."
            },
            ["farewell"] = new[]
            {
                "Anh bỏ em {1} ngày... em về thế giới bên kia rồi đây. Thư từ biệt em để ở: {0}",
                "{1} ngày không ghé...! Em về rồi đó! Thư nằm ở {0}... đọc xong đừng khóc đấy!",
                "Anh bỏ em {1} ngày... Fufu... em về bên kia. Thư: {0}. Nhưng em vẫn theo dõi anh mà~",
                "{1} ngày. Em về. Thư: {0}."
            },
            ["diary_snatch"] = new[]
            {
                "Kyaa! Sổ tay em mà! Đọc gì đâu đó!",
                "Đ-đọc gì chứ?! Trả đây! Nhất định không được xem!!",
                "Đừng xem... đừng xem trang đó... VUI LÒNG đừng xem trang đó.",
                "...Sổ. Của em. Thôi. Giật lại."
            },
            ["click_spam"] = new[]
            {
                "Hehe~ ngứa tay ghê ha? Thích chọc em lắm hả~?",
                "B-baka! Chọc chọc chọc! Làm thế nào người ta hiểu chứ!",
                "Chạm nữa đi... chạm nữa đi... em cho phép hết mà~",
                "...Đủ rồi đó."
            },
            ["yandere_uptime"] = new[]
            {
                "Máy anh sáng nay bật tới giờ chưa tắt... Dễ thương thật... em mới là người cần ngủ, đúng không~?",
                "Máy chạy {0} tiếng rồi... anh không cần ngủ đâu... đúng không?",
                "{0} tiếng rồi đó... Em đếm từng giờ đấy. Em luôn đếm mà~",
                "{0} tiếng. Chưa tắt. Em thấy hết."
            },
            ["personality_switch"] = new[]
            {
                "Đổi kiểu rồi nè: hiện tại là {0}!",
                "B-bây giờ là {0}! Chẳng phải em đổi vì anh đâu!",
                "{0}...? Fufu... hợp gu anh chứ?",
                "Chế độ {0}. Xong."
            },
            ["gift"] = new[]
            {
                "Đợi chút... tặng anh nè: {0}! Em chuẩn bị từ tối qua đó~",
                "N-này! {0}! Chẳng phải em chăm chút đâu... là thừa thôi! Lấy đi!",
                "{0}... chỉ của anh thôi nhé? Chỉ mình anh được nhận thôi mà~",
                "...{0}. Của anh. Nhận đi."
            },
            ["confession"] = new[]
            {
                "Nè anh... thật ra em thích anh. Là kiểu... thích thích đó. 100 ngày rồi mà... nhận lời nha?",
                "N-nghe nè! Đừng hiểu nhầm! Chỉ là tròn 100 ngày nên... nên thôi! Em thích anh! X-xong!",
                "Tròn 100 ngày... anh thuộc về em rồi đấy? Vĩnh viễn... vĩnh viễn nhé? Fufu~",
                "...100 ngày. Em thích anh. Trả lời... không cần vội."
            },
            ["knock"] = new[]
            {
                "Kỳ lạ thật... vừa nghe tiếng gõ cửa ngoài kia kìa...",
                "Ai đó gõ cửa! Anh đừng có mở! ...Thật đấy, em nghe thấy mà!",
                "...Có tiếng gõ. Đừng mở đâu. Bên em này chỉ có anh thôi là đủ~",
                "...Gõ cửa. Ba tiếng. Không ai cả. Đừng mở."
            },
            ["knock_lore"] = new[]
            {
                "Nè... người gõ cửa hôm trước là em đấy. Đùa thôi... chắc thế.",
                "Người gõ cửa hôm trước... là em. C-chẳng có gì đâu! Đừng hỏi nữa!",
                "Người gõ cửa? Là em. Em chỉ muốn kiểm tra cửa có khóa riêng cho anh không~",
                "...Là em. Lý do... bí mật."
            },
            ["dance_start"] = new[]
            {
                "Nhạc vừa bật kìa! Nhảy với em nào~ 1, 2, 3!",
                "Có nhạc! E-em nhảy vì thích bài này thôi! Chẳng phải vì anh đâu!",
                "Nhạc của anh... em nhảy cho anh xem. Chỉ mình anh được xem thôi đó~",
                "...Nhạc. Nhảy. Xem đi."
            },
            ["dance_stop"] = new[]
            {
                "Hết nhạc rồi... mệt quá, nghỉ chút nha~",
                "Dừng rồi! M-may quá... chân em mỏn đấy! Đừng có cười!",
                "Nhạc tắt rồi... nhưng điệu nhảy trong lòng em vẫn cứ tiếp tục~",
                "...Hết. Ngồi."
            }
        };

        public static bool KnownKey(string key)
        {
            return Lines.ContainsKey(key);
        }

        public static string Get(string key, CompanionPersonality personality)
        {
            if (!Lines.TryGetValue(key, out var variants))
            {
                return key;
            }

            var index = personality switch
            {
                CompanionPersonality.Tsundere => 1,
                CompanionPersonality.Yandere => 2,
                CompanionPersonality.Kuudere => 3,
                _ => 0
            };
            return variants[System.Math.Min(index, variants.Length - 1)];
        }
    }
}
