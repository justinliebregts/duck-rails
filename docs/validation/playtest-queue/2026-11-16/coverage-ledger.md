# Final queue coverage ledger — November 16, 2026

Final documentation snapshot of all **65 Pending entries**, reconciled through desktop evidence14:00:34 and final artifact/cleanup14:14:55 UTC November16,2026, commit **b9bbf51d4**. Final [rollup](scenario-rollup.md):32 accepted scoped groups,1 inconclusive station group,4 blocked families. Throttle default32/24 and edge19/15 post-fix reruns pass0failures within the existing group. Seven production corrections plus three fixture/editor-authoring corrections total10focused commits. Final APK builds/installs successfully; no new native survey was run. Native positional/head tracking remains blocked with no readiness reply. No65whole-entry or headset acceptance is implied.

Environment: Unity 6000.3.24f1; base `789a5be13f11d220f9b09a520a16fc204d169c36`, existing uncommitted art, then spider correction `217f5a65d` as identified in [bridge summary](bridge-summary.md). See [initial state](initial-status.txt), [tested base](tested-head.txt), and [run notes](run-notes.md). Offline fixtures have one process; authenticated evidence identifies main/Player2. Three accounts are configured, Player4 is absent. No diagnostic report inbox or diagnostic JSON was read for this audit.

“Observed” means the stated scope was actually recorded today. Earlier queue results remain historical evidence, **not freshly verified**. Script/patch files are reproducibility material, never proof of execution. Runtime rays/capsules, inventory equality, and synthetic Rigidbody checks do not prove natural VR traversal, every loot candidate, visual equivalence, perceived audio, or device performance. All original Needs human requirements remain open even where abbreviated below.

## Shared evidence and interpretation

- **L — lighting:** [corrected lifecycle](lighting-lifecycle.txt) loads ten named production/review prefabs in offline Play, translates, checks bindings/probes, suppresses baked fixtures, disables/reloads and cleans up. Finale has 1,746 valid bindings and 24 suppressed fixtures; helipad retains three realtime lights. Production Motel additionally restores sources when consolidation is disabled and chunks on reenable. [First attempt](lighting-lifecycle-attempt1.txt) is rejected for clean-run acceptance because unrelated shop behaviours lacked wallet services.
- **L2 — genuine Android build failure and player-compatible lighting correction:** [first build](quest-build-attempt1.txt) failed **CS1061 at RailBakedBlockLighting.cs:45** because `Light.lightmapBakeType` is editor-only. As detailed in [build fix](baked-fixture-build-fix.md), prefab `bakingOutput` also does not retain authored bake type; merely switching getters would silently fail suppression. The correction serializes `bakedFixtures`, captures membership in exporters/the legacy finale installer, and guards builds against missing/stale/null/duplicate membership. The tested working tree has **28 owners / 1,009 fixture references**. [Preservation proof](baked-metadata-preservation.txt) retains every prior prefab byte after reversing the new field and31 required nested stripped-Light records on StartingStation; unrelated art is preserved. [Fresh ten-prefab actual offline Play rerun](lighting-lifecycle-player-metadata.txt) passes with **0 failures**, including load/translation/reload/cleanup, exact original enabled-state restoration with an initially disabled fixture, and all three helipad realtime lights retained. Console afterward contains only the known LCK unsupported-DX12 startup error. Correction 33a080740 produced a successful [second Android build](quest-build-attempt2.txt),0 errors; [hash](quest-apk-sha256.txt) identifies that artifact. It predates later revive/menu/furnace/store changes. First device attempt was interrupted by Meta Link/desktop XR; [cancel/collection cleanup](quest-attempt1-cleanup.txt) verified stopped app, absent request, proximity false and recording false, but all performance data is excluded. The earlier ac918293b plus preserved working-art Android build succeeded with0errors in6m54.1798979s, APK2,552,541,888bytes, installed on Quest3. [Current build](quest-final-build.txt), [hash](quest-final-apk-sha256.txt), [working-tree provenance](quest-final-build-working-tree.txt). [Independent native attempt](quest-final-attempt.md) reaches authenticated Compound Ready with app/VR/input focus, XR running and userPresent true, but position/head tracking false and orientation true:0valid of7,024frames over101s. Explicit cancel/collection and [independent cleanup](quest-final-attempt-cleanup.txt) verify stopped app, absent request, proximity false and recording false. **B04: device tracking prerequisite blocked**, user readiness request pending; not a failed frame budget or backend/network error and no device PASS. [Status](quest-final-attempt-status.json), [metadata](quest-final-attempt-metadata.json), [summary](quest-final-attempt-summary.json). The build-fix note distinguishes isolated legacy metadata (978 references) from the tested working art (1,009); do not attribute the extra31 CityV3 fixtures to a clean legacy baseline or claim unrelated art was committed.
- **V — visibility:** [route visibility](route-visibility.txt) verifies sampled loaded neighboring blocks hide/show with colliders retained and companions ready. Its named list does **not** include Container Yard; do not silently extend that result to Container. [cosmetic motion](cosmetic-motion.txt) checks seven rotating brush visuals with fixed hull matrices and Farm swing without Rigidbody/joints.
- **I — inventories/rejoin:** [comparison](peer-inventory-comparison.txt) records four paired inventories with zero differences; the `peer-inventory-<run>-main/Player2.txt` files retain further snapshots. [Moving rejoin](moving-rejoin-main.txt) leaves to Compound, explicitly joins the same running route as a new actor at 4 m/s, and records ready reconstruction. [World text](world-after-moving-rejoin-main.txt), [main snapshot](snapshot-main.txt), [peer snapshot](snapshot-Player2.txt), and public/private solo inventories contain actual block/item data. The small JSON snapshots contain session/camera/ownership fields, **not** full block/item inventories. Snapshots taken at different times are not an exact pose-equality assertion.
- **G — geometry:** [block probes](block-live-probes.txt) finish **823 PASS, 0 FAIL, 0 BLOCKED**: repaired Farm mesh support, slide solids, pond/ditch/bridge support and wet-volume membership; Container north-yard capsule paths/tanks; Arcade window sweeps and temporary sphere passage; Sky retained/removed floors and tread support; Motel doorway/circulation samples; helipad fence/rock/seam checks; finale ramp support. Temporary radius-0.08 m unnetworked spheres settle/pass only against scenery; gameplay bodies and triggers are excluded. This is not player climbing, carried gameplay-item passage, combat pursuit, or visual acceptance.
- **C — real selected cargo:** [settled loot](loot-live-settled.txt) records 121 currently selected loose objects supported/settled across seven blocks. `FLAGGED starting-station-v1 count=0` reflects no selected station loose loot, not a failed support sample. [Carry/drop](area-carry-after-throttle.txt) claims one actual network item per five scavenging blocks, moves about 0.9–1.02 m using simulated locomotion, then releases. Container was still awake at the first short timeout; later [main](area-drop-peer-main.txt) and [Player2](area-drop-peer-Player2.txt) have the same five tokens/poses, all asleep. Thus the early Container `INCOMPLETE` is resolved for that sample, not evidence of defective collision. No claim covers all shelves/roofs/interiors or natural reach.
- **B — bridge:** [summary](bridge-summary.md), `bridge-raised/half/almost/success-*.txt`, orchestration and fault files cover two-client actual impact/crossing from a controlled approach, deterministic five-section collapse/floor, three fires, one impact lease, 3.2 s results delay, restoration and natural private return. Disconnects during/after collapse and master loss preserve state/effect IDs. Reconnection completes **after** falling ends; arrival while sections are actively falling remains untested. First reconnect observer's duplicate-impact failure was a second observer counting an existing burst; corrected observers preserve identity and pass. [Passenger recovery](bridge-passenger-recovery.txt) returns a far passenger in 2.079 s without changing locked finish state.
- **D — driving/menu/fog/wheels:** [physical throttle](throttle-physical-solo.txt) passes both ordinary grip paths with synthetic palm poses, GO/pull-back/re-push/release/regrip/sideways breakaway. [Receipt-aware solo controls](solo-controls.txt) checks Fuel100/0/refuel, 20 m/s, wheel phase/axles, moving menu/resummon, rumble source, saved fog and 120→175 m transition, and private return. Its “normal 4 m/s” line actually reports max20 because fast mode was inherited: **do not accept that label**. [Earlier controls](solo-controls-unacknowledged.txt) separately supplies observed normal max4, but six command-dependent failures were fixture receipt/CAS handling, corrected by retries. `peer-state-*.txt` supplies both-client wheel error zero and sampled fog/view states, not matched-frame camera appearance.
- **S — stores:** [funded goods matrix](store-goods-Running.txt) performs seven actual wall-pull purchases at **Departure Shop and helipad**, exactly one charge each, real network objects and free Default equip. [Low-wallet attempt](store-goods-low-balance.txt) has two valid earlier flashlight purchases and skipped unaffordable cases; it is not a purchasing defect. [Twenty actual gold sales](gold-main-102710.txt) and [receipts](gold-receipts.txt) fund the retry (last sale caps wallet at 2,000). [Peer](store-peer-Player2.txt) sees all recorded items; rejoin retains all **14 recent matrix purchases**. The two older flashlights in `store-peer-main.txt`/`moving-rejoin-main.txt` legitimately exceeded `PhotonGrabbable`'s 360-second dropped lifetime; their lifetime-unfiltered raw FAILs are fixture expectation errors, not replication failures. Both latest account snapshots lack all three bundle entitlements. [Camera events](store-camera-events.txt) and [capture checks](store-feed-captures.txt) show two independent feeds and proximity shutdown; [capture 0](store-feed-0.png) and [capture 1](store-feed-1.png) visibly contain rendered shop scenes. They do not prove every local/remote face and held-object framing case.
- **F — furnace:** [main coal](coal-main.txt) and [Player2 coal](coal-Player2.txt) pass four actual physical deposits stopped, moving, after master handoff, and empty-fuel recovery, with exactly-once fuel/removal and resumed driving. `furnace-events-main/Player2.txt` each record **4 fresh receipts, 4 bursts, 4 CutePixel +7.8 popups**, expected value7.8125. Isolate the 10:37–10:38 coal window in `audio-events-*`: each peer has four thud/flare/chime leases. Whole-run counts include earlier activity and observer lifetime differences, so do not interpret them as duplicate coal effects. Late join after these receipts is not yet established here.
- **A — audio:** `audio-events-main/Player2.txt` observe voice leases, music/rumble sources, rail joints, stop/out-of-fuel, throttle and crash events. Across five completed return run IDs observed by both, ClockTick occurs exactly at **5,4,3,2,1** on each. Music plays on the actual successful bridge run. No horn event is recorded in this snapshot. Dispatch/source inspection is not listening, and mere event presence does not prove cadence, fade, all threshold crossings, or volume controls.
- **E — enemy collision:** [spider correction](spider-fix.md) records a genuine adhesion/knock-off defect and scoped fix. Original rear failure, unsuccessful level-only retry and trace remain. Corrected rear/side/stalking loops pass; 32 wall-carry poses pass; focused empty-scene physics61/61 passes and minion rear regression passes. Terrain-contaminated focused attempt60/61 is an invalid no-support assumption, not a second unresolved product defect. Wall-carry teardown logged a later companion error: movement assertions remain valid, clean lighting acceptance comes from L. Minion depot/Motel/side logs cover actual isolated boarding/chase/dismount. Completed real-client transport follow-up is recorded in N below.
- **N — completed network follow-ups:** [spider main](spider-network-main.txt) and [Player2](spider-network-Player2.txt) now pass actual two-client carry, moving host migration, **actual non-owner melee detachment**, leave/rejoin, left exit/reboard and right exit. Fixture commit `730f48edc` exercises production correction `217f5a65d`; it is not another production fix. [Ground rescue main](ground-rescue-main.txt) and [Player2](ground-rescue-Player2.txt) pass actual RequestDown/helper-proximity authoritative rescue. [Archived main ground FX observer](ground-rescue-fx-main.txt) records that completion once with **three** layers; this does not establish every remote/moving/repeated rescue case. [Results main](results-current-main.txt) and [Player2](results-current-Player2.txt) pass report-only current actual-action stats/board/freeze/normal-return checks; finish was **controlled Test success preview**, deadline32 s, followed by natural return. It is not a second actual bridge-crossing proof or a complete award-attribution matrix.
- **R — radio/default lips:** [actor1](radio-lips-actor1.txt) has radio19 checks/0 failures: installed PressableButton press/hold/release,18 mm cap travel, renewed run lease/transmitter and release cleanup. [Actor3](radio-lips-actor3.txt) sees actor1 lease/transmitter true while held and false after release. Default lip source/capture plumbing passes6 checks through direct `PoseCaptureFace`; that is **explicit callback injection**, not live speech/LIV recording. Native LIV remains blocked by unsupported DX12 editor backend; all bundle entitlements remain missing. Mic meter samples are not audible delivery/quality evidence.

