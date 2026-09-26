# Cấu hình Anime Desktop Assistant

Sửa `desktop_layout.json`, lưu file rồi tắt và mở lại chương trình.

- `doorAnchorViewportX`: tọa độ ngang của tâm cửa, từ `0` (mép trái) đến `1` (mép phải).
- `doorAnchorViewportY`: tọa độ dọc của tâm cửa, từ `0` (mép dưới vùng làm việc) đến `1` (mép trên).
- `avatarStartViewportX`: vị trí ngang đầu tiên của nhân vật sau khi bước ra.
- `displayScale`: kích thước chung của cửa và nhân vật.
- `roamMinViewportX`, `roamMaxViewportX`: giới hạn đi lại theo chiều ngang.
- `roamNearDepth`, `roamFarDepth`: giới hạn đi gần/xa trong không gian 3D.
- `walkWorldUnitsPerSecond`, `runWorldUnitsPerSecond`: tốc độ đi/chạy.
- `recallButtonScale`: kích thước nút gọi cửa.
- `masterVolume`, `effectsVolume`, `voiceVolume`: âm lượng từ `0` đến `1`.
- `chatterEnabled`: bật/tắt lời nói ngẫu nhiên khi nhân vật ở ngoài.
- `chatterMinSeconds`, `chatterMaxSeconds`: khoảng nghỉ ngẫu nhiên giữa hai câu thoại (mặc định `9`–`22` giây).
- `chatterVolume`: âm lượng riêng của lời nói ngẫu nhiên, từ `0` đến `1`.
- `speechBubbleEnabled`: bật/tắt bong bóng thoại trên đầu nhân vật.
- `speechBubbleScale`: tỷ lệ đồng đều của cả đám mây thoại, chữ, viền và đuôi (`1.0` là kích thước nhỏ mặc định; khoảng `0.5` đến `2.0`).
- Bong bóng luôn hiện đúng ngôn ngữ đang nói: thoại Nhật hiện chữ Nhật, phản ứng Việt hiện chữ Việt; chữ được đồng bộ theo thời lượng âm thanh.
- Nhấp chuột trái vào nhân vật khi đang ở ngoài để nhân vật phản ứng bằng giọng Việt, nhảy cẫng và phát hiệu ứng pháo hoa.
- `speechTextCharactersPerSecond`: tốc độ dự phòng khi câu tương tác chưa có clip âm thanh; thoại có giọng sẽ tự đồng bộ theo thời lượng clip.

Ví dụ đặt tâm cửa gần góc phải dưới:

```json
"doorAnchorViewportX": 0.831,
"doorAnchorViewportY": 0.293
```

Khi đổi hai tọa độ này, cửa, nút gọi cửa và điểm nhân vật bước ra/đi vào sẽ di chuyển cùng nhau.

Thoát ứng dụng bằng `Ctrl+Shift+Q` hoặc nhấp chuột phải lên nhân vật/cửa.

## Tính năng "người đồng hành" (Companion)

Các hệ thống dưới đây chạy tự động khi nhân vật bước ra khỏi cửa. Dữ liệu gắn bó
lưu tại thư mục:

