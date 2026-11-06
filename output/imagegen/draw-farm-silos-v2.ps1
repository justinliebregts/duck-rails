$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Drawing
$dir=Join-Path $PSScriptRoot 'farm-silos-v2'
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
T 'FARM SILOS / CORN & CLIMBING' 65 30 30
T 'Top-down layout V2 | Proposal for review | 04 November 2026' 68 90 16
S -48 0 96 160 '#344b43';S -42 6 84 148 '#405a4b'
S -42 53.33 84 53.34 '#52634c'
for($z=0;$z -le 160;$z+=20){SiteLine -48 $z 48 $z '#61725f' 1;ST "$z" -54 ($z+1) 10}
function Corn([double]$x,[double]$z,[double]$w,[double]$d){
 S $x $z $w $d '#707a48'
 for($a=$x+1;$a -lt $x+$w;$a+=2.5){SiteLine $a ($z+1) $a ($z+$d-1) '#b8b66a' 3}
}
function Circle([double]$x,[double]$z,[double]$r,$c){$b=B $c;$g.FillEllipse($b,[single](65+($x+48-$r)*8),[single](150+(160-$z-$r)*8),[single]($r*16),[single]($r*16));$b.Dispose()}
function Hay([double]$x,[double]$z){S $x $z 8 6 '#b58d47';S ($x+1.3) ($z+1) 5.4 4 '#d0ad60';S ($x+2.6) ($z+2) 2.8 2 '#f2d58c'}
# Continuous farm lanes; buildings and corn pockets sit off these routes.
S -39 10 3 140 '#9c9374';S 38 10 3 140 '#9c9374'
S -36 47 74 3 '#9c9374';S -36 113 74 3 '#9c9374'
Corn -33 13 21 29;Corn 13 12 21 15;Corn 13 31 21 15
S -33 26 21 3 '#9c9374';S -33 135 21 3 '#9c9374'
ST 'CORN ROWS' -33 39 12 '#172e31';ST 'CORN ROWS' 13 24 12 '#172e31'
S -31 43 8 3 '#8b7760';ST 'WAGON' -30 46 9
S 15 49 8 3 '#8b7760';ST 'TRACTOR' 23 52 9
# Main farm: all primary footprints inside the middle third.
S 10 57 28 47 '#9c9374'
S 17 66 18 23 '#b17a65';ST 'BARN' 21 80 17 '#172e31'
SiteLine 26 66 26 89 '#704f46' 2
SiteLine 17 74 17 79 '#f2d58c' 7;SiteLine 25 89 29 89 '#f2d58c' 7
S 17 60 18 6 '#c39376';ST 'LOW ROOF' 19 64 10 '#172e31'
Hay 23 54;ST 'H1' 24 58 10 '#172e31'
Circle 22 98 5 '#a4b5ad';Circle 33 98 5 '#a4b5ad'
ST 'SILOS' 20 100 12 '#172e31'
S -34 69 18 14 '#b59673';ST 'FEED BARN' -33 78 11 '#172e31'
S -34 88 16 12 '#a28c72';ST 'WORKSHOP' -33 96 10 '#172e31'
Hay -31 63;ST 'H2' -30 66 10 '#172e31'
Circle -24 58 4 '#87afb5';ST 'WATER TOWER' -40 55 10
ST 'YARD' -28 87 10
# Secondary areas, extending through departure third.
Corn -33 122 21 29;Corn 13 121 21 13;Corn 13 138 21 13
S -33 135 21 3 '#9c9374'
ST 'CORN / OPEN GAPS' -34 146 10 '#172e31'
ST 'CORN ROWS' 13 132 11 '#172e31'
S -32 106 17 5 '#7d8b64';ST 'PENS' -29 110 10
S 16 106 14 5 '#8b7760';ST 'CART SHED' 16 110 10
Hay -22 115
foreach($z in @(12,30,44,119,139,151)){Circle -43 $z 1.8 '#789069';Circle 44 ($z+1) 1.8 '#789069'}
foreach($z in @(17,36,124,143)){SiteLine -35 $z -35 ($z+8) '#b6aa83' 2;SiteLine 36 $z 36 ($z+8) '#b6aa83' 2}
# Preserve existing sampled westward track curve.
function TX([double]$z){ -2*(1-[Math]::Cos([Math]::PI*$z/80)) }
for($z=0;$z -lt 160;$z+=1){S ((TX $z)-5) $z 10 1.1 '#657069'}
for($z=0;$z -lt 160;$z+=3){SiteLine ((TX $z)-2) $z ((TX $z)+2) $z '#888675' 3}
for($z=0;$z -lt 160;$z+=2){foreach($offset in @(-1.5,1.5)){SiteLine ((TX $z)+$offset) $z ((TX ($z+2))+$offset) ($z+2) '#e1e4db' 2}}
S -5.6 75 3.2 10 '#e4c677'
foreach($z in @(64,104)){SiteLine -14 $z 12 $z '#e4c677' 3}
foreach($z in @(53.33,106.67)){SiteLine -48 $z 48 $z '#f2d58c' 2}
ST '+Z / TRAVEL UP' -44 158 11
ST 'STOP' -12 81 9
T '01 / FULL BLOCK' 65 1450 17
T '96 x 160 m | 20 m grid | yellow lines divide thirds' 65 1484 12
T '02 / THE CENTRAL FARM' 900 150 22
T 'Barn + paired silos form the main destination.' 900 196 16
T 'Water tower anchors the opposite side of the yard.' 900 232 16
T 'Main buildings stay inside Z=53.3 to 106.7 m.' 900 268 16
T 'Doors at the barn front and rear keep a ground loop.' 900 304 15
T '03 / HAY-BALE CLIMB / SIDE STUDY' 900 380 22
T 'Pyramid stacks lead to a low roof, then barn roof.' 900 425 15
L 920 675 1580 675 '#d5dacc' 2
Rect 1180 555 150 120 '#c39376';Rect 1330 485 220 190 '#b17a65'
L 1330 485 1440 435 '#d5dacc' 4;L 1440 435 1550 485 '#d5dacc' 4
for($tier=0;$tier -lt 4;$tier++){Rect (970+$tier*45) (645-$tier*30) (210-$tier*45) 30 '#d0ad60';L (970+$tier*45) (645-$tier*30) 1180 (645-$tier*30) '#806533' 2}
Rect 1260 525 70 30 '#d0ad60';Rect 1295 495 35 30 '#d0ad60'
T 'H1: broad stepped stack' 920 705 13
T 'Low lean-to' 1185 705 13;T 'Barn roof' 1400 705 13
T 'Schematic heights: tune to existing hand locomotion.' 900 750 14
T 'H2: second pyramid to the feed-barn roof.' 900 783 14
T 'Keep climb optional; normal doors remain reachable.' 900 816 14
T '04 / FULL-LENGTH FARM CHARACTER' 900 890 22
T 'Approach: corn lanes, wagon, tractor, broken fences.' 900 936 15
T 'Departure: more corn, pens, cart shed and hay stacks.' 900 972 15
T 'Corn has cross-gaps and a clear perimeter bypass.' 900 1008 15
T 'Aim for enclosing rustle and silhouettes at duck height.' 900 1044 15
T 'Low rolling pasture at the sides; level farmyard.' 900 1080 15
T '05 / DRAWING REVIEW BEFORE BUILD' 900 1160 22
T 'Keep seams flat and the railway corridor clear.' 900 1206 15
T 'Tower is a landmark; climbing its tank is not assumed.' 900 1242 15
T 'Preserve loot counts and existing enemy coverage.' 900 1278 15
T 'After approval: rough Quackville blockout, offline' 900 1330 15
T 'navigation / lighting, then headset traversal review.' 900 1366 15
T 'PROPOSAL ONLY / NO UNITY GEOMETRY CHANGES' 900 1475 13 '#e4c677'
$bmp.Save((Join-Path $dir 'layout.png'),[Drawing.Imaging.ImageFormat]::Png);$g.Dispose();$bmp.Dispose()


