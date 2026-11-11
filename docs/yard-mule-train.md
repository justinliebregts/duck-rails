# Yard Mule train art — November 6, 2026

## Blender integration — November 10

The user approved the reviewed model and authorized Unity integration. The
existing `DuckRailsTrain.prefab` identity, nested gameplay modules, scene bindings,
passenger carrier and network components are retained. Three new static mesh
sections replace the old shell/furnace/housing renderers; the original objects
remain disabled where needed for identity/reference preservation. Throttle,
horn, fuel text/segments/gauge surround, and radio/table remain separate. Paired
dash light strips are absent. All active train mesh renderers share a moving
light-probe anchor at train-local `(0,0.5,0)` to prevent per-piece probe seams.
This deliberately samples one location for cohesive ambient illumination;
target-headset lighting and frame-time improvement still require measurement.

`Assets/Editor/RailBlenderTrainImport.cs` installs the reviewed meshes from
`Assets/DuckRails/Train/Blender/unity-integration.json`. This explicit Blender
Z-up to Unity Y-up bridge preserves metre-scale positions, triangle winding,
per-corner UVs and original Unity material assets without relying on FBX axis
or embedded shader conversions. The source FBX is retained alongside the mesh
assets. A new URP near-black material supplies the furnace lip. Blender source
is `output/train-model/YardMule-Refined.blend`; run
`Tools/Blender/export_train.py` in that file through Blender MCP for subsequent
exports, then **Duck Rails > Art > Import Reviewed Blender Train**. Update the
archival FBX in Assets from the output FBX when revising the model.

Matching convex furnace colliders replace the disabled old box colliders;
supplemental cowl and stepped lower-nose collision encloses the new body without
closing the intake. Fire is lowered to follow the ember bed. The existing
insertion anchor and acceptance half-extents `(0.90,0.22,0.50)` are preserved,
as are particle budgets and smoke placement. Older checks assumed smaller
acceptance bounds; rejection assertions now follow configured extents. Train
navigation has been rebaked and all 489 boarding checks pass. Legacy Yard Mule,
cockpit and furnace builders refuse to overwrite an installed Blender train.

Validation: all 24 exact-geometry/material/integration checks, 43 modular checks,
cockpit/fuel-bar checks, drive/session/Week 2 checks, hold-button and fingertip
checks pass. Isolated Play Mode passes six actual coal-collider entries, a
gravity-driven throw, transformed acceptance and clear sightlines. Expedition
inherits the shell and retains its furnace/fuel-gauge bindings. See
`docs/validation/blender-train/integration-checks.txt`, installed Unity renders
in that directory, and `docs/validation/front-furnace/checks.txt`.

The first actual two-client run passed stopped/moving deposits and exact fuel
agreement, then failed a pickup immediately after handoff. Its evidence is
retained as `coal-first-main.txt` / `coal-first-remote.txt`. The fixture now waits
for `PhotonMasterHandoff.LocalAvailable` before attempting the first grip;
ordinary gameplay is unchanged. Subsequent fixture repeats missed thrown coal
and timed out (`coal-second-*` and `coal-third-*`); changing its high release aim
to the middle of the downward-enlarged aperture did not establish a full pass.
These failures remain recorded, not treated as successful regression evidence.

With two real authenticated clients still connected and ownership settled, the
non-master launched an owned coal chunk from outside the mouth through ordinary
physics: it was consumed once and fuel rose exactly from 50 to 62.5. The retained
drive component's GO action moved the train 56.6 m at 4 m/s with fuel consumption;
STOP returned it to 0 m/s. Both installed horn/radio buttons published live hold
leases and released them; horn audio played and the radio station speaker was
present. See `settled-live-checks.txt`. This verifies those live component paths,
not a complete handoff regression, headset grip/reach or voice intelligibility.

The user reported Unity crashing/reopening during the final cleanup/preview
request. The prefab, meshes, materials, graph and above evidence were already
saved and the coal fixture had been disarmed. Crash cause is not established.

## Blender model review — November 10

User authorized a Blender cleanup of the existing layout: taller furnace aperture,
thinner side exhaust, and closed dash/firebox gap. Radio/table and all interactive
controls/display geometry must remain separate; remove the two small dash light
strips. The plain dashboard housing remains structural bodywork.

Review files: `output/train-model/YardMule-Refined.blend` (packed textures, hidden
original reference and editable parts), `YardMule-Refined.fbx` (three static mesh
sections only), `train-overview.png`, `dashboard-closeup.png`, and
`model-checks.json`. Unity was used only to read/export the source prefab before
the user requested Blender-only work. No Unity scenes, prefabs, collisions,
interaction anchors, navigation, or builders were changed by this pass.

