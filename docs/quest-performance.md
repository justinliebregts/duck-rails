# Quest 3 automated performance testing

## Setup status — November 13, 2026

Requested: repeatable native Quest 3 testing without manually playing the game.
The user subsequently finished the Unity session and authorized implementation
and device testing. The opt-in runner and PC controls are implemented and installed;
Unity compilation and request-guard checks pass. Native capture, automatic travel/
refuelling, measured viewpoints, collection and cleanup passed on the Quest.
The user clarified that a few representative minutes are sufficient for initial
tuning; full-route completion is not required. Default PC runs now stop after
180 seconds from the first observed Running expedition state, plus startup time.
Keep the headset upright in a lit room with unobstructed cameras. No manual game
interaction is needed. Longer route/thermal/multiplayer acceptance is separate.

Read-only device checks found an authorized Quest 3, Duck Rails package
`com.gunraiders.duckrails` version 0.2.0 (200), and OVR Metrics service version
88.0.0.1.0. The installed game's package output did not report DEBUGGABLE.
The standard CapturedMetrics directory did not exist: this is not an empty or
successful performance capture. ADB initially could not start its local server
inside the sandbox; the authorized outside-sandbox retry succeeded.

## Available PC helper

Full Motel atlas comparison: explicitly build through **Duck Rails > Tests > Quest
Performance > Build Motel Full Map Comparison**, then use `-MotelFullMapVariant
control` and `-MotelFullMapVariant atlas` on otherwise matched runs. This is separate
from `-MotelWingVariant`; requests cannot select both. Runtime metadata records the
applied full-map variant and rejects an unfulfilled selection before measured
views. Analyze with `Tools/compare-motel-wing.py --scope full-map`. Both variants
are resident; shipping memory/load savings are outside this comparison.

Run from the repository root:

```powershell
./Tools/quest-performance.ps1
./Tools/quest-performance.ps1 -Action Install
./Tools/quest-performance.ps1 -Action Run
./Tools/quest-performance.ps1 -Action Run -FarClipMeters 50 # explicit benchmark-only experiment
./Tools/quest-performance.ps1 -Action Run -DurationSeconds 0 # optional full survey
./Tools/quest-performance.ps1 -Action Status -RunId <id>
./Tools/quest-performance.ps1 -Action Cancel -RunId <id>
./Tools/quest-performance.ps1 -Action Collect -RunId <id>
./Tools/quest-performance.ps1 -Action CollectMetrics
python ./Tools/summarize-quest-performance.py ./output/quest-performance/<id>
```

Use `-Serial` if multiple devices are attached and `-Adb` for a specific SDK's
adb.exe. Preflight verifies the device model and installed app, then saves build
and battery/charging metadata under ignored `output/quest-performance/`.
Build from **Duck Rails > Tests > Quest Performance > Build Device Runner** in
an idle Android editor. Output is `output/quest-performance/duckrails-quest-profile.apk`.
Uses existing signing/versioning and enabled scenes, IL2CPP Development, without
Deep Profiling or profiler autoconnect. Restores the previous development/AAB
editor flags. Normal release builds exclude the runtime runner entirely.

