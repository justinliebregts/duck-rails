$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Drawing
Remove-Item Alias:r -ErrorAction SilentlyContinue
function B($c){[Drawing.SolidBrush]::new([Drawing.ColorTranslator]::FromHtml($c))}
$white=B '#edf3ef';$muted=B '#a9c0cb';$floor=B '#244653';$room=B '#345661';$prop=B '#52727b';$gold=B '#efbd62';$mint=B '#79dac8';$dark=B '#102938'
$wall=[Drawing.Pen]::new($white,3);$thin=[Drawing.Pen]::new((B '#4a6a78'),1);$yellow=[Drawing.Pen]::new($gold,3);$path=[Drawing.Pen]::new($mint,4);$dash=[Drawing.Pen]::new($mint,2);$dash.DashStyle='Dash';$ghost=[Drawing.Pen]::new((B '#4a6a78'),2);$ghost.DashStyle='Dash'
$f=[Drawing.Font]::new('Arial',17);$sm=[Drawing.Font]::new('Arial',13);$tiny=[Drawing.Font]::new('Arial',9);$h=[Drawing.Font]::new('Arial',25,[Drawing.FontStyle]::Bold);$big=[Drawing.Font]::new('Arial',33,[Drawing.FontStyle]::Bold)
function Text($s,$x,$y,$font=$f,$brush=$white){$g.DrawString($s,$font,$brush,[single]$x,[single]$y)}
function P([double]$x,[double]$z){[Drawing.PointF]::new([single]($ox+$x*$scale),[single]($oy-($z-66)*$scale))}
function R([double]$x,[double]$z,[double]$w,[double]$d,$fill=$floor,$pen=$wall){$p=P $x ($z+$d);if($fill){$g.FillRectangle($fill,$p.X,$p.Y,[single]($w*$scale),[single]($d*$scale))};$g.DrawRectangle($pen,$p.X,$p.Y,[single]($w*$scale),[single]($d*$scale))}
function L([double]$x,[double]$z,[double]$u,[double]$v,$pen=$wall){$g.DrawLine($pen,(P $x $z),(P $u $v))}
function Label($s,[double]$x,[double]$z,$font=$sm,$brush=$white){$p=P $x $z;Text $s $p.X $p.Y $font $brush}
function Arrow([double]$x,[double]$z,[double]$u,[double]$v){L $x $z $u $v $path;$p=P $u $v;$a=P $x $z;$dx=$p.X-$a.X;$dy=$p.Y-$a.Y;$len=[Math]::Sqrt($dx*$dx+$dy*$dy);if($len -gt 0){$dx/=$len;$dy/=$len;$g.DrawLine($path,$p.X,$p.Y,[single]($p.X-12*$dx+6*$dy),[single]($p.Y-12*$dy-6*$dx));$g.DrawLine($path,$p.X,$p.Y,[single]($p.X-12*$dx-6*$dy),[single]($p.Y-12*$dy+6*$dx))}}
function Dot($id,[double]$x,[double]$z){$p=P $x $z;$g.FillEllipse($gold,$p.X-5,$p.Y-5,10,10);Text $id ($p.X+8) ($p.Y-9) $sm $gold}
function Door([double]$x,[double]$z){L $x $z $x ($z+2.4) ([Drawing.Pen]::new($floor,6));L ($x-.2) $z ($x+.2) $z $path;L ($x-.2) ($z+2.4) ($x+.2) ($z+2.4) $path}
function Stairs([double]$x){R $x 71 2.4 11.32 $room;for($z=71.34;$z -lt 75.76;$z+=.34){L $x $z ($x+2.4) $z $thin};for($z=77.9;$z -lt 82.32;$z+=.34){L $x $z ($x+2.4) $z $thin};R $x 75.76 2.4 1.8 $prop $thin;Arrow ($x+1.2) 81.5 ($x+1.2) 72;Label 'UP' ($x+.2) 80 $sm}
function Base($upper=$false){R -13 68 10 24 $(if($upper){$null}else{$floor}) $(if($upper){$ghost}else{$wall});R 3 68 10 24 $(if($upper){$null}else{$floor}) $(if($upper){$ghost}else{$wall});for($z=66;$z -le 96;$z+=2){L -2 $z 2 $z $thin};L -1.5 66 -1.5 96 $thin;L 1.5 66 1.5 96 $thin;R -2.5 75 5 10 $(if($upper){$null}else{$prop}) $ghost;Label "TRAIN`n5 x 10 m`nDECK" -2.2 83 $sm;Arrow 0 89 0 95;Label 'TO TUNNEL' -2.8 97;Label '10 m' -10 94.5 $f;Label '6 m' -.8 94.5 $f;Label '10 m' 6 94.5 $f}
$img=[Drawing.Bitmap]::new(2000,1400);$g=[Drawing.Graphics]::FromImage($img);$g.SmoothingMode='AntiAlias';$g.Clear([Drawing.ColorTranslator]::FromHtml('#102938'))
Text 'SPLIT-PLATFORM STATION / DETAILED PLAN' 55 35 $big
Text 'Design proposal v2  |  Dimensions in metres  |  Track-local Z shown in notes; departure is up' 58 92 $f $muted
Text '01  PLATFORM LEVEL  /  Y +1.00' 55 160 $h
Text '02  FOOTBRIDGE LEVEL  /  Y +6.10' 1050 160 $h
$scale=24;$oy=1000;$ox=460;Base
Label '24 m' -17 80 $f
R -13 86 7 6 $room;Door -6 86.7;Label "SUPPLIES`n7 x 6 m" -12.5 91.6
R -12.6 86.4 .6 3.2 $prop $thin;R -10.8 86.5 2.7 .7 $prop $thin;Label 'shelves / counter' -12.5 89.6
R 6 89 7 3 $room;Door 6 89.3;Label 'STAFF  7 x 3 m' 6.5 91.6;R 11 89.4 1.5 .8 $prop $thin
R 6 86 7 3 $room;Door 6 86.3;Label 'STORE  7 x 3 m' 6.5 88.5;R 10.7 86.4 1.8 .7 $prop $thin
R 6.5 83 3 .7 $prop $thin;Label 'WAITING / BENCH' 6.3 85.2
Stairs -12.5;Stairs 10.1
R -12.5 82.32 2.4 2.4 $null $dash;R 10.1 82.32 2.4 2.4 $null $dash
Label 'CLEAR LANDING' -12.5 84.5 $sm
L -3 68 -3 92 $yellow;L 3 68 3 92 $yellow
Label "3 m CLEAR`nBOARDING`nAISLE" -5.9 84.3 $tiny;Label "3 m CLEAR`nBOARDING`nAISLE" 3.2 79.5 $tiny
R -9.5 72 3 5 $null $dash;Label "ARRIVAL /`nREGROUP" -9.3 76.5 $sm
L -8.8 68 -6.4 68 ([Drawing.Pen]::new($floor,6));Arrow -7.6 66.4 -7.6 71
Label 'ARRIVAL PASSAGE' -12.5 66.3 $sm
Arrow -7.5 78.5 -4.7 78.5;Arrow -4.7 78.5 -2.8 78.5
Arrow 7 78.5 3 78.5
R -12.5 68 25 3 $null $dash;Label 'FOOTBRIDGE ABOVE' -5.6 70.3 $sm $mint
Dot 'L1' -8 90;Dot 'L2' 8 90;Dot 'L3' -7 80.5;Dot 'L4' 7 81;Dot 'L5' -7 69.5;Dot 'L6' 7 69.5
$ox=1455;Base $true
R -12.5 68 25 3 $floor $wall
Stairs -12.5;Stairs 10.1
L -12.5 71 -10.1 71 ([Drawing.Pen]::new($floor,5));L 10.1 71 12.5 71 ([Drawing.Pen]::new($floor,5))
Arrow -10 69.5 9.5 69.5
Label '3 m WIDE CROSSING' -5.5 70.5 $sm
Label "OPEN TO PLATFORM BELOW`nNo second-floor rooms" -11.5 91 $f $muted
Label "STAIR VOID`n2.4 m clear" -12.5 85.5 $sm;Label "STAIR VOID`n2.4 m clear" 8.5 85.5 $sm
Dot 'L7' -8 69;Dot 'L8' 8 69
Label 'Guarded edges / 1.2 m rails' -7 66.5 $sm $muted
Text "ROOMS + CIRCULATION" 55 1090 $h
Text "Two 10 x 24 m platforms: 480 m2 gross (+11% vs Compound).`nBridge + upper landings: 75 m2 additional elevated floor.`nRoom doors 2.4 m clear; wall thickness deducted at greybox stage.`nTrain outline is the measured deck, not the full swept envelope." 55 1138 $f $muted
Text 'STAIRS + LIGHT POCKETS' 1050 1090 $h
Text "Each stair: 30 risers at 0.17 m; two flights, 14 treads each.`n0.34 m treads + 1.8 m mid-landing = 11.32 m horizontal run.`nL1-2 room pools; L3-4 platform pools; L5-8 stair / bridge lights.`nKeep fixtures above head clearance; dark gaps remain between pools." 1050 1138 $f $muted
Text 'Yellow: platform edge / light fixture   Mint: circulation / overhead outline   Dashed grey: train and lower-level reference' 55 1300 $sm $muted
Text 'Both-side boarding is a proposal: check the 0.50 m deck gap, current train rails and low VR eye line in greybox.' 55 1333 $f $gold
$img.Save((Join-Path $PSScriptRoot 'split-platform-detailed-v2.png'));$g.Dispose();$img.Dispose()
# second sheet: site and cross section
$img=[Drawing.Bitmap]::new(1800,1300);$g=[Drawing.Graphics]::FromImage($img);$g.SmoothingMode='AntiAlias';$g.Clear([Drawing.ColorTranslator]::FromHtml('#102938'))
Text 'STATION / MOUNTAIN EXIT + CLEARANCE' 55 35 $big
Text 'Proposed fit inside the existing 96 x 160 m starting block; straight track and Z 80 start retained' 55 92 $f $muted
Text '03  TOP-DOWN SITE STRIP' 55 160 $h
# X screen follows Z at 10px/m, vertical represents X at same scale
$g.FillRectangle($room,70,240,1400,320);$g.DrawRectangle($thin,70,240,1600,320)
$g.FillRectangle($dark,70,360,1600,80)
$g.FillRectangle($floor,750,270,240,100);$g.FillRectangle($floor,750,430,240,100)
$g.DrawRectangle($wall,750,270,240,100);$g.DrawRectangle($wall,750,430,240,100)
$g.DrawLine($thin,70,385,1670,385);$g.DrawLine($thin,70,415,1670,415)
$g.FillRectangle($prop,820,375,100,50)
$g.DrawRectangle($dash,750,275,30,250)
$g.DrawLine($yellow,1470,230,1470,570)
Text 'MOUNTAIN ROCK' 100 265 $f;Text 'STATION' 790 250 $sm
Text "SEALED / INACCESSIBLE`nREAR RAIL PASSAGE" 125 470 $f $muted
Text "48 m DEPARTURE TUNNEL`n8 m clear width; 7 m clear height" 1040 465 $f
Text "MOUNTAIN`nPORTAL" 1370 600 $f $gold;Text 'OUTDOORS' 1530 265 $f
foreach($z in @(0,68,80,92,140,160)){Text "Z $z" (60+$z*10) 570 $sm}
Text 'Station room envelope: X -13 to +13, Z 68 to 92. Rear arrival passage is scenic access, not a new matchmaking flow.' 55 655 $sm $muted
Text '04  CROSS SECTION AT FOOTBRIDGE  /  HEIGHTS ABOVE TRACK DATUM' 55 725 $h
# section x = 440 + x*24, y=1080-y*32
$g.FillRectangle($floor,128,1048,240,32);$g.FillRectangle($floor,512,1048,240,32)
$g.FillRectangle($prop,380,1048,120,10)
$g.DrawRectangle($dash,378,920,124,164)
$g.FillRectangle($floor,140,885,600,10);$g.DrawRectangle($wall,140,885,600,10)
$g.DrawLine($wall,140,846,740,846)
$g.DrawLine($wall,128,805,752,805)
$g.DrawLine($thin,128,1080,752,1080)
Text 'TRACK DATUM  0.00' 780 1065 $sm
Text 'PLATFORM / TRAIN DECK  +1.00' 780 1033 $sm
Text 'MEASURED MESH ENVELOPE TOP  ~+5.00' 780 920 $sm
Text 'BRIDGE UNDERSIDE  +5.80  /  TOP +6.10' 780 875 $f $gold
Text 'GUARD TOP  +7.30' 780 837 $sm
Text 'STATION CEILING  +8.60  (2.50 m above bridge)' 780 795 $sm
Text '0.80 m nominal margin over measured train meshes' 55 1130 $f $gold
Text "Train deck measured 5 x 10 m; all prefab mesh bounds: X +/-2.58 m, Y max +4.00 relative to deck pivot.`nRoute places that pivot at +1.00 m. Mesh envelope includes attached details; smoke and moving players need separate review.`nBridge / tunnel dimensions are proposed, not collision-tested. Tunnel remains level as surrounding terrain falls away." 55 1170 $f $muted
$img.Save((Join-Path $PSScriptRoot 'split-platform-site-section-v2.png'));$g.Dispose();$img.Dispose()

