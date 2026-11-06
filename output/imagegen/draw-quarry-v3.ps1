$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Drawing
$dir=Join-Path $PSScriptRoot 'quarry-v3'
New-Item -ItemType Directory -Force $dir | Out-Null
$bmp=[Drawing.Bitmap]::new(1900,1420);$g=[Drawing.Graphics]::FromImage($bmp);$g.SmoothingMode='AntiAlias';$g.Clear([Drawing.ColorTranslator]::FromHtml('#18282c'))
$ox=70
function B($c){[Drawing.SolidBrush]::new([Drawing.ColorTranslator]::FromHtml($c))}
function P([double]$x,[double]$z){[Drawing.PointF]::new([single]($ox+($x+48)*6),[single](190+(160-$z)*6))}
function T($s,$x,$y,$size=16,$c='#e7e8df'){$f=[Drawing.Font]::new('Arial',$size);$b=B $c;$g.DrawString($s,$f,$b,[single]$x,[single]$y);$b.Dispose();$f.Dispose()}
function Rect([double]$x,[double]$z,[double]$w,[double]$d,$c){$p=P $x ($z+$d);$b=B $c;$g.FillRectangle($b,$p.X,$p.Y,[single]($w*6),[single]($d*6));$b.Dispose()}
function L($x,$z,$xx,$zz,$c='#a8b9af',$w=2){$pen=[Drawing.Pen]::new([Drawing.ColorTranslator]::FromHtml($c),$w);$g.DrawLine($pen,(P $x $z),(P $xx $zz));$pen.Dispose()}
function Poly($coords,$c){$pts=[Drawing.PointF[]]@($coords|ForEach-Object {P $_[0] $_[1]});$b=B $c;$g.FillPolygon($b,$pts);$b.Dispose()}
function Label($s,$x,$z,$size=12,$c='#edf0df'){$p=P $x $z;T $s $p.X $p.Y $size $c}
function Path($pts,$c='#d9bf83',$width=24){for($i=1;$i -lt $pts.Count;$i++){L $pts[$i-1][0] $pts[$i-1][1] $pts[$i][0] $pts[$i][1] $c $width}}
function Base($underground){Rect -48 0 96 160 '#35483f';Rect -42 6 84 148 '#515b4b';if($underground){Rect -42 6 84 148 '#303e3e'};foreach($z in @(0,40,80,120,160)){L -48 $z 48 $z '#65705d' 1;Label "$z" -54 ($z+1) 10};foreach($z in @(53.33,106.67)){L -48 $z 48 $z '#7d846d' 1};Rect -7 0 14 160 '#434c45'}
function Pits {Poly @(@(-41,56),@(-34,49),@(-18,51),@(-11,62),@(-11,101),@(-22,112),@(-37,105),@(-41,89)) '#a49c7f';Poly @(@(-36,61),@(-29,57),@(-20,59),@(-16,68),@(-16,97),@(-24,105),@(-33,99),@(-36,86)) '#777f72';Rect -30 70 10 24 '#3f5253';Poly @(@(11,60),@(22,52),@(36,57),@(41,73),@(40,101),@(27,111),@(14,102)) '#929487';Poly @(@(17,66),@(25,59),@(32,65),@(36,78),@(34,96),@(26,103),@(19,96)) '#68786d';Rect 20 70 10 24 '#3f5253'}
function Rails {for($z=2;$z -lt 160;$z+=4){L -2 $z 2 $z '#82918b' 2};L -1.5 0 -1.5 160 '#d2d9cf' 2;L 1.5 0 1.5 160 '#d2d9cf' 2}
T 'TWIN PITS / a mine beneath the railway' 65 25 32
T 'V3 layout proposal  |  04 November 2026  |  96 x 160 m block  |  +Z / departure is up' 68 88 17 '#bdc7c0'
T '01   SURFACE' 70 142 21;T '02   UNDERGROUND / roof removed' 730 142 21
Base $false;Pits
Path @(@(-9,39),@(-26,46),@(-36,58),@(-36,74)) '#e6c262' 17
Path @(@(-36,74),@(-32,82),@(-25,82)) '#bdad86' 17
Label 'CLIMB' -40 48 11 '#ffe39a';Label '+2 m rim' -40 108 12
Label 'WEST' -34 91 12;Label '-6 m' -29 86 12
Path @(@(10,43),@(33,51),@(38,67),@(38,96),@(28,104),@(20,96),@(20,82),@(25,82)) '#bdad86' 18
Label 'EAST' 21 91 12;Label '-6 m' 23 86 12
Label 'Haul ramp' 18 116 12
Path @(@(-10,42),@(10,42)) '#b9b19b' 15;Path @(@(-11,119),@(11,119)) '#b9b19b' 15
foreach($rock in @(@(-38,18),@(20,17),@(-34,133),@(23,134),@(32,38),@(-38,118))){Poly @(@($rock[0],$rock[1]),@(($rock[0]+10),($rock[1]+2)),@(($rock[0]+8),($rock[1]+12)),@(($rock[0]+2),($rock[1]+9))) '#93917c'}
Rect -33 32 16 8 '#839b98';Label 'Excavator' -39 30 11
Rect 18 120 16 8 '#839b98';Label 'Loader' 18 119 11
Rect -37 145 16 6 '#839b98';Label 'Spares' -37 144 11
Rect 17 32 10 7 '#839b98';Label 'Truck' 16 31 11
Label 'Gravel piles' 13 151 11
Rails;Rect -1.8 75 3.6 10 '#e6c262';Label 'STOP' -5 92 10 '#ffe4a0'
Label 'Level seam' -40 159 10;Label 'Level seam' -40 5 10
$ox=730;Base $true;Pits
# Three distinct branches share a junction in each pit, forming traversable loops.
$routes=@(@(@(-25,82),@(-15,98),@(0,101),@(15,98),@(25,82)),@(@(-25,82),@(-12,82),@(12,82),@(25,82)),@(@(-25,82),@(-15,66),@(0,63),@(15,66),@(25,82)))
foreach($route in $routes){Path $route '#14282c' 38;Path $route '#8eafae' 24}
Rect 7 87 8 7 '#e4c476';Path @(@(11,82),@(11,87)) '#e4c476' 16
Label 'Lit office' 8 114 12 '#ffe1a1';L 14 108 12 94 '#e4c476' 2
Label '1  North branch' -19 106 12
Label '2  Direct branch' -19 84 12 '#18282c'
Label '3  South branch' -19 61 12
Label 'West junction' -41 77 10;Label 'East junction' 22 77 10
Label 'All three connect both pits' -35 44 14
Label '4 m clear width / 3 m clear height' -39 37 12
Label 'Track above / level at Y=0' -36 142 12 '#bdc7c0'
for($z=116;$z -le 134;$z+=3){L 0 $z 0 ($z+1) '#d2d9cf' 2};for($z=16;$z -le 54;$z+=3){L 0 $z 0 ($z+1) '#d2d9cf' 2}
T 'THE NEW DIRECTION' 1380 185 21
T "Two excavations, one on each side.`nThree tunnel branches cross beneath`nthe railway and reconnect at both pits.`nA small lit office sits off the middle`nbranch, spilling light into the junction." 1380 235 16
T 'ONE PIT HAS A RAISED LIP' 1380 425 20
T "West: climb over a +2 m rocky rim,`nthen descend on broad ledges.`nNo surface cut-through in the rim.`nThe tunnel network provides another`nway out through the east pit." 1380 475 16
T 'SURFACE CHARACTER' 1380 665 20
T "Excavator, loader, parked haul truck,`nspares and gravel piles.`nMain pits stay around the middle third;`nsmaller work areas fill both ends.`nRailway and block seams stay level." 1380 715 16
T 'PROPOSED DIMENSIONS' 1380 900 20
T "Pit / tunnel floor: -6 m`nTunnel ceiling: -3 m`nRail surface: 0 m`nRaised west rim: +2 m`nOffice: 8 x 7 m, outside rail corridor" 1380 950 16
T '03   SECTION / west pit to east pit through the direct branch' 70 1170 20
# Section with solid rail cover and hollow tunnel. Schematic horizontal scale.
$pen=[Drawing.Pen]::new([Drawing.ColorTranslator]::FromHtml('#bca982'),4)
$points=@(@(80,1280),@(165,1280),@(205,1240),@(245,1240),@(295,1370),@(425,1370),@(425,1280),@(775,1280),@(775,1370),@(905,1370),@(1000,1280),@(1250,1280))
for($i=1;$i -lt $points.Count;$i++){$g.DrawLine($pen,[single]$points[$i-1][0],[single]$points[$i-1][1],[single]$points[$i][0],[single]$points[$i][1])};$pen.Dispose()
$b=B '#8eafae';$g.FillRectangle($b,425,1325,350,45);$b.Dispose()
T '+2 rim' 170 1210 12;T '-6 pit' 310 1380 12;T 'solid cover / track at 0' 480 1284 12;T 'tunnel: floor -6 / roof -3' 480 1342 12 '#18282c';T '-6 pit' 820 1380 12;T 'ramp to surface' 985 1295 12
T "DRAWING FOR REVIEW`nGeometry, climbs and lighting untested." 1380 1260 16 '#e4c476'
$bmp.Save((Join-Path $dir 'twin-mine-plan.png'),[Drawing.Imaging.ImageFormat]::Png);$g.Dispose();$bmp.Dispose()

