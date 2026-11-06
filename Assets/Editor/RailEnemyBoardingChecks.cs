#if UNITY_EDITOR
using System;
using System.IO;
using Game.AI;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public static class RailEnemyBoardingChecks
{
    static int checks;
    static void Require(bool condition, string message)
    { if (!condition) throw new Exception("TRAIN BOARDING: " + message); checks++; }
    [MenuItem("Duck Rails/Tests/Enemy Boarding/Graph And Network Checks")]
    public static void Run()
    {
        checks = 0;
        var asset = AssetDatabase.LoadAssetAtPath<TextAsset>(RailEnemyBoardingAuthoring.GraphPath);
        var graph = CrawlGraph.Load(asset);
        Require(graph != null && graph.NodeCount > 10, "Baked train graph exists");
        var frame = new GameObject("Moving graph test frame");
        try
        {
            graph.Frame = frame.transform;
            int rear = graph.FindNearestGroundNode(new Vector3(0, .03f, -4.1f), .8f);
            int front = graph.FindNearestGroundNode(new Vector3(0, .03f, 2), 1f);
            Require(rear >= 0 && front >= 0, "Rear and forward deck nodes exist");
            int[] path = new int[512];
            var navigation = AssetDatabase.LoadAssetAtPath<GameObject>(RailEnemyBoardingAuthoring.TrainPath).GetComponent<RailTrainNavigation>();
            var openings = new int[RailTrainNavigation.OpeningCount];
            for (int opening = 0; opening < openings.Length; opening++)
            {
                openings[opening] = graph.FindNearestGroundNode(navigation.Landing(opening), .8f);
                Require(openings[opening] >= 0 && graph.GetComponent(openings[opening]) == graph.GetComponent(rear),
                    "Opening connects to deck: " + RailTrainNavigation.OpeningName(opening));
            }
            Vector3 localRear = graph.GetPosition(rear), localFront = graph.GetPosition(front);
            for (int i = 0; i < 32; i++)
            {
                frame.transform.SetPositionAndRotation(new Vector3(i * 31, i % 3, i * -13), Quaternion.Euler(i % 2 == 0 ? 8 : -8, i * 31, 0));
                Vector3 rearWorld = frame.transform.TransformPoint(localRear);
                Require((graph.GetPosition(rear) - rearWorld).sqrMagnitude < .000001f, "Node follows train pose " + i);
                Require(graph.FindNearestGroundNode(rearWorld, .3f) == rear, "Rotated local spatial index " + i);
                Require(graph.FindNearestGroundNode(frame.transform.TransformPoint(localFront), .3f) == front, "Forward local lookup " + i);
                foreach (int opening in openings)
                {
                    Vector3 expected = frame.transform.TransformPoint(new Vector3(
                        graph.NodePos[opening * 3], graph.NodePos[opening * 3 + 1], graph.NodePos[opening * 3 + 2]));
                    Require(graph.FindNearestGroundNode(expected, .3f) == opening, "Opening follows translated/yawed/sloped frame " + i);
                }
                var finder = new CrawlPathfinder(graph, 10000);
                finder.BeginSearch(rear, front, true, null);
                int guard = 0;
                while (!finder.Step(System.Diagnostics.Stopwatch.GetTimestamp() + System.Diagnostics.Stopwatch.Frequency / 100) && guard++ < 20) { }
                Require(finder.CopyResult(path, out int count, out bool complete) && complete && count > 0, "Deck path under translation/yaw/slope " + i);
            }
            for (int flags = 0; flags < 256; flags++)
            {
                var packet = new EnemyPosePacket { Position = new Vector3(-1.23f, .04f, 2.5f), Rotation = Quaternion.Euler(8, 45, 0), Flags = (byte)flags };
                var decoded = (EnemyPosePacket)EnemyPosePacket.Deserialize(EnemyPosePacket.Serialize(packet));
                Require(decoded.Flags == flags && (decoded.Position - packet.Position).sqrMagnitude < .000001f,
                    "Packet retains state/aim/knock/train/displaced flags " + flags);
                EnemyPosePacket.Return(decoded);
            }
            var minion = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Rails/RailZombie.prefab");
            Require(minion.GetComponent<RailEnemyBoarding>() != null, "Minion opts in");
            foreach (string name in new[] { "RailBomber", "RailSmasher", "RailSpider" })
                Require(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Rails/" + name + ".prefab").GetComponent<RailEnemyBoarding>() == null,
                    name + " remains unchanged in minion-first rollout");
        }
        finally { Object.DestroyImmediate(frame); }
        string message = "TRAIN BOARDING: " + checks + " graph/packet/authoring checks passed";
        Directory.CreateDirectory("docs/validation/enemy-boarding");
        File.WriteAllText("docs/validation/enemy-boarding/editor-checks.txt", DateTime.UtcNow.ToString("O") + " " + message);
        Debug.Log(message);
    }
}
#endif
