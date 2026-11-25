param([string]$OutPath = "C:\Users\hongj\CapstoneProject\ArtSource\UI_Concept\_preview_mapselect_v1.png")
Add-Type -AssemblyName System.Drawing
$W = 1920; $H = 1080
$root = "C:\Users\hongj\CapstoneProject"

$bmp = New-Object System.Drawing.Bitmap $W, $H, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
$gfx = [System.Drawing.Graphics]::FromImage($bmp)
$gfx.SmoothingMode = 'AntiAlias'; $gfx.TextRenderingHint = 'AntiAliasGridFit'; $gfx.InterpolationMode = 'HighQualityBicubic'

# backdrop: game shot dimmed
$bg = [System.Drawing.Image]::FromFile("$root\Captures\hud_layout\hud.png")
$gfx.DrawImage($bg, 0, 0, $W, $H)
$gfx.FillRectangle((New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(185, 4, 10, 20))), 0, 0, $W, $H)

function Col($a, $cr, $cg, $cb) { [System.Drawing.Color]::FromArgb($a, $cr, $cg, $cb) }
function Diamond($cx, $cy, $rad) {
    $p = New-Object System.Drawing.Drawing2D.GraphicsPath
    $pts = @([System.Drawing.PointF]::new($cx, $cy-$rad), [System.Drawing.PointF]::new($cx+$rad, $cy), [System.Drawing.PointF]::new($cx, $cy+$rad), [System.Drawing.PointF]::new($cx-$rad, $cy))
    $p.AddPolygon($pts); return $p
}
function CutRect($x, $y, $w, $h, $cut) {   # rectangle with two chamfered corners (top-right, bottom-left)
    $p = New-Object System.Drawing.Drawing2D.GraphicsPath
    $pts = @(
        [System.Drawing.PointF]::new($x, $y), [System.Drawing.PointF]::new($x+$w-$cut, $y),
        [System.Drawing.PointF]::new($x+$w, $y+$cut), [System.Drawing.PointF]::new($x+$w, $y+$h),
        [System.Drawing.PointF]::new($x+$cut, $y+$h), [System.Drawing.PointF]::new($x, $y+$h-$cut))
    $p.AddPolygon($pts); return $p
}
function Glow($path, $cr, $cg, $cb, $maxA, $widthMax) {
    for ($i = 8; $i -ge 1; $i--) {
        $pen = New-Object System.Drawing.Pen (Col ([int]($maxA * (9-$i) / 9)) $cr $cg $cb), ($widthMax * $i / 8)
        $pen.LineJoin = 'Round'; $gfx.DrawPath($pen, $path)
    }
}
function Txt($s, $x, $y, $size, $style, $color, $align) {
    $f = New-Object System.Drawing.Font "Malgun Gothic", $size, $style, ([System.Drawing.GraphicsUnit]::Pixel)
    $sf = New-Object System.Drawing.StringFormat; $sf.Alignment = $align; $sf.LineAlignment = 'Center'
    $gfx.DrawString($s, $f, (New-Object System.Drawing.SolidBrush $color), [System.Drawing.PointF]::new($x, $y), $sf)
}
# glyph from sprite sheet -> tinted ARGB bitmap
$sheet = New-Object System.Drawing.Bitmap "$root\Assets\Members\JJH\04_Assets\UI\Map_UI.png"
function Glyph($cy, $cr, $cg, $cb) {
    $gb = New-Object System.Drawing.Bitmap 100, 100, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    for ($yy = 0; $yy -lt 100; $yy++) { for ($xx = 0; $xx -lt 100; $xx++) {
        $px = $sheet.GetPixel(1141 - 50 + $xx, $cy - 50 + $yy)
        $lum = ($px.R + $px.G + $px.B) / 3
        if ($lum -lt 62) { $gb.SetPixel($xx, $yy, (Col 255 $cr $cg $cb)) }
        elseif ($lum -lt 95 -and $lum -ge 62) {   # soft edge
            $a = [int](255 * (95 - $lum) / 33); $gb.SetPixel($xx, $yy, (Col $a $cr $cg $cb)) }
    } }
    return $gb
}

