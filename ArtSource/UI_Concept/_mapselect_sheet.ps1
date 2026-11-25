param([string]$OutPath = "C:\Users\hongj\CapstoneProject\Assets\Members\JJH\04_Assets\UI\Map_UI.png")
Add-Type -AssemblyName System.Drawing
$root = "C:\Users\hongj\CapstoneProject"
$srcSheet = New-Object System.Drawing.Bitmap "$root\ArtSource\UI_Concept\Map_UI_original_JJH.png"
$SW = 1920; $SH = 1080
$bmp = New-Object System.Drawing.Bitmap $SW, $SH, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
$gfx = [System.Drawing.Graphics]::FromImage($bmp)
$gfx.SmoothingMode = 'AntiAlias'; $gfx.InterpolationMode = 'HighQualityBicubic'; $gfx.PixelOffsetMode = 'HighQuality'

function Col($a, $cr, $cg, $cb) { [System.Drawing.Color]::FromArgb($a, $cr, $cg, $cb) }
function Diamond($cx, $cy, $rad) {
    $p = New-Object System.Drawing.Drawing2D.GraphicsPath
    $p.AddPolygon(@([System.Drawing.PointF]::new($cx, $cy-$rad), [System.Drawing.PointF]::new($cx+$rad, $cy), [System.Drawing.PointF]::new($cx, $cy+$rad), [System.Drawing.PointF]::new($cx-$rad, $cy)))
    return $p
}
function CutRect($x, $y, $w, $h, $cut) {
    $p = New-Object System.Drawing.Drawing2D.GraphicsPath
    $p.AddPolygon(@(
        [System.Drawing.PointF]::new($x, $y), [System.Drawing.PointF]::new($x+$w-$cut, $y),
        [System.Drawing.PointF]::new($x+$w, $y+$cut), [System.Drawing.PointF]::new($x+$w, $y+$h),
        [System.Drawing.PointF]::new($x+$cut, $y+$h), [System.Drawing.PointF]::new($x, $y+$h-$cut)))
    return $p
}
function Glow($path, $cr, $cg, $cb, $maxA, $widthMax) {
    for ($i = 8; $i -ge 1; $i--) {
        $pen = New-Object System.Drawing.Pen (Col ([int]($maxA * (9-$i) / 9)) $cr $cg $cb), ($widthMax * $i / 8)
        $pen.LineJoin = 'Round'; $gfx.DrawPath($pen, $path)
    }
}
# dark glyph from the original sheet -> tinted bitmap (centre cx,cy of the old badge)
function Glyph($cy, $cr, $cg, $cb) {
    $gb = New-Object System.Drawing.Bitmap 100, 100, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    for ($yy = 0; $yy -lt 100; $yy++) { for ($xx = 0; $xx -lt 100; $xx++) {
        $px = $srcSheet.GetPixel(1141 - 50 + $xx, $cy - 50 + $yy)
        $mx = [Math]::Max($px.R, [Math]::Max($px.G, $px.B))
        if ($mx -lt 66) { $gb.SetPixel($xx, $yy, (Col 255 $cr $cg $cb)) }
        elseif ($mx -lt 84) { $gb.SetPixel($xx, $yy, (Col ([int](255 * (84 - $mx) / 18)) $cr $cg $cb)) }
    } }
    return $gb
}

