using System.Collections.Generic;
using Game.Animation;
using Photon.Pun;
using UnityEngine;

namespace Game.AI
{
    /// <summary>
    /// The only per-frame driver for enemies. Loads the crawl graph, owns the shared pathfinder,
    /// tracks player targets + trails, budgets one resumable path request per frame, staggers vision checks,
    /// ticks gaits, and (on the master client) spawns the enemies as room objects.
    /// </summary>
    public class EnemyManager : MonoBehaviour
    {
        public const int MaxTargets = 8;
        const int TrailLength = 32;

        public static EnemyManager Instance { get; private set; }

        [Header("Graph")]
        [SerializeField] TextAsset graphAsset;
        [SerializeField] int maxExpansions = 4000;
        [SerializeField] int corridorClusters = 3;
        [SerializeField, Min(0.1f)] float pathBudgetMilliseconds = 1f;
        [SerializeField, Min(1)] int maxPathRequestFrames = 32;
        [SerializeField] float floorCost = 1f;
        [SerializeField] float wallCost = 1.3f;
        [SerializeField] float ceilingCost = 1.8f;
        [Tooltip("Terrain mesh names whose footprints contain the playable crawl area. Empty leaves other scenes unrestricted.")]
        [SerializeField] string[] crawlBoundaryTerrainNames;
        [SerializeField, Min(0.01f)] float crawlBoundaryInset = 0.1f;

        [Header("Physics")]
        [SerializeField] LayerMask surfaceMask = ~0;
        [SerializeField] LayerMask visionMask = ~0;

        [Header("Targets")]
        [SerializeField, Min(1)] int perceptionInterval = 8;
        [SerializeField] float trailSpacing = 0.75f;
        [SerializeField] float targetGroundProbe = 4f;
        [SerializeField] float targetSnapRadius = 3f;
        [Tooltip("Chased when no PhotonPlayer exists (offline testing).")]
        [SerializeField] Transform fallbackTarget;

        [Header("Spawning (authority only)")]
        [SerializeField] GameObject[] enemyPrefabs;
        [SerializeField] Transform[] spawnPoints;
        [SerializeField, Range(0, 20)] int maxEnemies = 5;

        public CrawlGraph Graph { get; private set; }
        public CrawlPathfinder Pathfinder { get; private set; }
        public float PathBudgetMilliseconds => pathBudgetMilliseconds;
        // Independent mutable search state; the baked graph itself is shared.
        public CrawlPathfinder CreateCompanionPathfinder()
        {
            if (Graph == null) return null;
            var result = new CrawlPathfinder(Graph, maxExpansions) { CorridorClusters = corridorClusters };
            result.SetSurfaceCosts(floorCost, wallCost, ceilingCost);
            return result;
        }
        public LayerMask SurfaceMask => surfaceMask;
        public LayerMask VisionMask => visionMask;

        readonly Object[] _slotOwner = new Object[MaxTargets];
        readonly Vector3[] _slotHead = new Vector3[MaxTargets];
        readonly bool[] _slotSeen = new bool[MaxTargets];
        readonly Vector3[] _trail = new Vector3[MaxTargets * TrailLength];
        readonly int[] _trailSeq = new int[MaxTargets];

        readonly TargetGroundSnapshot targetGroundSnapshot = new TargetGroundSnapshot();
        int _frame;
        int _pathCursor;
        Enemy _pathEnemy;
        object _pathOwner;
        int _pathVersion, _pathFrames, _pathServiceFrames;
        long _pathDeadline;
        public int LastPathRequestFrames { get; private set; }
        static readonly Unity.Profiling.ProfilerMarker PathMarker = new Unity.Profiling.ProfilerMarker("EnemyPath.Service");
        static readonly Unity.Profiling.ProfilerMarker NodeMarker = new Unity.Profiling.ProfilerMarker("EnemyPath.ResolveNode");
        static readonly Unity.Profiling.ProfilerMarker TickMarker = new Unity.Profiling.ProfilerMarker("Enemy.TickAll");
        public int PathFrames { get; private set; }
        public int PathExpanded { get; private set; }
        public int CoarseExpanded { get; private set; }
        public int PendingPaths { get; private set; }
        public int QueueFrames { get; private set; }
        int _spawned;
        readonly List<int> _prefabOrder = new List<int>();
        int[] _spawnOrder;
        int _prefabCursor;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                {
                    if (DuckzLog.AllowWarning(out var logNotice1))
                        logNotice1.Write("[EnemyManager] Duplicate manager disabled.", this);
                }
                enabled = false;
                return;
            }
            Instance = this;

