# Anime Desktop Assistant

Overlay nhân vật anime 3D trong suốt dành cho Windows 10/11 x64.

## Chạy

Mở:

`Builds/Windows/AnimeAssistant.exe`

Điều khiển:

- Click cửa hoặc nhấn `Space`: gọi nhân vật ra/trở vào.
- Nhấp chuột phải lên cửa/nhân vật: thoát.
- `Ctrl+Shift+Q`: thoát ở bất kỳ vị trí chuột nào.

## Đổi VRM

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\Tools\Characters\Set-ActiveCharacter.ps1 -VrmPath "C:\path\character.vrm"
```

Để quay về nhân vật nhúng sẵn, ghi `embedded` vào:

`Builds/Windows/Characters/active_character.txt`

## Build

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\Tools\Build\Build-Windows.ps1
```

Project sử dụng Unity `6000.0.75f1`, URP `17.0.1` và UniVRM `0.131.2`.
Đặc tả hiện hành nằm tại `ANIME_DESKTOP_ASSISTANT_WINDOWS_MVP.md`.

## Tải bản chạy sẵn

Vào [GitHub Releases](https://github.com/quangrtit/anime_agent/releases/latest), tải
`AnimeAssistant-Windows-x64-Portable.zip`, giải nén toàn bộ rồi chạy `AnimeAssistant.exe`.

## Build và phát hành

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\Tools\Build\Build-Windows.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File .\Tools\Build\Build-SingleExe.ps1 -SkipUnityBuild
```

File ZIP để đưa lên Release được tạo tại:

`Builds/AnimeAssistant-Windows-x64-Portable.zip`

## Trợ lý giọng nói local

Thiết lập lần đầu (tải ASR và giọng nữ tiếng Việt khoảng 125 MB rồi build sidecar Windows):

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\Tools\Agent\Setup-VoiceAgent.ps1
```

Sau đó gọi nhân vật ra khỏi cửa, chờ câu báo sẵn sàng, nói từ đánh thức
`Airi` rồi nói lệnh. Agent không mở microphone khi nhân vật còn trong cửa.
Ví dụ:

- `Airi mở máy tính`
- `Airi tìm kiếm thời tiết Đà Nẵng`
- `Airi tăng âm lượng`
- `Airi chuyển bài`
- `Airi hiện desktop`
- `Airi tạo thư mục Báo cáo`
- `Airi tạo file Ghi chú chấm txt`
- `Airi mở thư mục Airi`
- `Airi mở thư mục Tải xuống`
- `Airi chuyển cửa sổ`
- `Airi thu nhỏ cửa sổ`

File và thư mục do giọng nói tạo ra chỉ nằm trong `Desktop\Airi`. Agent không
có lệnh xóa và không chấp nhận đường dẫn tùy ý bên ngoài khu vực này.

Nhận dạng giọng nói chạy local bằng CPU (1 thread), không dùng VRAM và không gửi
âm thanh ra internet. Agent chỉ thực thi các action trong danh sách cho phép tại
`Tools/AgentHost/commands.vi.json`; nó không sinh hay chạy shell command tùy ý.
Tùy chỉnh wake word, VAD và TTS trong `Tools/AgentHost/agent_settings.json`.

## Tính năng người đồng hành

Nhân vật nhớ "cuộc sống chung" với người dùng: kỷ niệm ngày cài app, đồng hồ
sinh học (thức khuya cùng bạn bị tích nợ ngủ), cơn ghen khi bạn mở ảnh 2D khác,
đếm cửa sổ để dòm, 4 tính cách deredere/tsundere/yandere/kuudere, cuốn nhật ký
chỉ được xem khi cô ấy giận, Pomodoro có waifu canh, lời nhắc hằng ngày và lễ hội
Nhật theo mùa (hanami, tanabata...). Bỏ app đủ lâu sẽ nhận được thư từ biệt.

Ngoài ra: sau khung cửa là "thế giới bên kia" đổi theo mùa, cô ấy mang quà ra
mỗi ngày (ngày đặc biệt có file PNG thật trong `Desktop\Airi\`), tỏ tình ở mốc
100 ngày, tiếng gõ cửa bí ẩn lúc nửa đêm, và **nhảy theo nhạc đang phát trên
máy** (WASAPI loopback, không micro) kèm badge live mini — demo được không cần
nhạc bằng `Alt+N`.

Điều khiển nhanh:

- `Alt+P`: bắt đầu/dừng Pomodoro 25 phút (nhân vật ngồi canh, hoàn thành được
  vỗ tay + pháo hoa).
- `Alt+C`: đổi tính cách.
- `Alt+D`: mở thử cuốn nhật ký.
- `Alt+N`: bật/tắt nhảy meme thủ công (128 BPM fabricate).

Chi tiết cấu hình (lời nhắc, công tắc từng tính năng) xem phần "Tính năng người
đồng hành" trong `RuntimeContent/Config/how_use.md`.

## Voice credit

Japanese character chatter: `VOICEVOX:猫使アル`.
Vietnamese click reaction: Microsoft `vi-VN-HoaiMyNeural`.