- **N2 — corrected three-client revive:** genuine remote effect-follow order defect fixed in cd39a40e0. [Fix/scope](revive-follow-fix.md) and the revive-follow-fixed, revive-moving-follow-fixed, revive-late-follow-fixed and revive-fx-follow-fixed role logs cover ground handover/overlap, moving 4 m/s, withdrawal/reset, master handoff,32 s controlled victory ride/frozen scores, late progress/unload/no historical replay. Post-LateUpdate observations: main 522, Player2 533, Player3 422, all 0 failures; cumulative completions10/10/8, not counts for one subcase. Earlier 11:22 run retains the0.1 s grace sample not repeated in the latest ground run. Latest late warmup 36 frames/0 uncovered is warm-cache evidence.
- **P2 — two-client Compound occupancy:** [main](compound-multi-main.txt)/[Player2](compound-multi-Player2.txt) finish ready in same expedition with 0 failures,2 On/2 Off,3 cm travel and fixed collider. Second-occupant/cancel/restart sequence uses synthetic positions and actual shared occupancy. Not three-client stranded-return or crowd acceptance.
- **C2 — actual local body/injected hand audio:** [contact log](contact-audio-actor1.txt) completes fountain 9 checks and Farm 12 checks, all 0 failures. Scripted drops invoke real NewPlayer body collisions; PlayHandHit is explicitly injected. Fountain dry outside has2 matching body leases, so no one-event/cadence claim. Farm covers two stalk patches, dry Grass and ditch Water, not pond surface. Listening and remote footsteps remain open.
- **H2 — fresh-load furnace/horn/UI verified:** genuine debugOverride=true defect corrected in 6fbeb4eda. [Fresh main](furnace-fresh-main.txt)/[fresh peer](furnace-fresh-Player2.txt) both start false without runtime assignment; [main](horn-fuel-ui-main.txt)/[peer](horn-fuel-ui-Player2.txt) repeat 5 settled fuel samples with 0 failures. Main 46.6 s includes horn hold/nonloop/release fade; peer 65 s supplies five fuel/UI/emission observations. Earlier manually assigned results are archived under horn-fuel-ui-live-flag. Button callbacks/poses injected; audio quality/VR touch remain open.
- **T2 — throttle network edges:** [actor3](throttle-edges-actor3.txt)/[actor4](throttle-edges-actor4.txt) RUN2 pass 19/15 checks: contention, loser rejection, pre-ack release, ordinary synthetic-palm non-master grip/GO, regrip during extended return and injected focus-loss release. Same peer frame measures early Update gap 0.084978 m versus post-LateUpdate0.008542 m; earlier remote-palm failure is fixture sampling order, not a throttle product fix. Default return duration, OS focus delivery and headset feel remain open.
- **M2 — menu lifecycle and teardown fix verified:** f7c6e0099 silences OnDisable only, avoiding fallback AudioManager creation during scene teardown. [Fresh lifecycle](menu-lifecycle-fixed-actor1.txt) passes all assertions 0 failures with actual Compound return/private rejoin and restoration; [cleared Error/Exception/Assert query](menu-teardown-console.txt) is empty. [Fix note](menu-teardown-fix.md) separates the genuine teardown bug from the earlier invalid same-instance assertion; Single scene loads intentionally recreate scene-owned menus/rigs. Synthetic poses and summon/grab callback injection do not establish headset input.
- **S2 — Compound installation/purchases/peers:** [19 collider envelopes](compound-store-clearance.txt) have 0 overlaps and [active binding checks](compound-store-active-check.txt) pass. [Seven real purchases](compound-store-goods-Compound.txt) charge exactly once each, wallet 1,191→191 (**1,000 total**, not 1,025), spawn actual held network cargo, and Default equip is free. [Main](compound-store-peer-main.txt)/[Player2](compound-store-peer-Player2.txt) each match all 7 IDs/tokens with 0 failures; all 3 bundle entitlements remain absent. [Grounded A/B](compound-store-grounded-camera-ab.txt) records 4 frames at two grounded positions/head 1.55 m and automatic restoration; all four images were viewed, confirming down 0.20 m removes the bright top-center bulb. Compound-only invisible mount/installer fix00945ca91 is persisted and freshly loaded. [Fresh trace](compound-store-fresh-camera-ab.txt) records two grounded1.55m-head captures; [frame0](compound-store-fresh-current-p0.png) and [frame1](compound-store-fresh-current-p1.png) pass review with bright bulb removed. Buyer-leave/rejoin/departure cleanup now additionally passesS3; headset approval remains open.
- **R2 — bounded reverb lifecycle:** [Player2](reverb-independent-Player2.txt) adds West bunker to earlier Arcade sampling; local target 0 m, Arcade 298.5 m outside 1 m. Actual Compound unload removes all captured zones; fresh zones return on private rejoin;0 failures and flags/train-relative pose restored. Sequential one-peer placements with main retaining room, not three simultaneous listeners or all 25 zones.

