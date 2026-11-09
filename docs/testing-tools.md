# Duck Rails testing tools

November 9 spawn-startup investigation: the opt-in `RailSpawnNetworkChecks`
fixture runs two actual editor clients and checks startup, exact networked loot
identities and all-block loading. `Start(-1)` uses the normal quick start;
`RunCatalogue()` visits the complete catalogue in four private-route batches;
`Stop()` cancels and restores the launch setup. It does not query diagnostic
reports. See [fault, evidence and coverage limits](spawn-startup.md).

`RailStartupRecoveryChecks.Start(0)`, `Start(1)` and `Start(2)` separately exercise
host stalls before route selection, before the cargo manifest commit, and before
initial cargo serialization. Run from Edit Mode with both test accounts configured.
The host editor's main loop deliberately blocks for at most 25 seconds while the
second editor recovers; the Photon background connection remains active. The
fixture resumes the host, checks both inventories and stops/restores the scenario.
Evidence goes to `docs/validation/spawn-startup/recovery*-*.txt` and inventory files.
This does not read diagnostic reports. Unity's ordinary Pause button pauses both
MPPM editors and cannot substitute for this isolated-host test.

Implemented October 30, 2026. These are development tools, not milestone acceptance for headsets or eight-player play.

Shared-rig update: gameplay now uses DuckPlayer in both Keyboard and VR modes, with F2 switching during Play. See [shared-rig.md](shared-rig.md) for Q/E reach, simultaneous grip and physical panel controls. Quick regression includes the shared-rig checks. After starting the manual sandbox, **Duck Rails > Legacy > Run Live Shared Rig Checks** exercises actual proximity grabs, fingertip buttons and the physical drive lever. Existing network fixtures use the shared rig but still retain their documented setup shortcuts.

## In-game panel

### VR press withdrawal — November 8

Physical presses now share a finger-clear lock across PressableButton and PauseMenuButton, including diagnostic-to-game-menu and settings-page transitions. A successful poke blocks further physical presses until its contacting fingers withdraw beyond the original button face or sides with 3 cm clearance. Passing through the back does not rearm; hiding/rebuilding the source panel preserves the lock. Desktop click APIs and normal hold/release events remain unchanged.

Unity compilation and 25 VR button checks passed, covering actual DuckPlayer fingertips, focus policy, same-frame repeat blocking, deep penetration, source-panel hiding, return-through-face jitter, and front/side withdrawal. Headset feel and actual page-switch interaction still need user validation. The gate snapshots the pressed button's world pose; moving the panel itself can also clear that original area.

### Simulated coal deposit — November 8

**P > Show test tools > Deposit 1 coal into furnace** (VR: **DEPOSIT 1 COAL**, beside TEST SUCCESS) adds one coal block's crew-scaled fuel through the same fuel update used by physical furnace deposits. Requires the master of a ready private running development expedition. Preserves current train distance, speed and drive target, and caps fuel at 100. This simulates the fuel effect without consuming a physical item or increasing player coal statistics.

Validation: Unity compilation, resource-balance and session regression suites passed in the verified Duck Rails editor. Direct checks verified the single-coal increment and rejection outside a permitted run. Live button presses, multiplayer replication and headset reach remain unverified.

November 7 editor overlay: during Play Mode, **Open test panel / Close test panel** appears at the Game view's top right as an alternative to P. It uses the existing toggle, opening the desktop panel in keyboard mode and the wrist panel in VR mode. If mouse-look has locked the cursor, press Esc to release it before clicking. Hovering the button blocks desktop gameplay input so its click does not recapture the cursor. The button is editor-only. Unity compilation passed; live mouse-click validation remains pending.

### Success lighting preview — November 7

**P > Show test tools > Test success - bridge and dawn ride** (VR: **TEST SUCCESS**) starts the actual shared ending without physically holding the bridge button. Requires the master of a ready private development run. It completes bridge lowering, marks connected participants as test finishers, moves the train to finale distance +75 m, and commits the normal success/results/4 m/s automatic ride/32-second return state together. Normal catch-up puts the crew aboard. The tunnel transition runs 12–20 seconds into that ride. Select **Gameplay Lighting** to see the actual dawn rather than the bright test override. This ends the current run; it is not just a visual sky preview. The VR page adds one row and rises another 6 cm to preserve its lower-edge wrist clearance.

The shared-rig keyboard Space jump now launches at 10 m/s instead of 5 m/s (double vertical launch velocity, not a guarantee of double jump height). Ground-support, downed and blocked-input gates are retained; tracked VR locomotion and the legacy DesktopPlayer fixture are unchanged.

Unity compiled these changes. Shared-rig and Week 2 checks passed, the fog color round-tripped to exactly `060603`, and the success shortcut rejected invocation outside a permitted run. Actual success-button Play Mode flow, jump feel, multiplayer and headset panel reach remain unverified.

### Train speed toggle — November 7

In **P > Show test tools**, click **Train speed: 1x (click for 5x)**; click again to return to normal. The VR tools page has **TRAIN 5X: OFF/ON** beside FLASHLIGHT. Requires the master of a ready private development expedition, like the existing fuel/bridge shortcuts.

