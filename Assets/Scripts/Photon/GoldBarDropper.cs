#if DEVELOPMENT_BUILD || UNITY_EDITOR
using Photon.Pun;
using UnityEngine;

/// <summary>
/// Clicking the left thumbstick (L3) drops a gold bar on the ground in front of the player. Admins only.
/// </summary>
public class GoldBarDropper : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        if (FindAnyObjectByType<GoldBarDropper>() != null)
            return;

        var gameObject = new GameObject(nameof(GoldBarDropper));
        DontDestroyOnLoad(gameObject);
        gameObject.AddComponent<GoldBarDropper>();
    }

    void Update()
    {
        PlayerSession session = PlayerSession.Instance;
        if (session == null || !session.IsAdmin)
            return;

        if (OVRInput.GetDown(OVRInput.Button.PrimaryThumbstick, OVRInput.Controller.LTouch))
            Drop();
    }

    public static void Drop()
    {
        if (!PhotonNetwork.InRoom)
            return;

        Camera cam = Camera.main;
        if (cam == null)
            return;

        Vector3 forward = Vector3.ProjectOnPlane(cam.transform.forward, Vector3.up).normalized;
        Vector3 position = cam.transform.position + forward * 1f;
        if (Physics.Raycast(position, Vector3.down, out RaycastHit hit, 10f, ~0, QueryTriggerInteraction.Ignore))
            position = hit.point + Vector3.up * 0.1f;

        Quaternion rotation = Quaternion.LookRotation(forward);
        GameObject bar = PhotonNetwork.IsMasterClient
            ? PhotonNetwork.InstantiateRoomObject("ItemGoldBar", position, rotation)
            : PhotonNetwork.InstantiateWithRoomLifetime("ItemGoldBar", position, rotation);

        if (PhotonItemManager.instance != null)
            bar.transform.SetParent(PhotonItemManager.instance.itemParent, true);
    }
}
#endif
