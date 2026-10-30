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
    public Transform finaleRoot;
    public RailExpedition expedition;
    public Game.AI.EnemyManager manager;
    public readonly List<RailBlock> Blocks = new List<RailBlock>();
    public bool Ready { get; private set; }
    public string LoadedRun { get; private set; }
    public RailRouteState Selection { get; private set; }
    public float FinaleStart { get; private set; }
    public const float StartZ = 70;
    void Awake() { Instance = this; }
    void OnDestroy() { if (Instance == this) Instance = null; }

    public static RailRouteState Choose(string run, RailBlock[] choices, int seed)
    {
        var rng = new System.Random(seed);
        var pool = choices.OrderBy(b => b.id, StringComparer.Ordinal).ToArray();
        for (int i = pool.Length - 1; i > 0; i--) { int j = rng.Next(i + 1); (pool[i], pool[j]) = (pool[j], pool[i]); }
        var state = new RailRouteState { run = run, blocks = new string[5], variations = new int[5], placements = new Vector3[5] };
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
        string run = PhotonSession.RunId;
        if (Ready && LoadedRun == run) return true;
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
        if (state.blocks == null || state.blocks.Length != 5 || state.blocks.Distinct().Count() != 5 ||
            state.variations?.Length != 5 || state.placements?.Length != 5) throw new InvalidOperationException("Invalid retained route.");
        // Validate everything before replacing the current presentation.
        float z = StartZ;
        for (int i = 0; i < 5; i++)
        {
            var asset = catalog.Single(b => b.id == state.blocks[i]);
            if (state.variations[i] < 0 || state.variations[i] >= asset.variations.Length ||
                Vector3.Distance(state.placements[i], new Vector3(0, 0, z)) > .001f || asset.graph == null)
                throw new InvalidOperationException("Incompatible block placement/variation.");
            z += asset.length;
        }
        Ready = false;
        foreach (var block in Blocks) if (block != null) Destroy(block.gameObject);
        Blocks.Clear(); var coal = new List<Transform>(); var enemies = new List<Transform>();
        var graphs = new List<Game.AI.CrawlGraph>(); var offsets = new List<Vector3>();
        float distance = 0;
        for (int i = 0; i < 5; i++)
        {
            var block = Instantiate(catalog.Single(b => b.id == state.blocks[i]), state.placements[i], Quaternion.identity, transform);
            Blocks.Add(block); distance += block.TrackLength;
            coal.AddRange(block.variations[state.variations[i]].coal); enemies.AddRange(block.variations[state.variations[i]].enemies);
            graphs.Add(Game.AI.CrawlGraph.Load(block.graph)); offsets.Add(block.transform.position);
        }
        FinaleStart = distance;
        finaleRoot.position = new Vector3(0, 0, z);
        graphs.Add(Game.AI.CrawlGraph.Load(finale.graph)); offsets.Add(finaleRoot.position);
        enemies.AddRange(finale.variations[0].enemies);
        expedition.coalPoints = coal.ToArray(); expedition.enemyPoints = enemies.ToArray();
        expedition.baseEnemyCount = enemies.Count - finale.variations[0].enemies.Length;
        expedition.bridgeZ = z + 80;
        var graph = RailGraphAssembly.Combine(graphs, offsets);
        graph.RestrictToBounds(Blocks.Select(b => new Bounds(b.transform.position + b.enemyBounds.center, b.enemyBounds.size))
            .Concat(new[] { new Bounds(finaleRoot.position + finale.enemyBounds.center, finale.enemyBounds.size) }).ToArray());
        manager.SetGraph(graph);
        LoadedRun = state.run; Selection = state; Ready = true;
    }
    public void Sample(float distance, out Vector3 position, out Quaternion rotation)
    {
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
