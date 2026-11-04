$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Drawing
$folder=Join-Path $PSScriptRoot 'motel-floorplans-v1'
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
foreach($floor in 1,2){
 $level=if($floor -eq 1){'GROUND / +0.0 m'}else{'UPPER / +3.6 m'}
 Sheet "ROADSIDE MOTEL / $level" 'Main L-wing: 30 x 55 m overall envelope. Local origin = world X +10, Z +54. Tracks lie to the west (left).' 30 55 18
 # Balcony / covered walk: an uninterrupted L outside all room doors.
 RectPlan 15 0 3 40 $propFill $cyan;RectPlan 0 40 30 3 $propFill $cyan
 for($i=0;$i -lt 5;$i++){EastRoom ([string]($floor*100+1+$i)) ($i*8)}
 if($floor -eq 1){
  RectPlan 0 43 10 12;Door 1 43 3.4 43;Door 0 47 0 49.4
  Label 'RECEPTION' .5 54;Prop 'Counter' 5 46 1.2 5;Prop 'Sofa' 1 51 3 1.2
  RectPlan 10 43 10 12;Door 11 43 13.4 43
  Label 'LAUNDRY' 10.5 54;Prop 'Washers' 17.5 46 1.5 5;Prop 'Fold' 13 49 2 1.5
  RectPlan 20 43 10 12;Door 21 43 23.4 43
  Label 'LINEN / STAFF' 20.5 54;Prop 'Racks' 28 46 1.2 6;Prop 'Table' 24 49 2 1.5
 }else{NorthGuest '206' 0;NorthGuest '207' 10;NorthGuest '208' 20}
 Stair 'A' 12.6 0 6.4 $floor;RectPlan 12.6 6.4 2.4 2.4 $propFill $cyan
 Stair 'B' 0 31.2 6.4 $floor;RectPlan 0 37.6 2.4 2.4 $propFill $cyan
 Label 'A' 10.8 5 $font;Label 'B' 3 36 $font
 if($floor -eq 1){
  Prop 'Car' 2 9 2.5 5;Prop 'Car' 2 17 2.5 5
  Label 'COURTYARD' 2 27;Label 'OPEN TO TRAIN <' 1 25 $small
  L 0 13 10 13 $cyan;L 10 13 10 35 $cyan;L 10 35 14 35 $cyan
 }else{
  RectPlan 3 9 10 18 $voidFill $thin;Label 'OPEN AIR' 4 20;Label 'COURTYARD BELOW' 3.2 18 $small
 }
 Note '01 / THE ROUTE' @('Train -> open courtyard -> covered walk.','Five guest doors along the east wing.','Reception / laundry / linen occupy the north wing.','Upper floor: eight guest rooms, one L-balcony.','Stair A at the south end; stair B at northwest.','Both stairs connect to the same balcony.','No mandatory jumps or locked-door objectives.') 180
 Note '02 / WORKING DIMENSIONS' @('Guest rooms east: 12 x 8 m nominal.','North rooms: 10 x 12 m nominal.','Walk / balcony: 3 m nominal width.','Door openings: 2.4 m; target 2.6 m high.','Stairs: 2.4 m wide, 6.4 m horizontal run.','Reserve 2.4 m landings at top and bottom.','Upper floor +3.6 m; roof approx. +7.2 m.','20 risers at 0.18 m; check tread/head clearance.') 465
 Note '03 / SIGHTLINES + SAFETY' @('Courtyard stays largely open toward the train.','Parked cars provide low, isolated cover.','Guard exposed balcony and stair edges.','Open rails: review safety and duck-eye sightlines.','Rooms are short search pockets, not maze corridors.','Props shown are static blockout masses.','No final loot or enemy positions implied.') 810
 Note '04 / LEVEL KEY' @('Room labels 1xx = ground; 2xx = upper.','Stair arrows point UP on ground, DOWN upstairs.','Blue walk areas are usable floor at this level.') 1090
 SaveSheet ("motel-floor-$floor.png")
}
Sheet 'ROADSIDE MOTEL / SERVICE BUILDING' 'Single storey across the tracks. 20 x 24 m envelope. Local origin = world X -36, Z +78. Train lies east (right).' 20 24 30 100 220
RectPlan 0 0 20 24
# Central spine and east cross-passage, all doors open to circulation.
VWall 8 0 10 4 6.4;VWall 8 14 24 17 19.4
L 0 10 8 10;L 0 14 8 14
VWall 11 0 10 4 6.4;VWall 11 14 24 17 19.4
L 11 10 20 10;L 11 14 20 14
Door 20 10.5 20 13.5;Door 8.3 0 10.7 0;Door 8.3 24 10.7 24
Label 'MAINTENANCE' .4 9;Prop 'Bench' 1 1 5 1.5;Prop 'Tools' 1 4 1.5 3
Label 'BULK STORE' .4 23;Prop 'Racks' 1 16 1.5 5;Prop 'Crates' 4 20 2.5 2
Label 'MANAGER' 11.5 9;Prop 'Desk' 15 2 3 1.5;Prop 'Files' 18 6 1.2 2
Label 'BREAK ROOM' 11.5 23;Prop 'Table' 14 18 3 1.5;Prop 'Sofa' 17.5 21 2 1.2
Label '3 m spine' 8.15 13 $small;Label 'ENTRY >' 15 13 $small
Note '01 / SECOND SEARCH AREA' @('Manager office, maintenance, bulk storage, break room.','Main entrance faces the train across open ground.','North and south exits avoid a single-door trap.','A 3 m central spine joins the 4 m entry passage.','Single storey; proposed roof +3.6 m.','Keep door leaves absent in the first greybox.') 220
Note '02 / RESOURCE DISTRIBUTION' @('Retain six coal chunks per variation: three each side.','This building and its yard support the west-side three.','Use three authored variations; avoid changing totals.','Final marker placement follows traversal review.','Retain gold identities and counts when implementing.') 510
Note '03 / WEST YARD' @('Open loading pad south of the building.','Scattered crates: no new fence maze.','Keep the approach to the east door unobstructed.','West side remains outside the existing enemy graph.','Expanding enemy coverage requires explicit follow-up.') 770
SaveSheet 'service-building.png'
# Site uses translated X: sheet 0 = world -48; Z matches world Z.
Sheet 'ROADSIDE MOTEL / SCALED SITE PLAN' '96 x 160 m existing block. North = travel direction. Track centre X=0. Stop Z=80 retained. Diagram X origin = -48 m.' 96 160 6.3 100 180
RectPlan 0 0 96 160
L 46.9 0 46.9 160 $cyan;L 49.1 0 49.1 160 $cyan
L 0 45 96 45 $cyan;L 0 115 96 115 $cyan
# Building wings and circulation.
RectPlan 76 54 12 40 $propFill;RectPlan 58 97 30 12 $propFill
RectPlan 73 54 3 40 $null $cyan;RectPlan 58 94 30 3 $null $cyan

