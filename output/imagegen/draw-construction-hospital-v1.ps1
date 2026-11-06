$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Drawing
$folder=Join-Path $PSScriptRoot 'construction-hospital-v1'
New-Item -ItemType Directory -Force $folder | Out-Null
$font=[Drawing.Font]::new('Arial',12)
$small=[Drawing.Font]::new('Arial',10)
$title=[Drawing.Font]::new('Arial',23,[Drawing.FontStyle]::Bold)
function Brush($c){[Drawing.SolidBrush]::new([Drawing.ColorTranslator]::FromHtml($c))}
function Text($s,$x,$y,$f=$font,$c='#273b3f'){$b=Brush $c;$g.DrawString($s,$f,$b,[single]$x,[single]$y);$b.Dispose()}
function P([double]$x,[double]$z){[Drawing.PointF]::new([single](54+($x+48)*6),[single](150+(160-$z)*6))}
function Box([double]$x,[double]$z,[double]$w,[double]$d,$c){$p=P $x ($z+$d);$b=Brush $c;$g.FillRectangle($b,$p.X,$p.Y,[single]($w*6),[single]($d*6));$b.Dispose()}
function Line([double]$x,[double]$z,[double]$xx,[double]$zz,$c='#485c60',$w=2){$pen=[Drawing.Pen]::new([Drawing.ColorTranslator]::FromHtml($c),$w);$g.DrawLine($pen,(P $x $z),(P $xx $zz));$pen.Dispose()}
function Label($s,$x,$z,$f=$small){$p=P $x $z;Text $s $p.X $p.Y $f}
function Outline([double]$x,[double]$z,[double]$w,[double]$d,$c){Box $x $z $w $d '#46555a';Box ($x+.5) ($z+.5) ($w-1) ($d-1) $c}
function Route($pts){for($j=1;$j -lt $pts.Count;$j++){Line $pts[$j-1][0] $pts[$j-1][1] $pts[$j][0] $pts[$j][1] '#34a4a4' 3}}
function Stair([double]$x,[double]$z){Outline $x $z 5 8 '#f5e8c4';for($j=1;$j -lt 8;$j++){Line $x ($z+$j) ($x+5) ($z+$j) '#807458' 1};Label 'S' ($x+1) ($z+6)}
function Tower([double]$x,[double]$z,[double]$w,[double]$d){Outline $x $z $w $d '#d9b779';for($a=$x+3;$a -lt $x+$w-1;$a+=6){for($b=$z+3;$b -lt $z+$d-1;$b+=7){Box $a $b 1.2 1.2 '#736447'}};Stair ($x+2) ($z+2);Stair ($x+$w-7) ($z+$d-10);Label 'TOWER' ($x+7) ($z+$d/2+3);Label 'column grid' ($x+4) ($z+$d/2-1);Line ($x+$w) ($z+12) ($x+$w) ($z+16) '#fff4d7' 5;Line $x ($z+$d-16) $x ($z+$d-12) '#fff4d7' 5}
function Wing([double]$x,[double]$z,[double]$w,[double]$d,$name){Outline $x $z $w $d '#a7cbc8';Label $name ($x+1.3) ($z+$d-2);for($b=$z+4;$b -lt $z+$d-7;$b+=7){Line ($x+$w*.45) $b ($x+$w) $b '#537e7e' 1;Box ($x+$w-3.5) ($b+1) 2 3.5 '#e8f1eb'};Line ($x+$w*.4) ($z+1) ($x+$w*.4) ($z+$d-7) '#537e7e' 1}
function Parking([double]$x,[double]$z,[double]$w,[double]$d){Box $x $z $w $d '#c0c5c0';for($b=$z+2;$b -lt $z+$d-2;$b+=5){Line $x $b ($x+6) $b '#f3f0e5' 1;Line ($x+$w-6) $b ($x+$w) $b '#f3f0e5' 1};Label 'PARKING' ($x+3) ($z+$d/2)}
function Yard([double]$x,[double]$z,[double]$w,[double]$d,$name){Outline $x $z $w $d '#d7c9ab';Label $name ($x+2) ($z+$d-2);for($b=0;$b -lt 3;$b++){Box ($x+3+$b*7) ($z+3) 4 7 '#9a8c71';for($j=1;$j -lt 4;$j++){Line ($x+3+$b*7+$j) ($z+3) ($x+3+$b*7+$j) ($z+10) '#cbbb96' 1}}}
function Crane([double]$x,[double]$z){Box ($x-2) ($z-2) 4 4 '#b98832';Line ($x-9) $z ($x+10) $z '#bb7c21' 6;Line $x ($z-5) $x ($z+5) '#bb7c21' 3;Label 'CRANE' ($x-5) ($z+8)}
$names=@('A  /  DIRECT ACCESS','B  /  COURTYARD LOOPS','C  /  STAGGERED CAMPUS')
$desc=@('Clear entrances; shortest return to the train.','Enclosed courts and longer exploration loops.','Offset landmarks and opposing service yards.')
$files=@()
for($i=0;$i -lt 3;$i++){
 $bmp=[Drawing.Bitmap]::new(684,1280);$g=[Drawing.Graphics]::FromImage($bmp);$g.SmoothingMode='AntiAlias';$g.Clear([Drawing.ColorTranslator]::FromHtml('#f5f1e7'))
 Text $names[$i] 48 25 $title;Text $desc[$i] 50 70;Text 'CONSTRUCTION' 70 118;Text 'HOSPITAL' 445 118;Text '+Z / DEPARTURE' 270 118 $small
 Box -48 0 96 160 '#c4cdb9';Box -42 6 84 148 '#dbe0d0';Box -6 0 12 160 '#b0b7ae'
 for($z=2;$z -lt 160;$z+=3){Line -2 $z 2 $z '#7b807a' 2};Line -1 0 -1 160 '#485451' 2;Line 1 0 1 160 '#485451' 2
 foreach($z in @(53.33,106.67)){Line -48 $z 48 $z '#9ba995' 1}
 for($z=10;$z -lt 154;$z+=13){foreach($x in @(-45,44)){ $p=P $x $z;$b=Brush '#8f9f7b';$g.FillEllipse($b,$p.X-6,$p.Y-6,12,12);$b.Dispose()}}
 foreach($z in @(58,103)){Line -10 $z 10 $z '#f7f0d6' 9;Line -1 ($z-2) -1 ($z+2) '#485451' 2;Line 1 ($z-2) 1 ($z+2) '#485451' 2}
 Box -2 74 4 14 '#d39b43';Label 'STOP' -4 72
 if($i -eq 0){
  Tower -38 63 26 36;Crane -26 110;Yard -39 124 28 24 'DELIVERY'
  Yard -39 15 28 22 'MATERIALS';Outline -38 41 24 10 '#d9b779';Label 'SITE OFFICES' -37 49
  Wing 12 61 11 38 'WARD';Wing 30 61 11 38 'WARD';Outline 23 74 7 13 '#c2ded8';Label 'LOBBY' 21 83
  Route @(@(6,80),@(26,80),@(26,104),@(43,104),@(43,57),@(10,57),@(10,80),@(6,80))
  Route @(@(-6,80),@(-10,80),@(-10,103),@(-40,103),@(-40,58),@(-10,58),@(-10,80))
  Outline 13 109 27 12 '#a7cbc8';Label 'EMERGENCY' 15 118;Parking 13 14 27 33;Label 'AMBULANCE COURT' 10 137
 } elseif($i -eq 1){
  Tower -38 83 25 22;Outline -38 60 10 23 '#d9b779';Label 'PODIUM' -40 77;Label 'WORK' -25 75;Label 'COURT' -25 71;Crane -27 116
  Yard -39 132 28 16 'MATERIALS';Outline -38 18 26 15 '#d9b779';Label 'OFFICES' -35 28;Yard -39 39 26 14 'STAGING'
  Wing 12 89 29 13 'NORTH WARD';Wing 12 60 29 13 'SOUTH WARD';Outline 32 73 9 16 '#a7cbc8';Label 'COURT' 16 82
  Route @(@(6,80),@(28,80),@(28,96),@(37,96),@(37,66),@(17,66),@(10,56),@(7,56))
  Route @(@(-6,79),@(-24,79),@(-24,89),@(-40,89),@(-40,57),@(-10,57),@(-10,79))
  Outline 13 112 27 12 '#a7cbc8';Label 'EMERGENCY' 15 121;Parking 13 14 27 30;Outline 20 47 20 8 '#a7cbc8';Label 'CLINIC' 23 54;Label 'SERVICE YARD' 14 145
 } else {
  Tower -38 78 26 29;Crane -26 119;Yard -39 131 27 18 'DELIVERY';Yard -39 43 27 24 'STAGING';Outline -38 16 26 13 '#d9b779';Label 'OFFICES' -35 26
  Wing 12 57 12 31 'WARD';Wing 29 57 12 31 'WARD';Outline 24 57 5 31 '#c2ded8';Label 'LOBBY' 20 56
  Outline 13 95 27 13 '#a7cbc8';Label 'EMERGENCY' 15 105;Label 'AMBULANCE' 14 122;Parking 13 14 27 28;Outline 20 136 20 12 '#a7cbc8';Label 'UTILITY' 23 145
  Route @(@(6,75),@(10,75),@(10,92),@(43,92),@(43,51),@(10,51),@(10,75))
  Route @(@(-6,90),@(-10,90),@(-10,112),@(-41,112),@(-41,72),@(-10,72),@(-10,90))
 }
 # Ambulances and door markers are schematic; upper floors require separate plans.
 $az=if($i -eq 2){115}else{128};foreach($x in @(19,31)){Outline $x $az 4 7 '#f3eee0';Line ($x+1) ($az+3) ($x+3) ($az+3) '#ba6d54' 2;Line ($x+2) ($az+2) ($x+2) ($az+4) '#ba6d54' 2}
 Text '0 m' 15 1098 $small;Text '160 m' 5 145 $small;Text 'APPROACH / +Z TRAVEL UP' 215 1123 $small
 Text 'Amber: construction   Teal: hospital   Cyan: suggested routes' 48 1160 $small
 Text '96 x 160 m envelope | flat edge strip | clear central railway' 48 1187 $small
 Text 'Ground-floor site study. Height / upper floors undecided.' 48 1214 $small
 Text 'PROPOSAL  /  04 NOV 2026  /  DRAWING REVIEW BEFORE BLOCKOUT' 48 1245 $small
 $f=Join-Path $folder ('option-'+[char](97+$i)+'.png');$bmp.Save($f,[Drawing.Imaging.ImageFormat]::Png);$files+=$f;$g.Dispose();$bmp.Dispose()
}
$sheet=[Drawing.Bitmap]::new(2052,1280);$sg=[Drawing.Graphics]::FromImage($sheet)
for($i=0;$i -lt 3;$i++){$im=[Drawing.Image]::FromFile($files[$i]);$sg.DrawImage($im,$i*684,0,684,1280);$im.Dispose()}
$sheet.Save((Join-Path $folder 'overview.png'),[Drawing.Imaging.ImageFormat]::Png);$sg.Dispose();$sheet.Dispose()
Write-Output $folder


