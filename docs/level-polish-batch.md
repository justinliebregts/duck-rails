# Level polish batch

Status: Items 1–24 are implemented and editor-checked, including the tunnel-floor visibility, whole-bank lighting and static gold-crate corrections. All remain **queued for playtest**, not gameplay-accepted. Other levels remain outside scope.

The earlier execution request authorized items 1–16 and the combined top-down proposal. The renewed request authorizes the five collected corrections to that layout; no new route/layout alternatives were introduced. No Play Mode was entered.

Execution baseline: Unity MCP confirmed `X:/Work/duck-rails/Assets`, Edit Mode, clean Farm bake scene and no Prefab Stage. No other active project chat appeared in the November 11 app snapshot. Existing finale prefab and Farm prefab edits are preserved; baseline finale copy and hierarchy are in `output/level-polish-batch/`. The current finale includes ten repositioned warning posts, four floodlight heads, four cottages, 28 coal / 26 gold candidates and two active tower enemies (six retained markers). No whole-block builder will be run.

## Reference conventions

- North = Z+; east = X+; south = Z-; west = X-.
- Group numbered checklist items by level and location, with stable numbers across the batch.
- Each item records the problem, requested outcome, screenshot reference, preservation constraints, uncertainty, and whether it is an explicit request or a tentative idea/question. Assistant suggestions remain separately labeled and unapproved.
- Reference screenshots by feedback message and image order, plus a short description where helpful.

## Feedback checklist

### bridge-finale-v1

#### Northeast and northwest

1. [x] Remove reported z-fighting across the northeast and northwest areas.
   - Problem: User reports “a bunch of z-fighting.” Red arrows in screenshots 1–2 identify multiple wall faces, corners, and platform/retaining-wall edges; screenshot 3 marks the rail/green platform edge near the dark tunnel opening.
   - Requested outcome: Resolve the reported surface overlap/flickering at the marked areas. Explicit user feedback; no additional redesign requested.
   - Screenshot references: Feedback message 1, images 1–3:
     - Image 1: `C:/Users/Justin/AppData/Local/Temp/codex-clipboard-34768da7-0a60-4799-9e19-bcd7f7905eb5.png` — concrete walls and railed platform, multiple red arrows.
     - Image 2: `C:/Users/Justin/AppData/Local/Temp/codex-clipboard-90f68366-f35b-4b5b-b981-7fab79021b5e.png` — opposite view of concrete structures and platform edges, multiple red arrows.
     - Image 3: `C:/Users/Justin/AppData/Local/Temp/codex-clipboard-3bc507ae-6a63-4443-bc7e-a2f26389cad7.png` — green platform and rail approach at dark tunnel opening, one red arrow.
   - Preserve: Existing layout, rail alignment/clearance, traversal, and gameplay under batch-wide constraints; no item-specific preservation request added.
   - Uncertainty: Exact objects and causes uninvestigated. Individual screenshots have not been assigned specifically to northeast versus northwest; image 3's precise overlap is to be inspected during execution.
   - Status: Implemented; editor evidence and resolved details below. Gameplay/headset acceptance queued, not verified.

#### Bridge entrance

2. [x] Close the visible seams at the bridge entrance.
   - Problem: User says the entrance looks bad; screenshot shows gaps/seams around the dark entrance frame and adjoining wall.
   - Requested outcome: Close those seams. Explicit request.
   - Screenshot reference: Feedback message 2, image 1 — `C:/Users/Justin/AppData/Local/Temp/codex-clipboard-43851088-2b18-4176-a917-099866aa9118.png`.
   - Preserve: Entrance passage and railway clearance; existing gameplay and batch-wide constraints. No broader entrance redesign requested.
   - Uncertainty: Exact intersecting objects and seam extents await execution inspection.
   - Status: Implemented; editor evidence and resolved details below. Gameplay/headset acceptance queued, not verified.

#### Water beside railway and houses

3. [x] Replace the panel-like water appearance with a depression in the ground containing a water plane.
   - Problem: Water reads as blue panels laid on the ground rather than a recessed watercourse.
   - Requested outcome: Make the water “actually like a dent in the ground with a water plane. not panels.” Explicit request.
   - Screenshot reference: Feedback message 2, image 2 — `C:/Users/Justin/AppData/Local/Temp/codex-clipboard-e18675af-454c-4d4e-b89a-61b54b12051c.png` — blue strip beside the track, houses, and pink trees.
   - Preserve: Railway clearance and surrounding level context under batch-wide constraints; no relocation of houses or trees requested.
   - Uncertainty: Depth, bank shape, and exact geometry await inspection; any layout approval required by AGENTS.md must be included in the combined proposal review before that work.
   - Status: Implemented; editor evidence and resolved details below. Gameplay/headset acceptance queued, not verified.

#### Houses — roof gaps

