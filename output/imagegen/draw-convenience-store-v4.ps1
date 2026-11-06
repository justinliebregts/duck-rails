$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Drawing
$dir=Join-Path $PSScriptRoot 'convenience-store-v4'
New-Item -ItemType Directory -Force $dir | Out-Null
$bmp=[Drawing.Bitmap]::new(1680,1530)
$g=[Drawing.Graphics]::FromImage($bmp);$g.SmoothingMode='AntiAlias';$g.Clear([Drawing.ColorTranslator]::FromHtml('#14252b'))
function B($c){[Drawing.SolidBrush]::new([Drawing.ColorTranslator]::FromHtml($c))}
function T($s,$x,$y,$size=15,$c='#e5ede8'){$f=[Drawing.Font]::new('Arial',$size);$b=B $c;$g.DrawString($s,$f,$b,[single]$x,[single]$y);$f.Dispose();$b.Dispose()}
function Rect($x,$y,$w,$h,$c){$b=B $c;$g.FillRectangle($b,[single]$x,[single]$y,[single]$w,[single]$h);$b.Dispose()}
function L($x,$y,$xx,$yy,$c='#d5dacc',$w=2){$p=[Drawing.Pen]::new([Drawing.ColorTranslator]::FromHtml($c),$w);$g.DrawLine($p,[single]$x,[single]$y,[single]$xx,[single]$yy);$p.Dispose()}
function S([double]$x,[double]$z,[double]$w,[double]$d,$c){Rect (65+($x+48)*8) (150+(160-$z-$d)*8) ($w*8) ($d*8) $c}
function SiteLine([double]$x,[double]$z,[double]$xx,[double]$zz,$c='#d5dacc',$w=2){L (65+($x+48)*8) (150+(160-$z)*8) (65+($xx+48)*8) (150+(160-$zz)*8) $c $w}
function ST($s,[double]$x,[double]$z,$size=12,$c='#e5ede8'){T $s (65+($x+48)*8) (150+(160-$z)*8) $size $c}
T 'QUACK MART / BOTH-SIDES SITE PLAN' 65 30 30
T 'V4 / 05 NOV 2026 / concept translated into buildable footprints / awaiting layout approval' 68 90 15
S -48 0 96 160 '#344b43';S -42 6 84 148 '#405a4b'
for($z=0;$z -le 160;$z+=20){SiteLine -48 $z 48 $z '#54695c' 1;ST "$z" -54 ($z+1) 10}
# Track and 3 m boarding paths remain continuous.
S -4 0 8 160 '#626b64';S -7 10 3 140 '#95958a';S 4 10 3 140 '#95958a'
S -48 104 96 8 '#505e64';S -42 102 84 2 '#96978b';S -42 112 84 2 '#96978b'
for($x=-46;$x -lt 48;$x+=7){SiteLine $x 108 ($x+3) 108 '#d8c77f' 2}
# Main east store and connected service loop.
S 10 58 32 44 '#626d70';S 20 58 3 32 '#a39d88';S 40 58 4 44 '#a39d88';S 20 58 24 4 '#a39d88';S 20 86 24 4 '#a39d88'
S 22 62 18 24 '#b7a482';ST '01' 28 77 21 '#14252b';ST 'STORE' 26 73 12 '#14252b';ST '18 x 24 m' 25 69 10 '#14252b'
SiteLine 22 77 22 80 '#f2d27f' 7;SiteLine 40 79 40 82 '#f2d27f' 7
SiteLine 7 78.5 21 78.5 '#f2d27f' 3;SiteLine 19 80 21 78.5 '#f2d27f' 3;SiteLine 19 77 21 78.5 '#f2d27f' 3
ST 'ENTRY >' 9 82 10 '#f2d27f'
for($z=62;$z -le 74;$z+=3){SiteLine 11 $z 16 $z '#d5dacc' 1};SiteLine 16 62 16 74
S 11.5 63 4 2 '#35484f';S 11.5 69 4 2 '#87796d'
S 10 88 10 12 '#6f9292';ST '02' 12 98 17 '#14252b';S 12 90 1 7 '#dfbb6b';S 17 90 1 7 '#dfbb6b'
S 24 93 3 5 '#87796d';ST 'VAN' 28 97 9
S 41 65 2 3 '#35484f';S 41 71 2 3 '#35484f';ST 'D' 41 74 9
S 20.5 83 1 2 '#7da9b0';ST 'ICE' 14 86 9
S 8 99 2 2 '#71d3d0';S 22 66 .5 9 '#d58cb6'
# Car wash, two-ended tunnel and surrounding return loop.
S 25 35 18 20 '#a39d88';S 28 38 12 14 '#a48e78';S 31 38 6 14 '#53676b';ST '03' 32 48 16;SiteLine 31 38 37 38 '#f2d27f' 5;SiteLine 31 52 37 52 '#f2d27f' 5
S 29 54 10 .5 '#d58cb6'
# North strip, front/back exits.
S 8 116 35 20 '#a39d88'
$names=@('PIZZA','DINER','NOODLES')
for($i=0;$i -lt 3;$i++){$x=10+$i*10;S $x 120 10 12 '#b09a7d';SiteLine $x 120 $x 132 '#14252b' 2;ST $names[$i] ($x+.5) 128 9 '#14252b';SiteLine ($x+3) 120 ($x+6) 120 '#f2d27f' 5;SiteLine ($x+3) 132 ($x+6) 132 '#f2d27f' 5;SiteLine ($x+.5) 119 ($x+9) 119 '#d58cb6' 4}
ST '04 / RESTAURANTS' 9 140 12
# West garage, east-facing bay and back exit. Wreck lot has two fence breaks.
S -40 62 30 40 '#6a716a';S -37 66 18 20 '#b7a482';ST '05' -31 79 21 '#14252b';ST 'GARAGE' -35 74 11 '#14252b'
SiteLine -19 76 -19 82 '#f2d27f' 7;SiteLine -37 69 -37 72 '#f2d27f' 5
SiteLine -10 79 -18 79 '#f2d27f' 3;SiteLine -16 80.5 -18 79 '#f2d27f' 3;SiteLine -16 77.5 -18 79 '#f2d27f' 3
S -16 67 3 6 '#b89e6b';ST 'TOW' -17 65 9
S -39 90 27 11 '#747c69';ST '06 / WRECK LOT' -39 100 10 '#14252b'
foreach($x in @(-36,-29,-22,-15)){S $x 91 2.5 5 '#3e5054'}
SiteLine -40 90 -40 101 '#b6aa83' 2;SiteLine -40 101 -12 101 '#b6aa83' 2;SiteLine -12 94 -12 101 '#b6aa83' 2
SiteLine -39 89 -30 89 '#b6aa83' 2;SiteLine -25 89 -12 89 '#b6aa83' 2
SiteLine -18.5 67 -18.5 73 '#dfbb6b' 4
# Rest stop and utility area establish destinations in both outer thirds.
S -37 28 25 28 '#60765b';ST '07 / REST STOP' -38 55 12
S -36 43 7 5 '#a48e78';S -17 44 4 7 '#a48e78'
foreach($z in @(32,39)){S -28 $z 5 2 '#b6a17b';SiteLine -29 ($z-1) -22 ($z-1) '#b6a17b' 3;SiteLine -29 ($z+3) -22 ($z+3) '#b6a17b' 3}
SiteLine -10 25 -10 60 '#b6a17b' 20;SiteLine -10 46 -28 46 '#b6a17b' 20
S -38 120 24 16 '#7e8570';S -34 125 6 5 '#526b70';S -22 124 4 4 '#3e5054';ST '08 / UTILITIES' -39 140 12
# Shallow drainage stops before rail corridor; bridges and dry bypasses.
foreach($x in @(-42,10)){S $x 19 32 3 '#527d89'}
foreach($x in @(-22,23)){S $x 17 4 7 '#b6a17b'}

