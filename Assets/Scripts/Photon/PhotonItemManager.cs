using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using System.Collections.Generic;
using Hashtable = ExitGames.Client.Photon.Hashtable;

public class PhotonItemManager : MonoBehaviourPunCallbacks
{
    public static PhotonItemManager instance;
    public Transform itemParent;
    public int respawnInterval = 30;
    public float occupiedRadius = 3f; //Don't respawn within this radius if there is already another gold bar here

    [Header("===Gold Bars===")]
    public Transform goldBarSpawnParent;
    public int maxGoldBars = 40;
    Transform[] goldBarSpawnPositions;
    [SerializeField] List<GameObject> spawnedGoldBars = new List<GameObject>();

    const string ScheduleProperty = "goldRespawns";
    const string InitializedProperty = "goldInitialized";
    readonly List<int> respawnAt = new List<int>();
    bool initialSpawn, wasMaster;
    double nextCheck;
    Room room;

    private void Awake() {
        instance = this;
    }

    void Start()
    {
        CacheSpawnPositions();
        HideSpawnPreviews();
    }

    void HideSpawnPreviews()
    {
        for (int i = 0; i < goldBarSpawnParent.childCount; i++)
        {
            Transform spawn = goldBarSpawnParent.GetChild(i);
            for (int c = 0; c < spawn.childCount; c++)
                spawn.GetChild(c).gameObject.SetActive(false);
        }
    }

    void Update()
    {
        if (!PhotonNetwork.InRoom || goldBarSpawnPositions == null || goldBarSpawnPositions.Length == 0) {
            wasMaster = false;
            room = null;
            return;
        }
        bool master = PhotonNetwork.IsMasterClient;
        if (!ReferenceEquals(room, PhotonNetwork.CurrentRoom) || (master && !wasMaster))
            RestoreSchedule();
        wasMaster = master;
        if (!master || Time.realtimeSinceStartupAsDouble < nextCheck) return;
        nextCheck = Time.realtimeSinceStartupAsDouble + 0.1;
        TickSpawning(PhotonNetwork.ServerTimestamp);
    }

    public override void OnMasterClientSwitched(Player newMasterClient)
    {
        wasMaster = false;
        // Let already received instantiations finish registering before replenishing.
        nextCheck = Time.realtimeSinceStartupAsDouble + 0.25;
    }

    void RestoreSchedule()
    {
        room = PhotonNetwork.CurrentRoom;
        initialSpawn = room.CustomProperties.TryGetValue(InitializedProperty, out object initialized) && initialized is bool done && done;
        respawnAt.Clear();
        if (room.CustomProperties.TryGetValue(ScheduleProperty, out object value) && value is int[] deadlines)
            respawnAt.AddRange(deadlines);
        // Also includes objects received this frame whose Start has not run yet.
        spawnedGoldBars.Clear();
        foreach (ItemGoldBar bar in FindObjectsByType<ItemGoldBar>(FindObjectsSortMode.None)) AddGoldBar(bar.gameObject);
        nextCheck = Time.realtimeSinceStartupAsDouble + 0.25;
    }

    void SaveSchedule()
    {
        room.SetCustomProperties(new Hashtable {
            { InitializedProperty, initialSpawn }, { ScheduleProperty, respawnAt.ToArray() }
        });
    }

    void TickSpawning(int now)
    {
        int target = Mathf.Min(maxGoldBars, goldBarSpawnPositions.Length);
        int missing = Mathf.Max(0, target - LiveGoldBarCount);
        if (!initialSpawn)
        {
            if (missing > 0) SpawnGoldBar();
            if (LiveGoldBarCount >= target) { initialSpawn = true; SaveSchedule(); }
            return;
        }

        // Derive missed destructions from the live room objects. This also recovers a
        // host that stopped before publishing its timer, without duplicating bars.
        bool changed = ReconcileRespawns(missing, now);
        for (int i = 0; i < respawnAt.Count; i++)
        {
            if (unchecked(now - respawnAt[i]) < 0) continue;
            if (SpawnGoldBar()) { respawnAt.RemoveAt(i); changed = true; }
            break; // Keep the original staggered spawning; no frame-sized refill burst.
        }
        if (changed) SaveSchedule();
    }

    bool ReconcileRespawns(int missing, int now)
    {
        bool changed = respawnAt.Count != missing;
        // Extra bars (e.g. player drops) satisfy the oldest outstanding replacements.
        while (respawnAt.Count > missing) respawnAt.RemoveAt(0);
        while (respawnAt.Count < missing)
            respawnAt.Add(unchecked(now + Mathf.Max(0, respawnInterval) * 1000));
        return changed;
    }

    void CacheSpawnPositions() {
        int count = goldBarSpawnParent.childCount;
        goldBarSpawnPositions = new Transform[count];
        for (int i = 0; i < count; i++) {
            goldBarSpawnPositions[i] = goldBarSpawnParent.GetChild(i);
        }
    }

    bool SpawnGoldBar() {
        if (!PhotonNetwork.InRoom || !PhotonNetwork.IsMasterClient ||
            LiveGoldBarCount >= maxGoldBars || goldBarSpawnPositions.Length == 0) return false;

        Transform spawn = PickFreeSpawnPosition();
        GameObject go;
        if (spawn == null) {
            // Fall back to any spawn point to maintain maxGoldBars.
            int rand = Random.Range(0, goldBarSpawnPositions.Length);
            spawn = goldBarSpawnPositions[rand];
            go = PhotonNetwork.InstantiateRoomObject("ItemGoldBar", spawn.position, spawn.rotation);
        } else {
            go = PhotonNetwork.InstantiateRoomObject("ItemGoldBar", spawn.position, spawn.rotation);
        }
        if (go == null) return false;
        AddGoldBar(go);
        go.transform.SetParent(itemParent);
        return true;
    }

    Transform PickFreeSpawnPosition()
    {
        List<Transform> freeSpots = new List<Transform>();
        for (int i = 0; i < goldBarSpawnPositions.Length; i++) {
            if (IsSpawnFree(goldBarSpawnPositions[i])) {
                freeSpots.Add(goldBarSpawnPositions[i]);
            }
        }
        if (freeSpots.Count == 0) {

            return null;
        }

        int pick = Random.Range(0, freeSpots.Count);
        return freeSpots[pick];
    }

    bool IsSpawnFree(Transform spawn)
    {
        Vector3 point = spawn.position;
        for (int i = 0; i < spawnedGoldBars.Count; i++) {
            GameObject bar = spawnedGoldBars[i];
            if (bar == null) {
                continue;
            }
            if (Vector3.Distance(bar.transform.position, point) <= occupiedRadius) {
                return false;
            }
        }
        return true;
    }

    public void AddGoldBar(GameObject go) {
        // Register immediately on the host; ItemGoldBar.Start also runs on every client.
        if (go != null && !spawnedGoldBars.Contains(go)) {
            spawnedGoldBars.Add(go);
        }
    }

    /// <summary>Live gold bars tracked on this peer. Prefer master client for room-wide economy pulses.</summary>
    public int LiveGoldBarCount
    {
        get
        {
            spawnedGoldBars.RemoveAll(bar => bar == null);
            return spawnedGoldBars.Count;
        }
    }
}
