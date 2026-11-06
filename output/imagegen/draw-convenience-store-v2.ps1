$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Drawing
$dir=Join-Path $PSScriptRoot 'convenience-store-v2'
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
T 'CONVENIENCE STORE / ROADSIDE STRIP' 65 30 30
T 'Top-down layout V2  |  Proposal for review  |  04 November 2026' 68 90 16
S -48 0 96 160 '#344b43';S -42 6 84 148 '#405a4b'
for($z=0;$z -le 160;$z+=20){SiteLine -48 $z 48 $z '#54695c' 1;ST "$z" -54 ($z+1) 10}
S -40 10 5 140 '#6c756a';S 35 10 5 42 '#6c756a';S 35 120 5 30 '#6c756a'
S -5 0 10 160 '#657069'
S -48 90 96 8 '#545d60';S -42 88 84 2 '#8a9189';S -42 98 84 2 '#8a9189'
for($x=-46;$x -lt 48;$x+=7){SiteLine $x 94 ($x+3) 94 '#d8c77f' 2}
S 10 76 32 12 '#667071';S 11 55 3 33 '#899187';S 38 55 6 33 '#899187';S 11 54 33 4 '#899187'
S 14 58 24 18 '#b7a482';ST 'STORE' 20 70 16 '#14252b';ST '24 x 18 m' 19 66 12 '#14252b'
SiteLine 23 76 26 76 '#f2d27f' 6;SiteLine 24 58 27 58 '#f2d27f' 6
S 40 61 2.5 3 '#405455';S 40 67 2.5 3 '#405455';ST 'D' 40 69 10
for($x=15;$x -le 33;$x+=3){SiteLine $x 77 $x 82 '#ced2c2' 1};SiteLine 15 77 33 77
S 16 78 2 4 '#39474c';S 28 78 2 4 '#88796b'
S 35 84 3 2 '#d78dba';ST 'NEON' 31 88 10
S 11 100 30 2 '#a29b88'
$names=@('PIZZA','DINER','NOODLES')
for($i=0;$i -lt 3;$i++){$x=11+$i*10;S $x 102 10 12 '#a28c72';SiteLine $x 102 $x 114 '#14252b' 3;ST $names[$i] ($x+0.5) 109 9 '#14252b';SiteLine ($x+3) 102 ($x+6) 102 '#f2d27f' 5;SiteLine ($x+3) 114 ($x+6) 114 '#f2d27f' 5}
S 8 114 36 3 '#899187';S 8 100 3 17 '#899187';S 41 100 3 17 '#899187'
ST 'ABANDONED RESTAURANTS' 9 122 12
S -38 65 25 23 '#667071';ST 'OVERFLOW LOT' -39 83 12;ST 'wrecks / bus shelter' -39 79 10
S -35 67 3 6 '#88796b';S -25 70 3 5 '#39474c';S -36 85 8 2 '#a28c72'
ST 'West-side salvage' -40 62 11;ST 'loot positions deferred' -40 58 10
for($z=0;$z -lt 160;$z+=3){SiteLine -2 $z 2 $z '#888675' 3};SiteLine -1.5 0 -1.5 160 '#e1e4db' 2;SiteLine 1.5 0 1.5 160 '#e1e4db' 2
S -1.6 75 3.2 10 '#e4c677';ST 'TRAIN STOP' -24 75 11
SiteLine -12 72 11 72 '#e4c677' 3
SiteLine -7 90 -7 94 '#d98d84' 5;SiteLine 7 94 7 98 '#d98d84' 5
ST 'GATES' -21 104 10
for($z=90;$z -lt 98;$z+=1.3){S 29 $z 3 0.6 '#d5dacc'}
# Supporting scenery extends beyond the central destination, while seams stay flat.

