# Railway surface consistency — November 10, 2026

User direction: dull grey metal rails and wooden sleepers in every block, including
Starting Station, the midpoint helipad shop and the complete finale. Reuse atlases.

`RailTrackSurfaceAuthoring` applies this as a surface-only migration to all 18
catalogue prefabs plus Starting Station. It preserves rail dimensions, track samples,
collision, spawn markers and baked navigation. Rails/sleepers remain collider-free.
The existing metro track gauge, grades and separate disused railway are retained.
The drawbridge rails and sleepers remain children of the moving hinge.

Sleepers use 2 m longitudinal centres. Standard segments start 1 m inside their
boundary, including the start, 60 m shop, finale approach and moving bridge.
The victory track continues at 111, 113 … 309 m, maintaining the bridge transition.
The metro and disused line retain their existing 2 m spacing and authored phase.
Curved/sloped segments have slightly greater distance measured along their path.

Rails use the existing Quackville Industrial atlas's neutral riveted metal tile,
with a shared `TrackSurfaces/Dull grey rail.mat` (metallic 0.35, smoothness 0.08).
Sleepers reuse `DepotMaterials/Old timber.mat` and the Town atlas. No new textures
or texture import changes. Metre-scaled UVs tile long rails; generated meshes have
one submesh/material slot and retain a complete secondary lightmap UV channel.
Meshes are shared by surface and size. This is asset reuse, not a measured claim
about Quest frame time or batching.

Reapply through **Duck Rails > Authoring > Track > Apply atlas surfaces and two
metre sleepers** after any legacy whole-block builder regenerates railway geometry.
Save scene edits first. The tool synchronizes the existing bake scenes and placed
Expedition instances, without replacing entire blocks. Reapplication is idempotent.
After applying, rebake/export all blocks and Starting Station, then install finale
lighting using the [lighting workflow](block-lighting.md).

Validation evidence is in `docs/validation/track-surfaces/`: the migration compares
track samples, graph references and all collider shapes/transforms before/after;
saved-prefab checks cover all 19 assets and 3,781 textured railway renderers.
All 19 lighting rebakes/exports completed. `lighting.txt` verifies every track
receiver is bound, texture/renderer references resolve, native lighting/probes exist,
and companion scenes contain no duplicate renderers. Expedition's placed finale
passes with 40 approach, 15 moving bridge and 100 victory sleepers and refreshed
lighting references. `special-segments.txt` covers metro/disused/bridge/exit spacing.

The batch was interrupted by an editor script reload after three exports; the
remaining 15 were resumed with `RailCatalogLightingAuthoring.RebakeFrom`, followed
by the separate Starting Station bake/export and Install Finale Lighting. All
saved exports were then re-opened and verified. No navigation rebake was needed.

Inspected the isolated neutral-light render at `output/track-surfaces/atlas-preview.png`:
dark grey rails and pixel wood sleepers read distinctly. The preview cleanup emitted
a RenderTexture release warning after the image was saved; its temporary scene and
objects were removed. The initial Scrapyard bake scene was restored after validation.
Headset appearance, route seams and device performance remain acceptance work.
