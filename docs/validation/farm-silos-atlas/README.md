# Farm Silos atlas review — November 14, 2026

Implemented on main after the referenced Container Yard integration finished.
Unity 6000.3.24f1 targets X:/Work/duck-rails/Assets; Blender 5.3.0.
The conversion preserved production and local edits. User subsequently approved the result; production now uses the same atlas assets. See [integration and recovery](production-integration.md).

Open **Duck Rails > Authoring > Farm Silos > Open full-map Blender review**.
The isolated prefab/scene are
`Assets/DuckRails/FarmSilosAtlasReview/FarmSilos-FullAtlas-Review.prefab` and
`FarmSilos-FullAtlas-Review.unity`. Editable Blender:
`ArtSource/FarmSilosAtlas/FarmSilos-FullMap-Atlas.blend`.

## Geometry and material adaptation

- 899 compatible baked MeshRenderers / 27 materials become 117 chunks / one shared
  URP Lit atlas material. Retain 54 original probe-lit renderers: 52 corn patches,
  pond water and combined horsetails. Total 953 -> 171 enabled renderers (82.1% fewer).
- Converted triangles 59,234 -> 104,040. Whole block, including exceptions:
  149,978 -> 194,784 (29.9% more). Repeated-UV clipping preserves texture repetition;
  no decimation, silhouettes, collision or layout changes.
- 167,036 exported chunk vertices. Cells 12 x 6 x 12 m, grouped by original
  lightmap and renderer settings; 27 oversized source parts stay separate.
  Maximum per-part surface-area relative error 1.843e-8.
- Native-resolution source textures, tint, scale/offset and interpolated normals/UV2
  retained. Three 1024-square Unity maps: sRGB base with mips, linear HDR emission
  and metallic/smoothness without mips; four-pixel padding, Point filtering,
  uncompressed review assets. Maximum selected emission component is 2.
- Farm has constant warm-lantern and green-white fluorescent emission. It has no
  textured emission atlas, car-wash brushes or TMP text. No property map was dropped.
  Probe-lit exceptions retain their actual original Unity meshes/materials/settings;
  Blender includes a separate reference-only context collection for them.
- Original-material reference collection is hidden in Blender. Existing Blender
  scenes are preserved and a before-conversion backup is retained.

## Passed authoring checks

- [Saved preservation](import-checks.txt): all 2,361 original transforms, 4,503
  serialized components and 1,434 colliders unchanged, except the 899 replaced
  renderer enabled flags. Original hierarchy, collision shapes/references, gameplay,
  navigation, spawn settings, lights, water interaction and six reverb zones retained.
- 105 coal / 117 gold / 22 enemy candidates retained, with unchanged per-run rules.
  All 899 original baked bindings and the lighting companion remain; 117 identity-ST
  chunk bindings appended (1,016 total). No missing components, duplicate render
  roots or stale generated-render switches.
- [Exact mesh/lightmap checks](detail-checks.txt): all 117 saved meshes match payload
  positions, normals, UV0, UV2 and indices. Valid indices, finite unit normals and
  all 1,016 actual scene bindings pass, including after reopening the scene.
- [Dependencies](dependency-checks.txt): all 734 source dependency files/metadata
  unchanged. Six virtual Unity package paths were mapped to their PackageCache
  files for shell hashing. Source prefab/bake snapshots retain the user's edits.
- [GUID check](guid-checks.txt): all nine newly generated asset metadata GUIDs are
  unique across Assets. Compilation succeeds; final error/exception/assert Console
  query is empty. [Reloaded editor state](editor-state.txt): Edit Mode, no bake,
  clean scene, original LightingData and 554 light probes.
- [Twelve visual pairs](visual-comparison.txt): whole block, railway, barn, silo
  grain/ceiling, basement cages, farmhouse interior/attic, pond, corn, emissive fixture
  and distant farm. Mean RGB byte differences 0.017–0.147; at most 0.16% of pixels
  exceed eight average bytes. Inspected [sheet 1](comparison-1.png),
  [sheet 2](comparison-2.png), [sheet 3](comparison-3.png), full-resolution farmhouse
  comparisons and Blender overview. No new missing surfaces or material/lighting
  drift observed in these samples. Fog disabled; overview alone adds temporary fill.

## Review scene lighting adaptation

A copied prefab instance loses baked-light output identity from the original
LightingData. Initial farmhouse comparisons exposed unwanted realtime editor
preview lighting; merely assigning Light.bakingOutput did not survive reopen.
The review scene therefore disables its 41 baked fixtures as scene-only overrides,
retaining their existing lightmaps/probes and emissive scenery. Both source and
review prefab light settings remain identical and enabled.

Unity also resets copied renderer lightmap indices on scene reload. The Farm-only
editor importer checks this specific open review once per second in idle Edit Mode
and restores bindings if needed. It does not operate in Play Mode, during a bake,
on production scenes or on other blocks. Direct scene reopen, all 1,016 bindings,
and paired visual comparisons were rerun successfully after this correction.

The final full-block Scene View is framed with fog off. A clearly labelled EditorOnly
overview fill (selected as review sun) lives outside the block; disable REVIEW ONLY -
overview fill (disable for baked night) for the original dark appearance. This
review aid does not change either prefab, baked lighting data or game atmosphere.

## Remaining work and recovery

User visual approval and production integration are complete; production selects the optimized Farm. See [integration evidence](production-integration.md). Moving-view/headset minification, darkness,
natural traversal/loot reach, translated loading, companion unload/rebind,
visibility, replay and real-client/late-join agreement are **queued for playtest**.
No pilot, Play Mode, APK, Quest capture, navigation bake or lighting bake was run.
Renderer counts do not establish device CPU/GPU or memory gains.

The existing seven roof/hay loot failures recorded in farm-silos-layout.md remain
outside this render-only change; no markers were moved. Existing dark interiors
and farmland are preserved, not relit. Static sampled comparisons are not complete
visual or runtime acceptance.

[ArtSource recovery notes](../../../ArtSource/FarmSilosAtlas/README.md) describe
snapshots/export/build/import helpers. Do not rerun whole-block Farm installers or
overwrite reviewed assets. Later regeneration must preserve original prefab identity and gameplay references; follow the integration recovery notes.
