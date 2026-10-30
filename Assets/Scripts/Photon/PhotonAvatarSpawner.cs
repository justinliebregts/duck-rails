using Game.Animation;
using Photon.Pun;
using UnityEngine;

/// <summary>
/// Spawns or rebinds the local duck after the gameplay rig and room are ready.
/// </summary>
public class PhotonAvatarSpawner : MonoBehaviour
{
    [SerializeField] GameObject photonPlayerPrefab;

    PhotonPlayer _boundPlayer;

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
            _boundPlayer = null;
            return;
        }

        var local = PhotonPlayer.Local;
        if (local == null || local != _boundPlayer || local.HandSource(true) == null || local.HandSource(false) == null)
            TrySpawn();
    }

    void TrySpawn()
    {
        if (photonPlayerPrefab == null || !PhotonNetwork.InRoom || !PhotonNetwork.IsMessageQueueRunning)
            return;

        // Joining an expedition can complete while the outgoing Compound rig
        // still updates. LoadWorldScene has already preserved existing views;
        // a new avatar here would be destroyed locally by scene unloading while
        // its cached Instantiate event survives on peers and late joiners.
        var session = PhotonSession.Instance;
        if (session != null && (session.HasRoomRequest || gameObject.scene.name != PhotonSession.RequiredScene))
            return;

        var driver = FindAnyObjectByType<LocalBodyDriver>();
        if (driver == null || driver.HeadAnchor == null || driver.LeftHandAnchor == null || driver.RightHandAnchor == null)
            return;

        if (PhotonPlayer.Local != null)
        {
            PhotonPlayer.Local.BindSources(driver.HeadAnchor, driver.LeftHandAnchor, driver.RightHandAnchor, driver);
            _boundPlayer = PhotonPlayer.Local;
            return;
        }
        // A reserved-slot rejoin restores the owner's cached Instantiate after
        // OnJoinedRoom. Spawning before that event arrives can reuse its ViewID
        // and replace a bound avatar with an unbound cached copy.
        if (PhotonNetwork.LocalPlayer.HasRejoined)
            return;

        var head = driver.HeadAnchor;
        var avatar = PhotonNetwork.Instantiate(photonPlayerPrefab.name, head.position, head.rotation);
        var player = avatar.GetComponent<PhotonPlayer>();
        player.BindSources(head, driver.LeftHandAnchor, driver.RightHandAnchor, driver);
        _boundPlayer = player;
        DuckzAnalytics.MarkAlive();
    }

}
