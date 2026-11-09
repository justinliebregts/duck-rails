# Nuclear power plant — layout and implementation

## Installed rough blockout — November 5

User approved V2 for 3D and requested closer barrel spacing and stacked drums. Installed `nuclear-power-plant-v1` as a new sixteenth scavenging location, with repeatable authoring in `Assets/Editor/RailNuclearPlant.cs`. Existing prototype rebuilds preserve this additional block. V1/V2 proposal sections below are historical design context; this section records actual implementation.

- Two 22 m-diameter, 35 m-high hollow cooling shells, open undercrofts, designated east/west approaches, internal supports, crosswalks and perimeter circulation. Tower A has shallow water dressing and partial overhead fill; Tower B has a dry floor and fallen panels. Both have a clear central view up through the shell. The shell bases are open between supports, not enclosed rooms with only two door holes.
- A 28 x 36 m turbine hall with two machinery units, opposite ground-level doors, an 8 m-wide gallery at +3 m, two open stairwells and 3 m-wide stairs, green console displays and workbench. Reactor containment is an exterior landmark with a sealed base.
- **130 static waste drums:** three tight clusters containing 117 drums, four on the truck bed, six tipped behind the truck and three other loose-positioned upright drums. Cluster spacing is 1.02 m for roughly 0.84 m bodies; 45 base drums, 45 second-tier drums and 27 third-tier drums. Every barrel has a radiation mark. Truck, lowered tailgate and two small spills are scenery, without new damage or moving-physics mechanics.
- Gatehouse, employee parking, maintenance shed, cooling basin, pump hut, transformers, fences, trees and utilities fill the approach/departure thirds. Flat seams and railway clearance retained. Water is a shallow visual surface above supported floors, not a swimming or deep-basin system.
- Rough Quackville materials, 29 baked environment lights, dedicated authoring/companion scenes and probes. The normal Expedition atmosphere is unchanged. Enemy bounds cover both plant sides at Z=45..115; a 14 m central navigation exclusion keeps enemies off the railway. Three spawn variations retain six coal, six enemy candidates and two gold candidates each.

### Validation

- [3,185 physical/layout/navigation/UV assertions](validation/nuclear-plant/geometry-checks.txt): tower access/rings, open shell tops, stair support/headroom, gallery circulation, barrel stack support and aisles, flat seams, railway clearance, spawn placement and connected graph routes on each side.
- [803 catalogue checks](validation/nuclear-plant/week3-checks.txt), including the new sixteenth location, coal separation, route serialization and graph-grid alignment.
- [Lighting export validation](validation/nuclear-plant/lighting-checks.txt): 29 baked lights, 1,344 static receiver bindings and enabled companion build entry.
- [Offline Play Mode route proof](validation/nuclear-plant/runtime/runtime-proof.txt): seeds 11 and 43 passed readiness, atlas bindings, translated probe comparison, finale relocation, route replacement and complete teardown with the plant plus four existing locations. The retained log contains two completed repetitions of the two-seed workflow, 42 PASS lines per repetition (84 total), with 12 lightmaps and 2,334 probes per assembled route. This is single-editor runtime evidence, not multiplayer or headset acceptance.

Actual Unity review captures: [whole block](../output/nuclear-plant/overview.png), [dense barrel yard](../output/nuclear-plant/barrel-yard.png), [tower interior](../output/nuclear-plant/tower-interior.png), [open tower sky](../output/nuclear-plant/tower-sky.png), [turbine hall](../output/nuclear-plant/turbine-hall.png), [control gallery](../output/nuclear-plant/control-gallery.png). These use a temporary fill light and disabled fog to inspect geometry; they are not final gameplay-atmosphere or headset evidence. No capture overrides were saved to the bake scene.

