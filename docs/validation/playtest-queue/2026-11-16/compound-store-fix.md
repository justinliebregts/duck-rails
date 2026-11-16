# Active Compound store — November 16

The current terminal replaced SubwayV2, but shared-store installation still
targeted the retired supplies module under inactive `SubwayCompound`. Both
authenticated clients found zero active stores. This was a real integration
failure; the user's existing Compound scene diff only changes hint text/colors.

The existing shared-store prefab is now nested in the current terminal prefab
at local (6.9, 0, -1.6), identity rotation and full scale. No terminal geometry,
scene file, retired module, catalogue, price or entitlement was changed.
The narrow authoring action updates only this prefab. Future full installations
use the current terminal; active-scene validation now requires one active store,
17 distinct bound displays and its own enabled security camera/TV.

Unity 6000.3.24f1 isolated placement: 19 conservative non-trigger collider
envelopes tested against the actual terminal colliders, zero overlaps greater
than 5 mm (envelopes reduced 2% to avoid exact surface-contact noise). All 17
displays remain present. Current Compound scene and three-location prefab checks
pass. Evidence: `compound-store-clearance.txt`, `compound-store-active-check.txt`.
The validator uses original-source identity for the nested prefab in the scene.

Live two-client commerce now passes: seven actual wall-pull purchases each charge once and spawn held network cargo. Wallet moves from **1,191 to191**, exactly **1,000 coins** in total (not1,025). Default equip is free. Main and Player2 independently match all seven view IDs/tokens with zero failures. Quackbear, Huggy and Scrap Duck ownership cases remain blocked by missing entitlements. Evidence: [purchases](compound-store-goods-Compound.txt), [main tokens](compound-store-peer-main.txt), [Player2 tokens](compound-store-peer-Player2.txt). Compound departure/late-rejoin retention is not established by those same-room snapshots.

The grounded camera A/B uses two floor-derived positions,(6.0,0,3.28) and(6.6,0,3.3), with measured head height1.55 m in all four images. Current framing contains a large bright bulb at top center; lowering only the invisible camera mount0.20 m in terminal/world up removes it while retaining the grounded duck. The fixture restores the mount, exact rig pose and input flags. The earlier camera-forward capture elevated the stand point and is not normal-grounded framing evidence. See [trace](compound-store-grounded-camera-ab.txt), [current0](compound-store-grounded-current-p0.png), [down0](compound-store-grounded-down020-p0.png), [current1](compound-store-grounded-current-p1.png), [down1](compound-store-grounded-down020-p1.png).

**Persistence pending:** main is applying this as a Compound-only nested invisible camera-mount override plus installer adjustment in Edit Mode. Store placement, all19 collider poses, shared base prefab/other two locations and terminal geometry remain outside that camera adjustment. Final persisted fresh-load validation and a new feed capture have not yet passed at this update; do not claim the temporary A/B proves saved behavior.

Headset reach, readability, broad remote-face/held-object framing and appearance remain Needs human. Existing baked lighting was not rebaked. Successful rendering and these two grounded views do not establish all store viewing positions or Quest cost.
Final saved-pose validation: the Compound-only nested camera mount override is
saved (compound-store-camera-save.txt). A fresh authenticated scene load rendered
two grounded views with the mount at world Y=2.08, head height1.55m, unchanged
rotation and FOV; both PNGs were visually inspected and the ceiling bulb is clear.
See compound-store-fresh-camera-ab.txt and compound-store-fresh-current-p0/p1.png.
No scene, shared store prefab, terminal collision, or other shop was modified.
