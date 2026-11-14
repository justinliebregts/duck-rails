# Container Yard shared-atlas source

November 14, 2026. Full-block render-only conversion of the current saved
`Assets/DuckRails/Blocks/container-yard-v1.prefab`, including existing local edits.
The user subsequently approved the review; optimized visuals are integrated into
the original-GUID production prefab. Bake scene and all other original dependencies
remain unchanged. [Integration evidence](../../docs/validation/container-yard-atlas/production-integration.md).

- Editable file: `ContainerYard-FullMap-Atlas.blend`, scene
  `ContainerYard full map - atlas review`. New scene contains 246 optimized objects
  and 246 hidden original-material reference objects. Earlier Blender scenes remain
  intact; `Blender-before-conversion.blend` is a recovery copy of the connected file.
- Snapshot: `original.prefab`, `original-bake.unity`, their metadata copies,
  `dependency-sha256.tsv`, `inventory.txt`, `source.json`, `exclusions.txt`.
- Build: `export-source.cs.txt` runs through Unity MCP; `build_atlas.py` runs in
  Blender. Both refuse to overwrite existing source/review work. These scripts are
  specific to this block, adapted from the Motel/Skyscraper implementation.
- Import: `Assets/Editor/RailContainerYardAtlasReview.cs`, menu **Duck Rails >
  Authoring > Container Yard > Create isolated full-map Blender review**.
  Existing import opens through **Open full-map Blender review**.

1,191 compatible source renderers / 17 opaque URP Lit materials become 246 chunks
sharing one atlas material. Three original CutePixel labels stay in Unity unchanged;
total enabled renderers fall from 1,194 to 249 (79.1%). No animated brushes,
textured emission, normal/property maps or runtime scenery switch exist in this
block. All selected materials are non-emissive; the Unity material therefore uses
no emission keyword/map. The generated black emission PNG is retained as source
output only, not imported. Other blocks' emissive atlases/animation are untouched.

Native-resolution tiles packed into 1024-square maps with four-pixel padding,
Point sampling, original base tint/texture transforms and linear surface data.
Base-color mips enabled; constant metallic/smoothness map mips disabled. Unity
metallic is red, smoothness alpha. Review textures remain uncompressed.

12 x 6 x 12 m spatial cells, split by source lightmap and renderer settings;
11 oversized sources remain separate. Two original lightmap identities retained.
Repeat-boundary clipping preserves geometry and texture repetition; original
lightmap scale/offset is folded into UV2. No decimation or collision replacement.
Triangles 108,336 -> 202,488 (+86.9%); 381,523 exported vertices. Maximum per-part
surface-area relative error 1.14e-8. Unity geometry is exported explicitly after
Blender's axis conversion, using stored source normals and root-relative positions.

Review prefab/scene: `Assets/DuckRails/ContainerYardAtlasReview/`. Shared material,
two textures and mesh container moved with GUIDs intact into production folder
`Assets/DuckRails/ContainerYardAtlas/`. Both prefabs use those assets.
Visual approval and production integration complete; runtime/headset acceptance
remains pending. No Play Mode, APK or device benchmark was run. Renderer savings are not a
measured device-performance or memory saving. See
[validation](../../docs/validation/container-yard-atlas/README.md).

`pre-integration.prefab` and its metadata copy retain the exact pre-integration
production state. All original object IDs and disabled renderers remain in place.
`production-state.json` records GUID/fingerprint for the build guard. Integrated
visuals reject the old lighting Prepare/Export/Bake Open workflow: restore original
rendering/bindings, reconcile the bake scene and regenerate the atlas before further
render-authoring changes. See the integration evidence for rollback details.
