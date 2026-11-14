# Skyscraper atlas authoring evidence — November 14, 2026

**Later status:** user approved the visuals and retained triangle count. Production
integration is complete, preserving the original prefab GUID/object IDs and source
state. [Integration evidence and recovery](production-integration.md).
The review-stage record below is retained as conversion evidence; only runtime/
headset acceptance remains queued.

Unity 6000.3.24f1, MCP verified `X:/Work/duck-rails/Assets`, main, Edit Mode,
not baking. Blender 5.3.0; existing dirty session backed up before creating a
separate scene. Conversion and isolated import complete; user visual approval,
production integration and runtime/headset acceptance remain pending.

## Result

`Assets/DuckRails/SkyscraperAtlasReview/Skyscraper-FullAtlas-Review.unity` is left
open with the complete optimized block and original baked lighting. Adjacent
review prefab is independent of the production catalogue. Editable Blender and
recovery/export files: [ArtSource](../../../ArtSource/SkyscraperAtlas/README.md).

3,403 compatible source renderers / 13 materials -> 394 spatial chunks / one
shared atlas material. The sole retained renderer is the CutePixel development
sign. Total enabled MeshRenderers: **3,404 -> 395**. No other scenery excluded.
Scenery triangles: **47,996 -> 228,866**; 436,706 output vertices. This is a
renderer reduction with substantial repeated-UV triangle growth, not measured
Skyscraper performance evidence. No decimation or geometric simplification.

## Checks passed

- [Saved preservation](import-checks.txt): 4,507 original transforms, 15,185
  serialized components (only expected Renderer.enabled differences), 3,649
  colliders and 3,403 original baked bindings retained. Reference comparisons
  normalize copied prefab identity while retaining local/external references.
  This includes RailBlock selection settings/navigation references, spawn poses,
  collider shapes/settings, lights, text/font and interactions.
- [Exact mesh and scene checks](detail-checks.txt): all 394 meshes match exported
  positions/normals/UV0/UV2/indices; finite unit normals, valid indices, correct
  lightmap identities and all 3,797 actual scene bindings. Zero missing components.
  Pools stay 270 coal / 270 gold / 26 enemies, with 55 lights and four reverb zones.
  These are candidate pools, not per-run quantities; per-run settings unchanged.
- [Dependency hashes](dependency-checks.txt): all 354 original dependency/metadata
  files unchanged, including source prefab, bake scene, graph and lighting data.
  Hash check runs in Unity to resolve virtual package paths; a shell-only attempt
  could not resolve 14 package paths and was superseded by the Unity check.
- New asset metadata resolves uniquely; GUID inventory is in ArtSource.
  Script compilation successful; final Console Error/Exception/Assert query empty.
  Preservation checker emits only an obsolete InstanceIDToObject API warning.
- Blender per-part surface-area comparison passes (maximum relative error
  6.60e-13). A material-view overview was visually inspected before Unity import.
- [Seven paired captures](visual-comparison.txt), identical cameras and variant
  switching in the review scene only: complete site, railway corridor, tower
  interior, upper crane, foundation, southern offices, side wall. Fog disabled;
  overview alone adds temporary fill. Original baked lighting retained elsewhere.
  Mean RGB-byte differences 0.039–0.214; at most 0.24% of pixels exceed eight
  average RGB bytes. No obvious missing faces, displaced geometry, broad color
  drift, emission bleed or lightmap seams observed in these views.
  [Comparison sheet 1](comparison-1.png), [sheet 2](comparison-2.png).

The source has only constant-color emission, maximum channel 2. No textured
emission atlas, animated brushes, animated parents or physics-driven scenery is
present here. Source material property maps are explicitly rejected by the exporter
rather than flattened. The preserved sign uses `Assets/Fonts/CutePixel SDF.asset`.

## Remaining issues and limits

- User visual review remains open; production Skyscraper is unchanged.
- Large ground, rail and side-wall sources remain individually bounded chunks;
  no new fine culling subdivision is claimed for those oversized meshes.
- Triangle growth is 4.77x. No frame-time, GPU, loading or memory improvement is
  claimed for Skyscraper; retained originals and new meshes coexist for recovery.
- Constant-property textures have no mipmaps to avoid distant emission bleed.
  Base-color padding/mips can still need moving-view/headset inspection. Source
  and atlas captures show minor quantization/rasterization differences.
- Existing dark gaps between worklights remain, especially southern yards and
  the track corridor; this pass does not rebalance illumination.
- Still queued: translated route lighting/rebinding, visibility hide/show,
  late joining, natural tower/shaft/crane traversal, loot settling and headset
  minification. Cheap authoring checks do not close those acceptance requirements.
- No Play Mode, APK build, device run, diagnostic report inspection or network
  backend query. No other block was converted or integrated in this chat.
