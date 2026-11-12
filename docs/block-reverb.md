# Broad block reverb — November 12, 2026

Installed the user-approved broad-zone pass using the existing `Game.Audio.BoxReverbZone`; no runtime code changes. Each prefab has a root `Reverb Zones` group. Select a named box to edit size/fade; its `Reverb settings` child holds the custom audio settings. Helicopter Shop is excluded.

These are initial listening tunings, not audio-quality acceptance. Broad volumes deliberately include some gaps between rooms/containers. Box edges fade in every direction, including vertically; small exterior/roof bleed is expected. The central railway stays outside the volumes except inside the finale tunnel. No acoustic occlusion or room-by-room simulation is added.

## Saved placements

Coordinates and box sizes below are prefab-local metres (X, Y, Z). Decay is seconds. All zones use custom settings derived from built-in presets, with reduced room/wet levels.

### bridge-finale-v1.prefab

- West bunker: center (-22, 1.8, 68), size (17, 3.4, 13); fade 1 m; decay 1.2 s.
- East bunker: center (22, 1.8, 68), size (17, 3.4, 13); fade 1 m; decay 1.2 s.
- Escape tunnel: center (0, 3.4, 140), size (10.8, 6.8, 39); fade 3 m; decay 2.5 s.

### farm-silos-v1.prefab

- Main barn: center (26, 3, 77.5), size (17.5, 6, 22.5); fade 1.5 m; decay 1.1 s.
- Feed barn: center (-25, 1.5, 76), size (17.5, 3, 13.5); fade 1 m; decay 0.7 s.
- Workshop: center (-26, 1.5, 94), size (15.5, 3, 11.5); fade 1 m; decay 0.85 s.
- Farmhouse: center (-27, 6.25, 134), size (17.5, 9.3, 19.5); fade 0.65 m; decay 0.65 s.
- Cellar: center (-27, -0.25, 134), size (17.5, 2.9, 19.5); fade 0.65 m; decay 1.8 s.
- Enterable silo: center (22, 9.7, 98), size (8.5, 4.2, 8.5); fade 1 m; decay 2.2 s.

### skyscraper-works-v1.prefab

- Tower A: center (-25, 9.5, 66), size (24, 19, 22); fade 1.5 m; decay 0.85 s.
- Tower B: center (25, 9.5, 66), size (24, 19, 22); fade 1.5 m; decay 0.85 s.
- Tower C: center (-25, 9.5, 98), size (24, 19, 22); fade 1.5 m; decay 0.85 s.
- Tower D: center (25, 9.5, 98), size (24, 19, 22); fade 1.5 m; decay 0.85 s.

### convenience-store-v1.prefab

- Store and stockroom: center (30, 2, 74), size (19, 4, 23); fade 1 m; decay 0.8 s.
- Arcade: center (-25, 2, 76), size (29, 4, 27); fade 1 m; decay 0.65 s.
- Car wash: center (34, 1.9, 45), size (19, 3.8, 17); fade 1.5 m; decay 1.5 s.
- Restaurant row: center (25, 1.75, 126), size (29, 3.5, 11); fade 1 m; decay 0.75 s.

### roadside-motel-v1.prefab

- East guest wing: center (34, 3.6, 74), size (11.5, 7, 39); fade 0.8 m; decay 0.6 s.
- North guest wing: center (25, 3.6, 103), size (29, 7, 11.5); fade 0.8 m; decay 0.6 s.
- Change rooms: center (-32.5, 1.85, 100), size (18, 3.4, 17.5); fade 0.8 m; decay 1.3 s.
- Pool vendor and supply: center (-16.5, 1.85, 100), size (12, 3.4, 17.5); fade 0.8 m; decay 0.75 s.

### container-yard-v1.prefab

- West lower container passages: center (-25.5, 1.4, 80), size (32, 2.8, 61); fade 1 m; decay 0.65 s.
- East lower container passages: center (26.5, 1.4, 80.5), size (34, 2.8, 63); fade 1 m; decay 0.65 s.
- Repair shed: center (-33, 2.5, 134), size (17.5, 5, 15.5); fade 1.5 m; decay 1.1 s.
- Open trailer: center (25, 2.8, 136.8), size (2.5, 2.4, 11.8); fade 0.6 m; decay 0.8 s.

## Validation and remaining acceptance

Verified Duck Rails Unity editor at `X:/Work/duck-rails/Assets`, in Edit Mode. Installer compiled/executed; saved reload checks passed for all 25 enabled boxes and their child references, positive sizes, custom presets and matching fade radii. Helicopter Shop has zero zones. Console error/exception query returned zero entries. Serialized comparison preserves all pre-existing prefab objects; only root child lists gain the new groups. No lighting/navigation rebake is needed for audio-only objects.

No Play Mode or listening test was run. Queued for playtest: doorway transitions, cellar/farmhouse and motel partition overlap, tower/skybridge and container-roof bleed, tunnel entry/exit with moving train, voice and item/footstep clarity, and target-headset audio/performance. Verify local listeners on two real clients hear the space each occupies, including late join.