Install updates the existing app, preserving its data. Run restarts that app,
pushes a one-shot request, temporarily simulates headset proximity, launches the
game, polls progress, pulls results and stops the app. It snapshots/enables the
installed OVR Metrics CSV recorder and runs a hidden PID-filtered `VrApi` metrics
log stream. Only newly created game CSVs are collected into the run's `ovr-metrics`
folder; older exports are not silently reused. It restores the original recording
and proximity states in `finally`; it does not change guardian/dialog settings,
resolution, refresh rate or clocks. Keep the headset upright in a lit room.
If a PC process is forcibly killed, restore simulated proximity with
`adb -s <serial> shell am broadcast -a com.oculus.vrpowermanager.prox_far` (only
if it was originally false; see the run's device-properties-before.txt).

Run defaults: 180 seconds after expedition startup (PC polling, roughly five-second
precision), seed 12345, 20 seconds compound warmup, 20 seconds compound capture,
20 seconds station warmup, 8 measured seconds per viewpoint after 2 seconds
settling, and a 1,200-second overall deadline. `-Blocks` optionally takes exactly
four distinct catalogue IDs in order; omitted uses normal seeded selection.
Use `-WarmupSeconds`, `-CompoundSeconds`, `-ViewSeconds`, `-TimeoutSeconds` to vary
duration. Never compare runs with different settings as equivalent workloads.
`-FarClipMeters 0` (default) preserves authored camera settings; 10–2000 overrides
the local player's XR cameras only while an explicitly armed development capture
runs. Original values are restored during cleanup; ordinary launches and release
builds are unchanged. Metadata records the requested/applied distance and cumulative
number of camera objects overridden (three per local rig, including replaced rigs
across scene changes). Request guards reject nonfinite/out-of-range distances.
`-DurationSeconds 0` disables the short capture timer. Short runs deliberately use
the installed runner's save-and-cancel path: raw status remains `failed / Cancelled
by PC`, while `capture-stop.json` records the intentional stop. The helper accepts
only its own timed stop as normal completion; actual failures still fail. The new
PC timer passes syntax checks but has not been device-rerun, per the user's request
to stop collecting. The underlying cancellation/save path has device evidence.

CollectMetrics retrieves existing OVR Metrics exports and fails explicitly when
unavailable. The built-in runner writes its own CSV and does not require that
external recorder. No action reads the diagnostic inbox or game diagnostic
reports, or clears device logs. If Python is not on PATH, this workstation has
`C:/Program Files/Blender Foundation/Blender 5.3/5.3/python/bin/python.exe`.

## Implemented first-pass scope

- Explicit opt-in development runner, inert during ordinary play. A PC command
  arms one bounded run; consume its request so restarting the app cannot keep
  rerunning the test. Use a new private room, real Meta/backend authentication
  and existing PUN session APIs. Abort on login, room or world-readiness timeout.
- Fixed seed and recorded block order. First pass uses the normal four-block
  route size; separate coverage passes include Motel and other requested blocks.
  Loading every catalogue block together would misrepresent ordinary memory cost.
- Record compound baseline, departure/loading, normal-speed train travel, fixed
  viewpoints in each block, and a longer sustained pass. Keep actual XR stereo
  rendering, lighting, AI, physics and loose items enabled. Label any invincibility
  or movement shortcuts and restore callbacks/settings on exit.
- Automate stationary aboard placement/view direction without replacing the XR camera with
  a desktop camera. Capture actual poses and focus state; reject suspended,
  unfocused or untracked intervals. Determine on-device whether unattended
  rendering remains active; a sleeping headset is not a low-cost game result.
- Capture app CPU/GPU times, dropped/stale frames, actual refresh rate, resolution,
  foveation, memory and thermal/clock context where the platform exposes them.
  Unsupported counters must be absent/flagged, never reported as zero cost.
  Separate loading peaks from steady state and report percentiles per segment.
- Use a development build without Deep Profiling for diagnosis, then repeat with
  release-equivalent settings to quantify instrumentation overhead. Match the
  actual refresh rate's frame budget; do not silently change clocks or resolution.
- Solo testing establishes one-client cost only. Add actual peers for crew/voice/
  replicated-item load; spawned dummy ducks do not establish eight-client cost.

The existing November 10 Quest profile in [transition-veil.md](transition-veil.md)
is useful startup evidence but does not establish current sustained performance.
The installed Meta SDK's `OVRPlugin.GetAppPerfStats` explicitly warns that it is
unsupported on OpenXR. Instead, the collector uses the installed OpenXR provider's
`perfmetrics.appcputime` and `perfmetrics.appgputime` counters in milliseconds,
as verified by that package's `MetaPerformanceMetricsTest.cs`. On the tested Quest
build those provider counters are unavailable, so raw
`OVRPlugin.GetPerfMetricsFloat(App_CpuTime_Float/App_GpuTime_Float)` values are also
recorded. These are seconds; the summarizer converts them to milliseconds. The
compound GPU median (4.5 ms) matched both the native `VrApi` App field and OVR CSV
`app_gpu_time_microseconds` (about 4500), establishing the conversion on this device.
This API is distinct from unsupported `GetAppPerfStats`.
Unsupported or nonpositive time samples are blank, not zero. Unity ProfilerRecorder main/render
thread durations can include waits and are not active CPU-time measurements.

Each run uses actual login and a newly created private solo PUN room. It closes
that room to joining, selects a deterministic four-block route, enables the
existing test invincibility callback, refills fuel before each leg, drives normally and stops at each block's
track midpoint, the helicopter shop and a safe finale approach. Four stationary
head-aligned yaw views are taken at each stop. The normal tracked pitch is retained
and recorded, so a headset lying face-down is not an equivalent view. This is a
rail-view survey, not automated interior exploration or combat coverage.

The request is consumed at startup. The runner aborts on deadlines, unexpected
room/authority/crew changes, sustained focus loss or a cancelled request. It
restores route/invincibility callbacks and disconnects the private benchmark room
without triggering success rewards. Host Run stops the app afterward.

`frames.csv` contains Update-to-Update wall time (not displayed FPS), supported
CPU/GPU timings, refresh rate, compositor drops, rendering/GC/memory counters,
train/head pose and sampled device context. `valid=0` means no full XR focus and
tracking; `valid=2` means viewpoint settling; only `valid=1` enters summaries.
Loading is labelled separately. Missing counters use blank or -1 as documented
by the summarizer. Frames are buffered in a fixed 150,000-entry array (size recorded
in metadata); capture file writing is excluded, but the buffer's memory overhead
and five-second heartbeat overhead remain part of the development-build test.
OVR raw values currently retain 0.0001-second precision (0.1 ms). Native performance
reports are one-second aggregates, aligned to segment times using device UTC; the
summary excludes reports spanning a segment/settling boundary. Raw CSV exports
are also retained for the richer clock/thermal/memory counters.
The summary also checks all 24 expected yaw segments for measured frames and at
least 95% focused/tracked coverage; inspect `coverage.valid_solo_survey`. A terminal
`complete` alone does not establish usable view coverage. The OVR battery-temperature
API returned zero on this build (unsupported); use the external battery/CSV context.

Meta documents OVR Metrics CSV export at
https://developers.meta.com/vr/documentation/native/android/ts-ovrmetricstool/
under `/sdcard/Android/data/com.oculus.ovrmonitormetricsservice/files/CapturedMetrics/`.

## Validation

The PC preflight helper passed against the connected Quest 3 on November 13.
It saved device/build/battery metadata without changing device or Unity state.
Subsequent device runs created and retrieved new app-specific OVR Metrics CSVs.

Runtime/editor compilation and request validation passed November 13: two valid
configurations, six rejected malformed/unsafe requests and the runtime logging
policy. Summary percentile and unavailable-counter checks pass. These are
configuration checks, not evidence of successful device gameplay.

Device setup validation: signed APK builds passed with zero errors (initial build
5m09s; instrumentation build 2m28s), and `adb install -r` succeeded. A microphone
permission prompt initially blocked input focus; the user explicitly approved
granting Duck Rails RECORD_AUDIO. The cancelled attempt exported its CSV and
restored simulated proximity. A subsequent startup attempt hit its 180-second
readiness timeout and was correctly recorded as failed. No baseline is claimed
from either attempt.

Instrumented attempt `73ac72b6951b4747b4430c7d7b6a44d4` confirmed every XR focus and
tracking flag true, 9,829/9,829 valid frames, real compound capture and new private
room creation. The first departure request did not take effect; the test remained
in Staging and was cancelled. Added a bounded admission retry for room-setup/CAS
contention, stopping if an observed departure returns to Staging. Cancellation,
native metrics stream, new OVR CSV retrieval, app stop and proximity cleanup passed.
This remains partial setup evidence; the aborted departure is not a route baseline.

Attempt `1c5f34298e654a189dc9adad55336f96` passed departure and all four Motel
viewpoints, then timed out travelling to Convenience Store. Captured distance
remained 160.064 m: normal starting fuel covers 160 powered metres, and the survey
had omitted refuelling. Added an explicit stopped refuel stage before each leg,
using the existing private test fixture and waiting for authoritative fuel >=99
before GO. This is labelled in metadata; it does not test coal collection or fuel
balance. Added fuel/distance/speed/driver ownership to progress status and bounded
GO/STOP retries. No normal gameplay code or resource constants changed.

Partial measured evidence from that attempt: Compound median app CPU/GPU 5.7/4.4 ms;
Motel yaw-0 18.9/16.8 ms with native median 53 FPS against 72 Hz (13.89 ms budget).
Other Motel views reached roughly 71–73 FPS. Both CPU and GPU timings exceeded
budget in the slow view; this is not evidence of a GPU-only bottleneck. The route
was Motel, Convenience Store, Farm, Skyscraper, with native OpenXR/Vulkan, Mobile
quality, 0.8 render scale, 1344×1408 eyes and 4×MSAA. These are development-build,
solo, stationary-head results, not release or multiplayer acceptance.

Refuel build: Unity 6000.3.24f1, signed development APK, zero errors, 1m32s build;
install succeeded. APK SHA256:
`A28883D1C4B911A19986045DB375A158034EBB092A36F222054ED6518BAF823E`.
Run `3f7a1d075339437b882d52e2ec483c16` timed out at 180 seconds with 0/12,848
valid frames. Compound was Ready, app/VR/input focus and user-presence were true,
orientation tracked, but positionTracked/headTracked remained false throughout.
The runner correctly withheld gameplay measurements. User was asked to reposition
the physical headset; no reply before this attempt ended. This does not validate
the new refuel stage. Battery was 75%, AC powered, 38°C at startup.
Afterward, verified no app PID, simulated proximity false and native CSV recording
false, matching pre-run settings. Unity Console has no errors. Captures remain
under ignored `output/quest-performance/<id>`; no diagnostic reports were read.

## Representative capture — November 13, 2026

After the user repositioned the headset, run `e1dc96566b4e4ab0ab45c83e870e9221`
captured 483 seconds and 28,226 frames (28,097 focused/tracked). The user stopped
it after sufficient data; this is intentional partial coverage, not a gameplay
failure. All four scavenging blocks and the helipad were sampled, with 20 yaw
segments. Automatic refuelling and resumed normal-speed travel passed repeatedly.
Convenience Store yaw-90 had only 88.4% tracking coverage and is excluded from
native comparisons; the other 19 views passed the 95% coverage check.

Representative valid-view medians at 72 Hz (13.89 ms budget): Motel yaw-0 CPU/GPU
19.2/17.5 ms and native 51 FPS; Convenience yaw-180 19.4/19.3 ms and 47 FPS;
Farm yaw-0 14.1/20.7 ms and 45 FPS; Skyscraper yaw-0 13.4/18.5 ms and 52 FPS.
Compound was 5.6/4.4 ms and about 72 FPS. These show view-dependent CPU and GPU
pressure, with GPU time dominant in the cited Farm/Skyscraper views. They do not
identify the expensive draw calls/scripts without a focused follow-up profile.
Battery temperature was 38°C before/after, AC powered, battery 77% to 76%.
The app stopped, recording returned to false, and proximity remained true,
matching this run's original state. CSV and JSON summaries are in the ignored
run folder; use the same settings/viewpoints for optimization comparisons.

## Validation still required

### Motel consolidation pilot — November 13, 2026

Implemented at the user's request, without a new device run. See
[Motel authoring instructions](roadside-motel-layout.md#motel-render-consolidation-pilot--november-13-2026).
The prefab retains editable source scenery and switches to offline-generated
spatial render chunks at runtime. 1,905 source renderers / 1,954 nonempty submeshes
become 698 single-material/lightmap chunks. Including untouched surfaces, active
Motel mesh renderers fall from 1,974 to 767 (61.1%). This is an asset count,
not a measured whole-frame draw count or FPS improvement.

All 36,636 combined triangles are retained; every triangle corner's position and
atlas UV was checked. Cells are 12 m laterally/longitudinally and 6 m vertically;
large surfaces, transparent materials and scripted/movable hierarchies are excluded.
Original collision, gameplay components, source transforms and 1,964 lightmap
bindings remain intact. No lighting/navigation rebake was needed. Empty material
slots disappear in derived meshes; shared source meshes/materials are unchanged.

Compile, saved-mesh reload, source fingerprint/build guard, activation/restore,
repeat activation, changed-source rejection and stale-lightmap fallback pass in
Edit Mode. [Generation](validation/motel-render-chunks/generation.txt) and
[checks](validation/motel-render-chunks/checks.txt). Console query also contained two
Unity Inspector missing-target exceptions; no C# compilation errors were reported.

Tradeoffs: originals remain loaded for editing/fallback; generated mesh native memory
was 6,262,320 bytes in the editor, not a Quest memory measurement. Original transforms
and renderer components still exist. Chunk bounds can submit more offscreen geometry
than individual pieces, and reflection-probe selection can differ. Actual lighting,
culling, initialization, memory/load cost and frame-time gains remain unverified.
Queued for playtest: matched Quest A/B using component enabled/disabled, same build
settings/route/poses as baseline, plus close/interior views and translated blocks.

Check the new default timed stop on the next requested capture. Full six-stop /
24-view coverage is optional, and `coverage.valid_solo_survey=false` on an
intentional short capture does not invalidate its adequately tracked segments.
Release-equivalent overhead comparison, sustained thermal repeats, additional
four-block sets, interior/combat viewpoints and actual multiplayer loads remain
separate work. No queued gameplay item is verified by the PC preflight.

## Slowdown investigation — November 13, 2026

Read-only analysis of run `e1dc96566b4e4ab0ab45c83e870e9221` and the saved prefabs,
using the verified Duck Rails editor in Edit Mode. No new gameplay run or asset
optimization was performed. Counters are whole-frame Unity counters, not attribution
to the named block; current and adjacent blocks remain visible. Excluded the
tracking-rejected Convenience yaw-90 view from comparisons.

The strongest lead is scenery rendering/submission cost. Motel yaw-0 had median
7,268 draw calls, 156,998 triangles, CPU/GPU 19.2/17.5 ms. Motel yaw-270 had
2,691 calls, 38,986 triangles and 12.3/9.9 ms. Convenience yaw-180 had 8,629 calls,
133,726 triangles and 19.4/19.3 ms, versus yaw-270's 1,404 calls and 9.6/7.8 ms.
The same stationary scene's simulation continues while view direction changes;
this strongly prioritizes rendering, but is not a script-level CPU attribution.
Farm yaw-0 was GPU-dominated at 20.7 ms versus 14.1 ms CPU. Its yaw-180 rendered
more triangles (195,778 versus 119,288) yet took only 9.5 ms GPU, so raw triangle
count alone does not explain the problem. Pixel shading/overdraw and small draws
need isolation. Skyscraper GPU medians were 14.5–18.5 ms across all four views.

Prefab audit:
- Motel: 1,974 mesh renderers; Convenience: 1,370; Farm: 953; Skyscraper: 3,404.
  None have BatchingStatic flags or LODGroups. Route blocks are instantiated at
  runtime; no StaticBatchingUtility or CombineMeshes call was found in Scripts.
- The shared depot material authoring path creates nine submeshes/material slots
  for every mapped mesh (`RailDepotMaterials.MakeMesh`/`Map`). Motel has 692 such
  renderers: 656 use only one nonempty submesh, 21 use two, 15 use three.
  Convenience has 365: 286 use one, 69 use two, 10 use three. There are 5,485 and
  2,831 empty slots respectively across those renderer instances. This is proven
  unnecessary layout complexity, NOT proof that each empty slot costs a GPU draw.
  No material-array-overflow/repeated-last-submesh defect was found.
- Repeated small objects dominate: Motel has 299 fence uprights and 143 vertical
  bars; Skyscraper has 658 guard posts, 608 floor slabs and 640 stair-flight pieces.
- `RailBlockVisibility` culls only blocks more than one block from the viewer.
  It does not provide fine-grained scenery distance/occlusion culling. It correctly
  leaves gameplay/physics loaded. Fog does not perform this culling.
- Mobile URP already has SRP Batcher on, render scale 0.8, HDR off and main/additional
  light shadows off. It uses 4x MSAA; runtime foveation was zero. Thus "turn off
  shadows" is not the first fix. SRP Batching does not merge these objects into
  a few geometry draws: see [Unity batching guidance](https://docs.unity.com/en-us/engine/6000.0/manual/analysis/graphics-performance-profiling/reduce-draw-calls/optimizing-draw-calls-choose-method).

Recommended order, each checked separately with the same short Quest capture:
1. Compact empty submesh/material slots in the shared authoring pipeline and
   mapped assets, preserving nonempty material correspondence, UVs, UV2, bounds
   and lighting references. This is a low-visual-risk first A/B, not a promised
   9x speedup. Fix the authoring source too so regeneration does not undo it.
2. Batch static scenery in spatial chunks by compatible material/lightmap. Start
   with rails, fences, building trim and Skyscraper repetitions. Preserve collision,
   interactions, render visibility groups and baked lighting; do not merge a whole
   block into one uncullable mesh. Compare draw count, CPU/GPU time, memory and load
   cost before expanding. Static flags alone will not batch runtime prefab instances.
3. Add measured detail culling/LODs within fog, then isolate Farm foliage and
   Skyscraper pixel cost. Brief same-view A/Bs of resolution, MSAA and foveation can
   distinguish pixel cost; do not lower global quality as the first response.
4. Profile scripts/GC after the render changes. Measured GC allocations are about
   37–44 KB/frame p95, worth reducing, but this capture has no collection pause or
   named-method timings proving GC, AI, physics or networking is the dominant cause.

No obvious thermal deterioration: native battery readings stayed 37–38°C and
power_level_state stayed 0 across 476 reports. Shader-hitch counter stayed 0.
GPU clocks varied with load (mostly 545 MHz); temperature alone cannot rule out
all power limits. Unity memory stayed roughly 1.08–1.09 GiB during the expedition.
Hardware fragment/vertex cost counters in the exported CSV were zero/unavailable,
so exact shader/overdraw attribution needs a GPU capture or controlled A/B.

### Quest device check — November 13, 2026

User explicitly requested a Quest test with Motel first. Signed development build
succeeded with zero errors in 4m45s; installation succeeded. Unity 6000.3.24f1,
build GUID 60456ebf303d49fb861c54dacec3c290, APK SHA256
27E829479E292D08803A297D635B87C040B93337659A811A040CC4F4EA1D4098.
Run fa93ca6c863246c68abcc707546cac32 explicitly requested and recorded Motel,
Convenience Store, Farm, Skyscraper, matching baseline order/placements/seed 12345.
Mobile/OpenXR/Vulkan, 72 Hz, 1344x1408 eyes, 0.8 scale and 4x MSAA match baseline.

Capture lasted 240 seconds including startup: 16,318/16,318 frames focused/tracked.
All four Motel views have 100% tracking coverage and eight measured seconds each.
The default 180-second expedition timer saved/cancelled the run normally and is now
device-validated. Raw status says failed / Cancelled by PC; capture-stop.json
identifies the intentional timed stop. This is a successful partial capture, not
full-route completion. New native VrApi and OVR Metrics CSVs were collected.

Motel median CPU/GPU milliseconds and native FPS, in yaw order 0/90/180/270:
- 0: 14.7/14.7 ms, 61.5 FPS (baseline 19.2/17.5 ms, 51 FPS).
- 90: 10.3/7.9 ms, 73 FPS (baseline 13.3/10.1 ms, 72 FPS).
- 180: 12.4/10.4 ms, 72 FPS (baseline 13.4/13.0 ms, 71 FPS).
- 270: 9.5/7.1 ms, 73 FPS (baseline 12.3/9.9 ms, 72 FPS).
Native one-second aggregates around 72 Hz can report 73; refresh remained 72 Hz.
Worst-view CPU/GPU p95: 15.7/16.0 ms. Both median and p95 still exceed the
13.89 ms budget there. Yaw-0 whole-frame draws fell 7,268 to 4,946 (32%);
other view draws were 429, 2,491 and 363. Triangles remained broadly comparable.
Motel maximum Unity memory was about 1,103.6 MiB versus 1,085.8 MiB baseline;
this whole-build difference cannot be attributed solely to generated mesh storage.

Limits: headset pitch was about -17.7 degrees versus -10 degrees in the earlier
capture (roughly 8 degrees different); baseline/new battery temperature 38/35 C.
Other concurrent project edits may also be in the new build. This is directional
before/after evidence, not a controlled same-build component-on/off A/B. Remaining
slow views include adjacent unoptimized scenery. No claim of isolated speedup,
visual equivalence, sustained thermals, interior/combat or multiplayer acceptance.

[Comparison CSV](validation/motel-render-chunks/quest-comparison.csv) retains all
Motel summary counters. Raw capture stays under ignored output/quest-performance/
fa93ca6c863246c68abcc707546cac32. Cleanup verified no game PID, proximity true and
recording false, matching original states. Battery remained 76%, 35 C, AC powered.
ADB initially failed under sandbox restrictions; authorized outside-sandbox access
succeeded. No diagnostic reports were inspected.

### 50 m far-clip experiment — November 13, 2026

User requested a deliberately short 50 m experiment and asked about realtime lights.
Added opt-in `-FarClipMeters` to the development runner/helper, default zero (authored).
Overrides only local player stereo cameras, retains/restores original values on
capture cleanup, and records applied distance/count in metadata. No camera prefab,
light settings or release gameplay changed. PowerShell syntax, request validation
(ten invalid cases including nonfinite/out-of-range clip), logging-policy and Motel
build guards pass. Final editor query confirms all three authored XR cameras remain
1,400 m. Normal launches do not arm the runner.

Build: Unity 6000.3.24f1, signed development, zero errors, 2m38s; install passed.
Build GUID a65798b095f842e8a15711fbd505896e; APK SHA256
E91C326F8874A1F2F130A07BCA2F321F253115A496BE8F040D8E58D053D3AB4E.
Both runs explicitly use Motel, Convenience, Farm, Skyscraper, seed 12345,
20 s warmups/compound and 8 s measured viewpoints. Mobile/OpenXR/Vulkan,
72 Hz, 1344x1408 eyes, scale 0.8, 4x MSAA, lighting unchanged.

- 50 m: dc767fd5a9094ad5b4efe0f6d9b6ece5, 241 s including startup,
  16,983/16,983 focused/tracked frames. Metadata confirms 50 m on three XR cameras
  per rig, six cumulative camera objects across compound/expedition rig replacement.
- Normal-distance same-build control: 087c9a2093dc4b27a9d68b6d2c19cf34,
  179 s including startup, 12,015/12,015 focused/tracked frames; override zero,
  no overridden cameras. Used 120 s expedition duration to stop after Motel;
  the 50 m run used 180 s. Per-Motel measurement/warmup settings match.
- Both stopped intentionally via the helper, collected native VrApi/new OVR CSVs,
  and completed all four Motel views at 100% tracking. These remain partial routes.

Motel medians, normal distance -> 50 m:
- Yaw 0: CPU 14.5 -> 10.0 ms; GPU 14.4 -> 7.9 ms; native FPS 63 -> 72;
  whole-frame draws 4,937 -> 450. **View mismatch:** headset moved during the 50 m
  yaw-0 capture; median pitch -9.36 degrees versus -24.17 in control, no measured
  frames within 0.5 degrees of control pitch. Do not claim an isolated percentage
  speedup from this pair.
- Yaw 90: CPU 9.9 -> 9.8 ms; GPU 7.9 -> 7.8 ms; draws 426 -> 432.
- Yaw 180: CPU 12.5 -> 9.6 ms; GPU 9.9 -> 7.1 ms; draws 2,470 -> 317;
  triangles 141,774 -> 56,460. Pitch -24.16 versus -24.07 degrees: substantially
  better matched than yaw 0. About 87% fewer draws, 23% lower CPU and 28% lower GPU
  medians. Both were already around 72 FPS due to the refresh cap.
- Yaw 270: CPU 9.6 -> 9.6 ms; GPU 6.5 -> 6.6 ms; draws 354 -> 355.

All four 50 m Motel views have median CPU/GPU under 13.89 ms; maximum per-view
p95 CPU/GPU 10.6/10.5 ms. Native aggregates 72–73 FPS at 72 Hz. The direction-specific
savings support substantial distant-rendering cost; they do not prove acceptable
appearance at 50 m. Need stationary repeats, natural headset visibility, distant
emissive signs, train headlamp, dawn/finale and multiplayer tests before choosing
any production clip distance. [Comparison CSV](validation/motel-render-chunks/quest-farclip-comparison.csv).
Raw captures remain under ignored output/quest-performance/<run-id>.

Lighting inspection (saved prefabs/config, not per-light GPU attribution): handheld
RailFlashlight/ItemFlashlight: realtime spot, 100 m, 30 degrees, no shadows, initially
off. The runner does not equip/toggle them, so this does not establish crew flashlight
cost. Train forward spotlight: realtime, 32 m, 40 degrees, no shadows; cabin point
light 5 m. Mobile URP uses per-pixel additional lights, limit eight per object,
additional-light shadows disabled. Long/overlapping beams can still cost fragment
shading and light selection; test train light on/off and representative crew beams
separately at a fixed clip distance. Neither light range nor shadows changed here.

Cleanup confirmed no game PID, recording false, proximity true (original state).
Both ended at 35 C; battery 75% at completion. No diagnostic reports inspected.
This experiment does not change the production clipping-distance decision.

### Editor 50 m visual preview — November 13, 2026

User requested 50 m for hands-on editor Play Mode review. Temporary editor-only
`RailFarClipPreview` is enabled by default while this helper exists, covering active
cameras under the local NewPlayer/XR or DesktopPlayer rig. Toggle **Duck Rails >
Tests > Preview 50m player far clip** to restore the original distance, including
while playing. It restores tracked camera values on Play Mode exit and assembly
reload. Toggle preference lasts for the editor session; remove this temporary helper
when the experiment ends. Authoring/Scene view cameras, asset camera settings and
player builds are unchanged. Visual acceptance remains queued for playtest; the
agent did not enter Play Mode for this setup.

### 100 m versus fresh 50 m — November 13, 2026

User requested a 100 m build comparison. Work paused when the user reported Unity
crashing and resumed after their confirmation. Verified Duck Rails MCP target in
Edit Mode. No rebuild was needed: the already installed signed development APK
supports request-specific far clip. Both tests use build GUID
a65798b095f842e8a15711fbd505896e (APK E91C326F...D3AB4E above), explicit Motel-first
route, seed 12345, same warmup/view timing, lighting, 72 Hz, 1344x1408 eyes,
0.8 scale, 4x MSAA, Mobile/OpenXR/Vulkan. Metadata verifies each override.

100 m run: 2c0f87980e1b43018ab3bb67967e5211, 240 s including startup,
16,858/16,858 focused/tracked frames; four Motel and four Convenience viewpoints.
180 s expedition duration. All sampled views passed 95% coverage (each 100%).
Motel CPU/GPU median milliseconds for yaw 0/90/180/270:
11.4/8.5, 10.3/7.8, 9.7/8.1, 9.9/6.5. Corresponding draws:
789, 424, 439, 352. Maximum per-view Motel CPU/GPU p95 12.1/8.8 ms.
Convenience CPU/GPU medians: 12.6/9.7, 11.6/7.7, 11.1/9.4, 11.1/7.3;
maximum per-view p95 CPU/GPU 13.1/9.9 ms. Native FPS remained around the
72 Hz target (one-second aggregates 72–73).

Fresh 50 m control: 7d5da5a38bfa45cea2afb5eb879f3fd0, 306 s including
132 s waiting for positional tracking. The user repositioned the headset; tracking
recovered before warmup/measurement. 12,267/21,668 overall valid frames; the invalid
startup is excluded, and all four Motel viewpoints have 100% tracking coverage.
120 s expedition timer stops after Motel, with the same per-view workload.
Motel CPU/GPU medians: 10.0/7.9, 9.9/8.0, 9.3/7.1, 9.8/6.7 ms;
draws 423, 425, 312, 358. Maximum per-view CPU/GPU p95 11.0/8.0 ms.
Both timers saved/cancelled normally; raw status failed/Cancelled by PC is the
intentional partial-capture convention, not a gameplay failure.

Fresh 50 m pitch was about -23.39 degrees versus 100 m -24.27 degrees (less than
one degree apart), materially better than the moving-head earlier 50 m forward
view. This is still sequential solo sampling, not a perfectly locked-pose or
thermal/multiplayer acceptance test. Forward-view cost for 100 versus 50 m:
+1.4 ms median CPU, +0.6 ms median GPU, 789 versus 423 draws; both reach the
72 Hz cap. Rear view adds about 1 ms GPU; side-view costs are close.
100 m therefore retains much of the measured distance-culling benefit and is a
promising visual-review candidate. It is not adopted as a production setting.

[Fresh comparison CSV](validation/motel-render-chunks/quest-100m-comparison.csv)
contains full per-view counters. Raw runs remain under ignored
output/quest-performance/<run-id>. Both runs finished at battery 75%, 35 C.
Final cleanup verified no game PID, recording false and proximity false, matching
the starting proximity setting. No diagnostic reports inspected. The editor-only
50 m preview remains enabled; normal camera assets/build launches are unchanged.

### Production view distance — November 13, 2026

User selected 120 m gameplay distance, increasing to 175 m when dawn happens.
Installed on the shared DuckPlayer (XR and current simulated desktop gameplay).
All three saved cameras default to 120 m; `RailPlayerViewDistance` applies the
current `RailAtmosphere.PlayerViewDistance` in LateUpdate. Night/pre-dawn stay
120 m; existing successful dawn progress (12–20 seconds after success) smoothly
blends 120 -> 175 m, synchronized with the existing fog transition. Failure cannot
increase it; restore/new-run paths reset to 120 m. Other cameras are untouched.
The obsolete `RailFarClipPreview` editor helper and meta were removed, superseding
the earlier editor-preview instructions and prior statements that production
camera defaults remain 1,400 m.

Explicit development captures can still choose a different clip via `FarClipMeters`;
`RailQuestPerformance.FarClipOverride` has priority and clears during cleanup.
The legacy retired DesktopPlayer fixture is not the gameplay rig; current desktop
play uses DuckPlayer with SimulatedRigInput and receives the same distances as XR.

Compilation and [17 disposable EditMode checks](validation/motel-render-chunks/view-distance-checks.txt)
pass: night/pre-dawn/mid/full dawn, failure, late-arrival dawn value, replay reset,
saved component/three camera values, unrelated cameras and removal of editor override.
The test fixture emitted Unity warnings when assigning stereoTargetEye under URP;
all distance assertions passed and the subsequent Console error query was empty.
Benchmark-priority integration is code-reviewed, not newly live-tested. No Play Mode,
APK rebuild/install or device run was performed for this final setting change.
The installed Quest APK still contains the previous benchmark build; rebuild to
exercise the new production policy. **Queued for playtest:** Player viewing distance
120 m to 175 m at dawn.

### Optimization handoff for the next chat

Current decision, November 13: user accepts the Motel optimization and authorizes
the same approach across the remaining blocks, **Convenience Store next**, without
repeating the full performance experiment per block. Follow
[the reusable workflow](block-atlas-optimization.md): adapt the exporter/material
handling, preserve collision/gameplay/lighting, perform cheap authoring checks and
visual review, queue remaining live acceptance. This supersedes recommendations
below to delay other maps until Motel visual fixes or additional profiling. No
other block was converted by this documentation pass; Motel full-map production
integration remains pending.

Latest full-map Quest result: user requested performance before fixing remaining
visual issues. Same-build 120 m control/atlas pair now recorded; all four views
100% tracked, full-map variants confirmed. Draw calls fall 24–70%, median CPU
improves 0.1–0.6 ms; max-view CPU p95 12.5 -> 11.8 ms. GPU improves in three views,
but forward GPU p95 rises 10.0 -> 10.4 ms. Both versions remain within sampled
72 Hz p95 budget. Pose differs 0.176 m / 0.23 degrees; both versions resident.
[Results, build correction and remaining scope](validation/motel-full-atlas/results.md).
Initial attempt was rejected because Unity reused the old wing scene cache;
performance builds now use CleanBuildCache and the full-map hook asserts/logs
substitution. No art fixes or production integration in that test pass;
runtime/interior/headset/multiplayer acceptance remains queued. The user subsequently
authorized broader conversion as recorded above. This supersedes the no-device-run
status in the historical note below.

Latest full-map follow-up: user authorized extending the approved atlas wing across
the rest of Motel. Separate Blender and Unity review now exists under
`ArtSource/MotelAtlas` / `Assets/DuckRails/MotelAtlasReview`. Open **Duck Rails >
Authoring > Motel > Open full-map Blender review**. 1,964 source renderers become
305 chunks with one shared material/three 1024-square maps; ten originals remain
(315 enabled mesh renderers total, versus 767 with the earlier consolidation).
Collision, gameplay, transforms and original baked-lighting bindings are retained;
[saved-asset evidence](validation/motel-full-atlas/import-checks.txt) and
[workflow/limits](../ArtSource/MotelAtlas/README.md). Repeated UV splitting increases
converted triangles 83,344 -> 168,243, so renderer savings need device measurement.
Corrected visible distant emission bleed by disabling mipmaps on constant-value
emission/surface atlas maps; base-color mipmaps remain for review. Full-map user
appearance review and runtime/performance acceptance are pending. No new Play Mode,
build or Quest run; production prefab/catalogue untouched. The existing wing
comparison build does not inject this new full-map review. Queued for playtest.

Latest November 13 result: user approved Unity review and authorized native testing.
The same-build 120 m wing pair shows yaw-90 draws 411 -> 309 and median GPU 8.0 ->
7.2 ms, with CPU 10.15 -> 9.9 ms. Other views show smaller/mixed changes; forward
CPU p95 worsens 11.9 -> 12.7 ms. All sampled CPU/GPU p95 values are below the 72 Hz
budget. Actual train stops differ 0.556 m despite nearly identical angles; user
accepted that limit and stopped the repeat. No production rollout. See
[full results and remaining scope](validation/motel-wing-comparison/results.md).
Both variants were resident, so shipping memory/load savings were not measured.
This supersedes the pending visual-review/test status in the historical notes below.

Unity import follow-up: Blender appearance approved; isolated review prefab/scene
created under `Assets/DuckRails/MotelWingReview`. Open through **Duck Rails >
Authoring > Motel > Open Blender wing review**. Original production assets untouched;
29 atlas chunks, retained outside-wing portions and existing baked lighting connected.
No tests run; awaiting Unity visual review. The review clone removes the prior runtime
consolidation component and uses original rendering outside the wing, so it is not
yet a controlled performance A/B. See the ArtSource README's Unity import section.

November 13 follow-up: user considers Motel art finished and authorized an east-wing
Blender atlas experiment before tests or broader rollout. Review artifact:
`ArtSource/MotelWingPilot/Motel-EastWing-Atlas.blend`; details and limitations in
`ArtSource/MotelWingPilot/README.md`. 415 intersecting source renderers become 29
review chunks sharing one material/three 1024-square maps. Texture repetition
splitting increases triangle count; no performance benefit is claimed. Production
Motel/collision/gameplay remain untouched; user visual review precedes integration
and tests. No tests run.

- Confirmed production decision: 120 m night/pre-dawn, 175 m dawn, as above.
- Motel consolidation implemented only on Motel: 1,974 -> 767 active renderers;
  editable originals retained, 698 generated chunks, unchanged collision/spawns and
  original baked-lightmap atlas UVs. Extra editor-measured mesh native memory ~6.3 MB.
  Authoring/regeneration/build guards: docs/roadside-motel-layout.md. No Blender rebuild.
- Same-build Quest distance captures are documented above. 100 m and fresh 50 m
  both held sampled ~72 Hz Motel views; 100 m forward CPU/GPU 11.4/8.5 ms versus
  50 m 10.0/7.9 ms, pose within 1 degree. 100 m sampled Convenience also held target.
  Short solo rail views do not establish interior/combat, dawn or multiplayer acceptance.
- Other blocks have not received this full-map atlas workflow. Convenience Store is
  next; broader conversion is authorized without routine per-block benchmarking.
  Farm's previously observed view-dependent GPU/foliage cost remains a recorded
  limitation, not a requirement to repeat benchmarking before conversion.
- Potential follow-ups: smaller scenery/detail culling, material/atlas consolidation,
  repeated-piece batching, foliage/overdraw investigation, and fixed-distance light
  on/off comparisons. Handheld spotlights 100 m/30 degrees; train spot 32 m/40 degrees;
  no shadows; Mobile URP per-pixel additional lights, maximum eight per object.
- Inspect current Git state before edits: changes are uncommitted, with unrelated
  work present. Work on main, preserve it, no diagnostic reports unless requested.
  Verify Unity MCP points to this project. Do not run Play Mode/device tests unless
  requested; follow AGENTS.md and docs/playtest-queue.md.