# --- card bars (image-space rects = unity rect flipped) ---
# name, unity y, unity height, old badge centre y (image space), colour
$bars = @(
    @{ y = 869; h = 172; gy = 125; c = @(200, 222, 238) },   # Map_UI_3 normal
    @{ y = 651; h = 172; gy = 343; c = @(240, 205, 115) },   # Map_UI_4 shop
    @{ y = 450; h = 173; gy = 543; c = @(110, 225, 205) },   # Map_UI_5 upgrade
    @{ y = 238; h = 172; gy = 757; c = @(185, 150, 255) },   # Map_UI_6 elite
    @{ y = 39;  h = 173; gy = 955; c = @(255, 115, 105) }    # Map_UI_7 boss
)
$bx0 = 1054; $bw = 711
foreach ($b in $bars) {
    $top = $SH - $b.y - $b.h            # image-space top of the rect
    $ar = $b.c[0]; $ag = $b.c[1]; $ab = $b.c[2]
    $ox = $bx0 + 6; $oy = $top + 14; $ow = $bw - 12; $oh = $b.h - 28
    $shape = CutRect $ox $oy $ow $oh 24
    $fill = New-Object System.Drawing.Drawing2D.LinearGradientBrush ([System.Drawing.PointF]::new($ox, 0)), ([System.Drawing.PointF]::new($ox + $ow, 0)), (Col 238 ([int]($ar*0.12+10)) ([int]($ag*0.12+20)) ([int]($ab*0.12+36))), (Col 190 8 16 30)
    $gfx.FillPath($fill, $shape)
    $gfx.SetClip($shape)
    $gfx.FillRectangle((New-Object System.Drawing.SolidBrush (Col 16 255 255 255)), $ox, $oy, $ow, ($oh / 2))
    $gfx.FillRectangle((New-Object System.Drawing.SolidBrush (Col 255 $ar $ag $ab)), $ox, $oy, 6, $oh)
    $gfx.ResetClip()
    $pen = New-Object System.Drawing.Pen (Col 235 $ar $ag $ab), 3; $pen.LineJoin = 'Round'
    $gfx.DrawPath($pen, $shape)
    # badge
    $cx = $bx0 + 90; $cy = $top + $b.h / 2
    $d = Diamond $cx $cy 76
    $gfx.FillPath((New-Object System.Drawing.SolidBrush (Col 255 9 18 32)), $d)
    Glow $d $ar $ag $ab 80 10
    $gfx.DrawPath((New-Object System.Drawing.Pen (Col 255 $ar $ag $ab), 4), $d)
    $gfx.DrawPath((New-Object System.Drawing.Pen (Col 110 $ar $ag $ab), 1.6), (Diamond $cx $cy 63))
    $gl = Glyph $b.gy $ar $ag $ab
    $gfx.DrawImage($gl, ($cx - 44), ($cy - 44), 88, 88)
}

# --- progress nodes (200x200 rects: unity (32,436) (314,440) (597,440)) ---
function Node($ux, $uy, $kind) {
    $cx = $ux + 100; $cy = $SH - $uy - 100
    if ($kind -eq 'done') {
        $d = Diamond $cx $cy 66
        $gfx.FillPath((New-Object System.Drawing.SolidBrush (Col 255 90 195 240)), $d)
        $gfx.DrawPath((New-Object System.Drawing.Pen (Col 255 200 242 255), 5), $d)
        $gfx.FillPath((New-Object System.Drawing.SolidBrush (Col 255 14 40 66)), (Diamond $cx $cy 24))
    } elseif ($kind -eq 'future') {
        $d = Diamond $cx $cy 66
        $gfx.FillPath((New-Object System.Drawing.SolidBrush (Col 255 14 26 42)), $d)
        $gfx.DrawPath((New-Object System.Drawing.Pen (Col 220 105 145 185), 5), $d)
    } else {
        $d = Diamond $cx $cy 86
        Glow $d 255 100 90 90 14
        $gfx.FillPath((New-Object System.Drawing.SolidBrush (Col 255 42 14 18)), $d)
        $gfx.DrawPath((New-Object System.Drawing.Pen (Col 255 255 125 115), 6), $d)
        $gl = Glyph 955 255 130 120
        $gfx.DrawImage($gl, ($cx - 48), ($cy - 48), 96, 96)
    }
}
Node 32 436 'done'
Node 314 440 'future'
Node 597 440 'boss'

$bmp.Save($OutPath, [System.Drawing.Imaging.ImageFormat]::Png)
Write-Output "OK $OutPath"