- **J — actual full route and natural-ending retained-actor rejoin:** [main](full-route-main.txt) passes 252 label samples/0 errors, actual distance 0→1,016.448 m at normal 4 m/s and natural Bridge crossed finish. Controlled fuel 1,000, injected operator placement and pre-lowered bridge exclude resource/defender/VR traversal acceptance. RailDistanceDisplay.BridgeMetres=1,000 at route finale 940.2463; actual finish display 1,081 m. [Contact recovery](bridge-hold-recovery.txt) corrects an initial Warp stand-point/stale-pose fixture issue and reaches fully lowered through real finger overlap/held input. [Player2](ending-latejoin-Player2.txt) actually disconnects, delays retained-actor RejoinRoom 12 s, returns at shared ending age 14.618 s with original deadline/retained scene, reaches 175 m dawn view and normal Staging night 120 m; 521 samples/0 failures. This is not fresh-actor/cold-scene join or public three-client stranded post-token return.

- **P3 — public original-token recovery:** [summary](public-stranded-summary.md) and [driver](public-stranded-driver.txt)/[cut](public-stranded-cut.txt)/[keeper](public-stranded-keeper.txt) finish13:24:47 with all3PASS. Actual20s public boarding, physical hold,40 real coal deposits,4m/s journey and natural success near856m. Cut disconnects2.025s into Ending; production token at finish age34.279s; actual original-room rejoin requests departure in0.038729s and ready Compound in2.818667s, all3to35148. Keeper session is deliberately disabled only during Ending to retain room, then restored; not original headset repro. Attempt1 post-token branch was NOT REACHED due changed actor/authority; corrected fixture acknowledges driver on all3clients after transfer, no product change.
- **S3 — Compound purchase lifetime:** [buyer](compound-purchase-cleanup-buyer.txt)/[observer](compound-purchase-cleanup-main.txt) pass two genuine25-coin charges191→166→141; first token disappears on hard buyer leave and does not replay after rejoin. Second token is absent from expedition views/current manifest after attempt1 actual public boarding; later fixture abort does not invalidate cleanup, rerun explicitly skips another claim.
- **F60 — actual flashlight attribution:** [actor3](flashlight-sixty-actor3.txt) ordinary actual hand claim/injected trigger switches on held flashlight; authoritative flashlightSeconds grows61.18078 over71.257wallseconds,4checks pass, original input/poses/cabin/on-state restored. No stats request/flag injection.

- **C3 — actual late coal and authority:** RUN2 [master](coal-late-fuel-master.txt)6checks/[peer](coal-late-fuel-peer.txt)14checks PASS13:34:12: actual nonmaster deposit, retained receipt, real leave/rejoin,0historical audio/burst/popup, presentation cache seeded and maximum ready observation gap0.312571s; nonmasterFuel100 is rejected. Earlier server-switch Photon-clock stall is an archived fixture limitation, [evidence](coal-late-clock-evidence.txt), not productionFAIL.
- **R3 — moving radio real contact/disable:** [actor1](radio-contact-actor1.txt)14checks/[actor4](radio-contact-actor4.txt)21checks PASS13:35:11 after real coal rejoin; controlled actual fingertip contact, moving shared lease, real disable clears transmitter/lease, withdrawal rearms and rig/input/control state restores. No audible voice acceptance.
- **A3 — private audio observation:** [read-only run](private-audio-readonly-actor1.txt) passes53.3eligible private Running seconds/0ticks,54Effects gain reads0dB at saved100%; rumble/music absent, so NO routing/volume-sweep/speed/music-fade/return-tick proof.

- **G2 — current station/Container geometry:** [27checks](station-container-readonly.txt) PASS13:41:32,0fail/0blocked in0.035s: current active Environment collider rays for rear doors/gate/sidewalls/retaining walls and both fences at3authoredZ samples; static X+/-2.75,Y.3..5.2,Z75..160 train/tunnel envelope clear; actual160m seam origins/support match; Container1,440renderers tested atall7viewer positions, exact show/hide/collision/visibility restoration and companion readiness. No rig/route transforms changed, no natural traversal/passenger/smoke claim. Initial System.Diagnostics clock command failed compilation and did not execute; EditorApplication monotonic replacement runs.

- **W2 — actual coasting/moving wheel reconstruction:** [master](wheels-coast-master.txt)8checks/[peer](wheels-coast-peer.txt)10checks PASS;100m real travel,76/84coast frames,1,278/1,162wheel samples0angle error, actual moving fresh-instance join before coasting, stable natural rest and original fuel restored at final distance. No distance reset or fresh-run replay claim.
- **A4 — controlled success audio/Effects:** [actor1](success-audio-volume-actor1.txt)/[actor3](success-audio-volume-actor3.txt) bothPASS13:50:13. Real SimulateSuccess receipt starts normal32s controlled Ending; real music0.8/full/fade/stop, Effects0/50/100 actual-80/-6.0206/0dB, original settings restored, actual5–1ClockTick leases exactly once on each client and no zero/above5/after-transfer. Attempt1 CAS-busy single request never finished; archived no-finish logs invalidate its erroneous return-completed literal. No fabricated playback or natural-finish claim.
- **A5 — retrospective actual-speed audio quantification:** existing [main](audio-events-main.txt)/[peer](audio-events-Player2.txt)4m/s consecutive clack windows: main14events10:37:27.204–38.541 median0.875s/range0.838–0.897; peer32events10:34:48.713–10:35:15.840 median0.875s/range0.847–0.906. Sampled distance steps3.348–3.592m and3.388–3.620m fit3.5m joints at4m/s with frame sampling.211/279settled rumble samples show0.6gain/~1.25pitch; acceleration increases and coast reduces pitch/gain, sampled stop cues0/.006m/s. Observer logs unique voice generations and half-second source snapshots; not an exhaustive speed sweep or exact smoothed-frame proof.
- **RP — repel attribution:** [interpretation](repel-attribution-interpretation.md) accepts callback-driven sender dispatch/authority confirmation/shared0→1. Raw entire fixture exclusivityFAIL is confounded by master’s independent local sequence2 versus peer1; no stats misattribution production defect is established. Natural weapon strike and uncontaminated exclusivity remain unproved.