The measured aperture vertical clearance increases from 0.4791 m to 0.5989 m
(25%), downward to preserve the 0.43 m upper surface and existing sightline.
The hollow stack shrinks from 0.48 m to 0.24 m wide/deep, retaining its height;
the connecting duct now reaches the stack. A fitted cowl closes the roughly
0.28 m dashboard/firebox break and connects the side cheeks. Export contains
body, canopy/spotlight, and wheels; controls and radio are excluded. Original
geometry is retained only in hidden reference collections, not the FBX.

Blender geometry checks passed for finite coordinates, nondegenerate triangles,
aperture ratio and excluded objects; textured overview and dashboard close-up
were inspected. This is a review model, not an installed or performance-validated
replacement. Unity light-probe behavior, FBX axis/material mapping, revised coal
collision/acceptance, effect placement, navigation and target-device performance
remain untested. Do not run legacy art builders over a future Blender integration
without first adapting their ownership of generated geometry.

November 10: the train safe module is removed. **Build Yard Mule** no longer searches for `TrainSafe` or shifts it inward. Furnace and radio placement is unchanged.

## Front-only furnace — November 6

User selected the exterior front-nose coal opening **only**, for playtesting. This supersedes the earlier side-mounted furnace and proposed secondary onboard opening. Keep the existing low cockpit controls. No stop-only rule or automatic braking is added: physical access determines when a player can fuel it.

`RailFrontFurnaceArt.Build()` / **Duck Rails > Art > Build Front Only Furnace** authors the nested `Furnace.prefab`, preserving its existing module, insertion anchor and particle identities. The module sits at train-local `(0,0,5.08)` with a closed rear and one forward opening. The shell generator authors separate nose cheeks rather than a convex collider spanning the aperture. The low nose top is 0.43 m above the deck, below the 0.5 m reference eye line; the front bumper extends to approximately Z=5.98. The right exhaust rises outside the canopy. Existing dashboard, radio, controls and fuel protocol remain in use.

Acceptance half-extents are `(0.65,0.20,0.22)` m inside the chamber. Fire remains a separate local particle effect (maximum 32); smoke retains its 24-particle budget. Mesh textures use square 32 texels/m and point filtering. Ember strips are recessed art, not pickups. Rebuild train enemy navigation after collision changes.

Validation: [isolated Play Mode checks](validation/front-furnace/checks.txt) pass six full-size coal-collider entries, a gravity-driven throw, rejection at the old onboard position/outside the chamber, eight transformed acceptance poses, three clear 0.5 m sightlines and 1,008 furnace UV edges. Existing 45 modular checks, cockpit checks and 6,846 shell UV edges pass. Offline navigation rebake produced 1,929 nodes / 17,490 edges and passed 489 boarding checks. The MCP bake request timed out, but Unity completed it and the saved result was verified. Expedition was restored in Edit Mode with its original insertion binding pointing into the relocated furnace.

Actual Unity previews: [exterior](validation/front-furnace/exterior.png), [mouth and fire](validation/front-furnace/mouth.png), [driver view](validation/front-furnace/driver.png). [Approved comparison](validation/front-furnace/approved-front-only-concept.png), option B. Preview generation also reported the existing unsupported bullet character in the safe's CutePixel text; the furnace introduces no text. Target-headset reach, aiming, ground-level visibility, departure timing and real-client moving fuel acceptance require playtesting; local collider checks do not establish those outcomes. Run **Duck Rails > Train > Validate Front Furnace** from Edit Mode for the isolated physical tests; it temporarily opens an empty Play Mode scene and restores the saved scene setup afterward.

## Integrated cockpit — concept 2 installed

User approved [concept 2](validation/yard-mule/cockpit-concept-approved.png) for 3D. The existing `CockpitControls.prefab` now contains a 3.52 m wide shallow charcoal dash, yellow perimeter and central brow, and two small emissive markers. Both decorative side handles and all six handle colliders were subsequently removed at user request. The gauge/horn plane slopes 25 degrees down toward the driver. The larger numeric gauge retains its original assembly/expedition reference; a ten-segment bar reads the same shared fuel value, with 40 fuel as full capacity. Ending fuel above capacity clamps the bar to full. The throttle stays upright with its original motion/grip logic; the existing horn button tilts with its contact volume and press direction. Legacy tabletop/supports are disabled. Furnace, radio, deck, shell and equipment placements are retained.