            Graph = CrawlGraph.Load(graphAsset);
            if (Graph == null)
            {
                {
                    if (DuckzLog.AllowError(out var logNotice2))
                        logNotice2.Write("[EnemyManager] No crawl graph assigned or it failed to load. Enemies will not move.", this);
                }
            }
            else
            {
                ApplyCrawlBoundaries();
                Pathfinder = new CrawlPathfinder(Graph, maxExpansions) { CorridorClusters = corridorClusters };
                Pathfinder.SetSurfaceCosts(floorCost, wallCost, ceilingCost);
#if !UNITY_EDITOR
                Resources.UnloadAsset(graphAsset);
#endif
            }

            if (enemyPrefabs != null)
            {
                for (int i = 0; i < enemyPrefabs.Length; i++)
                    PhotonPreparePool.Register(enemyPrefabs[i]);
            }
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void ApplyCrawlBoundaries()
        {
            if (crawlBoundaryTerrainNames == null || crawlBoundaryTerrainNames.Length == 0) return;
            var bounds = new List<Bounds>();
            var worldToLocal = new List<Matrix4x4>();
            foreach (MeshFilter mesh in FindObjectsByType<MeshFilter>(FindObjectsSortMode.None))
            {
                if (mesh.gameObject.scene != gameObject.scene || mesh.sharedMesh == null) continue;
                if (System.Array.IndexOf(crawlBoundaryTerrainNames, mesh.name) < 0) continue;
                // Mesh-local bounds follow rotated/scaled section instances;
                // a world AABB would admit exterior corners of a rotated section.
                Bounds region = mesh.sharedMesh.bounds;
                Vector3 min = region.min + Vector3.up * crawlBoundaryInset;
                Vector3 max = region.max;
                max.y += 10000f; // no gameplay ceiling cap: retain rooftops and tall structures
                region.SetMinMax(min, max);
                bounds.Add(region);
                worldToLocal.Add(mesh.transform.worldToLocalMatrix);
            }
            if (bounds.Count != crawlBoundaryTerrainNames.Length)
                {
                    if (DuckzLog.AllowError(out var logNotice3))
                        logNotice3.Write("[EnemyManager] Missing crawl boundary terrain meshes. Check the configured names; unmatched areas are excluded.", this);
                }
            // Inset the combined footprint, not individual sections, so shared
            // seams between town/city/factory/forest remain traversable.
            Graph.RestrictToBounds(bounds.ToArray(), crawlBoundaryInset, worldToLocal.ToArray());
        }

        void Update()
        {
            _frame++;
            targetGroundSnapshot.Clear();
            float dt = Time.deltaTime;
            RefreshTargets();
            TrySpawn();

            List<Enemy> enemies = Enemy.Active;
            if (Graph == null)
            {
                for (int i = 0; i < enemies.Count; i++)
                {
                    Enemy remote = enemies[i];
                    if (remote.HasAuthority)
                    {
                        if (remote.IsKnocked) remote.TickAuthority(this, dt, false);
                    }
                    else remote.ApplyRemote(dt);
                }
                return;
            }

            if (NavigationWorkBudget.TryBegin(pathBudgetMilliseconds, NavigationWorkBudget.PendingDuckPlans > 0,
                out long started, out long deadline))
            {
                _pathDeadline = deadline;
                try { ServicePathRequest(enemies); }
                finally { NavigationWorkBudget.End(started); _pathDeadline = 0; }
            }

            using (TickMarker.Auto())
            for (int i = 0; i < enemies.Count; i++)
            {
                Enemy e = enemies[i];
                if (e.HasAuthority) e.TickAuthority(this, dt, e.ShouldPerceive(this, _frame, i, Mathf.Max(1, perceptionInterval)));
                else e.ApplyRemote(dt);
            }
        }

        void LateUpdate()
        {
            float dt = Time.deltaTime;
            List<Enemy> enemies = Enemy.Active;
            for (int i = 0; i < enemies.Count; i++)
            {
                Enemy enemy = enemies[i];
                if (enemy.IsDormant) continue;
                if (enemy.IsKnocked || enemy.RagdollActive)
                {
                    enemy.TickRagdoll(dt);
                    continue;
                }
                if (enemy.IsStunned)
                {
                    enemy.TickStunHead(dt);
                    continue;
                }
                IGaitDriver gait = enemy.Gait;
                if (gait != null) gait.Tick(dt);
                if (enemy.WantsHeadAim)
                    enemy.AimHead(dt);
            }
        }

