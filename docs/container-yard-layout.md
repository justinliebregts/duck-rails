# Container Yard — approved maze and installed rough blockout

## Spawn placement review — November 8, 2026

Follow-up after positive user review: doubled the pools to **168 coal / 142 gold**, retaining all 173 existing marker transforms and all 18 enemy candidates. Added 84 coal (17 interior side-wall positions, 67 container-roof positions) and 71 gold (25 interior corners, 10 accessible container ends, 36 rooftop rewards). All new coal is on or in containers; roof candidates use horizontal container surfaces up to 6 m high. New positions passed support, four-corner footprint, spacing and overlap checks. Saved prefab passed 628 reference/seeded-selection checks. Preserved all 1,412 environment transforms and graph reference; per-run counts remain 6/2/5. User inspection of the expanded pass and natural VR reachability/settling remain pending. The initial pass below is historical.

User authorized replacing the old candidates using the manually authored Convenience Store as the reference. Replaced 42 markers with 84 coal, 71 gold and 18 enemy prefab candidates. Per-run counts remain 6/2/5; runtime sampling is unchanged.

Coal follows container wall corners, the six interiors, low pallets, crane bogies, dispatch and four low-terrace positions. Gold has 36 accessible candidates (pallets, interior corners and container-end recesses) and 35 intended climb rewards (roof ledges, upper terraces, dispatch/stacker roofs and overflow storage). These difficulty descriptions are authoring judgments, not verified VR reachability or runtime quotas. Enemy candidates cover both banks, loading/dispatch areas, interior search rooms, outer storage and one track position.

Verified in the Duck Rails Unity editor: all 173 proposed positions pass downward support and spawn-envelope overlap checks; loot also passes four-corner footprint support checks. Enemy locations are inside current bounds and within 0.8 m of baked ground navigation. Clearance-query positive control detected a known container. Saved-prefab verification passed 491 marker-reference, seeded-selection and navigation checks, including 100 seeds per type and bilateral enemies. Preserved all 1,412 environment transforms and the existing graph reference; no geometry, lighting or runtime code was edited. `git diff --check` passed.

Container Yard is left open in Prefab Mode for user inspection. Automated Scene-view captures were black and do not establish visual acceptance. Natural reachability, item settling, placement taste and headset/multiplayer acceptance remain untested. Marker names describe their location and distinguish accessible/climb gold for review.

November 5, 2026. User approved the denser V6 playground for 3D, explicitly placing the crane at the south side. V6 is installed with rough Quackville materials, superseding V5 and the earlier studies below.

[Approved V6 layout study](../output/imagegen/container-yard-v6/layout.png). The user's subsequent south-crane instruction overrides the drawing's crane position.

## Installed V6 playground

- Rebuilt the existing `Assets/DuckRails/Blocks/container-yard-v1.prefab` with 128 container modules. Retains the stable block ID, 96 × 160 m site, straight track, stop, three spawn variations and east-only enemy bounds.
- Six loose bands per side now contain double-depth clusters, varied ends, offset upper stacks and modestly rotated outer islands. Selected stacks reach four high (12 m). Ground collision occupies 46.6% of the sampled west yard and 43.1% of the east; two measured tight lanes are 3.5 m and 4.5 m wide. This replaces V5's thin, widely spaced strips.
- Six hollow ground interiors remain: four through-passages and two search containers. Independent collision-path checks reach all interiors and both outer return exits on each bank. Three fallen containers provide sideways, tilted and lodged silhouettes.
- Broad low roofs at 3 m lead to selected 6 m terraces. Six pallet approaches use 0.3 m rises: two ground approaches and one upper approach per bank. Upper containers are set back to leave supported landings; taller stacks remain physical scenery.
- The gantry is now on the **south/approach apron**, centered around Z=29, with legs at Z=22/36. Its cross-track boom underside remains 16.9 m above the railway. The southwest reach stacker remains beside the loading area. Both machines are static blockout scenery.
- Repositioned coal, gold and enemy candidates for the new interiors and terraces, preserving counts and enemy bounds. Rebaked the crawlgraph offline. Lighting has 16 baked lights and 1,070 exported renderer bindings; gameplay ambient/fog settings are unchanged.

