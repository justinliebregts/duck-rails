# Subway compound blockout

## Discord station — November 11, 2026

A local **JOIN DISCORD** physical button with a "Join the Discord!" TextMeshPro
heading sits on the inner right partition of the Station Info nook, facing the
rating station across the bench. It reuses `PressableButton`; `RailDiscordLink`
opens `https://discord.gg/sfyvhahnDW` with `Application.OpenURL` (Quest browser on
headset), ignoring repeat presses for 3 seconds. No networking or room change.

The editable prefab is `Assets/DuckRails/Compound/Discord/DiscordStation.prefab`
(change the invite on its `RailDiscordLink.inviteUrl`). `Duck Rails > Compound >
Install Discord Station` installs it under `Compound Geometry` only when absent and
validates the callback, URL and CutePixel SDF labels. Validation: installed through
Unity MCP in the open editor, validation passed, no Console errors; a scene camera
capture confirmed placement and readability. Browser launch on desktop/Quest and
headset reach are queued for playtest.

## Rating station — November 11, 2026

The compound has a local **RATE GAME** physical button beside a world-space PNG
instruction canvas on the inner left wall of the Station Info nook. It reuses
`PressableButton` for VR touch and desktop aiming/clicking. On Quest it requests
the initialized Meta SDK's native rating/review UI; desktop opens the configured
Meta app's store listing. Failure shows a retry message and does not change the
room. No rewards or submitted-review tracking are attached.

Replace `Assets/DuckRails/Compound/Rating/RateGameInstructions.png` with your art,
keeping its `.meta` file. The canvas updates automatically. Use 1024 × 768 (4:3)
to retain the current proportions, or assign another texture to the prefab's
`Instructions PNG - replace texture` RawImage. The editable prefab is
`Assets/DuckRails/Compound/Rating/RateGameStation.prefab`; its instance is under
`Compound Geometry` in `Assets/Scenes/Compound.unity`.

