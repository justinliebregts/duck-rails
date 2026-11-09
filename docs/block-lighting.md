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

1. Open `Assets/DuckRails/Lighting/StartingStation/StartingStationBake.unity`. It contains the actual station prefab at origin, its eight permanent baked spotlights, and a separate Light Probe Group. Edit the prefab geometry/fixtures here; preserve its track contract, origin, rotation and scale. Probe positions belong to this bake scene.
2. Use **Duck Rails > Lighting > Starting Station > 2 Bake**. Wait for Unity to finish. There is no runtime bake.
3. Use **3 Export Lighting Companion**. This applies renderer bindings to the station prefab and writes `StartingStationLighting.unity`, a probe-only additive scene referencing the baked LightingData and textures. Export after every rebake or renderer change. Do not edit the generated companion directly.
4. The companion must remain enabled in Build Settings. The exporter adds it without changing Login/Compound/Expedition build indices. Authoring and proof scenes are not shipping entry scenes.

The initial preparation command is one-time and refuses to overwrite an existing bake scene. Original shared station materials are retained. One UV-unwrapped mesh copy is used by the primitive geometry. The bake uses non-directional lightmaps, 8 texels/metre (ground scale 0.25), 1024 maximum atlas size, 32 direct/128 indirect samples, four bounces and indirect intensity 1.5. Current output is two atlases and 367 probes. Warm lighting on the existing teal platform material appears greenish; this is not a finished palette decision.

## Other blocks: bake and export together

Every prefab in `Assets/DuckRails/Blocks` now has four permanent baked spotlights: two warm shelter lights on the left and two cool work lights on the right. The finale instead lights its approach, control area, bridge threshold and tunnel. This adds 52 sample lights across thirteen blocks. It uses the same non-directional bake settings as the station and conventional probes at multiple heights across each block.

1. Open `Assets/DuckRails/Lighting/Blocks/<block-id>/Bake.unity`. Edit fixtures/geometry in this scene, with the prefab at the origin. Probe positions belong to the bake scene.
2. Use **Duck Rails > Lighting > All Blocks > Bake Open Block**. This asynchronously bakes and exports that block, including prefab bindings and its `<block-id>-Lighting.unity` companion. Choose **Bake Open Block And Keep Open** to reopen the original bake scene after export (also if export fails after switching scenes). Both commands save the bake scene before starting and refuse overlapping queued bakes. Wait for completion in the Console or `output/block-lighting/bake-progress.txt` before changing scenes.
3. After rebaking the bridge finale, use **Install Finale Lighting**. Expedition contains existing bridge objects and interaction references, so this transfers matching render bindings onto them without replacing the bridge.
4. Use **Validate Exported Catalogue** to verify all thirteen exports. **Bake Missing Samples** prepares/bakes missing companions; **Re-export Existing Bakes** refreshes all exports without rebaking.

The moving bridge hinge and variable spawn objects are excluded from static lightmaps and disabled as bake contributors. The far dawn bank/exit are also outside this night sample. Fourteen companions (station plus thirteen blocks) are enabled after the three gameplay scenes in Build Settings. Only seven load during a normal expedition: station, five selected stops and finale. Authoring and proof scenes are not build entries.

Do not run the general Week 3 gray-box rebuild to tune lighting: it regenerates prefab geometry/build settings and does not preserve this lighting authoring pass. Use the bake scenes and lighting menus. Geometry changes require another bake/export; do not edit generated companion scenes or texture bindings manually.

## Iteration and saved assets

For more light in a corner, first adjust nearby baked fixture coverage or add a restrained baked fill in the authoring scene, then rebake and export. Increasing bounce alone only modestly brightened Compound's remote corners; the added baked spotlights made a larger readability improvement. Increasing probe density does not brighten static walls: probes sample the baked illumination for moving subjects.

Rebake after changing static geometry, its lightmap UVs, lighting-relevant materials, fixture position/color/intensity/range, or probe positions. For a newly added static mesh, prepare lightmap UVs and Contribute GI/Receive GI settings before baking; **Bake Open Block** uses the existing authoring scene and does not rerun its initial mesh preparation. Keep moving geometry out of the static bake. Rails and sleepers are now collider-free visual geometry but remain valid lightmap receivers; collider removal alone did not require rebaking their unchanged visible surfaces.

Save/version the entire affected lighting folder, including authoring scene, generated companion, Lighting Settings, LightingData asset, lightmap textures, UV mesh copies and every `.meta` file. Include updated prefab bindings and Build Settings; include `Expedition.unity` after installing updated finale bindings. Generated textures alone are insufficient. Screenshots and proof logs live in the corresponding `output/*lighting*` folders; `tmp` backups are not required runtime assets.

Save other scene work before running export/proof menus: these tools open and replace the active editor scene. After a rebake, validate the export and inspect the result in the normal Expedition flow as well as the isolated bake scene.

## Troubleshooting

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

## Procedural night to dawn — November 7

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
