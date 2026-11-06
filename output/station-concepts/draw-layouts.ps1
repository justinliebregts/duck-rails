$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Drawing
Remove-Item Alias:r -ErrorAction SilentlyContinue
$img=[Drawing.Bitmap]::new(2100,1550);$g=[Drawing.Graphics]::FromImage($img);$g.SmoothingMode='AntiAlias';$g.Clear([Drawing.ColorTranslator]::FromHtml('#102938'))
function Brush($c){[Drawing.SolidBrush]::new([Drawing.ColorTranslator]::FromHtml($c))}
$white=Brush '#edf3ef';$muted=Brush '#adc1c6';$floor=Brush '#244451';$room=Brush '#385861';$gold=Brush '#e6ba65';$rock=Brush '#172f3b';$train=Brush '#698f8e'
$wall=[Drawing.Pen]::new($white,3);$thin=[Drawing.Pen]::new((Brush '#476471'),1);$edge=[Drawing.Pen]::new($gold,3);$route=[Drawing.Pen]::new((Brush '#75d6c5'),4);$dash=[Drawing.Pen]::new((Brush '#75d6c5'),2);$dash.DashStyle='Dash'
$f=[Drawing.Font]::new('Arial',16);$small=[Drawing.Font]::new('Arial',13);$h=[Drawing.Font]::new('Arial',24,[Drawing.FontStyle]::Bold);$title=[Drawing.Font]::new('Arial',34,[Drawing.FontStyle]::Bold)
function T($s,$x,$y,$font=$f,$b=$white){$g.DrawString($s,$font,$b,[single]$x,[single]$y)}
function P([double]$x,[double]$z){[Drawing.PointF]::new([single]($script:ox+$x*13),[single](800-($z-60)*13))}
function R([double]$x,[double]$z,[double]$w,[double]$d,$b=$floor,$pen=$wall){$p=P $x ($z+$d);$g.FillRectangle($b,$p.X,$p.Y,[single]($w*13),[single]($d*13));$g.DrawRectangle($pen,$p.X,$p.Y,[single]($w*13),[single]($d*13))}
function L([double]$x,[double]$z,[double]$u,[double]$v,$pen=$wall){$g.DrawLine($pen,(P $x $z),(P $u $v))}
function Label($s,$x,$z,$font=$small,$b=$white){$p=P $x $z;T $s $p.X $p.Y $font $b}
function Rails([double]$x){R ($x-2.4) 61 4.8 38 $rock $thin;for($z=62;$z -lt 99;$z+=1.5){L ($x-2) $z ($x+2) $z $thin}; L ($x-1.5) 61 ($x-1.5) 99 $wall;L ($x+1.5) 61 ($x+1.5) 99 $wall}
function Train([double]$x){R ($x-1.3) 74 2.6 12 $train $thin;Label 'TRAIN' ($x-1.1) 83 $small;L $x 88 $x 97 $route;L $x 97 ($x-.7) 95.5 $route;L $x 97 ($x+.7) 95.5 $route}
function Room($label,[double]$x,[double]$z,[double]$w,[double]$d){R $x $z $w $d $room;Label $label ($x+.4) ($z+$d-.8);L ($x+$w) ($z+1) ($x+$w) ($z+3.4) ([Drawing.Pen]::new($floor,5))}
function Lamp([double]$x,[double]$z){$p=P $x $z;$g.FillEllipse($gold,$p.X-5,$p.Y-5,10,10)}
T 'UNDERGROUND DEPARTURE STATION' 55 35 $title
T 'Three top-down layout studies  /  All dimensions proposed  /  Departure direction is up' 58 94 $f $muted
T 'Scale reference: Compound occupied floor 18 x 24 m = 432 m2. Station areas below exclude track beds; rooms are included.' 58 126 $f $muted
$script:ox=405
T 'A   SIDE HALL' 55 193 $h;T 'Recommended: compact, social, easy boarding' 55 233 $small $muted
R -19 65 16 30;Rails 0;Train 0
Room "SUPPLIES`n6 x 8 m" -19 87 6 8
Room "WAITING`n6 x 8 m" -19 77 6 8
Room "STAFF /`nSTORAGE" -19 65 6 8
Label "OPEN`nPLATFORM /`nGATHERING" -12 90
R -11 79 1 4 $room $thin;R -11 72 1 4 $room $thin
L -3.3 65 -3.3 95 $edge
L -16 61 -16 64 $route;Label 'ARRIVAL' -20 63
L -15 75 -6 75 $route;L -6 75 -6 81 $route;L -6 81 -3.2 81 $route
foreach($z in @(68,80,92)){Lamp -7 $z}
L -17 65 -14 65 ([Drawing.Pen]::new($floor,5))
Label '16 m' -13 98 $f;Label '30 m' -24 81 $f
T '480 m2 gross floor  /  +11% over Compound' 55 827 $f
T "One level. Rooms sit against the rock wall.`nTrain stays visible from the main gathering area.`nSealed rear access; no walking across the rails." 55 866 $small $muted
$script:ox=1150
T 'B   ISLAND REFUGE' 755 193 $h;T 'Most atmospheric: an old platform reused' 755 233 $small $muted
Rails -24;Rails 0;R -21 66 18 28;Train 0
Room "SUPPLIES" -19 86 7 6;Room "STORE /`nSTAFF" -19 77 7 7
R -19 68 3 6 $room $thin
for($z=68.5;$z -lt 74;$z+=.7){L -19 $z -16 $z $thin}
Label "ARRIVAL`nSTAIRS" -15.5 72
Label "GATHERING /`nWAITING" -11.5 87
L -3.3 66 -3.3 94 $edge;L -21 66 -21 94 $dash
Label "ABANDONED TRACK" -28 98 $small $muted
R -25.2 80 2.4 8 $room $thin
L -15 75 -7 75 $route;L -7 75 -7 81 $route;L -7 81 -3.2 81 $route
foreach($z in @(69,80,91)){Lamp -6 $z}
Label '18 m' -14 98 $f
T '504 m2 gross floor  /  +17% over Compound' 755 827 $f
T "One main level; stairs arrive from a sealed passage.`nDead track is scenery behind a continuous guard.`nA stronger subway silhouette, with more scenery." 755 866 $small $muted
$script:ox=1765
T 'C   SPLIT PLATFORMS' 1450 193 $h;T 'Most architectural: a small overhead crossing' 1450 233 $small $muted
R -13 68 10 24;R 3 68 10 24;Rails 0;Train 0
Room "SUPPLIES" -13 85 7 7;Room "STAFF" 6 85 7 7
Label 'WAITING' -12 82;Label 'BOARDING' 4 82
R -12 70 3 10 $room $thin;R 9 70 3 10 $room $thin
for($z=70.5;$z -lt 80;$z+=.65){L -12 $z -9 $z $thin;L 9 $z 12 $z $thin}
R -9 69 18 3 $floor $dash
Label 'BRIDGE ABOVE' -6 71.7 $small
Label 'UP' -12 78;Label 'UP' 9 78
L -3.3 68 -3.3 92 $edge;L 3.3 68 3.3 92 $edge
foreach($z in @(73,88)){Lamp -5 $z;Lamp 5 $z}
L -10 63 -10 67 $route;Label 'ARRIVAL' -13 65
L -12 68 -9 68 ([Drawing.Pen]::new($floor,5))
L 13 86 13 88 $wall
L 6 86 6 88.4 ([Drawing.Pen]::new($floor,5))
Label '10 m' -12 96 $f;Label '10 m' 5 96 $f
T '480 m2 floor + 54 m2 elevated bridge' 1450 827 $f
T "Smallest separate spaces; more vertical character.`nStairs / bridge need train-clearance and VR review.`nMost construction work; less direct crew regrouping." 1450 866 $small $muted
# common longitudinal context, full starting block
T 'SHARED ROUTE IDEA  /  STATION INSIDE THE MOUNTAIN' 55 987 $h
T 'Existing starting block: 96 m wide x 160 m long. Track stays straight; train start remains at local Z = 80 m.' 55 1027 $f $muted
$g.FillRectangle($rock,75,1080,1575,170);$g.FillRectangle($floor,750,1090,450,150)
$g.DrawRectangle($wall,750,1090,450,150)
$g.DrawLine($wall,75,1150,1875,1150);$g.DrawLine($wall,75,1180,1875,1180)
$g.DrawLine($edge,1650,1070,1650,1260)
T "SEALED REAR TUNNEL" 105 1097 $f;T "STATION" 815 1100 $f
T "ENCLOSED DEPARTURE TUNNEL" 1230 1097 $f
T "OPEN AIR" 1690 1097 $f;T "NEXT BLOCK" 1840 1220 $small
$g.FillRectangle($train,900,1140,135,50)
T 'Train start' 885 1200 $small
T 'Mountain face / portal' 1500 1280 $f $gold
T 'Z 0' 70 1255 $small;T 'Z 60' 735 1255 $small;T 'Z 80' 955 1255 $small;T 'Z 100' 1170 1255 $small;T 'Z 140' 1610 1255 $small;T 'Z 160' 1830 1255 $small
T 'Station diagram envelope Z 60-100; each option occupies only part of that zone. Portal at Z 140 is a proposal.' 58 1345 $small $muted
T 'Reveal: enclosed station > 40-48 m dark tunnel > rocky portal > first outdoor stretch. The mountain falls away; the train need not climb.' 58 1377 $f
T 'Key: yellow = platform edge / lamp     mint = walking route or overhead / guarded boundary     filled boxes = rooms, benches, stairs' 58 1425 $small $muted
T 'Concept only. Room functions, train outline and clearance allowances are provisional; no Unity geometry changed.' 58 1460 $small $muted
$img.Save((Join-Path $PSScriptRoot 'station-layouts-v1.png'),[Drawing.Imaging.ImageFormat]::Png);$g.Dispose();$img.Dispose()




