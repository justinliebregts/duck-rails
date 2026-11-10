# Transition veil

November 10, 2026. Masks the main-thread stall when players leave the compound for the expedition (and other in-game world changes). The user asked for this after the departure froze the headset view on a locked frame.

## Cause of the freeze

Departure stacks three main-thread loads:

1. `PhotonSession.LoadWorldScene` – `LoadSceneAsync(Expedition, Single)`. Scene activation (Compound teardown, rig destruction, Expedition `Awake`, the implicit unused-asset sweep) happens in one frame.
2. `RailRoute.BuildValidated` – instantiates four block prefabs and loads/combines all crawlgraphs in one frame.
3. `RailBlockLightingLoader` – serial additive lighting companion scenes, each followed by `LightProbes.Tetrahedralize()`.

In-engine fades or canvases freeze with the app. Only compositor layers keep rendering with head tracking during the stall.

## Implementation

`Assets/Scripts/Gameplay/RailTransitionVeil.cs` (persistent, self-bootstrapping):

- **Engages** when `PhotonSession.InWorldTransition` becomes true (a room request, a scene load, or a required scene that is not loaded yet). It engages only after the first world-ready state, so the Login flow keeps its own status UI.
- **Releases** once `WorldReady` has held for 0.175 s, so route build and lighting loads also stay covered. Fade in takes 0.15 s and fade out 0.25 s. These were halved from 0.35 / 0.3 / 0.5 s on November 10 because the hold and most of the fade-out add directly to the player's wait, and the fade-in delays private-run scene loads. Public transfers hide the fade-in behind the Photon room change.
- **Headset**: two `OVROverlay` layers (the Meta XR OpenXR feature is enabled):
  - an opaque black 8 px cubemap, `compositionDepth` 1;
  - a 1.6 × 0.8 m message panel, `compositionDepth` 0, placed 2.2 m ahead at engage time.

  The depths are deliberately the reverse of the SDK comment ("lower values render first"). Over Quest Link, cube 0 / panel 1 drew the cubemap over the panel. Cube 1 / panel 0 shows the panel, and swapping them back reproduced the problem.

  The panel pose is held fixed in tracking space every frame, which compensates for rig warps and the camera swap. Alpha fades through `colorScale`.
- **Desktop/mirror**: a ScreenSpaceOverlay canvas (black plus text).
- **Message text**:
  - "ALL ABOARD" when heading to the expedition;
  - "HEADING HOME" when returning to the compound;
  - "RECONNECTING" during connection recovery;
  - "LOADING" otherwise.

  The live `PhotonSession.Status` shows underneath. All text uses CutePixel SDF (the TMP default). The panel texture is re-rendered only when the text changes, via an on-demand camera and a hidden canvas that is active only during that render.
- `LoadWorldScene` now waits up to 0.75 s (`RailTransitionVeil.WaitUntilCovered`) for full cover before starting the scene load. The Photon message queue is already paused at that point.
- `PhotonSession.InWorldTransition` and `HeadingToExpedition` were added for the veil.
- Editor checks:
  - `RailTransitionVeil.EditorRenderPanelPreview(title, detail)` renders the headset panel offscreen.
  - `EditorForce` holds the veil on through the normal path.
  - `EditorHideCube` hides the black cubemap, to inspect the panel alone.

## Validation (editor, desktop, solo)

- Compiles with no new warnings. Console showed no errors during the runs.
- Manual sandbox (Login → public compound → private room → departure), traced per frame: [trace](validation/transition-veil/sandbox-trace-20261110.csv).
  - The public-to-private room hop was veiled.
  - Alpha reached 1.0 before the Expedition scene load started (frame 690; load frame 691).
  - The 10.3 s single-frame editor stall after scene load (route build) occurred at alpha 1.
  - The veil released after Ready plus the hold, then faded out.
- The first run exposed a race: a room request issued in the same frame as the first world-ready state was not veiled. Fixed with a `LateUpdate` readiness observation. The second run confirmed the fix.
- Offscreen panel render: [preview](validation/transition-veil/panel-preview.png).

### Quest Link (November 10)

Editor Play Mode in VR over Link. The compositor output was captured with `OculusMirror.exe` (PrintWindow every 0.4 s). Evidence:

- [overlay trace](validation/transition-veil/link-overlay-trace-20261110.csv)
- [during the stall](validation/transition-veil/link-during-stall.png)
- [panel alone](validation/transition-veil/link-panel-only.png)
- [panel on black after the depth swap](validation/transition-veil/link-panel-on-black-swapped-depth.png)

Findings:

- The headset showed solid black for the whole departure (about 17 s), including a 10.9 s single frame after scene load. Before this change, that frame showed a frozen frame of the world.
- Both layers reported `isOverlayVisible` throughout. The panel's tracking-space pose stayed constant across the Compound→Expedition rig swap.
- **Bug found:** the text panel was never visible, because the cubemap drew over it (see the depth note above). With the panel alone, it rendered upright, not mirrored, at the right distance, in CutePixel. With the depths swapped it showed on black.
- Fixed in code (depths swapped; title 128 px, detail 56 px). This fix has **not yet been compiled or re-run** through a full departure. Unity crashed while exiting Play Mode first.
- **Unity crash:** a native crash during Meta/OpenXR teardown when Play Mode exited, with the veil forced on (its layers alive). OVROverlay's `OnDestroy` destroys layers without checking for XR. The user reports that crashes on Play Mode exit are frequent when testing over Link, so the veil is not established as the cause. Two earlier VR exits with the veil off were clean.

- After the layer-order fix, the user ran their own headset test over Link from a 13-step checklist (departure, return, room change, no false triggers, exit) and reported it "looking good". Individual step results were not itemized.