4. [x] Fill the gaps beneath house roofs so the large holes are no longer visible.
   - Problem: Open triangular area between pitched roof and wall exposes a large hole; user refers to house roofs in the plural.
   - Requested outcome: Fill the gaps so the houses look closed beneath their roofs. Explicit request; no roof redesign requested.
   - Screenshot reference: Feedback message 2, image 3 — `C:/Users/Justin/AppData/Local/Temp/codex-clipboard-67a73dcd-ecff-4455-b5c9-27f199f06c34.png` — red arrow points to the open gable beneath the foreground house roof; another house is visible behind it.
   - Preserve: Existing house and pitched-roof appearance, with batch-wide constraints.
   - Uncertainty: Number of affected houses and whether shared assets cause the gaps await execution inspection; scope remains this level.
   - Status: Implemented; editor evidence and resolved details below. Gameplay/headset acceptance queued, not verified.

#### End piece — overall silhouette and tunnel

5. [x] Resolve the end piece's unclear silhouette; review the proposed cliff-face/manmade-tunnel direction.
   - Problem: User says the silhouette looks bad and its intended identity is unclear. Screenshot highlights the angular, stepped top of the large dark end structure.
   - Requested outcome: Make the end piece visually understandable. User's tentative direction: “I think it should actually resemble more like a cliff face and you're going through a manmade tunnel.” Record this as a proposed art direction, not an approved layout or finalized redesign.
   - Screenshot reference: Feedback message 3, image 1 — `C:/Users/Justin/AppData/Local/Temp/codex-clipboard-82b2c980-6b4b-4735-a22c-5a3e1438f166.png` — wide view of dark end structure, tunnel portal, and railway approach; red arrow marks upper silhouette.
   - Preserve: Railway route and clearance, tunnel passage, existing gameplay, and batch-wide constraints. Coordinate with item 2's entrance seam fix.
   - Uncertainty: Final silhouette, cliff treatment, and tunnel integration remain undecided. Prepare any required top-down proposal with the other layout proposals during execution; no concepts or investigations during collection.
   - Status: Implemented; editor evidence and resolved details below. Gameplay/headset acceptance queued, not verified.

#### Left and right outer sides — approach/car area

6. [x] Add low concrete walls along the left and right sides indicated in the overview.
   - Problem: The outer sides lack the low concrete boundary treatment the user wants.
   - Requested outcome: “The left and right sides need some kind of low concrete wall.” Explicit request; exact wall design remains open.
   - Screenshot reference: Feedback message 4, image 1 — `C:/Users/Justin/AppData/Local/Temp/codex-clipboard-3b1e5c27-f71e-4c60-a937-672a3aa51f02.png` — overview with red arrows at both outer edges beside the approach/car area.
   - Preserve: Existing gameplay, traversal, railway clearance, and batch-wide constraints.
   - Uncertainty: Exact wall height, endpoints, and extent await execution inspection and any required combined layout review. Left/right are screenshot-relative; compass assignment is not yet verified.
   - Status: Implemented; editor evidence and resolved details below. Gameplay/headset acceptance queued, not verified.

#### Left and right water edges — protruding cubes

7. [x] Improve the out-of-line, floating-looking cubes at both sides of the water.
   - Problem: User reports a weird cube on each side of the water that appears to float and does not line up with the rest of the level.
   - Requested outcome: Make both sides look better integrated/aligned. Tentative user solution: “Maybe bring it in on both the left and right sides”; inward movement is not a finalized placement decision.
   - Screenshot reference: Feedback message 4, image 2 — `C:/Users/Justin/AppData/Local/Temp/codex-clipboard-d0b3ad45-d8b9-4a46-a1af-b86d70ca37b4.png` — close view of one dark protruding block at the water edge. Image 1 provides overall context; user explicitly includes both sides despite only one close-up.
   - Preserve: Water-area traversal, surrounding structures, railway clearance, and batch-wide constraints.
   - Uncertainty: Exact objects, current alignment, and appropriate inward offset await execution inspection; include any required layout decision in the combined review. Do not assume the apparent floating is a confirmed geometry cause.
   - Status: Implemented; editor evidence and resolved details below. Gameplay/headset acceptance queued, not verified.

#### Railway — grassy end area

8. [x] Add gravel beneath the railway where it currently sits directly on grass.
   - Problem: User says the track sitting directly on grass does not make sense.
   - Requested outcome: Give the track a gravel bed. Explicit request.
   - Screenshot reference: Feedback message 5, image 1 — `C:/Users/Justin/AppData/Local/Temp/codex-clipboard-e4d318f7-378a-4b37-95dd-95d310d8b2af.png` — overview with arrow pointing at track through the grassy area beyond the tunnel.
   - Preserve: Existing track alignment, elevation, railway clearance, surrounding scenery, and batch-wide constraints.
   - Uncertainty: Exact gravel extent/material and transitions await execution inspection; screenshot establishes the grassy end area, not a request to change other levels.
   - Status: Implemented; editor evidence and resolved details below. Gameplay/headset acceptance queued, not verified.

#### Cliff/hill junctions — left and right openings

