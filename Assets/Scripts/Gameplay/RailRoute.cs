using System;
using System.Collections.Generic;
using System.Linq;
using Photon.Pun;
using UnityEngine;
using Hashtable = ExitGames.Client.Photon.Hashtable;

/// <summary>Instantiates only authored assets. Block IDs, spawn seed and positions are retained in the room.</summary>
[DefaultExecutionOrder(-20000)]
public sealed class RailRoute : MonoBehaviour
{
    public const string StateKey = "rails.route";
    public static RailRoute Instance { get; private set; }
    public RailBlock[] catalog;
    public RailBlock finale;
    public RailBlock startingStation;
    public Transform finaleRoot;
    public RailBlock midpointBlock;
    public RailExpedition expedition;
    public Game.AI.EnemyManager manager;
    public readonly List<RailBlock> Blocks = new List<RailBlock>();
    bool geometryReady;
    public bool Ready => geometryReady && (!Application.isPlaying || LightingReady());
    bool LightingReady()
    {
        bool Lit(RailBlock block) { var lighting = block != null ? block.GetComponent<RailBakedBlockLighting>() : null; return lighting == null || lighting.Ready; }
        return Lit(startingStation) && Lit(finale) && Lit(midpointBlock) && Blocks.All(Lit);
    }
    public string LoadedRun { get; private set; }
    public RailRouteState Selection { get; private set; }
    public float FinaleStart { get; private set; }
    public const float StationLength = 160;
    public const float StationDepartureLength = StationLength / 2;
    public const float StartZ = 150;
    void Awake() { Instance = this; }
    void OnDestroy() { if (Instance == this) Instance = null; }

    public const int NormalBlockCount = 4;
    public const int LayoutVersion = 2;
    public const float MidpointLength = 60;
    public const string MidpointId = "midpoint-helipad-v1";
    public static int MidpointBlockIndex(int count) => (count - 1) / 2;
    // Blocks remains the scavenging list. TravelBlocks includes the fixed service stop.
    public IEnumerable<RailBlock> TravelBlocks
    {
        get { for (int i=0;i<Blocks.Count;i++) { yield return Blocks[i]; if(i==MidpointBlockIndex(Blocks.Count) && midpointBlock!=null) yield return midpointBlock; } }
    }

    public static RailRouteState ChooseNormal(string run, RailBlock[] catalog, int seed)
        => Choose(run, catalog.Where(b => b != null && b.allowedInNormalRuns).ToArray(), seed, NormalBlockCount);

