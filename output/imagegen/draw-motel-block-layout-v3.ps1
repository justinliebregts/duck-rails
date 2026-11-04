$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Drawing
$folder=Join-Path $PSScriptRoot 'motel-block-layout-v3'
New-Item -ItemType Directory -Path $folder -Force | Out-Null
$bg=[System.Drawing.ColorTranslator]::FromHtml('#102c40')
$white=[System.Drawing.Brushes]::WhiteSmoke
$muted=New-Object System.Drawing.SolidBrush ([System.Drawing.ColorTranslator]::FromHtml('#adc8d6'))
$wall=New-Object System.Drawing.Pen ([System.Drawing.ColorTranslator]::FromHtml('#e5f4fa')),3
$thin=New-Object System.Drawing.Pen ([System.Drawing.ColorTranslator]::FromHtml('#50768c')),1
$cyan=New-Object System.Drawing.Pen ([System.Drawing.ColorTranslator]::FromHtml('#61dccb')),2
$propFill=New-Object System.Drawing.SolidBrush ([System.Drawing.ColorTranslator]::FromHtml('#294b60'))
$voidFill=New-Object System.Drawing.Drawing2D.HatchBrush ([System.Drawing.Drawing2D.HatchStyle]::ForwardDiagonal),([System.Drawing.ColorTranslator]::FromHtml('#31536a')),$bg
$font=New-Object System.Drawing.Font 'Arial',12
$small=New-Object System.Drawing.Font 'Arial',10
$head=New-Object System.Drawing.Font 'Arial',20
$title=New-Object System.Drawing.Font 'Arial',26
function Text($s,$x,$y,$f=$font,$brush=$white){$g.DrawString($s,$f,$brush,[single]$x,[single]$y)}
function P([double]$x,[double]$y){[System.Drawing.PointF]::new([single]($script:ox+$x*$script:scale),[single]($script:oy+($script:depth-$y)*$script:scale))}
function L($x,$y,$u,$v,$pen=$wall){$g.DrawLine($pen,(P $x $y),(P $u $v))}
function RectPlan($x,$y,$w,$h,$fill=$null,$pen=$wall){$p=P $x ($y+$h);if($fill){$g.FillRectangle($fill,$p.X,$p.Y,[single]($w*$script:scale),[single]($h*$script:scale))};$g.DrawRectangle($pen,$p.X,$p.Y,[single]($w*$script:scale),[single]($h*$script:scale))}
function Label($s,$x,$y,$f=$font){$p=P $x $y;Text $s $p.X $p.Y $f}
function Prop($s,$x,$y,$w,$h){RectPlan $x $y $w $h $propFill $thin;Label $s ($x+.15) ($y+$h-.2) $small}
function VWall($x,$y1,$y2,$door1,$door2){L $x $y1 $x $door1;L $x $door2 $x $y2;L ($x-.3) $door1 ($x+.3) $door1 $cyan;L ($x-.3) $door2 ($x+.3) $door2 $cyan}
function HWall($y,$x1,$x2,$door1,$door2){L $x1 $y $door1 $y;L $door2 $y $x2 $y;L $door1 ($y-.3) $door1 ($y+.3) $cyan;L $door2 ($y-.3) $door2 ($y+.3) $cyan}
function Stair($id,$x,$y,$run,$floor){
 RectPlan $x $y 2.4 $run $null $cyan
 for($t=.6;$t -lt $run;$t+=.6){L $x ($y+$t) ($x+2.4) ($y+$t) $thin}
 $from=if($floor -eq 1){$y+.7}else{$y+$run-.7};$to=if($floor -eq 1){$y+$run-.7}else{$y+.7}
 L ($x+1.2) $from ($x+1.2) $to $cyan
 $sgn=[Math]::Sign($to-$from)
 L ($x+1.2) $to ($x+.8) ($to-.5*$sgn) $cyan;L ($x+1.2) $to ($x+1.6) ($to-.5*$sgn) $cyan
 Label $id ($x+.15) ($y+$run+.9) $small
}
function StartSheet($name,$subtitle,$w,$d){
 $sheetHeight=if($d -lt 30){1000}else{1300}; $script:bmp=New-Object System.Drawing.Bitmap 1900,$sheetHeight
 $script:g=[System.Drawing.Graphics]::FromImage($bmp);$g.SmoothingMode='AntiAlias';$g.TextRenderingHint='AntiAliasGridFit';$g.Clear($bg)
 $script:scale=22;$script:depth=$d;$script:width=$w
 Text $name 70 30 $title;Text $subtitle 70 80 $font $muted
}
function Frame($floor,$height){
 $script:ox=if($floor -eq 1){140}else{1060};$script:oy=205
 Text "FLOOR $floor  /  $height" ($script:ox-10) 125 $head
 for($x=0;$x -le $script:width;$x+=2){L $x 0 $x $script:depth $thin}
 for($y=0;$y -le $script:depth;$y+=2){L 0 $y $script:width $y $thin}
 RectPlan 0 0 $script:width $script:depth
 Label ("{0} m" -f $script:width) ($script:width/2-2) ($script:depth+1.6)
 Label ("{0} m" -f $script:depth) -3 ($script:depth/2)
 Label 'N / +Z' ($script:width+1) $script:depth $small
 L ($script:width+1.8) ($script:depth-3) ($script:width+1.8) ($script:depth-1) $cyan
 L ($script:width+1.8) ($script:depth-1) ($script:width+1.3) ($script:depth-1.7) $cyan
}
function Door($x,$y,$u,$v){$erase=New-Object System.Drawing.Pen $bg,6;L $x $y $u $v $erase;L $x $y $u $v $cyan;$erase.Dispose()}
function EndSheet($file,$notes){ $base=if($script:depth -lt 30){780}else{1080}
 Text 'PLAN KEY: solid = walls | cyan threshold = open doorway | filled boxes = props | hatch = open to floor below' 70 $base
 Text 'Stairs A/B occupy identical positions on both floors. Arrows: UP on floor 1, DOWN on floor 2. Stair width 2.4 m.' 70 ($base+30)
 Text 'Target: 2.4 m main routes, doors and stair landings; 1.8 m secondary aisles. Wall thickness and hand clearance need greybox review.' 70 ($base+60) $font $muted
 Text $notes 70 ($base+100) $font $muted
 Text 'DRAWING PROPOSAL ONLY / no Unity changes / dimensions are nominal metres / 03 NOV 2026' 70 ($base+160) $small $muted
 $bmp.Save((Join-Path $folder $file),[System.Drawing.Imaging.ImageFormat]::Png);$g.Dispose();$bmp.Dispose()
}