- **T3 — committed default-window regrip fix:** b9bbf51d4, [fix note](throttle-regrip-fix.md). Local27drive/42session checks and independent review pass. Actual [default nonmaster](throttle-default-postfix-actor1.txt)32checks/[master](throttle-default-postfix-actor2.txt)24checks0fail:0.245366s default return within a0.029893s frame of0.25s; real new grip before own release echo captures/rendered0.9016726 and receives new authority token without return extension or direct grip injection. [Edge actor1](throttle-edges-postfix-actor1.txt)19/[actor2](throttle-edges-postfix-actor2.txt)15checks0fail repeat contention/pre-ack release/peer palm/extended-return regrip/injected focus cleanup. The edge2.5s return and disposable inactive eligibility checks are separately scoped. Menu/down/disconnect cases remain unrun.

- **L3 — final APK and restoration:** Final **b9bbf51d4 plus preserved working art** APK build and installation **PASS: 0 errors, 6m59.1986735s, 2,552,548,123 bytes**. SHA-256 **9E97731D4F2D0DCC318393A1E09C360D3A2DEB7CF3247DD3D37DB02CE4310EAD**. [Build](quest-throttle-build.txt), [hash](quest-throttle-apk-sha256.txt), [installation/idle-device readback](quest-throttle-install.txt), [HEAD](quest-throttle-build-head.txt), [working-tree provenance](quest-throttle-build-working-tree.txt). No new native survey was run; positional/head tracking still blocks performance acceptance. [Final editor cleanup](final-editor-cleanup.txt) at14:14:04 verifies the original StartingStation-bake scene clean in Edit Mode, scenarioNotRunning, runnerfalse, desktop/quick/brightfalse, exact original Farm/Canyon/Container/Skyscraper order, useOrder/invincibletrue, fixture inputs disarmed and regressionPendingfalse. Final Error/Exception/Assert query reports0. Original boolean backup was lost on restart, so these are the last recorded logical settings, not byte-identical preference restoration. Final install readback at14:14:55 finds no game process or pending performance request.

## Each Pending entry

### 01. City starting station v3

- Observed: [aligned jump routes](station-traverse-jump.txt) pass both platforms/boarding gaps, bridge crossing, supplies doorway/shop aisle, washroom and office. [Raised-hand retry](station-stairs-raised-hands.txt) passes both lower flights; **both upper flights remain unresolved fixture FAILs**; after earlier single-pulse/unready-Staging limitations, the final [30-second timed probe](station-upper-timed-pulse.txt) actually runs138samples/137actual contacts with one consumed jump and restored state. Hand contact cancelling jump is a strong inference; natural upper-flight completion remains **INCONCLUSIVE**, with collision geometry unchanged. L/I/S provide current lighting/loading and seven departure-shop purchases/camera coverage.
- Remaining automation: diagnose upper-flight contact/posture with grounded endpoints before deciding geometry changes; G2 now verifies rear/gate/retaining/yard static boundaries, centreline seam and static train tunnel envelope. Passenger/prop/smoke clearance and explicit late-join station checks remain distinct. First straight-line failures included salvage obstruction and unsuitable hand/orientation setup; they do not justify broad collision edits.
- Needs human/blockers: VR stair comfort/scale, reach/readability, room/yard lighting and Quest cost. No proven station geometry defect is asserted from the failed fixtures.

### 02. Bridge crash collapse

- Observed: B covers raised/half/almost crashes, restored fresh run, actual success, two-peer effects/results, reconnect/master loss and passenger recovery. Fixture-only helperac918293b fixes Warp standing-point placement; [main](bridge-helper-verify-main.txt)1,010samples and [peer](bridge-helper-verify-Player2.txt)1,018samples both reach full lowering with0failures. No bridge gameplay fix or added group.
- Remaining automation: target-device frame cost is tracking-blocked. Current reconnect completion after falling is narrower than an arrival during active collapse; queue permits during/after, so do not require a new cold-scene load inside3.2s or extend production timing solely for a fixture.
- Needs human/blockers: explosion/fire/smoke appearance, impact scale/pacing and VR comfort. No unresolved bridge production failure in accepted trials.

### 03. Test panel fuel 100

- Observed: H2 installed FUEL100 action callback and fresh-instance fuel/UI states pass with receipt-aware retries; furnace fix 6fbeb4eda is fresh-load verified.
- Observed additionally: C3 explicit nonmasterFuel100 authority rejection passes.
- Remaining automation: physical desktop/controller input remains separate from callback execution.
- Needs human/blockers: VR button delivery/readability; injected callback is not a real touch.

### 04. Baked fixture double lighting

- Observed: L/L2 load/suppress/restore, including initially disabled fixtures,24 finale fixtures and three helipad realtime lights; successful Android build 33a080740 closes the editor-only API compile defect.
- Remaining automation: current-source build/install passes; valid device lighting/performance remains blockedB04 by absent positional/head tracking. Neither invalid capture proves device behavior.
- Needs human/blockers: softness, hard-cone/double-light appearance, fog comparison and headset rendering.

### 05. Compound countdown plate animation and SFX

- Observed: solo observer3 cm/fixed collider/2 On/2 Off/21 positive-second ticks; P2 adds completed two-client occupancy/cancel/restart/transfer with 0 failures and no extra state-edge cue.
- Remaining automation: private-rest, comparable peer tick/volume and specific late-arrival timing cases beyond recorded windows; three-client stranded return is entry61.
- Needs human/blockers: Metal audible contact, balance, scene-rest fit and headset feel.

### 06. Compound fountain water sounds

- Observed: runtime mapping plus C2 fountain 9 checks/0 failures: two actual Water body falls and dry outside, with injected hand callbacks. Outside body records2 matching default leases.
- Remaining automation: natural hand-contact delivery, edge traversal and duplicate/cadence behavior; no local FootstepEmitter exists and mapping alone is not playback.
- Needs human/blockers: water sound quality/contact feel and blue apron appearance.

### 07. Compound pressure plate dark-metal finish

- Observed: entry05 confirms motion only. No new visual approval is recorded.
- Remaining automation: no essential mechanical gap beyond05; capture in gameplay can support review.
- Needs human/blockers: dark top/hazard border readability and preference while moving.

### 08. Saved fog settings and atmosphere tuner

- Observed: D saved endpoints plus J natural-ending retained-actor reconnect: shared-time fog/view interpolation matches expected progression through full dawn, original deadline retained and night reset on normal Staging return.
- Remaining automation: fresh Edit Mode tuner-close/preview restoration remains historical; retained-scene reconnect is not cold-scene/fresh-actor join or frame-matched visual equivalence.
- Needs human/blockers: horizon continuity, visibility and atmosphere preference.

### 09. Quackbear lip pivot normalization

- Observed: no new runtime lip evidence; prior import geometry checks remain historical.
- Remaining automation: owned equip/switch and visible-face targeting once entitlement exists.
- Needs human/blockers: **missing Quackbear entitlement**, actual microphone, mirror/LIV lip motion and resting shape.

### 10. LIV local lips follow equipped skin

- Observed: R default lip live-source/capture plumbing6/6 passes via direct PoseCaptureFace injection; no current live speech/native recording/owned-skin-switch result.
- Remaining automation: runtime selected-face references and peer/transition switching with owned bundles.
- Needs human/blockers: all three entitlements absent; actual microphone, headset/LIV preview/recording and remote speech acceptance.