S -32 18 19 24 '#667071';ST 'OLD CAR LOT' -32 44 11
foreach($z in @(21,30)){S -28 $z 3 5 '#88796b';S -20 ($z+2) 3 5 '#39474c'}
S 16 25 14 12 '#7a8174';S 19 28 4 5 '#536b70';S 25 30 3 4 '#536b70';ST 'UTILITY PAD' 13 42 11
S -31 125 18 12 '#a28c72';ST 'SHUTTERED' -31 134 9 '#14252b';ST 'AUTO SHOP' -31 130 9 '#14252b'
S -32 115 20 9 '#667071';S -27 118 3 5 '#88796b'
S 13 128 18 10 '#667071';S 17 131 3 5 '#39474c';ST 'DELIVERY LOT' 11 143 11
foreach($z in @(12,19,47,53,123,145,151)){foreach($x in @(-43,43)){S ($x-2) ($z-2) 4 4 '#789069'}}
foreach($z in @(12,18,46,49,145,151)){S 22 $z 4 4 '#789069';S -25 ($z+1) 3 3 '#789069'}
foreach($z in @(14,26,38,120,139,149)){SiteLine -33 $z -33 ($z+6) '#b6aa83' 2;SiteLine 32 $z 32 ($z+6) '#b6aa83' 2}
foreach($z in @(20,45,118,145)){S -7 $z 0.7 0.7 '#e4c677';S 7 $z 0.7 0.7 '#e4c677'}
ST 'SCATTERED TREES / FENCE REMNANTS' -32 9 10
ST '+Z / TRAVEL UP' -44 157 11
T '01 / FULL BLOCK' 65 1450 17
T '96 x 160 m | 20 m grid | existing stop at Z=80' 65 1484 12
T '02 / STORE INTERIOR' 900 150 22
T '24 x 18 m footprint / front faces road (+Z)' 900 195 14
# Interior uses 25 px per metre, north at top.
Rect 920 260 600 450 '#b7a482'
L 920 260 1520 260 '#e5ede8' 5;L 920 260 920 710 '#e5ede8' 5;L 1520 260 1520 710 '#e5ede8' 5;L 920 710 1520 710 '#e5ede8' 5
L 1145 260 1220 260 '#f2d27f' 9;T 'ENTRY / FRONT WINDOWS' 1040 225 13
Rect 940 290 125 38 '#6f7f79';T 'CHECKOUT' 945 297 11
T 'OPEN FRONT CROSS-AISLE' 1090 340 12 '#253d40'
foreach($x in @(1015,1127.5,1240)){Rect $x 385 37.5 175 '#7b826c';T 'AISLE' ($x-4) 455 10 '#172e31'}
Rect 1470 355 35 210 '#7fa4aa';T 'FRIDGES' 1380 390 11 '#253d40'
Rect 1365 505 50 80 '#7fa4aa';T 'FREEZER' 1350 590 10 '#253d40'
L 920 635 1150 635 '#e5ede8' 4;L 1225 635 1520 635 '#e5ede8' 4
T 'STOCK / DELIVERY' 1010 667 12 '#253d40';L 1170 710 1245 710 '#f2d27f' 9
T 'REAR EXIT TO SERVICE LOOP' 1010 735 13
T '3 m between shelf runs; front/rear cross-aisles.' 900 790 14
T 'Side lane: two dumpsters, with a clear bypass.' 900 822 14
T '03 / STREET CHARACTER' 900 900 22
T 'Large neon pylon at the parking-lot entrance.' 900 946 15
T 'Six front bays, two abandoned cars, faded markings.' 900 980 15
T 'Three restaurant shells: dining front / kitchen rear.' 900 1014 15
T 'Crosswalk, street lamps, stop signs and traffic cones.' 900 1048 15
T 'Rail crossing gates proposed as static scenery first.' 900 1082 15
T 'Keep the rail corridor and pedestrian bypass clear.' 900 1116 15
T '04 / REVIEW BEFORE BLOCKOUT' 900 1200 22
T 'Outer thirds: car lot, utility pad, auto shop, trees.' 900 1245 15
T 'Yellow openings = doors; blue = cold storage.' 900 1279 15
T 'Flat seams and clear rails; train drawn schematically.' 900 1313 15
T 'After layout approval: Quackville rough materials,' 900 1365 15
T 'offline navigation + lighting, then headset review.' 900 1399 15
T 'DRAWING ONLY / NO UNITY GEOMETRY CHANGES' 900 1475 13 '#e4c677'
$bmp.Save((Join-Path $dir 'layout.png'),[Drawing.Imaging.ImageFormat]::Png);$g.Dispose();$bmp.Dispose()



