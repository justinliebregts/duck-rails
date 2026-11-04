$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Drawing
$folder=Join-Path $PSScriptRoot 'campground-layout-v2'
New-Item -ItemType Directory -Force $folder | Out-Null
$bmp=[Drawing.Bitmap]::new(1800,1800)
$g=[Drawing.Graphics]::FromImage($bmp)
$g.SmoothingMode='AntiAlias'
$g.Clear([Drawing.ColorTranslator]::FromHtml('#14252b'))
function Brush($c){[Drawing.SolidBrush]::new([Drawing.ColorTranslator]::FromHtml($c))}
function Pen($c,$w=2){[Drawing.Pen]::new([Drawing.ColorTranslator]::FromHtml($c),$w)}
$white=Brush '#e7ede5';$muted=Brush '#b5c6bf'
$font=[Drawing.Font]::new('Arial',15);$small=[Drawing.Font]::new('Arial',12);$title=[Drawing.Font]::new('Arial',30)
function Text($s,$x,$y,$f=$font,$b=$white){$g.DrawString($s,$f,$b,[single]$x,[single]$y)}
function P([double]$x,[double]$z){[Drawing.PointF]::new([single](100+($x+48)*9),[single](170+(160-$z)*9))}
function Line($x,$z,$xx,$zz,$c='#b5c6bf',$w=2){$p=Pen $c $w;$g.DrawLine($p,(P $x $z),(P $xx $zz));$p.Dispose()}
function Box($x,$z,$w,$d,$c){$p=P $x ($z+$d);$b=Brush $c;$g.FillRectangle($b,$p.X,$p.Y,[single]($w*9),[single]($d*9));$b.Dispose()}
function Oval($x,$z,$w,$d,$c){$p=P $x ($z+$d);$b=Brush $c;$g.FillEllipse($b,$p.X,$p.Y,[single]($w*9),[single]($d*9));$b.Dispose()}
function Label($s,$x,$z){$p=P $x $z;Text $s $p.X $p.Y $small}
function Path($pts){for($i=1;$i -lt $pts.Count;$i++){Line $pts[$i-1][0] $pts[$i-1][1] $pts[$i][0] $pts[$i][1] '#c1ac7d' 27}}
function Cluster([double]$x,[double]$z){
 foreach($o in @(@(-3,-2),@(0,0),@(3,2),@(-2,4),@(2,-4),@(4,-1))){
  $tx=$x+$o[0];$tz=$z+$o[1]
  if([Math]::Abs($tx) -lt 41 -and $tz -gt 8 -and $tz -lt 152){
   Oval ($tx-1.8) ($tz-1.8) 3.6 3.6 '#1e3c30'
   Oval ($tx-1.1) ($tz-.8) 2.2 2.2 '#2c503b'
  }
 }
}
function Tent([double]$x,[double]$z){
 $points=[Drawing.PointF[]]@((P $x $z),(P ($x+4) $z),(P ($x+2) ($z+4)))
 $b=Brush '#bc8b57';$g.FillPolygon($b,$points);$b.Dispose()
 Line ($x+2) $z ($x+2) ($z+4) '#ead1a2' 2
}
function Key([int]$n,[double]$x,[double]$z){
 Oval ($x-1.7) ($z-1.7) 3.4 3.4 '#e5cf8c'
 $p=P ($x-.7) ($z+1.1);Text "$n" $p.X $p.Y $small (Brush '#152a25')
}
Text 'LAKESIDE CAMPGROUND / EXPANDED PLAN' 85 35 $title
Text 'Study 02  /  woodland, campsites and shore activities across the full block  /  04 NOV 2026' 88 90
Box -48 0 96 160 '#35483e';Box -42 6 84 148 '#435843'
for($z=0;$z -le 160;$z+=20){Line -48 $z 48 $z '#58705c' 1;Label "$z" -53 $z}
Oval 11 65 30 69 '#58704a';Oval 23 87 17 35 '#6c8053'
Oval -37 119 21 23 '#657255'
foreach($a in @(@(-35,19),@(-17,17),@(29,17),@(36,27),@(-39,42),@(-12,43),@(39,48),@(39,67),@(39,86),@(-40,70),@(-40,95),@(39,110),@(-37,117),@(-11,119),@(-37,143),@(-18,146),@(15,146),@(36,146),@(39,130))){Cluster $a[0] $a[1]}
$shore=@(@(-29,109),@(-22,108),@(-19,103),@(-20,97),@(-17,91),@(-15,85),@(-17,78),@(-21,73),@(-20,65),@(-23,57),@(-27,51),@(-33,53),@(-36,59),@(-35,67),@(-31,73),@(-32,78),@(-38,83),@(-37,91),@(-34,97),@(-35,103))
$sp=[Drawing.Drawing2D.GraphicsPath]::new();$sp.AddClosedCurve([Drawing.PointF[]]@($shore|ForEach-Object{P $_[0] $_[1]}),[single].2)
$g.DrawPath((Pen '#8a9470' 36),$sp);$g.FillPath((Brush '#2b6678'),$sp);$sp.Dispose()
Box -4 6 14 148 '#536155'
# Long trails use former approach/departure space, but stay within authored seams.
Path @(@(9,18),@(18,26),@(26,36),@(24,57),@(27,76),@(27,99),@(20,117),@(23,135),@(13,144))
Path @(@(9,80),@(15,80),@(15,99),@(20,117))
Path @(@(15,80),@(27,76))
Path @(@(-3,24),@(-19,29),@(-16,44),@(-10,65),@(-10,79),@(-8,88),@(-4,106),@(-15,116),@(-25,127),@(-21,140),@(-5,145))
Path @(@(-15,116),@(-24,112))
Path @(@(-8,88),@(-11,88),@(-11,97))
# Two visible branches to/from the train beyond its stopped footprint.
Path @(@(-10,65),@(9,65));Path @(@(-4,106),@(12,106))
# Existing central shells plus proposed relocation of reception and upper cabin.
Box -24 78 12 3 '#bfa67b'
Box -16 91 10 12 '#9c8464';Label 'Boathouse' -20 106
Box -16 53 10 9 '#9c8464';Label 'Supply' -16 52
Box 12 22 12 10 '#b69b70';Label 'Reception' 11 21
Box 29 46 10 8 '#9c8464';Label 'Washrooms' 26 45
Box 29 71 10 10 '#aa9270';Label 'C1 +1 m' 28 70
Box 29 94 10 10 '#aa9270';Label 'C2 +2 m' 27 108
Box 15 117 10 9 '#b69b70';Label 'C3 +1 m' 13 129
Oval 14 78 11 12 '#9a9068';Oval 18 82 3 3 '#d28d50'
Box 17 79 5 1 '#9c8464';Box 17 87 5 1 '#9c8464'
Label 'Campfire' 12 77
Label 'LAKE' -34 89;Label 'Dock' -26 77
# Three campsites, each a small clearing with a different story.
Oval -34 24 19 16 '#7c805b';Tent -29 29;Tent -22 32
Box -21 26 4 2 '#a38c68';Box -16 30 1 1 '#c0bda0'
Label 'Tent clearing' -36 23;Key 2 -33 39
Oval 24 30 16 13 '#7c805b';Box 30 34 4 7 '#b4b8a4';Tent 24 33
Box 25 39 3 1.5 '#a38c68';Label 'Camper pitch' 23 29;Key 2 38 40
Oval 13 130 22 13 '#7c805b';Tent 16 133;Tent 26 134
Box 22 137 3 1.5 '#a38c68';Label 'Woodland pitches' 12 132;Key 2 32 141
# Canoe beach, rack and two boats at the north end of the lake.
Oval -31 106 13 11 '#b3a17c';Box -20 109 2 6 '#9c8464'
Oval -29 108 1.3 5 '#c5b566';Oval -26 109 1.3 5 '#8fa697'
Label 'Canoe beach' -36 118;Key 3 -32 112
# Rocky northern rise with a view toward water; no new mandatory objective.
foreach($a in @(@(-32,130),@(-29,134),@(-24,131),@(-31,127))){Oval $a[0] $a[1] 4 3 '#8e9583'}
Label 'Rocky overlook' -37 140;Key 4 -34 135
# A shallow gully and short bridge on the upper trail, with an outside bypass.
Line 11 113 17 112 '#75929a' 8;Line 17 112 25 114 '#75929a' 8;Line 25 114 33 112 '#75929a' 8
Box 19 110 3 6 '#bfa67b';Key 5 28 115
# Fallen trunk damages a tent without obstructing the return path.
Line -32 34 -23 29 '#967650' 11
Line -30 33 -32 30 '#967650' 4;Line -27 31 -27 35 '#967650' 4
Key 7 -34 30
# Entry identity and small furnishings.
Box 10 15 6 .8 '#bfa67b';Label 'Entrance sign' 11 14;Key 6 9 18
Box 11 33 1.2 2 '#c1b58a';Box 25 54 1 1 '#a6ae95'
for($z=0;$z -lt 160;$z+=2){$x=4*[Math]::Pow([Math]::Sin([Math]::PI*$z/160),2);$next=4*[Math]::Pow([Math]::Sin([Math]::PI*($z+2)/160),2);Line ($x-.8) $z ($next-.8) ($z+2) '#d3d8d4' 2;Line ($x+.8) $z ($next+.8) ($z+2) '#d3d8d4' 2}
Box 2.5 75 3 10 '#e0c572';Label 'STOP' 1 90
Key 1 -36 148;Key 1 38 20
Label 'FLAT CONNECTION / scenery continues' -29 157
Label 'FLAT CONNECTION / scenery continues' -29 5
Text 'N / +Z' 880 130
Text '96 x 160 m | scenery and paths now reach roughly Z=15..145' 100 1640
Text 'Flat perimeter retained. Numbered additions are proposals, not installed assets.' 100 1680
Text 'MORE CAMPGROUND, LESS EMPTY BUFFER' 1020 180 ([Drawing.Font]::new('Arial',23))
$notes=@(
'1  WOODLAND THROUGHOUT',
'Clusters wrap the arrival, cabins,',
'far shore and departure trail.',
'Leave sightline gaps toward the train.',
'',
'2  THREE DISTINCT CAMPSITES',
'Southwest tents; southeast camper;',
'northeast woodland pitches.',
'Tables, chairs, coolers and small props.',
'',
'3  CANOE BEACH',
'North-shore gravel launch, two canoes',
'and an empty rack beside the boathouse.',
'',
'4  ROCKY OVERLOOK',
'A low wooded rise beyond the lake.',
'Trail loops through it before departure.',
'',
'5  GULLY + FOOTBRIDGE',
'A shallow cut below the upper cabin.',
'Short timber crossing; walk-around bank.',
'',
'6  CAMPGROUND IDENTITY',
'Reception moves toward the arrival.',
'Entrance sign, map, numbered posts, bins.',
'',
'7  FALLEN-TREE SCENE',
'A trunk crushes one abandoned tent.',
'The main return trail stays passable.'
)
$yy=245;foreach($n in $notes){Text $n 1030 $yy $font $muted;$yy+=32}
Text 'WHAT CHANGES IN THE SPACING' 1030 1310 ([Drawing.Font]::new('Arial',21))
Text 'Arrival: sign, reception, tents and camper.' 1030 1360
Text 'Middle: lake, dock, campfire and cabins.' 1030 1400
Text 'Departure: canoe beach, overlook, tents.' 1030 1440
Text 'The former quiet buffers become scenery' 1030 1510 $font $muted
Text 'and optional exploration, with open track.' 1030 1542 $font $muted
Text 'Exact enemy/loot expansion needs review.' 1030 1580 $font $muted
Text 'LAYOUT PROPOSAL / Unity unchanged' 1030 1680
$bmp.Save((Join-Path $folder 'expanded-site-plan.png'),[Drawing.Imaging.ImageFormat]::Png)
$g.Dispose();$bmp.Dispose()
