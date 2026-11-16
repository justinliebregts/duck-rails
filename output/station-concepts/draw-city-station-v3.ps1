$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Drawing
Remove-Item Alias:r -ErrorAction SilentlyContinue
function B($c){[Drawing.SolidBrush]::new([Drawing.ColorTranslator]::FromHtml($c))}
$white=B '#edf3ef';$muted=B '#a9c0cb';$floor=B '#244653';$room=B '#345661';$prop=B '#52727b';$gold=B '#efbd62';$mint=B '#79dac8';$dark=B '#0b1e2a';$rock=B '#1a3340';$ground=B '#23402f';$city=B '#2b3550'
$wall=[Drawing.Pen]::new($white,3);$thin=[Drawing.Pen]::new((B '#4a6a78'),1);$yellow=[Drawing.Pen]::new($gold,3);$path=[Drawing.Pen]::new($mint,3);$dash=[Drawing.Pen]::new($mint,2);$dash.DashStyle='Dash';$ghost=[Drawing.Pen]::new((B '#6f8a96'),2);$ghost.DashStyle='Dash';$fence=[Drawing.Pen]::new($mint,3);$fence.DashStyle='Dot'
$f=[Drawing.Font]::new('Arial',16);$sm=[Drawing.Font]::new('Arial',12);$tiny=[Drawing.Font]::new('Arial',10);$h=[Drawing.Font]::new('Arial',24,[Drawing.FontStyle]::Bold);$big=[Drawing.Font]::new('Arial',32,[Drawing.FontStyle]::Bold)
function Text($s,$x,$y,$font=$f,$brush=$white){$g.DrawString($s,$font,$brush,[single]$x,[single]$y)}
function P([double]$x,[double]$z){[Drawing.PointF]::new([single]($ox+$x*$scale),[single]($oy-($z-$z0)*$scale))}
function R([double]$x,[double]$z,[double]$w,[double]$d,$fill=$floor,$pen=$wall){$p=P $x ($z+$d);if($fill){$g.FillRectangle($fill,$p.X,$p.Y,[single]($w*$scale),[single]($d*$scale))};if($pen){$g.DrawRectangle($pen,$p.X,$p.Y,[single]($w*$scale),[single]($d*$scale))}}
function L([double]$x,[double]$z,[double]$u,[double]$v,$pen=$wall){$g.DrawLine($pen,(P $x $z),(P $u $v))}
function Label($s,[double]$x,[double]$z,$font=$sm,$brush=$white){$p=P $x $z;Text $s $p.X $p.Y $font $brush}
function Arrow([double]$x,[double]$z,[double]$u,[double]$v){L $x $z $u $v $path;$p=P $u $v;$a=P $x $z;$dx=$p.X-$a.X;$dy=$p.Y-$a.Y;$len=[Math]::Sqrt($dx*$dx+$dy*$dy);if($len -gt 0){$dx/=$len;$dy/=$len;$g.DrawLine($path,$p.X,$p.Y,[single]($p.X-12*$dx+6*$dy),[single]($p.Y-12*$dy-6*$dx));$g.DrawLine($path,$p.X,$p.Y,[single]($p.X-12*$dx-6*$dy),[single]($p.Y-12*$dy+6*$dx))}}
function Dot([double]$x,[double]$z){$p=P $x $z;$g.FillEllipse($gold,$p.X-5,$p.Y-5,10,10)}
# opening in a wall that runs along Z at $x
function DoorZ([double]$x,[double]$z,[double]$len,$fill=$floor){L $x $z $x ($z+$len) ([Drawing.Pen]::new($fill,7));L ($x-.3) $z ($x+.3) $z $path;L ($x-.3) ($z+$len) ($x+.3) ($z+$len) $path}
function Stairs([double]$x){R $x 71 2.4 11.32 $room;for($z=71.34;$z -lt 75.76;$z+=.34){L $x $z ($x+2.4) $z $thin};for($z=77.9;$z -lt 82.32;$z+=.34){L $x $z ($x+2.4) $z $thin};R $x 75.76 2.4 1.8 $prop $thin;Arrow ($x+1.2) 81.6 ($x+1.2) 72;Label 'UP' ($x+.25) 80.2 $tiny}

$img=[Drawing.Bitmap]::new(2200,1600);$g=[Drawing.Graphics]::FromImage($img);$g.SmoothingMode='AntiAlias';$g.TextRenderingHint='AntiAlias';$g.Clear([Drawing.ColorTranslator]::FromHtml('#102938'))
Text 'STARTING STATION v3 / CITY SUBWAY PLATFORMS' 55 30 $big
Text 'Top-down layout proposal for confirmation  |  Metres, starting-block local  |  Departure is up  |  Nothing here is built yet' 58 86 $f $muted
Text '01  PLATFORM LEVEL  /  Y +1.00' 55 150 $h
Text '02  WHOLE BLOCK  /  Z 60 - 160' 1290 150 $h

