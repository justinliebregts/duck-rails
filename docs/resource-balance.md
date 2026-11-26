# Resource balance tuning

## Implemented model — November 8, 2026

The user approved both the two-block tank and the 0.6 participation allowance,
including gold. Client/API source changes are implemented; deployment and real-client,
headset and natural-run acceptance remain separate. Enemy scaling is implemented
separately in [enemy spawn balance](enemy-spawn-balance.md).

Tune the client constants in `Assets/Scripts/Gameplay/RailResourceBalance.cs`.
Changing the 100-coin gold budget also requires updating the scaled-credit formula
in `duck-rails-web/api/gofiber/service/duckz/economy.go` and deploying that API;
changing worker/count tuning must keep denominator values within its 10–100 bounds.
Normal routes currently have four scavenging blocks followed by a loot-free finale.

### Crew scaling

For actual crew N (clamped to 1–8), effective workers E = 1 + (N - 1) * 0.6.
Solo counts as 1 worker; four players as 2.8; eight players as 5.2. This is a
predictable allowance for driving, defense, rescue and uneven participation, not
individual AFK/productivity detection. Organized crews will find it easier.

N starts at departure: private runs use connected players when Start is accepted;
public room creation uses the transferring roster, including members still loading.
The route snapshot retains this departure N (flashlights stay one per departing player).

November 24 (Justin, supersedes the earlier frozen crew): N follows the live crew
mid-run, both up and down. The master (`RailExpedition.UpdateCrew`) commits the
connected player count to `rails.resourceCrew` once it has held for 5 s (increase)
or 20 s (decrease, so brief reconnects do not churn loot), with `rails.crewRun`
marking the run as rescaled. `RailExpedition.ReconcileCrew` then adds or removes
coal and gold in scavenging blocks the train has not reached; blocks already
reached, the midpoint and the finale keep their counts. Removal touches only
untouched loot still at its spawn point; spawns and removals wait while any player
is within 35 m. The route holds full-crew (8) selections; the pool shuffle makes
each crew's selection a prefix, so markers and positions never move. Coal value
uses the current N at deposit time, so coal already aboard is worth less after a
crew increase (accepted). Host handoff does not change N.

### Fuel

- Capacity: 100 fuel, approximately two standard blocks.
- Consumption: 50 fuel per 160 metres (0.3125/metre), no idle consumption.
- Starting fuel: 50, covering the station's remaining half-block plus half of the
  first scavenging block. This supersedes the earlier 25-fuel start.
- November 24 (Justin): three times the coal at a third of the value, so players find
  coal more often; total fuel per block is unchanged and gold is unchanged.
- November 26 (Justin, the game was slightly too hard): coal value is 50 / (12 * E),
  plus 10%, rounded up to a whole number. Fuel added and the cockpit gauge show no
  decimals (the gauge rounds the burning tank up, so 0 means empty).
- Rounding up makes the real increase differ by crew size:

  | Crew | 1 | 2 | 3 | 4 | 5 | 6 | 7 | 8 |
  |---|---|---|---|---|---|---|---|---|
  | Fuel per chunk | 5 | 3 | 3 | 2 | 2 | 2 | 1 | 1 |
  | Over old value | +20% | +15% | +58% | +34% | +63% | +92% | +10% | +25% |
  | Chunks for 50 fuel | 10 | 17 | 17 | 25 | 25 | 25 | 50 | 50 |
  | Spawned fuel per block | 90 | 84 | 117 | 100 | 122 | 144 | 82 | 93 |

- Spawn ceiling: floor(18 * E) chunks per scavenging block
  (solo 18, duo 28, three 39, four 50, eight 93).
