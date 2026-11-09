using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

public static class RailResourceBalanceChecks
{
    public static void Run()
    {
        int checks = 0;
        void Check(bool ok, string message) { if (!ok) throw new Exception(message); checks++; }
        var blocks = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/DuckRails/Blocks" })
            .Select(id => AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(id)).GetComponent<RailBlock>())
            .Where(b => b != null && b.allowedInNormalRuns).ToArray();
        Check(blocks.Length == 4, "Four normal blocks");
        for (int crew = 1; crew <= 8; crew++)
        {
            int coal = RailResourceBalance.CoalCount(crew), gold = RailResourceBalance.GoldCount(crew);
            float value = RailResourceBalance.CoalValue(crew);
            Check(coal * value <= 75.0001f && coal * value > 65, "Fuel availability ceiling and useful surplus");
            Check(Math.Abs(4 * (float)RailResourceBalance.Workers(crew) * value - 50) < .0001f, "Collection target buys one block");
            Check(Enumerable.Range(1, gold).Sum(i => RailResourceBalance.GoldCredit(gold, i)) == 100, "Full gold budget");
            Check(Enumerable.Range(1, gold * 4).Sum(i => RailResourceBalance.GoldCredit(gold, i)) == 400, "Four-block gold carry");
            var state = RailRoute.ChooseNormal("resource-check", blocks, 427);
            state.resourceCrew = crew;
            var restored = JsonUtility.FromJson<RailRouteState>(JsonUtility.ToJson(state));
            Check(restored.resourceCrew == crew, "Late-join/master snapshot preserves crew");
            foreach (var block in blocks)
                for (int seed = 0; seed < 12; seed++)
                {
                    state.enemySeed = restored.enemySeed = seed;
                    foreach (var kind in new[] { RailSpawnKind.Coal, RailSpawnKind.Gold })
                    {
                        var selected = RailRoute.SelectResources(block, kind, state);
                        Check(selected.Length == (kind == RailSpawnKind.Coal ? coal : gold), block.id + " pool capacity");
                        Check(selected.Distinct().Count() == selected.Length, "No duplicate selection");
                        Check(selected.SequenceEqual(RailRoute.SelectResources(block, kind, restored)), "Retained deterministic selection");
                    }
                    Check(RailSpawnPool.Select(block, RailSpawnKind.Enemy, seed).Length == block.enemyCount, "Enemy counts unchanged");
                }
        }
        Check(RailResourceBalance.CoalCount(8) == 31 && RailResourceBalance.GoldCount(8) == 52, "Eight-player workload");
        RailSessionRules.EvaluateTrain(new object[] { 0d, 0f, 4f, 4f }, 100, 80, out var distance, out _, out var fuel);
        Check(distance == 320 && fuel == 0, "Full tank gives 320 powered metres");
        RailSessionRules.EvaluateTrain(new object[] { 0d, 0f, 0f, 0f }, 25, 1000, out _, out _, out fuel);
        Check(fuel == 25, "No idle drain");
        Check((float)RailSessionRules.Options(RailSessionRules.ExpeditionKind, "test").CustomRoomProperties[RailSessionRules.Fuel] == 50, "Initial fuel");
        RailSessionRules.EvaluateTrain(new object[] { 0d, 0f, 0f, 4f }, RailResourceBalance.StartingFuel,
            1000, out distance, out var stoppedSpeed, out fuel);
        Check(stoppedSpeed == 0 && fuel == 0 && blocks.All(b =>
            distance >= RailRoute.StationDepartureLength + b.TrackLength / 2),
            "Starting tank reaches the middle of every enabled first block, including exhaustion coast");
        var parse = typeof(DuckzWallet).GetMethod("TryParsePending", BindingFlags.NonPublic | BindingFlags.Static);
        foreach (var entry in new[] { "receipt:1", "receipt:1:52:21" })
        {
            object[] args = { entry, null, 0, 0, 0 };
            Check((bool)parse.Invoke(null, args), "Legacy/scaled persistent reward parses");
        }
        foreach (var entry in new[] { "receipt:1:0:1", "receipt:2:52:1", "receipt:1:52:0", "receipt:1:52:10001" })
        {
            object[] args = { entry, null, 0, 0, 0 };
            Check(!(bool)parse.Invoke(null, args), "Malformed scaled reward rejected");
        }
        Debug.Log("RESOURCE BALANCE CHECKS PASSED: " + checks);
    }
}