`Duck Rails > Compound > Install Rating Station` installs the saved prefab only
when absent and preserves subsequent art/placement edits. The placeholder uses
the CutePixel font source and all live labels use CutePixel SDF.
Quest store launch, return, physical reach and readability are queued for playtest.
Validation: Unity 6000.3.24f1 batch Edit Mode compilation and installation passed
with exit code 0, no C# errors or exceptions. The station's persistent callback,
PNG reference, world-space canvas and CutePixel labels passed setup checks;
the scene diff contains only the new station instance. Unity MCP was unavailable;
the existing project-scoped relay configuration was checked and CLI validation
targeted `X:/Work/duck-rails`. No Play Mode or review submission was performed.
The SDK integration follows [Meta's rating/review documentation](https://developers.meta.com/vr/documentation/unity/ps-rate-and-review/).

## Material concept — November 4, 2026

The user approved this concept for implementation. `RailCompoundMaterials` adds **Duck Rails > Compound > Materials > Apply Quackville depot treatment** for the open Compound scene. It maps existing box surfaces to shared depot materials, preserves bounds/colliders and lightmap UV charts, and applies only mesh/material properties to the outer subway prefab. Text, emissive lenses, see-through guards and shallow water retain their existing treatment. Original atlas files and depot materials are reused unchanged. After applying, rebake with **Compound > Lighting > Bake Sample**. This is a surface pass; geometry refinement and target-headset acceptance remain separate.

Implemented and validated November 4: 253 mapped surfaces, 75 shared meshes, 28,336 vertices across mapped instances and 279 nonempty material slots. Collision serialization, transforms, light settings and mesh bounds remained unchanged. All atlas coordinates stay inside their selected tiles; UV2 and material slots are complete, and all 253 saved prefab references match the scene. The 55 subway geometry/binding checks and session checks passed. Compound was rebaked to one lightmap with 342 probes and an updated reflection probe. The final Console error query was empty. [Evidence](validation/compound-materials/checks.txt), [geometry checks](validation/compound-materials/subway-checks.txt), [repeatable validation command](validation/compound-materials/validation-command.cs.txt).

Actual Unity captures: [forward](../output/compound-lighting/materials-after-forward.png), [low eye](../output/compound-lighting/materials-after-low-eye.png), [information nook](../output/compound-lighting/materials-after-info.png), [reverse](../output/compound-lighting/materials-after-reverse.png). These use the retained scene lighting without temporary review fill. The result is darker and simpler than the generated concept; it is not final lighting or target-headset performance acceptance. No new multiplayer/Play Mode claim is made. The diagnostic backend returned ten existing open reports after retrying outside the network-restricted sandbox; none was resolved.

The editor exited during the first bake/recompile; saved appearance assets survived, and a reopened editor completed the bake and checks. The pre-existing unsaved Expedition scene was backed up before switching scenes at `tmp/compound-materials/Expedition-unsaved-20261104-054637.unity`; its source scene file was not overwritten by this material pass. Compound remains open for review. Unity-generated prefab YAML contains its usual blank `value: ` trailing spaces; code/document whitespace checks are separate.

The user requested a compound pass using the existing Quackville/depot materials, starting with a concept render. [Forward material study](../output/imagegen/compound-material-study-v1/forward.png) uses the actual compound capture, Town/Industrial atlases, approved depot study and implemented depot capture as references. It proposes stained plaster, faded olive lower bands, sparse exposed brick, worn concrete and patched oxidized steel/timber while retaining the current layout. Generated with built-in imagegen; this is a preview, not exact atlas mapping or an implemented Unity change. [Reference and prompt notes](../output/imagegen/compound-material-study-v1/prompt-notes.txt). The implementation and its validation are recorded above; final lighting/headset review remains pending.

November 1, 2026. The user approved a walkable, roughly textured Unity blockout of the subway concept. The banked outdoor concept remains an alternative. This is a layout/interaction prototype, not finished environment art or headset acceptance.

## Open and edit

Open `Assets/Scenes/Compound.unity`. Under `Compound Geometry`, the `SubwayCompound` instance now references `Assets/DuckRails/Compound/SubwayV2/SubwayCompound.prefab`. Enter Prefab Mode to edit seven nested modules: terminal shell, blocked surface stairs, shallow fountain, station information nook, walk-through supplies store, platforms/controls wall, and lighting/repairs. The first version remains in `Subway` as a fallback.

Meshes, materials and point-filtered 64 x 64 textures are saved in the SubwayV2 folder. UVs repeat at consistent physical scale. All solid collision uses the existing Environment layer. Most shapes are boxes with independently editable transforms; custom silhouette modeling and final atlases come later.

The revised occupied floor is 18 m wide and 24 m long, with a 7 m clear original ceiling. The first version was 26 x 30 m with a roughly 4.8 m ceiling. Fountain rim height remains 0.36 m. These are implemented trial dimensions, not user-approved final measurements or twelve-player acceptance. The DuckPlayer rig and locomotion are unchanged.

## Concept fidelity corrections — confirmed November 1

The user identified that the first blockout did not reproduce the concept's architecture or spatial proportions. Passing collision/session checks does not establish visual acceptance. The November 1 V2 scene implements the following correction brief; visual acceptance remains with the user.

- **Former boarding edges on both sides:** the left and right boundaries originally allowed passengers to board trains. They must read as recognizable subway platform edges/open boarding bays now sealed with improvised barricades, not ordinary permanent room walls. Retain inaccessible track space beyond them and the intended offscreen tunnel sounds. Establish the original platform edge, track trench and repeated structural bays before dressing the barriers.
- **Narrower occupied space:** the current 26 m width feels too wide. Bring the information/store fronts, columns and shared center closer together. Maintain usable paths and player clearance, but do not use the whole outer structural footprint as an empty gathering court.
- **High original roof, low improvised enclosure:** the main subway ceiling is higher than the first blockout communicates. Makeshift partitions stop well below it, revealing an overhead volume with columns, beams, pipes and darkness continuing above. The player-level refuge feels enclosed while the original station feels larger overhead. Avoid a continuous low ceiling or perimeter infill that visually seals this upper space.
- **Pillars are structural landmarks:** place columns as a coherent original subway grid, with stalls and barricades fitted between them. The illustrated P1–P6 plan is an interpretation of the perspective concepts, not an approved measured grid. Preserve the prominent pillar beside the fountain/store relationship and the columns flanking the stair approach without blocking entrances.

Next-pass handoff should include a dimensioned plan, a cross-section through both track edges and central concourse, and forward/reverse cameras matched to the approved concept views. Annotate original structure versus survivor additions. Measure playable width between barrier faces, clear width between shop/info fronts, floor-to-ceiling height, partition height, pillar spacing and passage widths separately. Low-eye-line captures supplement the concept-camera comparisons rather than replacing them.

**Proposed trial dimensions, not user-approved:** occupied concourse width 16–18 m rather than 26 m; length 22–24 m rather than 30 m; original ceiling 6–7 m; improvised partitions approximately 2.4–2.8 m. Determine the separate track-strip widths from the platform section. Tune these together with the existing DuckPlayer and twelve-player staging needs. Do not resize the rig to force a fit.

Confirmed follow-up choices: provide occasional narrow glimpses of rails and platform edges through the side barricades (option 1A). Keep the overhead station volume mostly dark, with work lights catching selected beams and pipes (option 2C). V2 includes recessed tracks, platform lips and yellow edge strips behind patched corrugated panels with approximately 17 cm viewing slits. A continuous see-through wire guard closes the track boundary up to the ceiling, including above climbable panels. Small uplights catch selected original structure above the low shelters. The exact clustering and dimensions remain tunable.

## V2 measured layout

Coordinates use metres, with Y up and positive Z toward Platforms. The fountain is at the origin. The occupied floor extends X -9 to +9 and Z -13 to +11. Inner sheet faces lie at X ±8.585, giving 17.17 m between barricade faces. Track centres lie at X ±10.45; outer tunnel walls are at ±12.15. The original structural envelope is wider than the occupied refuge.

Six 0.65 m square pillars stand at X ±3.4, in three rows at Z 5.5, -0.8 and -7.1. P1/P2 are the far row, P3/P4 the fountain row, P5/P6 the arrival row; odd numbers are on the left (negative X). Row spacing is 6.3 m, transverse spacing 6.8 m, and clear space between paired pillars is 6.15 m. The 3 m fountain leaves 1.575 m between its sides and the nearest pillar faces. Validated circulation routes run at X ±2.5 around it.

Station info is centred at (-5.65, 0, 3.4), yaw -65 degrees; supplies at (5.7, 0, 2.4), yaw +70 degrees. Their open fronts turn toward the central space and arrival approach. Supplies retains a separate side opening. Because both bays are angled, front-to-front clearance varies along their length; use actual prefab outlines rather than a single nominal hall width. Platforms sits at Z 9; its boarding trigger spans Z 5–8.6. November 11: the keypad was removed; the wall beside the gate now carries the pause-menu private-room hint, and the gate holds the private Launch Expedition button. Spawn is at (0, 0.2, -6.7); sealed surface stairs occupy the opposite end.

Section dimensions: floor Y 0; recessed rail heads approximately Y -0.77; ballast top Y -1; salvage barriers about 2.6 m; shelter walls 2.8 m and canopy tops 3.15 m; ceiling underside 7 m. This leaves 3.85 m above the shelter roofs. Original crossbeams lower the local clearance to roughly 6.325 m. The full-height mesh is visually open, unlike the lower metal sheets. Pillars, beams, platform edges and tracks represent original subway structure; corrugated shelters, bracing, barricades and work lights represent survivor additions.

## Gameplay and authoring (current blockout)

The existing RailWorld, departure collider, countdown text, player prefab, GameManager and scene flashlight are retained. The departure zone moves to the gate. (The physical keypad that once stood beside it was removed on November 11; see `RailLaunchButtonBuilder`.) Decorative Create/Join doors/signs are absent. The supplies stock is visual placeholder geometry, not new purchasing functionality.

Old gray-box geometry is disabled, not deleted. The spawn anchor moves slightly forward of the stairs. The public boarding area is flush with the floor. `RailBoardingLiveChecks` now derives its approach/exit positions from actual trigger bounds instead of assuming the old platform location.

`RailSceneBuilder` installs the saved prefab whenever it rebuilds Compound. It does not regenerate or overwrite artist-edited prefab geometry. **Duck Rails > Compound > Create Subway Blockout** refuses to overwrite existing assets. **Install Saved Subway Blockout** reuses the prefab and reapplies gameplay placement. Whole-scene rebuilds still replace scene-level changes; preserve environment work in the prefab/modules.

## Validation

- Unity compilation succeeded. The existing session check suite passed.
- 55 V2 blockout checks passed: nested assets, missing scripts/materials, collision layers, all twelve spawn positions, routes around the fountain, both store openings, track guards at walking/climbing heights, viewing gaps, roof clearance, sealed stairs and retained keypad/boarding bindings. See [edit-mode evidence](validation/subway-compound-v2/editmode-checks.txt).
- One authenticated V2 desktop client using the real DuckPlayer keyboard simulation passed walk-on, empty-platform cancellation, re-entry, the full twenty-second countdown and transfer into a ready expedition. See [V2 live evidence](validation/subway-compound-v2/live-boarding.txt). This is not multi-client or worn-headset acceptance. The first test invocation was too early in session initialization and declined to run; the completed test began after the public room was ready. Existing XR/backend-cosmetics/voice warnings appeared at startup; no runtime error was returned by the Console query.
- Actual V2 geometry renders: [forward](../output/compound-blockout-v2/forward.png), [reverse](../output/compound-blockout-v2/reverse.png), [0.5 m eye line](../output/compound-blockout-v2/low-eye.png), [boarding edge](../output/compound-blockout-v2/left-boarding-edge.png), [upper volume](../output/compound-blockout-v2/upper-volume.png). These share the saved Unity geometry; their cameras approximate the concept directions and are not calibrated perspective matches.
- Current inventory: 274 renderers, 127 colliders, nine provisional shadowless real-time lights. Counts are not performance budgets or a performance pass. Baked lighting/probes and Quest profiling remain future work.
- Backend diagnostic inbox failed with DNS resolution unavailable. Existing local reports were inspected and left open. No report was resolved by this art task.

Re-run using **Duck Rails > Compound > Validate Subway Blockout** and **Render Subway Views**. The live boarding check requires one ready public-compound client in Keyboard mode.

## Remaining review

### Compound lighting sample — November 1

For the current cross-project workflow, bake/export commands, troubleshooting and asset ownership, use the [lighting workflow guide](block-lighting.md). This section records Compound-specific settings and iteration history.

The first baked-lighting sample is scene-owned in `Assets/Scenes/Compound.unity`. Existing fixture positions and intensities are retained; the nine environment lights are now Baked, with baked shadows. Flashlight objects remain realtime. This is an initial art/workflow sample, not Quest performance acceptance or final darkness tuning.

- Low cool ambient fill: RGB 0.075 / 0.085 / 0.11. Exponential-squared fog density 0.018, dark blue-gray fog color; ordinary fog does not create light shafts.
- Progressive CPU bake, 12 texels/metre, maximum 1024 atlas size, non-directional lightmaps, 32 direct / 128 indirect samples. November 1 brightness follow-up increases indirect intensity to 1.5 and maximum bounces from two to four at the user's request. Realtime GI is disabled; direct fixture intensities, ambient fill and fog are unchanged by this follow-up.
- Lightmap UV mesh copies and an emissive lens material live in `Assets/DuckRails/Compound/LightingSample`. Original SubwayV2 meshes/materials are unchanged. Scene prefab overrides reference the UV copies and bake flags.
- 342 conventional probes cover the concourse and bays at 0.3, 1.2, 2.4 and 4.5 metres, rejecting positions overlapping Environment colliders. Thin decorative geometry and doorway interpolation still need visual/headset review. Duck_Body uses Blend Probes.
- One 128-pixel baked, box-projected reflection probe covers the concourse. No realtime reflections or volumetric effects were added.

Use **Duck Rails > Compound > Lighting > Bake Sample** after moving static geometry or fixtures, and **Capture Sample** for fixed review views in `output/compound-lighting`. The apply command refuses to duplicate an installed sample. The original in-memory scene, including unsaved user changes, was backed up under `tmp/compound-lighting` before setup.

**Authoring limitation:** do not rebuild Compound using the scene builder to iterate this sample. Whole-scene rebuilds replace these overrides and lighting settings; reapply the sample and rebake after a rebuild. Normal scene/prefab editing is supported, but moved geometry invalidates the bake. Randomized expedition prefab loading was subsequently proven with the separate [lighting companion workflow](block-lighting.md).

The existing 55 subway geometry/binding checks passed after setup. The completed bake binds all 269 URP Lit environment renderers to one 1024 x 1024 lightmap, with 342 baked probes and an assigned reflection cubemap. Probe sampling shows warm illumination inside Station Info and low cool illumination at arrival. Forward, information-nook and 0.5 m eye-line captures were visually inspected; see `output/compound-lighting/after-forward.png`, `after-info.png`, `after-low-eye.png` and `validation.txt`. This remains darker and geometrically simpler than the concept render.

Diagnostic backend lookup failed with DNS unavailable; six open local reports were inspected and left open. Compilation completed; the final Console query showed a Unity Connect token-exchange network error, not a lighting compile/bake error. Standalone Quest visual quality, twelve-player Compound performance, moving-player probe transitions and flashlight overlap remain unverified. No Play Mode or headset performance pass was performed for this art sample.

Headset navigation, low-eye-line readability, fingertip reach, twelve-player crowding, and multiplayer transitions require review. Finished silhouettes, asymmetric repairs, map artwork, water treatment, texture refinement, lighting/shadows and performance remain pending. Side-track anchors and sealed geometry exist, but no new ambience clips or playback behavior were added. Store commerce integration is separate.

Concepts: [forward](../output/imagegen/duck-rails-compound-subway-v1.png), [blocked stairway](../output/imagegen/duck-rails-compound-subway-reverse-v1.png), [banked outdoor alternative](../output/imagegen/duck-rails-compound-outdoor-approved-v1.png).

November 1 bounce follow-up validation: rebake completed with indirect intensity 1.5 and maximum bounces 4; one lightmap and 342 baked probes remain. Saved matching views in output/compound-lighting/bounce-before-*.png and bounce-after-*.png. Visual comparison shows a modest lift near the lit areas; the remote corners remain dark. Current in-editor fixture placement was preserved. No headset performance validation was performed.

November 1 architectural fill follow-up: the user found the bounce-only result too dark relative to the concept. Added eight scene-owned Baked spotlights under Compound baked lighting sample/Baked architectural fill spotlights: two cool concourse/pillar washes (65 intensity, 15 m range, 115 degree cone), two warm stall-front fills (50, 10 m, 110 degrees), two warm platform-entrance washes (45, 9 m, 105 degrees), and two cool upward structure fills (30, 13 m, 120 degrees). All use baked soft shadows. These are art-directed fill sources without new fixture meshes. Existing lights, geometry and ambient/fog values were retained. The bake completed with one lightmap and 342 probes; forward and info-nook captures were visually inspected and show substantially improved surface readability. Compare output/compound-lighting/fill-before-*.png and fill-after-*.png. The scene now has 17 baked environment lights; these additions do not add realtime light rendering. Headset performance/visual acceptance remains pending. Scene backup before this pass is in tmp/compound-lighting. Fill objects are saved in the scene; Apply Sample Setup does not recreate them after a scene rebuild.
