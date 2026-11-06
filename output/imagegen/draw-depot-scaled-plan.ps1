Add-Type -AssemblyName System.Drawing
$bmp = New-Object System.Drawing.Bitmap 1800,1250
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.SmoothingMode = 'AntiAlias'
$g.TextRenderingHint = 'AntiAliasGridFit'
$g.Clear([System.Drawing.Color]::FromArgb(246,247,245))
$ink = [System.Drawing.Brushes]::Black
$font = New-Object System.Drawing.Font 'Arial',13
$small = New-Object System.Drawing.Font 'Arial',11
$title = New-Object System.Drawing.Font 'Arial',25
$head = New-Object System.Drawing.Font 'Arial',17
function Txt($s,$x,$y,$f=$font) { $g.DrawString($s,$f,$ink,[single]$x,[single]$y) }
function Pen($color,$w=2) { New-Object System.Drawing.Pen ([System.Drawing.ColorTranslator]::FromHtml($color)),([single]$w) }
function Brush($color) { New-Object System.Drawing.SolidBrush ([System.Drawing.ColorTranslator]::FromHtml($color)) }
function Pt([double]$x,[double]$z) { New-Object System.Drawing.PointF ([single]($script:ox+($x+48)*$script:sc)),([single]($script:oy+($script:topZ-$z)*$script:sc)) }
function Line($x1,$z1,$x2,$z2,$p) { $g.DrawLine($p,(Pt $x1 $z1),(Pt $x2 $z2)) }
function Rect($x,$z,$w,$d,$fill,$outline) {
 $p=Pt $x ($z+$d)
 if($fill){$g.FillRectangle($fill,$p.X,$p.Y,[single]($w*$script:sc),[single]($d*$script:sc))}
 if($outline){$g.DrawRectangle($outline,$p.X,$p.Y,[single]($w*$script:sc),[single]($d*$script:sc))}
}
function Label($s,$x,$z,$f=$font) {$p=Pt $x $z;Txt $s $p.X $p.Y $f}
$grid=Pen '#dce0df' 1
$edge=Pen '#555f64' 2
$dash=Pen '#bf6a29' 2
$dash.DashStyle='Dash'
$bfill=Brush '#cbd5df'
$path=Pen '#9dd7c8' 30
$path.StartCap='Round';$path.EndCap='Round'
Txt 'MAINTENANCE DEPOT / SCALED SITE PLAN' 55 25 $title
Txt 'Actual block boundary and track curve + fully proposed buildings, paths and lights. All coordinates are local metres; +Z is travel / diagram north.' 55 70
Txt 'FULL BLOCK  |  96 x 160 m' 80 115 $head
Txt 'ENCOUNTER DETAIL  |  same scale on X and Z axes' 720 115 $head
function Base($detail) {
 for($x=-40;$x -le 40;$x+=10){Line $x ($script:topZ-$script:h) $x $script:topZ $grid}
 for($z=[math]::Ceiling(($script:topZ-$script:h)/10)*10;$z -le $script:topZ;$z+=10){Line -48 $z 48 $z $grid;Label "$z" -52 $z $small}
 for($x=-40;$x -le 40;$x+=20){Label "$x" ($x-1) ($script:topZ+3) $small}
 Rect -48 ($script:topZ-$script:h) 96 $script:h $null $edge
 Line -48 45 48 45 $dash;Line -48 115 48 115 $dash
 # A proposed clear corridor, ten metres wide following the actual track.
 $strip=Pen '#e2e8e5' (10*$script:sc)
 $pts=New-Object 'System.Collections.Generic.List[System.Drawing.PointF]'
 for($z=[math]::Max(0,($script:topZ-$script:h));$z -le [math]::Min(160,$script:topZ);$z+=1){
 $x=-4*[math]::Pow([math]::Sin([math]::PI*$z/160),2);$pts.Add((Pt $x $z))
 }
 $g.DrawLines($strip,$pts.ToArray())
 foreach($off in @(-0.75,0.75)){
 $rail=New-Object 'System.Collections.Generic.List[System.Drawing.PointF]'
 for($z=[math]::Max(0,($script:topZ-$script:h));$z -le [math]::Min(160,$script:topZ);$z+=1){$x=-4*[math]::Pow([math]::Sin([math]::PI*$z/160),2)+$off;$rail.Add((Pt $x $z))}
 $g.DrawLines((Pen '#59646b' 2),$rail.ToArray())
 }
 Rect -32 92 16 18 $bfill $edge
 Rect -32 56 16 18 $bfill $edge
 Rect 16 62 20 30 $bfill $edge
 Rect 16 98 20 16 (Brush '#f1e2cc') $dash
 Rect -32 78 16 10 (Brush '#f1e2cc') $dash
 Rect 16 56 20 4 (Brush '#d6eee7') $dash
 # Existing enemy bounds deliberately omitted from the design proposal.
 $p=Pt -4 80
 $g.FillEllipse((Brush '#006d78'),($p.X-5),($p.Y-5),10,10)
 if($detail){
 foreach($a in @(@(-12,65,-12,101),@(-24,65,-12,65),@(-24,101,-12,101),@(-24,83,-12,83),@(-12,80,10,80),@(10,58,10,106),@(10,80,24,80),@(10,106,26,106),@(10,58,26,58),@(26,58,26,68),@(24,80,24,68),@(24,68,26,68))){Line $a[0] $a[1] $a[2] $a[3] $path}
 # Redraw marker over path; it is a point, not an invented train footprint.
 $g.FillEllipse((Brush '#006d78'),($p.X-6),($p.Y-6),12,12)
 Label "PARTS STORE" -31 108
 Label "16 x 18 m" -31 105 $small
 Label "WORKSHOP" -31 72
 Label "16 x 18 m" -31 69 $small
 Label "LOADING" -31 86 $small
 Label "16 x 10 m" -31 83 $small
 Label "REPAIR SHED" 17 90
 Label "20 x 30 m" 17 87
 Label "WORK YARD" 17 112 $small
 Label "20 x 16 m" 17 109 $small; Label "6 m gap" 19 96 $small
 Label "SERVICE PATH 4 m" 17 59 $small
 Label "Stop (-4,80)" -9 77 $small
 Label "Open face" -16 63 $small
 Label "Bay" 16 78 $small
 # Existing and additional fixture positions, not coverage circles.
 $lights=@(@(-17,101,'L1',1),@(-17,65,'L2',1),@(16,80,'L3',1),@(18,106,'L4',1),@(30,76,'L5',1),@(26,62,'L6',1))
 foreach($l in $lights){$p=Pt $l[0] $l[1];$g.FillEllipse((Brush '#f3c543'),($p.X-6),($p.Y-6),12,12);if($l[3]){$g.DrawEllipse((Pen '#9f6121' 2),($p.X-8),($p.Y-8),16,16)};Txt $l[2] ($p.X+10) ($p.Y-9) $small}
 Label "3 m paths" -12 93 $small
 Label "SIDE EXIT" 27 65 $small
 }else{
 Label "45 m departure buffer" -45 147 $small
 Label "70 m encounter zone" -45 118 $small
 Label "45 m arrival buffer" -45 35 $small
 Label "Entry (0,0)" 3 8 $small
 Label "Exit (0,160)" 3 158 $small
 }
}
$script:ox=80;$script:oy=180;$script:sc=5;$script:topZ=160;$script:h=160
Base $false
$script:ox=720;$script:oy=180;$script:sc=10;$script:topZ=122;$script:h=84
Base $true
# Scale bar uses same ten pixels per metre as detail.
$g.DrawLine((Pen '#202a30' 5),730,1050,930,1050)
Txt '0' 725 1060 $small;Txt '10' 820 1060 $small;Txt '20 m' 920 1060 $small
Txt '10 m grid. All building footprints and path widths are proposals.' 980 1045 $small
Txt 'Blue: proposed buildings    Tan / dashed: proposed yard and loading area    Teal: proposed 3 m walking paths' 55 1110
Txt 'Yellow L1-L6: proposed baked lights at doors, bay, yard, shed interior and side exit. Symbols are NOT light coverage.' 55 1140
Txt 'Orange lines: encounter zone Z 45-115. Pale track strip: proposed 10 m clear corridor, not train width.' 55 1170
Txt 'Boundary and curved track verified in Unity. Existing buildings ignored. Diagram only; no Unity edits. Nov 2, 2026.' 55 1200 $small
$bmp.Save((Join-Path $PSScriptRoot 'maintenance-depot-scaled-plan-v2.png'),[System.Drawing.Imaging.ImageFormat]::Png)
$g.Dispose();$bmp.Dispose()


