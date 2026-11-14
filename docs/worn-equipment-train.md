# Worn equipment on the train

## Fix — November 14, 2026

Remote item hats (`RailHelmet`) and night-vision goggles (`RailGoggles`) were
classified as loose cargo even while `HeadEquipment.IsEquipped` was true.
`HeadEquipment` attaches them to the interpolated avatar head at execution order
10000, then `RailCargo` at 10500 overwrote that pose with its independent,
network-delayed train-relative snapshot. Its carry and recovery paths also
considered worn items loose.

`RailCargo.Loose` now excludes equipped head gear. This single gate covers both
carry overloads, LateUpdate, frame capture/restore, prediction cancellation and
the below-floor cleanup check. HeadEquipment and PhotonGrabbable retain pose,
physics and transform-sync control while worn. Dropping restores the existing
loose-cargo behavior. No prefab, networking format or interpolation tuning changed.

## Verification

Unity 6000.3.24f1 compiled the change. `RailCargoEquipmentChecks.Run()` passed
28 assertions using both actual Rails prefab variants: owner and remote worn
state, stale frame restoration, both carry overloads, disabled world transform
sync, and dropped-item cargo resumption. `EquipmentPersistenceChecks.Run()` also
completed its assertions, but its offline synthetic-room fixture emitted Photon
RaiseEvent errors; that invocation is not a clean Console pass.

Two actual authenticated MPPM editor clients joined the same Photon expedition
on one workstation, using distinct saved accounts and no injected network faults.
The original-code peer capture measured a 13.201 mm head/item gap over 32.628 m
of train travel, with both worn items classified as loose cargo in all 706 samples.
This establishes the competing pose writer; it does not quantify the worst-case
lag reported during natural headset movement. Earlier incomplete captures and
stationary captures were excluded from moving-train evidence.

After the fix, both wearers exercised small circular walking and continuous head
turns through the real simulated rig input. Each observer measured both remote
items at URP camera-render time after all LateUpdates. All twelve eight-second
captures passed: stopped aboard, 4 m/s cruise, low-speed braking, off-train walking,
full 0–4 m/s acceleration and 4–0 m/s braking. Each had two remote items on every
sample, zero measured position/rotation error, no worn cargo frame and disabled
world transform sync. Cruise covered 32.5–32.7 m, acceleration 27.6–28.0 m, and
full braking 3.8–4.0 m. Both clients confirmed they were outside the train volume
before the ground captures.

[Raw measurements](validation/worn-equipment/train-attachment-2026-11-14.txt).

The live main-editor Console contained the pre-existing LCK unsupported graphics
API startup error, also present before the fix; no new equipment/cargo exception
was observed. Both clients' spawned test gear was destroyed and saved equipment
loadouts restored before stopping the scenario. Unity returned to Edit Mode.
Headset feel, network fault injection, late joining and host migration were not
tested in this scoped run; this does not close broader multiplayer milestones.

## Repeat

1. Run **Duck Rails > Tests > Check worn cargo exclusion** in disconnected Edit Mode.
2. Launch the existing two-client expedition with `RailTestRunner.Start("two-clients")`.
   Wait for both clients' world readiness. Keep the Game view rendering.
3. Invoke `RailWornEquipmentLiveProbe.Wear()` separately on each client through
   Unity MCP / `RailRemoteBridge`. This spawns and equips the two real networked
   items, preserving that account's saved equipment keys for cleanup.
4. After replication settles, invoke `Measure("unique-label", requireMoving)` on
   each client. It samples for eight real seconds and writes each client's local
   `Temp/worn-equipment/unique-label.txt`. A moving capture must cover >10 m;
   every capture requires >=60 frames, both remote items throughout, <1 mm and
   <0.1 degree attachment error, and no cargo/world-sync pose writer.
5. Exercise head turns/walking via `SimulatedRigInput`, and GO/STOP through
   `PhotonSession.SetTrainMoving`. Verify fuel and actual speed before capture.
   Warp outside the train for ground checks and confirm `RailTrain.Contains` is false.
6. Call `RailWornEquipmentLiveProbe.Cleanup()` on **both clients before stopping**;
   clear injected input and stop via `RailTestRunner.Stop`. Never count a missing
   output, startup failure or partial capture as a pass.
