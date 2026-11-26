using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;
using TMPro;
using EasyTextEffects.Effects;

public class CrateGoldBar : MonoBehaviour
{
    static readonly List<CrateGoldBar> activeCrates = new List<CrateGoldBar>();

    [Tooltip("World-space start point for the gold reward text")]
    [SerializeField] Transform goldTextSpawn;
    [SerializeField] TMP_FontAsset goldTextFont;
    [SerializeField] Material goldTextMaterial;
    [SerializeField] TextEffectInstance goldTextEffect;
    [SerializeField] CoinShower coinShower;
    [Tooltip("Coins shown in the +N text and flown to the watch per bar. Display only; the server decides the real credit.")]
    [SerializeField] int coinsPerBar = 100;
    [SerializeField] bool compoundDeposit;
    readonly Dictionary<string, Game.Audio.SoundEvent> pendingRewards = new();
    DuckzWallet wallet;

    private void OnEnable() {
        activeCrates.Add(this);
    }

    private void OnDisable() {
        activeCrates.Remove(this);
        if (wallet != null) wallet.OnRewardCredited -= CompoundRewardCredited;
        wallet = null;
        pendingRewards.Clear();
    }

    private void OnTriggerEnter(Collider other) {
        if (PhotonSession.IsRailsRoom) return;
        ItemGoldBar gold = other.GetComponent<ItemGoldBar>();
        if (gold != null && gold.enabled) {
            PhotonView pView = other.GetComponent<PhotonView>();
            if (pView.IsMine) { //player holding the bar should already have ownership
                gold.enabled = false; // one deposit per bar even if several colliders enter this frame
                DuckzWallet.Ensure().Deposit(1);
                RoomRpc.PlaySound(gold.DepositSound, transform.position);
                RoomRpc.GoldDeposited(transform.position, coinsPerBar);
                PhotonNetwork.Destroy(other.gameObject);
            }
        }
    }

    void OnTriggerStay(Collider other)
    {
        if (!compoundDeposit || !PhotonSession.IsRailsRoom || PhotonSession.OnTrain ||
            PhotonSession.Instance?.WorldReady != true) return;

        var gold = other.GetComponentInParent<ItemGoldBar>();
        var view = gold != null ? gold.GetComponent<PhotonView>() : null;
        var item = gold != null ? gold.GetComponent<PhotonGrabbable>() : null;
        string user = PhotonNetwork.LocalPlayer?.UserId;
        if (view == null || !view.IsMine || view.ViewID == 0 || item == null || item.IsStashed ||
            !gold.enabled || string.IsNullOrEmpty(user) || string.IsNullOrEmpty(view.SpawnToken)) return;

        if (wallet == null)
        {
            wallet = DuckzWallet.Ensure();
            wallet.OnRewardCredited += CompoundRewardCredited;
        }

        string receipt = RailGoldReceiver.Receipt("compound", view.SpawnToken, user);
        if (!wallet.QueueReward(receipt, 1)) return;
        pendingRewards[receipt] = gold.DepositSound;
        gold.enabled = false;
        PhotonNetwork.Destroy(view.gameObject);
    }

    void CompoundRewardCredited(string receipt, int amount)
    {
        if (!pendingRewards.Remove(receipt, out var sound) || amount <= 0 ||
            !PhotonSession.IsRailsRoom || PhotonSession.OnTrain) return;
        RoomRpc.PlaySound(sound, transform.position);
        RoomRpc.GoldDeposited(transform.position, amount);
    }

    /// <summary>depositor is the Photon actor number of whoever deposited; their coins fly to their watch, or to their body for everyone else.</summary>
    public static void PlayDepositFx(Vector3 position, int coins, int depositor) {
        CrateGoldBar crate = FindNearest(position);
        if (crate != null) {
            crate.PlayDepositFx(coins, depositor);
        }
    }

    static CrateGoldBar FindNearest(Vector3 position) {
        CrateGoldBar nearest = null;
        float nearestDist = float.MaxValue;
        foreach (CrateGoldBar crate in activeCrates) {
            float dist = (crate.transform.position - position).sqrMagnitude;
            if (dist < nearestDist) {
                nearestDist = dist;
                nearest = crate;
            }
        }
        return nearest;
    }

    void PlayDepositFx(int coins, int depositor) {
        GoldRewardText.Show($"+{coins}", GetGoldTextPosition(), goldTextFont, goldTextMaterial, goldTextEffect);
        if (coinShower == null) {
            return;
        }
        if (depositor == PhotonNetwork.LocalPlayer.ActorNumber) {
            coinShower.Play(coins);
        } else {
            coinShower.PlayTo(coins, PhotonPlayer.ForActor(depositor));
        }
    }

    Vector3 GetGoldTextPosition() {
        return goldTextSpawn != null ? goldTextSpawn.position : transform.position + Vector3.up * 0.5f;
    }

#if UNITY_EDITOR
    [ContextMenu("Debug: Play deposit effect")]
    void DebugPlayDepositFx() {
        RoomRpc.GoldDeposited(transform.position, coinsPerBar);
    }
#endif
}
