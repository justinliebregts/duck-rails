# Skyscraper triangle-preserving atlas — November 14, 2026

The user approved the reduced floors and grounded/varied stacks, then requested
another Blender atlas/chunk pass without increasing cube polygon counts. This is
an isolated review of that approved source. The user subsequently accepted the
conversion and it is now integrated into the original-GUID production prefab.
[Integration checks and recovery](production-integration.md).

## Result

- **37,184 input triangles -> 37,184 output triangles.** Every triangle is retained;
  all 2,140 source parts with 12 triangles still contribute exactly 12 triangles.
- **2,789 -> 649 enabled MeshRenderers**, including the unchanged CutePixel sign:
  619 atlas chunks and 29 repeating-material chunks, about 77% fewer renderers.
- One 1024-square atlas material plus four original repeating materials. The atlas
  contains repeated texture pixels at native texel density, rather than cutting
  geometry at UV repeats. 2,759 source parts fit the atlas; 29 oversized-repeat
  parts retain gravel/concrete/crane/scaffold materials and original UV behavior.
- Cells remain 12 x 4 x 12 m, separated by lightmap identity and renderer settings;
  oversized source meshes remain separate. No custom shader or decimation.
- Base color, constant HDR emission and metallic/smoothness are preserved through
  standard URP Lit. Base mips remain enabled; constant property maps have no mips.

## Review assets

- Unity: `Assets/DuckRails/SkyscraperLowPolyAtlasReview/Skyscraper-LowPoly-Atlas-Review.unity`
  and `.prefab`. Menu: **Duck Rails > Authoring > Skyscraper Works > Open low-poly Blender review**.
- Blender: `ArtSource/SkyscraperAtlasLowPoly/Skyscraper-LowPoly-Atlas.blend`;
  active scene `Skyscraper low-poly - atlas review`, with hidden original-material
  reference geometry. Existing Blender scenes/files were preserved.
- Source snapshot, export/build scripts, atlas images, manifest, mesh payload,
  per-part counts and independent `geometry-checks.json` are in the same ArtSource
  folder. Earlier high-poly review assets remain historical recovery material.

## Checks performed

- Independent source/output comparison covers all 37,184 triangles, winding,
  normals, UV2, texture repeat phase/gradients, renderer settings and lightmap
  identities. Maximum position error 0.00000379 m from float conversion; relative
  surface-area error 0.0000000639. No subdivided or omitted triangles.
- [Saved preservation](import-checks.txt): all 3,761 original transforms, 12,375
  serialized components, 3,011 colliders and 2,788 original lightmap bindings match.
  Source renderers are retained disabled in the review; production is unchanged.
- [Imported meshes and lighting](detail-checks.txt): all 648 meshes exactly match
  the payload; all 3,436 bindings work in the reopened review. Keep 439 probes,
  52 original lights, four reverb zones, 202/192/23 candidate pools, original
  navigation and CutePixel text. No missing components.
- [Dependency check](dependency-checks.txt): 304 original asset/metadata hashes
  unchanged. New asset GUIDs are unique. Editor compilation succeeds and final
  Error/Exception/Assert Console is empty.
- Eight matched source/atlas captures include whole block, rail corridor, floor,
  crane, foundation, offices, wall and stack contact. [Pixel comparison](visual-comparison.txt)
  has mean RGB differences of 0.019–0.116 byte values; no visible added seams,
  floating stack layers or lost structure were found in inspected views.

Copied review instances lose Unity's per-light baked output association. As in
the Farm Silos review, the opener restores that association and disables the 52
baked-only lights **in the review scene only**, avoiding realtime preview on top
of baked lighting. Both prefab light configurations stay unchanged. Captures use
the corrected review, with a temporary fill only for the overview.

The conversion stage performed no production integration, geometry/collision
changes, navigation/light bake, Play Mode, APK or device runs. Subsequent approved
integration is recorded above. Runtime/headset behavior is
**queued for playtest**. Renderer reduction is not a measured GPU/frame-time gain.
The 18 known source ground-loot overlaps remain documented separately in the
[material revision evidence](../skyscraper-materials/README.md).
