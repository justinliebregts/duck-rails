# Lakeside Campground

## Ferns, campfires and raised trails | November 26, 2026

The ferns are 75% of their previous size, shrunk about their buried crowns so every clump keeps its position. The rustle volumes shrank with them. Tall fronds now top out near 1.85 m, so the older sightline and head-clearance measurements below no longer describe the passage. `FERN_SCALE` in the Blender script sets the size.

Each campfire effect is at twice its previous size, with the particle systems set to follow hierarchy scale. The baked light sits 1.3 m above the fire bed, clear of the crossed logs that used to enclose it, with a warmer colour, intensity 16 and a 12 m range. The existing looping 3D crackle at each fire is twice as loud (0.26–0.30) and its priority moved from 220 to 200, ahead of the 210 ambience beds when voices run short.

A 24 cm earth bank runs from every outer trail edge down into the soil. The 1,706 outline edges add no colliders. Blender confirmed every sampled bank foot is below the terrain. Lighting was rebaked and the campground geometry checks passed. Collision geometry is unchanged, so the crawlgraph was kept. Headset review is queued for playtest. [Fire at night](validation/campground-polish/fire-at-night.jpg) · [Raised trails](validation/campground-polish/raised-trails.jpg)

## Ambient sound and cabin reverb

Outdoor crickets give way to cicadas by the pond and dock and nighttime leaves in the southwest woods. Each of the three campfires has a positional crackle with a 14 m range. The seven timber buildings have room-sized reverb with a 0.6 m edge fade. The bunk and north cabins have an attic bed and occasional timber creaks. Reception, the mess cabin and the supply cabin have intermittent creaks with quiet stretches. The washrooms have a slow leak and drips. The boathouse mixes drips and timber creaks.

The saved room decay times range from 0.44 s in the bunk cabin to 0.90 s in the washrooms. Edit Mode checks covered 126 indoor points, 56 points outside the walls and doorways, and 161 railway points. All eleven sound events resolved to playable clips and all three fire loops matched a burning fire pit. Doorway transitions and the headphone mix are queued for playtest.

## Lookout and interior loot additions | November 25, 2026

Eleven additional candidates bring the loot pools to 135 coal and 74 gold. On the central lookout, coal sits on the upper gallery and gold sits on the third landing and lake-facing observation deck. Five candidates occupy separate building interiors and three sit on tent groundsheets under canvas. Each addition passed item-footprint support and overlap checks. The nearest existing or new marker to each addition is at least five metres away in 3D, including ground-level markers beneath the lookout. Across the full block, no marker has two neighbours within four horizontal metres.

## Pond and fern spawn spacing | November 25, 2026

Coal, gold and enemy candidates now share a six-metre horizontal spacing check for Lakeside. The closest pair on the saved block measures 6.01 m across all 228 markers. Loot inside the actual fern foliage increased from 14 to 26 candidates. Loot on dry land within eight metres of the pond shoreline increased from 18 to 28. Cabin furnishings, the dock and the lookout still offer individual discoveries. The 128 coal, 70 gold and 30 enemy markers retain their native prefab connections.

## Ground, contact audio and scavenging | November 25, 2026

The ground renderer and walking collider share a 12,378-triangle mesh with 6,225 vertices. The old metre grid used 30,720 triangles and 61,440 vertices. The terrain builder merges coplanar cells within the same material and stitches the edges where the resolution changes. Unity raycasts at the centres of all 30,720 original triangles found a maximum height difference of 0.00000179 m. The lake, gully, rail corridor and cabin approaches passed the campground geometry checks. The new crawlgraph has 25,089 nodes and the lighting export has 666 valid renderer bindings across two lightmaps.

All 349 solid Environment colliders have contact audio. The ground samples a block-local material map at 16 samples per metre so grass, gravel, wooden sleepers and steel rails sound different even though they share one walking collider. The lake uses its Water volume. The 316 authored fern crowns use the farm corn sound and movement cadence through 35 spatial volumes.

