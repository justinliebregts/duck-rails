# Playtest queue

Changes that need in-Unity verification (Play Mode, single or multi-client) are **queued here instead of being tested immediately**. The user asks for the queue to be run when they are away ("run the playtest queue"). Results are written back into this file.

## Rules for the agent

### When finishing a change
1. Do the cheap checks now: compile, Console errors, EditMode/static checks that take under ~1 minute. Do not launch Play Mode or multi-client scenarios unless the user asks.
2. If the change needs a live check, add an entry under **Pending** (template below) in the same commit as the change when practical. Reference the destination commit SHA once it exists.
3. In the final message say "queued for playtest" and name the entry. Never write "verified" or tick a milestone/`docs/duckz-ports.md` item as validated for something that is only queued.
4. Omit `Mode` unless the entry should be report-only; fix-on-fail is the default.
5. If a later change invalidates a pending entry, edit or merge it. If an entry is superseded, move it to **Dropped** with a reason.

### When asked to run the queue
1. Verify Unity MCP targets Duck Rails, and that Unity compiles with an empty Console of new errors.
2. Work through **Pending** in order (cheapest/single-client first, multi-client later). Between entries, return Unity to Edit Mode and clear leftover test state.
3. Per entry: run the listed scenario, collect evidence (Test Center evidence under `Logs/Diagnostics/test-*`, `Temp/rails-*.txt`, Console excerpts), and record PASS / FAIL / BLOCKED with date, Unity version, client count, and the commit tested.
4. Do not stop on the first failure: continue with unrelated entries. **Fix on fail is the default** (an entry can opt out with `Mode: report-only`):
   - Diagnose, fix, recompile, and re-run that entry. Try at most 3 fix attempts per entry, then mark it FAIL and record what was tried.
   - Commit each fix separately on `main` (only the files changed, follow AGENTS.md), message naming the queue entry, so the user can review or revert each one. Never push.
   - Fixes stay within the entry's intended behaviour. If a fix would change a confirmed design decision, networking/boarding rules, or need a large new subsystem, don't apply it: record the failure and the proposed fix, and continue.
   - After a fix, re-run any earlier PASSed entries that touch the same code.
   - Record the failure, the root cause, the fix commit SHA, and the passing re-run in Results.
5. Move finished entries to **Results** (newest first) with evidence links, and update the matching docs (`docs/duckz-ports.md`, milestone checklist, feature docs) only for PASS.
6. Do not query diagnostic reports (see AGENTS.md).
7. At the end, give a short summary: counts of PASS / FAIL / BLOCKED and what the user must do (headset checks, anything not automatable).

### Not automatable (always stays manual)
Headset hand tracking and VR feel, real network jitter across machines, audio quality, anything marked `manual-only`. List these as **Needs human** in the entry; the run reports them but never marks them PASS.

## Entry template

```
### <short title> — queued <YYYY-MM-DD>
- Commit(s): <SHA or "uncommitted">
- Changed: <one line behaviour summary>
- Setup: <Test Center button / scenario / preset / block order, client count>
- Check: <what to observe; explicit pass criteria>
- Needs human: <headset/feel items, or "none">
- Priority: <low | normal | blocks other work>
- Mode: <fix-on-fail (default) | report-only>
```

## Pending

### Skyscraper Works production atlas integration — queued 2026-11-14
- Commit(s): see git log "Integrate approved Skyscraper atlas"; user approved the appearance and retained triangle count.
- Changed: 3,403 compatible scenery renderers / 13 materials -> 394 chunks / one atlas material, plus retained CutePixel sign. Total 3,404 -> 395 renderers; scenery triangles 47,996 -> 228,866. Four-metre vertical cells suit tower floors; oversized sources stay separate. Constant HDR emission retained; no textured emission atlas or animated brushes exist in this block.
- Cheap checks: 4,507 original transforms, 15,185 serialized components, 3,649 colliders, navigation/spawn settings, all 354 original dependency hashes and 3,403 original baked bindings preserved. Exact mesh data and 3,797 review-scene bindings pass; seven visual pairs inspected; compilation succeeds and final error Console empty. [Evidence](validation/skyscraper-atlas/README.md).
- Integration checks: all 23,068 original local object IDs and production metadata preserved; production matches the approved review; 353 pre-existing dependency files unchanged. Build fingerprint and lighting-workflow guards pass. [Evidence and recovery](validation/skyscraper-atlas/production-integration.md).
- Setup: use normal production Skyscraper Works via Test Block Order on a requested queue run. One client for loading/readiness/visibility, two real clients plus late join for agreement; include nonzero route placement and return/replay. Production atlas assets are in `Assets/DuckRails/SkyscraperAtlas`; the approved review remains available.
- Check: correct lighting/readiness at origin and translated placement, companion unload/rebind, hide/show/re-enable, no missing/duplicate scenery; unchanged tower floors/stairs/shafts/crane, foundation/collision, 270 coal / 270 gold / 26 enemy candidates and per-run selection, navigation and CutePixel text. Inspect normals, shadows, emission, atlas seams and distant minification.
- Needs human: Unity appearance approved. Moving-view/headset darkness/minification, natural shaft/stair/crane traversal and item reach/recovery remain queued. Existing block gameplay acceptance remains open.
- Priority: normal. No repeated wing pilot, routine Quest comparison or comparison APK; run those only on explicit request. Renderer reduction does not establish Skyscraper device performance.

### Convenience Store production atlas integration — queued 2026-11-14
- Commit(s): see git log "Integrate approved Convenience Store atlas"; user-approved visuals integrated into the original-GUID production prefab.
- Changed: 1,139 compatible static renderers -> 285 chunks / one shared atlas material. Retain 231 exceptions including textured HDR neon, seven animated brush hierarchies, transparent cooler panes, CutePixel labels and incomplete-UV rooftop models. Total enabled MeshRenderers 1,370 -> 516; whole-block MeshFilter triangles 55,702 -> 127,134.
- Review-stage cheap checks: saved preservation of 3,208 transforms / 5,852 components / 1,420 colliders, exact imported mesh arrays and all 1,557 scene lightmap bindings, 1,378 original dependency hashes, compilation/empty final error Console and seven source/atlas visual pairs pass. [Evidence](validation/convenience-store-atlas/README.md). No Play Mode/APK/device tests.
- Integration checks: all 11,745 original local object IDs preserved; production metadata byte-identical; 1,420 colliders and 1,557 lighting bindings match the approved review; 1,377 pre-existing dependency files unchanged. Build fingerprint/bake-workflow guards pass. [Evidence and recovery](validation/convenience-store-atlas/production-integration.md).
- Setup: use the normal production Convenience Store via Test Block Order on a requested queue run. One client for loading/readiness/visibility, two real clients plus late join for agreement; include a nonzero route placement and return/replay. Production atlas assets are in `Assets/DuckRails/ConvenienceStoreAtlas`; the approved review remains available.
- Check: correct lighting/readiness at origin and translated placement, companion unload/rebind, block hide/show/re-enable, no duplicate/missing scenery; retained brush animation with stationary full-size hulls; original collider contact, loot/spawns/navigation, sparse shelves/empty coolers/stockroom/trailer and CutePixel text remain intact. Inspect close/interior/distant seams, atlas minification, normal/shadow/reflection behavior and neon HDR colors.
- Needs human: full-block Unity appearance approved. Moving-view/headset minification/interior darkness and natural traversal/brush collision remain queued; existing block gameplay acceptance is not closed by authoring checks.
- Priority: normal. No repeated wing pilot or routine per-block Quest comparison; performance captures/builds only if requested.


Atlas rollout policy — November 13: user accepts Motel's approach and authorizes
the remaining blocks, Convenience Store next. Follow
[the workflow](block-atlas-optimization.md). Do not repeat full Quest A/Bs for each
block by default; keep authoring preservation checks/visual review and queue live
acceptance. Historical benchmark proposals below do not override this direction.
No new block conversion or device run occurred during this documentation task.