# Motel drawings use the same metre-scaled drafting helpers as the depot.
function Sheet($name,$sub,$w,$d,$s,$x=100,$y=180){
 $script:bmp=New-Object System.Drawing.Bitmap 1600,1400
 $script:g=[System.Drawing.Graphics]::FromImage($bmp);$g.SmoothingMode='AntiAlias';$g.TextRenderingHint='AntiAliasGridFit';$g.Clear($bg)
 $script:scale=$s;$script:depth=$d;$script:width=$w;$script:ox=$x;$script:oy=$y
 Text $name 65 32 $title;Text $sub 65 85 $font $muted
 for($a=0;$a -le $w;$a+=2){L $a 0 $a $d $thin}
 for($a=0;$a -le $d;$a+=2){L 0 $a $w $a $thin}
 Text 'N / +Z' $script:ox ($script:oy-50)
 Text "$w m" ($script:ox+$w*$s/2-20) ($script:oy-30)
 Label "$d m" -3 ($d/2)
}
function SaveSheet($file){
 Text 'KEY   white: walls   /   mint: open doors + circulation   /   blue fill: props / walk areas' 65 1290 $font $muted
 Text 'DRAWING PROPOSAL V1  |  04 NOV 2026  |  nominal metres; wall thickness/VR clearance to be checked in greybox' 65 1330 $font $muted
 $bmp.Save((Join-Path $folder $file),[System.Drawing.Imaging.ImageFormat]::Png);$g.Dispose();$bmp.Dispose()
}
function Note($titleText,$lines,$y){Text $titleText 830 $y $head; $yy=$y+40;foreach($line in $lines){Text $line 830 $yy $font $muted;$yy+=29}}
function EastRoom($id,$z){
 RectPlan 18 $z 12 8
 Door 18 ($z+1) 18 ($z+3.4)
 Label $id 18.6 ($z+7.5)
 Prop 'Bed' 22 ($z+1) 3 2.2
 Prop 'Desk' 28 ($z+.5) 1.3 2.5
 L 26 ($z+4) 30 ($z+4);VWall 26 ($z+4) ($z+8) ($z+4.6) ($z+7)
 Label 'BATH' 26.4 ($z+7.5) $small
 Prop 'WC' 28.3 ($z+5) 1 1
}
function NorthGuest($id,$x){
 RectPlan $x 43 10 12;Door ($x+1) 43 ($x+3.4) 43
 Label $id ($x+.6) 50
 Prop 'Bed' ($x+5) 46 3 2.2;Prop 'Desk' ($x+.5) 52 2 1.2
 L ($x+6) 51 ($x+6) 55;HWall 51 ($x+6) ($x+10) ($x+6.5) ($x+8.9)
 Label 'BATH' ($x+6.2) 54 $small
}