Added art is 264 triangles across three renderers sharing one material and two 256-square point-filtered atlases (albedo/emission). Square source texels remain 32/m, including the slopes; broad low-contrast texture patches match the shell. The moving emissive surfaces add no Light components and do not contribute baked GI. A convex housing collider provides the touchable dash surface; floor space and the 0.5 m forward eye line remain clear.

`RailCockpitArt.Build()` / **Duck Rails > Art > Build Integrated Cockpit** edits the nested controls in place, retaining the gauge/control identities. `RailCockpitArtChecks.Run()` checks geometry, UVs, fuel bar boundary values and source-mesh isolation; `Preview()` captures the assembly. After collision changes, rebake train enemy navigation offline. The installed graph was rebaked to 2,146 nodes / 19,934 edges with all rear/side openings connected.

Verified in Unity: live Expedition inherits the dash; 792 UV-edge checks pass (worst relative error 0.00002344), 45 modular-train checks pass, and 489 boarding graph/packet/authoring checks passed for the initial installation. After handle removal, cockpit checks and all three boarding-opening connectivity checks pass on the updated bake. Fuel-bar instance checks cover zero, partial, full, negative and over-capacity values. [Cockpit checks](validation/yard-mule/cockpit-checks.txt), [navigation](validation/yard-mule/cockpit-navigation.txt), [boarding regression](validation/yard-mule/cockpit-boarding-regression.txt), [interior render](validation/yard-mule/cockpit-integrated.png), [duck-eye render](validation/yard-mule/cockpit-eye.png). These are editor/asset checks; physical headset reach, moving hand contact, live multiplayer fuel presentation and device performance remain unverified.

## Continuous bumper stripes

The hazard bumper's top UVs now unfold from the front edge, so the diagonal stripes continue across the corner without a phase jump. Square texel density and geometry are unchanged. Rebuilt the shell and verified matching UVs at both shared edge corners (maximum difference 0.00000003 UV units); all 6,702 UV-edge checks pass. [Unity preview](validation/yard-mule/bumper-stripes-aligned.png), [seam check](validation/yard-mule/bumper-seam-check.txt).

## Centered front grille

Raised the three nose bars by 0.05 m to center them in the visible yellow panel above the bumper. Increased bar thickness from 0.055 m to 0.07 m, retaining their spacing and following the nose slope. Rebuilt only the shell; the train assembly prefab is byte-identical. All 6,702 UV-edge checks pass with square texels and unchanged triangle count. [Unity front preview](validation/yard-mule/grille-centered.png).

## Sharp bumper and four-corner overlap fixes

User reported muddy hazard stripes and z-fighting at both front and rear corners. Inspection found the main atlas had an Android override importing it at 512 × 512 with compressed format 52, despite the 1024 source and point sampling. A dedicated **32 × 32 RGB24 uncompressed, point-filtered repeating hazard texture** now keeps the stripe edges crisp on Android and Standalone. Mipmaps remain for distance stability. The existing paint/wood import settings are retained; this adds one small material/renderer rather than increasing the full atlas's device memory. Authored main-atlas UV density remains 32 source texels/m; the Android half-size override reduces its effective density to 16 texels/m. Hazard stripes retain 32 texels/m on the inspected Android import.

The nose is now one closed wedge without the duplicate backing-box side faces. Its side planes clear the deck-edge strips by 8 mm. Those strips end at Z=±4.97 m instead of sharing the deck end planes at ±5 m, resolving both rear corners as well. A separate simple box collider retains the bumper's collision envelope.

`RebuildShell()` updates the nested art asset without rewriting the main assembly or equipment placements; byte comparison verified the main train prefab remained unchanged. Current shell: **2,234 triangles, ten renderers, two materials, one active light**. Compilation, all **6,702 triangle-edge UV checks**, and all 45 modular-train checks pass. Imported hazard texture verified as 32 × 32 RGB24 with point sampling. [Four-corner close-ups](validation/yard-mule/four-corners-fixed.png) (front pair above, rear pair below), [whole model](validation/yard-mule/sharp-bumper.png). These are editor checks, not headset motion/performance validation.

## Softer paint and wood texture pass

User rejected the isolated, high-contrast yellow flecks and harsh deck grain, supplying a broader, softly shaded pixel reference. Yellow now uses larger connected patches with close tonal steps. Wood uses subdued plank variation, broad patches, and lighter seams instead of speckled grain. Point filtering and the square 32-texel/metre UV layout remain unchanged; softness comes from the painted colors and patch shapes, not stretched UVs or blurred sampling.