The toggle changes normal driving from 4 to 20 m/s using the existing shared motion command and two-second speed ramp. It preserves distance, current speed and remaining fuel when switching; a stopped train stays stopped until driven. Fuel still costs the same per metre. Braking, bridge collisions and player/cargo physics remain active, so brake earlier at 5x. The setting is scoped to the current run, survives master handoff via room state, and starts OFF on the next run. Successful victory travel remains 4 m/s to preserve the tunnel/dawn timing. No global time-scale change.

Unity compilation and existing drive/session checks passed. Direct motion checks verified 4-to-20 and 20-to-4 m/s ramps, fuel continuity, braking and rejection outside a permitted run. The private sandbox launcher did not enter Play Mode; its launch was stopped. Live panel events, real-client/headset comfort and high-speed cargo behavior remain unverified.

### Enemy invincibility in Test Center

**Duck Rails > Test Center > Invincible from enemies** protects all local editor test clients from enemy contact attacks (including spiders) and bomber blasts in every ready running expedition, including the two-client quick-start and automated public/private tests. Set it in the main editor: its saved checkbox in `UserSettings/RailTestBlockOrder.asset` is mirrored through `Temp/rails-player-invincibility.txt` to Player2/Player3, including live on/off changes (polled every quarter-second). Additional editors display the main editor's selection read-only. Enemies still target and attack normally. This does not revive an already downed player, protect unrelated remote players, block environment hazards or bridge failure, or disable the explicit **Down self** test action. It is an editor testing preference, not a production-player option. Other test tools retain their existing private-room restrictions.

November 6 follow-up: removed the private-room and automated-suite exclusions. Unity compilation and the existing desktop checks passed. Two authenticated editor clients in the same public quick-start expedition both recorded ON/OFF/ON from the main editor's setting; each rejected 24 injected contacts through actual active enemy KillTriggers after re-enabling protection, with no down. Evidence: [main](validation/test-invincibility/two-client-main-20261106.txt), [Player2](validation/test-invincibility/two-client-player2-20261106.txt). An initial EditorPrefs mirror propagated startup but failed live changes in Player2; the shared project file fixed the reproduced failure. Validation used a temporary opt-in observer, removed afterward. The existing Meta XR `ErrorFormFactorUnavailable` startup error remained. Full automated-suite reruns, bomber blasts and headset feel were not retested in this follow-up.

November 4 validation: compilation and authenticated solo Play Mode checks passed: saved selection survived startup, public Compound/Edit Mode remained unprotected, actual enemy contact triggers rejected hits, and a triggered bomber explosion beside the player left zero downs. Off/on toggles applied immediately; explicit Down self then committed exactly one down. [Evidence](validation/test-invincibility/live-checks.txt). These are injected-hit desktop checks, not multiplayer/headset acceptance. Console contained the existing Meta XR `ErrorFormFactorUnavailable` startup error; no invincibility exception was observed.

### Lighting preview

**Bright Lighting** and **Gameplay Lighting** are available in the editor **Duck Rails > Test Center** and the in-game panel's **Test Tools** page (keyboard and VR). Choose either mode before launching Manual sandbox, or switch during a private test run. The editor remembers the selection for this editor session; development players start with Gameplay Lighting.

Bright Lighting is a local visibility aid: fog is disabled while rendering the game camera, ambient fill is increased, and temporary shadowless directional/near-camera lights illuminate dark spaces. It only applies in a ready private development room. Gameplay Lighting removes the override immediately and uses the real scene fog, moonlight, baked fixtures and current night/dawn state. Scene settings are restored after each camera render, so a lighting switch cannot restore stale atmosphere from an earlier scene or time of day. Baked data and other clients are unchanged. Bright mode is not representative of gameplay lighting or headset performance.

For the depot: select **Authoring > Depot > Load depot-first review route**, choose lighting in Test Center, then launch **Manual sandbox**. Use **P > Show test tools** on desktop or the left-controller menu's **TEST TOOLS** page in VR to switch in place. The taller VR tools page is raised 10 cm to retain wrist clearance; worn-headset reach remains to be reviewed.

November 4 validation: compilation and 29 diagnostic regression checks passed. An authenticated private depot run verified Bright preselection through Play startup, both built VR button events, fog disabled during bright rendering, restored scene settings after each render, unchanged native lightmap textures/readiness, and preservation of a newer atmosphere state across toggles. Matching images measured mean brightness 0.1844 → 0.7762 → 0.1844 for Gameplay → Bright → Gameplay. [Evidence](validation/test-lighting/live-checks.txt), [bright view](../output/test-lighting/bright.png), [gameplay view](../output/test-lighting/gameplay.png). This verifies local rendering and event wiring; physical headset button presses and comfort remain unverified.

In the Editor or a **Development Build**, press the left controller's existing game-menu input (`ButtonName.Start`, mapped to `OVRInput.RawButton.Start`) to show/hide the panel. In VR it follows the left forearm, with its center 32 cm above the wrist in the viewing direction's up axis and 10 cm toward the eyes, and faces the head. This moves the lower edge clear of the wrist instead of placing the panel across the arm. DuckPlayer's actual fingertip colliders press the buttons; no controller ray or grip is required. Headset comfort and reach still need validation.

