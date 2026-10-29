# Duck Rails — Week 1 session prototype

Decisions confirmed October 28, 2026. This is a gray-box implementation, not a completed network acceptance milestone.

## Run it

Open `Assets/Scenes/Login.unity` and press Play. DesktopPlayer is enabled by default in the editor (Duck/Desktop Play preference) and Windows standalone builds. Use an authenticated Meta test account in the editor. Additional desktop editors now use real authentication rather than the inherited local-only identity shortcut; give each concurrent client its own account. Configure additional editors through **Duck Rails > Multiplayer Play Mode Users**; the Two Ducks scenario selects `Player2` for its second editor. See `authentication-setup.md`.

For fast train testing, select **Two Ducks** and press Play. **Duck Rails > Two Ducks Quick Start** is enabled by default: both authenticated editors join the same hidden test expedition and spawn aboard without the public platform countdown. Either editor can finish sign-in first; the other uses normal late-join loading. The main editor reads `RailsQuickStart` from the selected scenario's main-instance configuration, because its runtime tag list can be empty/stale. The additional editor retains the runtime `Player2` tag for account selection and quick start. A random room name is shared through local project-scoped EditorPrefs, so other developers do not share a hard-coded room. It persists while a client remains connected; stopping both clients closes the room. Disable the menu toggle before Play to exercise normal compound boarding. Default solo Play and built players retain normal matchmaking. Finishing a test expedition returns the crew to a compound normally; quick start only applies at initial login.

Login has its own station art and keeps the existing random LoginSpot selection. Normally it authenticates, connects to the US Photon region, joins a twelve-player public compound, then loads Compound as a single scene. Only Login and Compound are enabled in build settings. Neither Main nor Quackville loads.

In Compound: WASD/mouse to move/look, Space to jump, left mouse to grip, Tab to open controls. The compact status banner sits at the top center and omits control hints. The Duck debug panel is available with F8, with no persistent Duck button. Footsteps measure walking relative to the carriage, so standing aboard a moving train is silent. The yellow platform is the public boarding trigger. The first occupant starts twenty seconds; an empty trigger cancels; re-entry restarts twenty seconds. Eight admitted players depart; additional occupants wait for the next departure. The board shows countdown and admission state.

Walk to the private terminal on the left, press Tab, enter five digits, and choose Create or Join. Create never silently joins an occupied code. In private staging, the current Master Client can Start expedition. Anyone can join its code while running if capacity (including reconnect reservations) allows.

The test carriage contains a loose bat and a loaded backpack. The master can use Tab controls to GO, STOP, simulate success or failure, and return the crew. The inherited E-to-spawn-bats debug shortcut is disabled in Rails rooms. Private return preserves the room/code; public return creates a fresh compound. Leaving the last connected player closes a private room immediately.

## Source and state

- `PhotonSession.cs`: connection, filtered compound matchmaking, five-digit private operations, roster freeze, transfer destinations, phases, run IDs, reconnects and timeouts. Preserves issued Photon auth during normal room transfers. Distinct app-version suffix separates the new state protocol from inherited Duckz rooms.
- `RailSessionRules.cs`: capacities, 120-second player TTL, zero empty-room TTL, allowed phase transitions, stable eight-player admission, countdown and deterministic train motion.
- `RailWorld.cs`: Compound/expedition presentation in one scene shell, local loading gate, master validation of platform membership against avatar pose, seeded room-owned supplies, current cargo manifest, desktop terminal and temporary test controls.
- `RailTrain.cs`: actual moving carriage using existing `Helicopter.MoveCabin` passenger support. The existing Photon avatar cabin-relative interpolation also supports the carriage.
- `RailCargo.cs`: train-relative loose-item replication, carry, release/overboard velocity handling. Existing `PhotonGrabbable`, `BagObject`, ownership and room recovery remain responsible for item interactions and bag contents. Recovery checkpoints include the cargo frame.
- `RailSceneBuilder.cs`: repeatable authoring command, **Duck Rails > Build Week 1 Scenes**. Requires saved scenes and Edit Mode. Produces Compound, Login art, materials and RailBat/RailBag prefab variants; original item/player prefabs are reused.
- `RailSessionChecks.cs`: **Duck Rails > Run Session Checks** validates policy, timing, capacity, motion integration and built asset references.