Applied with **Duck Rails / Art / Refresh Yard Mule Textures Only** (`Retexture()`), which rebuilds the atlas without saving meshes or prefabs. Byte comparison confirmed all shell mesh assets and both train/shell prefabs were unchanged. Compilation and all 6,720 UV-edge metric checks passed. [Unity close-up](validation/yard-mule/soft-texture-closeup.png), [whole shell](validation/yard-mule/soft-paint-and-wood.png). This is an appearance revision, not additional headset/performance acceptance.

## Narrower train and square pixels — installed

The user approved the narrower render and requested unstretched square pixels and a flat textured floor. Installed deck is now **4 × 10 m**, with unchanged heights. Width is baked into shell vertices before UV projection; the assembly and shell keep unit scale. The physical deck, rail/cockpit collision groups and passenger cabin match the narrower footprint. Full-size furnace/radio/safe modules move inward without scaling their interaction anchors. Wheel centers align with the existing route rail centers at X=±1.5 m; the track is unchanged.

Each planar face gets an orthonormal UV projection at **32 texels per metre on both axes**, including slopes and wheel facets. Face islands are packed with four-pixel gutters into 1024 × 1024 albedo/emission atlases. Point sampling is retained. The larger atlases are the cost of preserving one shared material and consistent square texels; device texture-memory/frame-time profiling remains pending.

The visible wood floor is one quad (four vertices, two triangles), with all plank seams, end joints and grain in the texture. It retains the existing solid deck collision slab. Current shell: **2,240 triangles, nine renderers, one material, one active spotlight**.

Validation: all **6,720 triangle edges** match the intended UV/world metric, worst relative error 0.0000795 (under 0.008%). This checks both scale and distortion on every triangle, not just axis-aligned faces. Deck dimensions/topology, collision width, module scale and non-contributing GI checks pass. All 45 modular-train checks pass again. Live Expedition inherits the 4 × 10 m visual/collision deck and one active light without scene overrides. See [UV evidence](validation/yard-mule/narrow-uv-checks.txt), [modular checks](validation/yard-mule/editmode-checks.txt), and [actual Unity render](validation/yard-mule/narrow-square-pixels.png). Headset and real-client acceptance remain pending.

## Earlier width study

User requested a 20% width reduction and dimensionally accurate renders before further modeling. The [three-quarter view](validation/yard-mule/narrow-study/narrow-three-quarter.png) and [orthographic top view](validation/yard-mule/narrow-study/narrow-top.png) rendered a temporary copy at X scale 0.8. The subsequent approved [beauty concept](validation/yard-mule/narrow-beauty-approved.png) guided the installed revision above; it is generated concept art, not a mesh capture.

The user selected the yellow/charcoal Yard Mule, then approved a single enlarged roof spotlight and warm emissive safety markers. This supersedes the earlier teal shell, nose headlight, cockpit side lights and visible chair. The furnace, radio, controls and safe remain independent gameplay modules, outside the shell asset.

## Authoring

`Assets/Editor/RailYardMuleArt.cs` authors ordinary saved meshes, one shared URP material and two 1024 × 1024 point-filtered pixel atlases. There is no runtime procedural generation. Run **Duck Rails / Art / Build Yard Mule** to rebuild the shell and install it in `Assets/DuckRails/Train/Prefabs/DuckRailsTrain.prefab`. `Validate()` checks the saved UV metrics and geometry; `RailModularTrainChecks.Run()` validates retained gameplay links and clearance.

`Assets/DuckRails/Train/YardMule/YardMuleShell.prefab` is independently usable for shell review without gameplay equipment. Nine mesh groups keep the chassis, single-quad deck, wheels, rails, supports, canopy, nose, markers and spotlight editable. Pixel wear, wood grain and hazard stripes are authored into textures; broad silhouette details use geometry. These are new textures matching the low-poly pixel-art direction, not copies of the Quackville atlas.

Current deck footprint is 4 × 10 m, deck top Y=0. Rail tops remain about 0.38 m, posts 0.40 m. Cockpit supports move inward with the width reduction; wheel centers are X=±1.5 m. The faceted canopy reaches 1.90 m; the spotlight is approximately 0.75 × 0.58 m after the width reduction and tilts down 12 degrees. The sloped nose stays below Y=0.20 m for the approximately 0.50 m duck eye line. Bumper extends to about Z=5.39 m; rear stays open. Dimensions are local metres.

