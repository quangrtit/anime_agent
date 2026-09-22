# Công cụ dự án

- Unity `6000.0.75f1`: scene, animation, URP và Windows player.
- UniVRM `0.131.2`: nạp VRM 0.x/1.0 ở runtime.
- Blender: chỉnh mesh, rig hoặc animation khi cần.
- PowerShell: restore dependency, đổi nhân vật, kiểm tra và build.

## Lệnh thường dùng

Khôi phục dependency:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\Tools\Dependencies\Restore-Dependencies.ps1
```

Kiểm tra project:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\Tools\AssetValidation\Validate-Project.ps1 -RequireRestoredDependencies
```

Build Windows:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\Tools\Build\Build-Windows.ps1
```