### 11. FX_Heal_02 revive completion

- Observed: N actual ground down/proximity; N2 corrected three-client ground/moving/victory/late-join playback. Post-LateUpdate follow observation passes 0 failures on all three clients after cd39a40e0.
- Remaining automation: recovery-teleport frame executes later than follower and is not sampled; any four-account independent-pair case remains prerequisite-blocked.
- Needs human/blockers: new three-layer effect scale/placement/night materials and audible feedback; no old four-layer result substitutes.

### 12. Farm Silos screenshot repairs and pond collider

- Observed: G repaired five ramp meshes/support, slide solids, pond underside and temporary sphere support; ditch bed/wet-volume/dry bridge; L/I/C current loading and selected cargo.
- Remaining automation: actual simulated traversal over every landing/slide edge, carried gameplay items through joins, revised-area enemy pursuit/late-join behavior; pond footsteps after surface assignment.
- Needs human/blockers: hand contact, texture readability, lantern mounts/darkness. Pond Water AudioSurface assignment is an explicit queue prerequisite, not established by support rays.

### 13. Arcade open-window collision

- Observed: G finds no former pane colliders, retains frame inventory, passes16 cm aperture sweeps and actual temporary Rigidbody passage through both windows.
- Remaining automation: positive frame/sill gameplay contact and hand/carried-item passage. Temporary sphere passage does not prove hand locomotion.
- Needs human/blockers: headset contact/traversal feel.

### 14. Speed-matched train wheels

- Observed: D wheel phase follows actual distance, freezes at stop and mounts stay fixed; two-client peer logs maintain zero angle error across B and moving rejoin I.
- Observed additionally: W2 actual100m travel/fuel-exhaustion coast/rest and moving fresh-instance reconstruction pass on both clients with0wheel angle error.
- Remaining automation: explicit fresh-run wheel-phase replay and broad natural motion appearance remain separate; reverse was an EditMode check, not an added live requirement.
- Needs human/blockers: faceted wheel appearance/rolling direction in headset.

### 15. Quackville blocked-exit sign

- Observed: no new headset/readability evidence.
- Remaining automation: none essential; gameplay view capture can support human review.
- Needs human/blockers: approach/stair sightlines and readability.

### 16. Integrated radio console

- Observed: R installed button/cap/lease/transmitter19/19 passes, remote held/released snapshots agree; actual source configuration and near-zero mic meter observed.
- Observed additionally: R3 actual controlled fingertip contact/renewed moving lease/real disable cleanup passes on both clients after real rejoin, extending the original injected callback scope.
- Remaining automation: audible handheld-walkie voice exchange, natural hand reach and headset quality. No audible voice delivery is established by lease/transmitter state.
- Needs human/blockers: microphone/voice quality, reach, cap/icon seating and night readability.

### 17. Subway terminal raised sign lettering

- Observed: current Compound load is incidental; no fresh angular appearance acceptance.
- Remaining automation: no essential behavior test; gameplay captures for three signs if useful.
- Needs human/blockers: face/edge rendering, up arrows, no duplicate text and cream material readability.

### 18. Farm stalk-only foliage sounds

- Observed: 3,669 stalk samples/52 volumes and registrations; C2 actual body falls/injected hands at two patches plus Grass dry control and ditch Water priority pass 12 checks/0 failures.
- Remaining automation: natural hand boundary traversal/cadence/no-idle behavior and independent peer contact presentation; not every planted/path edge.
- Needs human/blockers: audible balance, suitability and natural boundary feel.

### 19. Platform and return clock ticks

- Observed: solo public21 expected ticks in05; A has exactly5–1 on both clients across five completed success/failure/fault returns, no zero event in those records.
- Observed additionally: completed public two-client occupancyP2 and actual public3departureP3; A4 supplies actual Effects sweep/restoration and exact5–1returnticks/nozero/after-transfer on both clients. A3 supplies private Running silence only.
- Remaining automation: explicit late-join current/future-only ticks and silent private Staging remain distinct from those observed windows.
- Needs human/blockers: loudness and perceptual alignment with clocks.

### 20. Pause menu follows the train

- Observed: D moving-frame tracking and M2 all lifecycle assertions 0 failures, including ashore, grab/drop, real public return/current-head resummon and rejoin. Silent-OnDisable fix f7c6e0099 has clean post-transition Error/Exception/Assert query.
- Remaining automation: slope/rotation and natural input not newly isolated. Prior same-instance FAIL is fixture-only and needs no persistence redesign.
- Needs human/blockers: headset stability/reach/grab feel; explicit close sounds preserved.

### 21. Subway terminal model swapped into Compound

- Observed: P2 occupancy/departure plus S2 active store,19 clear envelopes/17 displays, seven real paid goods/Default free and both peers matching 7 tokens. Grounded camera A/B removes the top-center lamp at down 0.20 m.
- Remaining automation: persisted camera offset00945ca91 freshly reloads and both grounded captures pass. Spawn/guard/stall traversal and private physical Launch path remain separately scoped.
- Needs human/blockers: eight-duck fit, natural contact/climbing, wire/gate appearance, reach and12-player Quest cost.

### 22. Train model-aligned collision

- Observed: E isolated minion/spider loops and corrected knock-off; F actual two-client furnace deposits/authority handoff; I moving rejoin; D physical grip and sampled support.
- Remaining automation: deliberate loose/thrown tools/gold/coal riding through slopes/turns and hand/body contacts on all shell regions; explicit replicated mounted hand. N now completes the scoped real-network spider regression.
- Needs human/blockers: natural moving-platform/edge feel and Quest physics cost. Genuine spider defect has a recorded production fix, isolated reruns and current two-client correction coverage; overall train collision acceptance remains broader.

### 23. LIV recording game audio and remote voice

- Observed: no native recording evidence. Known editor unsupported DX12/LCK message is an environment limitation, not a device recording result.
- Remaining automation: Quest build/device sequence can prepare recordings through Login/Compound/Expedition/return and repeat; main Quest work in progress.
- Needs human/blockers: microphone/remote speaker and listening to saved device clips, toggle independence/levels/sync. Source leases cannot substitute for captured audio.

### 24. Midpoint helipad side fences

- Observed: G samples both fences at multiple heights/Z, open railway seams,14 convex rock top contacts and ground sphere settling; L/I/C translated readiness, selected loot and peers.
- Remaining automation: actual body/item contact at rock/fence bases, shop/helicopter access and reach across enlarged rocks; selected rays do not exhaust fence gaps.
- Needs human/blockers: rock-vs-coal silhouettes, ground seating, night/edge readability and natural reach/contact.

### 25. Farm Silos production atlas integration

- Observed: L/V/I current production loads/translates/rebinds/hides/restores, current pool metadata105/117/22; G/C provide sampled geometry/cargo; foliage18.
- Remaining automation: broader silo/house/roof/cage/playground routes and pursuit, specific roof/hay candidate regressions under current geometry, water/reverb contacts.
- Needs human/blockers: moving-view atlas/normal/shadow/emission quality, natural reach/darkness. Historical renderer totals in queue are superseded by current runtime inventory where different; no new device benefit claimed.

### 26. Container Yard production atlas integration

- Observed: L translated lifecycle/bindings; I peer metadata/current pools168/142/32; G/C north-yard probes and one actual cargo sample.
- Remaining automation: G2 now closes explicit Container neighbor hide/show on all7viewer positions with exact restoration. Broad interiors/roofs/gantry traversal and reverb/navigation gameplay remain distinct.
- Needs human/blockers: seams/minification/normals, darkness, natural climbing/trailer/dock reach. No routine per-block A/B required by rollout policy.

### 27. Skyscraper reduced geometry and low-poly production atlas