        void ServicePathRequest(List<Enemy> enemies)
        {
            using var sample = PathMarker.Auto();
            int pending = 0;
            foreach (Enemy enemy in enemies) if (enemy.WantsPath && enemy.HasAuthority) pending++;
            PendingPaths = pending;
            if (_pathEnemy != null && _pathEnemy.isActiveAndEnabled && _pathEnemy.WantsPath && _pathEnemy.HasAuthority && enemies.Contains(_pathEnemy))
            {
                _pathServiceFrames++;
                _pathEnemy.RunPathRequest(this);
                return;
            }
            CancelPathRequest();
            int n = enemies.Count;
            for (int k = 0; k < n; k++)
            {
                int idx = (_pathCursor + k) % n;
                Enemy e = enemies[idx];
                if (!e.isActiveAndEnabled || !e.WantsPath || !e.HasAuthority) continue;
                _pathEnemy = e;
                _pathServiceFrames = 1;
                e.RunPathRequest(this);
                _pathCursor = idx + 1;
                return;
            }
        }

        void CancelPathRequest()
        {
            Pathfinder?.Cancel();
            _pathOwner = null;
            _pathEnemy = null;
            _pathFrames = 0;
            _pathServiceFrames = 0;
        }

        public bool HasPathRequest(object owner, int version) => ReferenceEquals(_pathOwner, owner) && _pathVersion == version;

        public enum PathStatus { Pending, Found, Failed }

        public PathStatus StepPathRequest(object owner, int version, int start, int goal, int[] result,
            out int count, out bool complete, bool groundOnly = false, HashSet<int> blocked = null, int queuedFrame = 0)
        {
            count = 0; complete = false;
            if (!HasPathRequest(owner, version))
            {
                _pathOwner = owner; _pathVersion = version; _pathFrames = 0;
                QueueFrames = Mathf.Max(0, Time.frameCount - queuedFrame);
                Pathfinder.BeginSearch(start, goal, groundOnly, blocked);
            }
            _pathFrames++;
            if (Mathf.Max(_pathFrames, _pathServiceFrames) >= Mathf.Max(1, maxPathRequestFrames)) Pathfinder.FinishPartial();
            // Direct test/editor calls also receive a finite budget.
            long deadline = _pathDeadline != 0 ? _pathDeadline : System.Diagnostics.Stopwatch.GetTimestamp() +
                (long)(Mathf.Max(0.1f, pathBudgetMilliseconds) * System.Diagnostics.Stopwatch.Frequency / 1000d);
            bool finished = Pathfinder.Step(deadline);
            PathFrames = _pathFrames;
            PathExpanded = Pathfinder.LastExpanded;
            CoarseExpanded = Pathfinder.LastCoarseExpanded;
            if (!finished) return PathStatus.Pending;
            LastPathRequestFrames = _pathFrames;
            bool found = Pathfinder.CopyResult(result, out count, out complete);
            _pathOwner = null;
            _pathEnemy = null; // Rotate after completion, even if the mover must request a fresh start.
            return found ? PathStatus.Found : PathStatus.Failed;
        }

        // Rejoin only at the current graph node or a directly connected neighbour.
        // Collision-aware locomotion still validates each physical movement step.
        public static int PathJoinIndex(CrawlGraph graph, int current, int[] path, int count)
        {
            if (!graph.IsNodeAllowed(current)) return -1;
            for (int i = 0; i < count; i++)
                if (path[i] == current) return Mathf.Min(i + 1, count - 1);
            for (int i = 0; i < count; i++)
                for (int e = graph.EdgeStart[current]; e < graph.EdgeStart[current + 1]; e++)
                    if (graph.EdgeTarget[e] == path[i]) return i;
            return -1;
        }

        public int ResolveNode(Vector3 point, bool reusePlanningObservation = false)
        {
            using var sample = NodeMarker.Auto();
            if (Graph == null) return -1;
            Vector3 query = point;
            if (targetGroundSnapshot.Query(point, targetGroundProbe, surfaceMask, out RaycastHit hit, reusePlanningObservation))
                query = hit.point;
            int node = Graph.FindNearestNode(query, targetSnapRadius);
            return node >= 0 ? node : Graph.FindNearestNode(point, targetSnapRadius);
        }