### Full Motel Blender atlas review — queued 2026-11-13
- Scoped Quest evidence 2026-11-13: accepted same-build 120 m pair, all four views 100% tracked, applied full-map variants confirmed. Draw calls fall 24–70%; CPU medians improve 0.1–0.6 ms and maximum view CPU p95 improves 12.5 -> 11.8 ms. GPU medians improve in three views; forward GPU p95 rises 10.0 -> 10.4 ms. All view p95 CPU/GPU remain under 13.89 ms; sampled native FPS remains capped around 72. Pose difference 0.176 m / 0.23 degrees; both variants resident, no release-memory claim. [Results/limits](validation/motel-full-atlas/results.md). Initial cached wing-fixture build was rejected before measurement; clean-build fix and substitution assertion passed. User's visual fixes intentionally deferred; broader runtime/headset/multiplayer acceptance remains queued.
- November 13 follow-up: user requested Quest performance testing before addressing remaining visual issues. Dedicated same-build fixture now selects exact production consolidation (767 enabled mesh renderers) or full-map atlas (315), with 10 common renderers and unchanged collision/gameplay. Both alternatives remain resident. Capture results pending below; this does not approve visual issues or production rollout.
- Commit(s): uncommitted. User authorized extending the approved wing approach across the rest of Motel; no repeat of the wing Quest capture requested.
- Changed: isolated full-map prefab/scene under `Assets/DuckRails/MotelAtlasReview`; 1,964 source renderers become 305 spatial chunks sharing one atlas material, with 10 originals retained (315 enabled mesh renderers total). Production Motel/catalogue unchanged. Open **Duck Rails > Authoring > Motel > Open full-map Blender review**.
- Cheap checks: importer compiles; saved assets retain 2,688 original transforms, 8,003 serialized components except renderer-enabled changes, all 1,900 colliders and 1,964 original lightmap bindings. 2,269 total bindings are valid. Surface-area preservation passed during conversion. See [import evidence](validation/motel-full-atlas/import-checks.txt) and [source/workflow](../ArtSource/MotelAtlas/README.md).
- Tradeoff: converted geometry rises from 83,344 to 168,243 triangles to preserve repeating UVs. Atlas emission/surface mipmaps disabled after correcting visible cross-tile glow; base-color mipmaps retained. No full-map performance benefit claimed.
- Setup when authorized: review appearance first, then isolated full-map route fixture against current production consolidation at 120 m with matched settings/poses. The existing wing comparison builder does not select this prefab. Exercise translated placement, companion unload/rebinding, hide/show/re-enable, interiors and natural traversal.
- Check: no missing/duplicate faces, color/emission/lightmap drift, atlas seams/bleeding or culling problems; unchanged collision/spawns/interactions; compare CPU/GPU median/p95, draws, triangles, memory and load cost. Keep production rollout pending until evidence supports it.
- Needs human: full-map Unity review, headset minification/interior appearance and traversal. Scoped device performance completed above; remaining acceptance queued for playtest.
- Priority: normal.

### Motel east-wing Blender atlas pilot — queued 2026-11-13, scoped Quest comparison recorded
- User approved Blender and Unity appearance, then authorized the Quest comparison. Same-build 120 m pair has all four Motel views at 100% tracking. Wing-facing yaw-90: draws 411 -> 309; median CPU/GPU 10.15/8.0 -> 9.9/7.2 ms. All per-view CPU/GPU p95 under 13.89 ms; forward CPU p95 increases 11.9 -> 12.7 ms. This is directional evidence, not a uniform speedup: stopped train/head positions differ 0.556 m, angles within 0.03 degrees. User accepted this positional limitation and requested no repeat. Third run cancelled at login with zero valid frames, excluded. See `docs/validation/motel-wing-comparison/results.md`.
- User approved Blender appearance and authorized separate Unity review import. Open **Duck Rails > Authoring > Motel > Open Blender wing review**, or `Assets/DuckRails/MotelWingReview/Motel-Atlas-Review.unity`.
- Changed: separate full-block review prefab/scene, 29 single-material wing chunks, retained collision/gameplay and outside-wing render portions, original two baked atlas bindings. Production Motel unchanged. See `ArtSource/MotelWingPilot/README.md` for workflow and limits.
- Comparison fixture: separate `MotelWingBenchmark` prefab has 590 shared outside chunks and selectable 130 original-material/29 atlas wing chunks; only the explicit development comparison build injects it. Production assets unchanged. Both variants resident, so shipping memory/load savings remain unmeasured.
- Proposed later checks: source/atlas color and emission equivalence, seams/mipmaps, normals, lightmap UV2/rebinding, interior and exterior culling, followed by matched 120 m Quest CPU/GPU/memory/load comparison if integration is authorized. Preserve all collision, gameplay and CutePixel text.
- Needs human: headset interior/seam/culling appearance and natural traversal. Blender and Unity appearance approved.
- Status: scoped native rail-view performance evidence only; production integration, runtime visibility/rebinding regressions, interior/combat, thermal and multiplayer acceptance remain queued for playtest.

### Player viewing distance 120 m to 175 m at dawn — queued 2026-11-13
- Commit(s): uncommitted.
- Changed: shared DuckPlayer cameras default to 120 m; runtime controller follows the existing successful dawn blend to 175 m. Failure/pre-dawn stay at 120 m; restore/replay returns to 120 m. Temporary editor-only 50 m override removed. Explicit development benchmark override still takes precedence.
- Checks passed: compilation and 17 EditMode checks for saved camera/controller references, transition endpoints/midpoint, failure, late-arrival dawn value, reset and unaffected unrelated cameras. See [evidence](validation/motel-render-chunks/view-distance-checks.txt).
- Setup: editor desktop and Quest; review nighttime blocks and successful finale through dawn, then return/replay. Test late join at dawn and optional benchmark override separately.
- Check: local XR eyes/simulated view use 120 m, smoothly reach 175 m with dawn and reset; no visible cutoffs on important signs/lights/buildings, no dawn valley disappearance, no mirror/veil camera changes; target-device CPU/GPU budget holds. These distances have not yet been device-profiled.
- Needs human: head-turning, distant silhouettes/emissive lights, flashlight reach and dawn appearance.
- Priority: normal.

