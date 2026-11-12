$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Drawing
$folder=Join-Path $PSScriptRoot 'skyscraper-yard-polish'
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
Text 'SKYSCRAPER WORKS / YARD POLISH' 66 25 $title
Text 'Proposed top-down layout | North up | 96 x 160 m | Existing tower loop retained' 70 83
Box -48 0 96 160 '#c6cfbb';Box -42 6 84 148 '#dce0d3';Box -7 0 14 160 '#adb8af'
Line -1 0 -1 160;Line 1 0 1 160
Tower -38 54 'A';Tower 12 54 'B';Tower -38 86 'C';Tower 12 86 'D'
Bridge -12 64 24 4 '#88b5ae';Bridge -12 96 24 4 '#648d9b';Bridge -27 78 4 8 '#88b5ae';Bridge 23 78 4 8 '#88b5ae'
Line -20 62 25 90 '#b77e32' 12
Outline -40 121 25 24 '#aa9279';Label 'FOUNDATION' -39 140
Box -37 122 6 8 '#d6c6a5';Box -24 137 6 8 '#d6c6a5';Label 'ramps' -37 130
Outline -39 146 13 7 '#d1a04b';Label 'Excavator' -40 153
Outline 17 132 23 16 '#b8c5bd';Label 'WORKSHOP' 18 145
Outline 19 115 9 13 '#d1a04b';Label 'Mixer' 19 126
Outline 33 115 7 12 '#ab9770';Label 'Piles' 33 125
Outline -40 14 23 8 '#c6b28c';Label 'Office 1' -39 21
Outline -40 24 8 18 '#c6b28c';Label '2' -38 37
Outline -30 25 12 10 '#b8c5bd';Label 'Break' -29 33
Outline -28 39 12 8 '#ab9770';Label 'Parking' -28 46
Outline 17 29 23 16 '#b8c5bd';Label 'LOADING' 18 43
Outline 18 12 8 14 '#d1a04b';Label 'Truck' 18 24
Outline 30 14 10 10 '#ab9770';Label 'Pipes' 30 22
Label 'Sign / toilets' -39 12
Label 'NORTH / DEPARTURE' -15 158
Label 'SOUTH / APPROACH' -15 5
Text 'NORTH: HEAVY WORKS' 900 180 $head
Text "Shallow foundation with two ramp exits.`nExcavator at the rim; concrete footings.`nMixer, aggregate and maintenance shed.`nShort fences and service tracks join clusters." 900 225
Text 'SOUTH: WORKERS COMPOUND' 900 390 $head
Text "L-shaped office trailers, one enterable.`nCovered break area, parking and toilets.`nTruck, loading shelter and material stacks.`nCheerful development sign; warm windows." 900 435
Text 'UPPER-FLOOR LOOT PROPOSAL' 900 620 $head
Text "Keep all existing 150 coal / 150 gold candidates.`nAdd 120 candidates of each type across towers:`n  +12 m: 10 coal + 10 gold per tower`n  +16 m: 20 coal + 20 gold per tower`nResult: 270 coal / 270 gold candidates.`nExact positions subject to support/spacing checks.`nPer-run totals stay unchanged; upper-floor odds rise.`nNo guaranteed floor coverage from random selection." 900 665
Text 'LAYOUT RULES' 900 940 $head
Text "Keep X -7..7 railway reserve clear.`nRetain flat 6 m perimeter seams.`nPreserve tower stairs, shafts and crossings.`nAsymmetric clusters with open return routes.`nCool worklights north / warm office lights south.`nStatic machinery; no new vehicle mechanics." 900 985
Text 'PROPOSAL / REVIEW BEFORE BUILD' 900 1250 $head
Text "Schematic footprints, not final collision geometry.`nCurrent prefab has local edits to preserve.`nNo scene or spawn changes made for this drawing." 900 1295
$bmp.Save((Join-Path $folder 'yard-polish-plan.png'),[Drawing.Imaging.ImageFormat]::Png)
$g.Dispose();$bmp.Dispose()
