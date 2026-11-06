$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Drawing
$dir=Join-Path $PSScriptRoot 'nuclear-power-plant-v2'
New-Item -ItemType Directory -Force -Path $dir | Out-Null
$bmp=[Drawing.Bitmap]::new(2400,1900)
$g=[Drawing.Graphics]::FromImage($bmp)
$g.SmoothingMode='AntiAlias'
$g.Clear([Drawing.ColorTranslator]::FromHtml('#15252c'))
function B($c){[Drawing.SolidBrush]::new([Drawing.ColorTranslator]::FromHtml($c))}
function T($s,$x,$y,$size=17,$c='#e6ebe2'){$f=[Drawing.Font]::new('Arial',$size);$b=B $c;$g.DrawString($s,$f,$b,[single]$x,[single]$y);$f.Dispose();$b.Dispose()}
function Rect($x,$y,$w,$h,$c){$b=B $c;$g.FillRectangle($b,[single]$x,[single]$y,[single]$w,[single]$h);$b.Dispose()}
function L($x,$y,$xx,$yy,$c='#dce3d6',$w=2){$p=[Drawing.Pen]::new([Drawing.ColorTranslator]::FromHtml($c),[single]$w);$g.DrawLine($p,[single]$x,[single]$y,[single]$xx,[single]$yy);$p.Dispose()}
function E($x,$y,$w,$h,$c){$b=B $c;$g.FillEllipse($b,[single]$x,[single]$y,[single]$w,[single]$h);$b.Dispose()}
function C($x,$y,$r,$c,$w=2){$p=[Drawing.Pen]::new([Drawing.ColorTranslator]::FromHtml($c),[single]$w);$g.DrawEllipse($p,[single]($x-$r),[single]($y-$r),[single]($r*2),[single]($r*2));$p.Dispose()}
function Dash($x,$y,$xx,$yy,$c='#93acb0',$w=2){$p=[Drawing.Pen]::new([Drawing.ColorTranslator]::FromHtml($c),[single]$w);$p.DashStyle='Dash';$g.DrawLine($p,[single]$x,[single]$y,[single]$xx,[single]$yy);$p.Dispose()}
function S([double]$x,[double]$z,[double]$w,[double]$d,$c){Rect (80+($x+48)*8) (240+(160-$z-$d)*8) ($w*8) ($d*8) $c}
function SiteLine([double]$x,[double]$z,[double]$xx,[double]$zz,$c='#e7c97e',$w=3){L (80+($x+48)*8) (240+(160-$z)*8) (80+($xx+48)*8) (240+(160-$zz)*8) $c $w}
function ST($s,[double]$x,[double]$z,$size=12,$c='#e6ebe2'){T $s (80+($x+48)*8) (240+(160-$z)*8) $size $c}
function SE([double]$x,[double]$z,[double]$r,$c){E (80+($x+48-$r)*8) (240+(160-$z-$r)*8) ($r*16) ($r*16) $c}
function WallBox($x,$y,$w,$h){L $x $y ($x+$w) $y;L $x ($y+$h) ($x+$w) ($y+$h);L $x $y $x ($y+$h);L ($x+$w) $y ($x+$w) ($y+$h)}
function Drum($x,$y,$r=5){E ($x-$r) ($y-$r) ($r*2) ($r*2) '#d5ae59';C $x $y $r '#382f27' 1;L ($x-$r*.6) $y ($x+$r*.6) $y '#382f27' 1}
T 'NUCLEAR POWER PLANT / TOP-DOWN FLOOR PLAN' 80 35 35
T 'V2  /  Enterable cooling towers + waste-drum truck yard  /  Proposed dimensions in metres' 83 100 20
T 'NORTH / +Z IS UP ON EVERY PLAN     |     Roofs removed     |     Drawing for review before rough Quackville blockout' 83 143 16 '#a9b9b9'
T '01 / WHOLE BLOCK' 80 194 23
S -48 0 96 160 '#394e43'
for($z=0;$z -le 160;$z+=20){SiteLine -48 $z 48 $z '#52675b' 1;ST "$z" -54 ($z+1) 11}
S -4 0 8 160 '#5a6666';S -7 6 3 148 '#a19e89';S 4 6 3 148 '#a19e89'
foreach($z in @(53.33,106.67)){Dash 80 (240+(160-$z)*8) 848 (240+(160-$z)*8) '#bdd1b3' 2}
ST 'FLAT DEPARTURE SEAM' -43 158 11
ST 'FLAT APPROACH SEAM' -43 5 11
# Continuous connected paths on both sides, outside rail clearance.
foreach($x in @(-41,-12,9,42)){S $x 22 3 120 '#8d9582'}
foreach($z in @(38,67,94,124)){S -41 ($z-1.5) 34 3 '#8d9582';S 7 ($z-1.5) 38 3 '#8d9582'}
# Cooling towers, centre coordinates -26,67 and -26,94; diameter 22 m.
foreach($z in @(67,94)){
 SE -26 $z 11 '#d0c5a7';SE -26 $z 10.2 '#697a79';if($z -eq 94){SE -26 $z 7.3 '#6e7871'}else{SE -26 $z 7.3 '#477b88'}
 S -37 ($z-1.5) 22 3 '#c9b992';S -27.5 ($z-10) 3 20 '#c9b992'
 foreach($dx in @(-4.3,4.3)){foreach($dz in @(-4.3,4.3)){S (-26+$dx-.6) ($z+$dz-.6) 1.2 1.2 '#2c3c43'}}
 SiteLine -37 ($z-1.5) -37 ($z+1.5) '#f5cf70' 5;SiteLine -15 ($z-1.5) -15 ($z+1.5) '#f5cf70' 5
}
ST 'B / DRAINED' -35 95 11 '#f4edcf';ST 'A / DAMP' -34 68 11 '#f4edcf'
# Hall ground floor: 28 x 36 m. Both entrance gaps are visible.
S 12 55 28 36 '#b2a387';S 32 55 8 36 '#817f75'
foreach($z in @(63,78)){S 20 $z 6 8 '#445e64';ST 'T' 22 ($z+5) 12}
foreach($z in @(57,80)){S 33 $z 3 9 '#b9ab90';for($v=$z;$v -lt $z+9;$v+=1){SiteLine 33 $v 36 $v '#4c5656' 1}}
SiteLine 12 71 12 75 '#f5cf70' 6;SiteLine 40 71 40 75 '#f5cf70' 6
ST 'TURBINE HALL' 13 90 11 '#23363c'
SE 26 103 10 '#7c8d91';ST 'REACTOR' 19 105 10;ST 'SHELL' 21 102 10
ST 'exterior only' 18 99 9
# North support destinations.
S -39 120 25 19 '#a4ad99';S -37 122 21 15 '#487d8d';S -37 140 10 7 '#b2a387';ST 'PUMP' -37 145 10 '#23363c'
ST 'COOLING BASIN' -39 119 10
S 15 121 25 20 '#727f75'
foreach($x in @(19,29)){foreach($z in @(125,134)){S $x $z 5 3 '#3e535f'}}
ST 'TRANSFORMERS' 15 145 11
# Approach buildings with roof-off layouts and open doors.
S -38 35 14 12 '#b2a387';S -37 43 6 2 '#445e64';SiteLine -24 38 -24 41 '#f5cf70' 5
ST 'GATEHOUSE' -40 50 10
S -39 17 25 15 '#667574'
foreach($x in @(-36,-29,-22)){S $x 20 3 6 '#354b59'}
ST 'PARKING' -37 16 11
S 27 37 13 12 '#b2a387';S 36 39 2 7 '#445e64';SiteLine 27 40 27 44 '#f5cf70' 5
ST 'MAINTENANCE' 22 52 10
# Waste drums and spilled truck load, all east of walking verge.
S 11 16 29 18 '#777965';ST 'WASTE-DRUM YARD' 11 15 10 '#f2ce7a'
foreach($x in @(27,29,31,33,35,37,39)){foreach($z in @(18,20.5,23,25.5,28,30.5)){Drum (80+($x+48)*8) (240+(160-$z)*8) 5}}
S 14 25 5 8 '#b7a992';S 14 31 5 2 '#405d6d'
foreach($pos in @(@(16,21),@(20,22),@(23,19),@(24,25),@(19,18),@(25,28),@(21,26))){Drum (80+($pos[0]+48)*8) (240+(160-$pos[1])*8) 5}
SE 23 22 1.8 '#889b55';SE 19 19 1 '#889b55'
ST 'TRUCK' 11 35 10
# Outer-third supporting dressing; clear flat seams retained.
foreach($z in @(10,27,49,116,146,151)){foreach($x in @(-44,45)){SE $x $z 1.7 '#65836a'}}
foreach($z in @(10,151)){foreach($x in @(-30,-17,16,29)){SE $x $z 1.6 '#65836a'}}
foreach($z in @(20,42,116,142)){SiteLine -44 $z -44 ($z+10) '#aaa583' 2;SiteLine 45 $z 45 ($z+10) '#aaa583' 2}
for($z=0;$z -lt 160;$z+=3){SiteLine -2 $z 2 $z '#9b9680' 3};SiteLine -1.5 0 -1.5 160 '#e5e9df' 2;SiteLine 1.5 0 1.5 160 '#e5e9df' 2
S -1.6 75 3.2 10 '#edc875';ST 'STOP' -3.8 89 10
T '96 m wide x 160 m long / grid every 20 m' 80 1550 17
T 'Main plant: Z 53-107. Supporting yards fill the outer thirds.' 80 1589 15
T 'Clear rail corridor: X -4 to +4. Walkways: 3 m each side.' 80 1625 15
L 82 1690 242 1690 '#e8dfbc' 5;L 82 1680 82 1700;L 242 1680 242 1700
T '0' 77 1705 13;T '20 m' 216 1705 13
T 'Amber gaps = entrances / exits' 350 1690 15 '#f5cf70'
T 'All proposed dimensions require 3D / headset clearance checks.' 80 1770 15 '#a9b9b9'
T 'No Unity geometry or gameplay changed.' 80 1805 15 '#a9b9b9'
# Two matching enlarged tower ground plans: 22 m diameter, 20 px/m.
function Tower($cx,$cy,$drained){
 E ($cx-220) ($cy-220) 440 440 '#c9be9d';E ($cx-205) ($cy-205) 410 410 '#9c9e8c'
 $pool='#447b8b';if($drained){$pool='#6e7871'}
 E ($cx-145) ($cy-145) 290 290 $pool
 Rect ($cx-220) ($cy-30) 440 60 '#d0bb89';Rect ($cx-30) ($cy-199) 60 398 '#d0bb89'
 # Four support clusters avoid the 3m circulation cross.
 foreach($a in @(45,135,225,315)){$rad=$a*[Math]::PI/180;$px=$cx+[Math]::Cos($rad)*120;$py=$cy+[Math]::Sin($rad)*120;Rect ($px-12) ($py-12) 24 24 '#32474f'}
 L ($cx-220) ($cy-30) ($cx-220) ($cy+30) '#f5cf70' 9;L ($cx+220) ($cy-30) ($cx+220) ($cy+30) '#f5cf70' 9
 T 'WEST' ($cx-292) ($cy-17) 13 '#f5cf70';T 'EAST' ($cx+233) ($cy-17) 13 '#f5cf70'
 T '3 m path' ($cx-45) ($cy-13) 13 '#263a40'
 T '3 m perimeter walk' ($cx-105) ($cy+154) 13 '#263a40'
 # Retained overhead fill shown dashed; clear centre sky opening.
 Dash ($cx-120) ($cy-120) ($cx+120) ($cy-120) '#b9d3d0' 3
 Dash ($cx-120) ($cy-120) ($cx-120) ($cy-50) '#b9d3d0' 3
 Dash ($cx+120) ($cy-120) ($cx+120) ($cy-50) '#b9d3d0' 3
 T 'overhead fill / pipes' ($cx-100) ($cy-157) 12 '#d3e5db'
 if($drained){
   Rect ($cx-100) ($cy+58) 50 24 '#a59071';Rect ($cx+57) ($cy+72) 45 31 '#a59071'
   T 'fallen fill' ($cx-42) ($cy+104) 12
   T 'dry basin / level floor' ($cx-79) ($cy-76) 12
 }else{
   T 'shallow basin' ($cx-75) ($cy-81) 13
   T 'water / 0.1 m' ($cx-70) ($cy+76) 13
 }
 C $cx $cy 220 '#e5dfc9' 3
 L ($cx-220) ($cy+245) ($cx+220) ($cy+245) '#b5c5c2' 1
 T '22 m diameter' ($cx-75) ($cy+255) 14
}
T '02 / TOWER A - DAMP' 970 194 23
T 'GROUND LEVEL / roof and upper shell omitted' 970 237 15 '#a9b9b9'
Tower 1250 520 $false
T 'Two 3 m entrances between base supports.' 970 830 16
T 'Broad crosswalk + loop; low pipes stay off paths.' 970 866 16
T 'Partial fill overhead; broken central opening to sky.' 970 902 16
T '03 / TOWER B - DRAINED' 1650 194 23
T 'GROUND LEVEL / same shell, different interior' 1650 237 15 '#a9b9b9'
Tower 1920 520 $true
T 'Two exits link the ring walk and drained basin.' 1650 830 16
T 'Fallen panels sit beside clear routes, not across them.' 1650 866 16
T 'Most fill missing: look up into the huge hollow shell.' 1650 902 16
# Hall details, both at 14 px/m and identical bounds, north up.
$hx=970;$hy=1040;$hs=14
function Hall($x,$z,$w,$d,$c){Rect ($hx+$x*$hs) ($hy+(36-$z-$d)*$hs) ($w*$hs) ($d*$hs) $c}
function HL($x,$z,$xx,$zz,$c='#e5dfc9',$w=2){L ($hx+$x*$hs) ($hy+(36-$z)*$hs) ($hx+$xx*$hs) ($hy+(36-$zz)*$hs) $c $w}
function HT($s,$x,$z,$size=12,$c='#e6ebe2'){T $s ($hx+$x*$hs) ($hy+(36-$z)*$hs) $size $c}
T '04 / TURBINE HALL - GROUND' 970 977 22
T '28 x 36 m / main floor +0.0 m' 970 1014 15 '#a9b9b9'
Hall 0 0 28 36 '#a79e87';WallBox $hx $hy (28*$hs) (36*$hs)
Hall 20 0 8 36 '#807e72'
foreach($z in @(8,23)){Hall 8 $z 6 8 '#415f68';HT 'TURBINE' 8.3 ($z+5) 10}
foreach($z in @(2,25)){Hall 21 $z 3 9 '#c9b893';for($n=$z;$n -lt $z+9;$n++){HL 21 $n 24 $n '#5e6461' 1};HT 'UP' 21.2 ($z+6) 11 '#243b40'}
HL 0 16 0 20 '#f5cf70' 8;HL 28 16 28 20 '#f5cf70' 8
HL 0 18 28 18 '#edcb76' 3;HL 4 5 4 31 '#edcb76' 3;HL 4 5 18 5 '#edcb76' 3;HL 4 31 18 31 '#edcb76' 3;HL 18 5 18 31 '#edcb76' 3
HT 'ENTRY >' .5 20 10 '#263b40';HT 'EXIT >' 23 20 10 '#263b40'
HT 'WORKBENCH / STORAGE' 2 3 10 '#263b40';Hall 1 1 13 1 '#485f66'
T 'Wide machinery loop; straight cross-hall escape route.' 970 1580 15
T '3 m stairs + landings at both ends of upper gallery.' 970 1616 15
$hx=1500
T '05 / CONTROL GALLERY - UPPER' 1500 977 18
T 'Same footprint / gallery +3.0 m / dashed = below' 1500 1014 15 '#a9b9b9'
Hall 0 0 28 36 '#283e47';Hall 20 0 8 36 '#a79e87';WallBox $hx $hy (28*$hs) (36*$hs)
foreach($z in @(8,23)){Dash ($hx+8*$hs) ($hy+(36-$z-8)*$hs) ($hx+14*$hs) ($hy+(36-$z-8)*$hs);Dash ($hx+8*$hs) ($hy+(36-$z)*$hs) ($hx+14*$hs) ($hy+(36-$z)*$hs)}
foreach($z in @(2,25)){Hall 21 $z 3 9 '#c9b893';for($n=$z;$n -lt $z+9;$n++){HL 21 $n 24 $n '#5e6461' 1};HT 'DN' 21.1 ($z+6) 10 '#243b40'}
Hall 26 12 1.5 12 '#6baf85';HT 'OPEN TO' 3 22 16;HT 'HALL BELOW' 3 19 16
HL 20 0 20 36 '#e6d7a1' 3;HT 'CONSOLES' 20.4 21 10 '#243b40'
HT '3 m aisle' 21 17 10 '#243b40'
T '8 m-wide gallery overlooks the turbines.' 1500 1580 15
T 'Consoles along east wall; two separate stairs down.' 1500 1616 15
# Compact waste yard detail and clear legend.
T '06 / SPILLED LOAD' 1995 977 22
T 'Approach yard / schematic inset' 1995 1014 14 '#a9b9b9'
Rect 2000 1050 325 350 '#737563'
Rect 2020 1080 75 130 '#b7a992';Rect 2020 1080 75 33 '#405d6d'
foreach($x in @(2140,2178,2216,2254,2292)){foreach($y in @(1090,1130,1170,1210)){Drum $x $y 12}}
E 2110 1260 72 44 '#929f55';E 2220 1330 50 30 '#929f55'
foreach($p in @(@(2040,1250),@(2090,1240),@(2160,1290),@(2210,1250),@(2260,1310),@(2100,1350))){Drum $p[0] $p[1] 12}
T 'TRUCK' 2019 1215 12
T 'Rows of drums + scattered load.' 1995 1430 15
T 'Some tipped / split / small spills.' 1995 1466 15
T 'Radiation labels on barrel sides.' 1995 1502 15
T 'Keep the return path clear.' 1995 1538 15
T 'Scenery; no damage mechanic added.' 1995 1580 14 '#a9b9b9'
L 970 1690 2320 1690 '#4e666c' 2
T 'COOLING TOWER INTERIORS / REAL STRUCTURE, ADAPTED FOR PLAY' 970 1720 20
T 'Real towers: collection basin, supports, cooling fill and water-distribution pipes below a tall air shaft.' 970 1763 16
T 'Game proposal: damaged / decommissioned interiors; partial fill at +4.5 m or higher, open sky above.' 970 1799 16
T 'All routes return to two exits. Water and waste spills are visual dressing; exact traversal remains for review.' 970 1835 16
$g.Dispose();$bmp.Save((Join-Path $dir 'floor-plan.png'),[Drawing.Imaging.ImageFormat]::Png);$bmp.Dispose()
Write-Output (Join-Path $dir 'floor-plan.png')



