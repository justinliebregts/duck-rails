# Duck Rails testing tools

Implemented October 30, 2026. These are development tools, not milestone acceptance for headsets or eight-player play.

Shared-rig update: gameplay now uses DuckPlayer in both Keyboard and VR modes, with F2 switching during Play. See [shared-rig.md](shared-rig.md) for Q/E reach, simultaneous grip and physical panel controls. Quick regression includes the shared-rig checks. After starting the manual sandbox, **Duck Rails > Run Live Shared Rig Checks** exercises actual proximity grabs, fingertip buttons and the physical drive lever. Existing network fixtures use the shared rig but still retain their documented setup shortcuts.

## In-game panel

### Enemy invincibility in Test Center

**Duck Rails > Test Center > Invincible from enemies** protects the local player from enemy contact attacks (including spiders) and bomber blasts in a ready private development expedition. The checkbox saves alongside the block-order settings in `UserSettings/RailTestBlockOrder.asset`, survives editor restarts/Play Mode reloads, and takes effect immediately when toggled during a run. Automated suites ignore it. Other players remain vulnerable; enemies still target and attack normally. This does not revive an already downed player, block environment hazards or bridge failure, or disable the explicit **Down self** test action. It is an editor testing preference, not a production-player option.

November 4 validation: compilation and authenticated solo Play Mode checks passed: saved selection survived startup, public Compound/Edit Mode remained unprotected, actual enemy contact triggers rejected hits, and a triggered bomber explosion beside the player left zero downs. Off/on toggles applied immediately; explicit Down self then committed exactly one down. [Evidence](validation/test-invincibility/live-checks.txt). These are injected-hit desktop checks, not multiplayer/headset acceptance. Console contained the existing Meta XR `ErrorFormFactorUnavailable` startup error; no invincibility exception was observed.

In the Editor or a **Development Build**, press the left controller's existing game-menu input (`ButtonName.Start`, mapped to `OVRInput.RawButton.Start`) to show/hide the panel. In VR it follows the left forearm, with its center 32 cm above the wrist in the viewing direction's up axis and 10 cm toward the eyes, and faces the head. This moves the lower edge clear of the wrist instead of placing the panel across the arm. DuckPlayer's actual fingertip colliders press the buttons; no controller ray or grip is required. Headset comfort and reach still need validation.

In **Keyboard mode**, **P** opens a centered, scrollable screen GUI with mouse buttons for reporting, status, network simulation and test shortcuts. Mouse look, movement, grabbing and throttle input are suppressed while it is open. **P**, **Esc** or **Close** hides it; click back into the game to resume mouse look. F2 switching chooses the corresponding screen or wrist interface. The GUI detects the real DuckPlayer's `SimulatedRigInput`, as well as the legacy desktop fixture. Diagnostic controls remain usable while downed or while world loading blocks ordinary gameplay input; the multiplayer simulation is not paused.

- **Report Bug** saves a report ID and up to 60 one-second state samples, recent diagnostic events, current route, build identifier, ownership, bag contents, connection state and reproduction preset. Manual presses have a five-second cooldown.
- **Game Menu** opens the existing room/settings menu. The arm panel does not pause the multiplayer simulation.
- Frame time, ping, room, phase, master, fuel and held-item owner IDs are shown. Optional markers show the actual fingertip contact positions.
- **Test Tools** includes bridge approach, board train, fuel 20, a shared bag containing coal, down self, and repeat route. Shared mutations require the master of a ready private room; local boarding/downing require a private room. Production builds cannot invoke these shortcuts through this API.
- **Simulate Lag** applies 120 ms each way, 25 ms jitter and 3% packet loss on this client. **Normal Network** restores the previous settings. Leaving the allowed private context, destroying the panel or ending a dashboard test also restores owned simulation settings. An existing simulator is never overwritten.

## Automatic reports and visibility

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
2. **Two clients** launches the dedicated `4 Diagnostic Two Client` scenario and the existing coal interaction test. It verifies actual non-master grab/release while stopped, moving and after master handoff.
3. **Three clients — public sessions** runs the stability fixture with public departure/return, a shopper left behind and transport faults.
4. **Three clients — expedition** runs the Week 3 route/shared bag/host handoff/rejoin/replay sequence.

The live buttons require distinct saved accounts in the existing account window. They select the correct scenario, disable competing fixtures, arm the selected fixture, and invoke Unity's scenario launcher. Every expected client's final PASS is required. Missing clients/timeouts/failures cannot yield a pass. On completion or cancellation, the runner disarms fixtures, restores the quick-start preference and previous scenario, and stops the scenario. Results and per-client logs are copied into `Logs/Diagnostics/test-<UTC timestamp>/`; old fixture evidence is backed up there before a new run.

Automated runs isolate and restore any previously selected reproduction preset. The expedition fixture uses a fixed seed chosen to include a curve, eliminating the random no-curve setup failure. Its startup count assertion now allows up to 15 seconds after local world readiness for replicated enemy spawns to arrive, and reports actual counts if they never converge. Cancellation waits for the scenario manager's stopped state before restoring configuration.

Unity 6000.3's scenario manager type is internal. The adapter uses its verified public `ActivePlayModeConfig`, `Start` and `Stop` members; unsupported editor versions produce an explicit error. Setting `EditorApplication.isPlaying` alone does not launch additional clients.

Quick results are stored as timestamped text files. The existing two-client *cleanup-only* fixture is not presented as a new dashboard option: its original two-supply assumptions predate the Week 2 inventory. The current coal and Week 3 fixtures are used instead.

## Reproduction presets

### Test Block Order — November 4, 2026

Open **Duck Rails > Test Center** (the **Duck Rails Tests** window). Enable **Use Test Block Order**, drag scavenging prefabs from `Assets/DuckRails/Blocks` onto the drop area or individual slots, and drag rows to reorder them. The + and - buttons add/remove slots. Launch **Manual sandbox** to play the list from top to bottom. One block is sufficient; the starting station and bridge finale are included automatically.

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

From the Unity project:

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