Authoring and review commands remain those documented below. In **Duck Rails Tests > Test Block Order**, select the existing **Container Yard** entry. The editor was restored to the Expedition scene after validation.

### V6 validation and acceptance

- Unity MCP verified `X:/Work/duck-rails/Assets`; scripts compiled. **2,215 geometry/navigation/UV assertions passed**, including physical ground-route connectivity, hollow exits, roof support/headroom, upper-stack support, south crane placement, railway clearance, flat seams and loot clearance. An initial blocked upper-climb arrival was fixed by setting the upper box back, then the checks were rerun successfully.
- **757 catalogue assertions passed** and all lighting exports validated. Two offline Play Mode route-loader rounds passed **42 assertions**, covering translated lightmaps/probes, replacement and teardown.
- Evidence: `docs/validation/container-yard-v6/geometry-checks.txt`, `catalogue-checks.txt`, `lighting-exports.txt`, and `runtime/runtime-proof.txt`. Historical V5 evidence remains separate.
- Actual Unity captures: [overview](../output/container-yard-v6/overview.png), [top-down](../output/container-yard-v6/top-down.png), [maze](../output/container-yard-v6/maze.png), [interior](../output/container-yard-v6/interior.png), [roof](../output/container-yard-v6/roof.png), [south gantry](../output/container-yard-v6/gantry.png), [reach stacker](../output/container-yard-v6/reach-stacker.png). Captures use temporary brighter review rendering, not gameplay lighting.

Remaining acceptance: natural hand climbing/descent, rescue and loose-item recovery, low-eye visibility, enemy pursuit, real-client late joining/authority changes and Quest performance. The offline proof does not simulate player locomotion or multiplayer. This is a rough art blockout.

## V6 — denser playground revision

November 5 user review: the installed V5 felt too rigid, with rows too far apart. The user liked the concept's double-depth sections, containers offset from rows and playground quality, and supplied four The Last of Us Part II reference screenshots showing thick clusters, irregular silhouettes, tighter passages and climbable stacking. The user subsequently specified the crane's south-side placement and approved implementation with “build it please.”

[V6 revised layout and traversal studies](../output/imagegen/container-yard-v6/layout.png). Generated with built-in image_gen using V5 and three supplied screenshots as spatial references; [prompt](../output/imagegen/container-yard-v6/prompt.txt). The references inform composition and container placement, not copied game assets or a change to Duck Rails' rough Quackville style.

Drawing-stage proposals (measured implementation recorded above):

- Replace the twelve evenly spaced, mostly single-depth strips with asymmetric thick clusters arranged across roughly six loose bands per side. Retain the full-width yard and central emphasis, but avoid mirrored banks, uniform gaps and long straight sightlines.
- Double up many ground containers side by side, producing approximately 6 m-deep masses and broad roofs. Mix in short deeper pockets, 1–3 m lengthwise offsets, 1–2 m forward/back offsets, perpendicular noses and a few modestly rotated boxes.
- Aim for mostly 3–5 m turning lanes rather than the installed approximately 8 m inter-row gaps; any roughly 2.5 m pinch remains subject to VR hand-clearance and rescue review. Keep reconnecting ground routes and multiple train-return exits.
- Step upper boxes back onto supported lower stacks to leave roof landings. Connect one-high roofs to selected two-high terraces using pallet approaches and supported tilted containers; retain occasional four-high silhouettes. No elaborate catwalk or stair-tower network is needed.
- Retain six ground interiors; propose one sideways and two tilted/lodged containers, with exact count/poses determined at measured authoring. Keep the crane, reach stacker, loading areas, straight track and flat seams. Higher silhouette masses remain physical scenery.
- Keep the current east enemy bounds and spawn counts unless explicitly revised; position required east-side routes/loot inside them. Reposition markers, then rebake navigation and lighting after geometry changes.

Visual review: the sheet demonstrates denser double-depth masses, uneven ends, asymmetric paths, broad roof landings and retained machinery. Its roof arrows and ground routes are schematic; it still uses an illustrative elevated plan rather than a reliable measured projection. Small courts, guarded edges and scattered props in the illustration are not fixed construction requirements. Container support, upper-route access, returning with items, low-eye visibility and physical clearances require measured implementation and headset review.

