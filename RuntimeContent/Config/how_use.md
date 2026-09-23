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
- `chatterMinSeconds`, `chatterMaxSeconds`: khoảng nghỉ ngẫu nhiên giữa hai câu thoại.
- `chatterVolume`: âm lượng riêng của lời nói ngẫu nhiên, từ `0` đến `1`.
- `speechBubbleEnabled`: bật/tắt bong bóng thoại trên đầu nhân vật.
- `speechBubbleScale`: tỷ lệ kích thước bong bóng thoại.
- `speechTextCharactersPerSecond`: tốc độ chữ hiện dần trong bong bóng.

Ví dụ đặt tâm cửa gần góc phải dưới:

```json
"doorAnchorViewportX": 0.831,
"doorAnchorViewportY": 0.293
```

Khi đổi hai tọa độ này, cửa, nút gọi cửa và điểm nhân vật bước ra/đi vào sẽ di chuyển cùng nhau.

Thoát ứng dụng bằng `Ctrl+Shift+Q` hoặc nhấp chuột phải lên nhân vật/cửa.
