# PC spectator

A private Windows tool for watching live Duck Rails rooms on a monitor: an invisible free camera
with no duck. Ported from the Duckz spectator (see [Duckz ports](duckz-ports.md#pc-spectator--491b6f2-fcdff53-a6e6a40)).

## Confirmed decisions — November 24, 2026

- Spectate compounds, public trains and private rooms (by code), including trains that are already running.
- Train rooms stay at eight. A spectator takes one of the eight slots, so a full train cannot be watched,
  and a spectator in a seven-duck train blocks an eighth duck from joining.
- The spectator never counts as crew (see below) and never runs the room as host.

## Using it

Run `DuckRailsSpectator.exe`. It needs no Meta sign-in: it connects to Photon with a local
`spectator-xxxxxxxx` id. The panel (top right) lists public trains and compounds. Rooms not in the list
can be joined by their five-digit code. Private rooms never appear in the list.

| Key | Action |
| --- | --- |
| Mouse | Look (Esc frees the cursor for the panel; click the view to look again) |
| WASD, Q/E, Shift | Fly, down/up, fast |
| Scroll | Fly speed, or zoom while following |
| F | Follow the current or nearest duck (orbit camera); F or WASD returns to free fly |
| Tab / Shift+Tab | Next/previous duck |
| R | Ride the train: free fly in the train's space (on by default aboard) |
| L | Fog off/on |
| H | Hide/show the panel |

- **Follow departing crews aboard** (compound panel, on by default): when a public crew departs,
  the spectator follows it into the new train room. A private departure keeps the same room, so it
  follows automatically.
- When the crew leaves a room (for example the return to a compound), the spectator goes back to the room list.
- Voice: the spectator hears ducks near the camera. It never records or sends audio.

## Compatibility

- Build the spectator from the **same commit as the Quest build** being watched. The Photon version
  includes `-spectator-v1`, so Quest builds made before the spectator change never share rooms with
  it (and could not leave it out of the crew).
- Photon custom authentication: the spectator sends no backend token. If the Duck Rails Photon app does not
  allow anonymous clients, the spectator is rejected at connect ("authentication rejected" in `Player.log`).
  See [validation](#validation) for what has been observed.

## How peers leave it out (every build)

The spectator sets player property `rails.spectator = true` before joining, so it arrives with the join.
`RailSpectators` filters it out of: run participants and the "everyone down" loss, resupply visibility,
crew size and loot/enemy scaling (`RailExpedition.UpdateCrew`, route crew, private departure crew), the
Final Bridge surge count, bridge and hold-control holders, departure readiness, the compound boarding
roster, the return-crew transfer list, duck colours and the players menu. It has no avatar, so
`PhotonPlayer`-based systems never see it.

It is never a host candidate: `PhotonMasterHandoff.Ready` is false for it, so as master it hands off
at once to a ready duck; it never spawns room objects (`CanSpawnRoomObjects`) or runs session/run
authority. If it stays master for 8 s (no ready duck), it leaves the room.

Locally the spectator deactivates the scene's duck rig before its `Start` (its singletons stay set for
`RailWorld`) and renders through its own camera tagged MainCamera, which block visibility follows.

## Building

- Build window (**Tools > DuckZ > Build > Open Build Window**): switch the platform to Windows, tick
  **Spectator (PC)**, Build. Output: `<output>/duckrails_spectator_<version>_<code>[_dev]/DuckRailsSpectator.exe`.
- Menu: **Tools > DuckZ > Build > Build PC Spectator (Windows)** (uses the window's output folder).
- Batch (switches to Windows if needed; the switch re-imports assets for Windows, and building for Quest
  afterwards switches back):

```powershell
& "C:\Program Files\Unity\Hub\Editor\6000.3.24f1\Editor\Unity.exe" -batchmode -nographics -projectPath C:\duck-rails -buildTarget Win64 -executeMethod DuckzSpectatorBuild.BuildFromCommandLine -spectatorOutput C:\builds\duckrails -logFile spectator-build.log
```

Zip the whole output folder (exe, `_Data` folder and DLLs) to share it.

- The block atlas build guards (`Rail*AtlasProduction`, five blocks) are skipped for the spectator build
  only (`DuckzSpectatorBuild.InProgress`). Their stored fingerprints include asset dependency hashes
  recorded on Android, which differ on Windows. On November 24 all five failed on Windows, including Farm
  Silos, which had no recent changes. Quest builds still run them.

- Editor: the Scene view **Spectator** button runs Play Mode as a spectator (Keyboard/VR switch back).
- Checks: **Duck Rails > Legacy > Run Spectator Checks** (batch: `-executeMethod RailSpectatorChecks.RunBatch`).

## Validation

See the [Duckz ports entry](duckz-ports.md#pc-spectator--491b6f2-fcdff53-a6e6a40) and the
[playtest queue](playtest-queue.md#pc-spectator--queued-for-playtest-november-24-2026).
