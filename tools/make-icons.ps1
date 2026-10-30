# Renders the ElForecast icon in every size into ElForecast/wwwroot/icons:
# stacked price bars (spot price + tariffs, like the chart) under a yellow lightning bolt on a blue tile.
#   powershell -File tools\make-icons.ps1
Add-Type -AssemblyName System.Drawing

$out = Join-Path $PSScriptRoot "..\ElForecast\wwwroot\icons"
New-Item -ItemType Directory -Force $out | Out-Null

function New-RoundedRect([float]$x, [float]$y, [float]$w, [float]$h, [float]$r) {
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $d = $r * 2
    $path.AddArc($x, $y, $d, $d, 180, 90)
    $path.AddArc($x + $w - $d, $y, $d, $d, 270, 90)
    $path.AddArc($x + $w - $d, $y + $h - $d, $d, $d, 0, 90)
    $path.AddArc($x, $y + $h - $d, $d, $d, 90, 90)
    $path.CloseFigure()
    $path
}

function Color([string]$hex, [int]$alpha = 255) {
    $c = [System.Drawing.ColorTranslator]::FromHtml($hex)
    [System.Drawing.Color]::FromArgb($alpha, $c.R, $c.G, $c.B)
}

# The artwork is drawn on a 100 x 100 grid. $content shrinks it towards the centre (maskable
# icons must stay inside the safe zone); $rounded gives the background its own rounded corners
# (favicons and manifest icons); iOS and maskable icons are rounded by the OS.
function New-Icon([int]$size, [double]$content = 0.84, [bool]$rounded = $false) {
    $bmp = New-Object System.Drawing.Bitmap $size, $size
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = "AntiAlias"
    $g.Clear([System.Drawing.Color]::Transparent)

    $rect = New-Object System.Drawing.RectangleF 0, 0, $size, $size
    $bg = New-Object System.Drawing.Drawing2D.LinearGradientBrush $rect, (Color "#256abf"), (Color "#0d366b"), 90
    if ($rounded) {
        $g.FillPath($bg, (New-RoundedRect 0 0 $size $size ($size * 0.22)))
    } else {
        $g.FillRectangle($bg, $rect)
    }

    $s = $size / 100.0 * $content
    $off = ($size - 100 * $s) / 2
    $g.TranslateTransform([float]$off, [float]$off)
    $g.ScaleTransform([float]$s, [float]$s)

    # Bars: [x, spot height, tariff height]. The last two are muted, like forecast bars.
    $bars = @(@(14, 22, 12), @(31, 36, 14), @(48, 16, 12), @(65, 28, 12), @(82, 20, 12))
    $baseline = 82
    $i = 0
    foreach ($b in $bars) {
        $tariff = if ($i -ge 3) { "#a8779f" } else { "#e87ba4" }
        $spot = if ($i -ge 3) { "#6b98d4" } else { "#86b6ef" }
        $x = $b[0] - 6; $w = 12
        $spotTop = $baseline - $b[1]
        $top = $spotTop - $b[2]
        $g.FillPath((New-Object System.Drawing.SolidBrush (Color $tariff)), (New-RoundedRect $x $top $w ($b[2] + 4) 3))
        $g.FillRectangle((New-Object System.Drawing.SolidBrush (Color $spot)), [float]$x, [float]($spotTop + 1), [float]$w, [float]($b[1] - 1))
        $i++
    }
    $g.FillRectangle((New-Object System.Drawing.SolidBrush (Color "#cde2fb")), 5, $baseline, 90, 2.5)

    # The bolt, outlined in the background blue so it stands clear of the bars.
    $bolt = [System.Drawing.PointF[]]@(
        (New-Object System.Drawing.PointF 60, 6), (New-Object System.Drawing.PointF 34, 50),
        (New-Object System.Drawing.PointF 50, 50), (New-Object System.Drawing.PointF 42, 94),
        (New-Object System.Drawing.PointF 72, 42), (New-Object System.Drawing.PointF 56, 42),
        (New-Object System.Drawing.PointF 70, 6))
    $outline = New-Object System.Drawing.Pen (Color "#0d366b"), 7
    $outline.LineJoin = "Round"
    $g.DrawPolygon($outline, $bolt)
    $g.FillPolygon((New-Object System.Drawing.SolidBrush (Color "#fab219")), $bolt)

    $g.Dispose()
    $bmp
}

function Save-Png($bmp, [string]$name) {
    $bmp.Save((Join-Path $out $name), [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
}

# iOS home screen (opaque, square: iOS rounds the corners itself).
Save-Png (New-Icon 180) "apple-touch-icon.png"
Save-Png (New-Icon 167) "apple-touch-icon-167.png"
Save-Png (New-Icon 152) "apple-touch-icon-152.png"

# Web app manifest (Windows/Android install) and a big master copy.
Save-Png (New-Icon 1024) "icon-1024.png"
Save-Png (New-Icon 512 0.84 $true) "icon-512.png"
Save-Png (New-Icon 192 0.84 $true) "icon-192.png"
Save-Png (New-Icon 512 0.64) "icon-512-maskable.png"

# Browser tab.
Save-Png (New-Icon 32 0.92 $true) "favicon-32.png"

# favicon.ico with 16, 32 and 48 px PNG images, for Windows and older browsers.
$images = foreach ($size in 16, 32, 48) {
    $bmp = New-Icon $size 0.92 $true
    $ms = New-Object System.IO.MemoryStream
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
    [pscustomobject]@{ Size = $size; Bytes = $ms.ToArray() }
}
$ico = New-Object System.IO.MemoryStream
$wr = New-Object System.IO.BinaryWriter $ico
$wr.Write([uint16]0); $wr.Write([uint16]1); $wr.Write([uint16]$images.Count)
$offset = 6 + 16 * $images.Count
foreach ($img in $images) {
    $wr.Write([byte]$img.Size); $wr.Write([byte]$img.Size); $wr.Write([byte]0); $wr.Write([byte]0)
    $wr.Write([uint16]1); $wr.Write([uint16]32)
    $wr.Write([uint32]$img.Bytes.Length); $wr.Write([uint32]$offset)
    $offset += $img.Bytes.Length
}
foreach ($img in $images) { $wr.Write($img.Bytes) }
$wr.Flush()
[System.IO.File]::WriteAllBytes((Join-Path $out "..\favicon.ico"), $ico.ToArray())

Write-Host "Icons written to $out"
