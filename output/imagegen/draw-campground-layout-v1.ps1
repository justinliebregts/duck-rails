$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Drawing
$folder=Join-Path $PSScriptRoot 'campground-layout-v1'
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
Text 'LAKESIDE CAMPGROUND' 85 35 $title
Text 'Layout study 01  /  lake basin + low wooded terraces  /  04 NOV 2026' 88 90
Box -48 0 96 160 '#35483e'
Box -42 6 84 148 '#435843'
for($z=0;$z -le 160;$z+=20){Line -48 $z 48 $z '#58705c' 1;Label "$z" -53 $z}
Oval 11 47 30 68 '#58704a';Oval 20 67 20 43 '#6c8053';Oval 27 81 11 24 '#81915e'
# Authored irregular shoreline: broad northern pool, western inlet and narrow southern cove.
$shore=@(@(-29,109),@(-22,108),@(-19,103),@(-20,97),@(-17,91),@(-15,85),@(-17,78),@(-21,73),@(-20,65),@(-23,57),@(-27,51),@(-33,53),@(-36,59),@(-35,67),@(-31,73),@(-32,78),@(-38,83),@(-37,91),@(-34,97),@(-35,103))
$shorePoints=[Drawing.PointF[]]@($shore | ForEach-Object {P $_[0] $_[1]})
$shorePath=[Drawing.Drawing2D.GraphicsPath]::new()
$shorePath.AddClosedCurve($shorePoints,[single]0.2)
$bank=Pen '#8a9470' 44
$g.DrawPath($bank,$shorePath)
$water=Brush '#2b6678';$g.FillPath($water,$shorePath)
$shelf=Pen '#526b66' 14;$g.DrawPath($shelf,$shorePath)
$bank.Dispose();$water.Dispose();$shelf.Dispose();$shorePath.Dispose()
Box -4 6 14 148 '#536155'
Path @(@(9,65),@(16,62),@(24,62),@(29,75),@(29,94),@(21,106),@(15,98),@(15,80),@(9,80))
Path @(@(15,80),@(22,83),@(29,94))
Path @(@(-3,65),@(-12,65),@(-15,78),@(-14,96),@(-8,106),@(0,106))
Box -24 78 12 3 '#bfa67b'
Box -16 91 10 12 '#9c8464';Label 'Boat shed' -24 106
Box -16 53 10 9 '#9c8464';Label 'Supply cabin' -31 51
Box 12 48 12 10 '#9c8464';Label 'Reception' 12 47
Box 29 54 10 8 '#9c8464';Label 'Washrooms' 27 53
Box 29 71 10 10 '#aa9270';Label 'C1 +1 m' 29 70
Box 29 94 10 10 '#aa9270';Label 'C2 +2 m' 27 108
Box 15 104 10 9 '#aa9270';Label 'C3 +1 m' 13 117
Oval 11 73 12 12 '#9a9068';Oval 15 77 3 3 '#d28d50'
Label 'Fire' 11 73
Label 'LAKE' -35 88;Label '-0.8 m' -35 84
Label 'Dock' -25 77
for($z=0;$z -lt 160;$z+=2){$x=4*[Math]::Pow([Math]::Sin([Math]::PI*$z/160),2);$next=4*[Math]::Pow([Math]::Sin([Math]::PI*($z+2)/160),2);Line ($x-.8) $z ($next-.8) ($z+2) '#d3d8d4' 2;Line ($x+.8) $z ($next+.8) ($z+2) '#d3d8d4' 2}
Box 2.5 75 3 10 '#e0c572'
Label 'STOP' 1 90
foreach($a in @(@(37,48),@(39,66),@(38,88),@(12,91),@(35,112),@(-8,55),@(-8,113),@(-38,115),@(-39,43),@(22,121))){Oval ($a[0]-1.5) ($a[1]-1.5) 3 3 '#203d31'}
Label 'FLAT EXIT BUFFER  /  Y = 0' -27 145
Label 'FLAT ENTRY BUFFER  /  Y = 0' -27 20
Text 'N / +Z' 880 130
Text '96 m wide x 160 m long | coordinates in metres' 100 1640
Text 'ALL FOUR EDGES: 6 m flat perimeter at Y = 0' 100 1680
Text 'PLAN NOTES' 1030 180 $title
$notes=@('01  A real, enclosed lake','Approx. 23 x 58 m water footprint.','Water at -0.8 m; basin bottom -2 m.','Shore stays inside the flat edge strip.','','02  Gentle terrain, level railway','Eastern terraces: +1 m and +2 m.','Main paths target 1:12 or gentler.','Cabins sit on level pads.','Track follows its existing 4 m bend.','','03  Two ways back','Cabin loop + central clearing shortcut.','Lakeside path connects at both ends.','Crossings clear of the stopped train.','','04  Flat seams','6 m perimeter strip: height AND slope 0.','Terrain blends to it before the strip.','Entry/exit quiet buffers stay level.','','05  Lake edge','Shallow bank with walk-out shelves.','Dock at -0.2 m, ramp from the shore.','No swim/drowning mechanic assumed.','','06  Next: greybox review','Check low-eye views and slope feel.','Bake crawlgraph and lighting offline.','Keep six coal / three per side.','No new enemy coverage implied.')
$yy=245;foreach($n in $notes){Text $n 1030 $yy $font $muted;$yy+=32}
Text 'TERRAIN SECTION  /  schematic, height exaggerated' 1030 1280
$section=@(@(1040,1420),@(1090,1420),@(1130,1450),@(1190,1490),@(1240,1450),@(1290,1420),@(1350,1420),@(1410,1390),@(1480,1360),@(1540,1390),@(1610,1420),@(1690,1420))
$p=Pen '#a8bd83' 4
for($i=1;$i -lt $section.Count;$i++){$g.DrawLine($p,$section[$i-1][0],$section[$i-1][1],$section[$i][0],$section[$i][1])}
$g.DrawLine((Pen '#60afc5' 3),1130,1450,1240,1450)
Text 'flat     lake basin      rail       +2 m       flat' 1040 1530 $small
Text 'DRAWING PROPOSAL / not Unity geometry' 1030 1680
$bmp.Save((Join-Path $folder 'site-plan.png'),[Drawing.Imaging.ImageFormat]::Png)
$g.Dispose();$bmp.Dispose()


