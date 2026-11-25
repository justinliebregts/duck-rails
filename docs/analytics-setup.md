# Duck Rails analytics

Updated November 24, 2026.

## Destination and setup

The inherited GameAnalytics settings were byte-for-byte identical to Duckz and named Duck Z. Justin created a separate Duck Rails game and entered its keys. Both saved keys were verified to differ from the source project; credentials are not reproduced here. The original Duckz project is unchanged.

`Assets/Resources/GameAnalytics/Settings.asset` currently configures **Android only**. Add a WindowsPlayer platform entry with the appropriate Duck Rails Windows game's keys before expecting telemetry from DesktopPlayer standalone builds. An unconfigured build platform logs one explanation and disables analytics. Never fill a missing platform with Duckz credentials.

The runtime uses the platform Game Key and Secret Key, not the inspector's display name or numeric dashboard game ID. Use **Window > GameAnalytics > Settings** to maintain platform entries. Restart Play Mode or the app after changing configuration.

The resource currency `coins` and item types `gold` / `shop` are registered in code just before SDK initialization (`DuckzAnalytics.EnsureSdk`), so the settings asset does not need them.

## Code layout

- `Assets/Scripts/Analytics/DuckzAnalytics.cs`: SDK wrapper (class name kept for inherited call sites). Session, purchase and IAP events, plus generic `Event`, `Progression` and `CoinFlow` helpers. Event parts are lower case, `[a-z0-9_-]`, at most 32 characters.
- `Assets/Scripts/Analytics/RailAnalytics.cs`: expedition events. `RailExpedition.Update` calls `Observe(State)` every frame aboard; events come from committed `RailRunState` changes, not from local guesses.
- **`run:start`, `run:launch` and the `run:end` / `run:result` family are claimed in the committed run state** (`RailRunState.TrackCrew`): the master writes its actor number into the same revision-checked commit as the event, and only that client sends it. A handoff, rejoin or app restart cannot send a run twice. If the claiming client loses power in the moment between commit and send, that one event is lost rather than duplicated.
- **Other run-level events (block, train, bridge) are sent by the current master.** A master handoff adopts the current bridge/fuel state without re-sending it; the block being crossed at handoff reports a shortened time.
- **Personal events are sent by each client about its own participant.** A client that joins only during the ending reports nothing for that run; a rejoin while already down is not a new down.

## Event catalog

### Expedition (Tier 1)

| Event | Sender | Value | Notes |
|---|---|---|---|
| Progression `Start` `expedition:<public\|private>` | each client | — | When the client first sees Running for the run (late joiners included). |
| Progression `Complete` / `Fail` `expedition:<kind>` | each client | score = gold deposited by that player | Team outcome. Fields: coal, gold, gold_picked, downs, revives, revived, repels, spent. |
| `run:start:<kind>` | claiming master | players in the run | One per run. |
| `run:end:<success\|fail>:<reason>` | claiming master | run seconds | Reason: `bridge_crossed`, `bridge_collision`, `crew_down`. Fields: kind, crew (everyone who took part), launch_crew, end_crew, peak_crew, late_joins, escaped, coal, gold, unbanked, downs, revives, fuel (last running value), block. |
| `run:last_block:<block>` | master | — | Where the train was when the run ended: furthest point reached. |
| `block:time:<block>` | master | seconds | Train time in each block, sent on leaving it and at the end. |
| `train:fuel_out:<block>` | master | run seconds | Fuel reached 0; re-arms after refuelling above 0.5. |
| `bridge:start` | master | run seconds | First hold on the bridge button. |
| `bridge:lowered` | master | real seconds from first hold | Includes pauses. |
| `down:<cause>:<block>` | downed client | run seconds | Cause: `zombie`, `bomber`, `smasher`, `spider`, `hook`, `hazard`, `other`, `unknown`. |
| `revive:done` | revived client | seconds spent down | |
| `down:unrevived` | client | — | Still down when the run ended. |
| `player:<escaped\|dnf>` | each client | run seconds | Same fields as progression. |
| `award:<title>` | each client | — | Superlative received. |

Block names: `station`, `midpoint`, `bridge`, or the block id without its `-vN` suffix (e.g. `convenience-store`).

### Party size and joins (November 24)

Custom fields are not browsable in the standard GameAnalytics dashboard, so party size and outcome are parts of the event ID. `<kind>` is `public`, `private` or `test`; `test` is any editor or development build, so it never mixes into the win rates. `<size>` is `p1` to `p8`.

| Event | Sender | Value | Notes |
|---|---|---|---|
| `run:join:<kind>:<launch\|late>` | each client, once per run | seconds into the run | `launch` = left with the departing crew; `late` = joined a run in progress (public train button or room code). Leaving and coming back is not a second join. |
| `run:launch:<kind>:<size>` | claiming master | party size | Party size at launch. One per run. Sent once the whole departure has loaded in, or 30 seconds into the run. |
| `run:result:<kind>:<size>:<success\|fail>` | claiming master | run seconds | Outcome by party size **at launch**. One per run. |
| `run:result_end:<kind>:<size>:<success\|fail>` | claiming master | run seconds | The same outcome by party size **at the end** (connected ducks at the finish). One per run. |

