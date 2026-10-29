using Photon.Pun;
using UnityEngine;

public sealed class DuckCompanionBootstrap : MonoBehaviour
{
    GameObject prefab;
    DuckCompanion knownCompanion;
    float nextCheck;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        if (FindAnyObjectByType<DuckCompanionBootstrap>() != null) return;
        var go = new GameObject("Duck companion experiment");
        DontDestroyOnLoad(go);
        go.AddComponent<DuckCompanionBootstrap>();
    }
    void Awake()
    {
#if UNITY_EDITOR
        gameObject.AddComponent<DuckCompanionDebug>();
#endif
        prefab = Resources.Load<GameObject>("DuckCompanion");
        PhotonPreparePool.Register(prefab);
    }
    void Update()
    {
        if (Time.time < nextCheck) return;
        nextCheck = Time.time + 1f;
        if (!PhotonNetwork.InRoom || GameManager.instance == null) return;
        CheckCompanion(PhotonNetwork.IsMasterClient);
    }

    void CheckCompanion(bool isMaster)
    {
        var existing = FindCompanion();
        if (isMaster && GameManager.instance.disableDuckCompanion)
        {
            if (existing != null) PhotonNetwork.Destroy(existing.gameObject);
            return;
        }
        if (existing != null)
        {
            DuckOfflineRecognition.Ensure();
            return;
        }
        if (!isMaster || PhotonPlayer.Local == null || prefab == null) return;
        if (!GameManager.instance.TryGetCompanionSpawnPose(out Vector3 position, out Quaternion rotation)) return;
        // PUN creates the local instance synchronously. Retain it immediately,
        // including while its registry/enable lifecycle is settling.
        var spawned = PhotonNetwork.InstantiateRoomObject(prefab.name, position, rotation);
        if (spawned != null) knownCompanion = spawned.GetComponent<DuckCompanion>();
    }

    DuckCompanion FindCompanion()
    {
        // Death, temporary disabling, and master handoff are not removal. Only
        // a destroyed object invalidates the cache and permits a replacement.
        if (knownCompanion != null) return knownCompanion;
        for (int i = 0; i < PhotonPlayer.Active.Count; i++)
        {
            PhotonPlayer player = PhotonPlayer.Active[i];
            if (player == null || !player.IsCompanion) continue;
            knownCompanion = player.Companion;
            return knownCompanion;
        }
        return null;
    }
}