- Observed: L/V/I loading/visibility/current metadata; G all17 retained floor groups, absent removed-floor support and sampled treads; highest-floor temporary spheres; C selected21 loot settled and one carried item.
- Remaining automation: actual stair/skybridge/crane routes and upper-floor item recovery; targeted18 baseline candidate-overlap spawn/pop cases across seeds rather than extrapolating from21 selections.
- Needs human/blockers: skyline/undersides/minification, natural shaft/rail contact, night readability and performance. Baseline flags remain historical known cases, not newly found defects.

### 28. Convenience Store production atlas integration

- Observed: L/V/I; seven brush visuals rotate while hulls remain fixed; G Arcade aperture physics; C selected22 loot settled and one cargo path.
- Remaining automation: broad interiors/shelf/stockroom/semi interaction and enemy pursuit; visibility transitions do not prove no missing rendered faces.
- Needs human/blockers: atlas/neon/reflection/shadow equivalence, moving minification/darkness and natural brush contact.

### 29. Full Motel Blender atlas review

- Observed: L explicitly loads/translates/unloads/reloads the **separate full-map review prefab** with 2,269 bindings. Production I/G/C do not automatically apply to review gameplay.
- Remaining automation: review-prefab hide/show/culling and interior interactions if needed for acceptance; no new production rollout or new comparison authorized by this ledger.
- Needs human/blockers: full-map review/minification/interior/traversal; prior November13 device comparison remains historical, not rerun or uniform performance proof.

### 30. Motel east-wing Blender atlas pilot

- Observed: L explicitly tests separate wing review lifecycle with 2,025 bindings.
- Remaining automation: review-specific visibility/interior gameplay if needed; no production integration implied.
- Needs human/blockers: headset seams/culling/interior movement. User accepted earlier scoped performance comparison and requested no repeat; do not reopen that A/B solely for this ledger.

### 31. Player viewing distance 120 m to 175 m at dawn

- Observed: J reconnect at shared ending age 14.618 s reconstructs mid-dawn view 137.15→175 m, then normal Staging night 120 m; 521 observer samples/0 failures. D/I earlier endpoints remain separately scoped.
- Remaining automation: unrelated camera/benchmark precedence and cold-world reconstruction where permitted; do not describe retained-actor warm-scene rejoin as a new actor admission during Ending. Current native cost remains pending.
- Needs human/blockers: head turns, distant signs/lights/valley and flashlight reach.

### 32. Motel spatial render consolidation

- Observed: L runtime chunks active/source hidden, component disable restores sources and reenable restores chunks, translation/rebinding/cleanup; V/I production readiness.
- Remaining automation: source/chunk matched rendered comparison if still required; current device work independent of historical A/B. No fresh memory/load-cost measurement here.
- Needs human/blockers: interiors/reflections/culling and natural traversal. Old50/100/1,400 m statements are historical experiments; current player default120 is covered by31.

### 33. Quest performance runner robustness and expanded coverage

- Observed: ADB/preflight, successful second APK after 33a080740, and verified interrupted-attempt cleanup. Meta Link/desktop XR interrupted first native attempt; its performance data is excluded.
- Remaining automation: final current-source build/install and explicit cancellation/collection/cleanup pass; valid device performance is blockedB04 by missing positional/head tracking. Additional crash/restart/request-consumption cases remain as originally required.
- Needs human/blockers: upright stable framing/comfort, interiors/combat/real multiplayer load; older comparisons are historical.

### 34. Train, furnace, throttle, horn and ending-music audio

- Observed: D/A/F sources/receipts; H2 fresh-load two-peer five-state fuel/UI/emission/out-of-fuel/relight0 failures and main horn hold/no retrigger/release fade. Furnace fix 6fbeb4eda now validated without runtime flag assignment.
- Observed additionally: C3 actual late-coal no-replay passes; A3 private Running silence/currentEffects0dB passes.
- Observed additionally: A4 real success music routed toEffects/full0.8/fade/stop and actual0/50/100mixer sweep/restoration; A5 quantifies4m/s clack cadence and rumble acceleration/coast trends from existing logs.
- Remaining automation: exact continuous smoothed curves/exhaustive speed sweep/20m/s audio remain unproved. A3 alone supplies no routing proof, while laterA4 does prove music/tickEffects routing.
- Needs human/blockers: perceived timing/mix and physical horn contact; unsupplied low-fuel warning clip remains a prerequisite.

### 35. Shared complete store and security camera

- Observed: S expedition matrices/feeds plus S2 Compound seven actual wall pulls/charges:1,191→191, exactly 1,000 coins. Both peers 7 matching identities/0 failures; Default free, three bundles entitlement-blocked. Four grounded current/down 0.20 m frames reviewed with actual 1.55 m heads; alternate removes bright lamp.
- Remaining automation: fresh-load camera override/installer and two feed captures now pass. Compound buyer-leave/rejoin and actual public-departure cleanup passesS3: current purchases retain only valid buyer/room/run lifetime and clear as intended. Further remote-held-object framing remains scoped. Existing expedition14-item retention does not prove Compound cleanup.
- Needs human/blockers: three missing bundle entitlements/Meta checkout; headset reach, labels, comfort and broad image-quality approval remain human.

### 36. Furnace Polygon flames

- Observed: H2 genuine debug override fixed6fbeb4eda; fresh instances false and 5 settled fuel samples pass on each peer, including corrected flame emission. Earlier manually matched instances are historical interim evidence.
- Observed additionally: C3 actual latejoin seeds receipt cache and produces0historical bursts/audio/popups. No need to repeat fresh-load debug-flag testing without another change.
- Needs human/blockers: flame/fire appearance and boarding fade remain human acceptance.

### 37. Quackbear, Huggy and Scrap Duck bundles

- Observed: current main/Player2 ownership snapshots confirm all three absent; Default equip is free in S. No fabricated ownership or successful bundle equip is claimed.
- Remaining automation: after legitimate entitlement, three avatars/default across peers/late join/transitions, separate120 s cooldowns, room reset/scene persistence, unique held/bagged/loose weapon/bag IDs and cleanup at all shops.
- Needs human/blockers: **entitlement/backend/Meta product prerequisite**, checkout/cancel/restore, reach/grip and eye/lip/label appearance.

### 38. Matching fuel/distance backing

- Observed: H2 five settled full/half/empty/refill states verify rendered CutePixel labels, segment counts/backing and matching peer state with 0 failures.
- Remaining automation: no essential repetition of those samples; full-route distance-label progress belongs to46; fresh-load furnace issue is separate.
- Needs human/blockers: visual/readability preference remains manual.

### 39. Throttle hand offset, variable speed and return

- Observed: D both-hand ordinary grip; T2 contention/loser rejection, pre-ack release, live regrip during deliberately extended return, actual non-master grip/GO and remote post-pose palm.
- Observed additionally: [master18checks](throttle-default-actor1.txt)/[operator24checks](throttle-default-actor2.txt)0fail for physically measured deadzone/proportional curve/detent, STOP and intermediate/complete default0.25s return.
- Genuine issue fixed and rerun: same-player released-token guard corrected inb9bbf51d4; T3 local27/42checks and two-client default32/24 plus edge19/15 pass0failures. Actual default-window regrip captures0.9016726 before own release echo and receives new lease; source fix is now accepted in that scope. Adjustable offsets and remaining control-loss cases stay separate.
- Needs human/blockers: headset feel; early remote0.085 m mismatch is a fixture-order error, corrected same-frame post-LateUpdate0.008542 m.

### 40. Approved cockpit and lowered throttle axle

- Observed: D both-hand grip, F/H2 fuel and injected horn Press/Release route.
- Remaining automation: wide-bar endpoints/full-arc clipping remain untested; existing horn callback test does not establish physical reach.
- Needs human/blockers: overall entry remains manual-only cockpit scale, comfort, reach and presentation.

### 41. Furnace fuel popup