**V6 is now installed and validated as recorded above.** The drawing review preceded the approved rebuild. The following V5 record is retained as history.

## Installed rough blockout

**Historical V5 record, superseded by V6 above.** Counts, north-crane placement and evidence in this section describe the earlier installation.

- Updated the existing `Assets/DuckRails/Blocks/container-yard-v1.prefab`, retaining its stable ID, 96 × 160 m site, straight railway, stop, three spawn variations and east-only enemy bounds. It remains the existing Container Yard catalogue entry; normal runs still select five distinct stops.
- Twelve staggered rows: six per side at Z=52,63,74,85,96,107, spanning X=-42..-10 and X=10..42. Most stacks are two or three high; four corner stacks reach 12 m. The yard contains 109 container modules including outer storage, endcaps, fallen pieces and the reach stacker's held scenery container.
- Six hollow ground interiors: four two-ended passages and two one-ended search containers. Shells use real floor/wall/roof collision with exterior landing gaps and static folded doors. Shared corrugated meshes keep hand collision smooth.
- Each side has a continuous 20 m low roof route at Y=3, with two pallet approaches (0.3 m rises); four approaches total. Taller stacks remain ordinary physical scenery, without invisible climbing restrictions. Two static fallen containers provide sideways and tilted silhouettes.
- North gantry: four east-side legs, portal braces, operator cabin, trolley and parked spreader. Mast/cable structure rises to about 22 m; cross-track boom underside is 16.9 m. Southwest reach stacker: wheels, cab, counterweight, telescopic boom, hydraulic cylinders and a supported scenery container. Both machines are static, with supports/cargo outside the railway reservation.
- Supporting storage, pallets, dispatch booth, fence fragments and weeds occupy the approach/departure areas and side edges. Rough Quackville material derivatives cover containers, machinery and yard surfaces. Sixteen baked lights, conventional probes and 933 exported renderer bindings; gameplay ambient/fog settings are unchanged.
- Each variation retains six coal (three per side), six enemy candidates and two gold markers. Coal includes ground interiors and the low roof routes; gold sits in the one-ended containers. Existing east-side enemy bounds remain X=9..37, Y=-2..10, Z=45..115; the west side remains player exploration.

Authoring: `Assets/Editor/RailContainerYard.cs`, menu **Duck Rails > Authoring > Container Yard > Install approved yard**. This explicitly rebuilds the yard; ordinary prefab refinement followed by graph/lighting rebakes is appropriate for manual edits. The general prototype builder now preserves this authored block. Geometry checks/captures and an offline route proof are in `Assets/Editor/RailContainerYardChecks.cs`.

Review through **Duck Rails Tests > Test Block Order** with the existing **Container Yard** prefab. Bake scene: `Assets/DuckRails/Lighting/Blocks/container-yard-v1/container-yard-v1-bake.unity`. `Assets/DuckRails/ContainerYard/RouteProof.unity` is an offline integration harness, not a gameplay entry scene. The user's pending Expedition scene edits were saved before authoring; the prior disk scene was backed up to `tmp/container-yard-backup/Expedition-before-save.unity`.

### Validation and remaining acceptance

- Unity MCP verified `X:/Work/duck-rails/Assets`; authoring and check scripts compiled successfully.
- 1,660 geometry/navigation/UV assertions passed: row count/width, actual interior exits, roof continuity, climb support/headroom, level seams, loot support/clearance, connected east ground/interior/roof graph, machine clearance and rebuild preservation. Evidence: `docs/validation/container-yard/geometry-checks.txt`.
- 757 catalogue assertions passed; all exported lighting companions validated. Evidence: `docs/validation/container-yard/catalogue-checks.txt` and `lighting-exports.txt`.
- Two offline Play Mode assembled-route rounds passed 42 assertions for real route-loader loading, translated atlas/probe binding, route replacement, finale translation and complete teardown. Evidence: `docs/validation/container-yard/runtime/runtime-proof.txt`.
- Initial checks found neighboring boxes obstructing open ends and lamp posts blocking return lanes. Added exterior landing gaps and moved posts, then rebuilt/rebaked and passed checks. Visual review used temporary brighter rendering and reopened the bake scene afterward; captures do not establish gameplay darkness.
- Actual Unity captures: [overview](../output/container-yard/overview.png), [top-down](../output/container-yard/top-down.png), [maze](../output/container-yard/maze.png), [interior](../output/container-yard/interior.png), [roof](../output/container-yard/roof.png), [gantry](../output/container-yard/gantry.png), [reach stacker](../output/container-yard/reach-stacker.png).

