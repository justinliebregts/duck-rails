$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Drawing
$folder=Join-Path $PSScriptRoot 'skyscraper-layout-v2'
New-Item -ItemType Directory -Force $folder | Out-Null
$bmp=[Drawing.Bitmap]::new(1600,1580)
$g=[Drawing.Graphics]::FromImage($bmp);$g.SmoothingMode='AntiAlias';$g.Clear([Drawing.ColorTranslator]::FromHtml('#f5f1e7'))
$font=[Drawing.Font]::new('Arial',16);$small=[Drawing.Font]::new('Arial',12);$title=[Drawing.Font]::new('Arial',30,[Drawing.FontStyle]::Bold);$head=[Drawing.Font]::new('Arial',20,[Drawing.FontStyle]::Bold)
function Brush($c){[Drawing.SolidBrush]::new([Drawing.ColorTranslator]::FromHtml($c))}
function Text($s,$x,$y,$f=$font,$c='#263c42'){$b=Brush $c;$g.DrawString($s,$f,$b,[single]$x,[single]$y);$b.Dispose()}
function P([double]$x,[double]$z){[Drawing.PointF]::new([single](70+($x+48)*8),[single](160+(160-$z)*8))}
function Box([double]$x,[double]$z,[double]$w,[double]$d,$c){$p=P $x ($z+$d);$b=Brush $c;$g.FillRectangle($b,$p.X,$p.Y,[single]($w*8),[single]($d*8));$b.Dispose()}
function Line([double]$x,[double]$z,[double]$xx,[double]$zz,$c='#526467',$w=2){$pen=[Drawing.Pen]::new([Drawing.ColorTranslator]::FromHtml($c),$w);$g.DrawLine($pen,(P $x $z),(P $xx $zz));$pen.Dispose()}
function Label($s,$x,$z,$f=$small){$p=P $x $z;Text $s $p.X $p.Y $f}
function Outline([double]$x,[double]$z,[double]$w,[double]$d,$c){Box $x $z $w $d '#4a5d60';Box ($x+.45) ($z+.45) ($w-.9) ($d-.9) $c}
function Stair([double]$x,[double]$z){Outline $x $z 5 8 '#f2e4c1';for($j=1;$j -lt 8;$j++){Line $x ($z+$j) ($x+5) ($z+$j) '#867453' 1};Label 'S' ($x+1) ($z+6)}
function Tower([double]$x,[double]$z,$name){
 Box $x $z 26 24 '#c6c6ba'
 for($a=$x+3;$a -lt $x+25;$a+=6){for($b=$z+3;$b -lt $z+23;$b+=6){Box $a $b 1 1 '#727c78'}}
 Stair ($x+2) ($z+2);Stair ($x+19) ($z+14)
 Outline ($x+8) ($z+16) 8 6 '#35454e'
 Line ($x+12) ($z+16) ($x+12) ($z+22) '#bec8c4' 2
 $ep=P ($x+9) ($z+21);Text 'E  E' $ep.X $ep.Y $small '#f5f1e7'
 Box ($x+8) ($z+2) 3 2 '#bfa16f';Box ($x+18) ($z+4) 4 2 '#bfa16f'
 Box ($x+2) ($z+16) 2 4 '#aa8b63';Box ($x+21) ($z+9) 3 1 '#818f95'
 Label $name ($x+10) ($z+12) $head
}
function Yard([double]$x,[double]$z,[double]$w,[double]$d,$name){Outline $x $z $w $d '#d8ccb3';Label $name ($x+2) ($z+$d-2);for($j=0;$j -lt 3;$j++){Box ($x+3+$j*8) ($z+3) 5 7 '#ab9770';for($a=1;$a -lt 5;$a++){Line ($x+3+$j*8+$a) ($z+3) ($x+3+$j*8+$a) ($z+10) '#e3d3b0' 1}}}
function Bridge([double]$x,[double]$z,[double]$w,[double]$d,$c){Outline $x $z $w $d $c;for($q=$x+1;$q -lt $x+$w;$q+=2){Line $q ($z+.5) $q ($z+$d-.5) '#dbe7d7' 1}}
Text 'SKYSCRAPER WORKS / FOUR-TOWER LOOP' 66 25 $title
Text 'V2 layout proposal  |  Open-air skybridges + a fallen-crane shortcut  |  Hospital is a separate block' 70 83
Text 'TRUE TOP-DOWN / all elevated connections overlaid' 70 125 $small
Box -48 0 96 160 '#c6cfbb';Box -42 6 84 148 '#dce0d3';Box -7 0 14 160 '#adb8af'
for($z=2;$z -lt 160;$z+=3){Line -2 $z 2 $z '#77857d' 2};Line -1 0 -1 160 '#43534d' 2;Line 1 0 1 160 '#43534d' 2
foreach($z in @(53.33,106.67)){Line -48 $z 48 $z '#91a18f' 1}
for($z=12;$z -lt 154;$z+=14){foreach($x in @(-45,45)){$p=P $x $z;$b=Brush '#8b9b79';$g.FillEllipse($b,$p.X-6,$p.Y-6,12,12);$b.Dispose()}}
Yard -39 124 29 24 'STEEL DELIVERY';Yard 11 123 28 25 'FORMWORK'
Outline -38 13 27 12 '#c6b28c';Label 'SITE OFFICES' -36 22
Outline 12 13 26 12 '#b8c5bd';Label 'WORKSHOP' 14 22
Yard -39 32 28 15 'MATERIALS';Yard 11 33 28 14 'LOADING'
Outline -37 113 22 6 '#b4c2b8';Label 'GENERATOR' -36 118
Outline 15 113 23 6 '#b4c2b8';Label 'TOOL SHED' 16 118
Box -2 72 4 12 '#d1a04b'
# Lower loop has two cross-rail decks and side links. North deck rises to +12 m.
Bridge -27 78 4 8 '#88b5ae';Bridge 23 78 4 8 '#88b5ae'
Tower -38 54 'A';Tower 12 54 'B';Tower -38 86 'C';Tower 12 86 'D'
Bridge -12 64 24 4 '#88b5ae';Bridge -12 96 24 4 '#648d9b'
Label '+8 m' -4 63;Label '+12 m' -5 104
Label '+8' -27 85;Label '+8' 23 85
# Fallen lattice jib connects A to D at +16 m; a fixed deck is proposed inside its truss.
$sx=-28.0;$sz=68.0;$ex=25.0;$ez=91.0;$nx=-0.67;$nz=1.34
Line $sx $sz $ex $ez '#b77e32' 21
Line ($sx+$nx) ($sz+$nz) ($ex+$nx) ($ez+$nz) '#805827' 2
Line ($sx-$nx) ($sz-$nz) ($ex-$nx) ($ez-$nz) '#805827' 2
for($i=0;$i -lt 10;$i++){$t=$i/10.0;$tt=($i+1)/10.0;$x=$sx+($ex-$sx)*$t;$z=$sz+($ez-$sz)*$t;$xx=$sx+($ex-$sx)*$tt;$zz=$sz+($ez-$sz)*$tt;Line ($x+$nx) ($z+$nz) ($xx-$nx) ($zz-$nz) '#f3d29b' 2}
Label 'FALLEN CRANE +16 m' -18 94
# Ground return lanes and entrances; overhead decks have no supports in the rail reserve.
foreach($x in @(-41,41)){Line $x 50 $x 112 '#e9d9b6' 5}
foreach($z in @(59,102)){Line -12 $z -7 $z '#e9d9b6' 6;Line 7 $z 12 $z '#e9d9b6' 6;Line -12 ($z-1.5) -12 ($z+1.5) '#faf4df' 5;Line 12 ($z-1.5) 12 ($z+1.5) '#faf4df' 5}
Label 'TRAIN' -4 71
Label 'ENTRY / TRAVEL UP' -13 8;Label 'EXIT / +Z' -8 156
for($z=0;$z -le 160;$z+=40){$p=P -48 $z;Text "$z" 26 ($p.Y-9) $small}
Text '96 m wide x 160 m long | proposed 6 m flat perimeter' 70 1465 $small
Text 'S = stairs | E = shaft | Grey = open slabs | Tan = supplies' 70 1497 $small
Text 'Line overlaps are at DIFFERENT heights. Dimensions are proposals.' 70 1529 $small