How it is decided: the departing crew's user IDs are stored on the room (`rails.launchCrew`) when the public crew transfers or the private host launches. A player on that list who arrives within 30 seconds of the train becoming live is a launch; everyone else is late. Spectators never count. A crew member who never arrives is not counted in the launch size.

Reading it in GameAnalytics (Explore, design events, by event count):

- Joins: compare `run:join:public:launch` with `run:join:public:late` (add `private` for code rooms).
- Party size: the split of `run:launch:public:*`.
- Win rate at launch size 4: `run:result:public:p4:success` / (`run:result:public:p4:success` + `run:result:public:p4:fail`). Use `run:result_end` for size at the end.
- Abandoned runs: `run:launch:<kind>:<size>` minus all `run:result:<kind>:<size>:*`. A run everyone quits has no result and is in neither win rate.

Success means at least one duck escaped. Editor **Simulate success** previews are not reported. Uploads need a configured platform: a run whose claiming master is an unconfigured Windows client is missing its launch and result.

### Economy (Tier 2)

| Event | Sender | Value | Notes |
|---|---|---|---|
| Resource `Source coins` `gold:deposit` | depositing client | coins credited | Only non-idempotent backend credits. |
| Resource `Sink coins` `shop:<item>` | buyer | price | Expedition shops only. |
| `gold:deposit` | depositing client | bars | |
| `gold:picked` | each client, at the end | distinct bars picked up | |
| `gold:unbanked` | master, at the end | bars | Loose bars in the world plus bars in bags. |
| `shop:buy:<departure\|midpoint>:<item>` | buyer | price | Expedition shops (coin items while on the train). |
| `shop:no_coins:<shop>:<item>` / `shop:fail:<shop>:<item>` | buyer | price / — | |
| `store:buy\|unlock\|no_coins\|fail:<item>` | buyer | — | Cosmetics store (and IAP checkout results), kept separate from gameplay coins. |

The one-time 100-coin starting wallet is granted by the backend and is not visible to the client, so it is not reported as a source.

### Session and purchases (unchanged)

- `session:join:<kind>` (value players), `session:join_fail` + error event.
- `rails:session:joined / rejoined / transfer / world_ready / master_changed / stranded_return`: participant observations, with kind, phase, players and client-type fields.
- `rails:phase:<phase>` (each participant) and `rails:disconnect:<cause>`.
- `session:first:<deposit|shop_buy|store_buy|iap>` within the first five minutes after joining.
- `iap:buy:<sku>` + business event, `iap:fail:<sku>`.

Event IDs and fields never contain room codes, run GUIDs, tokens or player IDs.

### Removed November 18

Removed because they no longer fired or no longer meant anything in Rails: `death:*` (Rails downs never reached it), `duck:*` companion events (no companion in Rails scenes), `gold:count` / `gold:alert:*`, `presence:visit/dwell` (duplicated the phase), `session:first:death/duck_act`, and `rails:session:success/failure` (replaced by `run:end` and progression). `AnalyticsArea` / `AnalyticsAreaId` stay only because the legacy Quackville scene still references them; nothing reads them.

## Console versus delivery

The SDK's Unity Editor implementation uses local log stubs and does not upload events. The wrapper bypasses SDK initialization in Editor and shows `[Duck Rails Analytics preview]` lines (design, progression and resource) when **Info Log Editor** is enabled. That flag is off by default to keep the Console quiet. Build informational logging is also off; configured error reporting remains enabled in builds.

Validation status (November 24, party size and joins): 16 Edit Mode checks (Duck Rails > Tests > Run Party Analytics Checks) and a player-define compile pass. `RailPartyAnalyticsLiveChecks.Start()` passed all four two-client runs: public departure of two, solo launch with a mid-run join and rejoin, master handed over mid-run, and the master leaving mid-run; each run produced exactly one start, launch, end and result across both clients ([events](validation/playtest-queue/2026-11-24-party-analytics/verdicts.txt)). These are editor previews; delivery from a Quest build is not verified.

Validation status (November 18): compiles with a clean Console; segment/block-name helpers and the superlative checks pass in Edit Mode. The new events are not yet exercised in Play Mode or on real clients; see the playtest queue. Earlier (October 28) the session events produced local previews in Play Mode. Compilation and local previews establish instrumentation, not delivery. Verify end to end in an actual configured Android build: launch, enter Compound, depart, finish or fail a run, return, then check the Duck Rails game's live/event view. Windows delivery remains pending Windows configuration. Do not infer successful upload from an Editor Console line.

Reference: [GameAnalytics Unity SDK documentation](https://docs.gameanalytics.com/event-tracking-and-integrations/sdks-and-collection-api/game-engine-sdks/unity/).