# ---------- 01 platform plan ----------
$scale=26;$z0=66;$ox=640;$oy=1050
# rock shell
R -22 65 42 31.5 $rock $null
# track bed and tunnel mouth
R -3 68 6 24 $dark $null
R -4 92 8 4.5 $dark $null
for($z=68.5;$z -le 96;$z+=1.5){L -1.6 $z 1.6 $z $thin}
L -.75 68 -.75 96.5 $thin;L .75 68 .75 96.5 $thin
# platforms
R -13 68 10 24 $floor $wall;R 3 68 10 24 $floor $wall
L -3 68 -3 92 $yellow;L 3 68 3 92 $yellow
# tunnel walls
L -4 92 -4 96.5;L 4 92 4 96.5;L -3 92 -4 92;L 3 92 4 92
Arrow 0 92.6 0 96;Label "TUNNEL 8 m wide" 4.3 95.6 $sm $muted
# rear wall and sealed doors
L -13 68 13 68
L -3 68 3 68 ([Drawing.Pen]::new($gold,7))
Label "SEALED ROLLING DOORS (track ends here)" -5.2 67.6 $sm $gold
L -9.5 68 -6.5 68 ([Drawing.Pen]::new($mint,7))
Label "SEALED GATE`n'TO CONCOURSE'" -12.6 67.6 $tiny $mint
# supplies (recessed west)
R -20 80 7 12 $room $wall
DoorZ -13 86.5 3
Label "SUPPLIES`n7 x 12 m  (84 m2)" -18.7 90.3 $sm
R -19.7 81 .7 10 $prop $thin;Label "shared store wall`n(existing prefab)" -18.7 86.2 $tiny
R -18.5 91.2 3.5 .6 $prop $thin;Label "gold exchange" -14.9 91.9 $tiny
R -15.2 80.4 1.6 1.2 $prop $thin;R -17.3 80.4 1.6 .9 $prop $thin;Label "crates" -17.3 82.2 $tiny
Label "3 m open`ndoorway" -16 88.5 $tiny $mint
# east rooms (recessed)
R 13 87 5 5 $room $wall;DoorZ 13 90.3 1.2
Label "STATION`nOFFICE`n5 x 5 m" 13.6 91.7 $sm
L 13 87.8 13 89.6 ([Drawing.Pen]::new($gold,5));Label "lit window" 13.3 88 $tiny $gold
R 16.6 87.4 1.1 2.2 $prop $thin
R 13 80 5 7 $room $wall;DoorZ 13 85.3 1.2
Label "WASHROOM`n5 x 7 m" 13.6 86.8 $sm
R 16.9 80.4 .8 1.2 $prop $thin;R 16.9 81.8 .8 1.2 $prop $thin;R 16.9 83.2 .8 1.2 $prop $thin;Label "stalls" 15.4 81.6 $tiny
R 13.3 80.4 .6 2.6 $prop $thin;Label "sinks" 14 80.9 $tiny
# stairs + bridge
Stairs -12.5;Stairs 10.1
R -12.5 82.32 2.4 2.4 $null $dash;R 10.1 82.32 2.4 2.4 $null $dash
R -12.5 68 25 3 $null $dash;Label 'FOOTBRIDGE ABOVE (Y +6.1, 3 m wide)' -12.3 69.2 $tiny $mint
# platform dressing
R -11 91.2 3 .6 $prop $thin;Label "bench" -10.6 90.5 $tiny
R 8 91.2 3 .6 $prop $thin;Label "bench" 8.4 90.5 $tiny
R -9.8 72.3 4.4 4.8 $null $dash;Label "SURVIVOR CAMP`nwork lamp,`ngenerator,`ncrates" -9.6 76.9 $tiny
R 5.6 72.3 4.2 4.8 $null $dash;Label "SALVAGE`nbarrels,`npallets,`ntarp stack" 5.8 76.9 $tiny
R -12.7 85 1 1.2 $prop $thin;R 11.6 88.2 1.1 1 $prop $thin
# train
R -2.5 75 5 10 $prop $ghost;Label "TRAIN`nDECK`n5 x 10" -1.7 81.6 $sm
Label "3 m`naisle" -5.7 80.2 $tiny;Label "3 m`naisle" 3.4 80.2 $tiny
# lights
foreach($z in 74,80,86){Dot 0 $z}
foreach($p in @(@(-8,71.6),@(8,71.6),@(-7,84),@(7,84),@(-12.6,88),@(12.6,86),@(12.6,90.9),@(-16.5,84),@(-14,90.9),@(15.3,83),@(17.3,90.8),@(-12.6,78),@(12.6,78))){Dot $p[0] $p[1]}
# dimensions
Label '10 m' -8.8 93.4 $f;Label '6 m' -.9 70.3 $f $muted;Label '10 m' 7.2 93.4 $f
Label "24 m" -21.6 78 $f
L -21.2 68 -21.2 79 $thin;L -21.2 76.4 -21.2 92 $thin
Label 'ROCK' -21.5 66.6 $sm $muted;Label 'ROCK' 15.5 66.6 $sm $muted

