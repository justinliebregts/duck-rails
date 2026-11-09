# Multiplayer regression — November 9, 2026

Status: **completed**. Three four-player private passes (nine expeditions), four
public departure/return rounds, targeted two-client checks, startup recovery,
full catalogue sweep and all 15 final editor suites passed. No unresolved critical
failure was found in the exercised cases. This is not a guarantee for untested paths.

Four distinct authenticated accounts ran in four actual Unity Multiplayer Play Mode
processes. Three reproduced runtime defects were fixed and exercised again.
Evidence is retained under [validation/overnight-regression](validation/overnight-regression/).
This is desktop multiplayer integration evidence, not target-headset acceptance.

## Runtime fixes

1. **Item traffic could exhaust the outgoing transport buffer.**
   Four-player loss testing reproduced `SendBufferFull`; an ordinary private run
   also became unstable. An initial sample recorded about 5 MB received, 5.5 MB
   sent and 577 queued outgoing commands over 24.8 seconds.
   `RailCargo.Awake` now selects PUN UnreliableOnChange, retaining normal moving
   updates and a reliable final rest snapshot. PUN resets the comparison cache
   when another player joins. `PhotonSession.Configure` limits pose events to
   four views plus two header entries instead of eighteen views plus two headers.
   Follow-up runs passed moving cargo, late joins, ownership changes and adverse
   network conditions; ordinary samples were approximately 20–30 KB/s incoming
   with empty queues. Failure evidence: `public-four-first/` and
   `private-transport-failure/`; follow-up: `public-pass1/`,
   `private-mixed-pass2/` and per-role `traffic-*.txt`.

2. **The host could be excluded from success while aboard.**
   Local finish checks used the rig root plus a fixed offset, while remote checks
   used actual avatar heads. `RailExpedition.Position` now consistently uses
   tracked/simulated head position, including ending catch-up checks.
   The reproduction counted only two of three aboard players; the corrected
   bridge crossing counted all three, including a downed passenger, and excluded
   the fourth player left behind. Safe payouts matched that roster.
   Evidence: `private-finish-height/`, `private-pass1/`,
   `private-mixed-pass2/`.

3. **A zero server-clock sample could abort departure.**
   Joining another game server briefly exposed time 0; authority set deadline 30,
   then the first real clock sample made it immediately expire.
   `PhotonSession.TickAuthority` now waits for positive server time before
   creating deadlines and reservation leases. The public fixture also rejects an
   Ending/Returning transition before initial readiness, so an eventual retry
   cannot hide this defect. Exactly four public allocations then completed four
   rounds, including observed zero-clock joins followed by Running.
   Evidence: `public-transitions.txt`, `public-clock-failure/`,
   `public-pass1/`.

## Completed live coverage

### Four-player private lifecycle, repeated

- **Pass 1:** all four completed at 09:16:49 UTC, seed 12345,
  [private-pass1](validation/overnight-regression/private-pass1/).
- **Expanded pass 2:** all four completed at 09:50:41 UTC, seed 54321 with temporary
  normal enemy mix,
  [private-mixed-pass2](validation/overnight-regression/private-mixed-pass2/).
- **Final pass 3:** all four completed at 10:42:30 UTC, seed 67890 with the restored
  spider-only default,
  [private-final-pass3](validation/overnight-regression/private-final-pass3/).

Each pass completed three fresh expeditions in the retained private code:
actual bridge success, all-four-down failure and raised-bridge collision failure,
with automatic clean returns and successful replay.

The expanded pass additionally verified:

- Three players start together; Player4 joins later with matching route, item and
  enemy identities, controllers and poses. Cargo convergence bound 0.3 m; enemies
  0.75 m. Fresh runs have exact authored coal/gold/base enemy counts, and normal
  mix includes all four enemy types. Three/four departing players receive exactly
  three/four free flashlights; late joins and reconnects add none.
- Physical departure-shop and midpoint-shop intake, real backend wallet requests,
  and per-recipient integer remainders. First sale paid each present player four
  coins and the absent fourth none. Second sale paid five to each original
  participant and four to the new participant. Wallets stayed 248/139/148/108
  through leave/rejoin. No retroactive shop reward.
- Safe deposit does not immediately credit wallets. Actual success paid four
  coins to each of the three escaped participants and none to the left-behind
  participant. The earlier pass paid five according to its different remainder.
  Failure awarded nothing; ending catch-up did not change the success roster.
- A non-master picks up/releases coal and the production furnace consumes it
  exactly once; all peers agree on the 5.681818 fuel increment for departure
  crew three, and the consumed object disappears.
