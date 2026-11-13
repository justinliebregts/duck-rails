# Approved cockpit — November 12, 2026

Installed as art overrides in `Assets/DuckRails/Train/Prefabs/DuckRailsTrain.prefab`; the standalone legacy CockpitControls prefab is retained. The Blender shell and original fuel geometry are unchanged.

One solid brown grip joins three metal arms through three fore/aft slots. The axle is 18 cm lower (controls-local Y 0.115), with the neutral grip centre retained at Y 0.455. Arms extend to the axle under the dashboard. Existing RailControl drives the entire assembly through its unchanged -35/+35 degree arc; the longer radius increases hand travel. Grip collider covers the bar. Horn cap remains attached to the existing PressableButton, with a faceted amber cap and cream horn pictogram. The left numeric distance panel has a stacked label/number, retaining its original distance scaling; there is no distance progress bar. The right paper card shows coal -> fire = fuel.

## Sources and rebuild
- Editable Blender source: `ArtSource/Cockpit/Cockpit.blend`; deterministic authoring: `build.py` beside it. Built using installed Blender 5.3 in background because the live Blender MCP connection was unavailable.
- FBX component library, JSON geometry, palette, generated instruction texture, Unity materials and meshes: `Assets/DuckRails/Train/CockpitV2/`.
- FBX contains separate anchored components. Unity uses exact metre-scale JSON mesh exports (as the train importer does) to avoid FBX axis/scale surprises; scene bindings stay on the existing objects.
- Run Blender with `--background --python ArtSource/Cockpit/build.py`, refresh Unity, then `Duck Rails/Art/Install Approved Cockpit V2`. Installer updates existing asset identities.
- Crayon texture created using built-in imagegen: flat cream paper, large childlike coal lumps -> orange/red flame = yellow segmented fuel gauge; no lettering. The full generated PNG is retained; Unity imports at max 1024.

## Acceptance
Installation compiled and saved-reference/font checks passed before Unity exited. The final added validator was not recompiled; the 71-angle slot-envelope calculation was checked offline. Final Console refresh is unavailable. See `docs/validation/cockpit-v2/editmode.txt` for axle and sampled slot-envelope checks. An initial EditMode render was reviewed; final screenshot refresh was interrupted when Unity exited. No final preview is delivered. Preview loading reported pre-existing missing CutePixel bullet/em-dash glyphs on SUPPLIES/BRAKE NOW; those unrelated labels were not modified. A first installer attempt hit Unity's fake-null component behavior; corrected before prefab save and reinstalled successfully.

Queued for playtest: headset grip reach, longer 70-degree hand sweep, STOP/GO latch/release and breakaway, two-client replication and late join, horn press/release, readout/card readability and moving probe lighting. No live acceptance claimed.


## Throttle feel polish — November 12, 2026

User reports the installed cockpit works well in headset. This is user-reported acceptance of the previous art/pivot revision, not multiplayer validation or acceptance of the changes below.

- `RailControl.palmOffset` is live-tunable in the Throttle inspector. Default `(0, 0.025, -0.015)` metres: positive Y lifts the rendered palm and negative Z moves it toward the driver, in the rotating handle's local axes. Offset is applied every pose update, so tuning while holding works. Actual tracked-hand input, acquisition contact and breakaway comparison are not offset.
- Master maps lever progress 0–0.2 to zero speed, then linearly maps 0.2–1 to 0–current configured cruise speed. Existing full-forward detent hysteresis remains (snap at 85%, release below/equal 72%). Desktop toggle remains full-speed/STOP. Room-state shape, ownership, lease expiry and distance-based fuel integration are unchanged. Partial-speed driving now counts toward driver awards.
- Release sends STOP immediately; existing train deceleration remains. Unheld lever artwork smoothsteps to OFF in `returnDuration` (default 0.25 seconds), also on remote clients when they observe release. Regripping during return uses its current angular progress. Holding/pulling the lever remains direct; this is not extra input smoothing.
- During Play select the scene's `CockpitControls > Throttle`, then adjust **Rail Control > Throttle feel (live tunable) > Palm Offset**. Start with Y increments of 0.005 (5 mm), then negative Z if needed. Keep X at zero unless a sideways adjustment is needed. Note the chosen values and re-enter them after stopping Play (or copy the component during Play and paste component values onto the same scene control afterward); normal Play edits are temporary. Set them on the train prefab to retain them across scenes/builds.

Validation: Unity target verified `X:/Work/duck-rails/Assets`; stopped the existing Play session to compile, did not start a new one. Compilation, 25 drive EditMode checks and existing session checks pass; Console error/exception query empty. Tests cover dead-zone endpoints, intermediate speed, retained detent thresholds, quarter-second return endpoints/midpoint, and regrip angular continuity. New headset feel and two-client speed/release acceptance remain queued for playtest.

## Matching panel backing — November 12
Fuel now shares the distance panel's DistanceBase mesh, Controls material, scale, slope and backing height. Retained all ten original dark segment sockets separately; fuel TMP, yellow segment mesh/material/transforms and cockpit gameplay serialization compared unchanged during installation. The narrow Match Fuel Panel Backing menu avoids resetting throttle tuning. Compilation and serialized preservation checks pass; visual/headset review queued. Full cockpit installer retains this change. The older Blender train-shell importer may restore the legacy trim; rerun Match Fuel Panel Backing afterwards.


## Final user tuning and approval — November 12
User reports the cockpit looks great and requested commit/push. Artwork and tuning have now been applied into the nested CockpitControls prefab, with train-level overrides/references retained. Saved throttle local X rotation is approximately -25.66 degrees, local position approximately (0.05, 0.12, 0.09), palm offset (0, 0.0200, 0.0315) metres and return duration 0.25 seconds. The prefab is the source of truth for final placement; the full initial art installer contains original layout defaults and should not be rerun over user tuning. The backing-only command remains scoped to panel artwork.

Pre-commit check: verified Duck Rails editor in Edit Mode; saved fuel, fuel-segment and distance bindings resolve; 25 drive checks and session checks pass. User visual/headset approval is recorded separately from outstanding multiplayer acceptance of proportional speed and return. No new Play Mode run was launched.