Room properties retain phase/revision, run ID, countdown/roster, transfer destination/users, test block, fuel, train motion and current cargo IDs. Phase commits use compare-and-swap revisions. Crew destination creation uses an unpredictable room name and Photon expected-user reservations; any selected member can create it if the previous coordinator disconnects. Transfer reservations expire after thirty seconds. A failed join displays its cause and returns to the originating public compound if possible, otherwise another compatible compound. Photon cannot distinguish a nonexistent code from one in a different app-version partition; the UI explicitly asks users to check both.

Private creators start as Master Client. Desktop focus changes do not move authority between windows; disconnects still trigger Photon succession. A rejoining owner rebinds the existing avatar, and inactive avatars are never mistaken for the master's local player. Slot recovery uses the authenticated Photon user ID. Bags/equipment are not recreated from local disk saves in Rails rooms. Gameplay supplies are shared and reset for each new run.

## Validation

- Quick-start main-editor fix: the initial implementation depended on `CurrentPlayer.Tags` and the user observed only Player2 entering the expedition. MCP reproduced empty main-editor tags with Two Ducks selected and quick-start eligibility false. Reading the selected scenario's serialized main tag changes eligibility to true in that same editor. All six `RailQuickStartChecks` passed through MCP (empty/stale main tags, other scenarios, Player2, other additional editors and disabled toggle); runtime/editor C# build passed with zero errors and 428 existing warnings. A real authenticated main-editor Play Mode run then entered the expected quick-start room, reached `Running` with `WorldReady` and `OnTrain` true, and activated the expedition environment. That validation used one client and was stopped afterward. Justin subsequently confirmed that the corrected Two Ducks launch works with both editors. This verifies the shortcut, not the remaining Week 1 network acceptance scenarios.
- Unity compiled and executed scene authoring through the installed Unity MCP relay.
- Runtime and editor C# builds passed with zero errors: `dotnet build Assembly-CSharp-Editor.csproj -p:LangVersion=9.0 -v:q`. Existing warnings remain. Pin C# 9 to match Unity rather than the installed SDK latest language overload rules.
- All 17 session regression checks passed in Unity.
- Desktop follow-up: all 10 regression checks passed for tagged-account selection, shared main/virtual-editor preference scope, stationary riding on a translating/rotating carriage, walking aboard and stepping off. Compilation passed with zero errors. The second account still needs to be saved in the new window before real two-client authentication can be verified.
- A real authenticated DesktopPlayer completed Login -> Photon -> Compound with no Quackville scene.
- The live smoke test passed public countdown start/cancel/re-entry, room departure, loaded backpack/bat spawn, acceleration, braking, passenger/cargo carry, deliberate overboard throw, public success return into a fresh compound, private creation/manual launch, failure return with the same code, replay without duplicated supplies, last-player departure and subsequent missing-code rejection.
- Voice followed the final Compound and matched the gameplay app-version partition after repeated transfers. An earlier expanded live pass ended with zero Unity Console errors. The final pass also completed, but the Console retained the failed-test diagnostic described below. This verifies routing, not intelligibility between remote speakers.
- Automated live checks are available under **Duck Rails > Run Live Desktop Smoke Test**, while playing in a public compound with only the local authenticated test player. They deliberately create/leave temporary test rooms. Results are written to ignored `Temp/rails-live-smoke.txt`.

## Observed intermittent issue

One final validation attempt timed out during a fresh private launch and returned to staging. A manual retry, a subsequent full fifteen-stage smoke pass, and ten consecutive fresh private launches all succeeded. The cause is unresolved; do not treat those retries as a fix. Timeout diagnostics now include phase, readiness, revision, run/seed, cargo manifest and editor pause state. Reproduce and resolve this before signing off Week 1.

## Pending acceptance

All Week 1 multi-client acceptance boxes remain unchecked. Exercise seven departing/one remaining, simultaneous code creation, live late entry with moving carriage and changed bag contents, ninth-player rejection, network-loss reconnect, owner/master loss while carrying cargo, voice after transfers, private code lifetime and repeated group returns. Run sustained acceleration/braking, drop/grab/handoff and deliberate overboard trials. Record client count, network conditions and failures. Single-client Play Mode and deterministic checks cannot establish these outcomes.

The carriage and controls are test fixtures. No fuel consumption, authored scavenging gameplay, enemy encounters, polished train controls, shop UI or full results/progression is implied. Headset acceptance remains deferred by the current DesktopPlayer scope.
