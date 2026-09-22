# Windows overlay native boundary

The native plugin is intentionally not implemented in M0. M4 will place only
transparent-window, hit-test, z-order, monitor-work-area, and DPI code here.
It must never inject or hook `explorer.exe`, `dwm.exe`, DirectX, or any other process.

