Remote LIV camera
=================

Ported from Duckz 35ea28d9250f82a70e18dcd34e7ffd5a2ad5aed4 (see docs/duckz-ports.md).

The owner uses the existing working CameraTablet and LIV SDK. Other players see
Assets/Resources/RemoteLIVCamera.prefab: one mesh, 80 triangles, one shared URP
material and a 128x128 atlas. It has no recording cameras, UI, colliders, lights,
LIV behaviours or shadows. Orange casing/handles match the real tablet, with a
dark screen and large camera icon. Handle geometry matches the actual tablet.

Networking and visibility
-------------------------
PhotonPlayer attaches PhotonLIVCamera to every avatar. TabletGrab.Local supplies
the owner's tablet. Reliable snapshots handle transitions and late joining.
Moving poses use Photon event 120 at up to 15 Hz with a reusable 73-byte packet
(Duckz uses 112, which Rails already uses for the pickup refresh). A reliable
final pose settles placement, with no ongoing stationary motion.
Held props use hand-relative offsets and the existing avatar hand stream.
Rails addition: an unheld tablet inside the train while its owner rides is sent
train-local (mode 4) and rebuilt in each peer's synced train frame, so it does not
trail the moving train; one left outside the train stays a world pose.
Sequence, ownership and spawn-lifetime checks reject stale or unrelated states.
Closing hides the visual; disabling/destroying its avatar destroys the visual.
Inactive owners hide it. Abrupt disconnection cleanup follows Photon detection.
The Photon AppVersion is not hand-edited in Rails (it changes every build).

RemoteLIVCamera applies a per-view 108m radial cutoff using squared distance.
Either stereo eye may retain a prop at the boundary; nested views restore their
parent visibility. Ordinary frustum/depth tests still apply.

Authoring and permanent checks
-----------------------------
Editable source: Tools/LIVCamera/RemoteLIVCamera.blend. Game exports live only in
Assets/Models/LIVCamera. Preserve manual edits and existing importer settings.
build_model.py is a guarded generator: it refuses existing outputs unless
-- --replace is deliberately supplied. Do not use replacement over user edits
without explicit authorization. The FBX and prefab's baked mesh are separate;
changing only the FBX does not update the existing baked mesh.

Unity menu Tools > DuckZ > LIV Camera:
- Create Missing Remote Assets creates only absent assets, preserving existing ones.
- Validate Remote Camera runs isolated checks: wire format, ownership, ordering,
  train-local mode, event-code uniqueness, handle bounds, visual-only budget,
  distance and nested-view restoration.
- Render Imported Preview renders the imported mesh without saving an open scene.

Source (Duckz) noted a known non-blocking LIV SDK disposal NullReferenceException
on final application quit. Rails validation status lives in docs/duckz-ports.md.
