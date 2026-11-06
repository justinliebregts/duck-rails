$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Drawing
$folder=Join-Path $PSScriptRoot 'remaining-layouts-v1'
New-Item -ItemType Directory -Force $folder | Out-Null
# Each footprint is x,z,width,depth,label. All measurements are proposed metres.
$plans=@(
 @{name='Convenience Store';bend=0;idea='Forecourt, shopping aisles and rear delivery loop';terrain='Low grassy mound west; store and forecourt at 0 m.';hills=,( @(-40,65,18,38,1) );build=@(@(14,64,24,34,'Store'),@(-33,51,17,15,'Stockroom'),@(-35,95,20,16,'Garage'));props=@(@(21,70,2,9),@(29,70,2,9),@(21,85,2,8),@(29,85,2,8),@(10,54,15,4));notes=@('Store: front and rear doors; wide aisle loop.','Rear delivery lane reconnects north of train.','West mound screens garage without enclosing it.')},
 @{name='Lumber Yard';bend=4;idea='Saw shed, stacked timber and a wooded rear ridge';terrain='East ridge +2 m; yard stays level. Long ramp on rim.';hills=,( @(27,48,14,66,2) );build=@(@(16,97,20,15,'Saw shed'),@(-34,51,18,16,'Tool shed'),@(-36,94,20,17,'Dry store'));props=@(@(17,55,7,10),@(29,55,7,10),@(17,74,7,10),@(29,74,7,10));notes=@('Stacks create two parallel routes, linked mid-yard.','Raised rear path provides a view over the timber.','Ridge route: 24 m minimum run for 2 m rise.')},
 @{name='Container Yard';bend=0;idea='Open-ended containers and a central dispatch court';terrain='Mostly level industrial slab; low perimeter banks.';hills=,( @(-41,65,15,27,1) );build=@(@(-35,50,21,16,'Dispatch'),@(-36,95,22,16,'Repair bay'));props=@(@(15,51,6,14),@(29,51,6,14),@(15,75,6,14),@(29,75,6,14),@(15,98,6,14),@(29,98,6,14));notes=@('Six containers: open at both short ends.','Central cross-aisle gives a direct rescue shortcut.','Keep this block flatter to contrast rural terrain.')},
 @{name='Passenger Station';bend=0;idea='Abandoned local stop with two platform-side loops';terrain='Station pads level; wooded bank +2 m on east edge.';hills=,( @(30,47,12,67,2) );build=@(@(17,58,20,32,'Waiting hall'),@(-36,51,20,17,'Ticket hall'),@(-36,96,21,16,'Parcel shed'));props=@(@(8,49,5,64),@(-13,49,5,64),@(23,98,12,10));notes=@('A scavenging stop, separate from StartingStation.','Hall has front/back exits and a rear service lane.','Cross tracks beyond both ends of stopped train.')},
 @{name='Farm Silos';bend=-4;idea='Rolling pasture, barn court and two silo landmarks';terrain='East pasture +2 m; west knoll +1 m; level barn pad.';hills=@(@(22,48,20,65,2),@(-41,66,22,28,1));build=@(@(13,53,22,23,'Barn'),@(-37,49,19,17,'Workshop'),@(-37,97,22,15,'Feed store'));props=@(@(27,85,9,9),@(27,101,9,9),@(13,84,7,23));notes=@('Round silos above the barn; pens face the train.','24+ m graded route climbs beside the east pasture.','Barn loop stays low; hill path is an optional detour.')},
 @{name='Tunnel Works';bend=0;idea='Hillside worksite with a cut-and-cover shell';terrain='East hill +3 m; work floor 0 m; contour path climbs.';hills=,( @(23,46,19,68,3) );build=@(@(15,86,23,24,'Tunnel shell'),@(-36,50,21,16,'Site office'),@(-37,97,23,15,'Tool store'),@(26,52,12,13,'Equipment'));props=@(@(16,73,9,2),@(29,79,8,2));notes=@('Tunnel shell is beside the railway, not across it.','Both shell ends open; rear hillside return path.','3 m rise needs 36+ m route; retain a level bypass.')},
 @{name='Scrapyard';bend=0;idea='Salvage lanes around a central sorting clearing';terrain='West mound +1 m; east rear berm +2 m.';hills=@(@(-41,68,20,24,1),@(30,47,12,67,2));build=@(@(17,99,20,14,'Workshop'),@(-35,50,19,17,'Office'),@(-36,96,21,16,'Parts store'));props=@(@(15,53,8,5),@(28,53,8,5),@(15,67,8,5),@(28,67,8,5),@(15,84,8,5),@(28,84,8,5));notes=@('Sorting court interrupts the stacked-car lanes.','Low berms change sightlines without trapping crews.','Keep salvage piles clear of sloped return paths.')},
 @{name='Electrical Substation';bend=0;idea='Transformer grid, control hall and outer service loop';terrain='Level equipment slab; +1 m grassy west shoulder.';hills=,( @(-41,69,19,23,1) );build=@(@(15,100,23,13,'Control hall'),@(-36,50,22,17,'Service cabin'),@(-36,97,22,15,'Cable store'));props=@(@(16,53,6,8),@(29,53,6,8),@(16,69,6,8),@(29,69,6,8),@(16,85,6,8),@(29,85,6,8));notes=@('Six transformers with clear cross-aisles.','Two fence openings aligned with return routes.','No new electrical damage or switch puzzle implied.')},
 @{name='Quarry';bend=0;idea='Shallow excavation, stepped rock shelves and conveyor';terrain='East pit -2 m, shelf -1 m, outer rim +1 m.';hills=,( @(12,48,30,66,-2) );build=@(@(15,100,20,13,'Conveyor shed'),@(-36,50,21,16,'Office'),@(-37,96,23,17,'Equipment'));props=@(@(23,89,15,3),@(29,68,8,12));notes=@('Pit is dry; benches provide clear height changes.','Two graded access paths: each 24+ m to pit floor.','No forced jump: return remains possible with coal.')}
)
function Brush($c){[Drawing.SolidBrush]::new([Drawing.ColorTranslator]::FromHtml($c))}
function Pen($c,$w=2){[Drawing.Pen]::new([Drawing.ColorTranslator]::FromHtml($c),$w)}
$font=[Drawing.Font]::new('Arial',15);$small=[Drawing.Font]::new('Arial',12);$title=[Drawing.Font]::new('Arial',29,[Drawing.FontStyle]::Bold)
function Text($s,$x,$y,$f=$font,$c='#e7ede5'){$b=Brush $c;$g.DrawString($s,$f,$b,[single]$x,[single]$y);$b.Dispose()}
function P([double]$x,[double]$z){[Drawing.PointF]::new([single](70+($x+48)*8),[single](145+(160-$z)*8))}
function Line($x,$z,$xx,$zz,$c='#a9b6a9',$w=2){$p=Pen $c $w;$g.DrawLine($p,(P $x $z),(P $xx $zz));$p.Dispose()}
function Box($x,$z,$w,$d,$c){$p=P $x ($z+$d);$b=Brush $c;$g.FillRectangle($b,$p.X,$p.Y,[single]($w*8),[single]($d*8));$b.Dispose()}
function Oval($x,$z,$w,$d,$c){$p=P $x ($z+$d);$b=Brush $c;$g.FillEllipse($b,$p.X,$p.Y,[single]($w*8),[single]($d*8));$b.Dispose()}
function Label($s,$x,$z){$p=P $x $z;Text $s $p.X $p.Y $small}
function Path($pts){for($i=1;$i -lt $pts.Count;$i++){Line $pts[$i-1][0] $pts[$i-1][1] $pts[$i][0] $pts[$i][1] '#bda97d' 24}}
$files=@();$md=@('# Remaining scavenging blocks - top-down studies','','November 4, 2026. Drawing proposals only. User requested elevation changes so some blocks feel hilly. Terrain direction is authorized; exact heights, footprints and slopes remain proposed. No Unity assets changed.','','Excludes the existing Depot, Motel and Campground studies. Passenger Station is a separate scavenging block from StartingStation.','','## Common contract','','96 x 160 m blocks; encounter Z=45..115. Preserve current track samples, entry/exit and stop. Proposed 6 m flat perimeter with height and slope blended to zero before the strip; flat railway and quiet approach/departure buffers. Building pads remain level. Paths nominally 3 m wide, targeting slopes no steeper than 1:12. Contour bands are schematic, not surveyed contours. Each detailed sheet includes a full-block plan and an exaggerated terrain section.','','Six coal per variation, three per side; placements deferred to interior review. Retain gold counts, stable IDs and current east-side enemy coverage. Hills and interiors require offline crawlgraph and lighting rebakes; no runtime baking. Physical slope, drainage, loose-item recovery, low-eye sightlines and headset traversal remain unverified.','','[Overview](../output/imagegen/remaining-layouts-v1/overview.png) | [Editable drawing source](../output/imagegen/draw-remaining-layouts-v1.ps1)','','## Sheets','')
foreach($plan in $plans){
 $bmp=[Drawing.Bitmap]::new(1440,1540);$g=[Drawing.Graphics]::FromImage($bmp);$g.SmoothingMode='AntiAlias';$g.Clear([Drawing.ColorTranslator]::FromHtml('#14252b'))
 Text $plan.name.ToUpper() 65 30 $title;Text $plan.idea 68 86
 Box -48 0 96 160 '#35483e';Box -42 6 84 148 '#435843'
 foreach($h in $plan.hills){Oval $h[0] $h[1] $h[2] $h[3] '#61744e';Oval ($h[0]+2) ($h[1]+4) ($h[2]-4) ($h[3]-8) '#7b885b';Oval ($h[0]+4) ($h[1]+8) ($h[2]-8) ($h[3]-16) '#91986b';if($h[4] -lt 0){Oval ($h[0]+5) ($h[1]+10) ($h[2]-10) ($h[3]-20) '#69736a'}}
 for($z=0;$z -le 160;$z+=20){Line -48 $z 48 $z '#586b59' 1;Label "$z" -54 $z}
 Box -7 6 18 148 '#536155'
 Path @(@(-3,46),@(-13,46),@(-13,80),@(-13,115),@(4,115),@(12,115),@(12,80),@(12,46),@(-3,46))
 $cross=if($plan.name -eq 'Container Yard'){93}else{80}
 Path @(@(-13,80),@(-27,80));Path @(@(12,$cross),@(25,$cross),@(39,$cross),@(39,96),@(39,115),@(12,115));Path @(@(39,$cross),@(39,46),@(12,46))
 # Building pads and footprint numbers; portals on railway-facing side and rear.
 $n=1
 foreach($b in $plan.build){Box ($b[0]-1) ($b[1]-1) ($b[2]+2) ($b[3]+2) '#657067';Box $b[0] $b[1] $b[2] $b[3] '#a28c69';$xx=$b[0]+$b[2]/2;$zz=$b[1]+$b[3]/2;Label "$n" ($xx-1) ($zz+1);$door=if($b[0] -gt 0){$b[0]}else{$b[0]+$b[2]};Line $door ($zz-1.5) $door ($zz+1.5) '#efd79d' 5;Line ($xx-1.5) ($b[1]+$b[3]) ($xx+1.5) ($b[1]+$b[3]) '#efd79d' 5;Text ("$n  "+$b[4]+"  /  "+$b[2]+' x '+$b[3]+' m') 880 (190+$n*35);$n++}
 foreach($o in $plan.props){if($plan.name -eq 'Farm Silos' -and $o[2] -eq 9){Oval $o[0] $o[1] $o[2] $o[3] '#879a9b'}else{Box $o[0] $o[1] $o[2] $o[3] '#879a9b'}}
 for($z=0;$z -lt 160;$z+=2){$x=$plan.bend*[Math]::Pow([Math]::Sin([Math]::PI*$z/160),2);$nx=$plan.bend*[Math]::Pow([Math]::Sin([Math]::PI*($z+2)/160),2);Line ($x-.8) $z ($nx-.8) ($z+2) '#e0e3db' 2;Line ($x+.8) $z ($nx+.8) ($z+2) '#e0e3db' 2}
 Box ($plan.bend-1.5) 75 3 10 '#e0c572';Label 'STOP' ($plan.bend-3) 91
 Label 'LEVEL EXIT BUFFER' -22 143;Label 'LEVEL ENTRY BUFFER' -22 21
 Text '+Z / EXIT' 680 115 $small;Text '96 m x 160 m | grid 20 m | north is diagram +Z' 70 1440 $small
 Text '01 / FOOTPRINTS' 880 170
 Text '02 / TERRAIN' 880 420
 $rect=[Drawing.RectangleF]::new(880,460,500,100);$br=Brush '#b5c6bf';$g.DrawString($plan.terrain,$font,$br,$rect)
 Text '03 / ROUTES + CHARACTER' 880 585
 $yy=625;foreach($note in $plan.notes){$rect=[Drawing.RectangleF]::new(880,$yy,490,78);$g.DrawString($note,$font,$br,$rect);$yy+=85}
 Text 'SECTION / schematic, height exaggerated' 880 925 $small
 $west=0;$east=0;foreach($h in $plan.hills){if($h[0] -lt 0){$west=$h[4]}else{$east=$h[4]}}
 $heights=if($plan.name -eq 'Quarry'){@(0,0,0,0,0,0,-1,-2,-1,1,0,0)}else{@(0,0,0,$west,0,0,0,0,($east/2),$east,0,0)}
 $peak=($plan.hills|ForEach-Object{[Math]::Abs($_[4])}|Measure-Object -Maximum).Maximum
 $pen=Pen '#b6c98b' 3
 for($i=1;$i -lt $heights.Count;$i++){$g.DrawLine($pen,[single](890+($i-1)*43),[single](1050-$heights[$i-1]*20),[single](890+$i*43),[single](1050-$heights[$i]*20))};$pen.Dispose()
 Text 'flat edge     west       rail       east     flat edge' 880 1100 $small
 if($plan.name -eq 'Quarry'){$peak=3}
 Text ('Relief: '+$peak+' m | level pads cut into slopes') 880 1140 $small
 Text 'KEY' 880 1210
 Text 'Tan: buildings     Grey: equipment / platforms' 880 1250 $small
 Text 'Sand paths: proposed 3 m walking routes' 880 1280 $small
 Text 'Green bands: hills / benches, indicative only' 880 1310 $small
 Text 'Yellow: stopped train / doorway positions' 880 1340 $small
 Text '6 m flat perimeter; blend to zero height AND slope.' 880 1415 $small
 Text 'PROPOSAL  /  04 NOV 2026  /  NOT UNITY GEOMETRY' 880 1470 $small
 $slug=$plan.name.ToLower().Replace(' ','-');$file=Join-Path $folder ($slug+'.png');$bmp.Save($file,[Drawing.Imaging.ImageFormat]::Png);$files+= $file;$g.Dispose();$bmp.Dispose();$br.Dispose()
 $md+=@(('### '+$plan.name),'',('[Top-down sheet](../output/imagegen/remaining-layouts-v1/'+$slug+'.png)'),'', $plan.idea+'. '+$plan.terrain,'',($plan.notes -join ' '),'')
}
# Large contact sheet preserves readable titles; individual drawings carry detail.
$sheet=[Drawing.Bitmap]::new(2160,2310);$sg=[Drawing.Graphics]::FromImage($sheet);$sg.Clear([Drawing.ColorTranslator]::FromHtml('#14252b'));$sg.InterpolationMode='HighQualityBicubic'
for($i=0;$i -lt $files.Count;$i++){$im=[Drawing.Image]::FromFile($files[$i]);$sg.DrawImage($im,($i%3)*720,[Math]::Floor($i/3)*770,720,770);$im.Dispose()}
$sheet.Save((Join-Path $folder 'overview.png'),[Drawing.Imaging.ImageFormat]::Png);$sg.Dispose();$sheet.Dispose()
$md | Set-Content (Join-Path $PSScriptRoot '../../docs/remaining-block-layouts.md') -Encoding utf8
Write-Output ('Created '+$files.Count+' sheets, overview and design notes.')



