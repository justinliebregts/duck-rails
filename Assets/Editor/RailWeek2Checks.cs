#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class RailWeek2Checks
{
    [MenuItem("Duck Rails/Run Week 2 Rules Checks")]
    public static void Run()
    {
        var state = new RailRunState { run = "test", bridgeAt = 100 };
        state.players.Add(new RailParticipant { id = "aboard", down = true });
        state.players.Add(new RailParticipant { id = "left-behind" });
        Check(state.BridgeProgress(99) == 0 && state.BridgeProgress(130) == .5f && state.BridgeProgress(160) == 1, "Bridge uses one retained 60-second clock");
        var reconstructed = JsonUtility.FromJson<RailRunState>(JsonUtility.ToJson(state));
        Check(reconstructed.BridgeProgress(145) == .75f, "Late-join/master reconstruction preserves bridge progress");
        state.Finish(161, "Bridge crossed", new[] { "aboard" });
        Check(state.success && state.Player("aboard").escaped && state.Player("aboard").down && !state.Player("left-behind").escaped, "One downed passenger wins; left-behind player remains a non-finisher");
        state.Finish(180, "duplicate", new[] { "left-behind" });
        Check(state.Player("aboard").escaped && !state.Player("left-behind").escaped && state.finishedAt == 161, "Catch-up/repeated finish cannot change locked results");
        var failure = new RailRunState(); failure.players.Add(new RailParticipant { id = "crew", down = true }); failure.Finish(200, "Bridge collision", Array.Empty<string>());
        Check(!failure.success && !failure.players.Any(p => p.escaped) && failure.players[0].down, "Crash produces failure without reviving anyone");
        var options = RailSessionRules.Options(RailSessionRules.ExpeditionKind);
        Check((float)options.CustomRoomProperties[RailSessionRules.Fuel] == 10, "Default expedition begins with ten fuel");
        foreach (string name in new[] { "RailCoal", "RailBat", "RailBag", "RailWalkie", "RailHelmet", "RailGoggles", "RailFlashlight" })
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Rails/" + name + ".prefab");
            Check(prefab != null && prefab.GetComponent<RailCargo>() != null && prefab.GetComponent<Photon.Pun.PhotonView>() != null, name + " uses shared cargo/ownership");
        }
        var graph = Game.AI.CrawlGraph.Load(AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Scenes/Compound_CrawlGraph.bytes"));
        Check(graph != null && graph.NodeCount > 100, "Authored depot has a baked crawlgraph");
        Check(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Rails/RailCoal.prefab").GetComponent<Photon.Pun.PhotonView>().OwnershipTransfer == Photon.Pun.OwnershipOption.Takeover,
            "Coal follows the existing grabbable ownership contract");
        state.consumed.Add(20); state.consumedSpawns.Add("first-spawn");
        Check(state.WasConsumed(20, "first-spawn") && !state.WasConsumed(20, "reused-view"), "Coal deduplication distinguishes recycled Photon view IDs");
        Check(RailCoal.ValidInsertion(2, 2, "coal", "coal", 3, 3, Vector3.zero, .1), "Remote controller release accepts the same hold generation despite a stale held pose");
        Check(RailCoal.ValidInsertion(2, 2, "coal", "coal", 3, 2, Vector3.up * .4f, .2), "Release may precede the remote grab snapshot");
        Check(!RailCoal.ValidInsertion(2, 3, "coal", "coal", 3, 3, Vector3.zero, .1), "Former controller cannot consume transferred coal");
        Check(!RailCoal.ValidInsertion(2, 2, "old", "new", 3, 3, Vector3.zero, .1), "Delayed release cannot consume a recycled view");
        Check(!RailCoal.ValidInsertion(2, 2, "coal", "coal", 3, 4, Vector3.zero, .1), "A newer pickup invalidates the pending insertion");
        Check(!RailCoal.ValidInsertion(2, 2, "coal", "coal", 3, 3, Vector3.right, .1), "Coal outside the furnace is rejected");
        Check(!RailCoal.ValidInsertion(2, 2, "coal", "coal", 3, 3, Vector3.zero, 3), "Expired insertion is rejected");
        Check(!RailCoal.ValidInsertion(2, 2, "coal", "coal", 3, 3, new Vector3(float.NaN, 0, 0), .1), "Invalid insertion geometry is rejected");
        Debug.Log("RAILS WEEK 2 RULES: 24 checks passed");
    }
    static void Check(bool pass, string message) { if (!pass) throw new Exception(message); Debug.Log("RAILS WEEK 2 PASS: " + message); }
}
#endif
