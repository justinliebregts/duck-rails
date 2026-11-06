$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Drawing
$dir=Join-Path $PSScriptRoot 'quarry-v2'
New-Item -ItemType Directory -Force $dir | Out-Null
$bmp=[Drawing.Bitmap]::new(1600,1510)
$g=[Drawing.Graphics]::FromImage($bmp)
$g.SmoothingMode='AntiAlias'
$g.Clear([Drawing.ColorTranslator]::FromHtml('#18282c'))
function B($c){[Drawing.SolidBrush]::new([Drawing.ColorTranslator]::FromHtml($c))}
function P([double]$x,[double]$z){[Drawing.PointF]::new([single](80+($x+48)*8),[single](160+(160-$z)*8))}
function T($s,$x,$y,$size=17,$c='#e7e8df'){$f=[Drawing.Font]::new('Arial',$size);$b=B $c;$g.DrawString($s,$f,$b,[single]$x,[single]$y);$b.Dispose();$f.Dispose()}
function Rect($x,$z,$w,$d,$c){$p=P $x ($z+$d);$b=B $c;$g.FillRectangle($b,$p.X,$p.Y,[single]($w*8),[single]($d*8));$b.Dispose()}
function L($x,$z,$xx,$zz,$c='#a8b9af',$w=2){$pen=[Drawing.Pen]::new([Drawing.ColorTranslator]::FromHtml($c),$w);$g.DrawLine($pen,(P $x $z),(P $xx $zz));$pen.Dispose()}
function Poly($coords,$c){$pts=[Drawing.PointF[]]@($coords|ForEach-Object {P $_[0] $_[1]});$b=B $c;$g.FillPolygon($b,$pts);$b.Dispose()}
function Label($s,$x,$z,$size=13,$c='#edf0df'){$p=P $x $z;T $s $p.X $p.Y $size $c}
function Path($pts,$c='#d9bf83',$width=24){for($i=1;$i -lt $pts.Count;$i++){L $pts[$i-1][0] $pts[$i-1][1] $pts[$i][0] $pts[$i][1] $c $width}}
function Building([double]$x,[double]$z,[double]$w,[double]$d,$label){Rect ($x-1) ($z-1) ($w+2) ($d+2) '#a1947b';Rect $x $z $w $d '#79654c';Label $label ($x+1) ($z+$d-1);L ($x+$w) ($z+3) ($x+$w) ($z+6) '#fff2ba' 7;L ($x+3) ($z+$d) ($x+6) ($z+$d) '#fff2ba' 7}
T 'QUARRY / excavation + loading yard' 70 30 31
T 'Top-down layout V2     |     proposed geometry     |     04 November 2026' 73 90 17 '#bdc7c0'
Rect -48 0 96 160 '#35483f';Rect -42 6 84 148 '#56604b'
for($z=0;$z -le 160;$z+=20){L -48 $z 48 $z '#64705e' 1;Label "$z" -54 ($z+1) 11}
Rect -7 0 14 160 '#434c45'
foreach($z in @(53.33,106.67)){L -48 $z 48 $z '#a6ab88' 2}
# Excavation is east of the track; outer eastern crest tapers before the seam.
Poly @(@(10,52),@(30,52),@(39,60),@(41,91),@(34,105),@(10,105)) '#989586'
Poly @(@(14,59),@(29,59),@(35,66),@(36,88),@(30,98),@(14,98)) '#7c7d70'
Poly @(@(18,66),@(28,66),@(32,72),@(32,86),@(27,92),@(18,92)) '#565e59'
Rect 38 66 3 22 '#b1a78c'
# Level circulation along both train shoulders, crossing beyond stopped carriage.
Path @(@(-12,20),@(-12,140)) '#b1a383'
Path @(@(9,35),@(9,126)) '#b1a383'
Path @(@(-26,47),@(12,47),@(16,54))
Path @(@(-26,116),@(14,116))
Path @(@(-12,70),@(-36,70),@(-36,91),@(-12,91))
# Each ramp is 26 m plan length, descends 2 m, with level joins to the floor.
Path @(@(16,54),@(16,80),@(20,80)) '#f0cf80' 32
Path @(@(24,108),@(24,116),@(14,116),@(14,114),@(30,114),@(30,101)) '#b1a383'
Path @(@(30,101),@(30,75)) '#f0cf80' 32
Label 'A' 13 62 18 '#18282c';Label 'B' 27 94 18 '#18282c'
Label '-2 m' 20 85 14
Label '-1 m' 31 64 12
Label '+1' 38 83 11 '#26342c'
# Main site buildings on level pads.
Building -36 54 20 13 '1  Office'
Building -37 94 22 17 "2  Repair`nbay"
Building 14 105 18 9 '3  Loader'
Rect 34 94 4 10 '#839b98';Rect 32 101 6 2 '#839b98'
Label 'belt' 32 103 11 '#18282c'
# Small entrance/departure areas populate the full block without moving encounters.
Building -37 25 11 9 '4'
Rect -25 21 6 18 '#8b9992';Label 'scale' -26 19 11
Rect -37 126 18 9 '#839b98';Label '5  Spares' -37 125 12
Rect 15 24 12 7 '#839b98';Label '6  Loader' 14 22 12
foreach($rock in @(@(25,38,12,9),@(17,128,11,10),@(30,139,9,8),@(-37,141,9,6),@(32,13,7,6),@(-37,12,8,6))){Poly @(@($rock[0],$rock[1]),@(($rock[0]+$rock[2]),($rock[1]+2)),@(($rock[0]+$rock[2]-2),($rock[1]+$rock[3])),@(($rock[0]+2),($rock[1]+$rock[3]-1))) '#92917d'}
Label 'Gravel' 15 126 12;Label 'stockpiles' 15 122 12
foreach($tree in @(@(-40,42),@(-39,117),@(-16,145),@(38,121),@(39,32),@(17,147),@(-16,14))){Poly @(@($tree[0],$tree[1]),@(($tree[0]+3),($tree[1]+3)),@($tree[0],($tree[1]+6)),@(($tree[0]-3),($tree[1]+3))) '#344d3b'}
# Track is the actual straight alignment, 3 m gauge; train mark is schematic.
for($z=2;$z -lt 160;$z+=4){L -2 $z 2 $z '#79847c' 3}
L -1.5 0 -1.5 160 '#d2d9cf' 2;L 1.5 0 1.5 160 '#d2d9cf' 2
Rect -1.8 75 3.6 10 '#e6c262';Label 'STOP' -5 92 12 '#ffe4a0'
Label 'Flat seam' -39 159 11;Label 'Flat seam' -39 5 11
T '+Z / EXIT' 690 125 13;T 'ENTRY / train travels upward' 80 1450 14
T '96 x 160 m   /   20 m grid   /   schematic train footprint' 80 1480 12 '#bdc7c0'
T '01   TWO WAYS OUT OF THE PIT' 900 170 20
T "Dry floor at -2 m, rock bench at -1 m.`nA and B: 4 m wide, 26 m run for 2 m fall.`nLevel joins at top and bottom add length.`nBoth return to the train without jumping." 900 215 17
T '02   CENTRAL WORKSITE' 900 370 20
T "1  Office / lockers: 20 x 13 m`n2  Open repair bay: 22 x 17 m`n3  Loading shed: 18 x 9 m`nConveyor on the east lip, outside exit B.`nTwo exits per building; room plans next." 900 415 17
T '03   SCENERY THROUGH THE WHOLE BLOCK' 900 600 20
T "Approach: gate hut (4), weighbridge,`nparked loader (6), rubble and scrub.`nDeparture: spare-parts racks (5), gravel`npiles, boulders and a broken fence line.`nLow eastern crest rises to +1 m." 900 645 17
T '04   TERRAIN SECTION / WEST TO EAST' 900 825 20
T 'Schematic at pit centre; heights exaggerated' 900 865 13 '#bdc7c0'
$pts=@(@(910,965),@(1040,965),@(1080,965),@(1120,965),@(1160,995),@(1190,995),@(1210,1025),@(1290,1025),@(1310,995),@(1360,995),@(1390,935),@(1415,935),@(1460,965),@(1510,965))
$pen=[Drawing.Pen]::new([Drawing.ColorTranslator]::FromHtml('#e0c58c'),3)
for($i=1;$i -lt $pts.Count;$i++){$g.DrawLine($pen,[single]$pts[$i-1][0],[single]$pts[$i-1][1],[single]$pts[$i][0],[single]$pts[$i][1])};$pen.Dispose()
T 'west / 0' 910 1040 13;T 'rail / 0' 1060 925 13;T '-2 m' 1230 1040 13;T '+1 m' 1375 900 13;T 'edge / 0' 1450 1040 13
T '05   PRESERVED CONSTRAINTS' 900 1130 20
T "Level railway; 6 m flat perimeter proposal.`nSand = paths, yellow = graded pit exits.`nTan = buildings, blue-grey = static equipment.`nKeep six coal per set, three on each side.`nEnemy encounters remain in central east area." 900 1175 17
T 'FOR LAYOUT APPROVAL / NO UNITY ASSETS CHANGED' 900 1410 15 '#e6c262'
$bmp.Save((Join-Path $dir 'quarry-plan.png'),[Drawing.Imaging.ImageFormat]::Png)
$g.Dispose();$bmp.Dispose()