    // Unfiltered selection remains available for explicit catalogue/coverage fixtures.
    public static RailRouteState Choose(string run, RailBlock[] choices, int seed, int count = 5)
    {
        if (choices == null || choices.Length < count || count < 1 ||
            choices.Any(b => b == null) ||
            choices.Select(b => b.id).Distinct().Count() != choices.Length)
            throw new InvalidOperationException("Enable at least " + count + " distinct blocks with spawn candidates for this route.");
        var rng = new System.Random(seed);
        var pool = choices.OrderBy(b => b.id, StringComparer.Ordinal).ToArray();
        for (int i = pool.Length - 1; i > 0; i--) { int j = rng.Next(i + 1); (pool[i], pool[j]) = (pool[j], pool[i]); }
        var state = new RailRouteState { layoutVersion = LayoutVersion, run = run, enemySeed = seed, blocks = new string[count], variations = new int[count], placements = new Vector3[count] };
        float z = StartZ;
        for (int i = 0; i < count; i++)
        {
            state.blocks[i] = pool[i].id; // Legacy variation slot remains zero for retained-state compatibility.
            state.placements[i] = new Vector3(0, 0, z); z += pool[i].length;
            if(i==MidpointBlockIndex(count)) z+=MidpointLength;
        }
        return state;
    }
    public bool EnsureReady()
    {
        if (!PhotonSession.OnTrain) return false;
        // StartingStation is already placed in Expedition. Its additive probe payload must
        // be ready before the normal route-ready gate admits gameplay/late joiners.
        var stationLighting = startingStation != null ? startingStation.GetComponent<RailBakedBlockLighting>() : null;
        if (stationLighting != null && !stationLighting.Ready) return false;
        string run = PhotonSession.RunId;
        if (geometryReady && LoadedRun == run) return Ready;
        string json = PhotonSession.Get(StateKey, "");
        var state = string.IsNullOrEmpty(json) ? null : JsonUtility.FromJson<RailRouteState>(json);
        if (state == null || state.run != run)
        {
            if (PhotonNetwork.IsMasterClient)
            {
                var chosen = RailTestTools.TryRoute(run, out var repeat) ? repeat : ChooseNormal(run, catalog, UnityEngine.Random.Range(0, int.MaxValue));
                chosen.resourceCrew = Mathf.Clamp(PhotonSession.Get(RailResourceBalance.CrewKey,
                    PhotonNetwork.PlayerList.Count(p => !p.IsInactive)), 1, 8);
                PhotonSession.Instance.Commit(new Hashtable { [StateKey] = JsonUtility.ToJson(chosen) });
            }
            return false;
        }
        Build(state); return Ready;
    }
    public void Build(RailRouteState state)
    {
        Validate(state, catalog);
        int count = state.blocks.Length;
        if(midpointBlock==null || midpointBlock.id!=MidpointId || midpointBlock.length!=MidpointLength || midpointBlock.graph==null)
            throw new InvalidOperationException("Missing or incompatible authored midpoint helipad.");
        float z = StartZ + state.blocks.Sum(id => catalog.Single(b => b.id == id).length) + MidpointLength;
        BuildValidated(state, count, z);
    }
    public static RailRouteState ChooseOrdered(string run, RailBlock[] blocks, int seed)
    {
        if (blocks == null || blocks.Length == 0 || blocks.Any(b => b == null))
            throw new ArgumentException("Select at least one scavenging block with spawn candidates.");
        var state = new RailRouteState { layoutVersion = LayoutVersion, run = run, enemySeed = seed, blocks = blocks.Select(b => b.id).ToArray(),
            variations = new int[blocks.Length], placements = new Vector3[blocks.Length] };
        float z = StartZ;
        for (int i = 0; i < blocks.Length; i++)
        {
            state.placements[i] = Vector3.forward * z; z += blocks[i].length;
            if(i==MidpointBlockIndex(blocks.Length)) z+=MidpointLength;
        }
        Validate(state, blocks);
        return state;
    }
    public static void Validate(RailRouteState state, RailBlock[] catalog)
    {
        int count = state?.blocks?.Length ?? 0;
        if (count == 0 || state.layoutVersion != LayoutVersion || state.blocks.Contains(MidpointId) || state.blocks.Distinct().Count() != count ||
            state.placements?.Length != count)
            throw new InvalidOperationException("Select one or more different scavenging blocks (no duplicates).");
        // Validate everything before replacing the current presentation.
        float z = StartZ;
        for (int i = 0; i < count; i++)
        {
            var asset = catalog.Single(b => b.id == state.blocks[i]);
            if (!float.IsFinite(state.placements[i].sqrMagnitude) || !float.IsFinite(asset.length) || asset.length <= 0 ||
                Vector3.Distance(state.placements[i], new Vector3(0, 0, z)) > .001f || asset.graph == null)
                throw new InvalidOperationException("Incompatible block placement.");
            z += asset.length;
            if(i==MidpointBlockIndex(count)) z+=MidpointLength;
        }
    }
    void BuildValidated(RailRouteState state, int count, float z)
    {
        geometryReady = false;
        foreach (var block in Blocks) if (block != null) Destroy(block.gameObject);
        Blocks.Clear(); var coal = new List<Transform>(); var enemies = new List<Transform>(); var gold = new List<Transform>();
        var graphs = new List<Game.AI.CrawlGraph>(); var offsets = new List<Vector3>();
        var enemyTypes = new List<int>();
        float distance = StationDepartureLength;
        for (int i = 0; i < count; i++)
        {
            var block = Instantiate(catalog.Single(b => b.id == state.blocks[i]), state.placements[i], Quaternion.identity, transform);
            Blocks.Add(block); distance += block.TrackLength;
            coal.AddRange(SelectResources(block, RailSpawnKind.Coal, state));
            RailEnemySpawnBalance.Append(block, state.enemySeed, state.resourceCrew, enemies, enemyTypes);
            gold.AddRange(SelectResources(block, RailSpawnKind.Gold, state));
            graphs.Add(Game.AI.CrawlGraph.Load(block.graph)); offsets.Add(block.transform.position);
        }
        int middle = MidpointBlockIndex(count);
        midpointBlock.transform.position = state.placements[middle] + Vector3.forward * Blocks[middle].length;
        // The short service stop uses its authored counts, like the finale.
        coal.AddRange(RailSpawnPool.Select(midpointBlock, RailSpawnKind.Coal, state.enemySeed));
        gold.AddRange(RailSpawnPool.Select(midpointBlock, RailSpawnKind.Gold, state.enemySeed));
        distance += midpointBlock.TrackLength;
        graphs.Add(Game.AI.CrawlGraph.Load(midpointBlock.graph)); offsets.Add(midpointBlock.transform.position);
        FinaleStart = distance;
        finaleRoot.position = new Vector3(0, 0, z);
        graphs.Add(Game.AI.CrawlGraph.Load(finale.graph)); offsets.Add(finaleRoot.position);
        expedition.baseEnemyCount = enemies.Count;
        RailSpawnPool.SelectEnemies(finale, state.enemySeed, enemies, enemyTypes);
        coal.AddRange(RailSpawnPool.Select(finale, RailSpawnKind.Coal, state.enemySeed));
        gold.AddRange(RailSpawnPool.Select(finale, RailSpawnKind.Gold, state.enemySeed));
        expedition.goldPoints = gold.ToArray();
        expedition.enemyTypes = enemyTypes.ToArray();
        expedition.coalPoints = coal.ToArray(); expedition.enemyPoints = enemies.ToArray();
        expedition.bridgeZ = z + 80;
        var graph = RailGraphAssembly.Combine(graphs, offsets);
        graph.RestrictToBounds(TravelBlocks.Select(b => new Bounds(b.transform.position + b.enemyBounds.center, b.enemyBounds.size))
            .Concat(new[] { new Bounds(finaleRoot.position + finale.enemyBounds.center, finale.enemyBounds.size) }).ToArray());
        manager.SetGraph(graph);
        LoadedRun = state.run; Selection = state; geometryReady = true;
        if (Application.isPlaying)
        {
            var visibility = GetComponent<RailBlockVisibility>() ?? gameObject.AddComponent<RailBlockVisibility>();
            visibility.Rebuild(this);
            if (GetComponent<RailPlayerBounds>() == null) gameObject.AddComponent<RailPlayerBounds>();
        }
    }
    public void Sample(float distance, out Vector3 position, out Quaternion rotation)
    {
        if (distance <= StationDepartureLength)
        {
            startingStation.Sample(StationDepartureLength + distance, out position, out rotation);
            position.y += 1; return;
        }
        distance -= StationDepartureLength;
        foreach (var block in TravelBlocks)
        {
            if (distance <= block.TrackLength) { block.Sample(distance, out position, out rotation); position.y += 1; return; }
            distance -= block.TrackLength;
        }
        position = finaleRoot.position + new Vector3(0, 1, distance); rotation = Quaternion.identity;
    }

