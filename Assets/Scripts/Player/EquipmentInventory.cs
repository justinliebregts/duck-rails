using Photon.Pun;
using UnityEngine;

// Local, per-account persistence. World objects and their worn state are replicated by Photon.
public sealed class EquipmentInventory : MonoBehaviourPunCallbacks
{
    const string HeadKey = "equipment.head";
    const string EyesKey = "equipment.eyes";
    public static EquipmentInventory Local { get; private set; }
    readonly bool[] restored = new bool[2];
    readonly string[] pendingKeys = new string[2];

    static string SaveKey(HeadEquipment.EquipmentSlot slot) =>
        slot == HeadEquipment.EquipmentSlot.Eyes ? EyesKey : HeadKey;

    void Awake() => Local = this;
    void OnDestroy() { if (Local == this) Local = null; }

    public override void OnLeftRoom()
    {
        for (int i = 0; i < restored.Length; i++)
        {
            restored[i] = false;
            pendingKeys[i] = null;
        }
    }

    void Update()
    {
        if (PhotonSession.IsRailsRoom) return;
        if (!PhotonNetwork.InRoom) return;
        var player = GR.Player.instance;
        if (!UserPlayerPrefs.IsReady || player == null || player.IsDead ||
            player.helmetAnchor == null || PhotonPlayer.Local == null) return;
        if (restored[0] && restored[1]) return;
        if (!MigrateLegacySave()) return;
        RestoreSlot(HeadEquipment.EquipmentSlot.Head, player.helmetAnchor);
        RestoreSlot(HeadEquipment.EquipmentSlot.Eyes, player.helmetAnchor);
    }

    // The old head key could contain goggles. Resolve the prefab before moving that save.
    static bool MigrateLegacySave()
    {
        string key = UserPlayerPrefs.GetString(HeadKey, "");
        if (string.IsNullOrEmpty(key)) return true;
        GameObject prefab = BagObject.ResolvePrefab(key);
        if (prefab == null) return false;
        var equipment = prefab.GetComponent<HeadEquipment>();
        if (equipment != null && equipment.Slot == HeadEquipment.EquipmentSlot.Eyes)
        {
            if (string.IsNullOrEmpty(UserPlayerPrefs.GetString(EyesKey, "")))
                UserPlayerPrefs.SetString(EyesKey, key);
            UserPlayerPrefs.DeleteKey(HeadKey);
            UserPlayerPrefs.Save();
        }
        return true;
    }

    void RestoreSlot(HeadEquipment.EquipmentSlot slot, Transform anchor)
    {
        int index = (int)slot;
        if (restored[index]) return;
        foreach (var item in anchor.GetComponentsInChildren<HeadEquipment>(true))
        {
            if (!item.IsLocallyEquipped || item.Slot != slot) continue;
            restored[index] = true;
            return;
        }
        string key = UserPlayerPrefs.GetString(SaveKey(slot), "");
        if (string.IsNullOrEmpty(key)) { restored[index] = true; return; }
        if (RoomObjectRecovery.Exists(UserPlayerPrefs.GetString(SaveKey(slot) + ".room", ""),
            UserPlayerPrefs.GetString(SaveKey(slot) + ".identity", "")))
        { restored[index] = true; return; }
        // Registration/catalog initialization may finish after the local rig starts.
        GameObject prefab = BagObject.ResolvePrefab(key);
        if (prefab == null) return;
        restored[index] = true;
        var equipment = prefab.GetComponent<HeadEquipment>();
        if (equipment == null || equipment.Slot != slot) return;
        pendingKeys[index] = key;
        RoomRpc.SpawnEquipment(key, anchor.position, anchor.rotation);
    }

    public bool AcceptRestore(HeadEquipment item)
    {
        int index = (int)item.Slot;
        if (pendingKeys[index] != PrefabKey(item)) return false;
        pendingKeys[index] = null;
        return GR.Player.instance != null && !GR.Player.instance.IsDead;
    }

    public static string PrefabKey(HeadEquipment item)
    {
        string key = item.name;
        int suffix = key.IndexOf('(');
        return (suffix > 0 ? key.Substring(0, suffix) : key).Trim();
    }

    public static void Save(HeadEquipment item)
    {
        if (Local != null)
        {
            Local.restored[(int)item.Slot] = true;
            Local.pendingKeys[(int)item.Slot] = null;
        }
        if (!UserPlayerPrefs.IsReady) return;
        MigrateLegacySave();
        UserPlayerPrefs.SetString(SaveKey(item.Slot), PrefabKey(item));
        UserPlayerPrefs.SetString(SaveKey(item.Slot) + ".room", PhotonNetwork.CurrentRoom?.Name ?? "");
        UserPlayerPrefs.SetString(SaveKey(item.Slot) + ".identity", item.GetComponent<PhotonGrabbable>()?.RecoveryIdentity ?? "");
        UserPlayerPrefs.Save();
    }

    public static void Clear(HeadEquipment.EquipmentSlot slot)
    {
        if (Local != null)
        {
            Local.pendingKeys[(int)slot] = null;
            Local.restored[(int)slot] = true;
        }
        if (!UserPlayerPrefs.IsReady) return;
        MigrateLegacySave();
        UserPlayerPrefs.DeleteKey(SaveKey(slot));
        UserPlayerPrefs.Save();
    }

    // Death clears both slots and cancels in-flight restore requests.
    public static void Clear()
    {
        if (Local != null)
        {
            for (int i = 0; i < Local.restored.Length; i++)
            {
                Local.pendingKeys[i] = null;
                Local.restored[i] = true;
            }
        }
        if (!UserPlayerPrefs.IsReady) return;
        UserPlayerPrefs.DeleteKey(HeadKey);
        UserPlayerPrefs.DeleteKey(EyesKey);
        UserPlayerPrefs.Save();
    }
}