# ---------- header ----------
Txt "다음 목적지를 선택하세요" 960 118 46 'Bold' (Col 255 235 245 255) 'Center'
Txt "스테이지 1  ·  방 3 / 6" 960 168 22 'Regular' (Col 255 232 196 120) 'Center'
$ln = New-Object System.Drawing.Drawing2D.LinearGradientBrush ([System.Drawing.PointF]::new(560,0)), ([System.Drawing.PointF]::new(1360,0)), (Col 0 130 220 255), (Col 0 130 220 255)
$cb = New-Object System.Drawing.Drawing2D.ColorBlend 3
$cb.Colors = @((Col 0 130 220 255), (Col 200 130 220 255), (Col 0 130 220 255)); $cb.Positions = @(0, 0.5, 1)
$ln.InterpolationColors = $cb
$gfx.FillRectangle($ln, 560, 196, 800, 2)
$gfx.FillPath((New-Object System.Drawing.SolidBrush (Col 255 150 230 255)), (Diamond 960 197 6))

# ---------- progress ----------
$py = 262
$stepX = 120
$gfx.FillRectangle((New-Object System.Drawing.SolidBrush (Col 90 120 200 240)), (960 - 3*$stepX), ($py-1), (6*$stepX), 2)
$gfx.FillRectangle((New-Object System.Drawing.SolidBrush (Col 230 130 225 255)), (960 - 3*$stepX), ($py-1), (2*$stepX), 2)
for ($i = 0; $i -lt 7; $i++) {
    $cx = 960 + ($i - 3) * $stepX
    if ($i -lt 2) {         # done
        $d = Diamond $cx $py 13
        $gfx.FillPath((New-Object System.Drawing.SolidBrush (Col 255 90 190 235)), $d)
        $gfx.DrawPath((New-Object System.Drawing.Pen (Col 255 190 240 255), 1.5), $d)
    } elseif ($i -eq 2) {   # current
        $d = Diamond $cx $py 21
        Glow $d 120 225 255 150 14
        $gfx.FillPath((New-Object System.Drawing.SolidBrush (Col 255 12 30 50)), $d)
        $gfx.DrawPath((New-Object System.Drawing.Pen (Col 255 170 240 255), 2.5), $d)
        $gfx.FillPath((New-Object System.Drawing.SolidBrush (Col 255 190 245 255)), (Diamond $cx $py 8))
    } elseif ($i -lt 6) {   # future
        $d = Diamond $cx $py 13
        $gfx.FillPath((New-Object System.Drawing.SolidBrush (Col 255 14 26 42)), $d)
        $gfx.DrawPath((New-Object System.Drawing.Pen (Col 200 110 150 190), 1.5), $d)
    } else {                # boss
        $d = Diamond $cx $py 24
        Glow $d 255 100 90 70 12
        $gfx.FillPath((New-Object System.Drawing.SolidBrush (Col 255 40 14 18)), $d)
        $gfx.DrawPath((New-Object System.Drawing.Pen (Col 255 255 120 110), 2.2), $d)
        $gl = Glyph 955 255 130 120
        $gfx.DrawImage($gl, ($cx - 17), ($py - 17), 34, 34)
    }
}