### Motel spatial render consolidation — queued 2026-11-13
- 100 m comparison 2026-11-13: same-build native 100 m capture and fresh 50 m control pass four Motel viewpoints with 100% tracking; pitch within 1 degree. Forward median CPU/GPU 11.4/8.5 ms at 100 m versus 10.0/7.9 at 50 m, both around 72 FPS. All sampled Motel/Convenience 100 m views under budget at p95. Visual/dawn/multiplayer acceptance remains pending; no production clip change. See [comparison](quest-performance.md#100-m-versus-fresh-50-m--november-13-2026).
- Editor review setup 2026-11-13: temporary **Duck Rails > Tests > Preview 50m player far clip** toggle enabled for desktop/XR Play Mode. User to review popping, signs/headlamps, interiors and dawn. Editor-only; production camera assets/builds unchanged. Queued for playtest.
- Far-clip experiment 2026-11-13: explicit 50 m benchmark and same-build authored-distance control completed all Motel views. Closely matched yaw-180 CPU/GPU improved 12.5/9.9 -> 9.6/7.1 ms, draws 2,470 -> 317; yaw-0 comparison is confounded by headset movement. All 50 m views meet the sampled frame budget, but visual/dawn/multiplayer acceptance remains open. Production far clip remains 1,400 m. See [experiment](quest-performance.md#50-m-far-clip-experiment--november-13-2026).
- Quest partial PASS 2026-11-13: fresh optimized build, explicit Motel-first route, all four Motel views at 100% tracking. Worst view 14.7/14.7 ms median CPU/GPU and 61.5 FPS versus prior 19.2/17.5 ms and 51 FPS; still misses 72 Hz budget. Head pitch differs ~8 degrees, so controlled component on/off A/B, runtime visual/culling and manual checks remain pending. See [device evidence](quest-performance.md#quest-device-check--november-13-2026).
- Commit(s): uncommitted.
- Changed: offline-generated 698 render chunks replace 1,905 static source renderers at runtime; source geometry/collision/spawns and original baked lighting retained. Active Motel mesh renderers: 1,974 to 767. See [evidence and tradeoffs](quest-performance.md#motel-consolidation-pilot--november-13-2026).
- Setup: first check Motel lighting readiness in a translated route and block hide/show/re-enable; then matched short Quest runs with `RailSceneryChunks` enabled versus disabled on Motel, identical route/settings/poses to baseline `e1dc96566b4e4ab0ab45c83e870e9221`. Restore component enabled after the comparison. Do not change render scale/MSAA/foveation during this A/B.
- Check: no missing/duplicate surfaces or lightmap drift at origin/translated placements and after another lighting companion unloads; source/optimized views match, including interiors/reflections; collision, markers and interactions unchanged; disabling component restores originals; leaving/re-entering visibility range restores chunks. Compare CPU/GPU median/p95, draw calls, triangles, memory, loading and missed frames; report regressions rather than assuming fewer renderers guarantees a win. Aim for both CPU/GPU below the 13.89 ms budget at 72 Hz; further optimization may be needed.
- Needs human: headset close-up/interior appearance, culling transitions and natural traversal.
- Priority: normal.

### Quest performance runner robustness and expanded coverage — queued 2026-11-13
- Follow-up 2026-11-13: default 180-second expedition stop/save, collection and cleanup passed on device in run `fa93ca6c863246c68abcc707546cac32`; explicit Motel-first route confirmed in metadata. This closes the timed-stop uncertainty below, not broader coverage/robustness acceptance.
- Commit(s): uncommitted.
- Changed: opt-in native Quest 3 solo rail survey, buffered frame counters and PC run/collect controls. See [setup and device evidence](quest-performance.md).
- Device evidence: 483-second native capture, four scavenging blocks plus helipad, automatic refuelling/travel, CPU/GPU metrics, cancellation/timeouts, CSV retrieval and cleanup passed. User deliberately stopped after sufficient representative data; full route is not required for initial tuning. New default PC 180-second timer has syntax checks only and needs validation on the next requested run. See linked evidence for the one tracking-rejected view.
- Setup: Development APK on authorized Quest 3; explicit `Tools/quest-performance.ps1 -Action Run`. Run additional four-block sets separately.
- Check: cancellation, login/room/readiness timeouts, focus/tracking-loss rejection, request consumed after crash/restart, cleanup restores callbacks and PC proximity state; compare repeated captures with matched settings and release-equivalent overhead. Interior/combat and actual multiplayer load need separate scenarios.
- Needs human: stationary upright headset framing and headset comfort checks are separate from unattended frame-cost measurements.
- Priority: normal.

### Shared complete store and security camera — queued 2026-11-12
- Avatar acceptance: equip Quackbear, Huggy and Scrap Duck individually from each store; verify the local avatar/mirror and hands update correctly, every connected remote client sees the correct equipped avatar, a late joiner sees the current avatar, switching between all three and Default Duck replicates without stale meshes/faces, and the selection survives the intended scene transitions/rejoin. Use real authenticated clients; a single editor or visual prefab check is insufficient. Still queued, not verified.
- Commit(s): uncommitted.
- Changed: one RailSharedStore prefab in Compound, Departure Shop and midpoint helicopter: seven coin goods with price/coin labels underneath, nine bundle items, Default Duck and local SecurityCam/SecurityTV/HelicopterMirror. Supersedes previous midpoint-exclusion expectations.
- Cheap checks: shared-store and helicopter authoring checks pass; no Play Mode.
- Setup: two authenticated clients and late joiner; Meta bundle products and backend rollout for checkout checks.
- Check: each location has one active store and TV; coin purchases charge once and spawn actual Rails cargo/bags on peers, all bundle ownership/equip/120-second timers, same-room cooldown sharing, default avatar, camera sees local face and remote players/held objects, screens have independent feeds and shut down out of range. Gold exchange still works only at the existing expedition intakes.
- Needs human: spacing/reach at all shops (compound is 100% scale; overlap accepted pending user layout adjustment), prices/icons and countdown readability, camera framing and headset rendering cost.
- Priority: normal.



### Furnace Polygon flames — queued 2026-11-12
- Commit(s): uncommitted.
- Changed: `RailFurnaceModule` spawns `FX_Fire_Big_02` (idle, already in `Assets/Plugins/PolygonParticleFX`, identical to the arpg copy) at the firebox; emission fades to 12% (`outOfFuelEmission`) when `PhotonSession.CurrentFuel` is 0 and back up on refuel. Each committed coal deposit plays `FX_Fire_Explosion_01` once on every client, from the same path as the "+N" fuel popup. Prefab refs live in `Furnace.prefab`; `fxScale` 0.25 is a first guess.
- Cheap checks: compile clean; prefab references resolve; burst bounds ~1.7 m at scale 0.25. No Play Mode run.
- Setup: two clients on the train, a late joiner.
- Check: flames burn idly; at 0 fuel they dwindle; dropping coal in gives a burst visible to the other client; size/position suit the firebox opening.
- Needs human: scale/position feel (headset).
- Priority: normal
- Mode: fix-on-fail

### Quackbear, Huggy and Scrap Duck bundles — queued 2026-11-12
- Commit(s): uncommitted.
- Changed: compound and Departure Shop bundle walls now use Duckz horizontal weapon–bag–avatar rows and source heading/countdown styling (overlap allowed for manual adjustment), three rigged avatars, separate four-slot bag and frying-pan placeholders, existing independent 120-second bag/weapon cooldowns and no avatar cooldown. See `docs/character-bundles.md`.
- Cheap checks: Unity compilation, 103 EditMode checks, visual preview and backend config/economy tests passed; no Play Mode run.
- Setup: two authenticated clients, then a late joiner and Quest; Meta durable products and matching backend catalog must be configured/deployed for purchase tests.
- Check: unowned checkout/cancel/restoration; owned avatar/default equip; bag and weapon spawn naturally into either hand; same item blocked until 120s, other item and avatar unaffected; shared wall cooldown, actual Photon room reset versus private same-room scene transition; peer/late-join appearances and unique weapon spawn IDs; held/bagged/loose moving cargo and departure cleanup. No gameplay coin charge; Departure Shop retains seven standard supplies alongside nine bundle pulls; the midpoint now uses the same shared full store. Check reach and cooldown sharing across all three stores.
- Needs human: headset reach (especially lower weapon row), natural grip, avatar eye/lip alignment and labels, Meta checkout interaction.
- Priority: normal.

### Matching fuel/distance backing — queued 2026-11-12
- Shared backing mesh/material; original fuel text, ten yellow segments and dark sockets retained. Compile and serialized UI/gameplay preservation checks pass. Check headset appearance and fuel readability/full-to-empty state. No Play Mode run.

### Throttle hand offset, variable speed and return — queued 2026-11-12
- Commit(s): uncommitted.
- Changed: live palm offset, speed dead zone to 20% then proportional speed, retained forward snap, immediate STOP with 0.25s visual return; regrip preserves current angle. Prior cockpit works well in headset per user report.
- Cheap checks: Unity compile, 25 drive and session EditMode checks pass; Console error/exception query empty; saved prefab/scene tuning defaults checked.
- Setup: headset then two real clients with late join, disconnect and host handoff; do not treat user report of prior art as acceptance of these new behaviors.
- Check: tune Y/-Z offset while holding without altering tracked input or breakaway; speed zero through 20%, intermediate speed thereafter, forward detent at 85%; return finishes in ~0.25s without instant snap, including release before network acknowledgement and regrip mid-return. Remote visual and train speeds agree; fuel is distance-based; timeout/disconnect/handoff brake; desktop T remains full-speed/STOP.
- Needs human: palm clearance on both hands, variable-speed feel and detent/return feel.
- Priority: normal.

### Approved cockpit and lowered throttle axle — queued 2026-11-12
- Commit(s): uncommitted.
- Changed: solid grip joining three arms/slots, axle 18 cm lower, faceted horn, distance bezel and coal/fire/fuel picture. See `docs/cockpit-v2.md`.
- Cheap checks: compilation, saved bindings/fonts, lowered neutral geometry, 71-angle slot envelope; EditMode render reviewed.
- Setup: solo then two real clients, late join and host handoff.
- Check: full STOP/GO arc, grip at either end of the wide bar, detent/release/breakaway, replicated grip, horn press/release, live distance and fuel. No arm/housing clipping at either end; no stale ownership on disconnect.
- Needs human: headset reach and increased hand travel, label/card readability and moving lighting; manual-only.
- Priority: normal.

### Furnace fuel popup — queued 2026-11-12
- Dropping coal into the furnace floats a "+N" (N = per-run `RailResourceBalance.CoalValue`, e.g. +12.5 solo) up from the firebox opening, using the gold crate's `GoldRewardText` look. Shown on every client once per committed deposit; late joiners do not replay old ones. Check solo and two-client; verify the number matches the gauge gain and reads well. Compile/Console checked only; queued for playtest.

### Seven-block material contact sounds — queued 2026-11-12
- Commit(s): uncommitted.
- Changed: AudioSurface assignments across Final Bridge, Farm, Skyscraper, Helicopter Shop, Convenience Store, Motel and Container Yard. See `docs/block-audio-surfaces.md` for material choices and approximation limits.
- Cheap checks: installer compiled in Unity 6000.3.24f1; all seven saved prefabs reloaded; 10,344 environment colliders match planned surface types; all seven used types resolve to valid player footstep sounds. Serialized comparison preserves existing geometry, collision, gameplay and baked data. Console contains an LCK graphics-API error. No Play Mode run.
- Setup: visit the seven blocks using Test Block Order; walk and make hand contact on representative surfaces, then compare remote contacts with two real clients.
- Check: distinct metal/concrete/wood/grass/gravel sounds on bridge, silo, tower slabs, helicopter cabin, store floors, motel and containers; wooden fences/playground/pallets/trailer decking sound wooden. Check water-volume transitions and mixed ground seams. Confirm no missing or duplicate sounds and no traversal changes.
- Needs human: headset listening and suitability of existing Wood/Stone/Metal approximations for rubber, fabric, glass and plastic; manual-only sound-quality acceptance.
- Priority: normal.

### Broad block reverb — queued 2026-11-12
- Commit(s): uncommitted.
- Changed: 25 box zones across Final Bridge (3), Farm (6), Skyscraper (4), Convenience Store (4), Motel (4), Container Yard (4). Helicopter Shop excluded. See `docs/block-reverb.md` for placements/tuning.
- Cheap checks: installer compiled; saved 25-zone count, enabled components, child references, positive sizes and matching fade radii pass; Console error/exception query empty. Existing serialized prefab objects preserved. No Play Mode run.
- Setup: visit all six blocks in Test Block Order, then two real clients with late join; headset listening separately.
- Check: smooth entry/exit; dry open railway except finale tunnel; restrained tower/container reflections; stronger tunnel/cellar/silo tails. Check cellar versus farmhouse, motel wings/change-room partition, container roof and skybridge transitions. Hear item impacts/footsteps and peer voice clearly; each client follows its own listener location. Tunnel fades on moving-train entry/exit; no lingering reverb after leaving/unloading a block.
- Needs human: audio quality, voice intelligibility, headset transition feel and Quest performance. Manual-only listening acceptance.
- Priority: normal.

### Container Yard north yard, fences and open containers — queued 2026-11-12
- Latest ground/spawn revision supersedes prior pool/marker evidence: main ground repeats at 12 m instead of 3 m; 168 coal / 142 gold / 32 enemy candidates redistributed across the updated yard, with unchanged runtime scaling. 3,092 editor checks pass, including all 342 supported/clear candidates and all 800 crew/seed cases per selector; 1,191 lighting bindings re-exported. See `validation/container-yard-redistribution/`.
- Redistribution checks: inspect loot in the shed workbenches/lower shelves, repair-container and trailer interiors, dock, tank-service corners, machinery, main container interiors and intended roof/climb rewards. Confirm natural reach, item settling, bilateral/quadrant encounters, low-eye ground texture scale and peer agreement. The old unsupported counterweight coal marker has been moved and its support issue is resolved. No Play Mode run for this revision.
- North extension: approved repair shed with workbenches/dismantled container, enterable truck trailer and loading dock, static forklift/pallet climb, two horizontal propane tanks and two baked floodlights. Original markers/poses/tuning retained. Additional 1,776 editor checks and six ground-route connectivity checks pass; navigation/lighting rebuilt, 1,191 total valid bindings including all 33 new combined renderers. See `validation/container-north-yard/`.
- North checks: walk around forklift into shed, inspect workbench/door/chain collision, traverse dock steps and threshold into trailer, climb pallets onto the fallen container, walk between/around propane tanks, confirm tank/support/valve contact and natural ground-enemy pursuit into the new interiors. Carry/drop items on the dock/trailer and compare both peers/late join. Headset night readability, dark rear-shed pocket and Quest frame time remain manual. No Play Mode run for this extension.
- Commit(s): uncommitted.
- Changed: east/west fences at X +/-47 m with continuous 9 cm wires, terminal posts and gap-free collision; four ground interiors per side, new Row 1 and existing Row 4 search openings face the tracks.
- Historical fence/interior checks: Unity 6000.3.24f1 compiled; 2,052 checks passed with 1,158 lighting bindings. The then-unsupported counterweight coal is resolved by the latest redistribution above. No Play Mode run.
- Setup: Container Yard first in Test Block Order; desktop then two real clients with late join; worn Quest separately.
- Check: walk into all eight interiors, carry/drop items through new openings, retain roof/climb routes; continuous fence contact along both sides and no seam protrusions or railway end caps; enemies navigate the opened interiors; both clients agree on geometry/loot and lighting loads correctly.
- Needs human: headset door/fence contact, nighttime wire/interior readability and Quest performance.
- Priority: normal.

### Hand flashlight stays on after release — queued 2026-11-12
- Commit(s): uncommitted.
- Changed: `HandFlashlight` no longer turns off on release/pickup; the owner turns an unheld light off after 60 s (`abandonTimeout`). Stashing still turns it off.
- Cheap checks: edit only; compile not yet confirmed in Console.
- Setup: two clients; one holds a flashlight and turns it on.
- Check: drop it - light stays on for both clients; picking it back up before 60 s keeps it on; left alone ~60 s it turns off with the off sound on both clients; stash turns it off.
- Needs human: none
- Priority: normal

### Train distance display — queued 2026-11-12
- Commit(s): uncommitted
- Changed: new `RailDistanceDisplay` + "Distance display" TMP label above the fuel gauge on `DuckRailsTrain.prefab`; shows `DISTANCE n m`, `RailTrain.Distance` rescaled so the bridge area (`RailRoute.FinaleStart`, ~780 m of track) reads ~1000 m (run ends ~1096 m). Display only; label hides with the other running labels at run end. Compiles clean, Console clear; not run in Play Mode.
- Setup: any expedition, one client, then a late joiner.
- Check: counter starts at 0 at departure, rises smoothly with speed, reads ~1000 at the bridge approach, is legible from the driver position, and matches on a late joiner.
- Needs human: legibility/placement in headset.
- Priority: low
- Mode: fix-on-fail

### Motel community leisure layout — queued 2026-11-12
- Latest correction supersedes prior pool counts: 48 coal / 64 gold / 32 enemy candidates, unchanged runtime spawn rules. All markers replaced for the current layout; all 100 seeds × crew sizes 1–8 meet full resource/enemy targets, quadrant quotas and spacing (2,691 checks). Inspect reach/return routes, bus-roof gold access, pool basin/upper-room enemy pursuit, ceiling-mounted lights, downward streetlight readability, low-poly wheels, six benches and outward golf/rink sides. No Play Mode run for this correction.
- Detail feedback adds nine unique greens/duck/shoot-through house, shack counter/clubs, golf/rental mesh signs, boarded windows/green handled doors, guest furniture/bathrooms, roof equipment, grey/blue cars, continuous stair/wall/rink surfaces and Quackville pixel materials. Inspect all board/stair/rail joints, furnished-room and bathroom circulation, house passage, wall seams at moving viewpoints, material scale and white-only facade lighting. Remains queued; no Play Mode authorization for this batch.
- Detail checks: 4,186 editor assertions pass, all 24 loot candidates supported/clear and all 18 enemy markers grounded; navigation rebuilt. Current saved layout contains no waterslide. Follow the current prefab and detail notes rather than the older slide-specific checks below.
- Screenshot revision supersedes original layout: seven closed rooms, half-width doors, room 101/102 connection, unglazed windows, stair rails; mesh neon/car improvements; nine-hole golf and relocated shack; north rink/rental entrances, white/blue rink walls; simple side fences, varied convex-collider trees; food vendor/exterior left cubbies/loungers; deeper pool, two diving boards and straight slide; brighter lights.
- Revision checks: 2,186 assertions pass; all loot support/clearance and named doorway checks pass. Check natural room/window passage, closed-room exclusion, internal door, all pool/board/slide recovery, vendor freezer/fryer/shelf/cubby reach, north rental/rink access, fences/trees, lights and peer agreement. Lighting brightness and Quest performance remain manual acceptance.
- Commit(s): uncommitted.
- Changed: separate public pool with change rooms/cubbies/static old waterslide; mini-golf, roller rink, five buses versus two retained cars; motel retained.
- Cheap checks: 2,114 editor assertions pass; all loot support/clearance checked; offline navigation rebuilt. See docs/roadside-motel-layout.md and docs/validation/motel-leisure/.
- Setup: motel-first private expedition, desktop then two real clients/late join; worn Quest separately.
- Check: pool entry/recovery and slide stairs/flume; open cubbies and shower/bench circulation; rink/rental/golf access; bus clearances; carried-item settling; enemy routes and boarding; identical environment/loot across peers.
- Candidate-capacity limitation resolved by the latest correction: 48/64/32 pools pass eight-player maxima and 800 enemy crew/seed selections. Natural gameplay balance and route accessibility still need playtest acceptance.
- Needs human: natural VR reach/locomotion, slide fall recovery, darkness, scenery review and Quest performance.
- Priority: normal.
- Status: queued for playtest; no Play Mode run for this change.

### Convenience Store screenshot polish and arcade mascot — queued 2026-11-12
- Basketball court: rim segments, net strands and playing surface now have fitted boxes; all 121 court colliders pass editor ray checks, hoop centres and south entrance remain clear, and navigation is rebaked. Check hand/item/player contact in the existing playtest.
- Crossing arms: one continuous fitted BoxCollider per lowered arm now replaces segmented collision. Editor rays hit along both full lengths; check avatar/hand/carried-item contact at the tips and stripe seams. Queued for playtest.
- Collision/sparse-stock follow-up: trees now collide across trunks and canopy tiers, all six rooftop models have shape-matching collision, and seven brush roots have stationary full-size hulls with rotating visual children. Check head/hand/carried-item contact and walking routes, particularly beneath trees and around wash rollers; ensure the Q opening remains open. Solid cooler glass/displays/fan/ceiling fixtures also collide. Shop shelves now have only nine scattered brown cubes; cooler shelves have no drinks, retaining coal/gold candidates. Verify loot reach and settling after local collision repairs; queued for playtest.
- Latest collision editor evidence: 1,848 saved-prefab checks pass, 184 new collider shape-ray checks, empty Console error query, offline navigation and lighting rebuilt (1,272 valid bindings). See `docs/validation/convenience-round2/collision-and-stock.txt`; earlier round-two totals below are historical.
- Q-logo follow-up: corrected inverted ring faces/normals; inspect the exterior and inner opening from both sides under the rooftop spots. Mesh winding and Unity preview checked; headset appearance queued for playtest.
- Round two follow-up: preserve the user's moved semi/deletions; inspect the court's open south-side gap (no swinging gate), off-court light and denser trees; lowered red/white railway road barriers; simple blue van; closed ornamental restaurant rear doors and empty framed windows; staggered wash pillars/horizontal rear brush; party carpet/yellow-pink arcade and eight back-room machines; all tree canopy undersides. Five new vendor roof models plus arcade duck have paired spotlights. Store has four upright drinks coolers (two open), varied shelf stock, six inventory racks and long fluorescent fixtures with one burnt out. Gas neon is cyan/teal again, superseding red below.
- Round two editor evidence: `docs/validation/convenience-round2/`; loot redistribution follows model changes, same candidate counts/identities and per-run settings. Check all restaurant loot, open drink-cooler reach, stockroom aisle and moved-trailer cargo. Rear restaurant doors must block exit. Validate cosmetic brush motion, both crossing road arms, rooftop spotlight readability and burnt-out fixture appearance. Compare shared-neon-atlas/combined-detail GPU and draw cost on Quest; performance acceptance is pending.
- Commit(s): Convenience Store polish commit; see Git history.
- Changed: bilateral streetlights, picnic grove/tables, rail-facing car wash/access road and rotating bristle rollers, northwest basketball court/open gate, wooden side fences, twelve scattered cars, enterable articulated semi, open freezers with loot, distinct restaurant interiors, shell/paint/sign repairs, rooftop equipment and low-poly arcade duck mascot. Wall triangle count reduced 86.4%; static details consolidated.
- Cheap checks: round-two compilation and 1,852 saved-prefab checks pass, including 800 enemy selection cases; navigation and lighting rebuilt/exported (1,450 bindings, two lightmaps). Console has an unrelated Unity Connect token-exchange error, no compile/block-authoring error. Round-one evidence (4,010 assertions/1,109 bindings) is historical. See `docs/convenience-store-layout.md` and `docs/validation/convenience-round2/`.
- Setup: Convenience Store first in a private expedition; one desktop client, then two authenticated clients including late join; worn Quest separately.
- Check: enter/exit court through the side gap; traverse car wash and rear road past rotating bristles; reach semi via ramp/open doors and recover trailer loot; reach open cooler loot; traverse each restaurant from its front and both arcade exits around the doubled middle cabinet row. Check enemy pursuit, roof spawns, vehicle clearance, side fence/seam behavior and loose-item recovery. Confirm paint does not flicker/collide and floating sign fixtures are gone. Review teal gas, red Noodle Box, green wash, added lamps and rooftop models at actual expedition darkness. Brush motion must remain cosmetic and must not move or trap players/items. Check both clients agree on loot and environment.
- Candidate pools: 126 coal / 109 gold / 27 enemies; per-run counts unchanged. Trailer/freezer bars are candidates, not guaranteed simultaneous contents.
- Needs human: mascot/art review, natural hand reach, full avatar/headset traversal, lighting/glare, draw cost and Quest frame time.
- Status: queued for playtest; no Play Mode run for this change.
- Priority: normal.

### Skyscraper north/south yards and upper-floor loot — November 11
- Side walls: check 3 m concrete walls at X +/-47, ground contact/collision along both full lengths, entry/exit seam transitions, no stranded loot/enemies, square pixel density and headset appearance.
- Foundation light: check grounded southwest tripod, broad cool forward illumination across the pit and onto the excavator, and glare/foot clearance on headset.
- Crossing lights: review the 23 baked warm point lights on all four skybridges and the fallen crane for nighttime visibility and glare, including carried-item traversal.
- Rooftop follow-up: check the four grounded tripod light stands for visual attachment and unobstructed walking/grabbing on +16 m floors.
- Follow-up: eight workbenches now have legs/braces; brighter baked spots plus warm/cool floods. Check bench collision, nighttime visibility and glare on all floors and in both yards.
- Status: pending, queued for playtest.
- Scope: skyscraper-works-v1 only; preserve the existing tower geometry.
- Checks: enter/exit both office trailers; walk the loading/maintenance shelters; enter the foundation and recover via each ramp; carry coal/gold out without catching on edges. Inspect worklights, windows, pipes and clutter at night on headset. Review upper-floor loot across repeated seeded runs (270 candidates of each type; per-run totals unchanged). Test enemy pursuit around new yards, late-join consistency, moving-train clearance and target headset performance.
- Editor evidence: docs/validation/skyscraper-polish; tower/rail preservation, spawn support and 1,000 seeded selection cases passed. See docs/skyscraper-layout.md for bake and final validation evidence. No Play Mode run performed for this change.

### Farm full screenshot polish — queued 2026-11-11
- Basement holding cages: central cage/four watching chairs, four side cells, three open lockers, four green-white baked fixtures. Added two coal/four gold candidates (pools now 105/117/22); per-run settings preserved. New loot checks and eight doorway clearance samples pass; navigation and lighting rebuilt. Queued for playtest: enter/exit every cell, reach locker loot, return through both basement exits, enemy pursuit, bar shadows and headset darkness/performance. See `validation/farm-polish/basement-checks.txt`.
- Silo ceiling follow-up: enterable silo underside now uses planar XZ texture coordinates (1.8m repeats). Geometry and baked UV2 retained; all 16 underside UV triangles pass. Inspect ceiling texel scale from doorway/in motion; queued for playtest.
- Corn follow-up: existing 3,669 stalks/52 patches retained; five leaves become four (20% fewer leaves, 102,732 -> 88,056 triangles). Geometry-scaled UVs replace stretched stem/leaf mapping. Saved mesh checks and Console pass; close-up editor capture inspected. Check in-headset texel scale, field concealment/readability and performance in motion. Queued for playtest.
- Silo surface follow-up: doorway overlap removed; grain uses planar UVs and golden dirt. Retained user's grain/shell offsets and adjusted nine silo loot heights. Check threshold flicker and grain texel scale in motion. Current broad checks flag seven roof/hay loot footprint/clearance issues outside the silo; resolve against the user's latest geometry when running this queue.
- Commit(s): uncommitted.
- Changed: furnished farm buildings/house; pond/entrance baked lights; railway-facing shed doors; ditch water audio; low-poly side fencing; elevated silo interior with wraparound ramp/loot; house floor/attic roof/stair/cellar repairs. Local user edits and per-run settings preserved. Pools 103 coal / 113 gold / 22 enemies.
- Cheap checks: Unity compilation, Console error query, 791 geometry/loot/reference/light checks, 35 entrance/closure/export checks and 1,000 seeded selections pass. Navigation and lighting rebaked/exported. See `docs/validation/farm-polish/` and `docs/farm-silos-layout.md`.
- Setup: private Farm route, one desktop client then two authenticated clients with late join; headset review separately.
- Check: all four nine-step house flights traverse without falling through side/bottom gaps; compact cellar is enterable/exitable; attic has no floor flicker or roof obstruction. Reach barn loft and silo grain, traverse ramp turns both ways, collect/return loot and recover drops. Furnished paths and doorways stay passable. Ditch produces wet footsteps; bridge stays dry. Review entrance/pond lights. Enemies pursue around structures; peers agree on scenery and seeded loot.
- Needs human: larger stair-rise and ramp comfort/hand locomotion, loft reach, water sound quality, nighttime readability and Quest performance.
### Attached hold-forward throttle — queued 2026-11-11
- Solo live follow-up: PASS on both hands for injected physical grip, smooth forward arc GO, forward jitter tolerance, pull-back OFF, re-push GO, release and sideways breakaway. Attached-palm query/closed finger curl pass; sampled palm gap 6.3 mm. Shared target speed/lease zero afterward; normal movement/input restored and existing Play session left running. See `docs/validation/shared-rig/throttle-hands-20261111.txt`. Remaining cases below, actual skin-contact visual review, headset feel and multiplayer remain queued; this is not full acceptance.
- Attempt 2026-11-11: user authorized solo desktop simulated-hand Play Mode. Verified Duck Rails in Edit Mode, then `RailTestRunner.Start("sandbox")` and the follow-up state query each timed out after 120 seconds. Editor process remained present; computer-use window activation failed twice (`failed to activate captured window`). Runner state remained `launching`. No interaction checks ran; no PASS claimed. On recovery inspect editor/runner state and stop through `RailTestRunner.Stop` to restore the prior scenario before retrying. Do not discard the dirty Farm bake scene.
- Commit(s): uncommitted.
- Changed: attached local/replicated palm and closed fingers; arc-driven lever; GO at 85%, OFF at 72%; 18 cm breakaway; detent haptic/audio; immediate state-edge requests. Keyboard T toggle retained.
- Cheap checks: Unity compilation, 20 Edit Mode drive checks and empty Console error query. See `docs/shared-rig.md#attached-throttle-trial--november-11`.
- Setup: private running expedition, shared DuckPlayer simulated physical grip first; two authenticated clients with late join/master handoff; worn Quest separately.
- Check: either hand acquires exclusively, palm stays on handle throughout slow/fast strokes and train motion; raw controller remains independent. Hold forward sustains GO with small tremor tolerance; pulling back while gripping cuts GO, pushing again resumes. Release, sideways/upward separation beyond 18 cm, downing, blocked input, focus loss and disconnect stop driving and restore free hand pose. Denied/lost lease does not attach or steal control. Peers see attached hand and matching lever; late join/host departure preserve safe ownership. Test keyboard T and ordinary pickups afterward.
- Needs human: hand/finger alignment, comfortable travel/tolerance, fast-stroke breakaway, mechanical sound and haptics on both controllers.
- Priority: normal.

- Priority: normal.

### Bridge finale batch polish — queued 2026-11-11
- Stationary gold-crate lighting: finale-only UV2 mesh copy, Contribute GI, Receive GI Lightmaps and block bake/export installed; 17 editor checks pass. **Queued for playtest:** baked crate readability beside the button and unchanged intake/coin behaviour. Reward-text fields were already null in the saved baseline; floating reward text is not validated by this change. See `output/level-polish-batch/followup/gold-crate-checks.txt` and before/after views.
- Bank lighting correction: static far-bank/tunnel floor now uses its own exported lightmap instead of one probe sample across the whole renderer. One finale bake/export and 23 editor checks pass; 1,741 bindings installed, all 22 user-edited lights preserved. Compare `output/level-polish-batch/followup/bank-light-BEFORE.png` / `bank-light-AFTER.png`. **Queued for playtest:** entrance light stays local on grass during approach, tunnel passage and dawn transition; floor/bridge remain visible on clients/headset.
- Tunnel-floor correction: restored the 149 disabled bake-excluded finale renderers and corrected their visibility lifecycle. Twenty-four EditMode checks pass, including export/reload persistence, Expedition visibility and tunnel floor collision. **Queued for playtest:** confirm visible floor throughout the tunnel and intact moving drawbridge on actual clients/headset; no Play Mode run for this fix.
- Follow-up November 11: items 17–21 add subdivided/higher-detail ground lighting, two thin middle ramp slabs, four movable fixture/light groups, dark non-overlapping bank lip, and perimeter walls extended to Z80. One lighting export installed (1,740 bindings), navigation rebuilt (72,503 nodes), and 1,862 scoped editor checks pass. Review `output/level-polish-batch/followup/`. Still **queued for playtest**: traverse both thin slabs and inspect underneath, test outer-wall/river junction access, view the dark bank lip in motion, inspect red light pools from headset height, and measure Quest performance/memory. Total loaded mesh cost is 44,230 triangles and 1,900 mesh renderers; editor evidence does not establish device performance.
- Commit(s): November 11 commit titled `Polish bridge finale geometry and baked lighting`.
- Changed: all 16 approved finale polish requests: ramps/inward access, climb blocks, asymmetric crates, low walls, concrete seam fixes, grounded cliff/tunnel, recessed stream, gravel/gables, floodlights and red warning caps. Two on-stair coal candidates move to adjacent flat roofs; counts and tower defenders retained.
- Cheap checks: compilation and Console error query pass; 1,375 geometry/spawn checks and 398 final bridge-sweep/train-clearance/reference checks pass. Both tower ground paths to the button pass; navigation and lighting baked/exported, 1,740 bindings installed in Expedition. See `docs/level-polish-batch.md` and `output/level-polish-batch/`.
- Setup: private test route to bridge-finale-v1, one desktop client followed by two authenticated clients with a late join; Quest review separately.
- Check: all ten ramps traverse without snagging, both river switchbacks return to the fortress, both four-block climbs reach north platforms, and crate groups preserve patrol circulation. Two tower defenders can reach the button without route traps. Hold/pause/resume and the full bridge sweep remain functional; train passes the tunnel and reaches dawn. Confirm no visible flickering on north concrete/river landings, tunnel gaps, open gables or exposed side-hill holes. Inspect recessed stream/collision and ballast from low viewpoints; ensure gravel does not obscure rails. Coal on X±23/Y4.2/Z62 settles and remains reachable. Peers/late joiners agree on geometry and loot counts.
- Needs human: natural VR ramp and block-climbing feel, hand clearance on recovery rails, night/dawn lighting and emissive readability, valley/cliff sightlines from elevated train positions, and Quest CPU/GPU/memory comparison. Geometry has 109 fewer renderer objects and 5,718 more serialized triangles; no frame-time claim.
- Priority: normal

### Farm northeast pond and playground — queued 2026-11-11
- Commit(s): uncommitted
- Changed: replace northeast corn with shallow pond, 108 horsetail stems, three playground platforms/two bridges, static slide/ladders, empty swing frame and hay/cart/fence dressing. Net whole-Farm reduction 43,104 triangles (21.5%). Reposition 14 coal/14 gold and two enemies; append four enemy candidates. Per-run counts unchanged.
- Cheap checks: compile, navigation bake, lighting bake/export and scoped saved references; loot footprint/clearance, enemy navigation/bounds, sampled deck/pond collision, 800 enemy crew/seed selections and 200 maximum-crew loot selections pass. See `docs/validation/farm-paddock/`. Broader catalogue lighting check has unrelated helipad light-count failure.
- Setup: private test order containing Farm, one desktop client then two authenticated clients with late join; Quest review separately.
- Check: reach all three platforms via ladders/bridges/slide without snags; traverse pond banks and recover loose items from its 0.38 m depression; inspect hay/cart loot support and natural reach. Enemies can approach/pursue through this area without trapping on new geometry. Peers and late joiners reconstruct matching candidate selections. Swing frame has no hanging parts or simulated physics. Review night/flashlight readability and measure Quest CPU/GPU time against the prior cornfield.
- Needs human: hand climbing, slide and shallow-water feel, loot reach/readability, headset performance.
- Priority: normal
- Mode: fix-on-fail

### Results board superlatives: stats, counters and award assignment — queued 2026-11-12
- Commit(s): uncommitted (B clone)
- Changed: honks removed; board shows Coal, Gold (deposited), Revives, Downs; 20 superlatives + Precious Cargo assigned once at the finish (`RailAwards`), counters from the master's 10 Hz sample and a batched client `stats` request. Spec: `docs/game-design.md` "Superlatives and board stats".
- Cheap checks: `Duck Rails/Legacy/Run Superlative Checks` (`RailAwardsChecks`) PASSED 2026-11-12 in B's editor (MCP verified `Application.dataPath` = `X:/Work/duck-rails-b/Assets`): project compiled with 0 errors (only pre-existing warnings) and the menu item logged "25 superlative checks passed". Edit-mode only; no Play Mode, network or headset run. Later the same day the new `Boundaries` method (20 awards x 2 assertions) first failed at Pacifist (float `RunSeconds * .8f` rounding); after `Present()` and Window Seat were changed to exact integer multiples, it recompiled with 0 errors and the menu item logged "65 superlative checks passed" (all awards' minimums are inclusive, just-below gets no title).
- Setup: private run to a bridge finish with 2+ clients (3+ ideal), a shop purchase, a gold sale, a revive, a hit on an enemy, a flashlight on for a minute.
- Check: board rows show Coal/Gold/Revives/Downs and exactly one title each; no title repeats; the top stat holder gets the title; players under every minimum read Precious Cargo; a non-escaper reads Missed the Train unless a higher title applies; titles are identical on every client and after a master handoff or reconnect before the finish; failed runs show titles too.
- Needs human: whether thresholds, the 15 m / 2 m proximity radii and the stillness limit feel right with real VR hand jitter.
- Priority: normal
- Mode: report-only

### Train aboard margin: escaped and end-of-run teleport — queued 2026-11-11
- Commit(s): 84985d551
- Changed: `RailTrain.ContainsPadded()` (Inspector-tunable `paddedMin`/`paddedMax`, yellow gizmo when selected) now decides who "Escaped" at the bridge finish and whether the end-of-run rescue teleport fires. Horns, boarding and cargo keep the tight `Contains()`.
- Cheap checks: compiles; fields read back on the train prefab instance. Not run in Play Mode.
- Setup: private run to a successful bridge crossing, one desktop client (plus a second client if available).
- Check: a player hanging off the side by hand, or standing on the front of the train, at the finish is listed "Escaped" and is not teleported. A player on the ground well away from the train still reads "Didn't make it" and is teleported to the deck.
- Needs human: hanging off the side by hand in VR.
- Priority: normal

### Expedition render warmup: flashlight and helicopter TV first use — queued 2026-11-11
- Commit(s): 1adf32ebe
- Changed: on every Expedition load, the transition veil stays up while `ExpeditionRenderWarmup` draws each scene and catalog render state with lights off and with a warm spot light, through the viewer camera and a copy of the helicopter feed camera. See docs/transition-veil.md.
- Cheap checks: compiles, Console empty. Not run in Play Mode.
- Setup: Compound → private run departure, one desktop client in DUCKZ_DIAGNOSTICS, then a second departure in the same session; one late join aboard a running train from Login.
- Check: log `[ExpeditionWarmup] Prepared N render states...` with N > 0 on the first departure, no "time limit", and far fewer states on the second departure. Veil reads ALL ABOARD / "Getting the train ready...", then releases. A late joiner sees no uncovered Expedition frame before the veil. No leftover "Warm proxies" object or extra camera after release. Scene lights such as the helicopter light come back on afterwards. Editor-only: proxies visible in the Scene view during the warmup is fine.
- Needs human: Quest development build profiled on a cold shader cache. Confirm the first flashlight or helmet light-on and the first helicopter TV view produce no shader or pipeline-creation spikes, and record the added veil time.
- Priority: normal

### Compound Discord button — queued 2026-11-11
- Commit(s): uncommitted
- Changed: local JOIN DISCORD physical button with a "Join the Discord!" heading on the inner right partition of the Station Info nook; opens https://discord.gg/sfyvhahnDW.
- Cheap checks: installed via Unity MCP, installer validation (callback, URL, CutePixel labels) passed, no Console errors; camera capture shows readable placement.
- Setup: Compound, one desktop client and a Quest client.
- Check: desktop click opens the invite in the default browser; headset touch opens it in the Quest browser. Repeated contact within 3 s opens it once. Returning to the game leaves the session usable; no room transition.
- Needs human: Quest browser launch/return, headset reach and readability at duck height.
- Priority: normal

### Compound rating button and instruction PNG — queued 2026-11-11
- Commit(s): uncommitted
- Changed: local RATE GAME physical button and replaceable 1024 × 768 PNG canvas in the Station Info nook; Quest requests native Meta rating/review UI, desktop opens the configured app's store listing.
- Cheap checks: Unity 6000.3.24f1 compiled and installed in batch Edit Mode, exit 0; callback, texture, canvas and font references passed; no C# errors or exceptions. Scene diff limited to the new instance.
- Setup: Compound, one desktop client and a Quest client authenticated through the normal login flow.
- Check: desktop click opens Duck Rails' store listing; headset touch requests the native rating UI without an eligibility precheck. Cancel/close returns to usable gameplay. Failures display a retry message, repeated contact does not repeatedly launch requests, and no room transition or reward occurs. Replace PNG while preserving its .meta and confirm the saved canvas picks up the new art.
- Needs human: actual Meta review-page launch/cancel, headset reach/readability and normal session recovery after system UI. Do not submit a rating as part of testing.
- Priority: normal

### Pause menu and rooms: headset buttons and feel (5aaa6ffdb, d480df22a) — queued 2026-11-11, manual-only
- Commit(s): 5aaa6ffdb, d480df22a; fix 1f1203b1c (automated part PASSED 2026-11-11, see Results)
- Check: Start button on a Quest controller and the P key open/close the menu (key input was not simulated). Poke/grab feel of the menu, keypad and gate LAUNCH EXPEDITION button. Hint readability at duck height.
- Needs human: all of the above.
- Priority: normal

### Test panel on L3, admin-gated: controllers and dev builds (7ae3a4e70, d46433b7b) — queued 2026-11-11, manual-only
- Commit(s): 7ae3a4e70, d46433b7b; help-text fix aeb8c3051 (editor toggle PASSED 2026-11-11, see Results)
- Check: Editor VR rig: L3 opens/closes the wrist panel and the menu button opens the pause menu. Esc closes the keyboard panel. Dev build: admin L3 opens the panel after sign-in; non-admin L3 does nothing and the menu button opens the pause menu; no gold bar drops on L3.
- Needs human: controllers, admin and non-admin accounts on a headset.
- Priority: normal

### Stranded return: three clients and the original repro (d9ab4c4fc) — queued 2026-11-11, manual-only
- Commit(s): d9ab4c4fc (2-client cases PASSED 2026-11-11, see Results)
- Check: with 3 clients (so the expedition room stays open), disconnect a non-master during Ending and rejoin after the return token; it must leave within about 5 s. Confirm the original three-player headset win no longer leaves anyone aboard.
- Needs human: headset repro. The 3-client rejoin case can be run with the bridge (see testing-tools) when a third account is configured.
- Priority: normal

### Finale defenders, loot, gold crate, water, walls: headset feel (0a062e440, 2b8c0d3e8, a38008a7b, 3a3056c87, 1f35eb82b) — queued 2026-11-11, manual-only
- Commit(s): as listed (automated parts PASSED 2026-11-11, see Results)
- Check: tower spawn presentation and defense difficulty; loot placement taste, roof/tower reach, resource balance; coin shower into the depositor, deposit sound and +100 text as seen by others; water splash/bubble/muffle levels and teal fog in a headset, Quest fog cost; invisible-wall contact, hand-climbing the wall and the instant fall-recovery warp. Economy balance at 100 coins per bar.
- Needs human: all of the above.
- Priority: normal

### Zombie hit shapes: in-game throws and swings (dcd2f9e1, 62ea1a73) — queued 2026-11-10, manual-only
- Commit(s): f073d29a4; fixture fixed in b76f4618f (automated part PASSED, see Results)
- Changed: zombie hit shapes follow animated bones (all five enemies); thrown items hit the earliest-contacted enemy; Spider head hit radius 23 cm.
- Setup: 1 client in Play Mode (headset preferred): throw/swing bat and gold bar at each enemy type, including crowded pairs and a locked (just-hit) zombie.
- Check: hits land on visible limbs/head; a throw hits the nearer of two zombies; deflect direction sane; held-swing feel unchanged.
- Needs human: all of the above (visual fit on animated enemies), throw and grab feel in headset.
- Priority: normal

### Enemy tuning + late-join flares: in-game (22262f75, 98ec9f5b, 6f4c3cba) — queued 2026-11-10, manual-only
- Commit(s): 1ea23d07f; fixture fixes 136fe60ea, 8005743a5 (automated part PASSED, see Results)
- Changed: speeds rebalanced, retreat 4–12 s, all zombies fear 40 s flares, flares restored for late joiners, smaller kill triggers, slower spider stalk.
- Setup: 2 real game clients: client A dies (flare), client B joins late, once on the ground and once aboard a moving train.
- Check: B sees the active flare with correct remaining life; enemies of every type avoid it; flare expires ~40 s. Known gap: flares are world-space, so one dropped on a moving train is left behind (not train-relative); record what B sees, do not treat as a fix target here.
- Needs human: speed/kill-trigger feel in headset; real-game late join (not covered by the batch fixture, which uses inert avatars).
- Priority: normal

### ZombieHook pull: no snapback, hands/gear clear of surfaces (6f27966d, 7b5dad14) — queued 2026-11-10, manual-only
- Commit(s): see git log "Port Duckz ZombieHook pull"; batch fixture re-run on HEAD 2026-11-10 PASS (see Results)
- Changed: hook pulls no longer snap the VR player back; while pulled, hands, held weapons/items and a hand-held bag stay out of floors and walls (locally and as sent to others), releases mid-pull drop instead of fling, and observers keep dropped items above the floor.
- Setup: 2 clients (A in headset). Force `RailHook` spawns. Pull A (1) on the ground near a wall, (2) standing on the moving train, including near the cabin walls/rear, (3) on a curve. Repeat with bare hands, nail bat, gold bar, and a hand-held bag; release the item mid-pull once inside and once outside the train cargo volume.
- Check: no rewind after the pull; no hand/bat/bag visibly inside geometry for A or B; train travel neither lost nor added; item released on the train becomes cargo and rides along, one released off it falls in place; B never sees a dropped bat tip through the floor.
- Needs human: headset hand/arm feel during and after the pull, comfort aboard the moving train.
- Priority: normal


## Results

### Queue run 2026-11-11 — live multi-client portions (Unity 6000.3.24f1, MPPM, tested at f7aeaf8de + fixes)
Two real clients: the main editor and the MPPM Player2, on the Photon cloud and backend, with the desktop simulated rig. Unity MCP drove the main editor. Player2 was driven through the new test-only `RailRemoteBridge`; see [testing tools](testing-tools.md). Buttons used their own `Press()` paths. P, L3 and Start were not simulated: an unfocused editor drops injected key input. Full evidence: [2026-11-11 live run](validation/playtest-queue/2026-11-11-live-queue-run.txt). The working tree also had unrelated uncommitted train/bridge-finale prefab edits.

- **Pause menu in Compound and Expedition — PASS** (automated part), after one fix. The menu opens 0.5 m in front of the head (the prefab value, same as Duckz; the entry said 1.2 m). It is grabbable and all four pages switch. The camera tablet opens above the menu and closes. Closing keeps the same room in Compound and Expedition. The GAME MENU path opens it and death closes it. FAIL first: opening the tablet logged two kinematic-velocity warnings. Fixed in `1f1203b1c`; the re-run was clean.
- **Pause-menu rooms, gate launch button, keypad removed — PASS** (automated part). (a) The keypad create/join put both clients in private staging 48213, and both gates show "lebo, push to start!". The gate is hidden in public compounds. The terminal is gone and the CutePixel hint is present. (b) The non-master's press was refused, even with a forced event. The owner's press departed both clients. (c) In an expedition the Room page shows only the code and Back to Compound. Back moved Player2 alone; main stayed aboard. (d) From a public expedition with no compound open, a fresh compound was created. After a staging owner handoff, the prompt names the new owner.
- **Test panel on L3 — PASS** (editor toggle only). The overlay-button path `Toggle()` opens and closes the panel without opening the pause menu, and GoldBarDropper is gone. Stale "P opens the test panel" help text was fixed in `aeb8c3051`. L3, the menu button, Esc and dev builds remain manual.
- **Midpoint and finale loot — PASS** (automated part). Each special block has 6 coal and 2 gold on both clients, with identical ViewIDs, tokens and poses, resting on real surfaces. After a Player2 rejoin, a master handoff and a late rejoin, the loot is the same with no duplicates.
- **Gold crate depositor-only payout — PASS** (automated part). Master, non-master and late-joiner deposits each paid +100 to the depositor only, with one recipient each. The observer saw "+100" in CutePixel at the crate. No replay after a rejoin or late join. The shower and sound were not audible-checked.
- **Player walls and fall recovery — PASS** (automated part). Walls sit at ±48 m on three blocks, with none on the track line. Below-floor falls recover in about 0.27 s of game time, with no down. A repeat fall within 6 s steps back one entry. Lateral recovery works past the wall. A fall from the moving train returns the player to the stored train-local spot. Coal and gold below the floor are destroyed on both clients.
- **Water volumes — PASS** (automated part). With the head under the lake: Water surface, a 900 Hz low-pass and the teal exponential fog. Teleporting out while submerged restores the exact original fog and removes the filter. In the finale river the floor is Water and the head stays dry.
- **First-hold finale tower defenders — PASS** (automated part). There are no defenders before the first hold. A non-master hold spawned exactly one RailZombie per tower, with the same views on both clients, and they walk to the button. The pair is unchanged after release, repress, a master handoff, a late join during the paused hold, and simultaneous holds. The bridge paused, resumed and completed, and a fresh run spawned a new pair. One earlier run ended "Bridge collision": a test artifact from an injected simulated throttle press.
- **Stranded non-master leaves the ending train — PASS** (2 clients, not 3). (a) A normal public return puts both clients in one fresh compound in about 6 s. (c) Cutting the client's crew-return control connection at the token made it leave and join the crew's compound. (b) The disconnect/rejoin case could not keep the room open with 2 clients: Player2's rejoin hit "Room not found" and it fell back into the crew's compound. Separately, an MPPM-only exception killed one handoff before it started. That client rode the train for about 90 s, then the new safety net moved it into the crew's compound, within the ~100 s limit. The clone was missing `Library/ScriptAssemblies`; that environment issue is fixed (see testing tools).

### Queue run 2026-11-10 — automated portions (Unity 6000.3.24f1, batch, tested at HEAD 5b21d0009 + fixture fixes)
Run in the ignored disposable copies `tmp/duckz-port-validation` and `-2` (synced to `main`), separate batch processes. Manual/headset parts were not run and remain in Pending.

- **Zombie hit shapes — `GrabbablePlayChecks`: PASS** (1 process). Initially FAIL at "Actual grab": `PhotonMasterHandoff` exists in Play Mode and the unfocused batch process has no input focus (`handCanGrab=False avail=False`). Fixture-only fix `b76f4618f` grants focus as `ZombieHookPullChecks` does. Re-run passed: idle/moving held and thrown sweeps, frontmost forearm contact in both spawn orders, cooldown front lets rear take the hit, dormant/respawned Bomber. [Evidence](validation/playtest-queue/2026-11-10-grabbable-play-checks.txt)
- **Enemy tuning + late-join flares: PASS** (fixtures).
  - `DeathLifecycleChecks` (1 process): FAIL ×3 then PASS. (1) "Expired network event ignored": the port missed 98ec9f5b's second check hunk (30 s is no longer expired with a 40 s lifetime); fixed `136fe60ea` with the source expression. (2,3) `DuckHomeChecks` follow/conversation assertions: Rails `PhotonPlayer.ForActor` only resolves remote avatars with a received pose (d7ddf37db), the imported fixture never gave its players one. Fails identically when run alone, so it predates the port. Fixture-only fix `8005743a5`. No production code changed.
  - `SmasherRetreatJumpChecks` (1 process): PASS ("ALL PASSED"; `Run` does not exit by itself, process stopped after the pass).
  - `FlareJoinNetworkChecks` (2 batch editors, real Photon cloud): host and guest PASS: late join reconstructs both flares once with original launch time, RPC/property dedup, master leaves mid-smasher-fall and the successor lands it once while respecting the flare, flares survive the publisher leaving, independent expiry, expired deaths never replay. First attempt BLOCKED by the second copy failing its first-open package resolution; passed after warming it.
  - [Evidence](validation/playtest-queue/2026-11-10-enemy-tuning-flares.txt)
- **ZombieHook pull — `ZombieHookPullChecks` re-run on HEAD: PASS** (24 pull + 6 surface cases, same values as the port run). Everything else in that entry is headset/two-client. [Evidence](validation/playtest-queue/2026-11-10-hook-pull.txt)

## Dropped

_(empty)_
