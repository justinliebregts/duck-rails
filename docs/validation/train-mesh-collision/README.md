# Train model collision — November 15, 2026

User authorized replacing the misaligned blockout train collision with collision
derived from the current reviewed model. Installed in the existing train prefab;
no Play Mode, multiplayer or headset run was performed.

## Change

- Disable solid colliders under Hull and deck, Side rails, Cockpit shell, Yard
  Mule shell and Blender supplemental collision. Keep the original objects/IDs.
- Three non-convex mesh colliders under `Model shell collision`: structural body
  (1,015 triangles), canopy/spotlight (228), wheels (704). Total: 1,947 triangles.
  Body faces use the reviewed Blender export positions/winding, weld material/UV
  vertex splits and discard three zero-area triangles. Decorative markers,
  furnace bolts and embers do not become contact geometry.
- The dash housing collider uses its current model faces in the module's local
  space. Keep the 15 existing convex furnace wall pieces, open chamber, radio
  table collision and passenger/drive/horn/radio triggers.
- Retain the existing transform-driven passenger system, layer 6 and absence of
  rigidbodies. Do not mark the new shapes static or make the whole shell convex:
  a convex hull would seal the boarding gaps/cavities. Runtime motion and network
  code are unchanged. Moving concave-collider cost still needs Quest measurement.
- `RailTrainMeshCollision.Install` updates collision only. The Blender importer
  calls the same `Apply` method after import; absent obsolete furnace artwork is
  now tolerated. Do not run the whole art importer just to update collision,
  since it also installs its archived art/trim/effect settings.
- Rebaked `TrainCrawlGraph.bytes`: 2,594 nodes. No runtime navigation baking.

## Evidence

Verified Duck Rails Unity MCP at `X:/Work/duck-rails/Assets`, Unity 6000.3.24f1,
Edit Mode. Compilation succeeds and the final error/exception Console query is
empty. Existing compiler deprecation warnings remain.

- [50 focused checks](editmode-checks.txt): exact structural faces, disabled old
  collision, mesh/layer configuration, retained gameplay bindings/triggers, deck
  contacts, rear/both side openings, duck-eye sightline and furnace mouth at
  origin and a translated/yawed/pitched/rolled pose.
- [Preservation checks](preservation.txt): all 378 original serialized prefab
  IDs retained, original gameplay/renderer/filter/light/particle documents
  unchanged, repeated installer leaves the prefab byte-identical. Expedition
  preview inherits the replacement shapes and retains furnace/gauge bindings.
  New asset GUIDs are unique; train prefab GUID is unchanged.
- [489 graph/packet/authoring checks](../enemy-boarding/editor-checks.txt) pass
  after the offline navigation rebuild. The prior editor scene was restored.

Older broad validators are **not counted as passes**: ModularTrainChecks still
expects the removed chair. Temporarily accommodating that exposed its old
24-particle smoke assumption (current saved prefab uses 100); those temporary
test edits were removed. Blender integration's old gauge-trim name and CockpitV2's
old throttle axle pose also disagree with current saved artwork. The relevant
prefab documents are unchanged by this work; this task does not reset them to
satisfy historical assertions. The focused collision checks cover the required
clearances independently.

## Pending

See **Train model-aligned collision** in [the playtest queue](../../playtest-queue.md).
Natural hand/body contact, loose-item impact and retention, moving passenger
support, enemy boarding/surface carry, multiplayer agreement and Quest cost are
unverified. Exact face/raycast checks do not establish live physics acceptance.
