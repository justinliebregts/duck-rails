# Block triangle counts

Measured November 5, 2026 through Unity MCP, after verifying `Application.dataPath` was `X:/Work/duck-rails/Assets`. Read-only inspection of every prefab under Assets with a root RailBlock component: 16 scavenging blocks, Bridge Finale and Departure Station.

Counts sum triangle-topology submesh index counts / 3 for each MeshRenderer or SkinnedMeshRenderer instance, including repeated meshes once per placed instance. All counted renderers were enabled with active ancestors. No LODGroups or Unity Terrains were present in these prefabs.

These are whole-prefab geometry counts, not per-frame GPU triangle measurements. They exclude separately spawned train, players, enemies and loot, collider-only meshes, and additional shadow/material/stereo passes. Camera culling can reduce visible geometry. No headset performance conclusion follows from these counts.

- Farm Silos: 198,001 triangles; 930 mesh renderers.
- Roadside Motel: 106,396; 905 renderers.
- Container Yard: 98,280; 1,070 renderers.
- Maintenance Depot: 97,164; 685 renderers.
- Nuclear Power Plant: 92,400; 1,344 renderers.
- Departure Station: 64,188; 271 renderers.
- Convenience Store: 54,668; 756 renderers.
- Lakeside Campground: 41,331; 762 renderers.
- Canyon: 40,828; 753 renderers.
- Skyscraper Works: 40,728; 3,394 renderers.
- Quarry: 38,848; 498 renderers.
- Abandoned Underground Station: 24,408; 2,034 renderers.
- Hospital District: 15,428; 1,246 renderers.
- Electrical Substation: 3,228; 235 renderers.
- Scrapyard: 2,856; 238 renderers.
- Lumber Yard: 2,808; 234 renderers.
- Tunnel Works: 2,760; 230 renderers.
- Bridge Finale: 1,488; 124 renderers.

Source prefabs: `Assets/DuckRails/Blocks/*.prefab` and `Assets/DuckRails/StartingStation.prefab`. Scenes and assets were not modified by the inspection.
