. "C:\Users\hieu\Documents\GitHub\anime_agent\Tools\test_session\testlib.ps1"

function Press-Hold { param([int]$ms)
  [Win32]::mouse_event(0x0002, 0, 0, 0, [UIntPtr]::Zero)
  Start-Sleep -Milliseconds $ms
  [Win32]::mouse_event(0x0004, 0, 0, 0, [UIntPtr]::Zero)
}

Write-Output "=== D3: long-press test at icon A (Recycle Bin), hold 700ms ==="
Move-MouseTo 55 50
Start-Sleep -Milliseconds 400
Press-Hold 700
Start-Sleep -Seconds 2
Write-Output "--- log after hold 700ms ---"
Find-Log "Glass forwarded|Ritual|ritual|new process"

Write-Output "=== if nothing, try hold 1200ms ==="
Press-Hold 1200
Start-Sleep -Seconds 2
Find-Log "Glass forwarded|Ritual|ritual|new process"
Move-MouseTo 700 500
