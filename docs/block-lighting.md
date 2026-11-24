# Lighting workflow — Compound and expedition blocks

Last updated November 1, 2026. This is the main reference for the implemented lighting workflow. The user has visually reviewed the result and reports that lighting works. Compound, StartingStation, all twelve scavenging blocks and the bridge finale now have baked-lighting samples. This confirms the working approach; final art, darkness/flashlight balance and measured Quest performance remain open.

## What provides the lighting

November 4 authored atmosphere: Expedition gameplay preserves the Environment settings saved in `Assets/Scenes/Expedition.unity`; `RailExpedition` no longer forces night ambient/fog colors at run start. The current user-authored scene uses Skybox ambient intensity 1.3 and black Exponential Squared fog at density 0.1. Tune this scene's Lighting window and save outside Play Mode; isolated block bake scenes do not define gameplay atmosphere. Select Gameplay Lighting in Test Center to see those settings.

The successful ending still applies dawn after 14 seconds. It captures the current ambient mode/color and fog color immediately before that transition, switches to flat dawn ambient, and restores the captured values on another run or component disable in the same active scene. New scenes keep their own settings. Ordinary gameplay leaves live Lighting-window edits alone. Fog density/mode, skybox, ambient intensity, sun and reflections are not changed by this transition; baked lightmaps/probes still require rebaking to change their illumination.

Validation: Unity compiled the change and six direct editor checks passed for initial preservation, dawn, repeated dawn calls, latest-edit restoration, a second cycle, and untouched environment properties. The SendMessage-based editor harness emitted Unity `ShouldRunBehaviour()` assertions because it invoked a runtime behaviour outside Play Mode; the settings comparisons passed. The original scene values were restored afterward. No new full expedition or headset run was performed. Diagnostic backend retry succeeded with ten existing open reports; none was resolved by this change.

- **Baked lights and lightmaps:** permanent fixtures and architectural fill illuminate static surfaces, including baked shadows and bounce. Realtime GI is disabled. The environment fixture lights are Baked, not Mixed or Realtime.
- **Conventional Light Probe Groups:** baked illumination is sampled by moving subjects such as ducks, loose items and the train. Moving renderers should use Blend Probes and must not be included in static lightmap bindings. Our exported block payloads use these conventional probes; Adaptive Probe Volumes are not part of this implementation. An APV warning is not evidence that this conventional-probe workflow is unavailable.
- **Realtime lights:** movable flashlights remain realtime so their illuminated area follows the player. A probe bake does not replace a moving beam or its shadow/occlusion behavior. Realtime light count and shadows still need Quest profiling.
- **Fog:** ordinary distance fog supplies atmosphere and distance fade. It is not volumetric lighting and does not produce visible flashlight shafts. Keep runtime ambient/fog tuning in the gameplay scene/code; block bake-scene settings describe the isolated authoring environment, not a per-block runtime fog volume. Expedition's dawn transition also changes ambient/fog through `RailExpedition`.
- **Reflections:** Compound has one baked reflection probe for surface reflections. Reflection probes are distinct from the diffuse Light Probe Groups. No realtime reflections or volumetric effects were added in this pass.

The prefab problem is solved with a dedicated bake scene per block, followed by export to a small additive lighting companion scene. Gameplay still instantiates the normal prefab. The companion supplies native baked data without loading a second copy of its geometry or gameplay objects. Nothing is baked at runtime.

## Compound: bake the gameplay scene directly

1. Open `Assets/Scenes/Compound.unity` in Edit Mode. Edit the existing fixtures or the eight fill lights under `Compound baked lighting sample/Baked architectural fill spotlights`.
2. Use **Duck Rails > Compound > Lighting > Bake Sample**, wait for completion, and save the scene. Use **Capture Sample** for matching review views in `output/compound-lighting`.
3. Preserve the scene overrides, `Assets/DuckRails/Compound/LightingSample` assets and generated lighting data together. Compound uses normal scene-owned baked lighting; it does not use the block companion loader.

