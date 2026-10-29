using Game.Animation;
using Photon.Pun;
using UnityEngine;

/// <summary>
/// Spawns or rebinds the local duck after the gameplay rig and room are ready.
/// </summary>
public class PhotonAvatarSpawner : MonoBehaviour
{
    [SerializeField] GameObject photonPlayerPrefab;

    bool _spawned;

    void Awake()
    {
        PhotonPreparePool.Register(photonPlayerPrefab);
    }

    void Start()
    {
        TrySpawn();
    }

    void Update()
    {
        if (!PhotonNetwork.InRoom)
        {
            _spawned = false;
            return;
        }

        if (!_spawned || PhotonPlayer.Local == null)
            TrySpawn();
    }

    void TrySpawn()
    {
        if (photonPlayerPrefab == null || !PhotonNetwork.InRoom)
            return;

        var driver = FindAnyObjectByType<LocalBodyDriver>();
        if (driver == null || driver.HeadAnchor == null || driver.LeftHandAnchor == null || driver.RightHandAnchor == null)
            return;

        if (PhotonPlayer.Local != null)
        {
            PhotonPlayer.Local.BindSources(driver.HeadAnchor, driver.LeftHandAnchor, driver.RightHandAnchor, driver);
            _spawned = true;
            return;
        }
        var head = driver.HeadAnchor;
        var avatar = PhotonNetwork.Instantiate(photonPlayerPrefab.name, head.position, head.rotation);
        var player = avatar.GetComponent<PhotonPlayer>();
        player.BindSources(head, driver.LeftHandAnchor, driver.RightHandAnchor, driver);
        _spawned = true;
        DuckzAnalytics.MarkAlive();
    }

}