# ---------- choice cards ----------
$cards = @(
    @{ name = "무너진 다리";      sub = "일반 전투  ·  적을 모두 처치하면 포털이 열립니다"; tag = "NORMAL"; gy = 125; c = @(200, 222, 238); hover = $false },
    @{ name = "폐허의 시장";      sub = "상점  ·  골드로 스킬과 장비를 구매";               tag = "SHOP";   gy = 343; c = @(240, 205, 115); hover = $false },
    @{ name = "수호자의 둥지";    sub = "정예 전투  ·  강한 적, 더 좋은 보상";               tag = "ELITE";  gy = 757; c = @(185, 150, 255); hover = $true }
)
$cx0 = 960 - 400; $cw = 800; $chh = 124; $gap = 26
$cy0 = 360
for ($k = 0; $k -lt 3; $k++) {
    $cd = $cards[$k]
    $y = $cy0 + $k * ($chh + $gap)
    $ar = $cd.c[0]; $ag = $cd.c[1]; $ab = $cd.c[2]
    $shape = CutRect $cx0 $y $cw $chh 22
    if ($cd.hover) { Glow $shape $ar $ag $ab 120 16 }
    # glass fill (gradient left->right)
    $fillBr = New-Object System.Drawing.Drawing2D.LinearGradientBrush ([System.Drawing.PointF]::new($cx0,0)), ([System.Drawing.PointF]::new($cx0+$cw,0)), (Col 225 12 26 44), (Col 150 8 16 30)
    if ($cd.hover) { $fillBr = New-Object System.Drawing.Drawing2D.LinearGradientBrush ([System.Drawing.PointF]::new($cx0,0)), ([System.Drawing.PointF]::new($cx0+$cw,0)), (Col 235 ([int]($ar*0.18+8)) ([int]($ag*0.18+16)) ([int]($ab*0.18+30))), (Col 170 10 20 36) }
    $gfx.FillPath($fillBr, $shape)
    # top sheen
    $gfx.SetClip($shape)
    $gfx.FillRectangle((New-Object System.Drawing.SolidBrush (Col 14 255 255 255)), $cx0, $y, $cw, ($chh/2))
    # accent bar on the left edge
    $gfx.FillRectangle((New-Object System.Drawing.SolidBrush (Col 255 $ar $ag $ab)), $cx0, $y, 5, $chh)
    $gfx.ResetClip()
    $bw = 1.6; if ($cd.hover) { $bw = 2.4 }
    $ba = 120; if ($cd.hover) { $ba = 255 }
    $bpen = New-Object System.Drawing.Pen (Col $ba $ar $ag $ab), $bw; $bpen.LineJoin = 'Round'
    $gfx.DrawPath($bpen, $shape)
    # badge
    $bx = $cx0 + 78; $by = $y + $chh/2
    $bd = Diamond $bx $by 52
    $gfx.FillPath((New-Object System.Drawing.SolidBrush (Col 255 9 18 32)), $bd)
    Glow $bd $ar $ag $ab 70 8
    $gfx.DrawPath((New-Object System.Drawing.Pen (Col 255 $ar $ag $ab), 2.4), $bd)
    $bd2 = Diamond $bx $by 43
    $gfx.DrawPath((New-Object System.Drawing.Pen (Col 90 $ar $ag $ab), 1), $bd2)
    $glyph = Glyph $cd.gy $ar $ag $ab
    $gfx.DrawImage($glyph, ($bx - 30), ($by - 30), 60, 60)
    # text
    $nameCol = Col 255 235 245 255
    Txt $cd.name ($cx0 + 160) ($y + 46) 38 'Bold' $nameCol 'Near'
    Txt $cd.sub  ($cx0 + 162) ($y + 90) 21 'Regular' (Col 255 150 170 195) 'Near'
    Txt $cd.tag  ($cx0 + $cw - 40) ($y + 34) 17 'Bold' (Col 255 $ar $ag $ab) 'Far'
    if ($cd.hover) {
        Txt "▶ 선택" ($cx0 + $cw - 40) ($y + 92) 20 'Bold' (Col 255 235 245 255) 'Far'
    }
}

# footer hint
Txt "M  닫기" 960 1010 20 'Regular' (Col 255 140 160 185) 'Center'

$bmp.Save($OutPath, [System.Drawing.Imaging.ImageFormat]::Png)
Write-Output "OK $OutPath"
