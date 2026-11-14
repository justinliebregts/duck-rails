# Skyscraper low-poly atlas source

Fresh conversion of the user-approved November 14 reduced/grounded source.
**37,184 -> 37,184 triangles; 2,788 scenery renderers -> 648 chunks**, plus the
unchanged CutePixel sign in Unity. User-approved conversion is now integrated into
the production catalogue prefab; the separate review remains available.

Open `Skyscraper-LowPoly-Atlas.blend`, scene `Skyscraper low-poly - atlas review`.
Original-material references are hidden; pre-existing Blender scenes are retained.

`build_atlas.py` repeats texture pixels inside padded 1024-square atlas tiles.
It never clips or subdivides triangles. Parts whose UV spans exceed a tile retain
their original repeating material. This yields 619 atlas chunks and 29 exceptions
using four source materials; standard URP Lit is used on import. Keep texture
density and per-triangle UV gradients. Do not reinstate the old UV clipping loop.

`source.json`, `original.prefab`, `original-bake.unity`, textures and dependency
hashes snapshot approved source geometry, collision/gameplay and baked lighting.
`export-source.cs.txt` runs in the verified Duck Rails Unity editor. It refuses to
overwrite an existing snapshot. `build_atlas.py` also guards existing authoring
output; do not bypass this guard over user edits.

`unity-meshes.json` is the explicit round-trip payload. `part-checks.json` records
every source contribution; `verify_export.py` independently compares all output
triangle corners, winding, normals, lighting UVs and texture phase to the source.
All 2,140 twelve-triangle input parts are preserved without triangle growth.

Shared production assets now reside in `Assets/DuckRails/SkyscraperLowPolyAtlas`
with unchanged GUIDs. `pre-integration.prefab` and metadata preserve original
source state; `production-state.json` is checked by `RailSkyscraperAtlasProduction`.

Import/open via `RailSkyscraperLowPolyAtlasReview`, which creates separate assets,
retains original source objects disabled, and preserves lighting/gameplay data.
See [validation and integration](../../docs/validation/skyscraper-low-poly-atlas/README.md).