Added 110 coal and 64 gold candidates across cabin interiors and furnishings, the lookout, dock, canoe beach, campsites, bridge, overlooks and fern patches. Twelve perimeter enemy alternatives complete the quadrant coverage. The saved pools contain 128 available coal, 70 gold and 27 distinct enemy positions. All eight crew sizes passed 100 seeds each for exact resource counts, unique selections and the 4/8/12 enemy quotas with at least 15 m of horizontal spacing. Eight players select 93 coal and 52 gold per visit. Listening, loot reach, traversal and headset frame time are queued for playtest.

## Fallen lantern lighting

Each of the four fallen lanterns has a warm baked point light above its housing. The point light uses 22% of the existing spotlight intensity and a 4.5 m range to lift the floor and nearby furniture around the beam. The four original spotlights still provide the directional light. [Cabin before and after](validation/campground-lanterns/cabin-comparison.jpg)

## Woodland and campsite detail pass

Ten small fern clumps occupy the three marked clearings near the entrance, between the lakeside cabin and railway, and along the northern approach. Each fern has five broad fronds. The full block now has 25,576 fern triangles, down from 48,769 before the extra clumps. In the original southwest passage the median sightline shortened from 3.50 m to 2.49 m across the same 896 rays. Sixteen mature pines with flared trunks and taller, broader crowns are mixed into the existing woods. [Whole block](validation/campground-woodland/whole-block.jpg) · [Fern passage](validation/campground-woodland/fern-passage.jpg) · [Sightline measurements](validation/campground-woodland/fern-sightlines.txt)

Both beach canoes and both rack canoes have fitted seat ends and closed stems with a wooden inner face. The southwest tent folds under the fallen trunk with visible canvas on both sides. Nine partly buried rocks of varied sizes replace the four repeated overlook rocks. The marked northeast chair and cooler, southeast picnic table and chair, and southeast path bin have been removed. [Canoe interior](validation/campground-woodland/canoe-interior.jpg) · [Beach and rack](validation/campground-woodland/canoe-beach.jpg) · [Collapsed tent](validation/campground-woodland/fallen-tree-tent.jpg) · [Overlook](validation/campground-woodland/overlook-rocks.jpg)

Unity Edit Mode found no overlapping coplanar faces on the six lettered signs or two crossbucks. All four canoes passed 192 stem checks and 168 paint samples over the seat and crossbar ends. The tent passed 442 upper and lower face checks and 21 trunk-contact samples. The regenerated graph has 25,008 nodes and all 42 spawn origins cleared the solid scenery. The baked prefab has 593 valid bindings across two lightmaps. Headset appearance, foliage frame time and sign readability from the train are queued for playtest. [Signs](validation/campground-woodland/signs.txt) · [Scenery checks](validation/campground-woodland/scenery.txt) · [Bake and clearance](validation/campground-woodland/bake-and-clearance.txt)

## Fern passage and grounded props

The southwest woods have a winding passage through mixed-height ferns. Broad, lobed fronds emerge across each crown, with lower leaves splaying outward and taller leaves breaking the view ahead. Both sides of each leaf render from the shared Quackville-derived atlas. The fern meshes total 48,769 triangles. In Edit Mode, 594 of 896 horizontal sightlines from two eye heights met foliage within 6 m. The median was 3.50 m. At the 28 sampled positions the center of the passage cleared the foliage by at least 18 cm in the tested directions, and a 64 cm wide body cleared the pine trunks. [Fern passage](validation/campground-grounding/fern-passage.jpg) · [Sightline measurements](validation/campground-grounding/fern-sightlines.txt)

The boathouse paddle rack mounts between the windows on the west wall. The marked trail now bends toward the canoe beach at Z=106 m. The two outer dock pilings extend 40 cm into the lakebed, with their caps at the original height. The four other capped pilings also meet the bed. All eight lantern bodies touch their supporting floor or ground, and four solar stakes penetrate the bank. The support pass also seated the camper wheels, northeast picnic table, fallen trunk, washroom mirrors, shelf crates, canoes and fire beds. [Paddle rack](validation/campground-grounding/paddle-rack.jpg) · [Edit Mode support checks](validation/campground-grounding/geometry.txt)

## Lookout tower and tent clearings