foreach($z in @(10,29,58,116,143,151)){foreach($x in @(-43,43)){S ($x-1.5) ($z-1.5) 3 3 '#789069'}}
foreach($z in @(12,145,151)){S -28 $z 4 4 '#789069';S 25 $z 4 4 '#789069';S 14 ($z-1) 3 3 '#789069'}
foreach($z in @(29,44,140)){SiteLine -41 $z -41 ($z+8) '#b6aa83' 2;SiteLine 41 $z 41 ($z+8) '#b6aa83' 2}
# Track drawn over road; crossing remains continuous.
for($z=0;$z -lt 160;$z+=3){SiteLine -2 $z 2 $z '#888675' 3};SiteLine -1.5 0 -1.5 160 '#e1e4db' 2;SiteLine 1.5 0 1.5 160 '#e1e4db' 2
S -1.6 75 3.2 10 '#e4c677';ST 'STOP' -4 89 10
SiteLine -9 104 -9 108 '#d98d84' 5;SiteLine 9 108 9 112 '#d98d84' 5
foreach($x in @(-15,24)){for($z=104;$z -lt 112;$z+=1.3){S $x $z 3 .6 '#d5dacc'}}
ST '+Z / TRAVEL UP' -43 158 10
ST 'DRAIN + BRIDGE' -40 24 9;ST 'DRAIN + BRIDGE' 12 24 9
T '01 / SITE  |  96 x 160 m  |  grid 20 m' 65 1450 17
T 'Flat 6 m seams / level railway / stop unchanged at Z=80' 65 1485 12
# Store detail north-up, west/front at left, same orientation as site.
T 'STORE / ROOF-OFF DETAIL' 900 150 22
T '18 m deep x 24 m frontage / west face toward tracks' 900 193 14
Rect 1010 240 396 528 '#b7a482'
foreach($ln in @(@(1010,240,1406,240),@(1010,768,1406,768),@(1010,240,1010,768),@(1406,240,1406,768))){L $ln[0] $ln[1] $ln[2] $ln[3] '#e5ede8' 4}
L 1010 372 1010 438 '#f2d27f' 8;T 'TRACKS' 900 370 13;T 'ENTRY >' 900 400 13 '#f2d27f'
L 1318 240 1318 328 '#e5ede8' 4;L 1318 394 1318 768 '#e5ede8' 4;L 1406 328 1406 394 '#f2d27f' 8
T 'STOCK' 1325 450 10 '#14252b';T 'REAR' 1430 342 12;T 'EXIT >' 1430 368 12 '#f2d27f'
Rect 1032 284 44 66 '#6f7f79';T 'TILL' 1035 303 10 '#14252b'
foreach($z in @(6,10.5,15)){Rect 1098 (240+(24-$z-1.5)*22) 154 33 '#7b826c'}
T 'SHELVES' 1105 654 10 '#14252b'
Rect 1098 257 176 26 '#7fa4aa';T 'FRIDGES' 1135 260 10 '#14252b'
Rect 1208 680 66 44 '#7fa4aa';T 'FREEZER' 1207 735 10 '#14252b'
T '3 m between shelf runs; 3 m door openings.' 900 800 14
T 'Clear front cross-aisle and rear stockroom route.' 900 830 14
T 'SITE KEY / DESIGN INTENT' 900 900 22
$notes=@('01  Store faces tracks; ice + vending at entrance.','02  Gas canopy: 2 islands / 4 pump faces, offset north.','03  Car wash: walk-through tunnel + outside loop.','04  Three restaurants: dining front, kitchens at rear.','05  Garage: open bay, workbench, closed bay, rear exit.','06  Wreck lot: two fence gaps; tow truck on apron.','07  Picnic tables, vending shelter and bus stop.','08  Utility yard; trees / fences spread toward edges.')
$yy=946;foreach($n in $notes){T $n 900 $yy 14;$yy+=36}
T 'Pink / teal / amber strokes = glowing business signs.' 900 1260 13
T 'Yellow gaps = doors; sand strips = walking routes.' 900 1290 13
T 'Keep +/-4 m railway envelope free of solid props.' 900 1330 14
T 'Trackside paths 3 m; rear/side loops nominally 3-4 m.' 900 1362 14
T 'Shallow drains remain off-track, with dry bypasses.' 900 1394 14
T 'PROPOSAL / NO UNITY CHANGES / REVIEW BEFORE BUILD' 900 1475 12 '#e4c677'
$bmp.Save((Join-Path $dir 'top-down.png'),[Drawing.Imaging.ImageFormat]::Png);$g.Dispose();$bmp.Dispose()

