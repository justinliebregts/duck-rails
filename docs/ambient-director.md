# Expedition ambience

## Approved selection — November 16, 2026

The user approved the latest shortlist except Gentle Pine Forest Wind, the fish-shop
refrigeration hum, and Floorboard Creaking Close and Dry. They added three Rural
Expedition recordings (calm cicadas, nighttime leaves, crow), the slowly-stepping
house creak, Spooky Attic, Mystic Spirits, Ghosts Shrieking, Phil Michalski's eerie
industrial ambience, and Horrid Signal. The Cape Town suburban wind was subsequently
explicitly skipped. Crickets must play quietly. Supernatural accents should be rare.

21 approved WAVs downloaded through signed-in Artlist into `Assets/Audio/Ambience`.
On November 17, the 13 background beds were processed with three-second circular
tail/head overlaps; the eight detail source recordings remain unchanged.
`ArtlistSources.json` records each source URL, original download filename, stable
project filename and initial volume. These are the approved individual recordings,
not entire packs. Original WAV masters total about 835 MB on disk. Unity imports long beds as
streaming Vorbis, short details as compressed-in-memory mono, with background
loading and preload disabled. Android and Standalone use 24 kHz, quality 0.55.
Original downloads remain in the user's Downloads folder.

## Runtime behavior and tuning

`RailRoute.BuildValidated` creates/rebuilds `RailAmbientDirector` for normal route
assembly, replay and late join. It uses actual block transforms and the local
AudioManager listener. Settings are in `Assets/Resources/RailAmbienceSettings.asset`.
Open them with **Duck Rails > Audio > Select ambience settings**.

- Seven profiles cover Starting Station, Skyscraper Works, Container Yard, Farm
  Silos, Convenience Store, Roadside Motel and Bridge Finale. The separate midpoint
  helipad has no profile in this pass.
- Each profile has up to two outdoor beds, a detail pool, and ordered local bounds
  for interiors/river regions. The first matching area wins, with 0.4 m boundary
  slack for the current area. Cellar precedes farmhouse. Bounds start from the
  authored reverb placement dimensions and require listening/traversal review.
- Up to two quiet 2D beds crossfade over three seconds when the listener changes
  area. Room beds include the approved attic/industrial textures and electrical
  hums. Existing sound-event volume ranges control loudness, including crickets
  at 0.0153–0.018 linear gain. These are initial tunings.
- Local positional details choose a fresh event after 25–55 seconds of quiet.
  Random origins are roughly 7–15 m away, constrained to the current region.
  House creaks/drips stay inside their eligible area. Existing occlusion/reverb
  applies to details. Crow and foliage are outdoor details.
- Long details now choose a random 8–14 second section with 1.25-second fades in
  and out. Duration and source bounds account for pitch. Clips short enough to fit
  the maximum window play in full with their original attack/tail. These defaults
  are exposed as `detailDuration` and `detailFadeSeconds` in the settings asset.
- Background wrap points use baked three-second complementary cosine crossfades.
  They require no extra runtime voices. Area changes still use the separate
  three-second runtime crossfade. Original downloads are preserved; regenerate
  with `python Tools/bake-ambient-loops.py --source-dir C:/Users/Justin/Downloads`
  using Python 3.11 or newer. Always use originals as input. Source/output hashes,
  durations and exact PCM boundary checks are in
  [loop bake evidence](validation/ambient-director/loop-bakes.json).
- Mystic Spirits and Ghosts Shrieking alternate without immediate repeats, with
  a 150–300 second randomized timer. They share the single detail voice and wait
  for a quiet gap. Area changes do not reset the long scare timer. Starting
  Station suppresses these two accents by initial authoring choice.
- All ambience goes through the existing six-voice ambience budget. Gameplay can
  reclaim those voices. Crossfades/loading count toward the same limit, and bed
  retries are throttled to once per second.
- Playback is local to each client. Nearby players can hear different random
  accents. This is an implementation default for atmosphere, with no networked
  gameplay events or shared enemy cues introduced.
- Unready routes, room transitions, ending/return phases, lost listeners and
  disable/rebuild stop ambience. Ending music takes over after the run finishes.

Edit the SoundEvent assets under `Assets/Audio/Events/Ambience` for volume. Edit the
settings profile arrays/bounds for where sounds play and the interval fields for
frequency. **Install approved ambience defaults** reapplies the recorded starting
asset settings/profiles and overwrites manual profile/event tuning, so use it only
when intentionally resetting that configuration.

## Validation

November 17: Unity compilation and 1,236 excerpt/import EditMode checks passed
([evidence](validation/ambient-director/excerpt-checks.txt)). All 13 baked loops
passed frame-count, overlap-endpoint and adjacent-source-sample wrap checks.
These numerical checks do not establish perceptual seam quality after compression;
full-loop listening, excerpt fades, pause/resume and voice reclamation remain queued.

Unity compilation and 352 focused EditMode checks passed on the verified Duck
Rails editor. All 21 WAVs import with positive sample counts/durations, every
approved recording is referenced, platform settings and quiet gains match,
region selection/hysteresis and repeated setup pass, and 100 rare-event selections
avoid immediate repeats. Console Error/Exception/Assert query returned zero.
See [check output](validation/ambient-director/editor-checks.txt) and the adjacent
re-runnable command. Runtime/headset volume, loop
seams, region transitions, spatial detail origins, split-party/late-join behavior
and device streaming cost are queued for playtest. Source titles and the user's
audition feedback determine selection; this pass does not establish listening
acceptance in the game.
