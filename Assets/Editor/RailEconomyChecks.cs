#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Photon.Pun;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class RailEconomyChecks
{
    [MenuItem("Duck Rails/Run Economy Checks")]
    public static void Run()
    {
        var lines = new List<string>();
        void Check(bool ok, string message) { if (!ok) throw new Exception(message); lines.Add("PASS " + message); }
        var state = new RailRunState { run = "economy-check" };
        state.players.Add(new RailParticipant { id = "winner", down = true });
        state.players.Add(new RailParticipant { id = "left-behind" });
        var gold = new RailGoldDeposit { token = "bar", safe = true, receiver = 0 };
        state.gold.Add(gold);
        Check(state.SafeBars == 1 && !RailGoldReceiver.Eligible(state, gold, "winner"), "Safe retains one unpaid bar during run");
        state.Finish(100, "Bridge crossed", new[] { "winner" });
        Check(RailGoldReceiver.Eligible(state, gold, "winner"), "Downed escaped passenger receives safe payout");
        Check(!RailGoldReceiver.Eligible(state, gold, "left-behind"), "Left-behind player receives no safe payout");
        var copy = JsonUtility.FromJson<RailRunState>(JsonUtility.ToJson(state));
        Check(copy.SafeBars == 1 && RailGoldReceiver.Eligible(copy, copy.gold[0], "winner"), "Reconnect and host reconstruction retain safe and eligibility");
        copy.Finish(110, "Catch-up", new[] { "left-behind" });
        Check(!RailGoldReceiver.Eligible(copy, copy.gold[0], "left-behind"), "Catch-up cannot grant safe payout");
        var failure = new RailRunState(); failure.gold.Add(gold); failure.players.Add(new RailParticipant { id = "winner" });
        failure.Finish(100, "Bridge collision", Array.Empty<string>());
        Check(!RailGoldReceiver.Eligible(failure, gold, "winner"), "Failure pays no safe gold");
        var shop = new RailGoldDeposit { token = "sold", safe = false, recipients = new[] { "winner", "left-behind" } };
        Check(RailGoldReceiver.Eligible(failure, shop, "winner") && RailGoldReceiver.Eligible(failure, shop, "left-behind"), "Shop proceeds survive failure for all deposit-time recipients");
        Check(!RailGoldReceiver.Eligible(state, shop, "late-arrival"), "Late join cannot claim previous shop deposits");
        string receipt = RailGoldReceiver.Receipt("run", "bar", "winner");
        Check(receipt.Length == 64 && receipt == RailGoldReceiver.Receipt("run", "bar", "winner"), "Reward receipt stable across retries");
        Check(receipt != RailGoldReceiver.Receipt("run2", "bar", "winner") && receipt != RailGoldReceiver.Receipt("run", "bar", "other"), "Receipts separate runs and recipients");
        Check(new RailRunState().SafeBars == 0, "Loose or held bars are absent from safe accounting");
        var catalog = AssetDatabase.LoadAssetAtPath<StoreCatalog>(RailEconomyBuilder.Folder + "RailStoreCatalog.asset");
        Check(catalog != null && catalog.items.Select(i => i.key).OrderBy(k => k).SequenceEqual(RailEconomyBuilder.Keys.OrderBy(k => k)), "Exactly seven standard gear types");
        Check(catalog.items.All(i => !i.IsDurable && !i.InBundle && i.payment == StorePayment.Coins), "All shop purchases are repeatable gameplay goods");
        foreach (var item in catalog.items.Where(i => i.prefab != null))
        {
            var cargo = item.prefab.GetComponent<RailCargo>(); var view = item.prefab.GetComponent<PhotonView>();
            Check(cargo != null && view.ObservedComponents.Contains(cargo), item.key + " replicates loose train cargo");
        }
        var shopPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(RailEconomyBuilder.Folder + "RailShop.prefab");
        Check(shopPrefab.GetComponentsInChildren<StoreWallItem>(true).Length == 7, "One Duckz pull-to-buy display per item");
        foreach (var block in Directory.GetFiles("Assets/DuckRails/Blocks", "*.prefab").Select(AssetDatabase.LoadAssetAtPath<RailBlock>).Where(b => b.id != "bridge-finale-v1"))
            Check(block.variations.All(v => v.gold.Length == 2 && v.gold.All(p => p != null && p.GetComponent<RailGoldSpawner>() != null)), block.id + " two editable gold spawners in every variation");
        var preview = EditorSceneManager.NewPreviewScene();
        try
        {
            var safe = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(RailEconomyBuilder.Folder + "TrainSafe.prefab"), preview);
            var receiver = safe.GetComponent<RailGoldReceiver>();
            Check(receiver.isSafe && receiver.coins != null && receiver.label != null, "Safe has count label and Duckz coin shower");
            Check(receiver.Accepts(receiver.intake.center) && !receiver.Accepts(new Vector3(0, -.1f, 0)), "Safe intake accepts interior and excludes deck below");
            Check(safe.GetComponent<CrateGoldBar>() == null, "Safe cannot use immediate Duckz deposit path");
        }
        finally { EditorSceneManager.ClosePreviewScene(preview); }
        Directory.CreateDirectory("Temp"); File.WriteAllLines("Temp/rails-economy-checks.txt", lines);
        Debug.Log("ECONOMY CHECKS: " + lines.Count + " passed");
    }
}
#endif