Current sample: 17 baked environment lights, one 1024 atlas, 342 conventional probes and one 128-pixel baked reflection cubemap. The bake uses 12 texels/metre, non-directional lightmaps, 32 direct/128 indirect samples, four bounces and indirect intensity 1.5. Low cool ambient fill and exponential-squared fog remain provisional. See [Compound lighting details and review history](subway-compound.md#compound-lighting-sample--november-1).

Do not rerun **Apply Sample Setup** over an installed sample or use the whole-scene builder to tune lighting. The setup command refuses duplicates; whole-scene rebuilding loses lighting overrides and the eight hand-authored fill lights. The setup command alone does not recreate those fills.

## StartingStation: bake, then export

1. Open `Assets/DuckRails/Lighting/StartingStation/StartingStation-bake.unity`. It contains the actual station prefab at origin, its eight permanent baked spotlights, and a separate Light Probe Group. Edit the prefab geometry/fixtures here; preserve its track contract, origin, rotation and scale. Probe positions belong to this bake scene.
2. Use **Duck Rails > Lighting > Starting Station > 2 Bake**. Wait for Unity to finish. There is no runtime bake.
3. Use **3 Export Lighting Companion**. This applies renderer bindings to the station prefab and writes `StartingStationLighting.unity`, a probe-only additive scene referencing the baked LightingData and textures. Export after every rebake or renderer change. Do not edit the generated companion directly.
4. The companion must remain enabled in Build Settings. The exporter adds it without changing Login/Compound/Expedition build indices. Authoring and proof scenes are not shipping entry scenes.

The initial preparation command is one-time and refuses to overwrite an existing bake scene. Original shared station materials are retained. One UV-unwrapped mesh copy is used by the primitive geometry. The bake uses non-directional lightmaps, 8 texels/metre (ground scale 0.25), 1024 maximum atlas size, 32 direct/128 indirect samples, four bounces and indirect intensity 1.5. Current output is two atlases and 367 probes. Warm lighting on the existing teal platform material appears greenish; this is not a finished palette decision.

## Other blocks: bake and export together

Every prefab in `Assets/DuckRails/Blocks` now has four permanent baked spotlights: two warm shelter lights on the left and two cool work lights on the right. The finale instead lights its approach, control area, bridge threshold and tunnel. This adds 52 sample lights across thirteen blocks. It uses the same non-directional bake settings as the station and conventional probes at multiple heights across each block.

1. Open `Assets/DuckRails/Lighting/Blocks/<block-id>/<block-id>-bake.unity`. Edit fixtures/geometry in this scene, with the prefab at the origin. Probe positions belong to the bake scene.
2. Use **Duck Rails > Lighting > All Blocks > Bake Open Block**. This asynchronously bakes and exports that block, including prefab bindings and its `<block-id>-Lighting.unity` companion. Choose **Bake Open Block And Keep Open** to reopen the original bake scene after export (also if export fails after switching scenes). Both commands save the bake scene before starting and refuse overlapping queued bakes. Wait for completion in the Console or `output/block-lighting/bake-progress.txt` before changing scenes.
3. After rebaking the bridge finale, use **Install Finale Lighting**. Expedition contains existing bridge objects and interaction references, so this transfers matching render bindings onto them without replacing the bridge.
4. Use **Validate Exported Catalogue** to verify all thirteen exports. **Bake Missing Samples** prepares/bakes missing companions; **Re-export Existing Bakes** refreshes all exports without rebaking.

The moving bridge hinge and variable spawn objects are excluded from static lightmaps and disabled as bake contributors. The exit rail and DAWN label are also outside this night sample. November 11 correction: the static far bank/tunnel floor is included in baked GI and exported lightmap receivers, with probe fallback disabled. A single probe sample on its large renderer had incorrectly spread warm tunnel light across the whole grass bank. Preserve localized lightmap illumination instead. Fourteen companions (station plus thirteen blocks) are enabled after the three gameplay scenes in Build Settings. Only seven load during a normal expedition: station, five selected stops and finale. Authoring and proof scenes are not build entries.

Do not run the general Week 3 gray-box rebuild to tune lighting: it regenerates prefab geometry/build settings and does not preserve this lighting authoring pass. Use the bake scenes and lighting menus. Geometry changes require another bake/export; do not edit generated companion scenes or texture bindings manually.

## Iteration and saved assets

November 11 visibility correction: bake-excluded renderers are hidden only during preparation/baking, with explicit prefab override tracking. Export restores their prefab visibility before applying data and saves the authoring scene with those objects visible. This prevents the finale floor/drawbridge from remaining invisible after export. Their receiver bindings are still excluded. **Re-export Open Block And Keep Open** refreshes only the current saved block's existing bake; use it only when lighting inputs have not changed. The finale correction passed a prepare/export/reload cycle plus prefab and Expedition visibility checks; see `output/level-polish-batch/followup/tunnel-floor-checks.txt`.

For more light in a corner, first adjust nearby baked fixture coverage or add a restrained baked fill in the authoring scene, then rebake and export. Increasing bounce alone only modestly brightened Compound's remote corners; the added baked spotlights made a larger readability improvement. Increasing probe density does not brighten static walls: probes sample the baked illumination for moving subjects.

Atlas-integrated blocks (Convenience Store, Skyscraper, Container Yard, Farm Silos) refuse **Bake Open Block**/Prepare/Export. To change or add only their baked lights, use the [lights-only rebake workflow](atlas-lights-only-rebake.md). Anything else still needs the full atlas reconcile/regenerate path.

Rebake after changing static geometry, its lightmap UVs, lighting-relevant materials, fixture position/color/intensity/range, or probe positions. For a newly added static mesh, prepare lightmap UVs and Contribute GI/Receive GI settings before baking; **Bake Open Block** uses the existing authoring scene and does not rerun its initial mesh preparation. Keep moving geometry out of the static bake. Rails and sleepers are now collider-free visual geometry but remain valid lightmap receivers; collider removal alone did not require rebaking their unchanged visible surfaces.

Save/version the entire affected lighting folder, including authoring scene, generated companion, Lighting Settings, LightingData asset, lightmap textures, UV mesh copies and every `.meta` file. Include updated prefab bindings and Build Settings; include `Expedition.unity` after installing updated finale bindings. Generated textures alone are insufficient. Screenshots and proof logs live in the corresponding `output/*lighting*` folders; `tmp` backups are not required runtime assets.

Save other scene work before running export/proof menus: these tools open and replace the active editor scene. After a rebake, validate the export and inspect the result in the normal Expedition flow as well as the isolated bake scene.

## Troubleshooting

### Starting Station fixture membership follow-up — November 16, 2026

Inspection found 63 baked fixture Lights but only 62 serialized `bakedFixtures`
references. `Env/City station v3/Baked lights/Supplies corner bulb (3)` was missing
in the station prefab and inherited Expedition instance, allowing it to escape
the existing gameplay suppression. Its bake-scene output was already baked.
Refreshed only the prefab fixture membership (one added reference); Expedition
and the bake scene inherit all 63 without scene overrides or scene saves.
All 95 exported texture/UV entries agree between prefab and Expedition; all 95
bake-scene native renderer UV transforms agree with the export. Saved metadata
has exactly 63 unique valid fixture references and the companion is enabled in
Build Settings. No rebake, light tuning or Play Mode performed. This repairs the
specific remaining fixture; it does not establish the cause of every reported
visual difference. Edit Mode still does not load the gameplay companion.
Supplies-room softness and station appearance remain queued for playtest under
**Baked fixture double lighting**.

### Finale hard light cones — November 16, 2026

User selected the softer `bridge-finale-v1-bake.unity` appearance as the target.
Read-only inspection in the verified Duck Rails editor found all 1,746 exported
renderer texture/UV bindings matched the open bake, and Expedition's finale
bindings matched the prefab. The 24 fixture lights were `Baked` with
`bakingOutput.isBaked=true` in the bake scene, but false and enabled in the prefab
and Expedition. Native baked-light output is scene-owned; transferring renderer
bindings does not transfer that fixture state. Unbaked editor previews therefore
show direct fixture lighting rather than the baked appearance.

`RailBakedBlockLighting` now suppresses its enabled baked-only lights when entering
the gameplay companion lifecycle, preventing direct fixture contribution over
the maps/probes. It restores only those lights on disable. Realtime/mixed lights,
previously disabled lights and nested lighting owners are excluded. This shared
owner correction also applies to the other companion-lit blocks. Authoring
fixtures, bake inputs, textures and environment settings are unchanged. It does
not add companion loading to Edit Mode: prefab/Expedition previews still differ;
use the bake scene for baked appearance review.

Validation: compilation passed; isolated Edit Mode checks using the actual finale
prefab passed baked suppression, realtime/mixed preservation, previously disabled
state, two restore/repeat cycles, repeated suppression and all 1,746 intact
bindings. Console error query was empty before the lifecycle checks; afterward
Unity reported `ShouldRunBehaviour()` assertions from the test harness calling
runtime methods through SendMessage in Edit Mode, although state assertions passed.
No Play Mode
or reports inspected. Runtime visual confirmation and atmosphere differences
remain queued in **Baked fixture double lighting** in `playtest-queue.md`; this is
not yet evidence of a matching gameplay screenshot.

- **Stationary imported prop ignores baked light:** on its stationary mesh object enable **Contribute Global Illumination**, choose **Receive Global Illumination: Lightmaps**, and ensure the mesh has secondary/lightmap UVs (FBX Model Import Settings > Generate Lightmap UVs, or a dedicated unwrapped mesh copy). Bake/export the block and install finale bindings if applicable. Do not mark runtime labels, coin effects or moving parts static. The finale gold crate uses a dedicated mesh copy in `Assets/DuckRails/FinaleFort/Polish/` so other uses of the shared FBX are unchanged.

- **Prefab looks unlit in isolation:** inspect its bake scene, or run the normal Expedition/proof flow so its companion can load. Native baked data is owned by the companion scene, not by the prefab alone.
- **Static surfaces have missing/wrong lighting:** check the companion's enabled Build Settings entry and `RailBakedBlockLighting.Failure`; rebake/re-export after geometry or UV changes. Do not hand-edit lightmap indices, which change as additive scenes load/unload.
- **Static scenery is lit but a moving subject is dark:** check the subject's probe usage/anchor and coverage at its position and height; confirm the companion is ready. Inspect transitions through doors and between blocks. Adding more static lightmaps does not fix missing probe coverage.
- **A route never becomes ready:** inspect each loaded lighting owner's `Ready`/`Failure`, missing scene/texture/renderer references, and the translation-only placement contract below. Do not bypass the readiness gate to hide a failed load.
- **Finale lighting differs from its bake scene:** run **Install Finale Lighting** after exporting its new bake. Its live geometry is separate from the prefab and needs matching bindings.

## Runtime ownership

`RailBakedBlockLighting` lives on the prefab and holds renderer references, baked texture references and UV scale/offset values. `RailBlockLightingLoader` loads a separate instance of the companion scene for each block, finds the registered atlas indices, and restores those renderer bindings. Unity owns the additive texture/probe registration; the loader does not replace the global lightmap array.

The loader calls `LightProbes.GetInstantiatedLightProbesForScene` and translates that scene's baked positions using `SetPositionsSelf`, then calls `Tetrahedralize`. This preserves baked spherical-harmonic coefficients while keeping source assets and simultaneous block instances independent. Probe clones are destroyed after unloading. `LightmapSettings.lightProbes` can be null for an unbaked active gameplay scene despite valid additive probes, so it must not gate tessellation or serve as the total probe count.

The supported placement contract is translation-only, unit scale. Rotation/scale is rejected explicitly. Route assembly can translate an already loaded block (notably the finale): readiness closes until the loader moves its native probes and tessellates again. This supports occasional route placement, not continuously moving lit platforms. Different lighting companions can coexist, but mixed directional/non-directional bake modes, overlapping blocks and arbitrary rotations are not supported by this proof. Use the same non-directional bake settings for future blocks. Baking blocks independently does not bake inter-block shadowing or bounce; use dark boundary probes and validate seams.

Disabling/destroying an owner releases its companion scene. Loads are serialized; an owner destroyed during loading is cleaned up after the async operation completes. Remaining renderer indices are rebound after loads/unloads. `RailRoute.Ready` waits for the station, every selected stop and the finale. `EnsureReady` retains the assembled route while lighting loads rather than rebuilding it each frame. Missing companion scenes or invalid bindings report a failure and do not silently declare the block ready.

## Reproducible validation

Use **4 Create Offline Proof Scene**, then Play. The proof scene deliberately suppresses automatic editor sign-in with an inactive LoginFlow sentinel. It uses the real station prefab, plus renderer-only copies of the existing duck, coal and train. Those visual copies are not locomotion, grabbing or network simulations.

`output/starting-station-lighting/runtime-proof.txt` records the completed Play Mode checks:

- Origin load binds the prefab's baked renderer UV transforms and textures.
- Simultaneous origin and translated (240, 0, 320) instances have independent native probe data; three corresponding lighting samples agree within 0.003 per SH coefficient.
- Unloading the first instance leaves the second's lightmaps and lighting valid.
- Unloading both returns probe/companion counts and lightmap count to baseline.
- Reload at the real gameplay offset (0, 0, -10) restores exactly one payload; entering the lit platform changes sampled illumination.
- Duck, train and coal visuals traverse the station; the duck renderers use Blend Probes. Captures show the subjects and flashlight off/on views over lightmapped surfaces.
- Final unload and cancellation during a pending load leave no companion scene or probes.

The updated Week 3 checks pass, preserving station/route alignment and the three gameplay scene indices while requiring the lighting companion in the build. The first offline attempt was invalidated by automatic sign-in; the rerun found and fixed the active-scene probe-count/tessellation assumption before passing. These failures were not counted as successful validation.

The normal authenticated single-editor private sandbox also reached a ready Expedition with a ready route and station lighting. The real train was driven through its normal grip-lease command path for 30 seconds: position (0, 1, 70) to (0, 1, 185.72), 115.72 metres. Sampled red L0 probe illumination at the train dropped from 0.1486444 to 0.003620661 as it left the station. The sandbox was stopped through its test runner. See `live-expedition.txt`, `live-station.png` and `live-expedition.png` in the output folder. This proves integration in the assembled five-block expedition on one editor client; it does not prove physical hand interaction, multiplayer late join, or headset quality. Expedition's existing global ambient/moonlight/fog were retained.

### Full catalogue evidence

Use **All Blocks > Create Catalogue Proof Scene**, then Play. This uses the actual `RailRoute.Build` with seeds 0, 7 and 4, covering all twelve scavenging blocks across three assembled five-stop routes. It suppresses automatic editor sign-in and writes `output/block-lighting/runtime-proof.txt` and per-block screenshots.

The completed run verified all renderer texture/index/UV bindings, corresponding translated probe samples against authoring data, readiness during finale relocation, exclusion of the moving bridge from static bindings, replacement of loaded routes, and full teardown to the original scene/probe/lightmap counts. Each route had thirteen lightmaps and 2,394–2,403 native probes. All thirteen exported prefabs also passed the four-light/binding/build-entry checks in `export-checks.txt`.

A separate authenticated private single-editor sandbox reached a ready Expedition with all seven lighting owners ready and every binding validated. That selected route had thirteen lightmaps and 2,397 probes; the finale was correctly placed at Z=950. See `output/block-lighting/live-expedition.txt`, `live-expedition.png` and `live-finale.png`. The sandbox was stopped through its test runner. These samples retain Expedition's existing global ambient, moonlight and fog; they do not set the final darkness or flashlight balance.

## Limits and follow-up

No Quest GPU/frame-time/memory budget, eight-player acceptance, adverse-network late join, or finished visual quality is claimed. Native loading and synchronous tessellation occur at load/unload or placement changes, not every frame; measure the complete route's cost on Quest before increasing lighting density. Large assemblies may need batched loading/tessellation. Probe density at building boundaries, seams and moving-subject quality still need headset review. Baked lighting on static meshes is not a substitute for realtime flashlights or their occlusion decisions.

The diagnostic inbox query failed with DNS resolution unavailable; six pre-existing open local reports were inspected and left open. No diagnostic report was resolved by this work.

## Storm sky night to dawn — November 22

User-confirmed (Justin, November 22): replace the procedural sky with the Duckz animated storm sky in Expedition and Login; night dark olive, a bit darker than Duckz; dawn just brighter and warmer (still overcast) with a warm sunrise glow; add a distant mountain ring along the route. Compound is unchanged (plain dark background, no skybox). Port details: [duckz-ports.md](duckz-ports.md#storm-sky-group--06f5f5b0-8628bc4c-01f8e3e4-ac6d5319-6a6f5a2f).

- Sky: `Assets/Shaders/AnimatedStormSky.shader` (`Duckz/Animated Storm Sky`, one pass, two samples of the 256² `StormCloudNoise.png`). Rails adds an optional sunrise glow (`_SunGlowColor/Strength/Size/Direction`); at strength 0 the output equals the Duckz shader. `ExpeditionSky.mat` (Expedition) and `Quackville_Skybox.mat` (Login) keep their GUIDs, so no scene was resaved. Both start at the night palette (exposure 0.6 versus Duckz 0.8).
- Transition: `RailAtmosphere` now requires the storm sky (it previously gated on `Skybox/Procedural`; swapping the material alone would have silently disabled the whole fog/ambient/sun transition). It clones the sky once per run and lerps zenith, horizon, cloud edge/shadow, exposure and glow between three endpoints in `Assets/Resources/Rails/Atmosphere.asset` (`nightSky`, `preDawnSky`, `dawnSky`, editable in the Atmosphere Tuner). The sky below the horizon is always set to the current fog colour (divided by exposure in linear space), so the sky seen past the 96 m-wide block edges matches fogged ground at dawn. Glow sits on the existing 125° sunrise bearing, 0.05 above the horizon. Fog, ambient, sun and timing curves are unchanged.
- Mountains: Duckz `DistantMountains.fbx` (three ridge rings 2.6–3.7 km, 2,304 triangles, one unlit draw, depth pinned just inside the far plane, no depth writes) in `Assets/Resources/Rails/DistantMountains.prefab`. `RailAtmosphere` instantiates it (DontSave) and recolours a runtime material copy per stage; `RailDistantMountains` centres it at X 0 between the starting station and the finale end and overrides bounds so it is never culled. It works with the 120/175 m player far plane because opaque geometry always writes nearer depth.
- Not done: the old `Quackville_Skybox.png` panorama was removed (`15c0e0233`); the Expedition lighting data's baked default reflection cubemap still comes from the old procedural sky until the next rebake. Duckz's isolated sky validation harness was not ported.

November 23 sunny dawn (Justin: dawn looked too foggy/overcast and artificial; make it really sunny). Dawn endpoints in `Atmosphere.asset`: fog (0.9, 0.8, 0.62) at density 0.005 (golden haze, still ~54% fogged at the 175 m far plane); sky blue zenith (0.3, 0.47, 0.72), warm horizon, cloud coverage 0.42 / opacity 0.8, gold cloud edges, glow strength 1.3, sun elevation 14°, flat ambient 0.5. Sky phases gained cloud coverage/opacity, sun elevation, realtime sunlight colour/intensity and ambient. The scene's sun ("Moonlight") is a Baked-only light, so changing it never lit anything at runtime; `RailAtmosphere` now adds a runtime Realtime directional "Dawn sun" (no shadows, DontSave) that exists only while dawn sunlight is above zero (intensity 1.3 at dawn) and lights the night-baked lightmapped scenery directly. Quest cost: one per-pixel main light without shadows during the dawn ending only. Validation: **Check Expedition Atmosphere** 61 assertions and **Check Atmosphere Tuner** pass; review video `output/skybox-videos/3-sunny-dawn-success.mp4` rendered in the editor with the finale's exported lightmaps bound as the runtime loader does (not committed).

Validation (November 22, editor only): runtime and editor assemblies compile; both shaders import with 0 messages. **Check Expedition Atmosphere** passed 51 assertions (stage fog, sky exposure/colours/glow, lower sky equals fog, glow bearing, mountain ring creation/colours/route centring, restore, replay, shared material untouched) and wrote nine captures, `output/atmosphere/stage-{0,1,2}-{forward,sunrise,side}.png`. **Check Atmosphere Tuner** passed. Login captures: `output/storm-sky/login-*.png`. Expedition and Login scenes stayed clean. November 23: Justin verified the result in Play Mode and asked to drop the playtest items. Quest 3 GPU cost was not measured.

## Procedural night to dawn — November 7

Sky superseded November 22 by the storm sky above; the stage timing, fog and ambient/sun notes below still apply.

User-approved direction: almost-black night, slightly brighter but still-dark pre-dawn upon entering the final bridge block, and muted dawn after successful escape. No daytime, plugins, volumetric effects or visible sun disk. This supersedes the November 4 atmosphere ownership notes above and below.

`Assets/DuckRails/ExpeditionSky.mat` uses Unity `Skybox/Procedural` with Sun None and is referenced by Expedition, retaining the shader variant in builds. `RailAtmosphere` clones it once per run, changes values only when stage progress changes, and restores/destroys the clone on reset/disable. It also restores fog, ambient mode/color and the existing directional light. No runtime GI updates, reflection captures, new lights or lightmap changes.

Initial tuning (implementation defaults, subject to headset review):

- Night: sky exposure 0.012; exponential-squared fog density 0.1; nearly black horizon/fog. The saved scene already uses this night setup before gameplay starts.
- Pre-dawn: exposure 0.08; fog density 0.065. Smooth progression over the first 25 metres of the finale, driven by shared train route distance (`RailRoute.FinaleStart`), rather than a local timer or local player's position.
- Successful dawn: exposure 0.3; fog density **0.0125** (November 7 user follow-up: halve the previous 0.025 endpoint). Smooth fade from pre-dawn density 0.065 over 12 to 20 seconds after the shared successful finish timestamp. At the existing 4 m/s victory speed, this covers approximately finale Z123–155 inside the Z120–160 tunnel. Failures never advance to dawn. Unity checks verified unchanged pre-dawn, midpoint 0.03875 and endpoint 0.0125; existing stage screenshots predate this reduction.
- Night/dawn fog colors match rendered procedural horizon samples along the forward railway (+Z): night (0.008, 0.008, 0.004), dawn (0.439, 0.463, 0.392). November 7 follow-up: the user explicitly selected pre-dawn fog **#060603**, replacing the sampled (0.173, 0.153, 0.075). Sky settings and fog density are unchanged. The sky varies with viewing direction, so uniform distance fog is an approximation; transitions blend these endpoint colors. Earlier `stage-1.png` captures show the original sampled fog, not this override.
- About 98% fog coverage occurs around 20 m / 30 m / 158 m respectively (not hard camera clipping distances). Ambient fill and the existing directional light transition together; lightmaps and baked probes retain their authored illumination.

Verified: Unity compilation; **Duck Rails > Lighting > Check Expedition Atmosphere** passed 27 assertions covering stage boundaries, failure exclusion, late-time reconstruction, isolated material mutation, restore and repeat cycles. Three editor captures were visually inspected, and all three rig eye cameras use Skybox clear mode. Evidence: `output/atmosphere/checks.txt` and `stage-0.png` through `stage-2.png`. Captures use the authored finale without the runtime additive lighting companions and are atmosphere previews, not full gameplay-lighting acceptance. Existing CutePixel missing bullet/em-dash warnings occurred during capture; the assertions completed successfully.

Still pending: full-run Play Mode, real-client late join/master change/replay, headset darkness/readability, dawn destination with loaded baked lighting, culling/seam exposure with thinner fog and Quest GPU performance. The dedicated Duck Rails relay was verified against this repository; sandboxed discovery returned no tools, while authorized access worked. No diagnostic reports were queried.

## Authored gameplay atmosphere — November 4 (superseded)

Expedition gameplay now follows the Environment settings saved in Assets/Scenes/Expedition.unity. RailExpedition no longer forces night ambient/fog colors at run start. The user's current scene settings were saved: Skybox ambient intensity 1.3; black Exponential Squared fog, density 0.1. Tune the Expedition Lighting window and save outside Play Mode. Select Gameplay Lighting in Test Center; isolated block bake scenes do not define runtime fog.

The successful ending still applies dawn after 14 seconds. It captures ambient mode/color and fog color immediately before dawn, temporarily uses flat dawn ambient, and restores the captured values on another run or component disable in that same active scene. New scenes retain their own settings. Ordinary gameplay leaves live editor tweaks alone. Fog density/mode, skybox, ambient intensity, sun and reflections are unchanged. Baked illumination still requires rebaking.

Unity compiled the change. Six direct editor comparisons passed for initial preservation, dawn, repeated calls, latest-edit restoration, a second cycle and untouched environment properties. The SendMessage editor harness emitted ShouldRunBehaviour assertions when invoking the runtime component outside Play Mode; settings comparisons passed and original values were restored. No new full-run/headset test was performed. The diagnostics backend retry succeeded with ten existing open reports; none was resolved.

November 9 editor convenience: added **Bake Open Block And Keep Open**. Verified the new command compiles in the Duck Rails Unity editor with no script compilation failure; full bake/export/reopen execution remains untested.

## Shared baked moonlight and searchable scenes — November 9

The user-approved farm root Moonlight is now the shared `Assets/DuckRails/Lighting/Moonlight.prefab`: Baked directional light, intensity 0.2, RGB (0.16995145, 0.17924529, 0.15303490), rotation (50, 330, 0), soft shadows. All 18 block authoring scenes and Starting Station contain one root prefab instance. It is outside the gameplay block prefab and is stripped from exported probe-only companions. Existing local fixtures, bake settings and gameplay atmosphere are retained. Compound still bakes its gameplay scene directly and is outside this block-bake migration.

Authoring scene names now follow `<prefab-file-name>-bake.unity`, including `farm-silos-v1-bake.unity` and `StartingStation-bake.unity`. Scene assets were moved with AssetDatabase so their GUIDs survive. Block lightmap folders follow the same stem. Authoring and proof tools use the new paths.

Edit the shared Moonlight prefab to change the moon across all authoring scenes, then use **Duck Rails > Lighting > All Blocks > Rebake All Blocks**. Starting Station retains its separate **2 Bake** and **3 Export Lighting Companion** commands. After the block batch, run **Install Finale Lighting** to update Expedition's separate finale render bindings. Changing the prefab alone does not update existing baked data. Keep scene instances free of light/rotation overrides if they should follow the shared moon.

Migration evidence: `output/shared-moonlight/migration.txt`. All 19 rebakes and exports completed; Expedition finale bindings were installed. Saved-scene checks passed for the shared moon, native lighting data, probe-only companions, build entries and renderer texture references in every level. All 19 renamed scene GUIDs were preserved. Headset appearance and block-seam review remain user acceptance work.

Validation: `output/shared-moonlight/export-validation.txt` records 19 passing exports; `output/shared-moonlight/runtime-proof.txt` records 84 PASS lines across four offline assembled routes (seeds 0, 5, 7, 8), covering all 16 scavenging blocks plus station, helipad and finale. Checks include readiness, texture/UV bindings, translated probes, moving-bridge exclusion, finale relocation and complete teardown. An initial editor setup attempt used unloaded prefab references; reloading those references before creating the proof resolved it. No gameplay code changes were needed.

The user reported Unity crashing during the subsequent exit-from-Play interval. PASS and complete teardown were written before exit; the crash cause is not established. After restart, MCP verified Duck Rails in Edit Mode, the saved shared prefab and all 19 exports. Removed the temporary proof scene without opening or replacing the user's current scene. No diagnostic reports were inspected. Headset/multiplayer acceptance and visual seam review remain open.

## Edit Mode fog tuner — November 15, 2026

Open **Duck Rails > Lighting > Atmosphere Tuner**. **Open Expedition and frame dawn exit** opens the production scene (prompting to save existing scene work), frames the far-side landscape and enables the Scene View preview. Alternatively use **Preview in Scene View** in the currently open scene. Select **Dawn**, or scrub 0 (night), 1 (pre-dawn), 2 (dawn). Edit the three stages' colour/density and click **Save fog settings**. Lower density means clearer air. Undo works on setting edits; stopping preview does not undo tuning.

Saved gameplay values live in `Assets/Resources/Rails/Atmosphere.asset`. Existing 0.06 / 0.05 / 0.0125 densities and colours are preserved initially. Gameplay loads the same asset; transition timing, sky, ambient and sun curves remain unchanged. Fog tuning does not require rebaking. Restart a running game to pick up tuning reliably.

Preview temporarily applies the shared controller while the selected Scene View camera renders, restoring the environment immediately afterward. It enables Scene View fog/sky/lighting while active and restores those toggles when stopped/closed or entering Play Mode. A scene without a procedural sky temporarily uses ExpeditionSky. Preview position is not saved gameplay progress. Baked lighting, route loading, headset visibility and actual player-camera clipping are not simulated.

Validation: verified Duck Rails editor, compilation and **Check Atmosphere Tuner** passed 14 Edit Mode checks: resource identity, three fog endpoints, per-render restoration, unchanged scene dirty state, custom midpoint/dawn and unchanged success/failure timing. Full gameplay/headset review is queued; no Play Mode launched.

## Starting Station interior allocation — November 16, 2026

User reported blocky lighting after raising resolution to 16 and flood shadow radius to 0.25. Read-only inspection of the verified station bake scene found two loaded 1024 maps, Station_Shell/Rooms/StairsBridge at Scale in Lightmap 1, City_Deck already 0.02, City_Backdrop without a lightmap binding, and the 96 x 160 m Station ground at 0.25. No UV overlap diagnosis has been established; large rectangular patches may be actual cast shadows.

Set authoring-scene overrides: Station_Shell, Station_Rooms and Station_StairsBridge 1 -> 2; Station ground 0.25 -> 0.05. Retained global resolution 16, raised maximum atlas size 1024 -> 2048 to allow larger combined interior allocations. Preserved other renderer priorities and user light edits. Saved scene/settings. MCP edit compiled and values were checked; no bake/export or Play Mode run. Existing maps still represent the previous settings. Next: comparison bake and inspect wall/floor shadows and UV charts, then export accepted lighting. Increased interior density can increase memory; headset quality/performance remains unverified.

November 16 investigation initially retained the patches in isolated no-shadow and replacement-floor bakes; see [historical investigation](validation/station-lightmap-audit/README.md).

### Confirmed square-patch fix

City_Deck's upward-facing surface and the station's downward-facing ceiling were
coplanar at Y=9. Backface hits caused invalid bake texels and rectangular neighbour
fills. Six platform hemisphere probes measured 15.6–29.7% deck backfaces against
the bake's 90% front-face requirement. Raising only the hidden, collider-free deck
by 0.25 m eliminated those hits and the patches in a controlled bake with original
Auto filtering. Production is rebaked/exported; east/west native camera views and
all 91 exported bindings pass editor checks. Runtime/headset review is queued.

Preserve the City_Deck local Y=0.25 override in StartingStation and its V3 source
prefab. RailCityStation.BuildPrefab applies it after FBX instantiation. Do not
restore the deck to the ceiling plane. User lights, mesh UVs and interior geometry
are unchanged. [Evidence, images and checks](validation/station-lightmap-fix/README.md).

### Darker night sky — November 23, 2026

Justin: night sky much too bright, then "still too bright". Night sky exposure 0.6 → 0.3 → 0.12 → 0.2 → 0.5 → **0.4** and pre-dawn 0.7 → 0.4 → 0.2 → 0.3 → **0.6** (Nov 23 evening: still much too dark; "a little bit darker than the original values"; mountains night near 0.005 / far 0.02, pre-dawn 0.008 / 0.035) (saved in `Atmosphere.asset`, code defaults, and both `ExpeditionSky.mat` and Login's `Quackville_Skybox.mat`); night/pre-dawn mountain colours reduced (night far 0.008, near 0.002) so the ridges stay darker than the sky; night Horizon colour halved to (0.06, 0.068, 0.045) so the band above the horizon is no brighter than overhead. Dawn unchanged. **Check Expedition Atmosphere** 61 assertions and **Check Atmosphere Tuner** pass. To tune further: **Duck Rails > Lighting > Atmosphere Tuner** → Preview, pick Night/Pre-dawn/Dawn, edit the `Night Sky` / `Pre Dawn Sky` / `Dawn Sky` fields (Exposure is the overall brightness), then **Save fog and sky settings**. Login's sky is the `Brightness` slider on `Quackville_Skybox.mat`.

### Deep night before the mid shop — November 23, 2026

Justin: the current night becomes the state once the players reach the mid shop block; before that it should be darker, fading to night on arrival. `RailAtmosphere` now spans -1 deep night, 0 night, 1 pre-dawn, 2 dawn. While the finale has not been reached (progress 0) it computes `MidpointLift()` from the shared `RailTrain.Distance` and the new `RailRoute.MidpointStart` (route distance where the helipad block begins, set in `Build`), smoothstepped over the first 30 m of that block, so every client, late joiner and replay agrees without new network state. Deep night (`deepNightSky` in `Atmosphere.asset`) uses sky exposure 0.22 (night 0.4), ambient (0.014, 0.017, 0.025) and darker mountains; it shares the night fog. Without a live route/train (editor preview, other scenes) it stays at night. Tuner: new **Deep night** button and **Deep Night Sky** fields. Validation: **Check Expedition Atmosphere** 68 assertions (including deep night values, night fog, fade midpoint and end) and **Check Atmosphere Tuner** pass; game, editor and player-define compiles pass. Justin verified it in Play Mode (November 23); two-client and headset runs were not done separately.