- Bag Put, another player's takeover/hold/drop, production BagInventory.TakeOut,
  another player's coal pickup/release, empty-slot retention and extracted-item
  survival after the creator leaves/rejoins. No duplicate Photon IDs.
- Actual enemy KillTrigger contact callback causes a synchronized down/flare.
  Hand rescue interruption resets progress; continuous rescue grants one revive.
  Downing the carrier releases the held coal. A downed passenger escapes aboard.
- Authority handoff and late re-entry preserve run, safe contents, loose cargo,
  enemies, flashlight grants and wallet balances.
- Actual fingertip bridge hold, release/pause, full hold and physical train
  crossing produce the success result. All-down and raised-bridge crash produce
  failure through gameplay. Each return removes expedition objects.

The final pass repeated the expanded sequence and strengthened bridge spawning:
all four observed exactly 37 enemies, including five newly spawned defenders,
with every expected spawn marker exactly once. Identities, controllers and poses
converged across peers. This closes a test gap where the earlier bridge barrier
compared peer identities without asserting the expected defender count. The final
pass also includes an explicit unchanged-wallet assertion on rejoin, supplementing
the before/after values already checked in pass 2.

### Public compound departures and adverse network

All four completed four rounds at 09:35:32 UTC:
[public-pass1](validation/overnight-regression/public-pass1/).

Two rounds departed with two passengers and two shoppers remaining; two rounds
departed with all four. Alternating session success/failure returns covered
occupied and fresh alternative compounds, keeping the crew together and restoring
voice-room routing.

Each round exercised 120 ms latency per direction, 25 ms jitter and 3% loss,
moving passengers, loaded bag contents and carriage-relative bag pose within
0.3 m, sixteen rapid GO/STOP requests and stopped fuel agreement within 0.02.
The initial host experienced 100% loss, disconnect, authority migration and
reserved-slot rejoin with retained route/cargo/voice-room membership.
Public finishes are direct session requests; actual gameplay endings are covered
by the private passes.

### Physical coal and empty-fuel recovery

Both clients completed at 10:04:07 UTC:
[coal-pass](validation/overnight-regression/coal-pass/).

Four actual palm selections/grabs, releases and controlled physics throws through
the front firebox: stopped, moving, after master handoff and after fuel exhaustion.
Each item added fuel once and disappeared on both peers; deliberate duplicate
requests added nothing. Empty fuel rejected GO; physical refueling restored real
movement. Fuel agreement includes burn with 0.08 tolerance.
Quick start retained resource crew one (12.5 fuel per coal); private passes cover
the three-player scaled increment.

### Enemy movement and synchronization

- Two real clients passed minion transport at 10:07 UTC and spider transport at
  10:11 UTC: stationary/moving carriage poses within 0.15 m, moving authority
  handoff, non-owner melee RPC reaction, support loss, moving leave/rejoin and
  ordinary AI left exit/reboarding/right exit. Evidence:
  [minion-network-pass](validation/overnight-regression/minion-network-pass/) and
  [spider-network-pass](validation/overnight-regression/spider-network-pass/).
- Six isolated physics/AI loops passed at 10:12–10:18 UTC: depot rear minion,
  motel minion, both minion side gaps, spider rear, spider surface and spider
  sides/passive stalking. Natural boarding, fast-departure rejection/interruption,
  translation/yaw/pitch carry, deck pursuit, dismount/reboard, melee shove,
  obstruction fallback/all-blocked hold/retry and spider wall/support/feet carry
  through 32 poses passed. Evidence:
  [isolated-enemies](validation/overnight-regression/isolated-enemies/).

Transport fixtures place/freeze an enemy to isolate networking; later exits use
ordinary pursuit. The spider support-loss setup follows a confirmed remote melee
reaction because valid spiders can cling after a hit. These complement isolated
natural AI tests; they are not natural multiplayer combat acceptance.
Temporary enemy overrides were restored to the existing spider-only default
(`ForcedEnemyType = 3`), with no runtime change to `RailEnemySpawns`.

### Startup recovery, full catalogue and editor suites