$script:bmp=New-Object System.Drawing.Bitmap 2000,2200
$script:g=[System.Drawing.Graphics]::FromImage($bmp);$g.SmoothingMode='AntiAlias';$g.TextRenderingHint='AntiAliasGridFit';$g.Clear($bg)
$script:ox=100;$script:oy=170;$script:scale=12;$script:depth=160
$asphalt=New-Object System.Drawing.SolidBrush ([System.Drawing.ColorTranslator]::FromHtml('#203b49'))
$walk=New-Object System.Drawing.SolidBrush ([System.Drawing.ColorTranslator]::FromHtml('#32605f'))
$roof=New-Object System.Drawing.SolidBrush ([System.Drawing.ColorTranslator]::FromHtml('#4b5c65'))
$treeBrush=New-Object System.Drawing.SolidBrush ([System.Drawing.ColorTranslator]::FromHtml('#193e41'))
$goldPen=New-Object System.Drawing.Pen ([System.Drawing.ColorTranslator]::FromHtml('#e3bc77')),3
Text 'ROADSIDE MOTEL / WHOLE BLOCK' 90 30 $title
Text 'TOP DOWN  /  96 x 160 metres  /  layout proposal V3  /  north and train travel upward' 90 85 $font $muted
for($x=0;$x -le 96;$x+=8){L $x 0 $x 160 $thin}
for($z=0;$z -le 160;$z+=8){L 0 $z 96 $z $thin}
# Quiet shoulders and decorative woodland. Central rail corridor remains clear.
foreach($z in 8,20,33,127,139,151){foreach($x in 7,19,32,66,79,90){$p=P $x $z;$g.FillEllipse($treeBrush,$p.X-22,$p.Y-22,44,44)}}
foreach($z in 52,67,83,99,112){foreach($x in 4,94){$p=P $x $z;$g.FillEllipse($treeBrush,$p.X-18,$p.Y-18,36,36)}}
# Entire block boundary and encounter limits.
RectPlan 0 0 96 160
L 0 45 96 45 $cyan;L 0 115 96 115 $cyan
# Track ballast / ties and visual rails, diagrammatic gauge.
RectPlan 45 0 6 160 $asphalt $thin
for($z=1;$z -lt 160;$z+=2){L 45.8 $z 50.2 $z $thin}
L 46.9 0 46.9 160 $wall;L 49.1 0 49.1 160 $wall
# Paved search areas, open routes and rear lane.
RectPlan 57 52 16 42 $asphalt $thin
RectPlan 12 71 26 7 $asphalt $thin
RectPlan 32 78 7 24 $asphalt $thin
RectPlan 88 52 5 61 $asphalt $thin
RectPlan 58 109 35 4 $asphalt $thin
L 93 52 93 113 $goldPen;L 58 113 93 113 $goldPen
for($z=53;$z -le 113;$z+=4){L 92.7 $z 93.3 $z $goldPen}
# Main motel, cutaway ground floor. Upper floor indicated by balcony note.
RectPlan 76 54 12 40 $roof
for($i=0;$i -lt 5;$i++){
 $z=54+$i*8;L 76 $z 88 $z $wall
 Door 76 ($z+1) 76 ($z+3.4)
 Label ([string](101+$i)) 80 ($z+5.5)
 # Alternate ajar leaves, opened inward clear of outside circulation.
 if($i%2 -eq 0){L 76 ($z+1) 78.3 ($z+1.4) $goldPen}
}
foreach($dividerZ in 62,70,78,86){L 76 $dividerZ 88 $dividerZ $wall}
RectPlan 58 97 30 12 $roof
L 68 97 68 109;L 78 97 78 109
Door 59 97 61.4 97;Door 69 97 71.4 97;Door 79 97 81.4 97
Label 'RECEPTION' 58.5 104 $small;Label 'LAUNDRY' 68.8 104 $small;Label 'LINEN' 80 104 $small
RectPlan 73 54 3 40 $walk $cyan;RectPlan 58 94 30 3 $walk $cyan
Stair 'A' 70.6 54 6.4 1;RectPlan 70.6 60.4 2.4 2.4 $walk $cyan
Stair 'B' 58 85.2 6.4 1;RectPlan 58 91.6 2.4 2.4 $walk $cyan
Label 'A' 68.8 58 $small;Label 'B' 61 89 $small
# Parking, two cars, clearance around stairs and walkway.
for($z=57;$z -lt 81;$z+=6){L 58 $z 63 $z $thin;L 58 ($z+5.5) 63 ($z+5.5) $thin}
Prop 'CAR' 58.7 58 2.7 4.8;Prop 'CAR' 58.7 70 2.7 4.8
Label 'PARKING' 63.5 83 $small;Label 'COURTYARD' 63 80.5 $small
# West office and garage: deliberate V2 rework of previous service floor plan.
RectPlan 12 78 20 24 $roof
L 24 78 24 102;L 12 90 32 90
Door 15 78 21 78;Door 32 82 32 84.4;Door 32 95 32 97.4
Door 24 82 24 84.4;Door 24 95 24 97.4;Door 16 90 18.4 90;Door 26 90 28.4 90
Label 'GARAGE' 14 85 $small;Label 'OFFICE' 25 85 $small
Label 'STORAGE' 14 97 $small;Label 'STAFF' 25 97 $small
L 15 78 21 78 $goldPen
# Transformer enclosure: world X=-42..-10, Z=47..71, south of office/garage.
RectPlan 6 47 32 24 $asphalt $thin
function Fence($x,$z,$u,$v){
 L $x $z $u $v $goldPen
 $length=[Math]::Sqrt(($u-$x)*($u-$x)+($v-$z)*($v-$z))
 for($d=0;$d -le $length;$d+=2){
  $px=$x+($u-$x)*$d/$length;$pz=$z+($v-$z)*$d/$length
  L ($px-.25) ($pz-.25) ($px+.25) ($pz+.25) $goldPen
  L ($px-.25) ($pz+.25) ($px+.25) ($pz-.25) $goldPen
 }
}
Fence 6 47 38 47
Fence 6 47 6 57;Fence 6 61 6 71
Fence 38 47 38 55;Fence 38 59 38 71
Fence 6 71 18 71;Fence 24 71 38 71
# Bent fence ends angle away from the clear 4 m breaches.
L 6 57 4.7 56 $goldPen;L 6 61 4.7 62 $goldPen
L 38 55 39.3 54 $goldPen;L 38 59 39.3 60 $goldPen
# Three machine rows, two units per row, with 6 m longitudinal and 4 m cross aisles.
foreach($machineX in 10,20,30){foreach($machineZ in 52,61){
 RectPlan $machineX $machineZ 4 5 $roof $wall
 for($fin=.7;$fin -lt 4;$fin+=.8){L ($machineX+$fin) ($machineZ+.5) ($machineX+$fin) ($machineZ+4.5) $thin}
 RectPlan ($machineX+1) ($machineZ+1.5) 2 2 $propFill $thin
}}
Label 'TRANSFORMER YARD' 9 50 $small
Label 'GATE' 18.5 73 $small
Label 'GAP' 1 60 $small;Label 'GAP' 39 58.5 $small
L 21 71 21 77 $cyan
L 38 57 44 57 $cyan
Prop 'VAN' 29 72 3 5;Prop 'CRATES' 34 73 3 3
Label 'SERVICE APRON' 7 77 $small
Prop '' 88.4 65 1.8 3;Prop '' 88.4 70 1.8 3
Label 'REAR LANE' 89 102 $small
# Return paths: cross only ahead/behind stop marker, not through the train.
L 39 88 44 88 $cyan;L 52 88 57 88 $cyan;L 44 88 52 88 $cyan
L 44 57 57 57 $cyan
# Stop marker is symbolic, avoids claiming train dimensions.
RectPlan 46.5 75 3 10 $walk $goldPen
Label 'TRAIN' 40 82 $small;Label 'STOP' 40 79.5 $small
RectPlan 58 48 2 2 $walk $goldPen;Label 'MOTEL SIGN' 61 50 $small
Label 'QUIET DEPARTURE / 45 m' 34 139
Label 'QUIET APPROACH / 45 m' 34 26
Label 'ENCOUNTER BEGINS  Z=45' 1 44 $small
Label 'ENCOUNTER ENDS  Z=115' 1 118 $small
Text '96 m' 650 132 $head
Text 'N / +Z' 1320 160 $head
Text '160 m total length' 1320 202 $font $muted
function Side($h,$lines,$y){Text $h 1320 $y $head;$yy=$y+45;foreach($t in $lines){Text $t 1320 $yy $font $muted;$yy+=32}}
Side 'EAST / MOTEL' @('Two-storey L-shaped building.','Five ground-floor guest rooms.','Reception, laundry and linen wing.','Eight guest rooms upstairs.','Mint strip: covered walk / balcony.','Stairs A and B connect both floors.','Gold door leaves: examples held ajar.','Door gaps stay clear for hand locomotion.') 290
Side 'COURTYARD + BACK SIDE' @('Small parking court, two abandoned cars.','Open approach faces the train.','Rear: 5 m service lane, dumpsters, fence.','Lane joins the north and south corners.','Trees lie beyond the boundary fence.','No extra rear buildings or parking field.') 665
Side 'WEST / OFFICE + GARAGE' @('Single-storey building, 20 x 24 m.','Garage bay opens south to loading yard.','Office entrance faces east toward the train.','Storage and staff room occupy north half.','Maintenance van and isolated crate stacks.','This revises the earlier service-room layout.','Office / garage identity remains a proposal.') 980
Side 'TRANSFORMER YARD' @('32 x 24 m enclosure south of the garage.','Three rows of machines, two units in each.','6 m aisles between rows; 4 m cross aisle.','Chain-link fence: gold line with mesh ticks.','6 m north gate opens onto the service apron.','Two 4 m fence breaks give alternate exits.','Bent panels stay outside the walkable gaps.','Equipment is scenery; no power puzzle implied.') 1340
Side 'PRESERVED BLOCK CONTRACT' @('Track centre X=0; stop at Z=80.','Block X=-48..48, Z=0..160.','Encounter remains Z=45..115.','Six coal chunks: three each side.','Three authored spawn variations.','Navigation and lighting need offline rebakes.','Drawing only; Unity remains unchanged.') 1700
Text '04 NOV 2026  /  PROPOSED SITE LAYOUT  /  dimensions nominal; collision, door clearance, navigation and VR scale require greybox checks' 90 2140 $font $muted
$bmp.Save((Join-Path $folder 'whole-block-top-down.png'),[System.Drawing.Imaging.ImageFormat]::Png)
$g.Dispose();$bmp.Dispose()



