# Anime Desktop Assistant — Sưu tập ý tưởng tính năng

> Brainstorm các tính năng vượt trội để thu hút anh em yêu thích anime.
> Tiêu chí: đánh vào tâm lý otaku, đa dạng, cho phép những ý tưởng viễn tưởng.
> Mỗi ý tưởng được đánh dấu mức khả thi trên kiến trúc hiện có:
> ✅ = khả thi ngay · 🔶 = cần effort vừa · 🔮 = viễn tưởng (làm prototype được)
>
> **Trạng thái 2026-09-26:** toàn bộ 10 mục ✅ + 7 mục 🔶 của nhóm 1/2/3 đã được triển khai bản happy case
> (xem phần ghi chú từng mục + `RuntimeContent/Config/how_use.md` → "Tính năng
> người đồng hành"). Điều khiển nhanh: `Alt+P` Pomodoro, `Alt+C` đổi tính cách,
> `Alt+D` xem thử nhật ký; dữ liệu lưu trong `Companion/` của persistentDataPath.

---

## 1. Gắn bó cảm xúc / "Waifu thật sự" (hook mạnh nhất)

### ✅ Kỷ niệm chung (Memory Album)
> **Đã làm:** kịch bản ✅ — lưu `firstMetUtc`, tự nói mốc 1/3/7/30/100/365 ngày (`CompanionProfile.IsAnniversaryDay`), kèm dòng thoại theo 4 tính cách.

Nhân vật nhớ các "mốc quan trọng" của hai người — ngày cài app, đêm bạn làm overtime mà cô ấy thức cùng, lần đầu nói "Airi mở máy tính" — và tự nhắc lại: *"Hôm nay đúng 100 ngày anh cài em đó~"*. Đánh vào nỗi cô đơn và khát vọng được ghi nhớ.

### ✅ Cơn ghen
> **Đã làm:** kịch bản ✅ — `DesktopActivityProbe` đọc tiêu đề cửa sổ foreground mỗi 2s; khớp từ khóa ảnh (`DesktopSignals.IsImageRelatedTitle`) → liếc đi (`RequestLookAway`), mặt giận (VRM expression Angry), mở cuốn nhật ký, cooldown 12 phút.

Mở file ảnh 2D/ảnh cô gái khác → nhân vật liếc sang, mặt tối sầm, speech bubble: *"...Anh xem ai thế?"*. Càng không hợp lý càng khiến anh em cười sướng rơn.

### ✅ Chế độ tsundere / yandere / kuudere
> **Đã làm:** kịch bản ✅ — `Alt+C` xoay vòng 4 tính cách (kể cả deredere), toàn bộ bảng thoại đổi giọng (`CompanionLines`); yandere + uptime ≥ 36h → câu tối + vignette đỏ nhẹ.

Personality switch:
- **Tsundere**: gõ lệnh sai liên tục → *"B-baka! Làm thế nào người ta hiểu chứ!"*
- **Yandere**: không tắt máy 36 tiếng → màn hình tối dần, một đôi mắt đỏ hiện ra: *"Anh không cần ngủ đâu... đúng không?"*

Meme sẵn có, làm chuẩn là viral.

### 🔶 Lời tỏ tình & mùa lễ tình nhân
> **Đã làm (kịch bản ✅):** Valentine 2/14 + White Day 3/14 có lời chào riêng và quà override là sô-cô-la tay làm; mốc **100 ngày** là ngày tỏ tình — thoại theo 4 tính cách + pháo hoa, chỉ xảy ra đúng một lần (`GiftCalendar.IsConfessionDay`).

Sự kiện Valentine/White Day, nhân vật tự chuẩn bị "sô-cô-la tay làm" (animation + item). Otaku cực kỳ quan tâm dịp này vì họ *không có* ai tặng ngoài nhân vật 2D.

### ✅ Đồng hồ sinh học thật
> **Đã làm:** kịch bản ✅ — mỗi phút ở ngoài trong khung 23h–5h được tính nợ ngủ (tối đa 10h); nợ ≥ 150 phút → ngáp (behaviour Stretch) + câu buồn ngủ; nghỉ app vài ngày tự trả nợ.

Nhân vật biết mình "thức cùng anh đến 3h sáng" — tích lũy sleep debt, có ngày buồn ngủ, ngáp, dựa tường. Tạo cảm giác cô ấy *sống chung hậu quả* với bạn.

---

## 2. Isekai / Portal (tận dụng tài sản sẵn có nhất: cánh cửa)

### 🔶 Cửa isekai có "thế giới bên kia"
> **Đã làm (kịch bản ✅):** `IsekaiPortalBackdrop` — quad texture procedural gắn sau khung cửa: gradient trời + sao/cánh hoa theo mùa (xuân hồng, hạ xanh, thu cam, đông trắng), lễ hội override màu; cửa mở hoặc đã collapse là thấy "thế giới bên kia".

Hé thấy hậu cảnh kỳ ảo phía sau khung cửa (lâu đài, pháo hoa, tuyết rơi) thay đổi theo mùa. Chi phí thấp — chỉ là plane texture động — nhưng cảm giác "cô ấy đến từ thế giới khác" là thứ otaku muốn có.

### 🔶 Nhân vật mang vật phẩm "từ thế giới kia" ra
> **Đã làm (kịch bản ✅):** mỗi ngày lần gọi đầu tiên cô ấy mang 1 món (`GiftCalendar`: trà/hoa/lá thư/bùa/sô-cô-la, lễ override), icon billboard nổi trước người 8 giây; ngày bùa/sô-cô-la **xuất file PNG thật** vào `Desktop\Airi\` (Bua may man yyyy-MM-dd.png).

Sáng ra cửa mở, cô ấy bưng ra một ly trà/bó hoa/lá thư viết tay. Mỗi ngày một món ngẫu nhiên theo mood. Item có thể là file PNG "bùa may mắn" đặt lên desktop.

### 🔶 Người lạ gõ cửa
> **Đã làm (kịch bản ✅, nén lại):** 22h–3h có xác suất nghe tiếng gõ (âm thanh knock tự sinh runtime, không cần file) + thoại rùng mình, cooldown 2 ngày; sau lần gõ thứ 2, lần ra sau cùng có câu lore "người gõ cửa là em đấy... chắc thế".

Thi thoảng có tiếng gõ cửa lúc nửa đêm. Mở ra... không ai. Rồi một tuần sau một nhân vật mới xuất hiện kèm câu chuyện. Tạo lore khiến người dùng mở app hằng ngày để xem "cốt truyện desktop" tiếp diễn.

### ✅ "Hồi Quy" (return to home world)
> **Đã làm:** kịch bản ✅ — `DaysSinceLastSeen ≥ 7` → viết `Desktop\Airi\Thu tu Airi.txt`, lời chào buồn + VRM expression Sorrow, chặn lặp lại 1 lần/ngày.

Lâu không dùng app, nhân vật buồn rồi trở về thế giới bên kia, để lại lá thư từ biệt trên desktop. Cơ chế retention tàn nhẫn nhất từng có (Neopets đã dùng kiểu này).

---

## 3. Idol / Vocaloid / Nhạc anime

### 🔶 Nhảy theo nhạc đang mở (audio-reactive dance) — **đã chọn triển khai**
> **Đã làm (kịch bản ✅):** `AudioMeterProbe` dùng **WASAPI loopback peak meter** (COM interop nhỏ, không micro, không ghi âm) phát hiện nhạc + ước lượng BPM bằng onset energy; nhảy tự bật khi có nhạc, tự dừng 4s sau khi im; portal bùng hạt theo từng beat. Đã kiểm chứng: WAV 128 BPM qua loa → log bắt đúng 128 BPM và tự nhảy.

Bắt âm thanh hệ thống qua WASAPI loopback (không cần micro, không ghi âm), phân tích beat/emergy local, cho nhân vật nhảy bám theo nhịp cùng hiệu ứng portal phát sáng theo beat.

### 🔶 Buổi live mini
> **Đã làm (kịch bản ✅):** badge **● LIVE** góc phải trên khi đang nhảy: tên bài rút từ tiêu đề cửa sổ media (YouTube/Spotify/...), BPM trực tiếp, tên điệu đang nhảy, nhấp nháy theo nhịp; portal sau lưng bùng beat như đèn sân khấu.

Nhân vật cầm mic nhảy trên "sân khấu" là taskbar, portal sau lưng là đèn sân khấu, tên bài hát hiện lên như thật. Nhạc lấy từ bài đang phát trên máy.

### 🔮 Karaoke cùng nhau
Lyric chạy đáy màn hình, nhân vật hát (TTS có pitch), bạn hát theo, nhân vật chấm điểm anime-style "PERFECT!!". Chế độ 2 người: nhân vật hát nhầm chữ nào bạn sửa chữ đó.

### 🔶 Nhảy meme anime
> **Đã làm (kịch bản ✅, motion data bịa):** `FabricatedDanceMoves` — 4 điệu procedural tên meme (Lucky Star Wave, Renai Swing, Otagei Pump, Side Step) tự đổi mỗi 8 beat; chưa có nhạc vẫn demo được bằng **Alt+N** (128 BPM fabricate). Chờ tải motion data thật để thay.

Khi phát hiện nhạc là bài hot (và nếu bạn tải motion data), tự nhảy Lucky Star dance, Renai Circulation... Motion từ anime thật — kho meme văn hóa cực lớn.

---

## 4. Thu thập / Gacha / Sưu tầm (hook độ hiếm)

### 🔶 Phim ảnh "chụp lén"
Nhân vật thi thoảng có khoảnh khắc hiếm (ngủ gật, hắt hơi, soi gương) — app tự chụp thành Polaroid dán vào góc màn hình. Đủ hiếm để anh em lên mạng khoe bộ sưu tập.

### 🔶 Gacha "quà từ thế giới kia"
Mỗi tuần quay 1 lần miễn phí bằng "điểm đồng hành" (điểm chăm sóc nhân vật). Trúng phụ kiện (nơ, kính, váy), pet nhỏ, hoặc *animation hiếm*. Chỉ bán bằng thời gian ân cần, không bán bằng tiền — chống pay-to-win, vẫn chạm cơn nghi gacha.

### ✅ Sổ tay tsundere
> **Đã làm:** kịch bản ✅ — nhật ký tự sinh từ sự kiện thật (ghen, hoàn thành Pomodoro, kỷ niệm...); khi cô ấy giận hoặc `Alt+D`, cuốn sổ trượt vào góc màn hình hiện 1 dòng rồi bị giật lại.

Một cuốn "Nhật ký của Airi" mà bạn *không được xem* — chỉ được xem khi cô ấy giận, cô ấy ném nó ra giữa màn hình rồi vụt lấy lại. Càng không được xem càng muốn xem.

---

## 5. Đồng hành năng suất (hook "wibu cũng phải sống")

### ✅ Pomodoro có waifu canh
> **Đã làm:** kịch bản ✅ — `Alt+P` bật/tắt, `PomodoroMachine` 25/5 phút; rời máy quá 3 phút (GetLastInputInfo) → bị nhắc; hoàn thành → vỗ tay + pháo hoa portal + đếm vào profile; chip đồng hồ ở góc trái dưới.

25 phút tập trung, cô ấy ngồi vẽ bên cạnh; rời máy quá lâu → cô ấy đứng dậy khoanh tay chờ. Phiên hoàn thành → cô ấy vỗ tay. Giống Forest nhưng có linh hồn — cơ chế kéo app chạy cả ngày làm việc thật.

### 🔶 "Cùng thức" qua đêm
Mode học thi/quay deadline — cô ấy kéo ghế ngồi cạnh, nói chuyện nhỏ, tự pha cà phê (animation), và *cô cũng ngủ gật* khi bạn quay lại chưa xong.

### ✅ Lời nhắc như người thân
> **Đã làm:** kịch bản ✅ — `reminders.json` hot-reload, hỗ trợ nhắc hằng ngày (`dailyTime`), chu kỳ (`repeatMinutes`, mặc định có lời nhắc uống nước) và một lần (`onceLocalTime`); kèm bảng gỗ hiện 18 giây trên màn hình.

"Lịch phỏng vấn 9h" → 8h30 cô ấy đứng trước màn hình cầm bảng nhắc. Nhắc gọi mẹ, nhắc uống nước. Thương cảm > tiện dụng.

---

## 6. Va chạm "2D ↔ 3D" (meme & kịch tính)

> **Đã làm thêm (kịch bản ✅):** lớp kính icon — glass pane phủ vùng icon, chặn double-click,
> nhân vật bay vào icon (0.75× icon, dock 1s) rồi mới forward cú click về Windows.


### 🔶 Chuunibyou mode
Cả desktop biến thành "phong ấn" — icon được đánh "con dấu", nhân vật chử chú tay, portal đổi thành cổng địa ngục tím. Không tác dụng gì ngoài ngầu — và đó chính là điểm bán hàng.

### 🔶 Băng qua rào 2D
Giật mình khi bạn chạm chuột vào cô ấy — lùi lại, quay mặt... rồi dần quen. Tiến trình "được chạm đầu" sau 30 ngày là event cả cộng đồng sẽ bàn tán.

### ✅ Thấy được tab của bạn (chế độ dòm)
> **Đã làm:** kịch bản ✅ — `EnumWindows` đếm cửa sổ có tiêu đề; ≥ 8 cửa sổ và đúng thời điểm ngẫu nhiên → câu dòm theo tính cách, cooldown 45 phút.

Đi ngang qua, liếc xuống góc phải dưới: *"Hmm... 27 tab vẫn đang mở sao anh?"*. Rùng mình 1 lần, dùng cả đời.

---

## 7. Nostalgia & Văn hóa otaku sâu

### 🔶 Chế độ CRT 2004
Cả overlay pha grain, scanline như anime DVD cũ; nhân vật thỉnh thoảng "mất tín hiệu" rồi comeback. Vừa background đẹp, vừa meme, vừa tuổi thơ.

### ✅ Lễ hội Nhật theo mùa thật
> **Đã làm:** kịch bản ✅ — lịch lễ hội trong `CompanionFestival` (Năm mới, Valentine, Hinamatsuri, Hanami 20/3–10/4, Tanabata, Halloween, Giáng sinh, Omisoka) + hạt hiệu ứng theo mùa bay quanh nhân vật (cánh hoa hồng/xanh/vàng).

Tanabata (viết điều ước treo lên portal), Hanami (hoa anh đào rơi trên taskbar), Năm mới (cô ấy mở cửa chào với kimono). Otaku gần văn hóa này hơn lễ hội quê mình — tăng độ "thật" của thế giới nhân vật.

---

## 8. Viễn tưởng / Bất khả thi (chỉ để chiêu hồn, làm prototype được)

### 🔮 Song đạo diễn AI
Hai nhân vật AI-AI tự tán gẫu, tranh luận về việc bạn để máy 3 ngày chưa restart. Bạn "chen vào" là cả hai im bặt nhìn bạn.

### 🔮 Waifu đọc tâm trạng bằng giọng bạn
Phân tích tone giọng qua micro để nhận ra bạn đang buồn và chủ động im lặng ngồi cạnh — hoặc bật bài nhạc bạn hay nghe lúc buồn.

### 🔮 Cửa sổ "khoảng không"
Kéo một cửa sổ ứng dụng thật gần nhân vật → cô ấy "ngồi" lên mép cửa sổ đó, thả chân xuống, tựa vào thanh title. Ảo giác hoàn hảo rằng 2D đang sống trong hệ điều hành của bạn.

### 🔮 Wallpaper sống
Khi bạn mở app fullscreen làm việc, cô ấy lùi xuống đáy màn hình thành silhouette mờ — chỉ để "ở đó". Không làm gì, chỉ *hiện diện*. Là thứ nhiều người thật sự cần.

---

## Khuyến nghị lộ trình

| Nhóm | Lý do chọn trước |
|------|------------------|
| **1 — Kỷ niệm chung, tsundere, đồng hồ sinh học** | Gần như chỉ là state machine + speech bubble: rẻ nhất, khớp project nhất, đánh trúng cảm xúc nhất |
| **5 — Pomodoro waifu, lời nhắc người thân** | Kéo app chạy cả ngày → thời gian gắn bó tăng, nền cho mọi event khác |
| **2 — Portal isekai** | Tận dụng trực tiếp tài sản cánh cửa sẵn có của project |
| **3 — Nhảy theo nhạc + live mini** | Demo viral nhanh nhất, độc lập với các nhóm còn lại |

Quan hệ phụ thuộc: nhóm 1 là nền móng (mood + memory), nhóm 2/4/6 mọc trên đó; nhóm 3 và 5 chạy song song độc lập.
