param([string]$Output = (Join-Path $PSScriptRoot "../../output/nuclear-full-block/blueprint.png"))
Add-Type -AssemblyName System.Drawing
$bitmap = [Drawing.Bitmap]::new(1380,1390)
$canvas = [Drawing.Graphics]::FromImage($bitmap)
$canvas.SmoothingMode = [Drawing.Drawing2D.SmoothingMode]::AntiAlias
$canvas.TextRenderingHint = [Drawing.Text.TextRenderingHint]::AntiAliasGridFit
function Colour($hex) { [Drawing.ColorTranslator]::FromHtml("#" + $hex) }
function Brush($hex) { [Drawing.SolidBrush]::new((Colour $hex)) }
function Pen($hex,$size) { [Drawing.Pen]::new((Colour $hex),[float]$size) }
function Text($value,$x,$y,$size,$hex,$bold=$false) {
    $style=if ($bold) {[Drawing.FontStyle]::Bold} else {[Drawing.FontStyle]::Regular}
    $font=[Drawing.Font]::new("Segoe UI",$size,$style,[Drawing.GraphicsUnit]::Pixel)
    $b=Brush $hex
    $canvas.DrawString($value,$font,$b,[float]$x,[float]$y)
    $b.Dispose(); $font.Dispose()
}
function Line($x1,$y1,$x2,$y2,$pen) { $canvas.DrawLine($pen,[float]$x1,[float]$y1,[float]$x2,[float]$y2) }
function X($u) { 690.0 + 6.75 * $u }
function Y($v) { 1250.0 - 6.75 * $v }
function Road($x,$z,$w,$d,$fill,$pen) {
    $u=X $x; $v=Y ($z+$d); $wd=6.75*$w; $ht=6.75*$d
    if($fill) {$b=Brush $fill; $canvas.FillRectangle($b,[float]$u,[float]$v,[float]$wd,[float]$ht); $b.Dispose()}
    $canvas.DrawRectangle($pen,[float]$u,[float]$v,[float]$wd,[float]$ht)
}
function Ring($x,$z,$r,$pen,$fill) {
    $u=X ($x-$r); $v=Y ($z+$r); $dia=13.5*$r
    if($fill) {$b=Brush $fill; $canvas.FillEllipse($b,[float]$u,[float]$v,[float]$dia,[float]$dia); $b.Dispose()}
    $canvas.DrawEllipse($pen,[float]$u,[float]$v,[float]$dia,[float]$dia)
}
function RailLine($x,$z,$xx,$zz,$pen) { Line (X $x) (Y $z) (X $xx) (Y $zz) $pen }
function Caption($name,$hint,$side,$pointX,$pointZ,$lineY) {
    $px=X $pointX; $py=Y $pointZ
    if($side -eq 'west') {$anchor=337; $label=40; $elbow=350} else {$anchor=1044;$label=1053;$elbow=1030}
    Line $px $py $elbow $lineY $guide
    Line $elbow $lineY $anchor $lineY $guide
    $b=Brush "e6c582"; $canvas.FillEllipse($b,[float]($px-3),[float]($py-3),6,6); $b.Dispose()
    Text $name $label ($lineY-25) 24 "dfedf1" $true
    Text $hint $label ($lineY+5) 17 "94b5bf"
}
$edge=Pen "c1e4e9" 2
$detail=Pen "80b1bd" 1.5
$walk=Pen "496572" 1
$grid=Pen "183746" 1
$guide=Pen "71909d" 1.3
$rail=Pen "e1bd82" 1.7
$water=Pen "77b6ba" 1.4
$canvas.Clear((Colour "102733"))
Text "Nuclear Power Plant" 39 31 44 "e6f3f5" $true
Text "Duck Rails  /  Full-block site plan" 42 88 21 "98bcc7"
Text "NORTH" 1215 52 18 "e1bd82" $true
Line 1323 88 1323 42 $rail; Line 1323 42 1314 58 $rail; Line 1323 42 1332 58 $rail
Line 42 134 1337 134 $walk
Road -48 0 96 160 "132e3b" $edge
for ($m=-45;$m -lt 48;$m+=5) {RailLine $m 0 $m 160 $grid}
for ($m=5;$m -lt 160;$m+=5) {RailLine -48 $m 48 $m $grid}
foreach ($u in @(-41,-12,-7,4,9,42)) {Road $u 6 3 148 "1a3542" $walk}
foreach ($v in @(38,67,94,124)) {Road -41 ($v-1.5) 34 3 "1a3542" $walk; Road 7 ($v-1.5) 38 3 "1a3542" $walk}
Road -4 0 8 160 "182d36" $walk
for ($m=1;$m -lt 160;$m+=2) {RailLine -1.9 $m 1.9 $m $guide}
RailLine -1.5 0 -1.5 160 $rail; RailLine 1.5 0 1.5 160 $rail
Road -37.5 10.5 29 19 "28414d" $detail; Road -21.4 16.5 2.8 9 "476773" $edge; Road -33 16.7 2.1 4.6 "476773" $detail
foreach($u in @(-31,-27,-15)){RailLine $u 17.5 $u 24.5 $detail}
Road 11 16 29 18 "28414d" $detail
Road 15.4 25 3.0 7 "476773" $detail
for($cl=0;$cl -lt 3;$cl++){for($row=0;$row -lt 3;$row++){for($col=0;$col -lt 5;$col++){Ring (27+1.02*$col) (18+$cl*5+$row*1.02) .42 $rail $null}}}
foreach($u in @(12,19,26,33)){for($i=0;$i -lt 4;$i++){Road ($u-2+$i*1.2-.48) 7 .96 9 "2b4752" $detail}}
Road -38 35 14 12 "335463" $edge; Road 27 37 13 12 "335463" $edge
Road -40 55 28 36 "36515f" $edge; Road -40 55 8 36 "2d4856" $detail
foreach($v in @(63,78)){Road -26 $v 6 8 "406271" $detail}
foreach($v in @(57,80)){Road -36 $v 3 9 "29424d" $detail; for($i=0;$i -lt 9;$i++){RailLine -36 ($v+$i) -33 ($v+$i) $detail}}
Ring -26 103 10 $edge "355362"; Ring -26 103 8.8 $detail $null; Ring -26 103 5.5 $detail $null
foreach($v in @(67,94)){
    Ring 26 $v 11 $edge "345363"; Ring 26 $v 8.5 $detail $null; Ring 26 $v 6.8 $detail $null
    RailLine 15 $v 37 $v $walk; RailLine 26 ($v-10.5) 26 ($v+10.5) $walk
}
foreach($u in @(14.3,15.24,16.18)){RailLine $u 49 $u 112 $detail}
Road -39 120 25 19 "274851" $detail; Road -37 122 21 15 "2d575f" $water; Road -34 124.5 20 13 "2b5059" $water
for($z=127;$z -lt 136;$z+=2){RailLine -32 $z -16 $z $water}
Road -37 140 10 7 "335463" $edge; Road 15 121 25 20 "28414d" $detail
foreach($u in @(19,29)){foreach($v in @(125,134)){Road $u $v 5 3 "3c5966" $detail;foreach($i in @(1,2.5,4)){Ring ($u+$i) ($v+1.5) .26 $edge $null}}}
for($i=0;$i -lt 4;$i++){Road 18.6 (144.9+$i*2) 8.8 2.2 "2d575f" $water}
Caption "Pump hut" "Basin service building" west -32 143.5 260
Caption "Settling pond" "Reed-lined cooling basin" west -25 130 395
Caption "Containment" "Sealed reactor landmark" west -26 103 555
Caption "Turbine hall" "Gallery and two stair flights" west -28 76 741
Caption "Gatehouse" "Employee entrance" west -32 40 975
Caption "Shift bus turnaround" "Employee parking" west -20 21 1121
Caption "Discharge flume" "Four stepped water ledges" east 23 150 240
Caption "Transformer yard" "Four cooling-fin banks" east 31.5 135.5 373
Caption "Cooling tower B" "Drained basin" east 31 95 603
Caption "Cooling tower A" "Wet basin and crosswalks" east 31 68 790
Caption "Maintenance shed" "Service access" east 34 43 969
Caption "Drum yard" "Truck and stacked barrels" east 30 25 1073
Caption "Pipe delivery" "Four storage racks" east 33 11 1178
Text "SOUTH  /  TRAIN ENTRY" 573 1258 18 "d6c095" $true
Line 42 1300 1337 1300 $walk
Text "96 m wide  ×  160 m long" 38 1322 21 "dce8eb" $true
$u=1100; $v=1345
Line $u $v ($u+135) $v $edge; Line $u ($v-5) $u ($v+4) $edge; Line ($u+67.5) ($v-5) ($u+67.5) ($v+4) $edge; Line ($u+135) ($v-5) ($u+135) ($v+4) $edge
Text "0" ($u-4) ($v+7) 15 "a7c0c9"; Text "10" ($u+57) ($v+7) 15 "a7c0c9"; Text "20 m" ($u+119) ($v+7) 15 "a7c0c9"
$dest=[IO.Path]::GetFullPath($Output)
[IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($dest)) | Out-Null
$bitmap.Save($dest,[Drawing.Imaging.ImageFormat]::Png)
foreach($p in @($edge,$detail,$walk,$grid,$guide,$rail,$water)){$p.Dispose()}
$canvas.Dispose(); $bitmap.Dispose()