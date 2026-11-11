using System.Collections.Generic;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using Hashtable = ExitGames.Client.Photon.Hashtable;

// Scene-owned, like the effect pool. The room keeps accepted deaths across player departures.
public sealed class PhotonRoomFlares : MonoBehaviourPunCallbacks
{
    const string Prefix = "deathFlare:";
    const int LifetimeMs = (int)((DeathFlare.DeathLaunchDelay + DeathFlare.Lifetime) * 1000);
    readonly Dictionary<string, (int diedAt, Vector3 origin, DeathFlare effect)> records = new();
    readonly List<string> expired = new();
    DeathFlare prefab;
    Room room;
    float nextCleanup;

    public static PhotonRoomFlares Ready(DeathFlare prefab)
    {
        if (prefab == null || !PhotonNetwork.InRoom) return null;
        var pool = DeathEffectPool.Get();
        if (!pool.TryGetComponent(out PhotonRoomFlares sync))
            sync = pool.gameObject.AddComponent<PhotonRoomFlares>();
        if (sync.room != PhotonNetwork.CurrentRoom)
        {
            sync.ClearRoom();
            sync.room = PhotonNetwork.CurrentRoom;
            sync.prefab = prefab;
            sync.Restore(sync.room.CustomProperties);
        }
        return sync;
    }

    public void Receive(int viewId, int diedAt, Vector3 position)
    {
        string key = $"{Prefix}{viewId:X8}{diedAt:X8}";
        if (Remember(key, diedAt, position) && PhotonNetwork.IsMasterClient)
            Publish(key, diedAt, position);
    }

    bool Remember(string key, int diedAt, Vector3 position)
    {
        if (records.ContainsKey(key)) return false;
        int age = unchecked(PhotonNetwork.ServerTimestamp - diedAt);
        if (age >= LifetimeMs || age < -1000) return false;
        var effect = DeathFlare.SpawnAfterDeath(prefab, position, Mathf.Max(0, age) / 1000f);
        if (effect == null) return false;
        effect.RoomEventKey = key;
        records.Add(key, (diedAt, position, effect));
        return true;
    }

    void Publish(string key, int diedAt, Vector3 position) =>
        room.SetCustomProperties(new Hashtable { [key] = new object[] { diedAt, position } });

    public override void OnRoomPropertiesUpdate(Hashtable changed)
    {
        if (room != null && room == PhotonNetwork.CurrentRoom) Restore(changed);
    }

    void Restore(Hashtable properties)
    {
        Hashtable remove = null;
        foreach (var entry in properties)
        {
            if (!(entry.Key is string key) || !key.StartsWith(Prefix, System.StringComparison.Ordinal)
                || !(entry.Value is object[] data) || data.Length != 2
                || !(data[0] is int diedAt) || !(data[1] is Vector3 position)) continue;
            if (!Remember(key, diedAt, position) && !records.ContainsKey(key) && PhotonNetwork.IsMasterClient)
                (remove ??= new Hashtable())[key] = null;
        }
        if (remove != null) room.SetCustomProperties(remove);
    }

    public override void OnMasterClientSwitched(Player master)
    {
        if (!master.IsLocal || room == null || room != PhotonNetwork.CurrentRoom) return;
        // The previous master may have left between receiving the RPC and publishing it.
        foreach (var entry in records)
            if (!room.CustomProperties.ContainsKey(entry.Key)
                && unchecked(PhotonNetwork.ServerTimestamp - entry.Value.diedAt) < LifetimeMs)
                Publish(entry.Key, entry.Value.diedAt, entry.Value.origin);
        Restore(room.CustomProperties);
    }

    void Update()
    {
        if (records.Count == 0 || room != PhotonNetwork.CurrentRoom || Time.unscaledTime < nextCleanup) return;
        nextCleanup = Time.unscaledTime + 1f;
        foreach (var entry in records)
            if (unchecked(PhotonNetwork.ServerTimestamp - entry.Value.diedAt) >= LifetimeMs) expired.Add(entry.Key);
        Hashtable remove = null;
        foreach (string key in expired)
        {
            Release(key, records[key].effect);
            records.Remove(key);
            if (PhotonNetwork.IsMasterClient) (remove ??= new Hashtable())[key] = null;
        }
        expired.Clear();
        if (remove != null) room.SetCustomProperties(remove);
    }

    public override void OnLeftRoom() => ClearRoom();
    public override void OnDisconnected(DisconnectCause cause) => ClearRoom();

    void ClearRoom()
    {
        room = null;
        foreach (var entry in records) Release(entry.Key, entry.Value.effect);
        records.Clear();
    }

    static void Release(string key, DeathFlare effect)
    {
        if (effect != null && effect.RoomEventKey == key && effect.gameObject.activeSelf)
            DeathEffectPool.Get().Release(effect.gameObject);
    }
}