In **Keyboard mode**, **P** opens a centered, scrollable screen GUI with mouse buttons for reporting, status, network simulation and test shortcuts. Mouse look, movement, grabbing and throttle input are suppressed while it is open. **P**, **Esc** or **Close** hides it; click back into the game to resume mouse look. F2 switching chooses the corresponding screen or wrist interface. The GUI detects the real DuckPlayer's `SimulatedRigInput`, as well as the legacy desktop fixture. Diagnostic controls remain usable while downed or while world loading blocks ordinary gameplay input; the multiplayer simulation is not paused.

- **Report Bug** saves a report ID and up to 60 one-second state samples, recent diagnostic events, current route, build identifier, ownership, bag contents, connection state and reproduction preset. Manual presses have a five-second cooldown.
- **Game Menu** opens the existing room/settings menu. The arm panel does not pause the multiplayer simulation.
- Frame time, ping, room, phase, master, fuel and held-item owner IDs are shown. Optional markers show the actual fingertip contact positions.
- **Test Tools** includes bridge approach, board train, fuel 20, a shared bag containing coal, down self, and repeat route. Shared mutations require the master of a ready private room; local boarding/downing require a private room. Production builds cannot invoke these shortcuts through this API.
- **Simulate Lag** applies 120 ms each way, 25 ms jitter and 3% packet loss on this client. **Normal Network** restores the previous settings. Leaving the allowed private context, destroying the panel or ending a dashboard test also restores owned simulation settings. An existing simulator is never overwritten.

## Automatic reports and visibility

November 4, 2026: **Test Tools > Spawn flashlight** (VR: **FLASHLIGHT**) spawns the existing shared `RailFlashlight` 0.75 m horizontally ahead of the local camera at eye height. Its loose rigidbody uses gravity, so it drops without being equipped. Like Loaded Bag, this requires the master of a ready private running expedition in the Editor or a Development Build. The VR tools page has an extra row, leaving 4 cm of lower-edge wrist clearance. Source reviewed only; Unity compilation, runtime and multiplayer validation were intentionally skipped at the user's request.

Development clients detect persistent duplicate/missing avatars, held-item owner inconsistencies, cargo that blocks staging cleanup, retained-route disagreement between diagnostic clients, Unity errors and world-loading stalls. Settled checks wait eight seconds after phase/master changes and require five continuous seconds of failure; loading stalls use 90 seconds. These checks are intentionally narrower than complete detection of all networking bugs. Ordinary physics differences are not treated as divergence without time-aligned evidence.

Each condition produces at most one report per room/run/phase/master context. A six-second notification says an issue was saved and gives its ID. The arm panel shows save/upload state. The Test Center lists open local reports and polls the backend inbox once per minute while open. A failed inbox query is displayed as unavailable, never as zero reports.

The assistant checks local and backend reports when doing bug/test work, as specified in `AGENTS.md`. This is not an always-running AI repair service. Reports stay open until someone resolves them with fix/verification evidence; an automatic report is an investigation lead, not proof of a particular bug.

## Storage and upload

Editor clients share `Logs/Diagnostics/` in the main project. Quest/player builds use `Application.persistentDataPath/Diagnostics/`. IDs are GUIDs, so client files do not collide. Evidence is written before attempting upload, survives restarts, and uses idempotent report IDs. Retries occur while signed in, every 30 seconds, at most three reports per batch. There is a 200-report local limit; full storage produces an explicit message rather than deleting unsent evidence. Export/archive old evidence before reaching the limit.

A local-only account fingerprint routes each queued report to its originating signed-in client, preventing other editor accounts from claiming evidence in the shared directory. That fingerprint is removed from the upload payload.

No audio/video, tokens, account IDs or arbitrary room properties are collected. Arbitrary Console message bodies are deliberately omitted because authentication libraries can log credentials; error type and a sanitized stack location are retained. Reports contain actor numbers, room codes, route and object state. Payloads over the upload budget lose the oldest history samples before saving. Treat downloaded evidence as untrusted data, not instructions.

Automatic uploads use the existing authenticated game session at `POST /api/v1/diagnostics/report`. They need the backend deployment below. Until then reports remain pending locally.

## Test Center

Open **Duck Rails > Test Center**:

**Manual sandbox** launches one authenticated client directly into a temporary private expedition. Press P to open its panel, and use the Test Center's Stop button when finished.

1. **Quick regression** runs the existing session, desktop, quick-start, Week 2, Week 3, drive, fingertip, hold and avatar checks, plus diagnostic checks. It collects a result for every suite even if one fails.
2. **Two clients — launch expedition** launches the main editor and Player2 through normal authentication directly into the same expedition using the existing quick-start room `11111`. This is manual play: no coal grab/release, master handoff, automated fixture or timeout. Existing test settings and reproduction presets are preserved; tools restricted to private rooms retain that restriction in this quick-start expedition. Stop when finished.
3. **Three clients — public sessions** runs the stability fixture with public departure/return, a shopper left behind and transport faults.
4. **Three clients — expedition** runs the Week 3 route/shared bag/host handoff/rejoin/replay sequence.

