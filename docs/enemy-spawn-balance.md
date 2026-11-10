# Enemy spawn balance — November 8, 2026

## Approved scope

Initial spawning only. No changes to movement, perception, pursuit, attacks,
retreats, navigation, boarding or enemy confinement. Quadrants never become AI
boundaries; enemies can converge naturally after spawning.

Scavenging blocks use the actual departure crew, retained in the route snapshot
alongside resource balancing (not the 0.6 effective-worker value):

- 1–2 players: one per quadrant, four total.
- 3–5 players: two per quadrant, eight total.
- 6–8 players: three per quadrant, twelve total.

Late join/disconnect/master handoff does not change an existing encounter's count.
The starting station stays enemy-free. The finale retains its existing fixed
selection and activation timing; this change does not expand its encounter.

## Implementation and tuning

`Assets/Scripts/Gameplay/RailEnemySpawnBalance.cs` owns the tiers and 15 m spacing.
Quadrants use block-local X < 0 / X >= 0 and Z < length/2 / Z >= length/2.
Seeded selection uses 32 deterministic attempts, selecting quadrants round-robin
and preferring the farthest viable candidates. Choose the attempt with the most
spawns, then the widest minimum spacing. Separation uses horizontal distance even
for different floors. An insufficient pool produces fewer enemies with a warning;
the selector never exceeds a quadrant quota or relaxes spacing.

Enemy types use a seeded permutation spread across quadrants: no repeated type in
one quadrant, and equal totals of the four existing types when the full target is
available. No enemy prefab or behavior code is changed.

`RailRoute` uses this selector only for scavenging blocks. Positive block enemyCount
is now the fixed authoring/finale count; zero still disables enemies. Matchmaking
adds `enemy-quadrants-v1` so clients with different spawn reconstruction cannot mix.

## Requested marker additions

User authorized adding markers only to Convenience Store and Skyscraper after the
initial spacing audit found shortages. Added eight to each: two in each quadrant,
spread through approach and departure areas. Existing interior, tower-floor and
track-level candidates remain, with their transforms and list order unchanged.

- Convenience Store: 19 -> 27 enemy candidates; coal/gold remain 124/101.
- Skyscraper Works: 18 -> 26 enemy candidates; coal/gold remain 150/150.

Both prefab diffs are additions only. Preserved environment transforms, existing
markers, loot arrays and graph references. No geometry, lightmap, navigation bake
or scene edits were needed. Skyscraper remains open in Prefab Mode for inspection.
The user confirmed its earlier dirty stage reflected browsing, and requested no backup.

The repeatable, scope-limited editor pass is `RailEnemyQuadrantPlacement.Run(bool)`:
false previews/restores without saving; true applies once and refuses duplicates.
It validates Environment floor/corner support, 0.9 m-wide clearance, overlap-query
positive control, enemy bounds, and baked ground within 0.8 m before registering
nested marker prefab instances. A generic envelope does not establish clearance
for every enemy type or natural VR acceptance.

See [placement evidence](validation/enemy-spawn-balance/placement.txt) and the
[future-block requirements](spawn-placement-workflow.md#enemy-quadrant-requirements--november-8-2026).
Each requested block passed 800 crew/seed quota checks before saving. Saved-pool
checks also cover all four enabled blocks, spacing, types, deterministic selection
and safe shortfall behavior: 283,202 assertions passed across 100 seeds and every
crew size. An actual saved-prefab eight-player route assembled 48 scavenging enemies,
124 coal and 208 gold, with the finale retaining its separate five enemies. Nested
marker-prefab references were verified after saving. Unity compiled successfully.
The save emitted pre-existing CutePixel missing-glyph warnings for scene text;
no font or text changes were made. Real-client/headset balance and performance remain
pending; editor selection checks do not establish gameplay acceptance.

## Five enemy types — November 10, 2026

`RailHook` joins the seeded type permutation. Counts (4/8/12) and placement are unchanged. With five types, no quadrant repeats a type and each type's total differs by at most one (4 spawns use four of five types; the seeded order decides which). Earlier "equal totals of the four existing types" no longer applies; the checks assert the new rule.
