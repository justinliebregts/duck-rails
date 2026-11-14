# Convenience Store atlas authoring evidence

November 14, 2026; Unity 6000.3.24f1, verified `X:/Work/duck-rails/Assets`, main,
Edit Mode only. Blender 5.3.0. Initial conversion was isolated from production.
The user subsequently approved the visuals and requested integration;
[production integration/recovery evidence](production-integration.md) records the
original-GUID production installation and remaining runtime checks.

- [Saved-prefab preservation](import-checks.txt): 3,208 transforms, 5,852 serialized
  components and 1,420 colliders unchanged; original 1,272 lighting bindings retained.
- [Exact geometry and exceptions](detail-checks.txt): all 285 payload meshes match
  imported positions/normals/UV0/UV2/indices; valid normals/indices; all 1,557 scene
  lightmap bindings correct; source gameplay/fonts/brushes/neon retained.
- [Dependency hashes](dependency-checks.txt): all 1,378 original dependency files
  and metadata unchanged, including source prefab/bake scene and Unity-resolved
  package files. Eight new asset GUIDs are unique across `Assets/**/*.meta`.
- Importer compiles; final Unity Console Error/Exception/Assert query is empty.
  No diagnostic reports were read. The inspection scripts' transient unreadable-
  texture/type-lookup issues were corrected; no production asset changes resulted.
- [Conversion inputs, counts, exclusions and recovery](../../../ArtSource/ConvenienceStoreAtlas/README.md).

## Visual inspection

[Comparison measurements](visual-comparison.txt) and reproducible
[capture command](capture-review.cs.txt). Every pair uses the same camera, scene,
original retained exceptions and lightmaps, toggling only source/atlas renderers.
Fog is temporarily disabled; only the whole-block overview adds a temporary fill
light. All temporary capture objects/settings are removed/restored afterward.
The review scene is saved and clean, with baked lighting enabled and only Scene
View fog hidden for a readable view.

- [Whole block](whole-block-atlas.png) / [source](whole-block-source.png): retained
  footprint, outer thirds, railway, court, moved trailer and building silhouettes.
- [Forecourt](forecourt-atlas.png) / [source](forecourt-source.png): cyan gas,
  pink Quack Mart, Q and retained textured emission.
- [Sparse shop](sparse-store-atlas.png) / [source](sparse-store-source.png): brown
  shelf boxes, open aisles, empty cooler shelves and fluorescent illumination.
- [Stockroom](stockroom-atlas.png) / [source](stockroom-source.png): inventory racks,
  texture repetition, ceiling/floor and baked lighting.
- [Arcade](arcade-atlas.png) / [source](arcade-source.png): carpet, cabinets,
  yellow/pink emission and interior surfaces.
- [Wash](wash-atlas.png) / [source](wash-source.png): retained brush geometry,
  passage, green neon and rooftop car. Rotation is not exercised in Edit Mode.
- [Rooftops](rooftops-atlas.png) / [source](rooftops-source.png): food identifiers,
  original spot lighting, signage and foliage silhouettes.

No obvious new visual defect was observed in these fixed views. Mean RGB byte
difference is 0.086–0.207 / 255; 0.01–0.65% of pixels differ by more than eight
average RGB byte values. Small atlas/minification/rasterization differences remain;
these figures are not a headset perceptual guarantee. Existing dark roofs/outer
areas are also dark in the source. The Unity copy preserves the seven labels;
Blender's reference view omits TMP and displays brushes at their saved pose.

No Play Mode, build, device test or multiplayer test. User visual approval and
production integration are complete. Runtime/headset checks are queued in the
**Convenience Store production atlas integration** playtest entry; no routine Quest A/B.
