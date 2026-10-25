# Renders the app mark (docs/design/icons/app-icon.svg) to a multi-size .ico with GDI+, no external tool.
# Sizes: 16, 20, 24, 32, 40, 48, 64, 256. Small sizes use whole-pixel gaps and no inner radius, as app-icon-16.svg does.
param([string]$Out = "$PSScriptRoot\..\spikes\hosting\HostingSpike\Assets\app.ico")

Add-Type -AssemblyName System.Drawing

function New-RoundedPath([float]$x, [float]$y, [float]$w, [float]$h, [float]$r) {
    $p = New-Object System.Drawing.Drawing2D.GraphicsPath
    if ($r -le 0) { $p.AddRectangle((New-Object System.Drawing.RectangleF $x, $y, $w, $h)); return $p }
    $d = $r * 2
    $p.AddArc($x, $y, $d, $d, 180, 90)
    $p.AddArc($x + $w - $d, $y, $d, $d, 270, 90)
    $p.AddArc($x + $w - $d, $y + $h - $d, $d, $d, 0, 90)
    $p.AddArc($x, $y + $h - $d, $d, $d, 90, 90)
    $p.CloseFigure()
    return $p
}

function Render-Mark([int]$size) {
    $bmp = New-Object System.Drawing.Bitmap $size, $size, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.Clear([System.Drawing.Color]::Transparent)
    $ground = [System.Drawing.ColorTranslator]::FromHtml("#2E3A4A")
    $pane = [System.Drawing.ColorTranslator]::FromHtml("#E9E4DA")
    $active = [System.Drawing.ColorTranslator]::FromHtml("#3FB8B8")
    $s = [float]$size
    if ($size -ge 32) {
        # 256-unit master: radius 56, panes at 40 and 136, 80 wide, radius 16
        $k = $s / 256
        $g.FillPath((New-Object System.Drawing.SolidBrush $ground), (New-RoundedPath 0 0 $s $s (56 * $k)))
        $tiles = @(@(40, 40, $active), @(136, 40, $pane), @(40, 136, $pane), @(136, 136, $pane))
        foreach ($t in $tiles) { $g.FillPath((New-Object System.Drawing.SolidBrush $t[2]), (New-RoundedPath ($t[0] * $k) ($t[1] * $k) (80 * $k) (80 * $k) (16 * $k))) }
    } else {
        # 16-unit pixel-aligned variant: radius 3.5, 1 px gaps
        $k = $s / 16
        $g.FillPath((New-Object System.Drawing.SolidBrush $ground), (New-RoundedPath 0 0 $s $s (3.5 * $k)))
        $tiles = @(@(3, 3, $active), @(8.5, 3, $pane), @(3, 8.5, $pane), @(8.5, 8.5, $pane))
        foreach ($t in $tiles) { $g.FillPath((New-Object System.Drawing.SolidBrush $t[2]), (New-RoundedPath ($t[0] * $k) ($t[1] * $k) (4.5 * $k) (4.5 * $k) (1 * $k))) }
    }
    $g.Dispose()
    $ms = New-Object System.IO.MemoryStream
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
    return ,$ms.ToArray()   # the comma keeps it a byte[]; PowerShell would otherwise unroll it
}

$sizes = @(16, 20, 24, 32, 40, 48, 64, 256)
$images = @{}
foreach ($z in $sizes) { $images[$z] = Render-Mark $z }

# ICO container: ICONDIR + ICONDIRENTRY per image + PNG payloads (PNG entries are supported since Windows Vista).
$outDir = Split-Path -Parent $Out
if (-not (Test-Path $outDir)) { New-Item -ItemType Directory -Path $outDir | Out-Null }
$fs = [System.IO.File]::Create($Out)
$w = New-Object System.IO.BinaryWriter $fs
$w.Write([uint16]0); $w.Write([uint16]1); $w.Write([uint16]$sizes.Count)
$offset = 6 + 16 * $sizes.Count
foreach ($z in $sizes) {
    $data = $images[$z]
    $w.Write([byte]($(if ($z -ge 256) { 0 } else { $z })))
    $w.Write([byte]($(if ($z -ge 256) { 0 } else { $z })))
    $w.Write([byte]0); $w.Write([byte]0)
    $w.Write([uint16]1); $w.Write([uint16]32)
    $w.Write([uint32]$data.Length); $w.Write([uint32]$offset)
    $offset += $data.Length
}
foreach ($z in $sizes) { $w.Write([byte[]]$images[$z]) }
$w.Flush(); $fs.Close()
"wrote $Out ($((Get-Item $Out).Length) bytes, sizes $($sizes -join ', '))"