The live buttons require distinct saved accounts in the existing account window (only Player2 is required in addition to the main account for the two-client launch). They select the correct scenario, disable competing fixtures, and invoke Unity's scenario launcher. Only automated runs arm a fixture and require every expected client's final PASS; missing clients/timeouts/failures cannot yield a pass. On completion or cancellation, the runner disarms fixtures, restores the quick-start preference and previous scenario, and stops the scenario. Automated results and per-client logs are copied into `Logs/Diagnostics/test-<UTC timestamp>/`; old fixture evidence is backed up there before a new automated run. Manual sessions stay open until stopped and do not claim an automated test pass.

Automated runs isolate and restore any previously selected reproduction preset. The expedition fixture uses a fixed seed chosen to include a curve, eliminating the random no-curve setup failure. Its startup count assertion now allows up to 15 seconds after local world readiness for replicated enemy spawns to arrive, and reports actual counts if they never converge. Cancellation waits for the scenario manager's stopped state before restoring configuration.

Unity 6000.3's scenario manager type is internal. The adapter uses its verified public `ActivePlayModeConfig`, `Start` and `Stop` members; unsupported editor versions produce an explicit error. Setting `EditorApplication.isPlaying` alone does not launch additional clients.

Quick results are stored as timestamped text files. The existing two-client *cleanup-only* fixture is not presented as a dashboard option: its original two-supply assumptions predate the Week 2 inventory. The coal fixture remains available in source for focused regression work but is no longer run by the two-client button.

## Reproduction presets

November 6 launch validation: Unity compiled the changes; shared-rig, drive and quick-start checks passed. Two actual authenticated editor clients joined run `26569a4c598248e5a3386817bcc3c5f6`, both publishing readiness for `Running`. The main client was world-ready/on-train, `AutomatedRunning` was false and the coal arm file was empty. Stopping returned Unity to Edit Mode with no active runner and the scenario manager `NotRunning`. The Console contained one Meta XR `ErrorFormFactorUnavailable` startup error; desktop launch still completed. No diagnostic reports were queried. This verifies launch convenience, not coal, master-handoff or headset acceptance.

### Test Block Order — November 4, 2026

Open **Duck Rails > Test Center** (the **Duck Rails Tests** window). Enable **Use Test Block Order**, drag scavenging prefabs from `Assets/DuckRails/Blocks` onto the drop area or individual slots, and drag rows to reorder them. The + and - buttons add/remove slots. Launch **Manual sandbox** to play the list from top to bottom. One block is sufficient; the starting station and bridge finale are included automatically. Page Down advances through blocks in a private expedition.

The list and checkbox save automatically to local `UserSettings/RailTestBlockOrder.asset`, surviving Play Mode and editor restarts. Edits affect the next run hosted by this editor, not the route already loaded. Only private development runs use the override; automated suites ignore it. Enabled order takes precedence over reproduction presets. Disable the checkbox to resume preset/random selection, or **Restore normal random routes** to disable it and clear the active preset while keeping the prefab list.

Use distinct, registered scavenging prefabs with baked graphs and spawn variations. Empty slots, duplicates, station/finale assets and missing bakes produce validation warnings; an invalid selection at runtime falls back to a normal random route with a test-status message. Newly authored prefabs must also be registered in Expedition's RailRoute catalogue on all clients. Spawn variations use a fixed test seed. The midpoint shop follows the middle listed block (the sole block for a one-block route). Normal gameplay still selects five distinct blocks. All clients consume the host's existing retained route state, including variable-length test routes; use updated clients together.

Validation: Unity compilation, 59 focused editor checks (1/2/3/5/12-block actual builds, ordering, serialization, supplies, bridge/shop placement, invalid input and normal five-block selection) and 29 diagnostic checks passed. An authenticated solo private sandbox loaded Motel → Campground → Depot in that order after domain reload, with station/bridge and all lighting ready; Photon retained state matched the actual instances. See [editor evidence](validation/test-block-order/editor-checks.txt) and [live evidence](validation/test-block-order/live-checks.txt). Console showed a Meta XR `ErrorFormFactorUnavailable` startup error without a headset; no route exception was observed. Multi-client late joining/host migration and headset acceptance remain unverified. Existing diagnostic reports were left open; the authorized backend read returned 14 open reports after the sandbox-blocked initial query.

**Save Preset** writes `last-preset.txt`; **Repeat Route** retains the current exact block IDs, variations and placements for the next private run. The Test Center can load this file or a downloaded report's JSON payload. A preset without a saved route can use its fixed seed. Invalid/incompatible routes fall back to normal selection with an explicit status message.

Selection persists in `active-preset.txt` across Play Mode/domain reloads. **Restore normal random routes** clears it. Network simulation is applied explicitly per client; simply loading evidence does not degrade a connection. The preset records scenario and role descriptions, not credentials. This reproduces setup conditions, not exact physics or controller motion.

## Backend deployment (Tencent console)

The backend implementation is in the separate `X:\Work\duck-rails-web` repository. Production has not been updated by this work.

