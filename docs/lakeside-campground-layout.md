# Lakeside Campground — layout study

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
