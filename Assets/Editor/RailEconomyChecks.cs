#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Photon.Pun;
using UnityEditor;
using UnityEngine;

public static class RailEconomyChecks
{
    [MenuItem("Duck Rails/Legacy/Run Economy Checks")]
    public static void Run()
    {
        var lines = new List<string>();
        void Check(bool ok, string message) { if (!ok) throw new Exception(message); lines.Add("PASS " + message); }
        var state = new RailRunState { run = "economy-check" };
        state.players.Add(new RailParticipant { id = "winner", down = true });
        state.players.Add(new RailParticipant { id = "left-behind" });
        var shop = new RailGoldDeposit { token = "sold", receiver = 1, recipients = new[] { "winner", "left-behind" } };
        state.gold.Add(shop);
        state.Finish(100, "Bridge crossed", new[] { "winner" });
        Check(RailGoldReceiver.Eligible(state, shop, "winner") && RailGoldReceiver.Eligible(state, shop, "left-behind"), "Shop proceeds stay with the players connected at the sale");
        var copy = JsonUtility.FromJson<RailRunState>(JsonUtility.ToJson(state));
        Check(copy.gold.Count == 1 && copy.gold[0].recipients.Length == 2 && RailGoldReceiver.Eligible(copy, copy.gold[0], "left-behind"), "Reconnect and host reconstruction retain shop recipients");
        var failure = new RailRunState(); failure.gold.Add(shop); failure.players.Add(new RailParticipant { id = "winner" });
        failure.Finish(100, "Bridge collision", Array.Empty<string>());
        Check(RailGoldReceiver.Eligible(failure, shop, "winner") && RailGoldReceiver.Eligible(failure, shop, "left-behind"), "Shop proceeds survive failure for all deposit-time recipients");
        Check(!RailGoldReceiver.Eligible(state, shop, "late-arrival"), "Late join cannot claim previous shop deposits");
        string receipt = RailGoldReceiver.Receipt("run", "bar", "winner");
        Check(receipt.Length == 64 && receipt == RailGoldReceiver.Receipt("run", "bar", "winner"), "Reward receipt stable across retries");
        Check(receipt != RailGoldReceiver.Receipt("run2", "bar", "winner") && receipt != RailGoldReceiver.Receipt("run", "bar", "other"), "Receipts separate runs and recipients");
        Check(new RailRunState().gold.Count == 0, "A fresh run has no gold deposits");
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
        foreach (var display in shopPrefab.GetComponentsInChildren<StoreWallItem>(true))
        {
            var serialized = new SerializedObject(display);
            var label = serialized.FindProperty("label").objectReferenceValue as Component;
            Check(label == null || label.transform.IsChildOf(shopPrefab.transform), display.name + " price cannot reference the source store asset");
            Check(display.transform.localPosition.y <= .75f, display.name + " lowered shelf height");
        }
        foreach (var block in Directory.GetFiles("Assets/DuckRails/Blocks", "*.prefab").Select(AssetDatabase.LoadAssetAtPath<RailBlock>).Where(b => b.id != "bridge-finale-v1"))
            Check(block.variations.All(v => v.gold.Length == 2 && v.gold.All(p => p != null && p.GetComponent<RailGoldSpawner>() != null)), block.id + " two editable gold spawners in every variation");
        var receiver = shopPrefab.GetComponentInChildren<RailGoldReceiver>(true);
        Check(receiver != null && receiver.coins != null && receiver.label != null && receiver.intake != null, "Shop gold receiver has intake, label and coin shower");
        Check(receiver.Accepts(receiver.intake.center), "Shop intake accepts its center");
        Check(shopPrefab.GetComponentInChildren<CrateGoldBar>(true) == null, "Shop cannot use the immediate Duckz deposit path");
        Directory.CreateDirectory("Temp"); File.WriteAllLines("Temp/rails-economy-checks.txt", lines);
        Debug.Log("ECONOMY CHECKS: " + lines.Count + " passed");
    }
}
#endif
