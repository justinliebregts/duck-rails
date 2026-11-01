using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using TMPro;
using UnityEngine;

/// <summary>One-way gold intake. The expedition snapshot commits consumption and reward eligibility together.</summary>
public sealed class RailGoldReceiver : MonoBehaviour
{
    public int receiverId; // 0 safe, 1 departure shop, 2 midpoint shop
    public bool isSafe;
    public BoxCollider intake;
    public TMP_Text label;
    public CoinShower coins;
    static readonly List<RailGoldReceiver> receivers = new List<RailGoldReceiver>();
    readonly Dictionary<string, double> nextRequest = new Dictionary<string, double>();
    readonly HashSet<string> rewards = new HashSet<string>();
    readonly HashSet<string> queuedBars = new HashSet<string>();
    DuckzWallet wallet;

    void OnEnable() => receivers.Add(this);
    void OnDisable()
    {
        receivers.Remove(this);
        if (wallet != null) wallet.OnRewardCredited -= RewardCredited;
        wallet = null;
    }
    void Update()
    {
        if (wallet == null && DuckzWallet.Instance != null)
        { wallet = DuckzWallet.Instance; wallet.OnRewardCredited += RewardCredited; }
        if (label != null) label.text = isSafe
            ? "SAFE  " + (RailExpedition.Instance?.State.SafeBars ?? 0) + " GOLD BARS\nDeposit only • Paid on escape"
            : "SELL GOLD\nEveryone gets coins";
    }
    void OnTriggerStay(Collider other)
    {
        var item = other.GetComponentInParent<PhotonGrabbable>();
        var view = item != null ? item.GetComponent<PhotonView>() : null;
        if (view == null || !view.IsMine || item.GetComponent<ItemGoldBar>() == null || item.IsStashed ||
            PhotonSession.Phase != RailPhase.Running || RailExpedition.LocalDown || intake == null) return;
        Vector3 sample = intake.transform.InverseTransformPoint(view.transform.position);
        if (!Accepts(sample)) return;
        string token = view.SpawnToken;
        if (string.IsNullOrEmpty(token) || (nextRequest.TryGetValue(token, out var next) && PhotonNetwork.Time < next)) return;
        nextRequest[token] = PhotonNetwork.Time + .5;
        PhotonNetwork.RaiseEvent(108, new object[] { PhotonSession.RunId, "gold", view.ViewID,
            new object[] { token, item.HoldVersion, sample, PhotonNetwork.Time, receiverId } },
            new RaiseEventOptions { Receivers = ReceiverGroup.MasterClient }, SendOptions.SendReliable);
    }
    public bool Accepts(Vector3 local) => intake != null && new Bounds(intake.center, intake.size).Contains(local);

    public static void ApplyDeposit(RailRunState state, RailParticipant player, Vector3 position, int target, object[] sample, double now)
    {
        if (state.finished || player.down || sample == null || sample.Length != 5 || !(sample[0] is string token) ||
            !(sample[1] is int version) || !(sample[2] is Vector3 offset) || !(sample[3] is double sent) || !(sample[4] is int id)) return;
        var receiver = receivers.Find(r => r.receiverId == id);
        var view = PhotonView.Find(target);
        var item = view != null ? view.GetComponent<PhotonGrabbable>() : null;
        if (receiver == null || view == null || item == null || view.GetComponent<ItemGoldBar>() == null ||
            state.WasConsumed(target, view.SpawnToken) || state.gold.Any(g => g.token == token) ||
            string.IsNullOrEmpty(token) || token != view.SpawnToken || player.actor != view.ControllerActorNr ||
            version < item.HoldVersion || now - sent < -.25 || now - sent > 2 || !receiver.Accepts(offset) ||
            Vector3.Distance(position, receiver.transform.position) > 5 || (view.IsMine && item.IsStashed)) return;
        state.consumed.Add(target); state.consumedSpawns.Add(token);
        if (view.InstantiationData is object[] data && data.Length >= 3 && data[data.Length - 1] is string marker)
            state.consumedMarkers.Add(marker);
        state.gold.Add(new RailGoldDeposit { token = token, receiver = id, safe = receiver.isSafe,
            recipients = receiver.isSafe ? Array.Empty<string>() : PhotonNetwork.PlayerList
                .Where(p => !p.IsInactive && !string.IsNullOrEmpty(p.UserId)).Select(p => p.UserId).ToArray() });
    }

    public static string Receipt(string run, string token, string player)
    {
        using var sha = SHA256.Create();
        return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(run + "|" + token + "|" + player))).Replace("-", "").ToLowerInvariant();
    }
    public static bool Eligible(RailRunState state, RailGoldDeposit deposit, string player) => deposit.safe
        ? state.finished && state.success && state.Player(player)?.escaped == true
        : deposit.recipients != null && deposit.recipients.Contains(player);

    public static void PresentRewards(RailExpedition run)
    {
        string player = PhotonNetwork.LocalPlayer?.UserId;
        if (string.IsNullOrEmpty(player) || DuckzWallet.Instance == null) return;
        foreach (var deposit in run.State.gold)
        {
            if (!Eligible(run.State, deposit, player)) continue;
            var receiver = receivers.Find(r => r.receiverId == deposit.receiver);
            if (receiver == null) continue;
            string key = run.State.run + "|" + deposit.token + "|" + player;
            if (receiver.queuedBars.Contains(key)) continue;
            string receipt = Receipt(run.State.run, deposit.token, player);
            receiver.rewards.Add(receipt);
            if (DuckzWallet.Instance.QueueReward(receipt, 1)) receiver.queuedBars.Add(key);
        }
    }
    void RewardCredited(string receipt, int amount)
    {
        if (!rewards.Contains(receipt)) return;
        // Runs before the wallet's OnChanged event so the watch counts up as coins land.
        if (coins != null) coins.Play(amount);
    }
}
