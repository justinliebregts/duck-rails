using System;
using System.Collections.Generic;
using System.Linq;
using Photon.Pun;
using UnityEngine;
using Hashtable = ExitGames.Client.Photon.Hashtable;

/// <summary>Instantiates only authored assets. Exact block IDs, variations and positions are retained in the room.</summary>
[DefaultExecutionOrder(-20000)]
public sealed class RailRoute : MonoBehaviour
{
    public const string StateKey = "rails.route";
    public static RailRoute Instance { get; private set; }
    public RailBlock[] catalog;
    public RailBlock finale;
    public RailBlock startingStation;
    public Transform finaleRoot;
    public Transform midpointShop;
    public RailExpedition expedition;
    public Game.AI.EnemyManager manager;
    public readonly List<RailBlock> Blocks = new List<RailBlock>();
    bool geometryReady;
    public bool Ready => geometryReady && (!Application.isPlaying || LightingReady());
    bool LightingReady()
    {
        bool Lit(RailBlock block) { var lighting = block != null ? block.GetComponent<RailBakedBlockLighting>() : null; return lighting == null || lighting.Ready; }
        return Lit(startingStation) && Lit(finale) && Blocks.All(Lit);
    }
    public string LoadedRun { get; private set; }
    public RailRouteState Selection { get; private set; }
    public float FinaleStart { get; private set; }
    public const float StationLength = 160;
    public const float StationDepartureLength = StationLength / 2;
    public const float StartZ = 150;
    void Awake() { Instance = this; }
    void OnDestroy() { if (Instance == this) Instance = null; }