9. [x] Close the large visible openings between the end structure and the hills on both sides.
   - Problem: Hills on the left and right leave massive openings, exposing the outside void beside the end structure.
   - Requested outcome: Hide/close these openings. Tentative user solution: “Probably want to extend that cliff to the right and left so you don't see that.” Cliff extensions are a proposed method, not an approved final layout.
   - Screenshot references: Feedback message 5, images 2–3:
     - Image 2: `C:/Users/Justin/AppData/Local/Temp/codex-clipboard-06a531ea-2441-4df4-8def-ebc8a4d42118.png` — one side opening beside the dark structure, marked with red arrows.
     - Image 3: `C:/Users/Justin/AppData/Local/Temp/codex-clipboard-83741bf5-eae8-4d27-a568-abe7f9c9816f.png` — opposite side opening, marked with a red arrow.
   - Preserve: Railway/tunnel clearance, existing hill and end-area context, and batch-wide constraints.
   - Uncertainty: Exact junction geometry and extension extents await execution inspection. Coordinate with item 5's cliff-face/tunnel proposal in the combined layout review; left/right are screenshot-relative until verified.
   - Status: Implemented; editor evidence and resolved details below. Gameplay/headset acceptance queued, not verified.

#### Compound — marked light fixtures

10. [x] Rework the marked fixtures into recognizable military-base/sports-field-style spotlights and light the compound with them.
   - Problem: Existing fixtures do not read clearly as the spotlights the user wants.
   - Requested outcome: Make the marked fixtures resemble lights found at a military base or sports field; use an emissive texture for the light fixture and wide-casting baked lights pointed into the compound. Explicit request. The sports-field photograph is a style reference, not a requirement to reproduce every detail.
   - Screenshot references: Feedback message 6, images 1–2:
     - Image 1: `C:/Users/Justin/AppData/Local/Temp/codex-clipboard-d46e75a1-b5ed-4005-9ac5-e96ae69a1e92.png` — compound overview with four arrows indicating fixtures at the two rear towers and the left/right side poles.
     - Image 2: `C:/Users/Justin/AppData/Local/Temp/codex-clipboard-154668c6-09bb-470f-8cfd-f4ba8e384b1d.png` — sports-field floodlight mast with a bright multi-lamp head, supplied as visual reference.
   - Preserve: Existing compound gameplay, traversal, railway clearance, and performance constraints. Keep requested illumination baked; group lighting work with other approved changes to avoid repeated bakes.
   - Uncertainty: Exact fixture geometry, lamp arrangement, beam width, intensity, and bake settings await execution inspection. No fixture count or height change beyond the marked fixtures is assumed.
   - Status: Implemented; editor evidence and resolved details below. Gameplay/headset acceptance queued, not verified.

#### Compound — upper-area access beside the track

11. [x] Add blocks on both sides of the track to make reaching the upper area easier.
   - Problem: Access from the ground to the raised areas beside the track needs easier climb-up routes.
   - Requested outcome: Place blocks in the left and right circled areas to help players get up to the upper area. Explicit request.
   - Screenshot reference: Feedback message 7, image 1 — `C:/Users/Justin/AppData/Local/Temp/codex-clipboard-85e27a27-e21c-4f84-9e9d-1a185bc5a373.png` — red circles mark ground-level spaces against the raised platforms on either side of the track, beneath the overhead walkway.
   - Preserve: Railway clearance, existing platform/walkway access, locomotion and climbing feel, and batch-wide constraints.
   - Uncertainty: Block counts, heights, spacing, and exact placement await execution inspection and any required combined layout review. Ease of traversal will require a queued gameplay check; implementation alone is not playtest evidence.
   - Status: Implemented; editor evidence and resolved details below. Gameplay/headset acceptance queued, not verified.

#### Compound — red posts alongside the track

12. [x] Add emissive red tops and baked spotlights to the red posts.
   - Problem: User wants the red sticks/posts to look better through illuminated tops and baked lighting.
   - Requested outcome: Give the red posts emissive red tops and baked spotlights. Explicit request.
   - Screenshot reference: Feedback message 8, image 1 — `C:/Users/Justin/AppData/Local/Temp/codex-clipboard-b42771a5-a5ef-4cad-ac57-6bd7eebb43de.png` — rows of red posts flanking the railway through the compound and approach.
   - Preserve: Existing post placement and railway clearance, gameplay, and performance constraints. Coordinate with item 10 and the batch lighting pass to avoid repeated bakes.
   - Uncertainty: Spotlight direction, spread, intensity, and light color are unspecified; red is explicitly requested for the emissive tops. Exact affected post count awaits execution inspection.
   - Status: Implemented; editor evidence and resolved details below. Gameplay/headset acceptance queued, not verified.

#### Compound — two south-side stairs

13. [x] Reorient and shorten both south-side access runs so they ascend from inside the compound to the upper areas; build them as ramps per item 15.
   - Problem: The two southern stairs currently approach from outside the compound and have overly long runs.
   - Requested outcome: Rotate both access runs so players start inside the compound and ascend to the upper areas; make them shorter in horizontal length. Explicit requests. The original taller-step suggestion is superseded by feedback message 10's explicit request to convert all stairs in this level to ramps (item 15).
   - Screenshot reference: Feedback message 9, image 1 — `C:/Users/Justin/AppData/Local/Temp/codex-clipboard-5f33510a-da32-461b-ae38-cfc6c8f58165.png` — compound overview with red markings at the two southern platform/stair connections.
   - Preserve: Connections to both upper platforms, usable locomotion/traversal, railway clearance, and batch-wide constraints. Coordinate with item 11's added climb-up routes.
   - Uncertainty: Exact rotation, footprint, landing placement, and ramp slope await execution inspection and required combined layout review. Traversability requires a queued gameplay check; do not equate geometry changes with verified traversal.
   - Status: Implemented; editor evidence and resolved details below. Gameplay/headset acceptance queued, not verified.

