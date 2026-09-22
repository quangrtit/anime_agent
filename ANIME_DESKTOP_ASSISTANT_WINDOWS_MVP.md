# Anime Desktop Assistant — Windows MVP

## Mục tiêu hiện tại

Ứng dụng là một overlay 3D trong suốt trên Windows:

- Một cánh cửa fantasy xuất hiện sát góc phải dưới, ngay phía trên taskbar.
- Vùng trong suốt cho phép thao tác với ứng dụng bên dưới.
- Click cửa hoặc nhấn `Space` để gọi nhân vật ra hay cho nhân vật trở vào.
- Nhân vật nhìn về phía người dùng khi đứng yên và quay đúng hướng khi di chuyển.
- Nhấp chuột phải lên cửa/nhân vật hoặc nhấn `Ctrl+Shift+Q` để thoát.

## Bố cục desktop

- Cửa được neo theo Windows work area, không dùng tọa độ pixel cố định.
- Mép phải cửa cách mép phải màn hình khoảng 3,5 cm theo DPI thực tế.
- Chân cửa cách mép trên taskbar khoảng 2 cm theo DPI thực tế.
- Cửa và nhân vật có tỷ lệ hiển thị bằng `2/3` bản prototype cũ.
- Overlay luôn phủ work area nhưng chỉ chặn chuột tại vùng đang có cửa hoặc nhân vật.
- Mặt cửa xoay theo camera để không bị nghiêng khi nằm ở mép màn hình.
- Phải hoạt động đúng khi thay đổi độ phân giải, tỷ lệ màn hình và vị trí taskbar.

## Trình tự cửa và nhân vật

### Đi ra

1. Tay nắm phản hồi.
2. Hai cánh cửa mở đủ rộng.
3. Portal phát sáng.
4. Nhân vật bước qua giữa cửa, không xuyên cánh cửa.
5. Nhân vật đi ra vị trí bên trái cửa rồi nhìn về người dùng.
6. Cửa đóng, tự ẩn và được thay bằng một nút gọi nhỏ ở góc phải dưới.

### Hoạt động ngoài desktop

Nhân vật tự chọn hành vi:

- đứng thở và nhìn quanh;
- vẫy tay hoặc chỉnh tóc;
- giãn cơ;
- đi bộ hoặc chạy đến một vị trí khác;
- di chuyển trong vùng ngang từ khoảng 6% đến 86% chiều rộng work area;
- đi sâu vào không gian phối cảnh, nhỏ dần khi ra xa và lớn dần khi lại gần.

Mọi animation phải chuyển tiếp mềm. Với Unity-Chan, ưu tiên clip gốc cùng rig;
với VRM tùy chỉnh, dùng thư viện Humanoid và procedural fallback.

### Trở vào

1. Người dùng click nút gọi để hiện lại cửa.
2. Nhân vật quay người tự nhiên về phía cửa.
3. Nhân vật đi tới ngưỡng cửa, không trượt lùi.
4. Cửa mở trước khi nhân vật đi qua.
5. Nhân vật đi sâu vào sau khung cửa.
6. Portal tắt và cửa đóng.

## Nhân vật

- Mặc định: Unity-Chan với các clip idle, walk, run, gesture, jump, stretch và win.
- Có thể thay bằng VRM 0.x/1.0 mà không cần build lại.
- File được chọn nằm trong `Characters/active_character.txt`.
- Giá trị `embedded` dùng nhân vật mặc định.
- Script thay nhân vật: `Tools/Characters/Set-ActiveCharacter.ps1`.

## Cấu hình ngoài

`Config/desktop_layout.json` nằm cạnh file EXE và cho phép chỉnh không cần build lại:

- tỷ lệ cửa/nhân vật;
- khoảng cách cửa với mép phải và taskbar;
- vùng ngang và chiều sâu mà nhân vật được di chuyển;
- tốc độ đi bộ/chạy;
- thời gian cửa thu gọn và kích thước nút gọi cửa.

## Kiến trúc

- Unity 6 LTS, URP và C#.
- `Domain`: state machine và quy tắc chuyển trạng thái.
- `Presentation`: cửa, nhân vật, animation và VFX.
- `Platform`: transparent window, work area, topmost và selective click-through.
- Native Windows code chỉ quản lý cửa sổ; không hook hoặc inject vào process khác.

## Tiêu chí hoàn thành

- Build Windows x64 thành công, không có lỗi compiler.
- Nền trong suốt và vùng rỗng click-through.
- Cửa nằm đúng góc phải dưới phía trên taskbar.
- Cửa và nhân vật nhỏ hơn prototype cũ 1,5 lần.
- Nhân vật có thể đi hoặc chạy qua nhiều vị trí trên màn hình.
- Không có T-pose, trượt chân nghiêm trọng, quay lưng sai hướng hoặc xuyên cửa.
- Click liên tục không phá state machine.
- Bản portable chạy được khi chép nguyên thư mục sang máy Windows khác.

## Chạy và build

Mở project bằng Unity rồi chạy scene:

`Assets/_Project/Scenes/DesktopPetPrototype.unity`

Build bản Windows:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\Tools\Build\Build-Windows.ps1
```

Đầu ra:

- `Builds/Windows/AnimeAssistant.exe`
- `Builds/AnimeAssistant-Windows-x64-Portable.zip`