**Not verified:**

- Itemized evidence for each checklist step.
- Standalone Quest. Link uses the PC compositor, so layer ordering and timing may differ.
- Return-to-compound and reconnect paths.
- Multiple clients.
- Quest timings. Editor stall durations are not representative of Quest.

## Load profiling and the crawl-boundary fix (November 10)

Desktop editor sandbox with the Unity Profiler (not deep profiling). Times are PC editor times, not Quest times.

| Departure frame | Before | After |
|---|---|---|
| Route build (`RailWorld.Update` → `RailRoute.Build`) | 10,304 ms | 1,160 ms |
| Expedition scene integrate (incl. forced `GC.Collect` ≈ 500 ms) | 634 ms | 638 ms |
| Departure → veil fully faded | ≈ 14.5 s | 5.4 s |

Timing each step of the route build in Play Mode isolated the stall:

- `CrawlGraph.RestrictToBounds` took 9,996 ms.
- Graph loads (2 ms), resource and enemy selection (11 ms), `Combine` (57 ms), and the visibility rebuild (11 ms) were small.
- Prefab instantiation stayed under the 206 ms profiler threshold; it measured 84 ms afterwards.

`ContainsCrawlSegment` sampled every edge at 0.1 m, checking 5 inset probes against every bounds box. That is millions of box tests across about 1.1 M edges.

**Fix:** an exact shortcut. If both ends of a segment, and of each inset probe, lie inside one box with a 1 mm margin from its faces, the segment is accepted without sampling. A box stays convex under the affine world-to-local transform, so the sampled test would pass. Other segments still use the original sampling. The shortcut also applies to runtime `ContainsCrawlSegment` callers in `ClimbLocomotion`.

**Equivalence check** (farm-silos, canyon, container-yard, skyscraper-works; 120,776 nodes; 1,112,970 edges). The new output was compared element by element with a reimplementation of the original sampling, with identical results in all three cases:

| Case | Edges kept | New | Original sampling |
|---|---|---|---|
| Real enemy bounds (no edges cut) | 1,112,970 | 425 ms | 4,884 ms |
| Shrunken boxes | 370,504 | 131 ms | 2,058 ms |
| Rotated frames, 0.5 m inset (the `EnemyManager` terrain path) | 523,762 | 818 ms | 4,251 ms |

In every case, node permissions, `EdgeStart` and `EdgeTarget` were identical. Enemy navigation behaviour was not separately play-tested.

### Bake-time filtering considered; exact runtime tightening instead

Each block's own baked graph was checked against its own enemy bounds. Every scavenging block and the midpoint helipad lie completely inside. The bridge finale has 188 of 63,975 nodes and 1,644 of 562,774 edges outside its box, and none of those are kept through a neighbour's box. The runtime filter therefore removes almost nothing.

Moving the filter into the bake was rejected:

- It would need a per-block "verified inside" flag that goes stale silently when a graph is rebaked or `enemyBounds` changes.
- It would not remove the bookkeeping cost (≈340 ms of the 981 ms on a full route).

Exact runtime changes instead:

- With no inset, the position test checks one point instead of five identical probes.
- Union and segment tests try the last matching box first. This is "any box" semantics, so no result changes.
- Cluster links are collected in a list, sorted and de-duplicated once, instead of a `SortedSet`.

On a full normal route (4 blocks + midpoint + finale; 191,213 nodes; 1.73 M edges), the filter now takes 285 ms. It took 981 ms with only the segment shortcut and 13,199 ms originally.

**Equivalence check:** the outputs were compared against a reimplementation of the original algorithm: node permissions, `EdgeStart`, `EdgeTarget`, `ClusterNode`, `ClusterEdgeStart` and `ClusterEdgeTarget`. They were identical in four cases:

- real bounds, including the finale removals;
- shrunken boxes;
- rotated frames with a 0.5 m inset;
- an empty bounds list.

### Departure timeline after both fixes

Desktop editor sandbox; 4.83 s from departure to the veil fully faded (was ≈ 14.5 s):

| Item | Time |
|---|---|
| Expedition scene swap: forced `GC.Collect` (547 ms) + GC marking (87 ms) + integrate | ≈ 0.72 s |
| Route build (`RailWorld.Update`; filter ≈ 285 ms, block instantiation ≈ 76 ms) | 0.55 s |
| Lighting companion loads, spread over frames | ≈ 0.15 s |
| Editor-only `EditorLoop` (absent from player builds) | ≈ 1.2 s |
| Veil fade-in, readiness hold and fade-out | ≈ 1.2 s |

The remaining player-relevant main-thread stall is ≈ 1.3 s on PC. Quest timings remain unmeasured.

After halving the veil timings, a desktop sandbox measured 4.08 s from departure to fully faded (was 4.83 s), and 0.47 s from world-ready to fully faded (was ≈ 0.86 s). Full cover was still reached one frame before the scene swap, and all long frames occurred at full cover. Comfort of the faster fades on a headset has not been reviewed.

## Next: shortening the rest of the load

Proposed, not implemented:

- The route build was 1.16 s before the tightening above; re-profile it. If it is still significant, spread it across frames (`EnsureReady` already gates readiness).
- Instantiate blocks across frames (`Object.InstantiateAsync`, Unity 6.3).
- Load Expedition additively, unload Compound separately, and defer `UnloadUnusedAssets` until under the veil.
- Preload Expedition with `allowSceneActivation = false` during boarding. Note that Unity holds other async scene loads until activation.
- Warm up Expedition shader variants.
- Run one `Tetrahedralize` after all lighting companions load.