#### Compound — upper stairs near the two rear towers

14. [x] Shorten the two marked upper access runs, converting them to ramps per item 15.
   - Problem: User says the two arrowed stair sets are too long.
   - Requested outcome: Reduce the horizontal length of both marked runs. Explicit request, combined with the ramp conversion below.
   - Screenshot reference: Feedback message 10, image 1 — `C:/Users/Justin/AppData/Local/Temp/codex-clipboard-462410a1-5f7c-4119-9438-337810f4f9c4.png` — red arrows identify left/right upper stairs near the rear towers.
   - Preserve: Connections between existing elevations, traversal, railway clearance, and batch-wide constraints.
   - Uncertainty: Exact length, slope, and landings await execution inspection and required combined layout review; gameplay traversal remains to be queued.
   - Status: Implemented; editor evidence and resolved details below. Gameplay/headset acceptance queued, not verified.

#### Level-wide — stair-to-ramp conversion

15. [x] Convert all stairs in `bridge-finale-v1` into ramps.
   - Problem/request: User says, “And I think in general, let's change all the stairs into ramps instead of stairs.” Record “let's change” as an explicit direction to convert, scoped to the active level, not other levels.
   - Requested outcome: Ramps replace all stair runs in this level, including items 13 and 14. Item 13's inward orientation and shorter run still apply; its taller-step suggestion is superseded.
   - Screenshot reference: Feedback message 10, image 1 — `C:/Users/Justin/AppData/Local/Temp/codex-clipboard-462410a1-5f7c-4119-9438-337810f4f9c4.png`; image highlights two runs but the user's wording explicitly extends conversion to all stairs in the level.
   - Preserve: Existing route connections and destination elevations unless an approved layout changes them, locomotion feel, railway clearance, and batch-wide constraints.
   - Uncertainty: Full stair inventory, ramp slopes, collision treatment, and any required layout decisions await execution inspection. Combine layout proposals and queue traversal checks; no Play Mode during collection or execution unless requested.
   - Status: Implemented; editor evidence and resolved details below. Gameplay/headset acceptance queued, not verified.

#### Compound — side walkways and two broad southern platforms

16. [x] Make the four marked raised areas more interesting with asymmetric dressing.
   - Problem: The marked left/right side walkways and broad southern platforms look plain and too symmetrical.
   - Requested outcome: Add interest and make the arrangements asymmetric. Explicit direction. “Maybe some crates or stacked crates” is a tentative user suggestion, not a finalized prop choice or placement approval.
   - Screenshot reference: Feedback message 11, image 1 — `C:/Users/Justin/AppData/Local/Temp/codex-clipboard-501d28c7-43d3-44e0-8187-65786a31df49.png` — four red arrows mark both side walkways and both broad southern raised platforms.
   - Preserve: Usable walkway widths, platform access, existing gameplay/spawn access, railway clearance, and performance constraints. Coordinate with ramp changes in items 13–15 and climb-up blocks in item 11.
   - Uncertainty: Prop selection, counts, stack heights, and asymmetric arrangements await execution inspection and required combined layout review. No spawn relocation is requested; if coal/gold/enemy placement becomes involved, follow the spawn-placement workflow.
   - Status: Implemented; editor evidence and resolved details below. Gameplay/headset acceptance queued, not verified.

#### Follow-up — compound ground and baked lighting

17. [x] Improve the ground's baked-lighting appearance; consider additional tessellation as suggested by the user.
   - Problem: User reports the ground lighting needs improvement and points to the ground mesh/lighting in a wireframe view.
   - Requested outcome: Better-looking baked lighting on the compound ground. User's tentative diagnosis/solution: “I think the ground needs a little more tessalation so that the light bake can look better.” Do not treat tessellation as a confirmed cause or guaranteed fix.
   - Screenshot reference: Feedback message 12, image 1 — `C:/Users/Justin/AppData/Local/Temp/codex-clipboard-d581d9da-6028-4138-bf27-2e17190e4564.png` — ground and red warning-light pools along the railway, with mesh edges visible.
   - Preserve: Ground shape, collision, railway clearance, existing lighting intent and performance constraints.
   - Collection uncertainty (historical): Cause of the lighting appearance and appropriate tessellation/bake adjustments await authorized investigation. No changes or investigation during collection.
   - Status: Implemented and editor-checked; queued for playtest. See follow-up execution result below.

#### Follow-up — left/right middle ramps

