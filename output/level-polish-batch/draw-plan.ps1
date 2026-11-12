Add-Type -AssemblyName System.Drawing
$bmp = [System.Drawing.Bitmap]::new(1800,1450)
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.SmoothingMode = 'AntiAlias'
$g.Clear([System.Drawing.ColorTranslator]::FromHtml('#f4f2ec'))
$font = [System.Drawing.Font]::new('Segoe UI',16)
$small = [System.Drawing.Font]::new('Segoe UI',12)
$title = [System.Drawing.Font]::new('Segoe UI',26,[System.Drawing.FontStyle]::Bold)
function Brush($c) { [System.Drawing.SolidBrush]::new([System.Drawing.ColorTranslator]::FromHtml($c)) }
function Text($s,$x,$y,$f=$font) { $g.DrawString($s,$f,(Brush '#263334'),[single]$x,[single]$y) }
Text 'BRIDGE FINALE / BATCH LAYOUT REVIEW' 40 22 $title
Text 'Proposed changes only. Current prefab footprints measured in block-local metres. North = Z+ (up); east = X+ (right).' 42 70
$script:ox=450; $script:oy=1030; $script:scale=7.6
function P([double]$x,[double]$z) { [System.Drawing.PointF]::new(($script:ox+$x*$script:scale),($script:oy-$z*$script:scale)) }
function Rect([double]$x,[double]$z,[double]$w,[double]$d,$c) { $p=P $x ($z+$d); $g.FillRectangle((Brush $c),$p.X,$p.Y,[single]($w*$script:scale),[single]($d*$script:scale)) }
function Line([double]$x1,[double]$z1,[double]$x2,[double]$z2,$c,$width=2) { $a=P $x1 $z1; $b=P $x2 $z2; $pen=[System.Drawing.Pen]::new([System.Drawing.ColorTranslator]::FromHtml($c),$width); $g.DrawLine($pen,$a,$b);$pen.Dispose() }
function Label($s,$x,$z) { $p=P $x $z;Text $s $p.X $p.Y $small }
function Arrow($x1,$z1,$x2,$z2) { $a=P $x1 $z1;$b=P $x2 $z2;$pen=[System.Drawing.Pen]::new([System.Drawing.ColorTranslator]::FromHtml('#143c35'),2);$pen.EndCap='ArrowAnchor';$g.DrawLine($pen,$a,$b);$pen.Dispose() }
Text 'A / Fortress, river and access' 40 120
Rect -48 0 96 80 '#d4d5cf';Rect -48 80 96 30 '#9fbfc3'
Rect -3.7 80 7.4 30 '#919e9d'
foreach($s in @(-1,1)) {
 $x=if($s -eq -1){-32}else{12}; Rect $x 36 20 12 '#a9afaa';Rect $x 60 20 16 '#a9afaa'
 $x=if($s -eq -1){-35}else{31};Rect $x 33 4 42 '#a9afaa'
 Rect ($s*34-4) 70 8 8 '#747f7b'
 Rect ($s*47-0.3) 0 0.6 31 '#375e57'
 Rect ($s*46-1) 80 2 30 '#375e57'
 # New inward approach to south platform, 4 m run / 2 m rise.
 $x=if($s -eq -1){-12}else{8};Rect $x 38.5 4 3 '#66b7a5';Arrow ($s*8) 40 ($s*12) 40
 Rect ($s*27-1.5) 48 3 8 '#66b7a5';Arrow ($s*27) 49 ($s*27) 55
 Rect ($s*26-1.5) 64 3 8 '#66b7a5';Arrow ($s*26) 65 ($s*26) 71
 foreach($i in 0..3) {Rect ($s*(8.5+$i*.9)-.65) 66 1.3 2 '#cfaa64'}
 # River recovery routes retain current footprints.
 Rect ([Math]::Min($s*16,$s*35.5)) 87 19.5 3 '#66b7a5'
 Rect ([Math]::Min($s*16,$s*35.5)) 81.5 19.5 3 '#66b7a5'
}
Rect -17 66.5 34 3 '#747f7b'
foreach($q in @(@(-29,41),@(-29,42.6),@(-22,46),@(22,38),@(23.6,38),@(25.2,38),@(29,45),@(-34,51),@(-34,62),@(34,41),@(34,53),@(34,65))) {Rect $q[0] $q[1] 1.3 1.3 '#986d47'}
foreach($x in @(-1.5,1.5)){Line $x 0 $x 110 '#44504d'}
Label 'N / Z+' -47 116;Label 'river recovery ramps' -38 95;Label 'retained bridge' -7 108
Label '+4 m' -22 65;Label '+4 m' 18 65;Label '+2 m' -25 44;Label '+2 m' 16 44
Label 'traffic stays' -35 18;Label 'traffic stays' 19 18
Text 'Green: ramps and walls   Ochre: climb blocks   Brown: crate groups' 40 1055 $small
Text '13-15  South ramps turn inward: 4 m run / 2 m rise, 3 m wide.' 40 1095
Text 'Upper ramps: 8 m run / 4 m rise (was 12 m). Middle ramps keep 8 m runs.' 40 1125 $small
Text 'River recovery keeps both switchbacks: 19.5 m run / 5.75 m rise per flight.' 40 1150 $small
Text '11  Four staggered climb blocks per side, rising toward the +4 m platforms.' 40 1190
Text '6-7  Low approach walls at X +/-47; river end walls tuck inward to X +/-46.' 40 1230
Text '16  Offset crate groups: west stacked pairs, east low rows and singles.' 40 1270
Text 'Keep at least 2 m clear on patrol walks; preserve loot and tower enemies.' 40 1300 $small
$script:ox=1330;$script:oy=1370;$script:scale=3.9
Text 'B / Cliff, tunnel and valley' 945 120
Rect -65 160 130 65 '#bdc9a8';Rect -35 110 70 190 '#cfdbb8'
Rect -65 160 30 140 '#a4b28b';Rect 35 160 30 140 '#a4b28b'
Rect -65 120 130 40 '#8e9790'
# Irregular outer cliff faces connect to existing hills at Z160-175.
foreach($s in @(-1,1)){Rect ([Math]::Min($s*47,$s*65)) 152 18 23 '#6c8075'}
Rect -5.5 120 11 40 '#dfdfd4';Rect -6.8 118.7 13.6 1.3 '#b7b6aa'
Rect -2.5 110 5 190 '#9f9e8c'
foreach($x in @(-1.5,1.5)){Line $x 110 $x 300 '#44504d'}
for($z=195;$z -le 260;$z+=1){$x=17+4*[Math]::Sin($z*.09);Rect ($x-3.1) $z 6.2 1.1 '#9cab83';Rect ($x-1.8) $z 3.6 1.1 '#68a5b5'}
foreach($x in @(-18,18)){foreach($z in @(265,289)){Rect ($x-4) ($z-3.5) 8 7 '#b6a481'}}
Label 'N / Z+' -60 302;Label 'houses retained' -30 296;Label 'recessed stream' 23 246
Label 'gravel' -17 191;Label 'cliff blends into hills' -61 175;Label 'manmade tunnel' -22 146
Text '3  Follow the existing stream curve; banks about 6 m wide.' 945 1095
Text 'Water 0.25 m below grass; solid bed about 0.6 m below grass.' 945 1125 $small
Text '5,9  Faceted rock cliff replaces the rectangular end masses.' 945 1170
Text 'Extend to X +/-65 and overlap the hills at Z160-175.' 945 1200 $small
Text 'Retain tunnel Z120-160 and its current clear opening.' 945 1225 $small
Text '2,4,8,10,12  Seams, gables, gravel and lights are separate fixes.' 945 1270 $small
Text 'Dimensions are proposed; all paths need editor checks and later playtest.' 945 1300 $small
Text 'No route, bridge-control, encounter-count or network changes. One final navigation/lighting bake after approved geometry.' 40 1380 $small
$bmp.Save('X:/Work/duck-rails/output/level-polish-batch/layout-review.png',[System.Drawing.Imaging.ImageFormat]::Png)
$g.Dispose();$bmp.Dispose()