        /// <summary>Nearest upward-facing graph node for a world point (probes down, then snaps).</summary>
        public int ResolveGroundNode(Vector3 point, bool reusePlanningObservation = false)
        {
            using var sample = NodeMarker.Auto();
            if (Graph == null) return -1;
            Vector3 query = point;
            if (targetGroundSnapshot.Query(point, targetGroundProbe, surfaceMask, out RaycastHit hit, reusePlanningObservation))
                query = hit.point;
            int node = Graph.FindNearestGroundNode(query, targetSnapRadius);
            return node >= 0 ? node : Graph.FindNearestGroundNode(point, targetSnapRadius);
        }

        public float NearestTargetDistanceSquared(Vector3 position)
        {
            float nearest = float.PositiveInfinity;
            for (int s = 0; s < MaxTargets; s++)
                if (_slotOwner[s] != null)
                    nearest = Mathf.Min(nearest, (_slotHead[s] - position).sqrMagnitude);
            return nearest;
        }

        public bool TryGetTarget(int slot, out Vector3 head)
        {
            head = _slotHead[slot];
            return _slotOwner[slot] != null;
        }

        public int TrailSeq(int slot) => _trailSeq[slot];

        public bool TryGetTrailPoint(int slot, ref int seq, out Vector3 point)
        {
            int latest = _trailSeq[slot];
            if (seq < latest - TrailLength) seq = latest - TrailLength;
            if (seq < 0) seq = 0;
            if (seq >= latest)
            {
                point = default;
                return false;
            }
            point = _trail[slot * TrailLength + seq % TrailLength];
            return true;
        }

        void RefreshTargets()
        {
            for (int s = 0; s < MaxTargets; s++) _slotSeen[s] = false;

            int registered = 0;
            List<PhotonPlayer> players = PhotonPlayer.Active;
            for (int i = 0; i < players.Count; i++)
            {
                PhotonPlayer player = players[i];
                if (player.IsDead) continue;
                if (player.TryGetHeadPosition(out Vector3 head))
                {
                    UpdateSlot(player, head);
                    registered++;
                }
            }

            if (registered == 0)
            {
                bool localDead = GR.Player.instance != null && GR.Player.instance.IsDead;
                if (!localDead && fallbackTarget != null)
                    UpdateSlot(fallbackTarget, fallbackTarget.position);
                else if (!localDead)
                {
                    List<LocalBodyDriver> drivers = LocalBodyDriver.Active;
                    for (int i = 0; i < drivers.Count; i++)
                    {
                        Transform head = drivers[i].HeadAnchor;
                        if (head == null) continue;
                        UpdateSlot(drivers[i], head.position);
                    }
                }
            }

            for (int s = 0; s < MaxTargets; s++)
            {
                if (!_slotSeen[s]) _slotOwner[s] = null;
            }
        }

        void UpdateSlot(Object owner, Vector3 head)
        {
            int slot = -1, free = -1;
            for (int s = 0; s < MaxTargets; s++)
            {
                if (ReferenceEquals(_slotOwner[s], owner)) { slot = s; break; }
                if (free < 0 && _slotOwner[s] is null) free = s;
            }
            if (slot < 0)
            {
                if (free < 0) return;
                slot = free;
                _slotOwner[slot] = owner;
                _trailSeq[slot] = 0;
            }

            _slotSeen[slot] = true;
            _slotHead[slot] = head;

            int seq = _trailSeq[slot];
            if (seq > 0)
            {
                Vector3 last = _trail[slot * TrailLength + (seq - 1) % TrailLength];
                if ((head - last).sqrMagnitude < trailSpacing * trailSpacing) return;
            }
            _trail[slot * TrailLength + seq % TrailLength] = head;
            _trailSeq[slot] = seq + 1;
        }

        bool SpawnAuthority
        {
            get
            {
                if (PhotonNetwork.InRoom) return PhotonNetwork.IsMasterClient;
                if (PhotonSession.ConnectPending || PhotonNetwork.IsConnected) return false;
                return true;
            }
        }

        /// <summary>Destroys enemies instantiated before the room existed. Their PhotonView id is still 0, so IsMine stays false.</summary>
        public static void ReleaseLocalSpawns()
        {
            if (Instance == null) return;
            Instance.DropUnowned();
        }

        void DropUnowned()
        {
            List<Enemy> enemies = Enemy.Active;
            for (int i = enemies.Count - 1; i >= 0; i--)
            {
                Enemy enemy = enemies[i];
                if (enemy == null) continue;
                PhotonView view = enemy.GetComponent<PhotonView>();
                if (view != null && view.ViewID != 0) continue;
                Destroy(enemy.gameObject);
            }
            _spawned = 0;
            _prefabOrder.Clear();
            _prefabCursor = 0;
        }