Text '01 / THE ROUTE' 890 155 $head
$notes=@('A/B: front towers beside the train.','C/D: rear towers and delivery courts.','A-B: 4 m wide open deck at +8 m.','A-C and B-D: side links at +8 m.','C-D: second rail crossing at +12 m.','A-D: fallen crane at +16 m (optional).','Each tower: two independent stair cores.')
$yy=198;foreach($s in $notes){Text $s 892 $yy;$yy+=36}
Text '02 / CROSS-TRACK SECTION' 890 490 $head
Text 'Looking along the railway; schematic vertical scale' 892 528 $small
# Pixel-space elevation, separate from the site projection.
function Rect($x,$y,$w,$h,$c){$b=Brush $c;$g.FillRectangle($b,[single]$x,[single]$y,[single]$w,[single]$h);$b.Dispose()}
$base=910
Rect 922 606 145 304 '#d9bd8b';Rect 1335 574 145 336 '#d9bd8b'
for($y=622;$y -le 910;$y+=48){Rect 922 $y 145 3 '#8b7854';Rect 1335 $y 145 3 '#8b7854'}
Rect 1067 814 268 6 '#88b5ae';Rect 1067 766 268 6 '#648d9b';Rect 1067 718 268 8 '#b77e32'
Rect 1145 865 100 45 '#b1bbb0';Rect 1166 880 58 25 '#d1a04b';Rect 1180 849 8 31 '#5b655d';Rect 910 910 590 3 '#667464'
Text '+8 m deck' 1100 822 $small;Text '+12 m deck' 1100 775 $small;Text '+16 m crane' 1100 687 $small
Text 'RAIL Y=0' 1150 929 $small
Text 'Lowest deck underside proposed at +7.5 m.' 892 976
Text 'Verify actual train, stack, riders and carried items.' 892 1011 $small
Text 'No bridge supports or crane debris in rail corridor.' 892 1040 $small
Text '03 / VERTICAL CHARACTER' 890 1102 $head
Text 'Mostly open concrete slabs; exposed pillars, no office walls.' 892 1146 $small
Text 'Proposed landings: 0 / 4 / 8 / 12 / 16 m.' 892 1178 $small
Text 'Upper skeletons could rise to 24-32 m for the skyline.' 892 1210 $small
Text 'E = paired elevator shafts; S = independent stair cores.' 892 1242 $small
Text 'Pallets, timber, rebar, pipes and cement bags on each floor.' 892 1294 $small
Text 'Irregular authored scatter; keep bridge / stair approaches clear.' 892 1326 $small
Text 'Shaft openings and recovery routes need detailed review.' 892 1358 $small
Text 'DRAWING ONLY / REVIEW BEFORE ROUGH QUACKVILLE BUILD' 890 1467 $small
Text '04 NOV 2026 / no Unity geometry or clearance validation' 890 1499 $small
$bmp.Save((Join-Path $folder 'skyscraper-plan-v2.png'),[Drawing.Imaging.ImageFormat]::Png)
$g.Dispose();$bmp.Dispose();Write-Output $folder