A timber lookout stands east of the railway opposite the lake. Its 2 m wide ramp wraps around all four sides, with level corner landings, wooden guardrails and a lake-facing deck 5 m above the entrance. The two dead-end spurs south of the signed crossing are gone. Farther south the lakeside path ends near the fire and the reception spur toward the rails is gone. Two wordless crossbucks face approaching drivers. The camper warning signs stand on the shoulders with their posts behind the boards. The TRAIL and OVERLOOK signs have been removed. [Lookout](validation/campground-round2/lookout.jpg) · [Whole block](validation/campground-round2/whole-block.jpg)

Three tent groups each share a central fire. The southeast group occupies the clearing between the track and the cabins. Each of the nine pitched tents has interior canvas and an entrance facing its fire. The damaged tent under the fallen tree remains in the southwest. Four solar stakes sit on dry banks, at least 0.51 m above lake level. Eight ground lanterns light individual camp features. [Southeast clearing](validation/campground-round2/southeast-pitches.jpg) · [Baked firelight](validation/campground-round2/southeast-at-night.jpg)

The gables on all seven buildings meet both the wall plate and the sloping roof skin. Pine crowns clear the cabin roofs and porches. The southwest ferns have broader, more widely spaced leaves spread across roughly 40 × 42 m of woodland around the camp clearing. Their imported meshes contain 35,072 triangles, down from 71,048. The scenery uses pixelated Quackville tiles in the shared Lakeside atlas and matching terrain and building textures. [Roof joint](validation/campground-round2/roof-joint.jpg) · [Ferns at player height](validation/campground-round2/fern-trail.jpg)

