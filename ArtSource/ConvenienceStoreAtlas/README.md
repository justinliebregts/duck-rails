# Convenience Store full-block atlas review

November 14, 2026. Conversion, user visual approval and production integration are
complete. Runtime/headset acceptance remains queued. The original production GUID,
objects/gameplay/collision and baked lighting are preserved. Shared atlas assets
now live in `Assets/DuckRails/ConvenienceStoreAtlas`, retaining their GUIDs and review
references. [Integration and recovery](../../docs/validation/convenience-store-atlas/production-integration.md).
This follows the accepted Motel workflow,
without a wing pilot, Play Mode, APK build or Quest comparison.

## Open the result

- Blender: `ConvenienceStore-FullMap-Atlas.blend`, active scene
  `ConvenienceStore full map - atlas review`.
- Unity: **Duck Rails > Authoring > Convenience Store > Open full-map Blender review**,
  or `Assets/DuckRails/ConvenienceStoreAtlasReview/ConvenienceStore-FullAtlas-Review.unity`.
  The separate prefab is alongside it. Baked lighting is bound; Scene View fog is
  hidden for inspection without changing saved environment/fog settings.
- [Evidence and images](../../docs/validation/convenience-store-atlas/README.md).

## Counts and material handling

- 1,139 source renderers / 41 compatible materials become **285 spatial chunks /
  one URP Lit material**. Retained exceptions total 231 renderers, so the whole
  block has **516 enabled MeshRenderers versus 1,370** (62.3% fewer).
- Converted triangles: **50,538 -> 121,970**. Whole-block MeshFilter triangles:
  **55,702 -> 127,134**, excluding TMP-generated text meshes. Final exported
  vertices: **221,119**. Triangle growth comes from clipping repeated UVs, not added
  scenery. Maximum per-part surface-area relative error: **2.334e-8**.
- 12 x 6 x 12 m cells, split by original lightmap identity and renderer settings;
  oversized sources remain separate. No decimation or collision conversion.
- Two 1024 x 1024 Unity textures: sRGB base color with mipmaps, linear metallic-red /
  smoothness-alpha with no mipmaps. Four-pixel tile padding, native source texture
  dimensions, Point/clamp sampling, uncompressed review import. Source tint and
  texture scale/offset are retained. No source emission exists in the selected set;
  the new Unity material has emission disabled. Blender's additional Surface and
  black Emission maps are authoring intermediates, not additional Unity imports.
- The **existing textured HDR neon atlas stays completely unchanged** on 109
  original renderers, including its RGBAHalf linear emission texture and UVs.
  Motel's constant-emission conversion is deliberately not applied to it.
- Other retained exceptions: **91 renderers beneath seven RailWashBrushSpin
  components**, **eight transparent cooler panes**, **seven CutePixel TMP labels**,
  and **16 rooftop-model renderers with incomplete UV0/UV2 data**. Exact paths and
  exclusion reasons are in `exclusions.txt`. No renderer was silently dropped.

## Preservation

The copied prefab retains all original source objects. Only the 1,139 converted
MeshRenderer enabled flags change; the new visual root and 285 lighting bindings
are appended. There was no RailSceneryChunks switch/generated duplicate to remove.
Existing authored combined geometry is treated as source, not rebuilt.

Saved checks retain 3,208 transforms, 5,852 serialized components, all 1,420
colliders, all 1,272 original lightmap bindings, the lighting companion, navigation,
64 lights, four reverb zones and seven CutePixel labels. The pools remain 126 coal /
109 gold / 27 enemies with unchanged per-run configuration. Brush descendant
colliders remain zero; their original stationary parent hulls are untouched.
The user's moved trailer, sparse shop, empty cooler shelving and stocked inventory
room are retained. All 1,378 dependency-file/metadata SHA-256 comparisons passed
for the isolated review; after integration 1,377 still match, with only production
prefab content intentionally changed.

## Blender/source recovery and reproduction

`original.prefab` and `original-bake.unity` snapshot the current saved originals;
`dependency-sha256.tsv` records their dependency contents. `source.json`, `textures/`,
`manifest.json` and `unity-meshes.json` retain the exact conversion inputs/output.
The source objects also remain in the Unity review prefab with their original
meshes/materials and colliders, so conversion is reversible.

`Blender-before-conversion.blend` preserves the previously dirty Blender session locally; this unrelated session backup is not included in the commit.
Existing Motel scenes were not deleted or changed. The new file retains a hidden
original-material collection and an initial Convenience Store draft. The final
scene includes 224 reference-only exception mesh parts; brush animation, collision,
gameplay and the seven TMP labels remain owned by Unity. Blender coordinates are
(Unity X, Unity Z - 80, Unity Y); Unity output returns to block-root coordinates.

Historical review reproduction (restore/reconcile original rendering and bindings
before re-exporting an integrated block; never overwrite later edits):
run `export-source.cs.txt` read-only in verified Duck Rails Unity, then
`build_atlas.py` in Blender. `RailConvenienceStoreAtlasReview.Create()` imports the
explicit Unity mesh payload into one asset with subassets and refuses an existing
review folder or a changed source dependency hash. `export-exceptions.cs.txt` and
`append_exceptions.py` add Blender-only context; they do not replace the Unity
exceptions. Do not regenerate over user review edits.

The builder preserves normalized, interpolated source normals in a vertex
attribute (`Unity source normal`) and exports those directly. Blender corner-normal
recalculation had produced 32 invalid normals on tiny clipped triangles in the first
draft; the final payload/import has zero invalid normals and preserves source
shading. Do not export recalculated corner normals from that draft.

## Remaining work

Seven matching editor source/atlas views show no obvious layout, emission or
lighting regression; the user approved this appearance. Close/moving
minification and seams, reflection/probe behavior, translated runtime lightmap
rebinding, block visibility/re-enable, natural collisions/brush rotation,
and multiplayer/headset acceptance remain **queued for
playtest**. No Convenience Store performance gain, shipping memory reduction or
runtime acceptance is claimed from renderer counts. Routine per-block Quest A/B
is not required by the accepted rollout policy.