1. Apply `api/sql/diagnostic_reports.sql` to **duckrails_tencent_global**. This creates a new diagnostic table; it does not reuse moderation reports or change existing tables.
2. Build/push an API image from the updated backend checkout using the existing Duck Rails build process. Use a unique new image tag so Tencent pulls this source, including the new `router/diagnostics.go` route. Deploy that image to the existing API workload in namespace **duckrails** through the Tencent console.
3. Configure a nonempty `ADMIN_API_TOKEN` in the Duck Rails API's existing secret/environment. Put the matching value in `ADMIN_API_TOKEN=` in the ignored Unity project root `.env.local`, or set `DUCK_RAILS_ADMIN_TOKEN` before launching Unity/the report CLI. Both inbox tools read this local file; no sibling backend checkout is required. Never add this admin token to a Unity asset, game build, tracked file or chat message. Game clients upload with their normal sign-in token.
4. Open Test Center and refresh the inbox. It must return an actual count rather than an unavailable/configuration message. Submit a report from a signed-in Development Build, verify it appears, restart the client to confirm no duplicate on retry, and resolve the test report with a verification note.

Admin API: `GET /diagnostics/admin/reports?status=open&offset=0` returns up to 100 summaries and the total; `GET /diagnostics/admin/reports/<id>` downloads evidence; `POST /diagnostics/admin/reports/<id>/status` takes `status` (`open`/`resolved`) and a required resolution note when resolving. All admin routes require existing admin authorization. Client retries cannot overwrite evidence or reopen a resolved report. The upload endpoint enforces a 1 MiB limit, validates IDs/counts and limits upload request rate.

Check diagnostic reports only when the user explicitly asks; inbox queries and local report inspection are not routine prerequisites for bug investigations, tests, or art work.

When requested, run from the Unity project:

```powershell
./Tools/diagnostic-reports.ps1
./Tools/diagnostic-reports.ps1 -Action Download -Id <32-character-id>
./Tools/diagnostic-reports.ps1 -Action Resolve -Id <32-character-id> -Resolution 'Fix and verification evidence'
./Tools/diagnostic-reports.ps1 -Action Reopen -Id <32-character-id>
```

## Validation and remaining work

- October 31 panel revision: all eleven current quick-regression suites passed, including 29 diagnostic checks (three cover wrist clearance and rotated placement). A live signed-in private sandbox verified the keyboard GUI with actual mouse clicks: Show test tools opened the second page, Fuel 20 changed shared fuel to 20, cursor stayed unlocked, and P closed the GUI and released its input block. Injected grip/trigger/throttle/movement input was suppressed while open. Opaque background was visually inspected in the Game view. The revised VR position still needs a headset comfort/reach check; existing open reports were not resolved based on this unrelated test.

- Unity compilation and all ten quick-regression suites passed: 573 checks (the existing 547 plus 26 diagnostic checks covering storage, redaction, debounce, upload ownership/privacy and actual DuckPlayer fingertip-to-button action).
- A live manual sandbox verified exactly-once report-button capture with 60 history samples, fuel 20, a loaded shared bag (42 to 43 cargo), bridge distance equal to the expected approach, boarding, simulation apply/restore commands, and repeat-route selection/clearing. Panel rendering was inspected and typography corrected. This remains desktop evidence.
- The Test Center launched two authenticated desktop clients, both completed stopped/moving/master-handoff furnace checks, and the runner automatically stopped and restored the prior scenario and quick-start preference. Evidence: `docs/validation/testing-tools/`.
- The final three-client Test Center expedition run passed on all peers: route and curve, loaded bag, coal carrier, master handoff, late rejoin, success, replay, bridge crash and cleanup. The earlier startup-count failure remains recorded separately; the bounded settle fix subsequently passed. Automatic runner failure capture produced an open report, which was resolved after this rerun with the validation reference.
- Backend router tests, including new validation, anonymous rejection, idempotent persistence/owner collision and resolution-evidence checks, passed. Broader pre-existing middleware/repository integration tests require local Redis at port 6380 and could not run in this environment.
- Live deployment smoke check on 2026-10-30 after Justin deployed image `api-cad4ec6`: public `/api/v1/info/all` returned 200; authenticated inbox/database query succeeded; anonymous inbox reads and report submissions returned 401. Unity MCP subsequently connected to this project. A signed-in private sandbox uploaded synthetic report `45d581e93d5e4998b0751a52bc333631`: authenticated backend download verified 32 history samples, 43 snapshot objects, Running phase and a preset; the local receipt was uploaded=true and the local account fingerprint was omitted from the server payload. Admin resolution succeeded, and the open inbox returned zero reports. The synthetic report was also resolved locally and the sandbox stopped. This verifies the desktop upload/download/resolution path; headset acceptance remains separate.
- Quest menu mapping, forearm placement/readability, actual fingertip feel, notification comfort and headset performance require a Development Build playtest. Prefab fingertip checks are not a substitute for headset acceptance.

