using UnityEngine;
using Game.Audio;

public class ItemGoldBar : MonoBehaviour
{
    [SerializeField] SoundEvent depositSound;

    public SoundEvent DepositSound => depositSound;

    private void Start() {
        // Rails owns authored gold spawning and cargo lifetime; no Duckz respawn manager is needed.
        if (PhotonSession.IsRailsRoom) return;
        // Bag retrieval can grab the newly spawned bar before Start runs.
        Grabbable grabbable = GetComponent<Grabbable>();
        if (grabbable == null || (!grabbable.IsHeld && !grabbable.IsStashed)) {
            transform.SetParent(PhotonItemManager.instance.itemParent, true);
        }
        PhotonItemManager.instance.AddGoldBar(gameObject);
    }

}