# ---------- 02 whole block ----------
$scale=8.6;$z0=60;$ox=1690;$oy=1110
R -48 60 96 80 $rock $null
R -48 140 96 20 $ground $null
# city on top of the rock (backdrop)
foreach($b in @(@(-45,116,13,18),@(-29,122,10,13),@(-16,112,9,20),@(24,114,12,20),@(38,108,9,24),@(22,90,18,14),@(-42,86,16,12))){R $b[0] $b[1] $b[2] $b[3] $city $ghost}
Label "DERELICT CITY BACKDROP`nlow-poly blocks on top of the wall,`na few lit windows, unreachable" -45 109 $sm $muted
# tunnel, station cavity
R -4 92 8 48 $dark $thin
R -13 68 26 24 $floor $wall;R -20 80 7 12 $room $wall;R 13 80 5 12 $room $wall
R -3 68 6 24 $dark $null
L -.75 68 -.75 160 $thin;L .75 68 .75 160 $thin
Label "STATION" -11.5 78 $sm
Label "48 m TUNNEL`n8 x 7 m clear`nlamp every 8 m" 5.5 133 $sm
foreach($z in 100,108,116,124,132){Dot 0 $z}
# retaining wall + portal
L -48 140 -4 140 ([Drawing.Pen]::new($gold,6));L 4 140 48 140 ([Drawing.Pen]::new($gold,6))
L -4 140 -8 144 $yellow;L 4 140 8 144 $yellow
Label "CITY RETAINING WALL  (about 9 m, railing + road on top)" -47 139 $sm $gold
Dot 6 140.8;Dot 10 139
# outdoor
L -47 140 -47 160 $fence;L 47 140 47 160 $fence
Label "CHAIN-LINK`nFENCE" -46 158.5 $sm $mint;Label "CHAIN-LINK`nFENCE" 35 158.5 $sm $mint
R -34 145 9 5 $prop $thin;Label "signal hut" -34 144.5 $tiny
R -20 150 6 3 $prop $thin;Label "rubble / weeds" -20 149.5 $tiny
R 12 151 6 3 $prop $thin;Label "sleeper stack" 12 150.6 $tiny
R 26 144 10 4 $prop $thin;Label "dead bus / skips" 26 143.6 $tiny
R -12 153 4 2 $prop $thin;R 9 155 3 2 $prop $thin
Label "OUTDOOR APPROACH  Z 140 - 160`ngravel, flat seam to next block" -32 159 $sm
L -48 160 48 160 $dash
# sealed rear
Label "SEALED REAR, not modelled (Z 0 - 68)" -30 66.5 $sm $muted
L -48 60 -48 160 $thin;L 48 60 48 160 $thin
foreach($z in 68,80,92,140,160){Label "Z $z" 49 ($z+1.2) $sm $muted}
Label "96 m" -4 163.5 $f

# ---------- notes ----------
Text 'WHAT CHANGES FROM THE GREYBOX' 55 1190 $h
Text "Supplies doubles to 7 x 12 m and moves behind the west wall, entered by a 3 m open doorway.`nWashroom and station office move behind the east wall; both platforms become fully open (10 x 24 m).`nRear end closes with sealed rolling doors on the track and a sealed 'To Concourse' gate (the Compound side).`nSurvivor dressing as in the Compound: work lamps, generator, crates, salvage. Olive band on plaster walls." 55 1236 $f $muted
Text 'WHAT STAYS' 1290 1190 $h
Text "160 m block, track at X 0, train stop at Z 80, platform height +1.`nBoth 10 x 24 m platforms, footbridge, both stairs, 48 m tunnel.`nNo enemies; shop and gold exchange behaviour unchanged." 1290 1236 $f $muted
Text 'Yellow dot: warm baked fixture (proposed)   Mint dashed: overhead / dressing zone   Mint dotted: chain-link fence   Gold line: sealed door, window or retaining wall' 55 1450 $sm $muted
Text 'Mountain exit is replaced by the city retaining wall. Room sizes, fence lines and scenery positions are proposals until confirmed.' 55 1490 $f $gold
$img.Save((Join-Path $PSScriptRoot 'city-station-layout-v3.png'));$g.Dispose();$img.Dispose()