18. [x] Change both middle ramps from solid wedges to thin plank-like sloping slabs.
   - Problem: The two middle ramps have bulky wedge-shaped sides/undersides.
   - Requested outcome: A thinner, approximately constant-thickness plank-like profile, following the outlined example. Explicit shape request, revising item 15's implementation for this pair only; “plank” does not explicitly require a wood material.
   - Screenshot references: Feedback message 12, images 2–4:
     - Image 2: `C:/Users/Justin/AppData/Local/Temp/codex-clipboard-62591476-8883-4275-bfee-8ee0a29715c7.png` — arrow at one middle connecting ramp.
     - Image 3: `C:/Users/Justin/AppData/Local/Temp/codex-clipboard-a354f890-bef0-4c8f-aa9a-726bf4af3947.png` — arrow at the opposite middle ramp's wedge underside.
     - Image 4: `C:/Users/Justin/AppData/Local/Temp/codex-clipboard-9eed72c9-c930-47b4-9ff0-4bb7c803c704.png` — red outline shows the intended thin sloping slab.
   - Preserve: Both ramp endpoints, slope/access between platforms, traversability and existing materials unless later requested otherwise.
   - Collection uncertainty (historical): Exact slab thickness and underside collision await execution; no request to change every ramp's profile.
   - Status: Implemented and editor-checked; queued for playtest. See follow-up execution result below.

#### Follow-up — four spotlight assemblies

19. [x] Group each of the four spotlights into its own repositionable assembly, including its Light component.
   - Problem: Fixture parts and their lights are not conveniently grouped for manual repositioning.
   - Requested outcome: One group per spotlight containing the associated fixture assembly and actual light, so moving the group moves the whole unit. Explicit request applying to all four compound floodlights, extending item 10; not the ten red warning posts.
   - Screenshot reference: Feedback message 13, image 1 — `C:/Users/Justin/AppData/Local/Temp/codex-clipboard-07950828-c07c-4a2f-87b1-05b7565e3f2e.png` — arrows identify the tower fixture and nearby pole fixture on one side; user explicitly includes all four lights.
   - Preserve: Current world positions/orientations, fixture appearance, light settings and baked-lighting references when reorganizing. Include the associated support/pole where it belongs to the spotlight assembly, not the tower/platform itself.
   - Collection uncertainty (historical): Group pivots/names are routine implementation details; exact hierarchy and references await authorized execution. Later repositioning of baked lights will require rebaking.
   - Status: Implemented and editor-checked; queued for playtest. See follow-up execution result below.

#### Follow-up — tunnel-bank front edge

20. [x] Fix the remaining front-edge z-fighting and make the exposed edge black/dark to match the wall.
   - Problem: The green front lip above the water-facing tunnel-bank wall still shows z-fighting. This is user-reported remaining work after item 1, not resolved by the initial editor checks.
   - Requested outcome: Remove the flickering/overlapping surface appearance; the front edge need not be green and can match the black/dark wall. Explicit correction with permission to replace that edge's green finish.
   - Screenshot reference: Feedback message 14, image 1 — `C:/Users/Justin/AppData/Local/Temp/codex-clipboard-32bac9e0-e09a-4d82-982e-dac8004b3d09.png` — arrow marks the green water-facing lip beneath the tunnel approach.
   - Preserve: Railway alignment, bridge landing/tunnel clearance, bank collision and surrounding valley grass. The request concerns the front edge, not wholesale recoloring of the valley.
   - Collection uncertainty (historical): Exact overlapping faces and edge-material boundary await authorized investigation. Color replacement alone must not be treated as proof the z-fighting is fixed.
   - Status: Implemented and editor-checked; queued for playtest. See follow-up execution result below.

#### Follow-up — outer left/right perimeter fences

21. [x] Extend both outer side fences/walls northward all the way to the water.
   - Problem: The low outer boundaries stop partway along the level, leaving the continuation toward the north water edge open.
   - Requested outcome: Continue the left and right fences indicated in the overview to the north-side water. Explicit request extending item 6's low approach walls.
   - Screenshot reference: Feedback message 14, image 2 — `C:/Users/Justin/AppData/Local/Temp/codex-clipboard-ff0bc758-88e7-41ee-8de2-f81d50a54db8.png` — arrows identify the two low outer perimeter walls beside the traffic approach.
   - Preserve: Existing boundary style/height, traffic, inner compound structures, river recovery access and railway clearance. North is Z+.
   - Collection uncertainty (historical): Exact termination/junction with the water-side boundary awaits authorized inspection; this does not request a fence across the water or railway.
   - Status: Implemented and editor-checked; queued for playtest. See follow-up execution result below.

#### Follow-up — missing tunnel ground

