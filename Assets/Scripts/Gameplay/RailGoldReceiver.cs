using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using TMPro;
using EasyTextEffects.Effects;
using UnityEngine;

/// <summary>One-way gold intake. The expedition snapshot commits consumption and reward eligibility together.</summary>
public sealed class RailGoldReceiver : MonoBehaviour, IOnEventCallback
{
    const byte RewardEffectEvent = 109;
    public int receiverId; // 1 departure shop, 2 midpoint shop
    public BoxCollider intake;
    public TMP_Text label;
    public CoinShower coins;
    [Header("Duckz '+N' reward text")]
    public Transform goldTextSpawn;
    public TMP_FontAsset goldTextFont;
    public Material goldTextMaterial;
    public TextEffectInstance goldTextEffect;
    static readonly List<RailGoldReceiver> receivers = new List<RailGoldReceiver>();
    readonly Dictionary<string, double> nextRequest = new Dictionary<string, double>();
    readonly HashSet<string> rewards = new HashSet<string>();
    readonly HashSet<string> queuedBars = new HashSet<string>();
    readonly HashSet<string> shownRewards = new HashSet<string>();
    DuckzWallet wallet;
    Game.Audio.SoundEvent depositSound;

    void OnEnable()
    {
        receivers.Add(this);
        PhotonNetwork.AddCallbackTarget(this);
    }
    void OnDisable()
    {
        receivers.Remove(this);
        PhotonNetwork.RemoveCallbackTarget(this);
        if (wallet != null) wallet.OnRewardCredited -= RewardCredited;
        wallet = null;
    }
    void Update()
    {
        if (wallet == null && DuckzWallet.Instance != null)
        { wallet = DuckzWallet.Instance; wallet.OnRewardCredited += RewardCredited; }
        if (label != null) label.text = "SELL GOLD\n+100 coins per bar";
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
        var bar = item.GetComponent<ItemGoldBar>();
        if (bar.DepositSound != null) depositSound = bar.DepositSound; // remembered: the bar is gone by the time the credit lands
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
        player.gold++; // superlatives count the depositor, not recipients
        if (view.InstantiationData is object[] data && data.Length >= 3 && data[data.Length - 1] is string marker)
            state.consumedMarkers.Add(marker);
        // Like Duckz, only the depositor is paid (CoinsPerBar per bar); nobody else shares the sale.
        var depositor = PhotonNetwork.CurrentRoom?.GetPlayer(player.actor);
        state.gold.Add(new RailGoldDeposit { token = token, receiver = id,
            recipients = depositor != null && !string.IsNullOrEmpty(depositor.UserId)
                ? new[] { depositor.UserId } : new string[0] });
    }

    public static string Receipt(string run, string token, string player)
    {
        using var sha = SHA256.Create();
        return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(run + "|" + token + "|" + player))).Replace("-", "").ToLowerInvariant();
    }
    public static bool Eligible(RailRunState state, RailGoldDeposit deposit, string player) =>
        deposit.recipients != null && deposit.recipients.Contains(player);

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
        if (!rewards.Contains(receipt) || amount <= 0 || !shownRewards.Add(receipt)) return;
        // Runs before the wallet's OnChanged event so the watch counts up as coins land.
        ShowText(amount);
        if (coins != null) coins.Play(amount);
        // Same deposit sound Duckz plays at the crate, heard by everyone nearby.
        if (depositSound != null) RoomRpc.PlaySound(depositSound, transform.position);
        // Only the depositor is ever credited; observers render that stream into the depositor,
        // without touching any wallet or watch count.
        if (PhotonNetwork.InRoom)
            PhotonNetwork.RaiseEvent(RewardEffectEvent,
                new object[] { PhotonSession.RunId, receiverId, receipt, amount },
                new RaiseEventOptions { Receivers = ReceiverGroup.Others }, SendOptions.SendReliable);
    }

    public void OnEvent(EventData ev)
    {
        if (ev.Code != RewardEffectEvent || !PhotonSession.IsRailsRoom ||
            !(ev.CustomData is object[] data) || data.Length != 4 ||
            !(data[0] is string run) || run != PhotonSession.RunId ||
            !(data[1] is int receiver) || receiver != receiverId ||
            !(data[2] is string receipt) || receipt.Length != 64 ||
            !(data[3] is int amount) || amount <= 0 || amount > DuckzWallet.MaxCoins ||
            ev.Sender == PhotonNetwork.LocalPlayer?.ActorNumber) return;
        var player = PhotonPlayer.ForActor(ev.Sender);
        if (player == null || coins == null || !shownRewards.Add(receipt)) return;
        // This transient, uncached event is presentation only: never award money here.
        ShowText(amount);
        coins.PlayTo(amount, player);
    }

    void ShowText(int amount) =>
        GoldRewardText.Show($"+{amount}", goldTextSpawn != null ? goldTextSpawn.position : transform.position + Vector3.up * 0.5f,
            goldTextFont, goldTextMaterial, goldTextEffect);
}