The initial visual pass caught coplanar ground/interior floor overlap; interior floors are now slightly raised and rechecked. The first offline proof attempt incorrectly supplied only one catalogue entry to the five-stop selection harness; it was stopped and the setup changed to five existing locations, including the plant. That aborted attempt is not acceptance evidence. During the corrected run, a tool inspection showed an idle test object with no route; explicitly starting its coroutine produced a second completed repetition in the retained log. The proof itself needs no runtime-gameplay changes. Editor Multiplayer Play Mode persistent-settings and pooled-audio warnings appeared during test startup; no related project-wide behavior changes were made.

Headset traversal, low-eye readability, enemy pursuit/rescue, real-client behavior and Quest performance remain open. The blockout does not satisfy those milestone acceptance items.

### Reproduce / review

Open `Assets/DuckRails/Lighting/Blocks/nuclear-power-plant-v1/nuclear-power-plant-v1-bake.unity`, or include **Nuclear Power Plant** in Duck Rails Tests > Test Block Order. Authoring entry point: **Duck Rails > Authoring > Nuclear Plant > Install approved block**. After geometry edits, rerun `RailNuclearPlantChecks.Run()`, bake/export via **Lighting > All Blocks > Bake Open Block**, and validate the catalogue export. `RailNuclearPlantChecks.PrepareProof()` creates the offline assembled-route proof; then enter Play Mode. `Register()` adds the prefab to Expedition without rebuilding other blocks.

## V2 — enterable cooling towers and waste-drum yard

November 5: user requested a top-down floor plan and explicitly wants to enter the cooling towers. They also requested many oil-style barrels representing nuclear waste outdoors, possibly spilled from a truck. These requirements supersede V1's predominantly scenic tower treatment; exact layouts remain proposals.

[Top-down site and interior sheet](../output/imagegen/nuclear-power-plant-v2/floor-plan.png) | [Editable drawing](../output/imagegen/draw-nuclear-floorplan-v2.ps1)

The drawing uses deterministic scaled footprints rather than image-generation perspective. Whole site is 96 x 160 m, north/+Z up. Central railway remains at X=0, with X=-4..4 clear and 3 m walking verges outside it; flat 6 m seams. Towers are centered at (-26,67) and (-26,94), both 22 m diameter, within the middle third. East turbine hall occupies X=12..40, Z=55..91; rounded reactor shell centered (26,103), radius 10 m, remains exterior-only in this proposal and extends slightly into the departure third.

### Enterable tower interiors

Both towers have east/west 3 m access openings between base supports, a nominal 3 m perimeter walk, broad crosswalks and connected exterior return routes. Four interior support clusters sit inside the basin footprint, away from crosswalks. These are scaled-down fictional game towers, not engineering replicas.

- **A / damp:** shallow decorative basin quadrants, proposed water depth 0.1 m, dry crosswalks and perimeter route. Partial fill and water-distribution pipework remain overhead at +4.5 m or higher; damaged central fill opens a view upward.
- **B / drained:** level dry basin floor, fallen fill panels outside the main paths, mostly missing overhead fill and a clear view up the hollow shell. No pit or mandatory jump is proposed. Both exits remain accessible from the interior.