All three two-client recovery cases passed: host stall before route creation
(10:20:23 UTC), after issuing cargo but before manifest commit (10:22:05 UTC),
and after manifest publication but before first cargo serialization (10:26:19 UTC).
The other editor recovered while the original host was paused; the resumed host
converged on identical inventories, preserving prior identities without duplicate
IDs. Evidence: [startup-recovery](validation/overnight-regression/startup-recovery/).
The complete catalogue sweep passed at 10:29–10:34 UTC in four private-route
batches: all 16 encounters plus station/finale, 48 peer/block observations,
matching loot identities, exact authored counts, lighting/collider/visibility
readiness, current-context host eligibility and no unexpected errors or duplicate
IDs. Evidence: [catalogue-pass](validation/overnight-regression/catalogue-pass/).
All 15 final editor suites passed: session, desktop, quick start, Week2, current
Week3 catalogue, driving, fingertip buttons, hold controls, bridge hold, shared
rig, economy, resource balance, enemy spawn balance, avatar lookup and enemy
boarding graph/packet/authoring. The last includes 489 checks; resource balance
includes 2,740 and spawn balance 283,202 deterministic checks.
Evidence: [final-checks.txt](validation/overnight-regression/final-checks.txt) and
[details](validation/overnight-regression/final-check-details.txt).
The shared-rig suite emits its existing multiple-OVRCameraRig warning while
constructing synthetic rigs; these checks do not establish tracked VR behavior.

An independent saved-evidence comparison confirms identical loot identities
across every peer/block in each catalogue batch and identical nonempty inventories
after all three startup faults:
[evidence-audit.txt](validation/overnight-regression/evidence-audit.txt).

## Fixture corrections and reproducibility

The initial selected editor baseline was 12/13. Week3 asserted obsolete fixed
three-variation art; it now checks the current 16-block catalogue, typed candidate
pools, four-block routes, composed navigation and actual spawn placement.
That migrated suite passed in `catalogue.txt`.

Failed fixture attempts are retained separately from runtime defects:
`public-initial/` used a private-only bag helper;
`private-route-setup/` lacked the intended normal-route override;
`private-join-barrier/` asserted before avatar readiness;
`private-coal-convergence/` asserted before network destruction converged;
`private-bag-fixture/` used ordinary player lifetime instead of production bag
extraction; `coal-fixture-old/` aimed through the current firebox's solid ceiling;
`coal-moving-fixture/` did not lead the moving mouth at its 10 Hz update rate.
The corrected coal fixture releases outside the opening, then applies a controlled
throw velocity; gravity, collision and intake remain real. No production bag or
furnace fix was required.

New opt-in tools:

- `RailRegressionLauncher.Start("private", 4)`,
  `.Start("public", 4)` (also three), `.Start("coal", 2)`; `.Stop()` disarms and
  restores selected scenario/quick-start preference. Monitor each peer log;
  private/public/coal completion does not itself stop the launcher.
- To vary the private route seed, call `RailDeepRegressionChecks.ArmTest(seed)`
  immediately after launcher Start, before Play begins.
- Four-client scenario: `Assets/Settings/PlayMode/6 Four Client Regression.asset`.
  Uses saved distinct Player2/3/4 credentials; no credentials are stored in evidence.
- `RailRegressionTraffic` records opt-in counters only, no message bodies.
  Shared test files carry steps/observations; gameplay state travels through Photon
  and the real backend. Logs are overwritten on launch; archive accepted runs.

## Environment, boundaries and remaining acceptance

Verified project: `X:/Work/duck-rails`, Unity 6000.3.24f1, base commit
`09b1b21ef18de977b176c655c53b9cbef4c4dcf8` plus uncommitted changes.
Player4's initial backend 401 was resolved by Justin's allowlist update/redeployment.
Real test accounts received normal gameplay coin awards during these runs.

No diagnostic reports were read or queried. The previously open Test Center was
closed to prevent automatic inbox polling. Source Duckz and backend repositories
were not modified. The user's Player4 project configuration is preserved.

Fixture warps, controlled throws, direct setup damage/contact invocation and
selected session-finish requests are explicitly bounded test inputs.
These passes do not establish headset reach/feel, physical tracked VR geometry,
voice audio quality, visual presentation, Quest performance, natural full-run
combat/balance, all simultaneous deposit races, eight-player load or a long soak.
Existing milestone acceptance boxes for those areas remain open.

## Final workspace state

At 10:44 UTC Unity was in Edit Mode with no active or queued test scenario, all
eight fixture controls disarmed, network simulation and optional traffic counters
off, and no unsaved scene changes. Quick start, route and invincibility delegates
were restored to the user's saved settings; the pre-existing invincibility
preference remains enabled. The spider-only playtest default remains 3.
See [cleanup.txt](validation/overnight-regression/cleanup.txt).
The Player4 configuration is preserved alongside the regression tools and evidence.