- Observed: F four current receipts produce four CutePixel +7.8 popup leases per peer, matching rounded7.8125 crew-scaled gain; no duplicate among observed receipts.
- Observed additionally: C3 actual latejoin after deposit produces0historical popup plays; peer cache seeds committed receipt.
- Remaining automation: fresh solo-value case remains distinct from current two-client receipts.
- Needs human/blockers: number readability/placement in headset.

### 42. Seven-block material contact sounds

- Observed: C2 supplies actual local body contact plus injected hand dispatch for fountain Water/default and Farm foliage/Grass/ditch Water. No invented PlayerFootstepAudio adapter; actual path is NewPlayer.bodyLandingSounds/OnCollisionEnter and PlayHandHit.
- Remaining automation: representative remaining seven-block surfaces, natural hand contacts, peer presentation/boundaries and duplicate suppression. Outside fountain2 leases prevents an exactly-one cadence claim.
- Needs human/blockers: audio quality/contact feel; previous29 probes remain historical.

### 43. Broad block reverb

- Observed: R2 passes sequential Player2 Arcade and West bunker placement, actual unload destroys captured zones, original private rejoin recreates fresh named zones;0 failures/flags and pose restored.
- Remaining automation: all 25 zones, moving entry/exit, simultaneous independently positioned peer-listener observations and late-join tails beyond sampled reconstruction.
- Needs human/blockers: audible decay/smoothness/interior transitions and voice quality; local properties are not listening evidence.

### 44. Container Yard north yard, fences and open containers

- Observed: G scoped north-yard paths/support/tank side contacts and temporary drops; C real selected item carry/drop later settles on both peers; L/I current pools/loading/agreement.
- Remaining automation: actual dock-step/trailer threshold/pallet climb and eight opened interiors, fence continuity and real enemy pursuit; targeted workbench/lower-shelf/tank-corner reach.
- Needs human/blockers: night/wire/interior appearance, natural door/fence contact and Quest cost. Early awake cargo sample is resolved as described in C.

### 45. Raider Legends revive feedback

- Observed: N/N2 ground, handover/overlap, moving 4 m/s, withdrawal/reset/master handoff, controlled32 s victory/frozen score, late progress/unload/no replay and corrected particle follow pass across three clients.
- Remaining automation: four-client independent rescue pairs require fourth account; recovery-teleport-frame ordering remains untested. Earlier0.1 s grace evidence is retained rather than falsely attributed to latest rerun.
- Needs human/blockers: headset placement/scale/audio. Genuine follow bugcd39a40e0 is validated with 0 post-LateUpdate failures; fixed ground and late cases do not add duplicate scenario counts.

### 46. Train distance display

- Observed: J actual full drive from 0 to 1,016.448 m at 4 m/s, 252 matching monotonic/no-jump label samples/0 errors and natural Bridge crossed success. Current BridgeMetres=1,000 at finale start 940.2463, so finish label 1,081 m is correct; never 10,000.
- Remaining automation: no need to repeat this completed route/label test without a relevant change. Explicit label reconstruction on other reconnect/late-join paths remains distinct from peer atmosphere checks.
- Needs human/blockers: driver/headset legibility/placement; controlled fuel 1,000 does not prove resource balance.

### 47. Motel community leisure layout

- Observed: L/V/I current production; G sampled leisure circulation/open doors/support; C selected17 loot settled and real cargo; E isolated Motel minion route.
- Remaining automation: actual stair completion, pool recovery, golf/rink/rental/vendor/bus-roof routes and targeted pursuit/item recovery. Old failed stair probes are not freshly resolved by today's capsule points.
- Needs human/blockers: natural reach/darkness/scenery/Quest cost. **Current prefab has no waterslide**; obsolete slide-specific queue text is superseded and should not cause a false failure.

### 48. Convenience Store screenshot polish and arcade mascot

- Observed: L/V/I, cosmetic brushes fixed hulls, G current Arcade aperture, C actual selected loot/cargo.
- Remaining automation: court/arm/tree/rooftop shape contacts, wash/rear road/semi/cooler/restaurant paths and pursuit, closed rear-door exclusion, frame positive controls.
- Needs human/blockers: mascot/Q/neon/tree underside appearance, natural contact/reach, glare and Quest cost. Renderer/collider inventories do not prove all surfaces visually match.

### 49. Skyscraper north/south yards and upper-floor loot

- Observed: same current17-floor G/L/V/I/C scope as27, with selected21 objects settled.
- Remaining automation: actual office/shelter/foundation routes, roof/upper-floor carry/recovery, wall contacts, repeated upper-floor selections and pursuit; inspect specific18 known ground candidates for pop rather than infer from unrelated selections.
- Needs human/blockers: natural stairs/bridge/crane reach, worklight glare/darkness and device cost.

### 50. Attached hold-forward throttle

- Observed: D both-hand synthetic grip; T2 real two-client contention, no ghost pre-ack release, non-master attached palm/closed index post-LateUpdate, extended-return regrip and injected focus-loss cleanup all pass.
- Observed additionally: T3 post-fix contention/pre-ack release, physical peer palm, default and extended-window regrip, injected focus cleanup all pass.
- Remaining automation: menu/down/disconnect interruption cases, actual OS focus/T delivery, full slow/fast strokes and18cm separation where not independently recorded. Narrow fixture failed its second-grip setup before panel interruption; it is not panel-cleanup evidence.
- Needs human/blockers: headset grip/feel and real-machine jitter; extended return is not default timing acceptance.

### 51. Farm full screenshot polish

- Observed: G silo support/pond/ditch samples; L/V/I/C current Farm;18 foliage. No fresh all-house-flight/cage/loft route completion.
- Remaining automation: four house stair flights, cellar exits/cells/lockers, attic/barn/silo turns with real cargo and enemy pursuit; verify latest roof/hay candidate geometry without assuming old reports current.
- Needs human/blockers: ramp/stair comfort, grain/ceiling/corn texel quality, mounted lights/darkness, water sound and Quest cost.

### 52. Bridge finale batch polish

- Observed: G all ten ramp support lines; L/V/I/C current lighting/selected loot; B actual bridge crossing/tunnel/dawn sequence after controlled approach and fault restoration.
- Remaining automation: **actual** ten-ramp and four-block climbing routes, river return, defender approach/hold-pause-resume and targeted roof-coal recovery. Support rays do not close older incomplete upper/inward traversal cases.
- Needs human/blockers: flicker/gables/cliff/bank/ballast views, low slab underside/hand clearance, nighttime/dawn readability and Quest CPU/GPU/memory.

### 53. Farm northeast pond and playground

- Observed: G pond support/temporary sphere recovery-surface samples and slide solids; V static swing without physics; L/I/C loaded peers and selected loot.
- Remaining automation: actual three-platform ladder/bridge/slide routes, pond-bank traversal and gameplay-item recovery, hay/cart reach and enemy approach.
- Needs human/blockers: natural climbing/slide/shallow-water feel, night readability and performance; no comparison with prior cornfield inferred.

### 54. Results board superlatives: stats, counters and award assignment

- Observed: N current two-client report-only board/state/freeze checks pass with actual purchase/gold/coal/ground-rescue counters, controlled success finish,32 s shared deadline and natural return. Main723 visible samples/0 distinct failures. This is current evidence, separate from B's actual crossing.
- Observed additionally: [current pure EditMode checks](awards-editmode-current.txt) pass all65 existing award checks (all20 minima, fallback, top-holder/priority, nonescaper, serialization) plus isolated failed-Finish no-escape/success, Hero/fallback and JSON lock. Same report-only group, no live stat fabrication or new count.
- Observed additionally: F60 supplies actual61.18078-second flashlight attribution with ordinary injected trigger/real held lamp and restoration; no submitted score.
- Observed additionally: RP controlled nonowner SubmitSwing sender dispatch/authority confirmation/shared0→1, not a natural weapon strike.
- Remaining automation: uncontaminated exclusive attribution is INCONCLUSIVE because master also locally submitted hits; no product misattribution bug established. Targeted pre-finish reconnect/handoff remain distinct from pure algorithm coverage.
- Needs human/blockers: threshold/proximity/stillness feel. **REPORT-ONLY: record failures; do not apply gameplay fixes under this entry.**

