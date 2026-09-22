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
