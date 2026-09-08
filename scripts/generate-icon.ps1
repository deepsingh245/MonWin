<#
.SYNOPSIS
    Generates Assets/app.ico: a small rounded-square glyph with ascending bars,
    matching the app's minimal Windows 11 aesthetic. Run once (or whenever the
    icon design changes) - the output is committed as a binary asset.

    Uses System.Drawing's own Icon.Save (via Bitmap.GetHicon) rather than a
    hand-rolled ICO writer, since that's a well-tested path guaranteed to produce
    a file the Windows resource compiler (csc /win32icon) accepts.
#>
Add-Type -AssemblyName System.Drawing

$size = 128
$accent = [System.Drawing.Color]::FromArgb(255, 0, 103, 192)   # MonWin.Accent (#0067C0)
$dark   = [System.Drawing.Color]::FromArgb(255, 32, 32, 32)    # near-black rounded square

$bmp = New-Object System.Drawing.Bitmap $size, $size, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
$g.Clear([System.Drawing.Color]::Transparent)

$radius = [int]($size * 0.22)
$rect = New-Object System.Drawing.Rectangle 0, 0, ($size - 1), ($size - 1)
$d = $radius * 2

$path = New-Object System.Drawing.Drawing2D.GraphicsPath
[void]$path.AddArc($rect.X, $rect.Y, $d, $d, 180, 90)
[void]$path.AddArc($rect.Right - $d, $rect.Y, $d, $d, 270, 90)
[void]$path.AddArc($rect.Right - $d, $rect.Bottom - $d, $d, $d, 0, 90)
[void]$path.AddArc($rect.X, $rect.Bottom - $d, $d, $d, 90, 90)
$path.CloseFigure()

$bgBrush = New-Object System.Drawing.SolidBrush $dark
$g.FillPath($bgBrush, $path)

$barBrush = New-Object System.Drawing.SolidBrush $accent
$margin = $size * 0.24
$gap = $size * 0.08
$barWidth = ($size - 2 * $margin - 2 * $gap) / 3
$baseline = $size - $margin
$heights = @(0.30, 0.55, 0.40)
for ($i = 0; $i -lt 3; $i++) {
    $h = $size * $heights[$i]
    $x = $margin + $i * ($barWidth + $gap)
    $y = $baseline - $h
    $barRect = New-Object System.Drawing.RectangleF($x, $y, $barWidth, $h)
    $g.FillRectangle($barBrush, $barRect)
}

$g.Dispose()

$outPath = Join-Path $PSScriptRoot "..\src\SystemMonitor\Assets\app.ico"
$hIcon = $bmp.GetHicon()
$icon = [System.Drawing.Icon]::FromHandle($hIcon)

$fs = [System.IO.File]::Open($outPath, [System.IO.FileMode]::Create)
$icon.Save($fs)
$fs.Flush()
$fs.Close()

$icon.Dispose()
$bmp.Dispose()

$writtenSize = (Get-Item $outPath).Length
Write-Output "Wrote $outPath ($writtenSize bytes)"
