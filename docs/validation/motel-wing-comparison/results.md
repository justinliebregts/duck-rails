# Motel east-wing atlas comparison — November 13, 2026

User approved Blender and Unity review, then authorized device comparison. No
production rollout performed. The user subsequently accepted the 56 cm stopped
train position difference and explicitly requested no repeat.

## Setup and scope

- Verified Duck Rails Android editor, Unity 6000.3.24f1. Signed development APK
  succeeded with zero build errors in 4m10s; install succeeded.
- Build GUID `85fde19b7aa448cda42a2e0bfefff0c7`; APK SHA256
  `44BAB712910FF4696DB59A30732CD49B939914BC8D2CB945BB498154E2531DB3`.
- Same build, seed 12345, Motel/Convenience/Farm/Skyscraper order, 120 m override,
  20-second warmups/compound, eight measured seconds per viewpoint, 120-second
  expedition timer. Mobile/OpenXR/Vulkan, 72 Hz, 1344x1408 eyes, 0.8 scale, 4x MSAA.
- 590 common outside-wing chunks, 130 original-material spatial wing chunks versus
  29 atlas wing chunks. Control is boundary-clipped original-material scenery, not
  an exact copy of the production Motel's whole-block chunk partition.
- Both render alternatives are resident in the same prefab/build. This isolates
  active rendering better than different APKs, but cannot quantify shipping mesh
  memory or loading savings. The rest of the review prefab itself is NOT this fixture.
- [Setup checks](setup-checks.txt): 2,745 nonnull lighting bindings, source collider
  count retained (1,900), saved selection, valid/invalid request guards. 720 generated
  common/control meshes use 16-bit indices where capacity allows. Collider count
  alone is not collision equivalence or traversal acceptance.

## Captures

- Control: `d3b673afc1ff4c6890ad1524d5a6cdda`, 199 seconds, 12,340/13,995 overall
  valid frames; invalid startup excluded. All four Motel views 100% tracked.
- Atlas: `359d60f2da0444aaa6fdae3bdb996dc7`, 266 seconds, 12,407/18,759 overall
  valid frames. Startup positional tracking recovered after user repositioning;
  recovery preceded warmup. All four Motel views 100% tracked.
- Both saved via the intentional timer cancellation path, retrieved fresh VrApi/
  OVR metrics, and stopped normally. Metadata confirms the requested variant and
  120 m override actually applied. Raw terminal `failed / Cancelled by PC` is the
  established short-capture convention, not a product failure.
- Third control `56bf1a2d98ff4c46a3f8a10eb64c7c4e` was cancelled at user request
  during login, 66 seconds, zero valid frames. Excluded completely. Helper exit 1
  reports manual cancellation, not evidence of a product regression.

## Measured results

Median CPU/GPU milliseconds, control -> atlas, by yaw:

- 0: CPU 11.6 -> 11.8; GPU 10.1 -> 9.9; draws 1,307 -> 1,299.
- 90 (wing-facing): CPU 10.15 -> 9.9; GPU 8.0 -> 7.2; draws 411 -> 309.
  About 25% fewer whole-frame draws and 10% lower GPU median in this sampled view.
  Submitted triangles increase 52,331 -> 64,543, consistent with atlas UV splitting.
- 180: CPU 10.0 -> 10.1; GPU 8.7 -> 8.3; draws 445 -> 424.
- 270: CPU 10.1 -> 9.6; GPU 6.5 -> 6.4; draws 356 -> 354.

All view p95 CPU/GPU values stay below the 13.89 ms budget in both runs. Highest
per-view p95 CPU/GPU: control 11.9/10.2 ms; atlas 12.7/10.3 ms. In particular the
forward CPU p95 worsens by 0.8 ms, so this is not a uniform performance win. Native
one-second FPS medians are 72-73 at a 72 Hz refresh cap; no increased displayed
frame-rate claim. Unity memory during the four views is about 1,133-1,135 MiB in
both; both alternatives remain loaded, so this is not shipping-memory evidence.

The pitch difference is only 0.017-0.021 degrees, but the actual train stop/head Z
differs 0.556 m: train distance 160.604 m control versus 160.048 m atlas. Head Y is
1.445 m in both. The comparison CSV flags the strict 5 cm pose criterion false.
User accepts this tolerance for the initial experiment; retain the flag and qualify
the conclusion rather than relabeling it a locked-pose A/B. Headset repositioning
did not materially change pitch. Temperature ended at 34 C control / 35 C atlas,
charging, battery 100%; sequential clock/thermal variation is not isolated.

Conclusion: promising modest wing-facing draw/GPU savings despite more triangles;
CPU and worst-view headroom are mixed. This supports further Motel-specific work,
not a catalogue-wide conversion or a proven overall FPS gain. Shipping memory,
load cost, runtime hide/show/rebinding, interiors/combat, worn-headset and real
multiplayer acceptance remain open.

[Per-view CSV](first-pair/comparison.csv), [JSON and limits](first-pair/comparison.json).
Reproduce summaries with `Tools/summarize-quest-performance.py`, then
`Tools/compare-motel-wing.py <control-folder> <atlas-folder> <output-folder>`.

## Cleanup and retained state

After user cancellation: no game PID, simulated proximity false and Meta CSV
recording false, matching all three runs' starting settings. Sandbox initially
blocked ADB server startup; authorized outside-sandbox preflight succeeded. No
diagnostic reports read. No additional captures after the user stopped the repeat.

Production prefab SHA256 unchanged:
`2035190C9CE33425E025EE52DFFB19728B99F266853806E0E2D0995EE70D9183`.
Expedition scene SHA256 unchanged:
`A5039498B89CC5375B9CF30936D41E652B9F0D2379BAA84C0790993DFF683B17`.
Comparison catalogue substitution occurs only in the explicit development build's
processed scene, not saved scenes/assets. Installed Quest APK remains this
development comparison build; normal build command does not inject the fixture.
