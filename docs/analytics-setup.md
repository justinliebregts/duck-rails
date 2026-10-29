# Duck Rails analytics

Updated October 28, 2026.

## Destination and setup

The inherited GameAnalytics settings were byte-for-byte identical to Duckz and named Duck Z. Justin created a separate Duck Rails game and entered its keys. Both saved keys were verified to differ from the source project; credentials are not reproduced here. The original Duckz project is unchanged.

`Assets/Resources/GameAnalytics/Settings.asset` currently configures **Android only**. Add a WindowsPlayer platform entry with the appropriate Duck Rails Windows game's keys before expecting telemetry from DesktopPlayer standalone builds. An unconfigured build platform logs one explanation and disables analytics. Never fill a missing platform with Duckz credentials.

The runtime uses the platform Game Key and Secret Key, not the inspector's display name or numeric dashboard game ID. Use **Window > GameAnalytics > Settings** to maintain platform entries. Restart Play Mode or the app after changing configuration.

## What is useful now

The wrapper retains its inherited `DuckzAnalytics` class name to preserve existing interaction callers. New session instrumentation records:

- `session:join:<kind>` and `rails:session:joined` / `rejoined` for room entry.
- `rails:session:transfer` and `world_ready` for travel and loading completion.
- `rails:phase:<phase>` for committed phase observations, with `rails:session:success` / `failure` on Ending.
- `rails:disconnect:<cause>` and `rails:session:master_changed` for connection loss and local authority promotion.
- `session:join_fail` plus an error event for rejected requests.
- Presence visit/dwell events by Rails room kind and phase instead of grouping everything under the inherited map's `other` area.

Session events carry kind, phase, player count and editor/development/release client classification. Event IDs and these fields contain no private room codes, run GUIDs, authentication tokens or player IDs. Counts are participant observations, **not unique expedition counts**; the success/failure buttons currently produce prototype test outcomes, not real gameplay completions.

The automatic five-minute gold-count/missing-gold-manager reporting has been removed. Existing death and purchase callers remain, but the full gameplay and purchasing flows are not validated by this change. The inherited purchase amount parser is not authoritative revenue validation.

## Console versus delivery

The SDK's Unity Editor implementation uses local log stubs and does not upload events. The wrapper now bypasses SDK initialization in Editor and can show explicit `[Duck Rails Analytics preview]` lines when **Info Log Editor** is enabled. That flag is off by default to keep the Console quiet. Build informational logging is also off; configured error reporting remains enabled in builds.

Validation: the C# build passed with zero errors. A Unity Play Mode check produced the three expected local previews (session, phase, disconnect), confirmed that no SDK upload component was created, restored quiet logging and stopped Play Mode. Compilation and local event previews establish instrumentation, not successful delivery. Verify end to end in an actual configured Android build: launch, enter Compound, depart, return, then check the new Duck Rails game's live/event view. Windows delivery remains pending Windows configuration. Do not infer successful upload from an Editor Console line.

Reference: [GameAnalytics Unity SDK documentation](https://docs.gameanalytics.com/event-tracking-and-integrations/sdks-and-collection-api/game-engine-sdks/unity/).
