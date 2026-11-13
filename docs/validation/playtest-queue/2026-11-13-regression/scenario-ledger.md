# November 13 older-queue regression ledger

Unity 6000.3.24f1. Current working tree beginning at `3c64b55a44e9c118c176838fa2e19136bd4b3eec`; final gameplay code includes `8522aae9f2065946a9e4e639b7ca0910e5871166` (fixture) and `8379de0f15405a0541434dc20037fd929d8161f3` (store fix), plus preserved unrelated edits. Three distinct real authenticated desktop MPPM clients, and disposable batch processes built from the current destination tree.

**58 PASS, 3 FAIL, 2 BLOCKED, 18 deliberately excluded scenario groups.** The 63 attempted-or-blocked groups below and 18 [precise exclusions](run-notes.md#deliberate-exclusions) are the counting units; individual assertions, retries, peers and human-only checks are not additional tests. A PASS applies only to the named scope. Failed initial attempts resolved by a passing rerun are counted once at their final verdict. Earlier Claude/agent PASS labels were not used to skip tests.

## Fresh scenario verdicts

1. **PASS — Death lifecycle current-tree batch.** [DeathLifecycleChecks.log](DeathLifecycleChecks.log).
2. **PASS — Smasher retreat/jump current-tree batch.** [SmasherRetreatJumpChecks.log](SmasherRetreatJumpChecks.log).
3. **PASS — Animated hit-order/cooldown/held/thrown batch.** [GrabbablePlayChecks.log](GrabbablePlayChecks.log).
4. **PASS — Hook pull: 24 movement and six surface cases.** [ZombieHookPullChecks.log](ZombieHookPullChecks.log).
5. **PASS — Two real Photon batch clients: flare replay, expiry and falling-master handoff.** [flare-host.log](flare-host.log), [flare-guest.log](flare-guest.log).
6. **PASS — Compound pause menu/pages/tablet/close/death path.** [menu-True-Compound.txt](menu-True-Compound.txt).
7. **PASS — Expedition pause menu/pages/tablet/close/death path.** [menu-True-Running.txt](menu-True-Running.txt).
8. **PASS — Private keypad, owner-only launch, individual Back and staging owner handoff.** [rooms-main.txt](rooms-main.txt), [rooms-Player2.txt](rooms-Player2.txt), [rooms-Player3.txt](rooms-Player3.txt).
9. **PASS — Editor test-panel toggle, no GoldBarDropper.** [menu-True-Compound.txt](menu-True-Compound.txt).
10. **PASS — Discord and Rate button action/debounce/status/room retention.** [menu-True-Compound.txt](menu-True-Compound.txt).
11. **PASS — First and repeat same-process expedition warmup under veil.** [rooms-main.txt](rooms-main.txt).
12. **PASS — Cold initial Login directly into a running expedition.** [cold-login-fresh-Player3.txt](cold-login-fresh-Player3.txt).
13. **PASS — Warm Compound late join under covered warmup; no time limit/timeout.** [revive-late-Player3.txt](revive-late-Player3.txt).
14. **PASS — Special-block loot identity/pose through rejoin and master handoff.** [snapshot-comparison.txt](snapshot-comparison.txt), [observer-main.txt](observer-main.txt), [observer-Player2.txt](observer-Player2.txt), [observer-Player3.txt](observer-Player3.txt).
15. **PASS — Actual master, non-master and late-player gold payouts, depositor-only exactly once.** [gold-receipts.txt](gold-receipts.txt), [gold-main-075358.txt](gold-main-075358.txt), [gold-Player2-075455.txt](gold-Player2-075455.txt), [gold-Player3-074034.txt](gold-Player3-074034.txt).
16. **PASS — Gold receipt/popup no replay after rejoin.** [rejoin-Player2.txt](rejoin-Player2.txt), [observer-Player2.txt](observer-Player2.txt), [observer-Player3.txt](observer-Player3.txt).
17. **PASS — Lateral boundaries, below-floor/repeat and moving-train last-ground recovery.** [safety-water-main.txt](safety-water-main.txt).
18. **PASS — Coal/gold below-floor destruction, including after store fix.** [cargo-floor-after-fix.txt](cargo-floor-after-fix.txt), [below-floor-main.txt](below-floor-main.txt).
19. **PASS — Lake underwater effects/exact restoration and dry-head finale water surface.** [safety-water-main.txt](safety-water-main.txt).
20. **PASS — First-hold defenders, pause/repress, simultaneous holders, handoff/late-join retained pair.** [bridge-firsthold.txt](bridge-firsthold.txt), [finish-corrected-main.txt](finish-corrected-main.txt).
21. **PASS — Fresh run creates new defender spawn tokens.** [full-route-main.txt](full-route-main.txt).
22. **PASS — Actual public departures, failed finish and normal three-client return.** [public-return-main.txt](public-return-main.txt), [public-return-Player2.txt](public-return-Player2.txt), [public-return-Player3.txt](public-return-Player3.txt).
23. **PASS — Return-control connection cut still reunites all three clients.** [public-return-main.txt](public-return-main.txt), [public-return-Player2.txt](public-return-Player2.txt), [public-return-Player3.txt](public-return-Player3.txt).
24. **PASS — Current award assignment checks.** [awards-current.txt](awards-current.txt).
25. **PASS — Real-action counters/results agree after pre-finish rejoin and master handoff.** [snapshot-comparison.txt](snapshot-comparison.txt), [observer-main.txt](observer-main.txt).
26. **PASS — Actual flashlight slot insertion through BagInventory grip-release path.** [flashlight-natural-bag.txt](flashlight-natural-bag.txt).
27. **PASS — Compound actual coin purchases/one charge/object lifetime after fix.** [store-cleanup-fix.md](store-cleanup-fix.md), [store-fixed-Player2-Compound.txt](store-fixed-Player2-Compound.txt).
28. **PASS — Purchased objects survive peer/late-join/master handoff, buyer leave cleans them.** [store-lifetime-Player3.txt](store-lifetime-Player3.txt), [store-buyer-leave.txt](store-buyer-leave.txt).
29. **PASS — Private same-room staging purchases and departure cleanup.** [store-fixed-Player3-Staging.txt](store-fixed-Player3-Staging.txt), [store-private-departure.txt](store-private-departure.txt).
30. **PASS — Both expedition stores actual flashlight/bag purchase and peer delivery after fix.** [store-fixed-Player2-Running.txt](store-fixed-Player2-Running.txt), [store-fixed-expedition-main.txt](store-fixed-expedition-main.txt).
31. **PASS — Separate right and left ordinary throttle grip/stroke/detent/release/side breakaway.** [throttle-hands.txt](throttle-hands.txt), [throttle-left-isolated.txt](throttle-left-isolated.txt).
32. **PASS — Non-master shared input, rival denial/fresh press, toggle STOP.** [drive-safety-main.txt](drive-safety-main.txt), [drive-safety-Player2.txt](drive-safety-Player2.txt), [drive-safety-Player3.txt](drive-safety-Player3.txt).
33. **PASS — Focus/menu/pause/down release and pre-acknowledgement cancellation.** [drive-safety-main.txt](drive-safety-main.txt), [drive-down-acknowledged.txt](drive-down-acknowledged.txt).
34. **PASS — Actual driver and master disconnect from 4 m/s, successor braking and OFF rejoin.** [drive-disconnect-fuelled-main.txt](drive-disconnect-fuelled-main.txt), [drive-disconnect-fuelled-Player2.txt](drive-disconnect-fuelled-Player2.txt), [drive-disconnect-fuelled-Player3.txt](drive-disconnect-fuelled-Player3.txt).
35. **PASS — Ordinary pickups after throttle; six-block carry/drop and peer agreement.** [area-carry-after-throttle.txt](area-carry-after-throttle.txt), [area-drop-comparison.txt](area-drop-comparison.txt).
36. **PASS — Independent peer reverb regions, unload and recreation.** [reverb-independent-Player2.txt](reverb-independent-Player2.txt), [reverb-independent-Player3.txt](reverb-independent-Player3.txt).
37. **PASS — 29 nearby material hand/foot AudioHandle playback probes across seven blocks.** [contact-probes-near.txt](contact-probes-near.txt).
38. **PASS — Seven cosmetic rollers with stationary collision and static Farm swing.** [cosmetic-motion.txt](cosmetic-motion.txt).
39. **PASS — Farm/Convenience/Finale candidates and 210 selected live loot support/settling samples.** [loot-candidate-current.txt](loot-candidate-current.txt), [loot-live-settled.txt](loot-live-settled.txt).
40. **PASS — Twelve completed original flat collision routes.** [traversal.txt](traversal.txt).
41. **PASS — Five basement cells, entry and exit with aligned pose.** [traversal-basement-aligned.txt](traversal-basement-aligned.txt).
42. **PASS — Both foundation recovery ramps.** [traversal-additional-jump.txt](traversal-additional-jump.txt).
43. **PASS — Both finale middle ramps, river ramps and east south-inward ramp.** [traversal-additional-jump.txt](traversal-additional-jump.txt), [traversal-final-desktop.txt](traversal-final-desktop.txt).
44. **PASS — Correct-height silo segments and playground bridge/platform traversal.** [traversal-additional-jump.txt](traversal-additional-jump.txt), [traversal-final-desktop.txt](traversal-final-desktop.txt).
45. **PASS — Selected natural pursuit in Container, Motel, Farm, Skyscraper and Lakeside.** [enemy-approach.txt](enemy-approach.txt).
46. **PASS — Natural full-route odometer travel and bridge crossing.** [full-route-audit.txt](full-route-audit.txt), [full-route-main.txt](full-route-main.txt).
47. **PASS — Actual finish classification: padded side/nose and far ground; side/far follow-up.** [finish-corrected-main.txt](finish-corrected-main.txt), [finish-corrected-Player3.txt](finish-corrected-Player3.txt).
48. **PASS — Nose boundary traced six seconds with no rescue teleport.** [finish-nose-trace.txt](finish-nose-trace.txt).
49. **PASS — Moving-train revive, single completion, long withdrawal reset.** [revive-remaining-main.txt](revive-remaining-main.txt), [revive-remaining-Player2.txt](revive-remaining-Player2.txt), [revive-remaining-Player3.txt](revive-remaining-Player3.txt).
50. **PASS — Two-helper revive through master handoff remains at least three seconds.** [revive-remaining-main.txt](revive-remaining-main.txt), [revive-remaining-Player2.txt](revive-remaining-Player2.txt), [revive-remaining-Player3.txt](revive-remaining-Player3.txt).
51. **PASS — Victory-ride revive leaves locked result counters unchanged.** [revive-remaining-main.txt](revive-remaining-main.txt), [revive-remaining-Player2.txt](revive-remaining-Player2.txt), [revive-remaining-Player3.txt](revive-remaining-Player3.txt).
52. **PASS — Game-effects mixer binding and actual 100/50/0 volume changes/restoration.** [game-volume.txt](game-volume.txt), [revive-remaining-main.txt](revive-remaining-main.txt).
53. **PASS — Natural ground enemy death, real late join and original 40-second flare expiry.** [natural-ground-flare.txt](natural-ground-flare.txt), [rejoin-Player2.txt](rejoin-Player2.txt).
54. **PASS — Moving-train Physics enemy death, real late join and original flare expiry.** [moving-flare-main.txt](moving-flare-main.txt), [moving-flare-late.txt](moving-flare-late.txt).
55. **FAIL — Composite shared-rig fixture still stops at its legacy throttle gate after three fixture fixes.** [rig-fixture-fix.md](rig-fixture-fix.md), [rig-fixture-fixed-attempt3.txt](rig-fixture-fixed-attempt3.txt).
56. **FAIL — Physical throttle acceptance: combined hand-switch, remote regrip/palm probe and right wide-bar end.** [throttle-hands.txt](throttle-hands.txt), [throttle-peer-physical-attempt1.txt](throttle-peer-physical-attempt1.txt), [throttle-peer-physical-attempt2.txt](throttle-peer-physical-attempt2.txt), [throttle-peer-physical-attempt3.txt](throttle-peer-physical-attempt3.txt), [throttle-peer-physical.txt](throttle-peer-physical.txt), [throttle-remote-palm.txt](throttle-remote-palm.txt), [throttle-wide-left-end.txt](throttle-wide-left-end.txt), [throttle-wide-right-end.txt](throttle-wide-right-end.txt).
57. **FAIL — Remaining desktop traversal/pursuit acceptance incomplete after bounded probe corrections.** [traversal-additional-jump.txt](traversal-additional-jump.txt), [traversal-final-desktop.txt](traversal-final-desktop.txt), [traversal-threshold-rerun.txt](traversal-threshold-rerun.txt), [enemy-approach.txt](enemy-approach.txt).
58. **BLOCKED — Owned bundle equip/cooldown/replication/transition tests: none of the three real accounts owns the products.** [store-main-Compound.txt](store-main-Compound.txt), [store-Compound-Player2.txt](store-Compound-Player2.txt), [store-Compound-Player3.txt](store-Compound-Player3.txt).
59. **BLOCKED — Four-client simultaneous independent revives: Player4 has no configured authenticated account.** [run-notes.md](run-notes.md).
60. **PASS — Public gate hidden and CutePixel prompt retained.** [revive-late-Player3.txt](revive-late-Player3.txt).
61. **PASS — Real late join during authoritative revive progress starts at current pitch.** [revive-late-main.txt](revive-late-main.txt), [revive-late-Player2.txt](revive-late-Player2.txt), [revive-late-Player3.txt](revive-late-Player3.txt).
62. **PASS — Revive Compound cleanup and post-completion late join without replay.** [revive-late-Player3.txt](revive-late-Player3.txt).
63. **PASS — Natural real-network bare-hand Hook pull retains displacement after two seconds.** [hook-live-ground.txt](hook-live-ground.txt).

## Failure interpretation and remaining attempts

- The broad rig harness received three fixture corrections, committed separately from production. Its movement/grab/finger stages pass, but the legacy lever gate remains a failed suite. No fourth fix was attempted.
- Focused right and left throttle trials pass separately. The combined hand switch still fails. The non-master probe needed explicit editor focus and a slower stroke; its release assertion also needed to distinguish immediate local release from a delayed network receipt. After those three corrections, it still failed reacquisition. The remote pose probe recorded a large maximum gap and does not establish attached-hand acceptance. The left wide-bar end acquired; the right-end probe did not. These are failed acceptance probes, not proof of a diagnosed runtime root cause. No locomotion/networking redesign was made from these fixtures. The upward-breakaway step after failed reacquisition was **not reached**, not counted as BLOCKED or PASS.
- Desktop traversal used actual simulated room-scale input through collision, warping only to each route's start. Full remaining-route acceptance failed: both upper finale ramps, west south-inward ramp, Motel stairs, barn/repair-container thresholds, ditch/pond and straight car-wash probe still lack valid completed routes. Some straight lines meet intended scenery; this is not proof those authored routes are impossible. The old Farm utility line crossed newly authored cages; ten aligned cell entry/exit replacements pass. Raw silo-downhill-2 PASS is rejected because it ended at the wrong height. Convenience's selected spider entered Chase but did not close the required distance within the probe. Broader intended interior/roof/climb routes and every-shelf reach are **not established by these samples**. Geometry and VR feel were left unchanged; bounded fixture corrections were exhausted.
- Earlier unacknowledged down/fuel CAS attempts are rejected setup attempts. Final drive-down and fuelled-disconnect files contain the accepted evidence. The first two disconnect passes proved lease clearing but not braking from speed; the final pass observes 4 m/s before each cut.
- The full-route raw FAIL is a TMP parser error, independently corrected by [the audit](full-route-audit.txt). It never seeded distance; test fuel was supplied and the train paused while peers completed traversal. The corrected nose trial disabled local locomotion to isolate the padded finish/rescue volume; natural nose standing remains human acceptance.
- Dropped items from the first area-carry pass had reached their normal six-minute pickup/drop lifetime by the delayed snapshot. The fresh after-throttle pass took immediate snapshots and matches on all peers. No persistence bug is inferred from the expired items.
- Contact-audio evidence is nearby dispatched playback, not natural hand/foot cadence, duplicate-step listening or acoustic quality. Reverb evidence proves independent listener regions/lifecycle, not perceived acoustics. The existing microphone/output-device error prevents meaningful voice/listening acceptance in this environment.

## Genuine blockers

- All three authenticated test accounts lack the new bundle entitlements. Owned avatar/default-switch replication, bundle weapon/bag cooldowns and ownership persistence cannot be accepted from these accounts. No entitlements or balances were fabricated. Real Meta checkout/restoration remains human work.
- Player4 has no configured authenticated account. Four-client simultaneous independent rescues were not run; the three-client two-helper test is not a substitute.

## Needs human; not counted as failed or blocked automated tests

Worn-headset hand tracking, reach, locomotion/comfort, visual fit of animated enemy hits, natural throwing, full block circulation/climbing, light/material/night readability, UI/camera framing and local/remote face/held-object appearance, spatial audio/voice quality, revive particle scale, Quest shaders/frame time and real cross-machine jitter remain in Pending. Physical keyboard/controller P/T/L3/Start/Esc delivery and non-admin development-build gating were not verified. Desktop UI control was stopped after the user's Escape; external browser/store windows were not visually accepted. Real-game Hook held-gear/release/train/curve acceptance remains pending; the current batch covers 24 pull and six surface cases and a real-network bare-hand ground pull was added. These checks were not silently converted to PASS because related automated cases passed.

## Run hygiene

No diagnostic report inbox, report script or diagnostic JSON was read. Nothing was pushed, branched, stashed or reverted. Unrelated scene/prefab/code/doc changes remain. Editor resets separated entry groups; related network/traversal subscenarios shared an expedition to test retained state. This is not a claim of a fresh Edit Mode reset for every numbered assertion group. The first OpenXR teardown crash and user restart are recorded in the run notes. Final restoration is recorded in [cleanup](cleanup.txt).