22. [x] Restore the missing visible tunnel floor.
   - Problem: User sees empty background on both sides of the railway through the tunnel.
   - Requested outcome: Continuous visible ground through the existing tunnel.
   - Screenshot reference: `C:/Users/Justin/AppData/Local/Temp/codex-clipboard-c3a0381a-bfd1-4277-9181-b7372a1dd1f3.png`.
   - Preserve: Floor shape/collision, railway clearance, dark bank edge, grass, existing lighting and unrelated edits.
   - Finding: The floor mesh had upward-facing triangles and supported collision, but its renderer was disabled in the saved prefab and bake scene. All 149 bake-excluded finale renderers, including the drawbridge, had the same disabled state. This supersedes the earlier visibility-check result: that result did not establish persistence through later editor/export operations.
   - Implemented: Restored those renderer flags; temporary bake exclusions are now explicitly recorded, restored from the prefab before applying export data, and left visible in the saved authoring scene. Preparation hides them again only for baking. Added a scoped open-block re-export command; retained existing bindings/exclusion rules. Existing lighting re-exported and installed in Expedition; no geometry/navigation/light bake was needed.
   - Validation: [24 EditMode checks](../output/level-polish-batch/followup/tunnel-floor-checks.txt) pass through preparation, export, two scene reloads, prefab/Expedition floor and drawbridge visibility, and 15 tunnel collision samples. [Restored-floor view](../output/level-polish-batch/followup/tunnel-floor-restored.png) inspected. Compilation succeeds; capture retains the known CutePixel missing-em-dash warning.
   - Status: Implemented and editor-checked; **queued for playtest** for actual tunnel crossing and drawbridge visibility on clients/headset. No Play Mode or diagnostic-report inspection.

#### Follow-up — tunnel light colours the entire grass bank

23. [x] Keep tunnel lighting local instead of illuminating the entire grass bank.
   - Problem: User reports that a small tunnel-entrance light makes the full grass bank brightly yellow.
   - Requested outcome: Local illumination near the tunnel, with the rest of the bank staying appropriately dark.
   - Screenshot reference: `C:/Users/Justin/AppData/Local/Temp/codex-clipboard-60811acb-6a28-4072-80ab-f1cef23ed3f3.png`.
   - Preserve: Current user-edited lights, bank materials/geometry/collision, tunnel readability and night-to-dawn flow.
   - Finding: The 70×50 m bank renderer had no lightmap and used Blend Probes, spreading the sampled warm tunnel illumination across the whole renderer. Current authored scene contains five baked tunnel point lights, plus edited floodlight positions/settings and point-type red warning lights; these intervening edits are retained.
   - Implementation: Include the static far-bank mesh in baked GI and receiver export, disable its probe fallback, and retain its existing lightmap UVs at scale 1. It now remains visible during bake preparation, superseding item 22's former floor-exclusion policy; moving-bridge exclusions are unchanged.
   - Validation: One finale-only lighting bake/export completed; 1,741 bindings installed in Expedition. [23 EditMode checks](../output/level-polish-batch/followup/bank-light-checks.txt) pass, including exported bank binding, probe fallback disabled, retained visibility/collision, scene reload, compilation and preservation of all 22 current light transforms/types/bake modes/intensities/ranges/cone angles/colours in both scene and prefab. Matching [before](../output/level-polish-batch/followup/bank-light-BEFORE.png) and [after](../output/level-polish-batch/followup/bank-light-AFTER.png) views show localized entrance light and dark green outer grass. Capture reports the known CutePixel missing-em-dash warning.
   - Status: Implemented and editor-checked; **queued for playtest** for night-to-dawn appearance and actual client/headset tunnel crossing. No geometry or navigation change; no Play Mode.

#### Follow-up — stationary gold deposit crate lighting

24. [x] Make the stationary finale gold crate receive baked lighting.
   - Problem: User reports that the fixed crate beside the bridge button does not pick up lightmap illumination and asks how to fix it.
   - Requested outcome: Baked illumination on the stationary crate mesh.
   - Screenshot reference: `C:/Users/Justin/AppData/Local/Temp/codex-clipboard-be7bb394-d7d2-45d3-97c8-6001cc0c0066.png`.
   - Preserve: Current location, collision, gold intake/payout behaviour, dynamic label, coin/reward effects and other instances of the imported crate model.
   - Finding: `Finale gold deposit/CrateGoldBarMesh` had no Contribute GI flag and its FBX mesh had no UV2. Receive GI already said Lightmaps, but the renderer had no actual lightmap binding.
   - Implementation: Created a finale-only mesh copy with generated secondary UVs; enabled Contribute GI and Receive GI Lightmaps on the stationary mesh only, with lightmap scale 3 and probe fallback off. Source FBX, collider mesh, label and receiver/effect components retained. Saved intervening user scene edits before making changes.
   - Validation: Finale-only bake/export completed and installed in Expedition (1,746 total bindings including current user edits). [17 editor checks](../output/level-polish-batch/followup/gold-crate-checks.txt) pass: generated UV2, retained geometry counts/bounds and original collision mesh, static GI, actual exported crate binding, preserved receiver intake/label/coin references, and reopened scene atlas assignment. Matching [before](../output/level-polish-batch/followup/gold-crate-BEFORE.png) / [after](../output/level-polish-batch/followup/gold-crate-AFTER.png) views inspected. Original FBX remains unchanged; dynamic label is not GI static.
   - Existing issue discovered during validation: reward-text spawn/font/material/effect fields are null both in the pre-follow-up baseline and current prefab. An initial check incorrectly assumed they were populated; corrected to verify preservation against the baseline. This lighting change does not claim reward-text functionality. Capture retains the known CutePixel em-dash warning.
   - Status: Implemented and editor-checked; **queued for playtest** for crate lighting/readability and deposit interaction. No Play Mode or navigation change.

## Follow-up execution result — November 11

Items 17–21 were authorized together by “execute that feedback.” The collection wording above records the original feedback; the following implementation/validation supersedes its pending statuses.

