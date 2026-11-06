$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Drawing
$dir=Join-Path $PSScriptRoot 'convenience-store-v5'
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
T 'V5 / 05 NOV 2026 / concept translated into buildable footprints / awaiting layout approval' 68 90 15
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
S -40 62 30 40 '#6a716a';S -37 66 18 20 '#b7a482';ST '05' -31 79 21 '#14252b';ST 'ARCADE' -35 74 11 '#14252b'
SiteLine -19 77 -19 80 '#f2d27f' 7;SiteLine -37 69 -37 72 '#f2d27f' 5
SiteLine -10 79 -18 79 '#f2d27f' 3;SiteLine -16 80.5 -18 79 '#f2d27f' 3;SiteLine -16 77.5 -18 79 '#f2d27f' 3
S -16 67 2 2 '#7da9b0';ST 'ENTRY PLAZA' -38 64 9
S -39 90 27 11 '#747c69';ST '06 / OLD PARKING' -39 100 10 '#14252b'
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
# Eight warm streetlights outside the clear rail envelope, on both sidewalks.
foreach($z in @(103,113)){foreach($x in @(-36,-18,18,36)){$px=65+($x+48)*8;$py=150+(160-$z)*8;$br=B '#f2d27f';$g.FillEllipse($br,[single]($px-5),[single]($py-5),10,10);$br.Dispose();SiteLine $x $z $x ($z+1.5) '#f2d27f' 2}}
ST '+Z / TRAVEL UP' -43 158 10
ST 'DRAIN + BRIDGE' -40 24 9;ST 'DRAIN + BRIDGE' 12 24 9
T '01 / SITE  |  96 x 160 m  |  grid 20 m' 65 1450 17
T 'Flat 6 m seams / level railway / stop unchanged at Z=80' 65 1485 12
# Arcade interior, north-up; east/front at right, matching the site.
T 'ARCADE / ROOF-OFF DETAIL' 900 150 22
T '18 x 20 m / front faces east toward tracks' 900 193 14
Rect 990 245 432 480 '#b7a482'
foreach($ln in @(@(990,245,1422,245),@(990,725,1422,725),@(990,245,990,725),@(1422,245,1422,725))){L $ln[0] $ln[1] $ln[2] $ln[3] '#e5ede8' 4}
L 1422 389 1422 461 '#f2d27f' 8;T '< ENTRY' 1440 405 13 '#f2d27f';T 'TRACKS >' 1440 437 12
L 990 581 990 653 '#f2d27f' 8;T 'REAR' 900 595 12;T '< EXIT' 900 620 12 '#f2d27f'
# Rear 4 m service strip and wide connection at south end.
L 1086 245 1086 557 '#e5ede8' 4;L 1086 629 1086 725 '#e5ede8' 4
T 'SERVICE' 998 295 10 '#14252b';T 'STORAGE' 998 320 10 '#14252b'
# Cabinet banks: clear main east/west entrance route at Z=77..80.
foreach($x in @(1110,1146,1182,1218)){Rect $x 275 26 36 '#536c79';Rect $x 282 26 5 '#9cbbc5'}
foreach($x in @(1182,1218,1254,1290)){Rect $x 500 26 36 '#536c79';Rect $x 507 26 5 '#d58cb6'}
T 'CABINET ROWS' 1106 335 11 '#14252b'
Rect 1338 270 60 70 '#897d9e';T 'PRIZES' 1329 350 10 '#14252b'
Rect 1314 650 84 42 '#7b826c';T 'COUNTER' 1310 700 10 '#14252b'
Rect 1140 620 84 48 '#7b826c';T 'AIR HOCKEY' 1120 680 10 '#14252b'
L 1400 425 1134 425 '#f2d27f' 3;L 1134 425 1134 593 '#f2d27f' 3;L 1134 593 1000 593 '#f2d27f' 3
T 'Wide entrance route; loop behind cabinet rows.' 900 780 14
T 'Glowing ARCADE sign + a few surviving screens.' 900 812 14
T 'Machines are scenery; no minigame scope added.' 900 844 13
T 'SITE KEY / DESIGN INTENT' 900 900 22
$notes=@('01  Store faces tracks; ice + vending at entrance.','02  Gas canopy: 2 islands / 4 pump faces, offset north.','03  Car wash: walk-through tunnel + outside loop.','04  Three restaurants: dining front, kitchens at rear.','05  Arcade: cabinet rows, prize desk and rear exit.','06  Abandoned parking: wrecked cars, two fence gaps.','07  Picnic tables, vending shelter and bus stop.','08  Utility yard; trees / fences spread toward edges.')
$yy=946;foreach($n in $notes){T $n 900 $yy 14;$yy+=36}
T 'Colored strokes = neon; gold circles = streetlights.' 900 1260 13
T 'Yellow gaps = doors; sand strips = walking routes.' 900 1290 13
T 'Keep +/-4 m railway envelope free of solid props.' 900 1330 14
T 'Trackside paths 3 m; rear/side loops nominally 3-4 m.' 900 1362 14
T '8 warm road lamps; poles stay clear of crossings.' 900 1394 14
T 'PROPOSAL / NO UNITY CHANGES / REVIEW BEFORE BUILD' 900 1475 12 '#e4c677'
$bmp.Save((Join-Path $dir 'top-down.png'),[Drawing.Imaging.ImageFormat]::Png);$g.Dispose();$bmp.Dispose()



