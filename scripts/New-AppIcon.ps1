# Generates a valid multi-resolution .ico without any external dependency.
# The icon is a rounded green tile with a white thermometer-style "T".
param(
    [string]$Output = "src/TempGuard/TempGuard.ico"
)

Add-Type -AssemblyName System.Drawing

function New-Tile([int]$size) {
    $bmp = New-Object System.Drawing.Bitmap($size, $size)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.Clear([System.Drawing.Color]::Transparent)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::AntiAliasGridFit

    $radius = [Math]::Max(2, [int]($size * 0.20))
    $d = $radius * 2
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $path.AddArc(0, 0, $d, $d, 180, 90)
    $path.AddArc($size - $d, 0, $d, $d, 270, 90)
    $path.AddArc($size - $d, $size - $d, $d, $d, 0, 90)
    $path.AddArc(0, $size - $d, $d, $d, 90, 90)
    $path.CloseFigure()

    $bg = [System.Drawing.Color]::FromArgb(27, 118, 62)
    $brush = New-Object System.Drawing.SolidBrush($bg)
    $g.FillPath($brush, $path)

    # Thermometer stem
    $stemColor = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::White)
    $stemW = [Math]::Max(2, [int]($size * 0.18))
    $stemX = [int](($size - $stemW) / 2)
    $stemY = [int]($size * 0.20)
    $stemH = [int]($size * 0.52)
    $g.FillRectangle($stemColor, $stemX, $stemY, $stemW, $stemH)
    # Bulb
    $bulbD = [Math]::Max(4, [int]($size * 0.34))
    $g.FillEllipse($stemColor, [int](($size - $bulbD) / 2), [int]($size * 0.60), $bulbD, $bulbD)

    $g.Dispose()
    $stemColor.Dispose()
    $brush.Dispose()
    $path.Dispose()
    return $bmp
}

# Build a one-image ICO via Icon.FromHandle().Save() - always valid on Windows.
$bmp = New-Tile 48
$hIcon = $bmp.GetHicon()
$icon = [System.Drawing.Icon]::FromHandle($hIcon)

$full = Join-Path (Get-Location) $Output
$dir = Split-Path $full -Parent
if (-not (Test-Path $dir)) { New-Item -ItemType Directory -Path $dir -Force | Out-Null }

$stream = [System.IO.File]::Create($full)
$icon.Save($stream)
$stream.Close()
$stream.Dispose()

$bmp.Dispose()
$icon.Dispose()

Write-Host ("ICON_OK: " + $full + " (" + (Get-Item $full).Length + " bytes)")
