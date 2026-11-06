$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Drawing
$folder=Join-Path $PSScriptRoot 'depot-floorplans-v3'
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
# PARTS STORE: a full second floor, two stairs, a central circulation spine.
StartSheet 'PARTS STORE / FLOOR PLAN STUDY V3' '26.4 x 21.6 m. Rear wing extends west, away from tracks. Frontage stays X=-16. Proposed roof: 7.2 m.' 26.4 21.6
Frame 1 'ground level'
Door 26.4 7.8 26.4 13.8;Label 'TO TRACKS >' 27 11 $small
Door 3 0 6 0;Label 'SERVICE EXIT' 2 -1 $small
VWall 9 0 21.6 8 10.4;Door 9 16 9 18.4
HWall 13.6 0 9 4 6.4
Label 'BULK STOCK' .7 12.5;Prop 'Pallets' 1 2 3 3;Prop 'Racks' 1 7 3 3
Label 'RETURNS' .7 20.8;Prop 'Crates' 1 15 3 2
Label 'PICKING HALL' 13 20.8
Prop 'Rack' 13 14 1.4 4;Prop 'Rack' 17 14 1.4 4
Prop 'Rack' 15 3 1.4 4;Prop 'Rack' 18.5 3 1.4 4
Prop 'Counter' 22 14 2 4
Label '3 m circulation spine' 11 12.4 $small
Stair A 10 2.4 6 1;Stair B 22 2.4 6 1
Frame 2 '+3.2 m / full upper floor'
VWall 9 0 21.6 9 11.4
HWall 14.4 0 9 4 6.4
HWall 7 0 9 4 6.4
Label 'ARCHIVE' .7 20.8;Prop 'Files' 1 16 2 3;Prop 'Files' 6 16 2 3
Label 'OFFICE' .7 13.5;Prop 'Desk' 1 9 3 1.5
Label 'STAFF ROOM' .7 6.4;Prop 'Table' 4 2 3 1.5
HWall 14.4 9 26.4 16 18.4
Label 'OVERFLOW STOCK' 10 20.8
Prop 'Crates' 11 17 3 2;Prop 'Racks' 21 16 2 4
Label 'SORTING / RETURN LOOP' 11 12.4
Prop 'Sort table' 15 9 4 1.5
Stair A 10 2.4 6 2;Stair B 22 2.4 6 2
EndSheet 'parts-store-floors-v3.png' 'Traversal: two stairs connect the picking hall to sorting, archive and staff rooms. Front counter and staggered racks interrupt long sightlines.'
# WORKSHOP: compact ground rooms and an upper ring gallery.
StartSheet 'WORKSHOP / FLOOR PLAN STUDY V3' '19.2 x 21.6 m. Smaller than the parts store; repair tables below, gallery above. Proposed roof: 6.8 m.' 19.2 21.6
Frame 1 'ground level'
Door 19.2 7.8 19.2 13.8;Label 'TO TRACKS >' 20 11 $small
Door 0 10 0 12.4;Label 'REAR EXIT' .3 12.8 $small
HWall 15 0 8 4 6.4;L 8 15 8 21.6
Label 'OFFICE' .7 20.8;Prop 'Desk' 1 17 3 1.5
VWall 6 0 8 3 5.4;L 6 8 12 8;L 12 0 12 8
Label 'TOOL CAGE' 6.4 7.4 $small;Prop 'Tools' 7 1 1.2 3
Label 'REPAIR FLOOR' 5 14.4
Prop 'Bench' 7 10.8 4 1.5;Prop 'Machine' 13 3 3 2
Prop 'Bench' 10 18 3 1.5
Stair A 1 2.4 6 1;Stair B 15.5 12 6 1
Frame 2 '+3.2 m / gallery'
RectPlan 6 8 7 5.6 $voidFill $cyan;Label 'OPEN TO' 6.5 12.5;Label 'BELOW' 6.5 11
HWall 16 0 9 5 7.4;L 9 16 9 21.6
Label 'SUPERVISOR' .5 20.8 $small;Prop 'Desk' 1 18 3 1.5
Label 'STAFF / LANDING' 10 20.8 $small
Prop 'Lockers' 10 18 2 1
Prop 'Parts bins' 6 2 6 1.3
Label 'INSPECTION GALLERY' 5 6.7 $small
Stair A 1 2.4 6 2;Stair B 15.5 12 6 2
EndSheet 'workshop-floors-v3.png' 'Traversal: stairs at opposite corners connect a continuous gallery around the repair floor. Void edges require railings; no mandatory jumps.'
# SHED: double-height repair bay and wide perimeter mezzanine.
StartSheet 'REPAIR SHED / FLOOR PLAN STUDY V3' '24 x 36 m. Proposed roof height 8.4 m; mezzanine +4.0 m. Tall central bay remains open through both levels.' 24 36
Frame 1 'ground level'
Door 0 17 0 25;Label '< TRACKS' -4 21 $small
Door 8 0 12 0;Label 'SIDE EXIT' 8 -1 $small
Door 10 36 14 36;Label 'TO YARD' 15 37 $small
HWall 28 0 9 4 6.4;L 9 28 9 36
Label 'DISPATCH' .7 35;Prop 'Desk' 1 30 3 1.5
VWall 17 12 24 14 16.4;Door 17 20 17 22.4
L 17 12 24 12;L 17 18 24 18;L 17 24 24 24
Label 'TOOLS' 17.5 17.5;Prop 'Racks' 21 13 1.2 3
Label 'COMPRESSOR' 17.4 23.5 $small;Prop 'Plant' 20 19 2.5 2
Label 'REPAIR BAY' 3 27
Prop 'Repair stand' 5 10 4 5;Prop 'Repair stand' 11 18 4 5
Prop 'Crates' 2 2 3 2
Stair A 19 2.4 7.2 1;Stair B 19 26.4 7.2 1
Frame 2 '+4.0 m / mezzanine'
RectPlan 3 11 14 14 $voidFill $cyan;Label 'DOUBLE-HEIGHT' 4 19;Label 'REPAIR BAY BELOW' 4 17.5
HWall 28 0 17 11 13.4;Door 4 28 6.4 28;L 9 28 9 36
Label 'SHIFT OFFICE' .6 35 $small;Prop 'Desk' 1 31 3 1.5
Label 'BREAK ROOM' 9.5 35 $small;Prop 'Table' 11 31 3 1.5
HWall 8 0 17 6 8.4;Door 12 8 14.4 8;L 10 0 10 8
Label 'SPARE PARTS' .5 7.4 $small;Prop 'Racks' 1 2 2 3
Label 'CONTROL' 10.5 7.4 $small;Prop 'Console' 11 2 4 1.4
Stair A 19 2.4 7.2 2;Stair B 19 26.4 7.2 2
Label '3 m gallery' .1 26 $small
EndSheet 'repair-shed-floors-v3.png' 'Traversal: two stairs and a continuous perimeter gallery provide alternative returns. Main bay, side exit and yard exit connect the ground-floor loop.'





