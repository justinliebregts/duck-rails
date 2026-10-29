using Game.AI;
using Photon.Pun;
using UnityEngine;

/// <summary>Victim-reported kills, master-controlled explosion and reusable bomber respawn.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Enemy), typeof(PhotonView))]
public sealed class ZombieBomberExplode : MonoBehaviourPun
{
    [SerializeField] GameObject gibPrefab;
    [Tooltip("Lethal radius in meters, measured from the explosion to the local player root. Walls do not block the blast.")]
    [SerializeField, Min(0f)] float blastRadius = 6f;
    [SerializeField, Min(0f)] float respawnDelay = 10f;
    [SerializeField, Min(0.1f)] float effectLifetime = 10f;

    Enemy enemy;
    EnemyNetSync net;
    KillTrigger[] triggers;
    Renderer[] renderers;
    Collider[] colliders;
    bool[] rendererEnabled, colliderEnabled;
    int revision;
    int lastExplosionRevision;
    double respawnAt;
    float nextRespawnAttempt;

    bool Networked => PhotonNetwork.InRoom && photonView.ViewID != 0;
    double Now => Networked ? PhotonNetwork.Time : Time.timeAsDouble;

    void Awake()
    {
        if (gibPrefab != null) DeathEffectPool.Get().Prewarm(gibPrefab, 4);
        enemy = GetComponent<Enemy>();
        net = GetComponent<EnemyNetSync>();
        triggers = GetComponentsInChildren<KillTrigger>(true);
        renderers = GetComponentsInChildren<Renderer>(true);
        colliders = GetComponentsInChildren<Collider>(true);
        rendererEnabled = new bool[renderers.Length];
        colliderEnabled = new bool[colliders.Length];
    }

    void OnEnable()
    {
        foreach (var trigger in triggers) trigger.PlayerKilled += OnPlayerKilled;
    }

    void OnDisable()
    {
        foreach (var trigger in triggers)
            if (trigger != null) trigger.PlayerKilled -= OnPlayerKilled;
    }

    void OnPlayerKilled()
    {
        if (enemy.IsDormant) return;
        if (Networked) photonView.RPC(nameof(RequestExplosion), RpcTarget.MasterClient, revision);
        else Explode();
    }

    [PunRPC]
    void RequestExplosion(int lifeRevision)
    {
        // Several victims can report the same contact. Only accept once per life.
        if (!PhotonNetwork.IsMasterClient || lifeRevision != revision || enemy.IsDormant) return;
        Explode();
    }

    void Explode()
    {
        Vector3 position = enemy.Root.position;
        Quaternion rotation = enemy.Root.rotation;
        PublishState(true, Now + respawnDelay, position, rotation);
        // The master supplies one blast origin/radius. Each peer owns its local player's death.
        // Keep damage and visuals unbuffered so late joiners don't receive an old blast.
        if (Networked) photonView.RPC(nameof(PlayExplosion), RpcTarget.All,
            revision, position, rotation, blastRadius);
        else ApplyExplosion(revision, position, rotation, blastRadius);
    }

    [PunRPC]
    void PlayExplosion(int explosionRevision, Vector3 position, Quaternion rotation,
        float radius, PhotonMessageInfo info)
    {
        if (info.Sender != PhotonNetwork.MasterClient) return;
        ApplyExplosion(explosionRevision, position, rotation, radius);
    }

    void ApplyExplosion(int explosionRevision, Vector3 position, Quaternion rotation, float radius)
    {
        using var sample = DeathProfile.Bomber.Auto();
        // A repeated event must not kill a player again after their respawn.
        if (explosionRevision <= lastExplosionRevision) return;
        lastExplosionRevision = explosionRevision;
        SpawnEffect(position, rotation);
        radius = Mathf.Max(0f, radius);
        foreach (var duck in FindObjectsByType<DuckCompanion>(FindObjectsSortMode.None))
            if ((duck.transform.position - position).sqrMagnitude <= radius * radius) duck.TryKill();

        // GR.Player is the local DuckPlayer or DesktopPlayer, not a remote Photon avatar.
        // SetDead handles local ragdoll/respawn; PhotonPlayer replicates the death state.
        var player = GR.Player.instance;
        if (player == null || !player.isActiveAndEnabled || player.IsDead) return;
        radius = Mathf.Max(0f, radius);
        if ((player.transform.position - position).sqrMagnitude <= radius * radius)
        {
            Vector3 impact = enemy.PlayerDeathLaunch(player.transform.position, position);
            player.SetDead("ZombieBomber", impact, enemy.PlayerDeathRotation(impact));
        }
    }

    void SpawnEffect(Vector3 position, Quaternion rotation)
    {
        if (gibPrefab == null) return;
        var pool = DeathEffectPool.Get();
        var effect = pool.Rent(gibPrefab, position, rotation);
        pool.Activate(effect, effectLifetime);
    }

    void Update()
    {
        if (!enemy.IsDormant || !enemy.HasAuthority || Now < respawnAt ||
            Time.unscaledTime < nextRespawnAttempt) return;
        nextRespawnAttempt = Time.unscaledTime + 1f;
        // Retry if the manager or a supported spawn point is temporarily unavailable.
        var manager = EnemyManager.Instance;
        if (manager == null || !manager.TryRespawnRandom(enemy, out Vector3 position, out Quaternion rotation)) return;
        // Publish the selected spawn pose, not a Transform readback that can still contain
        // the explosion pose until Unity synchronizes the rigidbody and its Transform.
        PublishState(false, 0, position, rotation);
    }

    void PublishState(bool dormant, double deadline, Vector3 position, Quaternion rotation)
    {
        int nextRevision = revision + 1;
        ApplyState(nextRevision, dormant, deadline, position, rotation);
        if (!Networked) return;
        // Cache only the latest lifecycle state, including the shared deadline for host migration.
        PhotonNetwork.RemoveBufferedRPCs(photonView.ViewID, nameof(ReceiveState));
        photonView.RPC(nameof(ReceiveState), RpcTarget.OthersBuffered,
            nextRevision, dormant, deadline, position, rotation);
    }

    [PunRPC]
    void ReceiveState(int stateRevision, bool dormant, double deadline,
        Vector3 position, Quaternion rotation, PhotonMessageInfo info)
    {
        // Buffered state may have been sent by a previous master before it left.
        if (stateRevision <= revision) return;
        ApplyState(stateRevision, dormant, deadline, position, rotation);
    }

    void ApplyState(int stateRevision, bool dormant, double deadline, Vector3 position, Quaternion rotation)
    {
        revision = stateRevision;
        respawnAt = deadline;
        nextRespawnAttempt = 0f;
        if (dormant != enemy.IsDormant)
        {
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] == null) continue;
                if (dormant) rendererEnabled[i] = renderers[i].enabled;
                renderers[i].enabled = dormant ? false : rendererEnabled[i];
            }
            for (int i = 0; i < colliders.Length; i++)
            {
                if (colliders[i] == null) continue;
                if (dormant) colliderEnabled[i] = colliders[i].enabled;
                colliders[i].enabled = dormant ? false : colliderEnabled[i];
            }
        }
        enemy.IsDormant = dormant;
        enemy.Teleport(position, rotation);
        if (net != null) net.ResetRemotePose(position, rotation);
    }
}