# Stair A actually at world 22.6 (sheet 70.6); B at world 10.
RectPlan 70.6 54 2.4 8.8 $null $cyan;RectPlan 58 85.2 2.4 8.8 $null $cyan
RectPlan 12 78 20 24 $propFill;RectPlan 12 58 20 16 $null $thin
RectPlan 47 74 2 12 $propFill $cyan
L 32 90 46 90 $cyan;L 50 80 73 80 $cyan
Label 'MOTEL' 60 103 $small;Label 'rooms' 77 77 $small
Label 'COURT' 59 76 $small;Label 'SERVICE' 13 95 $small
Label 'LOADING' 13 70 $small;Label 'STOP' 39 82 $small
RectPlan 58 48 2 2 $propFill $cyan;Label 'SIGN' 61 51 $small
Label '45 m quiet approach' 6 28;Label '45 m quiet departure' 6 142
Label 'Z 115' 0 119 $small;Label 'Z 45' 0 44 $small
Note '01 / VERIFIED BASE' @('Source: roadside-motel-v1.prefab + RailWeek3Builder.cs.','Ground 96 x 160 m; track is straight at X=0.','Suggested stop at Z=80.','Encounter span Z=45..115; quiet buffers retained.','Train rectangle marks the stop, not its exact footprint.') 180
Note '02 / PROPOSED FOOTPRINTS' @('Motel east wing: X=28..40, Z=54..94.','Motel north wing: X=10..40, Z=97..109.','3 m walk/balcony faces the courtyard.','Service building: X=-36..-16, Z=78..102.','West loading pad: X=-36..-16, Z=58..74.','Motel sign near X=11, Z=49.','No buildings placed inside the central track corridor.') 430
Note '03 / GAMEPLAY INTENT' @('Recognisable motel doors and exposed upper walk.','Two stairs create a ground/balcony return loop.','Players can see the train from the courtyard.','Separate west search area encourages crew splitting.','Six coal chunks total per chosen variation, three per side.','Keep enemies off the train and inside this block.') 730
Note '04 / IMPLEMENTATION FOLLOW-UP' @('Drawings only: existing prefab is unchanged.','Replace placeholder shells only after drawing review.','Extend east graph bounds to cover wing at X=40.','Bake stairs / upper balcony offline; no runtime baking.','Preserve track, IDs, spawn identities and lighting workflow.','Review room scale, guards and stairs in VR.') 1010
SaveSheet 'site-plan.png'


