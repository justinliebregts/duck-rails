# Bridge hold — November 1, 2026

Confirmed: players must continuously hold the external button to lower the bridge. Releasing pauses without losing progress; another player can resume it. At full lowering the bridge locks down. The train remains safe and enemies remain inside their authored block. This replaces the one-touch minute-long timer.

November 17 tuning is **60 seconds** of total holding, with no speed multiplier
for extra hands or players. Two initial defenders approach the control area;
one extra defender joins once at 30 seconds of accumulated holding (30% rounded
up, bridge defenders only). See [current encounter](final-escape-layout.md#fortress-encounter-refresh--november-17-2026).
Historical 30-second evidence below predates this change. Current timing,
solo/unarmed feasibility and multiplayer encounter balance are queued for playtest.

The physical `HOLD / LOWER` button uses DuckPlayer fingertip contact in both Keyboard simulation and VR, through the same `PressableButton` and `RailHoldControl` paths as the horn and radio. Withdrawing the hand releases it. The sign shows remaining holding time and lowering/paused/ready status. The old one-shot bridge action is ignored.

The master validates run-scoped, expiring player holds against connected, standing participants, body distance, hand proximity and being off the train. The retained expedition snapshot contains accumulated work and a bounded active segment; late joiners and replacement masters reconstruct that state. Holds renew every 0.25 seconds and expire after one second, bounding progress after a lost release or disconnect. Normal release pauses when received by authority. No elapsed time after lease expiry is added during recovery. Finishing the run stops further movement. New runs start raised.

Protocol is `session-v9`: restart/update every client together. `Duck Rails > Authoring > Install Bridge Hold Button` installs the control in Expedition, the preserved Week 2 authoring baseline and the finale prefab without rebuilding geometry, graphs or lighting. Week 2/3 builders retain it. `Duck Rails > Legacy > Run Bridge Hold Checks` is included in Test Center quick regression.

## Validation

- November 17 current tuning: authenticated solo editor Play Mode passes actual
  defender approaches and production physical lever holding: both initial enemies
  reached controls at 11.415 s accumulated work, third spawned at 30.209 s observed,
  and bridge reached 100%/horizontal at 60.000 s. Operator invincibility enabled;
  no enemy position or bridge-progress injection. Multiplayer/headset acceptance
  remains open. [Trace](../output/bridge-finale-live/trace.txt).

- Unity compiled the change. All 17 bridge-specific editor checks passed for pause/resume, bounded lease expiry, retained-state reconstruction, overlapping holds, completion, replay reset and physical-button installation. Existing Week 2 (24), Week 3 (607), hold-button, fingertip, drive, rig, diagnostics, economy and other quick suites passed. The initial session check expected exactly three build scenes; it was corrected to retain the Login/Compound/Expedition order while allowing the existing lighting companions. Its 40 checks then passed. Evidence: `output/bridge-hold/editor-checks.txt` and `final-checks.txt`.
- An authenticated solo shared-rig fixture exercised the actual fingertip/button path. Downing paused at 8.479 seconds of work; returning aboard left that value unchanged through an idle wait. Resuming produced a pressed, moving state at 21.864 seconds and completed at 30 seconds, with a horizontal bridge and `BRIDGE READY / BOARD THE TRAIN` sign. The Console error/exception query was empty during the live check. Evidence: `output/bridge-hold/live-solo.txt`.
- The full expedition fixture passed on all three authenticated desktop clients at 19:59 Pacific on November 1 (03:59 UTC November 2). All peers agreed on physical release retaining exactly 0.628 seconds of work, resumed lowering through master handoff and third-client leave/re-entry, and completion. Two finishers/one non-finisher, automatic ride, group return, fresh replay, raised-bridge crash and cleanup also passed. The runner stopped all clients and restored the previous scenario. Evidence: `output/bridge-hold/three-client-main.txt`, `three-client-player2.txt`, `three-client-player3.txt` and `multiplayer-summary.txt`.

The updated Week 2/3 network fixtures position the operator's actual simulated fingertip at the button and use the production hold path. Their existing editor-only self-revive request keeps fixture combat from blocking state verification; this is not solo difficulty evidence.

Still required: natural solo and unarmed completion, crew-size pressure, target-headset button comfort/readability, operator disconnect/network-loss and concurrent-operator checks across real clients, and eight-player acceptance. Rule tests for lease expiry and overlapping holds do not replace those live tests.

Backend diagnostic inbox was unavailable due to DNS failure. Existing local reports were inspected and left open.