- Capacity: every normal-run block holds the eight-player 93: Container Yard (168),
  Convenience Store (126), Farm (105), Roadside Motel (96 after the November 24 pass,
  [checks](validation/motel-leisure/coal-capacity-checks.txt)) and Skyscraper (202).
  Every future placement pass must keep this ([rule](spawn-placement-workflow.md#eight-player-capacity-rule--every-pass)).
- Physical coal stockpiling aboard/in bags is explicitly allowed. Surplus can earn
  skipped stops; no mandatory-stop rule. Excess fuel beyond the tank cap is discarded
  on insertion, retaining the existing consumption behavior.

The original 20/block and 30 spawned fuel/block discussion was rescaled to 50/75
(superseded by the November 26 values above).
Starting fuel was subsequently increased to 50 to reach the first encounter's middle.
The 100-point tank is capacity, not a full-run budget.
The current normal tracks are 160 m, except Farm at approximately 160.246 m. Distance
consumption includes braking; exhaustion retains the existing short coast (up to 4 m
at cruise). Therefore two blocks is an approximate range, not a hard spatial barrier.

Starting fuel covers 160 powered metres: the station's remaining 80 m plus roughly
80 m into the first encounter. Existing exhaustion coast reaches beyond the midpoint
even on the slightly curved Farm track. Players can stop earlier to scavenge.
Natural headset pacing still needs review; no starting loot or geometry was added.
Include the pre-finish finale approach in the budget. The committed successful
victory ride retains its sufficient-fuel grant.

### Gold

- Spawn ceiling G = floor(10 * E) bars per scavenging block.
- Each eligible recipient earns an average of 100 / G coins per bar.
- Eight players: 52 bars, approximately 1.923 coins/bar/recipient.
- Maximum recovered gold pays 100 coins per eligible player per block, or 400 across
  four scavenging blocks, before wallet-cap effects. Missing candidate capacity or
  uncollected gold reduces that amount; remaining bars are not revalued upward.
- Shop eligibility, failure, wallet-cap and purchase rules are retained. November 10 removes the train safe, so gold pays only through shop deposits.

The integer wallet uses rational carry: eligible bar k credits
floor(k * 100 / G) - floor((k - 1) * 100 / G). This avoids rounding every bar to the
same integer. Carry continues across shop sales for each recipient. Shops count only
that recipient's eligible sales. A
partial run can leave less than one coin unpaid; fractions do not persist into the
next run. Capped rewards retain existing discard behavior.

Scaled requests use `POST /api/v1/coins/deposit/scaled` with one bar, denominator G
and eligible index k. The API computes the integer credit, applies existing caps and
persists the same per-recipient receipt deduplication. Legacy pending rewards retain
the old endpoint/contract. The new client keeps scaled rewards queued on HTTP 404
until the matching API is available; it never falls back to the old 100-coin payout.

This remains the existing client-authorized reward architecture, not server-side
Photon simulation. The server validates bounded inputs and computes coin amounts;
it cannot independently prove loot collection or the room roster.

### Spawn authoring and tuning

Authored candidate pools are unchanged. Scavenging routes select the global
crew-scaled counts from those pools; existing block coalCount/goldCount = 0 still
disables that resource. Positive values remain fixed-count authoring previews and
legacy/finale counts; runtime scavenging counts come from RailResourceBalance.
Enemy counts and selection now follow the separately approved quadrant model.
Undersized enabled resource pools emit a warning
and use available distinct locations, without increasing item values.

November 24: all five normal blocks have capacity for the eight-player maxima (93 coal/52 gold):
Container Yard 168/142, Convenience Store 126/109, Farm 105/117, Roadside Motel 96/64 and
Skyscraper 202/192 usable coal/gold candidates. The finale has zero available coal/gold candidates.
Other catalogue blocks may need expanded pools before enabling normal runs at
these budgets. No placement pass was performed in this change.

### Deployment and validation

The updated API in `X:/Work/duck-rails-web` must be deployed with/before the client.
No SQL migration is required. November 8 publication: game commit `f4bd82b28` and
backend commit `6c50e16` were pushed to their respective GitHub `main` branches.
Backend checks passed again before building from the clean committed checkout.
The project's TCR script successfully built and pushed
`uswccr.ccs.tencentyun.com/gr-tcr/duckrails-api-golang:api-6c50e16` with registry digest
`sha256:e1fc456a84b41ea4c11e398896164e83bf468a4b8e097af481b3b9c07adb09a9`.
TKE rollout and live API/client verification remain pending; TCR publication is not
a production rollout. Restart clients on the new resource/enemy matchmaking suffix;
old and new balance clients must not share rooms.

Passed: Unity compilation in the verified `X:/Work/duck-rails` editor; focused
resource checks across crew sizes 1–8, seeded selection, pool capacity, serialized
crew state, distance/idle fuel and persistent reward parsing; existing session,
economy and cockpit gauge checks. API service/repository/route tests pass, including
four-block payouts for every crew size, duplicate receipts and malformed requests.
See `Assets/Editor/RailResourceBalanceChecks.cs` for the repeatable focused suite.

Focused suite: 2,739 assertions; session: 40; economy: 53; assembled-route suite:
430, including an eight-player normal route. Expanded explicit test routes emitted
expected gold-pool shortfall warnings for disabled catalogue locations; all assertions
completed. The MCP wrapper reports those warnings as an error despite the completed
suites; this is not a claim that those disabled locations meet the new loot budget.
Cockpit evidence is in `docs/validation/yard-mule/cockpit-checks.txt`; route evidence
is in `docs/validation/test-block-order/editor-checks.txt`.

Still required: deployed API/client integration, actual-client late join/reconnect/
host migration and shop credit, physical coal intake, full-run feasibility,
first-coal reachability, eight-player item performance and natural headset pacing.
Record arrival/departure fuel, coal recovered/left, hauling time, gold recovered,
skipped stops and failures before adjusting balance. Editor checks do not establish
multiplayer or balance acceptance.

## Separate follow-up — enemy population

Enemy spawning now uses separately approved actual-crew tiers and quadrant spacing;
see [enemy spawn balance](enemy-spawn-balance.md). It does not use the 0.6 worker
factor and does not change enemy behavior.