Remaining acceptance: natural hand climbing and descent, rescue and loose-item recovery, low-eye visibility, enemy pursuit, real-client late joining/authority changes and Quest performance. The offline proof does not simulate player locomotion or multiplayer. Machinery, perimeter dressing and texture repetition remain rough art.

## Approved V5 — wider rows and freight machinery

November 5: user requested more width and rows while retaining V3's simpler construction, supplied gantry-crane and reach-stacker references, then approved the resulting V5 sheet with “build it in 3D.” Implementation is authorized with rough Quackville materials.

Six staggered rows on each side span approximately X=-42..-10 and X=10..42, at Z=52,63,74,85,96,107. Keep broad connected alleys, six ground interiors, two fallen containers, low climbable roofs and selected four-high stacks. Supporting storage extends through both outer thirds. An east-side cantilever gantry occupies the north loading apron; a reach stacker occupies the southwest apron. Both are static scenery with train clearance preserved. The crane's overhead boom supersedes the earlier completely open-sky reservation; its supports and parked spreader stay off-track.

[V4 wider rows](../output/imagegen/container-yard-v4/layout.png) and [V5 freight loading](../output/imagegen/container-yard-v5/layout.png) were generated with built-in image_gen. Prompts are saved beside each drawing. V5 corrects V4's overly square site proportions. Rows, arrows, height labels, machinery details and depicted train are illustrative, not surveyed geometry or a request to change the gameplay train. The proposed crane is about 20 m high with at least 16 m under its cross-track boom; exact authored values and validation will be recorded below. V3 and V2 remain history.

## V3 — simplified alternative, current review proposal

Historical proposal, superseded by approved V5 above. The user liked V2's appearance but requested a simpler version because of construction complexity. [V3 simplified drawing](../output/imagegen/container-yard-v3/layout.png) proposed the following reduced layout; its narrow footprint was then rejected in favor of wider rows.

Propose two compact clusters, each with three staggered rows, a broad ground loop with zigzag branches and one search pocket. Mostly sealed repeated modules stacked two or three high, with one four-high rear landmark per side (approximately 12 m). Retain the maze feeling through offset sightlines and ground passages rather than numerous elevated connections.

- Six enterable ground containers total: two two-ended passages and one one-ended search container per side.
- One continuous low roof route per side at approximately 3 m, on adjacent container roofs, with pallet/crate approaches at both ends.
- Two static fallen containers total: one sideways, one tilted against a low stack.
- Remove upper interior rooms, five-high towers, bridges, stair towers and complicated balcony/catwalk networks from the proposal.
- Simplify outer scenery to spare closed containers, a dispatch booth, pallets, fence sections and weeds distributed toward block ends. Omit the repair workshop and forklift.

Reuse closed, hollow passage and one-ended search modules; reuse ordinary container modules for fallen poses and tall backdrops. This reduces unique geometry, interior collision work, elevated navigation connections and lighting detail. It is not a measured schedule or performance estimate. Tall backdrop stacks remain physical scenery; no new invisible climbing restriction is proposed.

The generated plan is schematic and partly oblique despite the requested overhead view. Its perimeter arrows do not fully show the intended internal zigzags; measured layout must preserve cross-passages between the three rows. Roof arrows do not prove contiguous collision or reachable climbs. Keep the main stacks centered in Z=53..107, with low transition scenery outside; preserve baseline track, seams and gameplay constraints below. Some drawing labels/roof details are illustrative, not exact module specifications.

Built-in image_gen edited V2 using the [saved V3 prompt](../output/imagegen/container-yard-v3/prompt.txt). Visually reviewed for reduced density, six visible ground interiors, two fallen containers, low roof paths and retained tall silhouette. No Unity assets changed or traversal validation claimed.

