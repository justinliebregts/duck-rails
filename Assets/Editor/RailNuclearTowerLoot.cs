#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>November 26 loot candidates along both cooling tower ramps and the pipe walkway.
/// Appends to the existing pools; rerunning replaces only the markers this tool made.</summary>
public static class RailNuclearTowerLoot
{
    public const string RampTag = "tower scatter", WalkwayTag = "pipe walkway";
    public const int RampCoalPerTurn = 4, RampGoldPerTurn = 3, WalkwayCoal = 12, WalkwayGold = 10;
    [Serializable] class Manifest { public Vector3[] rampSamples; }

    [MenuItem("Duck Rails/Authoring/Nuclear Plant/Add tower and walkway loot")]
    public static void Install()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Install in Edit Mode.");
        var samples = JsonUtility.FromJson<Manifest>(File.ReadAllText("ArtSource/NuclearFullBlock/site.json")).rampSamples;
        var instance = PrefabUtility.LoadPrefabContents(RailNuclearPlant.PrefabPath);
        try {
            var block = instance.GetComponent<RailBlock>();
            var physics = instance.scene.GetPhysicsScene();
            Physics.SyncTransforms();
            foreach (var kind in new[] { RailSpawnKind.Coal, RailSpawnKind.Gold }) {
                var old = RailSpawnPool.Points(block.spawnCandidates, kind)
                    .Where(t => t && (t.name.EndsWith(RampTag) || t.name.EndsWith(WalkwayTag))).ToArray();
                RailSpawnPool.SetPoints(block.spawnCandidates, kind,
                    RailSpawnPool.Points(block.spawnCandidates, kind).Except(old).ToArray());
                foreach (var t in old) Object.DestroyImmediate(t.gameObject);
            }
            var added = new Dictionary<RailSpawnKind, List<Transform>> {
                { RailSpawnKind.Coal, new List<Transform>() }, { RailSpawnKind.Gold, new List<Transform>() } };
            void Add(RailSpawnKind kind, Vector3 position, string place) {
                var marker = RailSpawnerPrefabs.Instantiate(kind, instance.scene);
                var parent = instance.transform.Find(kind + " Spawns");
                marker.transform.SetParent(parent, false);
                marker.transform.position = position;
                int number = RailSpawnPool.Points(block.spawnCandidates, kind).Length + added[kind].Count + 1;
                marker.name = $"{kind} {number:000} - {place}";
                PrefabUtility.RecordPrefabInstancePropertyModifications(marker);
                PrefabUtility.RecordPrefabInstancePropertyModifications(marker.transform);
                added[kind].Add(marker.transform);
            }

            // Each turn's sloped run gets an even string of finds, alternating sides so the
            // middle stays walkable. The level service bays keep their existing candidates.
            int perTurn = RampCoalPerTurn + RampGoldPerTurn;
            for (int tower = 0; tower < 2; tower++)
            for (int turn = 0; turn < 4; turn++) {
                int gold = 0, coal = 0;
                for (int k = 0; k < perTurn; k++) {
                    bool isGold = (k + 1) * RampGoldPerTurn / perTurn > k * RampGoldPerTurn / perTurn;
                    var kind = isGold ? RailSpawnKind.Gold : RailSpawnKind.Coal;
                    int section = tower * 160 + turn * 40 + 2 + Mathf.RoundToInt(k * 33f / (perTurn - 1));
                    var q = samples[section];
                    var radial = new Vector3(q.x - (tower == 0 ? -26 : 26), 0, q.z - 103).normalized;
                    int index = isGold ? gold++ : coal++;
                    var p = q + radial * (index % 2 == 0 ? .62f : -.62f);
                    if (!physics.Raycast(p + Vector3.up, Vector3.down, out var floor, 2, 1 << 6, QueryTriggerInteraction.Ignore))
                        throw new InvalidDataException("No ramp deck under " + p);
                    p.y = floor.point.y + (isGold ? .08f : .23f);
                    Add(kind, p, $"{(tower == 0 ? "West" : "East")} tower ramp, turn {turn + 1}, {RampTag}");
                }
            }

            // Items sit in the two grooves between the three pipes, resting across two crests.
            var from = new Vector3(-16.40f, 35.49f, 104.5f);
            var to = new Vector3(16.40f, 35.49f, 101.5f);
            var along = (to - from).normalized;
            var across = new Vector3(-along.z, 0, along.x);
            int total = WalkwayCoal + WalkwayGold, golds = 0, coals = 0;
            for (int k = 0; k < total; k++) {
                bool isGold = (k + 1) * WalkwayGold / total > k * WalkwayGold / total;
                float x = Mathf.Lerp(-15.6f, 15.6f, k / (total - 1f));
                var p = Vector3.Lerp(from, to, Mathf.InverseLerp(from.x, to.x, x));
                int index = isGold ? golds++ : coals++;
                p += across * (index % 2 == 0 ? .1225f : -.1225f);
                p.y = 35.49f + (isGold ? .05f : .20f);
                Add(isGold ? RailSpawnKind.Gold : RailSpawnKind.Coal, p, "Tower " + WalkwayTag);
            }

            foreach (var pair in added)
                RailSpawnPool.SetPoints(block.spawnCandidates, pair.Key,
                    RailSpawnPool.Points(block.spawnCandidates, pair.Key).Concat(pair.Value).ToArray());
            PrefabUtility.SaveAsPrefabAsset(instance, RailNuclearPlant.PrefabPath, out bool saved);
            if (!saved) throw new IOException("Failed to save nuclear prefab.");
        } finally { PrefabUtility.UnloadPrefabContents(instance); }
        AssetDatabase.SaveAssets();
    }

    /// <summary>Drops stand-in item bodies at every new candidate in the open bake scene and
    /// returns those that slide or fall once physics runs.</summary>
    public static string[] SettleCheck()
    {
        var block = Object.FindFirstObjectByType<RailBlock>();
        var moved = new List<string>();
        var proxies = new List<(Rigidbody body, Vector3 start, string name)>();
        var mode = Physics.simulationMode;
        try {
            foreach (var marker in block.GetComponentsInChildren<RailSpawnMarker>()
                         .Where(m => m.name.EndsWith(RampTag) || m.name.EndsWith(WalkwayTag))) {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(marker.kind == RailSpawnKind.Gold
                    ? "Assets/DuckRails/Economy/RailGoldBar.prefab" : "Assets/Resources/Rails/RailCoal.prefab");
                var source = prefab.GetComponent<Rigidbody>();
                var go = new GameObject("Settle proxy") { layer = prefab.layer, hideFlags = HideFlags.DontSave };
                go.transform.position = marker.transform.position;
                foreach (var box in prefab.GetComponentsInChildren<BoxCollider>().Where(c => !c.isTrigger)) {
                    var copy = go.AddComponent<BoxCollider>();
                    var matrix = Matrix4x4.TRS(prefab.transform.position, prefab.transform.rotation, Vector3.one).inverse * box.transform.localToWorldMatrix;
                    copy.center = matrix.MultiplyPoint3x4(box.center);
                    var size = matrix.MultiplyVector(box.size);
                    copy.size = new Vector3(Mathf.Abs(size.x), Mathf.Abs(size.y), Mathf.Abs(size.z));
                    copy.sharedMaterial = box.sharedMaterial;
                }
                var body = go.AddComponent<Rigidbody>();
                body.mass = source.mass; body.linearDamping = source.linearDamping; body.angularDamping = source.angularDamping;
                proxies.Add((body, go.transform.position, marker.name));
            }
            Physics.SyncTransforms();
            Physics.simulationMode = SimulationMode.Script;
            for (int i = 0; i < 400; i++) Physics.Simulate(.02f);
            foreach (var (body, start, name) in proxies) {
                var delta = body.position - start;
                if (new Vector2(delta.x, delta.z).magnitude > .3f || delta.y < -.35f) moved.Add(name + " moved " + delta);
            }
            if (proxies.Count == 0) moved.Add("No new candidates found");
        } finally {
            Physics.simulationMode = mode;
            foreach (var p in proxies) Object.DestroyImmediate(p.body.gameObject);
        }
        return moved.ToArray();
    }
}
#endif