- **17 — Ground:** retained the clipped ground surface and its BoxCollider; subdivided existing triangles to a maximum 5.57 m edge. Ground mesh grows from 328 to 6,538 triangles. Retained interpolated lightmap UV charts and increased this renderer's lightmap scale from 0.25 to 2 at the existing 2 texels/metre bake setting. Low lightmap density was a concrete issue; tessellation alone was not treated as the lighting fix. Ground surface area remains 14,081.04 → 14,081.03 square metres within float precision. Baked review shows smoother warning-light pools.
- **18 — Middle ramps:** both now have 0.2 m vertical thickness along the full incline, with matching mesh collision. Top endpoints, slope, width and concrete material remain unchanged. Other ramps keep their existing profiles.
- **19 — Four assemblies:** `Env/November 11 polish/{West,East} {south,north} floodlight`, each containing the fixture array, pole and actual baked spotlight. Pivots sit at the pole bases. World transforms/settings remain unchanged. Moving these groups later requires rebaking lighting.
- **20 — Bank edge:** removed the grass slab's front face where it overlapped the stone foundation, closed only the exposed 0.03 m lip, and used Ridge rock for the edge and a 1.5 m top strip. Retained the grass behind it and the original bank collision. Neutral close views show a continuous dark water-facing edge.
- **21 — Side boundaries:** extended both original low concrete walls at X±47 from Z0–31 to Z0–80, ending at the river retaining walls. Height/thickness and concrete texture scale are retained; no wall crosses the railway or river.

Actual validation and limits:

- Verified MCP project `X:/Work/duck-rails/Assets`, clean finale bake scene in Edit Mode, and no other active project chat. Preserved existing Farm and audio-settings edits; no gameplay/networking scripts or spawn markers changed.
- [1,383 geometry/spawn checks](../output/level-polish-batch/followup/geometry-checks.txt), [81 follow-up checks](../output/level-polish-batch/followup/checks.txt), and [398 bridge-sweep/railway/reference checks](../output/level-polish-batch/followup/final-checks.txt) pass: **1,862 total**. Floor-only routes from both tower defenders to the button pass.
- [Navigation rebuilt](../output/level-polish-batch/followup/navigation-bake.txt): 72,503 nodes, approximately 4.5 MB. One coordinated finale-only lighting bake/export; [1,740 bindings](../output/level-polish-batch/followup/lighting.txt) installed into Expedition. Two atlases, 512×512 and 256×256.
- Compilation succeeds (`scriptCompilationFailed=False`). The post-bake Console query was empty; the final refresh subsequently logged three UnityEditor.Connect web-request/completion errors, with no project-code stack frames. These editor service errors were not investigated as part of level polish. Neutral geometry and actual baked captures inspected. A first subdivision attempt exposed incorrect assumed vertex ordering; corrected using the actual triangle indices before the bake, and checked area preservation plus the corrected views. Five new helper/mesh assets have metadata; the saved scene is clean.
- Complete loaded geometry now has 44,230 triangles (+6,214 versus the initial pass), still 1,900 mesh renderers. No device performance claim. No Play Mode, real-client/headset testing or diagnostic-report inspection. **Queued for playtest:** slab traversal/underside clearance, extended boundaries, bank edge from moving viewpoints, warning-light readability and Quest cost.

Views: [before overview](../output/level-polish-batch/followup/before-fortress.png), [after overview](../output/level-polish-batch/followup/after-fortress.png), [before bank](../output/level-polish-batch/followup/before-cliff.png), [after bank](../output/level-polish-batch/followup/after-cliff.png), [baked ground](../output/level-polish-batch/followup/baked-ground.png), [baked slab ramp](../output/level-polish-batch/followup/baked-ramp.png). Geometry comparisons use matching temporary neutral lighting; baked views use the saved authoring scene. Helper: `Assets/Editor/RailFinalePolishFollowup.cs`; guarded against repeat application. The finale bake scene is left open and clean.

## Execution result — November 11

The user explicitly approved the [combined top-down proposal](../output/level-polish-batch/layout-review.png). Checked items 1–16 mean **implementation complete**, not gameplay acceptance. Follow-up items 17 onward are excluded from this result. Original feedback/uncertainty wording is retained as history; the outcomes below supersede it for the initial pass.

