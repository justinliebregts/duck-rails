# Quest 3: four versus eight ducks and moving flashlights

Requested overnight device comparison, November 14, 2026. This measures synthetic
avatar rendering/animation and moving lights in the current expedition; it does
not establish real eight-client acceptance.

## Results

**Both workloads generally held the 72 Hz target. The forward Skyscraper view
has little GPU headroom even with four ducks.** View medians ranged
70–73 FPS with four and
72–73 FPS with eight.
Native one-second counters sometimes report 73 at a 72 Hz display; that is counter
quantization, not a higher refresh rate. These are view medians, not minimum
instantaneous frame rates.

Across the 24 matched views, the equal-weight mean of per-view median CPU time
was **9.74 → 9.89 ms**
and GPU time **8.31 → 8.48 ms**
when moving from four rigs/four lights to eight/eight. The observed differences
were **+0.16 ms CPU** and **+0.17 ms GPU**.
These are small measured differences under the tested conditions, not isolated
per-avatar costs or a guarantee of spare capacity during real multiplayer play.

- **Container Yard**: worst CPU/GPU p95 12.5 / 12.3 ms; lowest individual-view median FPS 72.0 / 72.0 (four / eight).
- **Convenience Store**: worst CPU/GPU p95 11.1 / 11.8 ms; lowest individual-view median FPS 72.0 / 72.5 (four / eight).
- **Helipad**: worst CPU/GPU p95 11.1 / 11.9 ms; lowest individual-view median FPS 72.0 / 72.0 (four / eight).
- **Farm Silos**: worst CPU/GPU p95 10.9 / 11.3 ms; lowest individual-view median FPS 72.0 / 72.0 (four / eight).
- **Skyscraper Works**: worst CPU/GPU p95 14.0 / 13.9 ms; lowest individual-view median FPS 70.0 / 72.0 (four / eight).
- **Finale approach**: worst CPU/GPU p95 12.0 / 12.8 ms; lowest individual-view median FPS 72.0 / 72.0 (four / eight).

All **96 measured views** passed capture-validity checks, with **100% focus/tracking**
and 82,942 analyzed frame samples. Four surveys took
42.7 minutes of app capture; measured stationary views
account for 19.2 minutes. The same eye resolution, render scale and 72 Hz refresh
were retained. Battery temperature stayed at 37°C after the first minute.

Largest paired camera position difference was **0.77 m**;
pitch/yaw differences were 0.008° /
0.114°. The largest same-workload repeat
difference in a view's median GPU time was
1.10 ms,
so normal scene/pose/governor variation is material relative to the aggregate
workload delta. CPU and GPU times are parallel pipeline measurements and must
not be added together.

Native stale frames were nonzero; stable medians do not mean every frame was
on time. These counts cover only native sampling windows within measured views,
not the whole session or a compositor dropped-frame rate:

- pass-1-crew-8: `6057bb5e335543febba7b9a858f32163`; 290,990 remote pose/lip applications; 28 native stale frames across 259 sampled seconds. [Summary](pass-1-crew-8-summary.json).
- pass-2-crew-4: `4436d39d1e0e4bfb81fead9f9bb9caf8`; 126,165 remote pose/lip applications; 49 native stale frames across 259 sampled seconds. [Summary](pass-2-crew-4-summary.json).
- pass-3-crew-4: `d6442e6c6bd1485ba7bebf9a1d2f0c7d`; 124,017 remote pose/lip applications; 53 native stale frames across 258 sampled seconds. [Summary](pass-3-crew-4-summary.json).
- pass-4-crew-8: `1cad7412e22c45c6bab96f9ae188558f`; 296,142 remote pose/lip applications; 35 native stale frames across 255 sampled seconds. [Summary](pass-4-crew-8-summary.json).

Accepted capture window: 2026-11-14T10:22:43.4606960Z through
2026-11-14T11:06:21.2037812Z (UTC). The practical finding is that this
synthetic eight-duck workload is feasible in the surveyed views, while the
Skyscraper forward view deserves further GPU headroom work before treating
72 FPS as robust across real gameplay. No performance optimization was applied
as part of this measurement task.

## Test conditions

- One USB-powered Quest 3; Unity 6000.3.24f1, Android Vulkan/OpenXR.
- Same Development IL2CPP APK for all accepted runs; no Deep Profiling or
  profiler autoconnect. Version 0.2.0 (200), build GUID
  `3936abc068214654acc381b642e1e142`.
- APK SHA-256: `0F7614F607FA5D7C845A0B7180BAF7B2D6F29CC8D27E742C09D5D5C6E75572A6`.
- Mobile quality, 72 Hz (13.89 ms budget), 1344 x 1408 per-eye buffer,
  0.8 render scale, 4x MSAA and authored 120 m view distance. No clock,
  resolution or view-distance overrides; frequency governors remain active.
- One real local rig plus three/seven default production duck rigs. Real
  two-arm IK and finger posing; synthetic upper/lower lip movement. Local visual
  hands use animated head-relative targets while retaining real XR head/camera
  tracking and normal local IK/render callbacks.