`%USERPROFILE%\AppData\LocalLow\Wibu Project\Anime Desktop Assistant\Companion\`

- `companion_profile.json` — sổ kỷ niệm: ngày cài app, số ngày bên nhau, nợ ngủ,
  số Pomodoro hoàn thành, tính cách hiện tại, nhật ký của cô ấy.
- `reminders.json` — danh sách lời nhắc. Lưu file là có hiệu lực ngay, không cần
  khởi động lại (hot-reload sau ~3 giây). Mỗi lời nhắc có 1 trong 3 dạng:
  - `dailyTime: "20:30"` — nhắc mỗi ngày lúc 20:30.
  - `repeatMinutes: 90` — nhắc theo chu kỳ (mặc định có sẵn lời nhắc uống nước).
  - `onceLocalTime: "2026-12-31 09:00"` — nhắc một lần duy nhất.
- `companion_settings.json` — công tắc riêng cho từng tính năng: `enabled`,
  `jealousyEnabled`, `tabSnoopingEnabled`, `sleepClockEnabled`, `pomodoroEnabled`,
  `remindersEnabled`, `diaryEnabled`, `festivalsEnabled` (tất cả bật mặc định).

Phím tắt (nhấn giữ `Alt`):

- `Alt+P` — bắt đầu/dừng Pomodoro 25 phút. Nhân vật ngồi cạnh canh; rời máy quá
  3 phút giữa phiên là bị nhắc, hoàn thành thì được vỗ tay kèm pháo hoa.
- `Alt+C` — xoay vòng tính cách: deredere → tsundere → yandere → kuudere. Mọi
  câu thoại đổi giọng theo tính cách đang chọn.
- `Alt+D` — mở thử cuốn "Nhật ký của Airi" (bình thường chỉ tự bay ra khi cô ấy
  giận: ghen, bị chọc liên tục...).

Phản ứng tự động:

- **Cơn ghen**: tiêu đề cửa sổ đang mở chứa dấu hiệu ảnh con gái khác (`.jpg`,
  `.png`, Photos, Instagram, Pixiv...) → liếc đi, mặt giận, ném cuốn nhật ký ra
  màn hình rồi giật lại.
- **Dòm tab**: thỉnh thoảng đếm số cửa sổ đang mở và ghẹo nếu mở hơn 8 cái.
- **Đồng hồ sinh học**: thức cùng cô ấy từ 23h sáng hôm sau sẽ tích "nợ ngủ";
  nợ cao là cô ấy ngáp, buồn ngủ. Ngủ đủ (nghỉ app vài ngày) là hồi phục.
- **Kỷ niệm**: ngày cài app, mốc 3/7/30/100/365 ngày... cô ấy tự nhắc lại.
- **Lễ hội Nhật**: 1/1 Năm mới, 2/14 Valentine, 20/3–10/4 Hanami (hoa anh đào
  rơi), 7/7 Tanabata, Halloween, Giáng sinh — kèm hạt hiệu ứng theo mùa.
- **Hồi Quy**: bỏ app đủ 7 ngày, lần ra sau cùng cô ấy để lại lá thư từ biệt
  trong `Desktop\Airi\Thu tu Airi.txt` rồi buồn bã về thế giới bên kia.

## Isekai, lễ tình nhân và nhảy theo nhạc

- **Thế giới bên kia**: sau khung cửa là bầu trời kỳ ảo tự sinh theo mùa
  (xuân hồng, hạ xanh, thu cam, đông trắng); lễ hội đổi màu riêng. Cửa mở là thấy.
- **Quà mỗi ngày**: lần gọi đầu tiên trong ngày cô ấy mang một món quà ra
  (trà, hoa, lá thư, bùa may mắn, sô-cô-la). Ngày bùa/sô-cô-la sẽ có file PNG
  thật trong `Desktop\Airi\`. Valentine 2/14 và White Day 3/14 luôn là sô-cô-la
  tay làm. Tròn **100 ngày** là ngày cô ấy tỏ tình (chỉ một lần duy nhất).
- **Người lạ gõ cửa**: từ 22h đến 3h thỉnh thoảng có tiếng gõ cửa bí ẩn
  (âm thanh tự sinh, không cần file audio). Sau lần gõ thứ hai sẽ có lời giải.
- **Nhảy theo nhạc**: khi nhạc đang phát trên máy (qua WASAPI loopback, không
  dùng micro, không ghi âm ra file), cô ấy tự nhảy theo nhịp với BPM ước lượng
  từ âm thanh; portal sau lưng bùng sáng theo beat, badge **● LIVE** hiện tên
  bài (lấy từ tiêu đề cửa sổ YouTube/Spotify...) và điệu đang nhảy.
- **Nhảy meme + demo không cần nhạc**: `Alt+N` bật/tắt nhảy thủ công ở 128 BPM.
  Bốn điệu bịa sẵn, đổi mỗi 8 beat: Lucky Star Wave, Renai Swing, Otagei Pump,
  Side Step. Motion data thật (Lucky Star, Renai Circulation...) có thể thay
  bảng procedural này về sau.
- **Lời tỏ tình**: chỉ xảy ra ở ngày tròn 100 ngày, kèm pháo hoa.