Real natural-draft wet cooling towers contain cooling fill and water-distribution equipment above a collection basin; warm moist air rises through the tower. Sources: [USGS tower description](https://www.usgs.gov/media/images/natural-draft-cooling-tower) and [NRC steam/power conversion description](https://www.nrc.gov/sites/default/files/doc_library/cdn/legacy/reading-rm/doc-collections/nuregs/staff/sr1793/initial/chapter10.pdf). The drained floors, broad routes and missing fill are deliberate abandoned-site gameplay adaptations. No operating-tower access realism or radiation mechanic is implied.

### Hall and outdoor waste scene

Turbine hall has two 6 x 8 m machinery footprints, a rail-facing west entrance and east service exit at Z=71..75, and an unobstructed cross-hall route. An 8 m-wide east gallery at +3 m has consoles against the east wall, a clear aisle and two 3 m-wide, 9 m-long stair flights with end landings. Ground and upper plans share orientation and footprints. Stair headroom, railings and low-eye visibility require 3D validation.

The southeast waste yard occupies X=11..40, Z=16..34, next to the maintenance shed at X=27..40, Z=37..49. The site plan proposes 42 upright drums plus seven scattered drums; counts are drawing defaults. A parked truck, barrels fallen from its rear, some tipped/split barrels and small stylized spills establish the requested accident scene. Use radiation markings on barrel sides, dull yellow/rusted steel and restrained spill color. Keep a broad accessible yard route; spills are scenery rather than new damage or cleanup mechanics. The enlarged truck inset is schematic, not a second exact barrel-placement plan.

Gatehouse, parking, cooling basin, pump hut, transformer yard, fences and vegetation continue through the outer thirds. Reactor shell is exterior-only for this revision; no interior is promised without a further layout.

### Validation and next step

PNG rendered and visually inspected for legible labels, matching north-up plans, entrances, clear railway and full-block distribution. No Unity edits or runtime validation. User review of this sheet precedes rough Quackville blockout. Before implementation reconcile east/west enemy coverage, bake authored navigation and lighting, check all stairs/openings and ensure loose items can be recovered from basins. Headset, multiplayer and performance acceptance remain separate.

## V1 — historical concept

November 5, 2026. User requested a nuclear power plant block concept. This is an unapproved layout and atmosphere study; no Unity assets or gameplay have changed.

[Concept sheet](../output/imagegen/nuclear-power-plant-v1/concept.png) | [Generation prompt](../output/imagegen/nuclear-power-plant-v1/prompt.txt)

## Proposed direction

A deserted plant that still appears partly alive: enormous cooling towers, faint machinery hum, amber maintenance lights and an elevated control room glowing green. Use simple faceted forms and coarse Quackville-style pixel textures. The warm train remains the obvious return landmark.

Use the standard 96 x 160 m layout as a starting envelope, with a straight central railway and the stop at the midpoint. Preserve the clear railway corridor, walking verges and flat seams. Exact dimensions and geometry await a measured top-down revision and user confirmation before rough blockout.

- Middle third, west: two cooling towers with a pipe court and paths around their bases. The towers primarily provide scale and silhouette; full-height climbing is not proposed.
- Middle third, east: turbine hall with large machinery, a broad ground-floor circulation loop and an elevated control-room gallery reached by two stairs. Provide a rail-facing entrance and a second exit connected back around the building. A separate rounded reactor containment building provides a secondary landmark.
- Approach third: gatehouse and abandoned employee parking west; maintenance shed, loading apron and service truck east.
- Departure third: cooling-water basin and pump hut west; fenced transformer yard east. Paths, broken fence openings, utility infrastructure and vegetation distribute supporting scenery toward the outer edges.

Quick scavenging sits near rail-facing maintenance spaces; a longer excursion leads through the turbine hall and gallery. Reuse established coal/equipment rules, with coal presented as railway maintenance supplies rather than plant fuel. Exact spawn placements and enemy coverage require a later layout pass; the concept does not change counts or authorize new enemy behavior.

The horror comes from obscured views behind machinery, distant silhouettes and the contrast between apparently active controls and abandoned rooms. Radiation damage, meltdown timers and power-restoration puzzles are not part of this proposal.

## Drawing limitations and next step

Generated with the built-in image-generation tool and visually inspected. Labels and major areas are readable. The overhead panel is illustrative rather than a measured orthographic drawing: tower sidewalls are visible, the towers are arranged along the railway rather than side by side, and the main structures occupy somewhat more than the strict middle third. The perspective panel also invents train details; it is not a train redesign or a Unity capture.

Review the location concept, then resolve exact footprints, middle-third fit, gallery stairs, basin recovery routes, railway clearance and enemy/navigation boundaries in a measured top-down plan before implementation. No headset, multiplayer or performance validation is claimed.