Unity Edit Mode passed 2,661 geometry checks, including 714 rays across the wall and roof joints. A separate check sampled the lookout floor and body clearance at 547 points. The rebuilt navigation graph has 24,194 nodes. All 42 existing spawn positions resolve to the main connected surface. A separate 60-node surface sits above reception. Lighting exported 591 valid renderer bindings across two lightmaps and 587 probes. Quest traversal, sightlines from a moving train and two-client play are queued for playtest. [Roof, tent and shore checks](validation/campground-round2/edit-mode.txt) · [Ramp checks](validation/campground-round2/lookout-checks.txt) · [Bake and clearances](validation/campground-round2/bake-and-clearance.txt) · [Playtest queue](playtest-queue.md#canyon-lakeside-campground-and-nuclear-power-plant--queued-for-playtest)

## November 25 campground refinement

One Pine Lake entrance sign stands at the south end. A two-rail timber fence follows each outer side at X ±47 m. Ballast follows the saved track curve, and two camper warning signs face the lake and cabin approaches to the central trail crossing. The old trackside posts are gone. The gravel trails share a continuous surface at junctions and follow the terrain triangles 6.5 cm above the ground. [Whole block](validation/campground-finish/whole-block.jpg)

The 101 pines have rotated, irregular crowns with closed undersides. Slender trees reaching roughly twice the common height break up the skyline. A walk-through fern patch separates the fallen-tree camp from the lake, with a winding gap and an open gravel route alongside it. Farm-style horsetail grows in patches at the waterline. Partly buried boulders and smaller stones break up the bank. Two open canoes lie on the beach and two sit on a timber rack. [Canoe beach](validation/campground-finish/canoe-beach.jpg) · [Duck-height ferns](validation/campground-finish/fern-trail.jpg)

All seven building floors sit 14 cm above the original pad. The walls have framed through-openings and selected pairs of weathered boards. Reception has a check-in counter and cubbies. The cabins have bunks, a dining nook or luggage, the washroom has basins, the supply cabin has shelves and crates, and the boathouse has paddles and a repair bench. [Reception interior](validation/campground-finish/reception-interior.jpg)

Four fire pits have looping flame and ember effects. Four upright lanterns and four dropped lanterns light the camp and interiors. Four solar stakes illuminate short stretches near the lake. Static illumination comes from the block lightmaps and probes. [Fire pit](validation/campground-finish/fire-at-night.jpg) · [Dropped lantern](validation/campground-finish/lantern-at-night.jpg) · [Lakeshore at night](validation/campground-finish/shore-at-night.jpg)

Unity Edit Mode passed 2,071 geometry checks. All 7,412 imported gravel vertices cleared the terrain by 6.5 cm, all 56 window apertures passed ray checks, and all 42 existing spawn origins cleared the new solid scenery. The rebuilt crawlgraph has one component with 24,101 nodes and 218,000 edges. The saved prefab has 613 valid renderer bindings across two color lightmaps and 587 baked probes. Quest traversal, night visibility, frame time and two-client acceptance are queued for playtest. The saved block remains outside normal rotation with 18 coal, 6 gold and 18 enemy candidates. [Edit Mode measurements](validation/campground-finish/refinement.txt) · [Route checks](validation/campground-finish/geometry-checks.txt) · [Playtest queue](playtest-queue.md)

To rebuild the art, run Blender in background mode from the repository root with `--python ArtSource/CampgroundFinish/build_finish.py`. The model uses the saved woodland positions, route curves and terrain heights beside that script. In Duck Rails Edit Mode, run **Authoring > Campground > Install approved Pine Lake exterior**, then bake the campground crawlgraph and lighting from its bake scene. [Blender authoring source](../ArtSource/CampgroundFinish/build_finish.py) · [Unity installer](../Assets/Editor/RailCampgroundFinish.cs)

## Earlier Pine Lake exterior

The finished Unity block follows the approved full-block render. On the south approach, the entrance sign leads to reception, the southwest tent clearing and a camper with a striped awning. In the middle, a timber dock projects into the irregular lake opposite a loop of terraced cabins. At the north end, the trail crosses the gully footbridge and branches toward the woodland pitches, the canoe beach and the rocky overlook. All seven buildings have standing-seam roofs, chimneys and warm windows. Five porches and three ember beds supply the additional baked light. [Approved render](../output/full-block-finished-reviews/lakeside-campground.png)

[Full-block Unity view](../output/campground-finish/whole-block.png) · [Overhead plan](../output/campground-finish/site-plan.png) · [South arrival](../output/campground-finish/arrival.png) · [Cabin terraces](../output/campground-finish/cabin-terraces.png) · [North canoe beach](../output/campground-finish/canoe-beach.png)

The prefab retains its original sculpted ground, open interiors, track and spawn positions. The finished model adds 116 spatially grouped mesh sections on a shared atlas. Seventy sections supply collision on trunks, campsite objects, roofs, porches and other solid details. The shell has eleven campground-local Quackville materials. The water and small shoreline brush stay traversable. The terrain perimeter stays flat, and the maximum sampled path grade is 0.136 metres of rise per metre of travel.

Unity 6000.3.24f1 exported 917 renderer bindings across two color lightmaps, 19 baked fixtures and 587 probes. The rebuilt crawlgraph contains 23,377 nodes and 214,370 edges. The main component spans the block and serves all 42 spawn candidates. A separate 16-node surface sits on top of the northern canoe rack, 10.17 m from the nearest candidate. The saved main-project prefab resolved all 116 finish meshes and all 42 existing spawn candidates. All 2,067 geometry checks passed. A 0.16 m sphere at each spawn origin cleared all new solid scenery. Nighttime legibility, VR prop clearance, item recovery along the shore and multiplayer traversal are queued for playtest. [Geometry checks](validation/campground-finish/geometry-checks.txt) · [Spawn clearance](validation/campground-finish/spawn-clearance.txt) · [Canoe rack connectivity](validation/campground-finish/graph-components.txt) · [Graph and bake](validation/campground-finish/edit-mode.txt) · [Main-project asset check](validation/campground-finish/main-editor.txt)

The Blender authoring source rebuilds the FBX and textures from the captured terrain and the production court pine. Run Blender in background mode from the project root with `--python ArtSource/CampgroundFinish/build_finish.py`. The Unity Campground authoring menu installs the finish on the original shell, checks geometry and captures the five daylight review views. The review capture uses an isolated Unity prefab preview. The block bake scene supplies the shipping lightmaps and probes. [Blender source](../ArtSource/CampgroundFinish/build_finish.py) · [Unity installer](../Assets/Editor/RailCampgroundFinish.cs)

## Layout history

November 4, 2026. The user selected the campground next, explicitly requesting a lake, terrain variation and flat block edges. The revised layout is now installed as a Unity greybox with rough Quackville materials. The original drawing proposals are retained below; implementation differences and validation follow.

Implementation authorization, November 4: the user approved greyboxing this study, then clarified that the surface pass should use rough Quackville materials, matching the depot/station workflow. The implementation is owned by `Assets/Editor/RailCampgroundGreybox.cs`; current validation evidence is recorded below as it completes.

[Site plan and terrain section](../output/imagegen/campground-layout-v1/site-plan.png). [Editable drawing source](../output/imagegen/draw-campground-layout-v1.ps1).

## Expanded layout proposal — November 4

**Latest drawing:** [Expanded top-down plan V2](../output/imagegen/campground-layout-v2/expanded-site-plan.png), [editable source](../output/imagegen/draw-campground-layout-v2.ps1). The user approved this plan for 3D implementation with rough textures. It is now installed, with the physical refinements below.

After reviewing the greybox, the user requested more trees and additional campground features, then requested a top-down proposal. They explicitly permitted features to extend into the sections between blocks instead of confining everything to the middle third. For this drawing, scenery and optional exploration occupy approximately Z=15..145 of the existing 160 m block, using the former approach/departure buffers without extending geometry into another block's authored footprint. Flat six-metre perimeter connections and the level railway remain. The former empty-buffer restriction no longer applies to campground scenery; expanding enemy/loot coverage or changing encounter pacing is not implied by this drawing.

The numbered proposals are clustered woodland throughout; three campsite clearings (southwest tents, southeast camper and northeast woodland tents); north-shore canoe beach/rack; a low rocky overlook beyond the lake; a shallow gully and footbridge below the upper cabin with a walk-around route; entrance sign/map/numbered posts/bins; and a fallen trunk across one southwest tent. Trails connect the arrival, central stop and departure areas, with sightline gaps toward the train.

Proposed relocations: reception to X=12..24, Z=22..32; washrooms to X=29..39, Z=46..54; upper cabin C3 to X=15..25, Z=117..126. The existing irregular lake, dock, boathouse, supply cabin and two other cabins retain their broad locations. Building openings, graded trail intersections, tree spacing, gully/bridge clearance and campsite prop positions need a subsequent physical pass. V2 uses approximate terrain bands rather than measured contours. No extra loot, interactive boats, falling-tree event or new water mechanic is specified.

### Installed expanded campground V2

[V2 runtime loading evidence](validation/campground-greybox/v2/live-check.txt): the authenticated private expedition reached Running/WorldReady with the campground first, 73 trees, 761 lighting bindings and the translated bridge deck at Y=1. Unity crashed during subsequent test cleanup; the cause is not established. [Post-restart verification](validation/campground-greybox/v2/restart-check.txt) confirmed the saved prefab and lighting remain intact and the test runner is idle. The temporary review preset was cleared to normal random routes; the prior preset held in SessionState was lost on restart. No additional Play Mode run was performed. The restarted Console contains a Unity PlayModeUserSettings persistence error ("You may not pass in objects that are already persistent"); this remains unresolved and is not established as the crash cause. Headset, multiplayer and performance acceptance remain pending.

The approved relocations are installed along with 73 pines in clusters, three campsite areas/five tents (one crushed), an abandoned camper, picnic tables/chairs/coolers, two static canoes and a rack, four overlook rocks, a fallen trunk with branches, entrance/map/pitch/trail signs and bins. The additional authoring code is `Assets/Editor/RailCampgroundExpansion.cs`, sharing the existing campground installer and owned assets. Rough materials use Quackville tiles throughout. Boats, camper and campsite props are static scenery, not new interactive inventory.

Terrain and trails now extend into the former approach/departure buffers; the railway and six-metre perimeter remain flat. The upper cabin and northeast woodland campsite share a +1 m terrace to avoid a sharp descent. The northwest overlook rises about 1.5 m. A three-metre-wide timber footbridge crosses a genuinely lowered dry gully; the surrounding terrain remains walkable. Trail positions and furnishings were adjusted to retain body clearance. Spawn counts/IDs and eastern enemy bounds remain unchanged; the previous upper-cabin coal position now sits on the central north trail within existing enemy bounds.

Validation: [2,055 physical checks](validation/campground-greybox/v2/geometry-checks.txt), [619 catalogue checks](validation/campground-greybox/v2/week3-checks.txt) and [lighting export checks](validation/campground-greybox/v2/lighting-checks.txt) pass. The sampled maximum route centreline grade is 0.136 (about 7.7 degrees); VR feel and full-width hand clearance remain acceptance work. The bridge deck is checked as the walking surface over the lower gully. Eleven baked lights and 761 static receiver bindings are exported; navigation was baked offline. The pre-test authorized diagnostic query returned 12 open reports after a sandbox-blocked attempt; none was resolved.

[Actual V2 overview](../output/campground-greybox/v2/overview.png), [north shore](../output/campground-greybox/v2/north-shore.png), [lake and dock](../output/campground-greybox/v2/lake-and-dock.png). These are bright editor review renders, not final gameplay atmosphere or Quest performance evidence. Earlier implementation/validation sections below describe the V1 baseline.

Shoreline revision: user requested a less oval lake. The drawing replaces the concentric ellipses with an irregular authored shoreline: a broad northern pool, a western inlet and projecting bank, and a narrower southern cove. The approximate envelope, flat perimeter, dock and dry building pads are retained. This supersedes the oval outline in the initial drawing and is reflected in the installed basin.

## Base and proposed terrain

The existing RailWeek3Builder campground is catalogue index 9: 96 x 160 m ground, entry (0,0,0), exit (0,0,160), stop (4,0,80). Its track bends east by 4 m at the midpoint; retain the sampled track and level rail corridor. The diagram uses that curve, not a straight replacement. Existing authored encounter extent is Z=45..115.

Reserve a 6 m perimeter strip on all four sides at Y=0 with zero slope. Blend the interior back to zero height and gradient before that strip; edge vertices alone are insufficient. Retain level approach/departure buffers outside the encounter. Preserve a level corridor around the existing track, approximately X=-4..10 in the encounter.

West: enclosed lake water footprint approximately X=-38..-15, Z=51..109, 23 x 58 m, water Y=-0.8, nominal deepest bed Y=-2. The basin must actually cut into the ground mesh: do not leave the original full ground collider across it. Shore slopes and shallow walk-out shelves connect back to land; no swimming, drowning or new water damage mechanic is authorized by this drawing. Detailed lake collision and item recovery need greybox review. Keep shoreline and all water inside the flat perimeter.

East: broad wooded rises with level cabin pads at +1 m and +2 m, softened back to the level rail corridor and perimeter. Main paths are nominally 3 m wide, targeting slopes no steeper than 1:12; exact graded paths must be measured in the greybox. Two metres of rise therefore requires at least 24 m of route, not a short ramp. Colored terrain bands are indicative landforms, not surveyed contour lines.

## Layout proposal

Three cabins form an eastern loop around a train-facing campfire clearing, with a diagonal return shortcut. Reception and washrooms occupy the south approach. The lake has a supply cabin at X=-16..-6, Z=53..62 and a boathouse at X=-16..-6, Z=91..103, on raised dry pads outside the water footprint. Their shore-facing sides can use small decks. A 3 m wide dock extends west from X=-12 to -24 at Z=78..81; proposed deck Y=-0.2, approached by a gentle shore ramp. Maintain a continuous shore path around the building fronts, with separate return connections north and south of the stopped train. Exact crossings depend on measured train clearance; the drawing's train rectangle is symbolic.

Keep six coal per variation, three per side, stable block and variation IDs, and existing gold counts. Marker placement follows terrain/room review. Retain current right-side enemy coverage unless separately revised; bake the adjusted terrain, paths and accessible cabin interiors offline. No runtime navigation baking. Preserve authored work against catalogue regeneration and rebake/export lighting after implementation.

## Status

The drawing review and authorized greybox/material pass are complete. Headset review of lake visibility, cabin traversal, rescue/return routes and loose-item recovery remains pending.

## Greybox and rough Quackville implementation

The authoring pass targets only `Assets/DuckRails/Blocks/lakeside-campground-v1.prefab` and its existing bake scene. It replaces the placeholder flat ground with a one-metre grid collision mesh containing the irregular basin and low terraces. The water surface is clipped to submerged terrain triangles, has no collider and uses a translucent Quackville water tile. Players/items can reach the physical bed; no swimming or drowning rule was added. Lake recovery feel remains a headset acceptance item.

Seven single-storey shells provide three raised cabins, reception, washrooms, supply cabin and boathouse. Each has two 2.6 m clear openings, a roof, simple furniture, a baked interior light and a level foundation. The dock, approach ramp, fire ring, benches and simple pines provide scale and landmarks. The north return route and diagonal shortcut are adjusted during greyboxing to grade the slopes and avoid building collision.

The rough materials are cropped copies of the existing Quackville Town atlas: grass, gravel, timber, dark roofing, concrete, weathered paint and water. Point-filtered repeating tiles preserve the pixelated style with metre-scaled UVs. Original Quackville textures/import settings and depot/station materials are not modified. The new owned materials/textures/meshes live under `Assets/DuckRails/CampgroundGreybox`. They are provisional art, not finished lake rendering or final lighting.

The six coal markers per variation remain three per side, with west coal in the supply cabin, dock and boathouse. Existing marker identities, variation arrays, gold counts, track samples and stop remain. Enemy coverage stays east of the railway. The graph bake volume uses the catalogue's shared `(8,-2,32)` origin so translated chunk assembly can place it in every route slot. The general Week 3 builder preserves the authored campground subtree.

Review commands: **Duck Rails > Authoring > Campground** provides installation, physical checks, review captures and a campground-first preset. Clear that preset using **Test Center > Restore normal random routes** after review. Captures use temporary bright fill with fog disabled, restored afterward; they do not represent final gameplay darkness.

Diagnostics before validation: the sandbox-restricted inbox request failed; authorized network retry succeeded with ten open reports. Local reports were also inspected. No existing report was resolved by this art task.

### Editor verification

- Unity compiled the authoring/check changes. [1,052 physical checks](validation/campground-greybox/geometry-checks.txt) pass: six-metre level perimeter plus an extra flat inner row, level rail/approach/departure ground, actual -2 m basin, +1/+2 m cabin pads, supported track and continuous unobstructed route centreline samples.
- Maximum measured path centreline grade is 0.090 (about 5.2 degrees). This is slightly steeper than the proposed 1:12 target; headset comfort and full-width hand clearance still need review. The greybox relocated benches and adjusted doorway/shore paths after collision checks found obstructions.
- [619 Week 3 catalogue checks](validation/campground-greybox/week3-checks.txt) pass, including stable marker references/counts, three coal per side, spawn clearance/support, navigation proximity, aligned graph origins, route serialization and graph assembly. The spawn-clearance check now uses sphere penetration so it supports non-convex terrain colliders.
- Offline navigation and lighting were rebaked. [Catalogue lighting validation](validation/campground-greybox/lighting-checks.txt) passes with eleven baked lights and 376 campground receiver bindings; the additive companion is enabled for builds.
- Actual Unity bright-review captures were visually inspected: [overview](../output/campground-greybox/overview.png), [lake/dock](../output/campground-greybox/lake-and-dock.png), [low-eye terraces](../output/campground-greybox/cabin-terraces.png). These are editor renders with temporary fill, not generated concepts or final gameplay lighting.

No multiplayer/headset traversal or Quest performance acceptance is claimed by these checks.

### Runtime loading check

[Authenticated single-editor evidence](validation/campground-greybox/live-check.txt), November 4 at 07:05 UTC: private expedition reached Running/WorldReady with campground first at Z=150. The complete route reported ready, all 376 campground lighting bindings were valid, and a runtime ray reached the translated lake bed at Y=-2. Play was stopped and the prior route preset restored afterward.

This was not an error-free headset run. The editor logged MRUK global-context/OpenXR initialization errors, an unavailable OVR display-frequency message, and an OpenXR teardown NullReferenceException when stopping. The scene nevertheless reached the verified ready state. These XR/editor issues were not investigated or resolved by the campground art pass; no headset acceptance is inferred.