### 55. Train aboard margin: escaped and end-of-run teleport

- Observed: B current far-passenger crash recovery with locked state unchanged. This does not reverify side/nose successful-finish classification.
- Remaining automation: optional real second-client side/nose/far classification at actual finish; earlier desktop boundary results remain historical.
- Needs human/blockers: **manual-only** hand-hanging/headset finish acceptance.

### 56. Expedition render warmup: flashlight and helicopter TV first use

- Observed: I ready moving rejoin and S live feeds; no fresh first/repeat cold-cache warmup timing/veil/proxy-cleanup assertion. Snapshot containing a warm feed while loading is not itself a leak proof.
- Remaining automation: current Quest cold-cache/first-use and veil duration trace, explicit no leftover proxies/camera after readiness; earlier desktop cold-login tests remain historical.
- Needs human/blockers: **manual-only** headset first-use hitch/appearance and device shader/pipeline capture.

### 57. Compound Discord button

- Observed: no fresh dispatch/browser result; previous tests remain historical.
- Remaining automation: none required to repeat desktop debounce unless moved-button regression is needed with 21.
- Needs human/blockers: **manual-only** Quest browser launch/return, reach/readability and usable session.

### 58. Compound rating button and instruction PNG

- Observed: no fresh native/store UI result.
- Remaining automation: optional moved-button dispatch/debounce/status without submitting a rating; PNG replacement only if requested.
- Needs human/blockers: **manual-only** Meta launch/cancel/return, eligibility/error experience, PNG and reach/readability. Do not submit a rating.

### 59. Pause menu and rooms: headset buttons and feel

- Observed: M2 fresh callback/rig lifecycle passes actual return/rejoin with clean teardown after f7c6e0099. No actual P/Start/controller delivery.
- Remaining automation: focused desktop key delivery remains distinct; raw same-instance identity failure is invalid, not an outstanding defect.
- Needs human/blockers: manual-only Start/P, poke/grab/keypad/gate reach and hint readability.

### 60. Test panel on L3, admin-gated: controllers and dev builds

- Observed: H2 invokes installed FUEL100 action callback; this is more than setting a command but still does not prove L3/Esc, touch or build gating.
- Remaining automation: device admin/non-admin actual-input gating remains pending.
- Needs human/blockers: manual-only Quest controller delivery, authenticated account gating and menu-button separation.

### 61. Stranded return: three clients and original repro

- Observed: J now proves actual two-client private natural-ending disconnect/retained-actor rejoin with 12 s retry delay, original deadline and timely normal Staging return. B/P2 retain earlier scopes.
- Observed additionally: P3 actual public three-client original-token post-token recovery passes departure0.038729s/ready Compound2.818667s, keeper restored, all3terminalPASS. This resolves the explicitly automatable subcase; original headset repro remains separate.
- Needs human/blockers: manual-only original headset repro; account configuration is not execution.

### 62. Finale defenders, loot, gold crate, water, walls: headset feel

- Observed: current S actual gold intake/receipts, C selected loot support, B passenger recovery and I peer inventories. No fresh broad defender/water/wall feel acceptance.
- Remaining automation: only uncovered objective assertions if needed with52; no need to relabel old passes as new.
- Needs human/blockers: **manual-only** encounter/resource taste, natural roof/tower reach, coin/text/sound presentation, splash/bubble/muffle/fog, wall climbing/warp feel and Quest cost.

### 63. Zombie hit shapes: in-game throws and swings

- Observed: E real isolated spider melee knock-off concerns displacement, not all-five-enemy animated hit-shape/nearest-hit acceptance.
- Remaining automation: optional current thrown/held crowded-enemy fixture regression; no fresh general hit-shape result recorded here.
- Needs human/blockers: **manual-only** natural throws/swings, visible limb/head fit and grab/deflect feel.

### 64. Enemy tuning + late-join flares: in-game

- Observed: E scoped boarding/stalking behavior; no new ground/moving-train death and40 s late-join flare lifecycle proof.
- Remaining automation: real deaths/late join/avoidance/remaining expiry if fresh regression is needed; prior November13 evidence is historical.
- Needs human/blockers: manual speed/kill-trigger feel. Known world-space flares left behind by moving trains are explicitly out of scope as a fix target.

### 65. ZombieHook pull: no snapback, hands/gear clear of surfaces

- Observed: no fresh pull fixture result; ordinary carried loot in C is not hook-pull acceptance.
- Remaining automation: actual network pulls with bat/gold/hand-held bag on ground, moving train and curve; mid-pull drops inside/outside cargo volume and observer floor clearance remain objectively testable if pursued.
- Needs human/blockers: **manual-only** headset hand/arm clearance/comfort and moving-train feel. Historical bare-hand/batch passes remain historical.

## Remaining efficient automation priorities

1. **Public purchase cleanup and original-token return now pass:** S3/P3 are completed, not future work. Independent simultaneous rescue pairs still require a fourth account.
2. **Remaining control/repel/wheel/geometry work:** C3 nonmasterFuel100/latecoal no-replay, R3 moving radio contact/disable and A3 private silence/current gain now pass. Actual wheel/coast and success audio/Effects/ticks now passW2/A4; T3 committed default-window throttle regrip and post-fix edge regression now pass. Menu/down/disconnect interruption scopes remain unrun.
3. **Valid current native survey:** finalb9bbf51d4 build/install and cleanup passL3, but no new survey was run and positional/head tracking still blocks performance. Retry only after tracking readiness; both invalid attempts stay excluded. Full route/natural-ending retained-actor reconnect already pass inJ.
4. **Station upper-flight uncertainty:** final bounded timed trace is inconclusive with actual hand contacts and one consumed jump. Hand-solver cancellation is a strong inference; no geometry defect or redesign follows. Natural traversal remains open.
5. **Only targeted uncovered acceptance:** see [finite audit checklist](remaining-essential-checklist.md). Pure65 awards checks plus failed-run lock/serialization now pass; actual minute-light now passesF60; enemy-hit attribution remains separate. Do not repeat823 geometry assertions, fixed revive suite, reverb lifecycle, contact dispatch, menu sequence, full route or throttleRUN2 merely because old text called them pending.

Persistent prerequisites: legitimate bundle entitlements/Meta checkout, absent fourth account, unsupported nativeLIV DX12, missing Quest positional/head tracking, unsupplied low-fuel clip and pond Water assignment. The last two are explicit queue content/user-assignment prerequisites, not invented production defects. All original human/headset/audio-quality and manual-only requirements stay open; localMPPM does not establish cross-machine jitter. Final recorded32 accepted/1 inconclusive/4 blocked scenario-family counts belong to [rollup](scenario-rollup.md), not whole-entry PASS totals.

The opportunistic public audio observer missed its window; no result is claimed for that attempt. LaterA4 controlled-success preview actually exercises music fade/stop, Effects sweep/restoration and5–1returnticks, without another full journey. G2 static station/Container geometry andW2 coast/moving wheel reconstruction now pass. A5 retrospective4m/s audio windows support bounded cadence/pitch/gain observations; exhaustive smoothed curves remain unproved. Committed throttle released-token correctionb9bbf51d4 now passesT3; remaining interruption scopes are still explicit. Latest-source b9bbf51d4 Android build/install now passesL3; no valid device performance exists.

See [run closure](run-closure.md) for final artifact/install provenance and editor restoration. Successful final build/install does not change the native tracking/performance limitation or close unrun objective/human criteria.
