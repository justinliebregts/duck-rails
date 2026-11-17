# Block audio surfaces — November 12, 2026

## Starting station and compound — November 16, 2026

Tagged the active city starting station and Blender subway compound by material:
concrete/plaster/masonry/asphalt/ceramics/glass use Stone; steel, sheet metal,
fixtures and guard collision use Metal; timber, boards, crates and furniture use
Wood. Compound track ballast uses Dirt. Existing fountain Water, pressure-plate
Metal and security equipment assignments are retained. Disabled legacy art,
merchandise interaction volumes and collider-free scenery are outside this pass.

The Blender meshes combine different materials in single colliders, including
multiple types in the same atlas material. `RailStationAudioSurfaces` partitions
their existing triangles using the authored atlas UV tiles and source-specific
exceptions (olive wall paint versus crates/boards; washroom ceramics). Fifteen
original MeshColliders remain as disabled references; 42 collider-only children
use saved meshes under `Assets/DuckRails/StationAudioCollision`. Visual meshes,
renderers and lighting are unchanged. Do not re-enable the original colliders.
The Compound scene had eight explicit enabled overrides; these were removed.
Expedition needed no such override correction and was not saved.

Saved assets: `StartingStation.prefab`, `StartingStationV3/StartingStationV3.prefab`
and `Compound/SubwayTerminal/SubwayTerminal.prefab`. All 32 compound and 29
starting-station active solid Environment colliders resolve non-default types.
Unity EditMode checks passed exact vertex and oriented triangle preservation,
collider transforms/layers/materials/cooking settings, saved prefab coverage and
loaded Compound inheritance. No original serialized records were removed; scene
changes against the immediate backup are only those eight overrides. Final
Console error/exception query was empty. [Counts](validation/station-audio-surfaces/partitions.txt),
[checks](validation/station-audio-surfaces/editmode.txt).

If the Blender prefabs are rebuilt, rerun **Duck Rails > Authoring > Audio > Tag
station and compound surfaces** and check scene collider overrides again. This
is an authored collision partition, not a runtime atlas lookup. Headset listening,
movement across partition boundaries and remote hand-contact playback remain
**queued for playtest** under “Station and compound material contact sounds”.

## Farm stalk-only foliage — November 15, 2026

Added `Foliage` and `HandHit_Foliage` using the supplied `handHitFoliage.wav`.
Player hand contacts and footsteps select this sound only within 0.6 m of an
authored corn stalk, from ground contact tolerance (0.1 m) to its actual height.
The 52 production corn patches contain 3,669 baked stalk positions, extracted
from their mesh stems. Paths, clearings, pond and the rest of the farm retain
their normal sounds. Corn stays non-solid; this selects existing contact sounds,
not a continuous rustle or a new collision/hand-sweep event. Water takes priority;
sound sets without a foliage entry retain their original surface selection.
The corn authoring tool regenerates coverage when rebuilding corn.

EditMode: all 3,669 stalk-center checks passed, plus five path/clearing/rail/pond
exclusions and above-canopy exclusions. Saved prefab retains 1,434 colliders.
Translation/rotation, both player mappings, outside fallback, minion sound
preservation and clip selection passed with explicitly invoked volume lifecycle
(ordinary MonoBehaviours do not enable in EditMode). Compilation passed.
The explicit lifecycle fixture produced Unity `ShouldRunBehaviour()` assertions
through EditMode `SendMessage`; its sound comparisons passed, but this is not a
clean runtime lifecycle/Console acceptance. Temporary fixture objects were removed.
No Play Mode or listening acceptance was performed. See “Farm stalk-only foliage
sounds” in the playtest queue.

User requested an inferred-material AudioSurface pass on Final Bridge, Farm,
Skyscraper, Helicopter Shop, Convenience Store, Motel and Container Yard.
Applied only to their seven `Assets/DuckRails/Blocks/*-v1.prefab` assets:
`bridge-finale`, `farm-silos`, `skyscraper-works`, `midpoint-helipad`,
`convenience-store`, `roadside-motel`, `container-yard`.

## Material choices

- Stone: concrete, masonry, asphalt, tiled surfaces, roofing and glass.
- Metal: bridge steel, containers, machinery, vehicle bodies, helicopter,
  fixtures, cages, lockers and metal fences.
- Wood: timber floors, pallets, crates/cartons, furniture, tree trunks,
  farm/convenience-store wooden fences, playground decks and trailer decking.
- Grass: pasture, foliage, hay and putting felt. Dirt: construction gravel,
  excavation and aggregate. Sand: loose grain/feed sacks. Water: finale river.
- No dedicated rubber, fabric, glass or plastic surface assets exist. Rubber
  and upholstery use Wood as a dull-contact approximation; glass uses Stone;
  hard rooftop advertising models generally use Metal. These are authoring
  judgments for listening review, not new sound assets or confirmed sound design.
- One collider can only resolve one type. Mixed ground meshes use their dominant
  walkable material (e.g. Grass on the finale's mixed grass/rock far bank).
  Existing WaterVolume contact-point overrides remain intact.

Used renderer materials, object/hierarchy names and relevant authoring code
(including wooden farm fences/trailer deck and metal silo access/workbenches).
Homogeneous subtrees receive parent tags; mixed groups have more specific tags.
Existing non-null surface assignments are retained. Triggers and the helicopter's
17 merchandise/display colliders were not directly tagged. The shared helicopter
source prefab is unchanged; four component additions are local instance overrides.

## EditMode evidence

Unity MCP confirmed `X:/Work/duck-rails/Assets`, Unity 6000.3.24f1, Edit Mode.
Installer compiled and all seven saved prefabs were reloaded successfully.
10,344 environment solid colliders resolve to the planned type; 17 display
colliders are excluded. Added 8,300 AudioSurface components:

- Final Bridge: 1,140 solid colliders, 1,057 new tags.
- Farm: 1,434 solid colliders, 578 new tags.
- Skyscraper: 3,649 solid colliders, 3,476 new tags.
- Helicopter Shop: 155 solid colliders (17 excluded), 134 new tags.
- Convenience Store: 1,420 solid colliders, 1,023 new tags.
- Motel: 1,900 solid colliders, 1,747 new tags.
- Container Yard: 663 solid colliders, 285 new tags.

Serialized record comparison against immediate pre-edit copies found no removed
existing records or unrelated changes. Existing records differ only by component
references, four nested helicopter component overrides and empty-field whitespace.
Transforms, collider geometry/settings, renderers, gameplay fields and baked data
are preserved. No rebake is needed for these audio-only additions.

Player_Footsteps resolves all seven used types to the corresponding non-null
`HandHit_<type>` SoundEvent. No sound sets or runtime audio code changed.
Console query returned one LCK unsupported-graphics-API error, unrelated in content
to surface assignment; the Console was not empty. No Play Mode was launched.

**Queued for playtest:** see “Seven-block material contact sounds” in
`playtest-queue.md`. Material suitability and headset listening are not verified.
