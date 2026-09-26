param([string]$src, [string]$dst, [int]$x, [int]$y, [int]$w, [int]$h, [int]$zoom = 4)
Add-Type -AssemblyName System.Drawing
$img = [System.Drawing.Bitmap]::FromFile($src)
$crop = New-Object System.Drawing.Bitmap ($w * $zoom), ($h * $zoom)
$g = [System.Drawing.Graphics]::FromImage($crop)
$g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::NearestNeighbor
$g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::Half
$g.DrawImage($img, (New-Object System.Drawing.Rectangle 0, 0, ($w * $zoom), ($h * $zoom)), (New-Object System.Drawing.Rectangle $x, $y, $w, $h), [System.Drawing.GraphicsUnit]::Pixel)
$g.Dispose()
$crop.Save($dst, [System.Drawing.Imaging.ImageFormat]::Png)
$img.Dispose(); $crop.Dispose()
Write-Output "cropped: $dst"
