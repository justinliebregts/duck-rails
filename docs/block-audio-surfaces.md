# Block audio surfaces — November 12, 2026

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