    public static RailRouteState Choose(string run, RailBlock[] choices, int seed)
    {
        var rng = new System.Random(seed);
        var pool = choices.OrderBy(b => b.id, StringComparer.Ordinal).ToArray();
        for (int i = pool.Length - 1; i > 0; i--) { int j = rng.Next(i + 1); (pool[i], pool[j]) = (pool[j], pool[i]); }
        var state = new RailRouteState { run = run, enemySeed = seed, blocks = new string[5], variations = new int[5], placements = new Vector3[5] };
        float z = StartZ;
        for (int i = 0; i < 5; i++)
        {
            state.blocks[i] = pool[i].id; state.variations[i] = rng.Next(pool[i].variations.Length);
            state.placements[i] = new Vector3(0, 0, z); z += pool[i].length;
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
                PhotonSession.Instance.Commit(new Hashtable { [StateKey] = JsonUtility.ToJson(
                    RailTestTools.TryRoute(run, out var repeat) ? repeat : Choose(run, catalog, UnityEngine.Random.Range(0, int.MaxValue))) });
            return false;
        }
        Build(state); return Ready;
    }
    public void Build(RailRouteState state)
    {
        Validate(state, catalog);
        int count = state.blocks.Length;
        float z = StartZ + state.blocks.Sum(id => catalog.Single(b => b.id == id).length);
        BuildValidated(state, count, z);
    }
    public static RailRouteState ChooseOrdered(string run, RailBlock[] blocks, int seed)
    {
        if (blocks == null || blocks.Length == 0 || blocks.Any(b => b == null || b.variations == null || b.variations.Length == 0))
            throw new ArgumentException("Select at least one scavenging block with spawn variations.");
        var rng = new System.Random(seed);
        var state = new RailRouteState { run = run, enemySeed = seed, blocks = blocks.Select(b => b.id).ToArray(),
            variations = new int[blocks.Length], placements = new Vector3[blocks.Length] };
        float z = StartZ;
        for (int i = 0; i < blocks.Length; i++)
        {
            state.variations[i] = rng.Next(blocks[i].variations.Length);
            state.placements[i] = Vector3.forward * z; z += blocks[i].length;
        }
        Validate(state, blocks);
        return state;
    }
    public static void Validate(RailRouteState state, RailBlock[] catalog)
    {
        int count = state?.blocks?.Length ?? 0;
        if (count == 0 || state.blocks.Distinct().Count() != count ||
            state.variations?.Length != count || state.placements?.Length != count)
            throw new InvalidOperationException("Select one or more different scavenging blocks (no duplicates).");
        // Validate everything before replacing the current presentation.
        float z = StartZ;
        for (int i = 0; i < count; i++)
        {
            var asset = catalog.Single(b => b.id == state.blocks[i]);
            if (asset.variations == null || state.variations[i] < 0 || state.variations[i] >= asset.variations.Length ||
                !float.IsFinite(state.placements[i].sqrMagnitude) || !float.IsFinite(asset.length) || asset.length <= 0 ||
                Vector3.Distance(state.placements[i], new Vector3(0, 0, z)) > .001f || asset.graph == null)
                throw new InvalidOperationException("Incompatible block placement/variation.");
            z += asset.length;
        }
    }
    void BuildValidated(RailRouteState state, int count, float z)
    {
        geometryReady = false;
        foreach (var block in Blocks) if (block != null) Destroy(block.gameObject);
        Blocks.Clear(); var coal = new List<Transform>(); var enemies = new List<Transform>(); var gold = new List<Transform>();
        var graphs = new List<Game.AI.CrawlGraph>(); var offsets = new List<Vector3>();
        var enemyTypes = new List<int>();
        var encounterRandom = new System.Random(state.enemySeed);
        float distance = StationDepartureLength;
        for (int i = 0; i < count; i++)
        {
            var block = Instantiate(catalog.Single(b => b.id == state.blocks[i]), state.placements[i], Quaternion.identity, transform);
            Blocks.Add(block); distance += block.TrackLength;
            coal.AddRange(block.variations[state.variations[i]].coal);
            RailEnemySpawns.Select(block.variations[state.variations[i]].enemies, encounterRandom, enemies, enemyTypes);
            gold.AddRange(block.variations[state.variations[i]].gold ?? Array.Empty<Transform>());
            graphs.Add(Game.AI.CrawlGraph.Load(block.graph)); offsets.Add(block.transform.position);
        }
        if (midpointShop != null) midpointShop.position = state.placements[count / 2] + new Vector3(-16, 0, 148);
        expedition.goldPoints = gold.ToArray();
        FinaleStart = distance;
        finaleRoot.position = new Vector3(0, 0, z);
        graphs.Add(Game.AI.CrawlGraph.Load(finale.graph)); offsets.Add(finaleRoot.position);
        expedition.baseEnemyCount = enemies.Count;
        RailEnemySpawns.Select(finale.variations[0].enemies, encounterRandom, enemies, enemyTypes);
        expedition.enemyTypes = enemyTypes.ToArray();
        expedition.coalPoints = coal.ToArray(); expedition.enemyPoints = enemies.ToArray();
        expedition.bridgeZ = z + 80;
        var graph = RailGraphAssembly.Combine(graphs, offsets);
        graph.RestrictToBounds(Blocks.Select(b => new Bounds(b.transform.position + b.enemyBounds.center, b.enemyBounds.size))
            .Concat(new[] { new Bounds(finaleRoot.position + finale.enemyBounds.center, finale.enemyBounds.size) }).ToArray());
        manager.SetGraph(graph);
        LoadedRun = state.run; Selection = state; geometryReady = true;
    }
    public void Sample(float distance, out Vector3 position, out Quaternion rotation)
    {
        if (distance <= StationDepartureLength)
        {
            startingStation.Sample(StationDepartureLength + distance, out position, out rotation);
            position.y += 1; return;
        }
        distance -= StationDepartureLength;
        foreach (var block in Blocks)
        {
            if (distance <= block.TrackLength) { block.Sample(distance, out position, out rotation); position.y += 1; return; }
            distance -= block.TrackLength;
        }
        position = finaleRoot.position + new Vector3(0, 1, distance); rotation = Quaternion.identity;
    }
    public Bounds EnemyBounds(Vector3 position)
    {
        foreach (var block in Blocks)
            if (position.z >= block.transform.position.z && position.z < block.transform.position.z + block.length)
                return new Bounds(block.transform.position + block.enemyBounds.center, block.enemyBounds.size);
        return new Bounds(finaleRoot.position + finale.enemyBounds.center, finale.enemyBounds.size);
    }
    public bool IsEncounter(Vector3 position) => EnemyBounds(position).Contains(position);
}