## V2 history

## Confirmed direction

The user requested a container maze with a mixture of open and closed containers, fallen containers, climbable stacks and accessible interiors, stacked high. Preserve the established block workflow: drawing approval first, then rough Quackville materials and geometry. Keep the main location concentrated in the middle third, with supporting areas toward both block ends and edges.

## Proposed arrangement

- Retain the 96 × 160 m site, X=-48..48 and Z=0..160, straight railway at X=0, suggested stop Z=80 and +Z departure. Reserve a proposed 12 m central strip for railway/boarding; no overhead container crossing.
- West and east mazes occupy approximately X=-42..-10 and X=10..42, with their densest tall stacks near Z=53..107. Some transition stacks extend into Z=45..115. Stagger perpendicular container rows to create branching alleys, short dead ends, small courts and reconnecting loops.
- Proposed modules are about 3 m wide, 3 m high and either 6 or 12 m long. This is a gameplay sizing proposal, not a claim about standard shipping dimensions. Mix sealed stacks, hollow two-ended passages and one-ended open search rooms; selected elevated containers also have accessible interiors.
- Mostly two-to-four-high stacks, with selected five-high landmarks around 15 m. Connected roof routes occupy the first three levels; local higher lookouts are optional extensions. Short bridges connect stacks within each side of the railway.
- Give each side multiple climbing approaches using supported tilted containers, pallet/crate steps and short industrial stairs. Side-overturned containers create alternate interiors and low roof routes. Fallen containers are proposed static scenery; dynamic falling or door mechanics are not requested.
- Keep a continuous ground return loop and more than one descent from the main roof route. Avoid requiring a blind drop or a long jump to retrieve coal or return to the train. Exact routes and climb spacing require measured authoring and headset review.
- Approach third: entry booth, loading apron, forklift, scattered containers and pallet piles. Departure third: overflow storage, repair bay, spare doors and smaller stacks. Broken fence sections, weeds, gravel shoulders and service paths fill the side edges. Keep seam strips flat and clear.
- Use faded industrial container colors and rough Quackville materials at implementation. Warm practical lights identify entrances and returns; the concept's bright presentation is for review and does not change gameplay atmosphere.

## Verified existing baseline and implementation constraints

Unity MCP verified `Application.dataPath` as `X:/Work/duck-rails/Assets` before inspecting `Assets/DuckRails/Blocks/container-yard-v1.prefab`. Read-only inspection confirmed stable ID `container-yard-v1`, length 160, entry (0,0,0), exit (0,0,160), stop (0,0,80), and straight track samples. Six existing open shells sit at X=19/32 and Z=58/77/96, approximately 5 m wide and 3 m high.

The existing block has three variations, each with six coal, six enemy candidates and two gold markers. Retain counts, stable ID and three coal per side when repositioning markers. Existing enemy bounds are east-side only: X=9..37, Y=-2..10, Z=45..115. The drawing does not authorize expanding enemies into the west side or raising enemy coverage above existing bounds; settle and validate any navigation change during implementation. Do not place mandatory east-side pursuit routes outside the retained bounds by accident.

After approval, author collision/openings and reachable loot first, rebake crawlgraph and lighting offline, then check route integration, railway clearance, flat seams, support/headroom, connectivity, rescue and dropped-item recovery. Natural hand climbing, low-eye readability, real-client behavior and Quest performance remain acceptance work. No runtime baking.

## Drawing review and limits

Generated with built-in image_gen; [saved generation prompt](../output/imagegen/container-yard-v2/prompt.txt). Visually reviewed for two-sided maze, open/closed interiors, fallen and tilted containers, high stacks, climbing and populated outer areas.

The sheet is illustrative, not a surveyed or collision-validated plan. In particular, it spreads tall stacks farther into the outer thirds than the proposed central emphasis: use the coordinate brief above for measured implementation. Height markers, bridges, interior cutaways and arrows are schematic. The approach gate drawn near the track must not become a railway obstruction. The repair bay and entry booth are proposed supporting scenery. Roof access, exact container count and all dimensions remain review proposals.

No production geometry, navigation, lighting, Play Mode, multiplayer or headset validation was performed for this drawing.