- Four/eight moving production flashlight lights: spot, range 75 m, intensity
  250, cone 25 degrees, no shadows. One local ForcePixel light; others retain
  Auto priority. Normal URP light selection/culling applies; not every surface
  necessarily evaluates all eight. Extra item meshes/physics are not simulated.
- Extra ducks occupy a forward fan relative to each view, keeping the rigs
  visible together. They wave, bob, animate fingers/lips and sweep lights.
  Common slots and animation phases match between workload sizes.
- Existing solo world AI, physics and loose items remain active. Extra ducks
  add no Photon transport, voice decoding, gameplay registration or enemy
  targets. Resource crew stays one. No cosmetic mix was tested.

## Capture and analysis

Four surveys in order **8, 4, 4, 8**, seed 12345, same route. Each measures four
yaw directions at six stops: 24 views per survey. Each view settles two seconds
then measures twelve seconds. Startup, compound, warmup, travel, braking,
settling and final file writing are excluded from the workload comparison.
Compound samples contain no synthetic load.

FPS uses the game's PID-filtered native VrApi one-second app counter aligned to
device UTC; boundary-spanning reports are excluded. CPU/GPU timings use OVRPlugin
app counters converted from seconds to milliseconds, cross-checked with native/
OVR CSV output. Observed precision is 0.1 ms. Unity Update intervals and main-thread
durations including waits are not presentation FPS or active CPU cost.

Each comparison value is the arithmetic mean of two per-run medians/p95 values,
with individual ranges retained; these are **not pooled percentiles**. Worst p95
is the larger CPU/GPU p95 across both repeats and relevant views. Equal weighting
of 24 views describes this survey, not a real player's time distribution.

The analysis checks completion, rig/light counts, pose/lip updates, native sample
coverage, matched builds/settings and at least 95% focus/tracking per view. Raw
local captures remain under `output/quest-performance/<run-id>/`: frame CSV,
native counters, OVR CSV, requests, status and device context. Repository evidence:
[comparison JSON](comparison.json), [CSV](comparison.csv), [overview](overview.json),
per-run metadata/summaries and [analysis script](analyze.py). Run the script from
the repository root to regenerate comparisons from those local captures.

To repeat with the installed benchmark APK:

```powershell
./Tools/quest-performance.ps1 -Action Run -CrewLoad 8 -DurationSeconds 0 -ViewSeconds 12 -Blocks @('container-yard-v1','convenience-store-v1','farm-silos-v1','skyscraper-works-v1')
```

Use `-CrewLoad 4` for the smaller workload. Rebuild through **Quest Performance >
Build Device Runner** after source changes and install with `-Action Install`.
[Source hashes](source-files.sha256) identify the benchmark code and PC helper.

## Interpretation limits

The headset was stationary, tilted upward about 25 degrees and rolled. This is
not natural interior traversal, close combat, locomotion or crowded-shop coverage.
Automatic braking produces stopping-position differences; results quantify them.
Submillisecond differences cannot be attributed entirely to the added workload.

Rigs and lights change together, so this cannot separate IK, rendering and light
cost. Automatic clocks affect timings. One device and two repeats per workload
do not establish production multiplayer, voice or release-build performance.
Median FPS can stay near 72 despite occasional stale frames. Rounded 13.9 ms p95
is essentially at the 13.89 ms budget, not a large sustained frame-rate failure.

## Build, fixture and cleanup

The final clean-cache build succeeded with zero build errors in 5m18s and was
installed preserving app data. Request guards accept solo/four/eight and reject
thirteen invalid requests; runtime logging policy passes. The verified Duck Rails
editor is idle on Android with no C# compile errors. A later Console query showed
one cancelled Package Manager list-fetch error, unrelated to the successful build.

Earlier eight-duck run `b7b7eb6e23c3458781b19d16743fae72` was cancelled because a
near-camera green mesh obstructed the view. Its precise ordinary-game cause was
not established. The benchmark now stabilizes local visual hands and reasserts
first-person head hiding. Clear headset images were inspected for both workloads
after rebuilding. Earlier four-duck run `3f36b0e72a424b45ac9657c1e27d25c5` is also
excluded because it used the old fixture. One native clang rebuild failed without
a useful diagnostic; an unchanged retry succeeded. None of these attempts enters
the accepted comparison.

The workload is Editor/Development only and requires a one-shot request. Completion
destroys it and restores the local pose callback. Ordinary launch does not spawn
extra ducks/lights. Source changes are uncommitted on `main`, based on
`61ea0c9452024cf78a538391f796b1a7b6055caf`.

Final [device cleanup check](cleanup.json) confirms the game stopped, request file
absent, CSV recording disabled as before, and device properties unchanged
(including pre-existing proximity-close=true). The temporary screenshot was
removed from the device. The benchmark APK remains installed and raw measurements
remain saved locally. Broader gameplay and real-client acceptance stay queued.
