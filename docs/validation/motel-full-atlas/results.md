# Full Motel atlas performance comparison — November 13, 2026

User requested Quest performance testing before fixing remaining visual issues.
No art changes or production rollout are authorized by this performance result.
Completed one valid same-build pair at 120 m. This supports continued Motel atlas
work: draw calls decrease substantially and CPU frame times improve, while the
forward GPU tail is slightly worse. This is scoped rail-view performance evidence,
not full visual/runtime/multiplayer acceptance.

## Accepted captures and settings

- Clean signed development build succeeded with zero errors in 6m17s; fixture
  substitution confirmed for Expedition ([build record](build-substitution.txt)).
  Unity 6000.3.24f1, build GUID `ae35b53f4dcc4a75a631d99166d52b34`, APK SHA256
  `13B497ECA209843ACB8669EFDF7CE698324EB4AA239868C52A0B3AB7E9DD7011`.
- Control `a7a40f66723d4375ae52740a33c1b156`, atlas
  `164ad1442f6141b4b7235a136929864e`. Both metadata files confirm the requested
  full-map variant; wing variant is empty. Four measured Motel views each, all
  100% focused/tracked, approximately eight seconds / 576 frames per view.
- Same seed 12345, Motel/Convenience/Farm/Skyscraper order, 120 m clip, 20-second
  warmups/compound and eight-second view windows. Native Mobile/OpenXR/Vulkan,
  72 Hz, 1344x1408 eyes, 0.8 render scale and 4x MSAA. Recorded CPU/GPU levels 2/2
  and foveation 0 in both runs. No clock/resolution/refresh overrides.
- Control waited for positional tracking at startup; user repositioned the headset.
  Invalid startup frames are excluded. Atlas tracked throughout. Both PC helpers
  exited successfully after their requested 120-second expedition timers, using
  the documented save/cancel path. Raw status says `failed / Cancelled by PC`, with
  `capture-stop.json` recording the intentional stop. Control total 220 s, atlas
  180 s; these are partial routes, not route-completion claims.

## Measured changes

Median CPU/GPU milliseconds and draws, control -> atlas, by yaw:

- 0: CPU 11.60 -> 11.05; GPU 9.85 -> 9.90; draws 1,298 -> 984 (24% fewer).
- 90: CPU 10.10 -> 9.70; GPU 7.90 -> 7.50; draws 429 -> 146 (66% fewer).
- 180: CPU 10.00 -> 9.40; GPU 8.50 -> 7.90; draws 456 -> 231 (49% fewer).
- 270: CPU 9.70 -> 9.60; GPU 6.50 -> 6.00; draws 356 -> 106 (70% fewer).

CPU p95 improves in all four views (12.5/10.4/10.5/10.8 -> 11.8/10.1/9.9/10.1 ms).
GPU p95 is 10.0/8.0/8.7/6.6 -> 10.4/7.6/8.1/6.1 ms: the forward view regresses
0.4 ms despite fewer draws. All sampled per-view CPU/GPU p95 values remain under
the 13.89 ms budget. Native FPS medians remain 72–73 at the 72 Hz cap; no displayed
FPS gain claimed. Native stale-frame samples across views total 2 control / 3 atlas,
too sparse to establish a reliable hitch trend.

Submitted triangles increase in every view: 83,202 -> 146,723 forward;
55,196 -> 78,809 right; 98,912 -> 129,083 back; 38,098 -> 61,008 left. The geometry
cost is visible even though CPU/draw savings generally outweigh it in these views.
Unity used memory is approximately 1,157–1,158 MiB control / 1,157 MiB atlas; both
alternatives remain resident, so this does not establish release memory/load cost.

Actual stopped train distance is 160.548 m control / 160.724 m atlas. Median head
positions differ about 0.176 m; pitch differs 0.23 degrees and yaw under 0.01 degrees.
Head height is 1.445 m in both; pitch approximately -24 degrees. The strict 5 cm
pose criterion remains false. Treat this as a useful initial pair with a small
viewpoint difference, not perfectly locked poses. End battery temperature 35 C
control / 36 C atlas, both charging at 100%; sequential thermal/clock variation is
not isolated. OVR frame battery-temperature values were zero; use device battery
reports for those temperatures.

[Per-view CSV](first-pair/comparison.csv), [JSON with pose flags](first-pair/comparison.json).
Raw frames, metadata and native metrics are under ignored `output/quest-performance`
run folders. Reproduce with `Tools/summarize-quest-performance.py` on each folder,
then `Tools/compare-motel-wing.py <control> <atlas> <output> --scope full-map`.

Recommendation: keep this atlas approach for Motel and address the user's visual
issues next. Preserve the measured baseline before further geometry/material changes.
Do not extend the result to other maps or claim shipping performance acceptance.
Interiors/combat, culling/rebinding cycles, worn-headset appearance, sustained load
and real multiplayer remain queued for playtest.

## Cleanup

Verified no game PID, simulated proximity false and Meta CSV recording false,
matching starting settings. Unity remains in Edit Mode with the full-map review
scene clean/open, build flag false and no Console errors. Production prefab SHA256
`2035190C9CE33425E025EE52DFFB19728B99F266853806E0E2D0995EE70D9183` and Expedition
scene `A5039498B89CC5375B9CF30936D41E652B9F0D2379BAA84C0790993DFF683B17` are unchanged.
Quest retains the explicit full-map comparison development APK. Normal runner
builds clear the scene cache and do not inject the fixture. No art fixes were made.

## Fixture and build correction

One development APK contains exact production consolidation meshes/materials
(767 active Motel mesh renderers) and the full-map atlas (315). Ten original
renderers are common; the selector switches 757 control-only renderers against
305 atlas chunks. Collision, gameplay and lightmap identities are shared.
Both render alternatives are resident, so shipping memory/load savings are not
measured. [Fixture checks](benchmark-setup.txt).

The first build reported success, but Unity reused the processed scene from the
older wing comparison. Rejected control run `de8ec73b7c3048b890eee7993142f9f6`
stopped on entry to the first Motel view after 118 seconds. Metadata reports
`appliedMotelWingVariant=control`, empty `appliedMotelFullMapVariant`; the explicit
full-map guard correctly rejected it. Tracking was valid, but **no measured Motel
performance from that run is accepted**. Build GUID
`43c592610cc24c23a442afd4bce5f914`, APK SHA256
`BF375E50FEBF2F71B190D51A47F15A7E41F8080BEC37598402D22FA6672D85B9`.

Correction: the shared performance build helper now uses `CleanBuildCache` because
incremental scene caching cannot track the editor-only comparison scope. The
full-map builder records scene substitutions and rejects a build if none occurred.
This also prevents a later ordinary runner build from retaining a cached comparison
scene. Saved production scenes/catalogue remain unchanged.

Device access initially required an authorized outside-sandbox ADB retry; it then
succeeded. No diagnostic reports were inspected.