        void ShufflePrefabCycle()
        {
            _prefabOrder.Clear();
            _prefabCursor = 0;
            for (int i = 0; i < enemyPrefabs.Length; i++)
            {
                if (enemyPrefabs[i] != null) _prefabOrder.Add(i);
            }
            // Fisher-Yates: each configured entry appears once per complete cycle.
            for (int i = _prefabOrder.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                int temp = _prefabOrder[i];
                _prefabOrder[i] = _prefabOrder[j];
                _prefabOrder[j] = temp;
            }
        }

        void TrySpawn()
        {
            if (GameManager.instance != null && GameManager.instance.disableEnemySpawning) return;
            if (enemyPrefabs == null || enemyPrefabs.Length == 0 || spawnPoints == null || spawnPoints.Length == 0) return;
            if (Enemy.Active.Count >= maxEnemies || !SpawnAuthority) return;

            if (_prefabCursor >= _prefabOrder.Count) ShufflePrefabCycle();
            if (_prefabOrder.Count == 0) return;
            GameObject prefab = enemyPrefabs[_prefabOrder[_prefabCursor]];
            if (prefab == null) return;

            if (!TryGetGroundedSpawnPose(out Vector3 position, out Quaternion rotation)) return;
            GameObject go = PhotonNetwork.InRoom
                ? PhotonNetwork.InstantiateRoomObject(prefab.name, position, rotation)
                : Instantiate(prefab, position, rotation);
            if (go == null) return;
            _prefabCursor++;
            Enemy enemy = go.GetComponent<Enemy>();
            if (enemy != null) enemy.ResetBrain();
        }

        public void Respawn(Enemy enemy)
        {
            if (enemy == null || spawnPoints == null || spawnPoints.Length == 0) return;
            if (TryGetGroundedSpawnPose(out Vector3 position, out Quaternion rotation))
                enemy.Teleport(position, rotation);
        }

        /// <summary>Authority-only respawn using a randomly selected valid spawn point.</summary>
        public bool TryRespawnRandom(Enemy enemy, out Vector3 position, out Quaternion rotation, string recoveryReason = null)
        {
            position = default;
            rotation = Quaternion.identity;
            if (!SpawnAuthority || enemy == null || !enemy.HasAuthority ||
                spawnPoints == null || spawnPoints.Length == 0) return false;
            if (!TryGetGroundedSpawnPose(out position, out rotation, true)) return false;
            if (recoveryReason != null)
                {
                    if (DuckzLog.AllowWarning(out var logNotice4))
                        logNotice4.Write($"Enemy recovery: {enemy.name}, reason={recoveryReason}, state={enemy.CurrentState}, from={enemy.Root.position}, to={position}", enemy);
                }
            enemy.Teleport(position, rotation);
            return true;
        }

        bool TryGetGroundedSpawnPose(out Vector3 position, out Quaternion rotation, bool random = false)
        {
            position = default;
            rotation = Quaternion.identity;
            // Shuffle indices so every valid point has equal chance, even when some are invalid.
            int[] order = null;
            if (random)
            {
                if (_spawnOrder == null || _spawnOrder.Length != spawnPoints.Length)
                    _spawnOrder = new int[spawnPoints.Length];
                order = _spawnOrder;
                for (int i = 0; i < order.Length; i++) order[i] = i;
                for (int i = order.Length - 1; i > 0; i--)
                {
                    int j = Random.Range(0, i + 1);
                    int temp = order[i];
                    order[i] = order[j];
                    order[j] = temp;
                }
            }
            // Spawn height is independent of the short locomotion ground probes.
            // Resolve before instantiation so network peers receive the grounded pose.
            for (int i = 0; i < spawnPoints.Length; i++)
            {
                Transform point = spawnPoints[random ? order[i] : _spawned++ % spawnPoints.Length];
                if (point == null) continue;
                if (!Physics.Raycast(point.position + Vector3.up * 0.1f, Vector3.down,
                    out RaycastHit hit, Mathf.Infinity, surfaceMask, QueryTriggerInteraction.Ignore)
                    || !CrawlGraph.IsWalkableGround(hit.normal)) continue;

                position = hit.point;
                Vector3 forward = Vector3.ProjectOnPlane(point.forward, Vector3.up);
                if (forward.sqrMagnitude < 1e-6f) forward = Vector3.forward;
                rotation = Quaternion.LookRotation(forward, Vector3.up);
                return true;
            }
            // Try another point instead of creating an enemy in unsupported space.
            return false;
        }
    }
}