The prior shell's renderers are disabled, retaining original collision identities and passenger/cargo components while narrowing their bounds. Three supplemental convex colliders cover the new canopy, nose/bumper and spotlight. Legacy lighting and the chair module are disabled; their objects/references are retained for compatibility. Furnace, radio and safe move inward at unit scale; their functionality and linked anchors are preserved. Changes are made in the shared prefab, allowing existing scene instances to inherit them while retaining unrelated overrides.

Safety markers use an emission map on non-static renderers that do not contribute GI. They have no Light components and do not bake illumination into scenery. URP requires the material's BakedEmissive flag to retain its emission shader keyword; this does not make these moving, non-static renderers bake light. Do not mark them Contribute GI. Only the roof spotlight lights the environment: shadowless, 32 m range, 65-degree outer cone. These are initial tuning values, not measured headset performance claims.

## Initial wide-shell validation (historical)

Initial appearance reference: [concept](validation/yard-mule/approved-concept.png). The first wide shell had 2,450 triangles across eight renderers sharing one material. Unity compilation and 45 modular-train checks passed. The narrower revision and current validation above supersede those dimensions and statistics.

Unity mesh previews: [front](validation/yard-mule/front.png), [rear](validation/yard-mule/rear.png), [driver eye line](validation/yard-mule/driver.png). These isolate the shell and use preview lighting; they are not gameplay footage. Target-headset visibility, performance, natural boarding/grabbing, spotlight reach, and real-client moving-cargo acceptance remain separate from editor validation.

November 10 Blender review follow-up: furnace surround and chamber leading edges now follow the sloping nose face, removing the projecting horizontal hood. The taller aperture is retained; chamber fronts sit 8 mm behind the trim to avoid coplanar overlap. Updated packed Blender source and FBX, verified nondegenerate triangles, and inspected output/train-model/furnace-flush.png. Unity remains untouched.

November 10 lower-nose follow-up: enclosed the side/underside gap between chassis and front bumper in Blender, on both sides. Recessed center remains below the furnace floor; underside stays at the existing chassis bottom (-0.645 m). New enclosure is manifold, export triangles are nondegenerate, and output/train-model/enclosed-nose.png was visually checked. Updated Blender/FBX; no Unity operations.

November 10 ceiling-finish follow-up: furnace ceiling underside now uses Firebrick; front lintel and concealed ceiling front face use a dedicated near-black Blender material. Ceiling leading edge recessed another 25 mm to remove the visible yellow sliver. Low-angle furnace-underside.png reviewed; Blender/FBX saved. New black material needs mapping during future Unity integration. No Unity operations.

Post-restart verification passed in the confirmed Duck Rails editor: saved Blender integration, modular train bindings, cockpit/fuel display and 489 boarding checks. Exactly one drive/horn/radio control and all furnace references remain intact; coal fixture marker is absent. Editor remains in clean Login Edit Mode. See validation/blender-train/restart-checks.txt. The Play selector remains '4 Diagnostic Two Client'; the pre-test scenario SessionState was lost on restart, so no original selection was guessed. Console contains a startup PlayModeUserSettings serialization error ('You may not pass in objects that are already persistent'); no crash cause is established by this observation.

November 10 seam/rail correction: fitted both furnace jamb tops to the lintel underside, removing overlapping front faces; raised the bumper top 8 mm to separate it from the nose undersides. Lowered all side rail geometry and attached markers 75 mm into the deck edging, and matched the rail collision parent. Saved Blender source/FBX, imported exact geometry into the shared Unity train prefab, and rebaked the train crawlgraph. Blender front/side previews: output/train-model/front-seams-fixed.png and rails-seated.png. Integration, modular/cockpit checks and all 489 boarding checks passed. Gameplay bindings were preserved by the importer. No new multiplayer/headset run for this geometry correction. Boarding rebuild emitted existing missing bullet-glyph warnings for CutePixel SDF.

November 10 deck/cockpit follow-up: seated the wood deck at -0.03 m, exactly on the chassis boundary, and removed the concealed chassis top triangles to avoid coplanar faces. Extended canopy feet to the lowered surface. Matched the walking slab top to -0.03 m. Added a flat yellow cockpit furnace cover at forward coordinate 4.88 m, flush with the adjacent nose rear faces, plus matching collision; the internal firebrick and front opening remain intact. Saved Blender source/FBX and installed the shared Unity prefab. Integration, modular train, cockpit and 489 boarding checks passed after crawlgraph rebake. Verified collision coordinates explicitly. Visual evidence: output/train-model/deck-seated.png and cockpit-wall-fixed.png. No multiplayer/headset retest for this geometry change.