    public static Transform[] SelectResources(RailBlock block, RailSpawnKind kind, RailRouteState state)
    {
        int count = RailSpawnPool.Count(block, kind) == 0 ? 0 : kind == RailSpawnKind.Coal
            ? RailResourceBalance.CoalCount(state.resourceCrew) : RailResourceBalance.GoldCount(state.resourceCrew);
        var selected = RailSpawnPool.Select(block, kind, state.enemySeed, count);
        if (selected.Length < count)
            if (DuckzLog.AllowWarning(out var notice, block.id + " " + kind))
                notice.Write($"Resource pool shortfall: {block.id} {kind} has {selected.Length}/{count} usable candidates for crew {state.resourceCrew}.", block);
        return selected;
    }
    // Authored blocks run along world +Z; use their footprint length, not curved track distance.
    public bool TryBlockSkip(Vector3 position, out Vector3 destination)
    {
        destination = position;
        RailBlock current = null;
        bool Contains(RailBlock block) => block != null && position.z >= block.transform.position.z &&
            position.z < block.transform.position.z + block.length;
        if (Contains(startingStation)) current = startingStation;
        else foreach (var block in TravelBlocks) if (Contains(block)) { current = block; break; }
        if (current == null || finale == null || current.length <= 0) return false;
        var target = position + new Vector3(0, 1, current.length);
        if (target.z >= finale.transform.position.z + finale.length) return false;
        destination = target;
        return true;
    }

    public Bounds EnemyBounds(Vector3 position)
    {
        foreach (var block in TravelBlocks)
            if (position.z >= block.transform.position.z && position.z < block.transform.position.z + block.length)
                return new Bounds(block.transform.position + block.enemyBounds.center, block.enemyBounds.size);
        return new Bounds(finaleRoot.position + finale.enemyBounds.center, finale.enemyBounds.size);
    }
    public bool IsEncounter(Vector3 position) => EnemyBounds(position).Contains(position);
    // Player walls, fall recovery and below-floor cargo removal. Allocation-free; called per item per frame.
    public bool TryPlayerArea(Vector3 position, out RailBlock block)
    {
        block = null;
        if (InFootprint(startingStation, position)) block = startingStation;
        for (int i = 0; block == null && i < Blocks.Count; i++) if (InFootprint(Blocks[i], position)) block = Blocks[i];
        if (block == null && InFootprint(midpointBlock, position)) block = midpointBlock;
        if (block == null && InFootprint(finale, position)) block = finale;
        return block != null;
    }
    static bool InFootprint(RailBlock block, Vector3 position) => block != null &&
        position.z >= block.transform.position.z && position.z < block.transform.position.z + block.length;
}
