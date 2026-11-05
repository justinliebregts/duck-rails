# Local block visibility — November 5, 2026

The user approved showing the local player's current block and the immediately adjoining block on either side, hiding more distant scenery for performance. This changes rendering only. Every selected block, collider, enemy, supply, network object, crawlgraph and baked-lighting companion remains loaded and active for the expedition.

## Implementation

`RailRoute.BuildValidated` installs/rebuilds `RailBlockVisibility` in Play Mode, so existing scenes, rebuilt scenes, normal five-stop expeditions and custom test orders all use the same behavior without prefab/scene edits. Edit Mode authoring stays fully visible.

The ordered window includes StartingStation, the selected scavenging blocks and the bridge finale. The station's child scenery and departure shop fixtures follow the station. The separately positioned midpoint shop fixtures follow their assigned middle block. The finale's bridge/tunnel/far-bank scenery follows the finale, which remains visible beyond the end of the route during the dawn ride.

Visibility follows the local rig's head-centered X/Z position, with the main camera as a fallback when no local player is available. It does not follow the train or remote players. World Z and the authored block starts determine membership; roof height, tunnels, sideways exploration and curved track distance do not change it. Crossing a seam or teleporting updates the window in LateUpdate after locomotion, before normal rendering. At the route ends there are only two available blocks in the window.

The component caches scenery renderers once per route build and updates `Renderer.forceRenderingOff` only when the current block changes. It preserves `Renderer.enabled`, GameObject activity, colliders and behaviours. Pre-existing force-hidden states are restored on return, component disable, route disable or destruction. If no viewer is available, visibility fails open. Route replacement restores the old cache before capturing the new instances.

Renderers belonging to `PhotonView` or `Grabbable` hierarchies are excluded, including movable shop display items. Train, avatars, loose cargo and spawned enemies retain their existing rendering. This is scenery culling, not distance-based simulation or network interest management. Lightmap/probe memory, AI and physics costs remain unchanged. Fog settings and authored geometry are unchanged; there is no extra crossfade.

## Validation

November 5 result: the saved [Play Mode evidence](validation/block-visibility/play-mode.txt) contains 260 passing assertions plus the final pass summary across three builds (five dense blocks, their reverse order, and one block). At the starting station, the five-block rounds suppressed 6,783 / 6,287 additional scenery renderers out of 7,820 cached in the block hierarchies, depending on the adjacent block. These are renderer-state counts, not measured draw-call or frame-time savings.

Unity subsequently crashed during the end-of-session cleanup interval, as reported by the user; successful fixture teardown was already recorded in the evidence file. Console inspection before the crash showed two Meta MRUK global-context initialization errors. The crash cause has not been established. After the user reopened Unity, MCP verified the Duck Rails project, compiled visibility/proof types and the original clean farm bake scene in Edit Mode. No additional Play Mode run was started after the crash.

The reusable `RailBlockVisibilityProof` is an editor-only offline Play Mode fixture using the actual route builder, five dense authored blocks, station, finale and shop prefab. It exercises forward/reverse seam crossings, exact boundaries, large lateral/vertical offsets, distant teleports, dawn overshoot, reversed route rebuild, a one-stop test route, missing viewer recovery, disable/re-enable and teardown. It compares renderer flags, colliders, behaviour enabled states and lighting readiness/payload lifetime. Store gameplay is inactive in the fixture to avoid requiring wallet/login initialization; grabbable store displays are explicitly excluded from scenery assertions.

To repeat: save the current scene; create an empty temporary scene with an inactive GameObject carrying `LoginFlow` (the normal offline sign-in guard) and an active GameObject carrying `RailBlockVisibilityProof`; enter Play Mode. Read `docs/validation/block-visibility/play-mode.txt`, stop Play and reopen the original scene. Do not add the temporary scene to Build Settings.

Headset frame time/GPU improvement and visual pop-in, especially tall skyline views or Bright Lighting with fog removed, still need direct testing. Real-client split-party/backtracking, late join and replay also remain acceptance work. Offline checks do not establish these results.
