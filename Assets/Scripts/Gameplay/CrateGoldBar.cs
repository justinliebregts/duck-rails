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

    private void OnEnable() {
        activeCrates.Add(this);
    }

    private void OnDisable() {
        activeCrates.Remove(this);
    }

    private void OnTriggerEnter(Collider other) {
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