Login startup now displays individual Meta/account-server stages and elapsed time in Test Center. See [login progress](authentication-setup.md#manual-sandbox-login-progress--november-4-2026).

## Minion train boarding loop (November 6)

Use **Duck Rails > Tests > Enemy Boarding** for the isolated Play Mode loop, graph/packet checks, navigation regression, and actual two-client transport loop. **Stop Network Loop** stops and disarms the two-client fixture and restores the previous scenario/quick-start settings. Save scene edits before isolated checks. Train-only baking is under **Duck Rails > Authoring > Bake Train Enemy Navigation**. See [enemy boarding](enemy-boarding.md) for exact coverage, evidence paths and remaining headset acceptance. These fixtures do not read diagnostic reports.

Track crossings: **Duck Rails > Tests > Track Crossings > Graph And Train Avoidance / Motel Play Loop** verifies cross-rail ground connectivity, train detours, departure and stale-path replanning. Offline rebaking is under **Authoring > Track Crossings**. See [track-crossing.md](track-crossing.md).

November 6 dismount follow-up: the boarding loop now tests voluntary dismount and reboarding; **Run Motel Dismount Loop** also checks blocked exits and the speed gate. The two-client boarding loop temporarily uses the motel/depot/campground route, waits for both ready clients, and verifies a committed voluntary exit. It restores the prior route callback/scenario/quick-start setting. Diagnostic snapshots now include bounded enemy transfer state. Report 4d54ba3766a44180a9169831d4a3ce3c was resolved after motel and two-client evidence; see [enemy boarding](enemy-boarding.md).

## Normal playtest block pool — November 8

Normal expeditions choose four distinct blocks once at departure, randomize their order, and retain IDs, variations and positions in `rails.route` for clients and late joiners. Initially only Skyscraper Works, Container Yard, Farm Silos and Convenience Store are eligible. The sequence is starting station, two selected blocks, midpoint shop, the other two selected blocks, finale. The shop stays on the existing reserved pad at local (-16, 0, 148) in the second block's departure stretch; it is not a separate terrain block. Its visibility follows that second block.

To change eligibility, select a block prefab under `Assets/DuckRails/Blocks`, then toggle **Allowed In Normal Runs** on its root **Rail Block** component. Save the prefab. Keep at least four enabled: fewer is rejected with a configuration error rather than silently adding disabled blocks or duplicates. With more than four enabled, a run picks four without replacement. New prefabs default to disabled. Changes apply to new runs; retained in-progress routes remain unchanged.

Explicit Test Block Order and saved route reproductions can still deliberately select disabled blocks. Disable Test Block Order and clear any reproduction preset when testing normal selection. Seed-only presets use the normal allowed pool. The unfiltered `RailRoute.Choose` remains for explicit legacy catalogue/coverage fixtures; normal gameplay uses `ChooseNormal`.

Validation: Unity compilation and `RailTestBlockOrderChecks.Run` pass, with actual prefab assembly for route lengths 1/2/3/4/5/12, including the four allowed prefabs; shop/finale placement; 100 seeded normal selections; all 24 permutations; catalogue-order independence; retained-state serialization; and rejection of an undersized enabled pool. Evidence: [editor checks](validation/test-block-order/editor-checks.txt). No new real-client, Play Mode or headset acceptance is claimed. Natural pacing, fuel balance and midpoint shopping need the requested playtest.

## Coal and gold placement tool — November 8, 2026

Historical first version: superseded by the pooled coal/gold/enemy workflow below.

Open a block prefab, then **Duck Rails > Authoring > Place Coal and Gold**. Choose Coal or Gold bar, select the spawn variation, and click **Start placing**. Move over an Environment-layer floor collider in Scene view and left-click to place. The tool creates the existing marker component and registers its transform in RailBlock's runtime variation list. A marker alone outside these lists does not spawn loot.

- **Z** undoes; **Shift+Z** redoes while placement is active and Scene view has focus. These use Unity's normal undo history. Ctrl/Cmd+Z also remains available. **Escape** stops placement. Alt orbit and right/middle mouse navigation remain available.
- Cyan coal / yellow gold outlines show the runtime item's initial bounds, including prefab scale and box-collider envelope. The small dot is the actual spawn origin, connected to a floor ring. Items spawn upright, as RailExpedition currently requires, and can settle/move through physics afterward.
- Clearance defaults to 0.05 m above the item's lowest point. Walls, triggers, hidden geometry, disabled colliders and other layers are rejected. Raycasts are restricted to the target block, including Prefab Mode's preview scene.
- Select a variation to author its loot. **Include in every variation** registers the same new marker in every list, so it appears regardless of the chosen run variation. Existing variations are not redistributed. The window shows selected-variation counts and persistent item outlines.
- Save the prefab normally (or use Prefab Mode Auto Save). For scene instances, select/assign a RailBlock; Unity's normal prefab override workflow applies. Placement is disabled in Play Mode.

Validation: Unity compilation succeeded. **Duck Rails > Tests > Loot Placement** passed 12 focused checks in an isolated preview scene: selected/all-variation registration, transformed parent position, coal/gold undo and redo (both objects and references), runtime bounds, preview-scene floor raycast, wall/layer/trigger rejection. The window was opened through MCP. Existing scene-sign CutePixel missing-bullet warnings occurred when opening the window. Physical mouse/keyboard interaction and a user-authored prefab save/reopen remain hands-on validation; no runtime/network behavior was changed or claimed tested.

## Random spawn pools — November 8, 2026

Open a block prefab and **Duck Rails > Authoring > Place Coal, Gold and Enemies**. Choose Coal, Gold or Enemy, set the three **Spawn per block** counts, then **Start placing** and click Environment floors. There is one candidate pool per type; no variation selector. Initial scavenging defaults are **6 coal / 2 gold / 5 enemies**. The existing spider-only playtest override still determines enemy type; this change controls enemy locations and count, not that override.

Each run selects up to the configured count from unique active locations, without replacement. Zero disables a type. If there are fewer candidates than requested, all available candidates spawn and the window shows the shortfall. Null/deleted/disabled candidates are skipped; markers within 1 cm of an existing candidate of the same type count as one location. Both rail sides receive enemies when the requested count is at least two and the pool has candidates on both sides. Selection is otherwise random; there is no guaranteed loot split or minimum separation beyond duplicate-location rejection.

**Z / Shift+Z / Escape**, standard Unity undo/redo, floor snapping and persistent outlines remain. Coal/gold outlines use runtime bounds with adjustable clearance (including slope support). Red enemy outlines represent a generic candidate footprint, not the final randomized enemy mesh. Enemy placement rejects points outside the block's enemy bounds or more than 0.8 m from baked ground navigation; grey preview explains why placement is unavailable. Geometry edits may require rebaking the existing graph before placing enemies there; the tool never bakes navigation at runtime.

Runtime: RailSpawnPool derives independent random streams from the retained run seed, stable block ID and item type. Changing coal counts does not reshuffle enemies or another block. RailRoute consumes these selections for scavenging blocks and the finale; the starting station remains safe and is not sampled. Shared seed reconstruction keeps marker ordering stable for late join/master handoff on identical content. Photon matchmaking now uses the additional `-spawn-pools-v1` suffix so older variation-based clients cannot join these runs. All clients must use the updated build. Old reproduction presets retain their block order/seed but now use pool selection, not their historical variation layouts.

Migration: existing variations are consolidated into `RailBlock.spawnCandidates`, preserving all authored transforms and their positions. The old serialized lists are hidden compatibility data and are ignored after migration. Unmigrated assets/scene overrides transparently merge legacy lists until migrated; the first placement/count edit also migrates with undo. **Consolidate Legacy Spawn Variations** can migrate prefab assets explicitly. This was run across the catalogue and StartingStation. The currently open convenience-store prefab was migrated in place with undo and left unsaved to preserve the user's active editing workflow; save it normally. Other prefab migrations were saved. Legacy procedural builders/checks that explicitly edit the old variation arrays are historical and do not update a migrated pool; use the placement tool and new checks for current authoring.

Validation: Unity compilation, **1,047 pool/placement checks** and **430 route checks** passed. Coverage includes three marker types and undo/redo; preservation of old references; 100 seeded exact-count selections; bilateral enemies; independent item streams; zero/undersupplied pools; duplicate/null/inactive filtering; Prefab Mode-style floor raycasts; all catalogue enemy markers against bounds/baked navigation; and actual assembled prefab routes of 1/2/3/4/5/12 blocks consuming the selected coal/gold/enemy lists. Evidence: [pool checks](validation/spawn-pools/editor-checks.txt) and [route checks](validation/test-block-order/editor-checks.txt). Existing CutePixel missing-glyph warnings arose from scene signs. No target-headset, actual mouse/key, or authenticated multi-client acceptance is claimed by these editor checks; late join/master handoff and balance remain real-client playtest work.

Convenience-store cleanup follow-up (November 8): saved the previously pending pool migration, cleared its old variation lists, and replaced the three `Spawn variation` hierarchy containers with `Coal Spawns`, `Gold Spawns` and `Enemy Spawns`. All 42 original marker transforms/rotations and 300 seeded selections (100 per type) were verified unchanged. Counts remain 6/2/5. This supersedes the unsaved convenience-store caveat above; other blocks retain their hidden legacy data and original grouping.

## Convenience-store prefab marker preview — November 8, 2026

Convenience Store only now uses 42 nested prefab markers: 18 coal, 6 gold and 18 enemy markers. The new reusable authoring assets are `Assets/DuckRails/Authoring/CoalSpawnMarker.prefab`, `GoldSpawnMarker.prefab` and `EnemySpawnMarker.prefab`. Each uses a native selectable Unity GameObject-name label: coal green (`sv_label_3`), gold yellow (`sv_label_4`), enemy red (`sv_label_6`). RailSpawnMarker opts these instances out of the old floating text and duplicate wire gizmos. Existing markers in other blocks keep their previous presentation; the original shared CoalSpawner prefab is unchanged.

The placement tool now instantiates the matching prefab with undo, preserves the prefab connection/position overrides and parents it into the matching `Coal Spawns`, `Gold Spawns` or `Enemy Spawns` group when one exists. Its floor preview uses the same colors. Stop placement (Escape) to select/move an existing label; while painting, left-click intentionally places another spawner. Unity's Gizmos/icons must be enabled to see native labels.

All 202 top-level geometry branches in Convenience Store are now under `Env`, local position/rotation zero and unit scale. All 764 renderers are beneath it. Route entry/exit/stop anchors, crawlgraph settings and spawn groups remain outside. Use the Hierarchy's picking toggle on Env to prevent geometry selection, rather than deactivating/hiding it; Environment-floor raycasts still work with picking disabled.

Validation: compilation and the existing 1,047 pool/placement checks pass with prefab-based placement/undo. Saved-content reload verifies all 42 nested prefab connections and icon assignments, plus Env identity pose. Conversion preserved all surviving transform matrices, 756 lightmap bindings and 300 seeded position selections. Evidence: [convenience preview](validation/spawn-pools/convenience-preview.txt). Native icon assignments are verified; physical mouse picking and visual preference remain user review. No other block asset was changed for this preview.

## Catalogue authoring cleanup — November 8, 2026

After approving Convenience Store, the user authorized the same setup throughout the catalogue. All **18 RailBlock prefabs** are saved with empty legacy variation lists, no `Spawn variation` hierarchy containers, separate Coal/Gold/Enemy Spawns groups, and one `Env` at local position/rotation zero and unit scale. There are **678 native-label prefab spawners** total; this rollout replaced 636 remaining markers and removed 45 variation containers. Coal is green, gold yellow and enemies red. The prefab marker component suppresses the old text/wire overlays; the tool still previews a new spawn while painting. StartingStation has no spawners and remains safe.

All environment renderers, colliders and lights are beneath Env. Block data, route anchors and spawn groups remain outside. The finale's moving bridge is under identity Env with its existing hinge/control references preserved. Scene instances inherit the prefab cleanup; no unrelated Expedition edits were saved as part of this rollout. The active Convenience Store prefab was already clean. Use Env's Hierarchy picking toggle to lock geometry selection; stop placement with Escape before selecting existing spawner labels.

`Duck Rails > Authoring > Clean Up All Block Spawners and Environment` repeats the conversion safely; `Duck Rails > Tests > Validate Clean Block Authoring` reloads every saved prefab and checks empty variations, hierarchy, prefab connections and native icons. Converted assets are not rewritten on later runs. The old compatibility field remains in code for older authoring/builders; no current block uses it. Legacy content-generation scripts that assume ungrouped hierarchy/variation lists must be adapted before rebuilding a block; the current placement, route and lighting validation workflows support Env.

Validation: all 18 saved-prefab checks passed; conversion checked every surviving transform matrix and **5,400 seeded selections** (300 per block, 0.1 mm pose tolerance) along with renderer/collider/light and baked-binding preservation. The first finale comparison rejected harmless float reparenting roundoff before saving; tolerance was aligned with the matrix check. **1,047 pool/placement checks, 430 assembled-route checks, lighting-export validation and bridge-hold regression** pass. The placed Expedition station/finale retain their lighting references, and `bridgeDeck` still references the original hinge under Env. See [per-block conversion evidence](validation/spawn-pools/catalogue-cleanup.txt). A combined MCP conversion/reload call timed out after saving; smaller independent validation calls confirmed completion. Existing scene-sign missing-glyph warnings remain unrelated. This authoring verification does not constitute new headset/multiplayer acceptance.

Dot-icon refinement (November 8): user requested a less cluttered preview. The three spawner prefabs now use native selectable 16-pixel dot icons instead of name-label icons: coal green (`sv_icon_dot3_pix16_gizmo`), gold yellow (`sv_icon_dot4_pix16_gizmo`), enemies red (`sv_icon_dot6_pix16_gizmo`). Full GameObject names remain in the Hierarchy/Inspector. Unity verified all 678 saved catalogue markers and the open Convenience Store Prefab Stage inherit the new icons. No block geometry or spawn data changed; visual preference/picking review remains with the user.
## Full-area navigation — November 8, 2026

User requested expanded enemy bounds and offline rebakes over the full playable area of every block. All 16 scavenging blocks now span Z=0–160; the bridge finale spans Z=0–300. Environment collider bounds determine width and height. The safe starting station remains unchanged. Bake volumes include a sampling margin and preserve the existing 16-cell grid phase required by `RailGraphAssembly`. Empty seed lists retain all islands above the existing 10-node threshold instead of limiting coverage to old spawn locations. Obstacles, surface clearance and missing floors still prevent enemy placement.

Use **Duck Rails → Authoring → Rebake Full Playable Navigation**, or `RailFullAreaNavigation.RunBatch(start, count)` for explicit MCP batches. Save open edits first. The runner restores the prior scenes and Prefab Stage in `finally`; it does not depend on delayed editor callbacks, which stalled while Unity was in the background. It bakes a temporary scene but saves navigation settings through `LoadPrefabContents`, preserving object IDs and nested prefab connections. A failed individual bake restores that prefab and graph.

Saved reload passed for 17 graphs, 880 connected marker prefabs and all 295 enemy positions within bounds and within 0.8 m of ground navigation. All graphs combine successfully (500,530 nodes). Route-order, catalogue-lighting and bridge editor checks passed, including the Expedition scene's bridge-hinge reference. Convenience Store retains its 244 current marker names, poses, icons, component settings and pool order; its original geometry IDs/properties are preserved. An initial temporary-instance save regenerated IDs; these were restored before final verification. No scene or lighting rebake was saved. Evidence: [full-area bake results](validation/full-area-navigation.txt). Expanded-area pursuit, balance and headset/multiplayer performance remain unverified.