1. Trimmed overlapping/hidden concrete box faces, including the recovery-landing/approach intersection. Original structural colliders retained; north seam views inspected.
2. Closed tunnel wall/roof junctions and added fitted concrete portal frames without narrowing the railway opening.
3. Replaced the sixteen water panels with a continuous recessed water surface at Y-0.25 and a shaped solid stream bed near Y-0.6. Removed the underlying flat ground from that valley section.
4. Closed all four cottage gables beneath the retained pitched roofs.
5. Replaced the rectangular end masses with grounded faceted stone cliffs around the manmade tunnel. Final cliff bases reach the river bed, avoiding floating outer edges.
6. Added 1.2 m-high, 0.6 m-thick approach walls at X±47, Z0–31.
7. Moved/narrowed both river end walls to X±46 (2 m width), aligning them inside the outer footprint.
8. Added a 5 m-wide repeating gravel bed beneath the retained victory railway, Z110–310. Rail geometry, sleeper spacing and collision are unchanged.
9. Extended cliff shoulders to X±65, overlapping the hills through Z160–175 to close the lateral openings.
10. Replaced four plain heads with three-lamp floodlight arrays, emissive textured lenses and four wide inward-facing baked spots.
11. Added four climb blocks per side, rising in 0.8 m increments toward the north bastions.
12. Added ten emissive red caps with downward baked red pools, retaining the user's current post positions.
13. Replaced south stairs with inward-facing 3 m-wide ramps, 4 m run / 2 m rise.
14. Shortened upper runs from 12 m to 8 m, with a 4 m rise.
15. Converted all ten flights to ramps. Middle runs retain 8 m; river recovery keeps 19.5 m switchbacks and existing landings. Extended all 64 recovery posts down onto the ramp surfaces while preserving rail tops.
16. Added fourteen static crates in twelve asymmetric groups across the two platforms and side patrol walks, retaining clear circulation and spawn access.

Only two coal candidates move: the former on-stair candidates now sit on adjacent flat roof at X±23/Y4.2/Z62. Retained identities, pool order, 28 coal / 26 gold / six enemy candidates, two active tower markers, and 6 coal / 2 gold / 2 enemies per run. No gameplay or networking scripts changed; existing local traffic/post changes and unrelated Farm edits are preserved.

Actual validation:

- Compilation succeeded; final Console error query returned zero entries. Captures reported the existing CutePixel missing-em-dash warning on `BRAKE NOW`; no font substitution was introduced.
- [1,375 geometry/spawn checks](../output/level-polish-batch/geometry-checks.txt) pass: ramp support/body clearance, stream depression, lightmap UVs, new fixture settings, four-corner spawn support, new-prop clearance and 100 seeds per spawn type.
- [398 final checks](../output/level-polish-batch/final-checks.txt) pass: full-width train corridor, all 17 bridge angles from 0–80 degrees, recovery-post seating, runtime visibility and saved lighting/navigation references. Existing bridge-hold checks also passed.
- [Navigation bake](../output/level-polish-batch/navigation-bake.txt): 72,306 nodes, 4.4 MB. [Both tower floor-only routes to the button](../output/level-polish-batch/navigation-checks.txt) pass; nearby ground nodes are within 0.8 m.
- Lighting baked/exported and installed in Expedition: 18 total baked lights and 1,740 receiver bindings. A second bake was necessary after the post-seating check found the inherited tread-to-ramp gap. No catalogue-wide rebake.
- [Geometry accounting](../output/level-polish-batch/geometry-cost.txt): same serialized-mesh method gives 32,274 → 37,992 triangles (+5,718) and 2,009 → 1,900 mesh renderers (-109); unchanged nested marker internals excluded from both. Complete final loaded mesh count is 38,016 triangles. This is not measured headset performance.
- Source/document whitespace check passes. Whole-tree diff reports Unity's serialized blank `m_Name: ` whitespace; no semantic YAML repair was needed. Generated assets have `.meta` files and unique GUIDs.
- No Play Mode, multi-client run, headset test or diagnostic-report inspection occurred. **Queued for playtest:** [Bridge finale batch polish](playtest-queue.md#bridge-finale-batch-polish--queued-2026-11-11). Natural ramp/climbing feel, low/elevated sightlines, night/dawn readability, peer behavior and Quest performance remain unverified.

Views: [before fortress](../output/level-polish-batch/before-fortress.png), [after fortress](../output/level-polish-batch/after-fortress.png), [before cliff](../output/level-polish-batch/before-cliff.png), [after cliff](../output/level-polish-batch/after-cliff.png), [after valley](../output/level-polish-batch/after-valley.png), [north seam view](../output/level-polish-batch/after-north-seams.png), [actual baked authoring lighting](../output/level-polish-batch/baked-fortress.png). Before/after images use temporary neutral review lighting; the last image uses the saved bake scene, not a gameplay run.

Authoring helper: `Assets/Editor/RailFinalePolish.cs`, a guarded one-shot adaptation of the current prefab. Do not rerun the original `RailFinaleFort.Install` builder: it would replace user edits and this polish. Evidence/check command sources and the original serialized baseline are retained under `output/level-polish-batch/`. The final bake scene is left open in Edit Mode.

## Execution and validation requirements

- Read required design, execution-plan, prototype/setup and relevant feature/layout guidance before execution; inspect current assets to account for other chats' completed work.
- Preserve unrelated edits, existing gameplay/networking, railway clearance, and performance constraints. Coordinate related work and avoid repeated bakes.
- Bundle any required top-down layout proposals into one approval review; continue independent authorized work meanwhile.
- Follow spawn-placement guidance for coal, gold, or enemies.
- Verify the Unity MCP project target and avoid conflicts with another chat actively editing the project.
- Run appropriate editor checks without Play Mode unless requested. Record actual validation separately from implementation; add gameplay checks to `docs/playtest-queue.md` as queued for playtest, never verified merely because implemented.
- Track unresolved items and finish with a concise per-level report and useful before/after views where practical.
