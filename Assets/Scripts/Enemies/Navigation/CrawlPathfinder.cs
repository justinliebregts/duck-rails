using UnityEngine;

namespace Game.AI
{
    /// <summary>
    /// Two-level A* over a CrawlGraph with no runtime allocations.
    /// Long searches run a coarse A* over clusters first, then a fine A* restricted to the first
    /// CorridorClusters clusters of that corridor (the path is partial; replan near its end).
    /// Fine searches are capped at MaxExpansions and fall back to the node closest to the goal.
    /// One instance is shared by all enemies; searches are single-threaded and sequential.
    /// </summary>
    public sealed class CrawlPathfinder
    {
        public readonly int MaxExpansions;
        public int CorridorClusters = 3;
        public float HeuristicWeight = 1.3f;
        public float DirectSearchDistance = 12f;    // closer than this: skip the coarse pass

        public int LastExpanded { get; private set; }

        readonly CrawlGraph _g;
        readonly System.Collections.Generic.HashSet<long> _groundConnections = new System.Collections.Generic.HashSet<long>();
        readonly float[] _surfaceCost = { 1f, 1.3f, 1.8f };

        readonly float[] _gScore;
        readonly int[] _parent;
        readonly int[] _stamp;
        readonly int[] _closed;
        int _search;
        readonly MinHeap _heap;
        readonly int[] _scratch;

        readonly float[] _cgScore;
        readonly int[] _cParent;
        readonly int[] _cStamp;
        readonly int[] _cClosed;
        readonly int[] _allowed;
        int _cSearch;
        readonly MinHeap _cHeap;
        readonly int[] _corridor;
        int _corridorCount;

        public CrawlPathfinder(CrawlGraph graph, int maxExpansions = 4000)
        {
            _g = graph;
            for (int a = 0; a < graph.NodeCount; a++)
            {
                if (!graph.IsGroundNode(a)) continue;
                for (int e = graph.EdgeStart[a]; e < graph.EdgeStart[a + 1]; e++)
                {
                    int b = graph.EdgeTarget[e];
                    if (graph.IsGroundNode(b))
                        _groundConnections.Add(((long)graph.NodeCluster[a] << 32) | (uint)graph.NodeCluster[b]);
                }
            }
            MaxExpansions = Mathf.Max(64, maxExpansions);
            int n = graph.NodeCount, c = graph.ClusterCount;
            _gScore = new float[n];
            _parent = new int[n];
            _stamp = new int[n];
            _closed = new int[n];
            _blocked = new int[n];
            _heap = new MinHeap((MaxExpansions + 1) * 26 + 1);
            _scratch = new int[MaxExpansions + 2];

            _cgScore = new float[c];
            _cParent = new int[c];
            _cStamp = new int[c];
            _cClosed = new int[c];
            _allowed = new int[c];
            _cHeap = new MinHeap(Mathf.Max(16, graph.ClusterEdgeTarget.Length + 1));
            _corridor = new int[c];
        }

        public CrawlGraph Graph => _g;

        bool HasGroundConnection(int from, int to) =>
            _groundConnections.Contains(((long)from << 32) | (uint)to);

        public void SetSurfaceCosts(float floor, float wall, float ceiling)
        {
            _surfaceCost[0] = floor; _surfaceCost[1] = wall; _surfaceCost[2] = ceiling;
        }

        enum Stage { Done, CoarsePop, CoarseEdges, CoarseTrace, CoarseAllow, FinePop, FineEdges, Trace }
        Stage _stage;
        int _start, _goal, _goalCluster, _targetCluster, _best, _active, _edge, _trace, _length, _allowIndex;
        bool _groundOnly, _restrict, _reached;
        Vector3 _hTarget;
        float _bestH;
        // Snapshot by node stamp: no per-request allocation or mutable caller data.
        readonly int[] _blocked;
        public bool IsSearching => _stage != Stage.Done;
        public int LastCoarseExpanded { get; private set; }
        public int LastSliceWork { get; private set; }
        static readonly Unity.Profiling.ProfilerMarker CoarseMarker = new Unity.Profiling.ProfilerMarker("EnemyPath.Coarse");
        static readonly Unity.Profiling.ProfilerMarker FineMarker = new Unity.Profiling.ProfilerMarker("EnemyPath.Fine");
        static readonly Unity.Profiling.ProfilerMarker TraceMarker = new Unity.Profiling.ProfilerMarker("EnemyPath.Reconstruct");

        public void Cancel() { _stage = Stage.Done; _length = 0; _reached = false; }

        public void BeginSearch(int start, int goal, bool groundOnly = false,
            System.Collections.Generic.HashSet<int> blockedNodes = null)
        {
            Cancel();
            LastExpanded = LastCoarseExpanded = 0;
            _start = start; _goal = goal; _groundOnly = groundOnly;
            _targetCluster = -1; _restrict = false;
            if (!_g.IsNodeAllowed(start) || !_g.IsNodeAllowed(goal)) return;
            if (groundOnly && (!_g.IsGroundNode(start) || !_g.IsGroundNode(goal))) return;
            if (start == goal) { _scratch[0] = start; _length = 1; _reached = true; return; }
            int startCluster = _g.NodeCluster[start];
            _goalCluster = _g.NodeCluster[goal];
            if (_g.ClusterComponent[startCluster] != _g.ClusterComponent[_goalCluster]) return;
            _search++;
            if (blockedNodes != null)
                foreach (int node in blockedNodes)
                    if (node >= 0 && node < _blocked.Length) _blocked[node] = _search;
            _hTarget = _g.GetPosition(goal);
            if ((blockedNodes == null || blockedNodes.Count == 0) && startCluster != _goalCluster &&
                (_g.GetPosition(start) - _hTarget).sqrMagnitude > DirectSearchDistance * DirectSearchDistance)
            {
                _cSearch++;
                _cHeap.Clear();
                _cStamp[startCluster] = _cSearch;
                _cgScore[startCluster] = 0f;
                _cParent[startCluster] = -1;
                _cHeap.Push(startCluster, 0f);
                _stage = Stage.CoarsePop;
            }
            else BeginFine();
        }

        void BeginFine()
        {
            _heap.Clear();
            _stamp[_start] = _search;
            _gScore[_start] = 0f;
            _parent[_start] = -1;
            _heap.Push(_start, 0f);
            _best = _start;
            _bestH = (_g.GetPosition(_start) - _hTarget).sqrMagnitude;
            _stage = Stage.FinePop;
        }

        // A unit is a heap pop, edge or trace node. Duplicate heap entries and
        // high-degree clusters cannot evade the elapsed-time and work budgets.
        public bool Step(long deadlineTicks, int maxWork = 16384)
        {
            LastSliceWork = 0;
            while (IsSearching && LastSliceWork < maxWork)
            {
                int phase = ProfilePhase;
                var marker = phase == 0 ? CoarseMarker : phase == 2 ? TraceMarker : FineMarker;
                using (marker.Auto())
                {
                    do
                    {
                        if ((LastSliceWork & 15) == 0 && System.Diagnostics.Stopwatch.GetTimestamp() >= deadlineTicks)
                            return false;
                        StepUnit();
                        LastSliceWork++;
                    } while (IsSearching && LastSliceWork < maxWork && ProfilePhase == phase);
                }
            }
            return !IsSearching;
        }

        int ProfilePhase => _stage <= Stage.CoarseAllow ? 0 : _stage == Stage.Trace ? 2 : 1;

        // Yield a best partial route for an unusually long request so others get a turn.
        public void FinishPartial()
        {
            if (_stage == Stage.FinePop || _stage == Stage.FineEdges) BeginTrace(_best, false);
            else if (_stage != Stage.Trace && IsSearching) Cancel();
        }

        void BeginTrace(int end, bool reached)
        {
            _length = 0;
            _reached = reached;
            if (end == _start) { _stage = Stage.Done; return; }
            _trace = end;
            _stage = Stage.Trace;
        }

        void StepUnit()
        {
            var g = _g;
            switch (_stage)
            {
                case Stage.CoarsePop:
                    if (_cHeap.Count == 0) { Cancel(); return; }
                    _active = _cHeap.Pop();
                    if (_cClosed[_active] == _cSearch) return;
                    _cClosed[_active] = _cSearch;
                    if (_active == _goalCluster)
                    {
                        _corridorCount = 0; _trace = _active; _stage = Stage.CoarseTrace; return;
                    }
                    LastCoarseExpanded++;
                    _edge = g.ClusterEdgeStart[_active];
                    _stage = Stage.CoarseEdges;
                    return;
                case Stage.CoarseEdges:
                    if (_edge >= g.ClusterEdgeStart[_active + 1]) { _stage = Stage.CoarsePop; return; }
                    int c = g.ClusterEdgeTarget[_edge++];
                    if (_cClosed[c] == _cSearch || (_groundOnly && !HasGroundConnection(_active, c))) return;
                    float cg = _cgScore[_active] + Vector3.Distance(g.GetClusterCenter(_active), g.GetClusterCenter(c));
                    if (_cStamp[c] == _cSearch && cg >= _cgScore[c]) return;
                    _cStamp[c] = _cSearch; _cgScore[c] = cg; _cParent[c] = _active;
                    _cHeap.Push(c, cg + Vector3.Distance(g.GetClusterCenter(c), g.GetClusterCenter(_goalCluster)));
                    return;
                case Stage.CoarseTrace:
                    if (_trace >= 0)
                    {
                        _corridor[_corridorCount++] = _trace; _trace = _cParent[_trace]; return;
                    }
                    _allowIndex = 0; _restrict = true; _stage = Stage.CoarseAllow;
                    return;
                case Stage.CoarseAllow:
                    int last = Mathf.Min(Mathf.Max(1, CorridorClusters), _corridorCount - 1);
                    if (_allowIndex <= last)
                    {
                        _allowed[_corridor[_corridorCount - 1 - _allowIndex++]] = _search; return;
                    }
                    if (last < _corridorCount - 1)
                    {
                        _targetCluster = _corridor[_corridorCount - 1 - last];
                        _hTarget = g.GetClusterCenter(_corridor[_corridorCount - 2 - last]);
                    }
                    BeginFine(); return;
                case Stage.FinePop:
                    if (_heap.Count == 0) { BeginTrace(_best, false); return; }
                    _active = _heap.Pop();
                    if (_closed[_active] == _search) return;
                    _closed[_active] = _search;
                    if (_active == _goal || (_targetCluster >= 0 && g.NodeCluster[_active] == _targetCluster))
                    { BeginTrace(_active, _active == _goal); return; }
                    if (++LastExpanded > MaxExpansions) { BeginTrace(_best, false); return; }
                    float h = (g.GetPosition(_active) - _hTarget).sqrMagnitude;
                    if (h < _bestH) { _bestH = h; _best = _active; }
                    _edge = g.EdgeStart[_active]; _stage = Stage.FineEdges;
                    return;
                case Stage.FineEdges:
                    if (_edge >= g.EdgeStart[_active + 1]) { _stage = Stage.FinePop; return; }
                    int t = g.EdgeTarget[_edge++];
                    if (_closed[t] == _search || _blocked[t] == _search ||
                        (_groundOnly && !g.IsGroundNode(t)) || (_restrict && _allowed[g.NodeCluster[t]] != _search)) return;
                    Vector3 pt = g.GetPosition(t);
                    float ng = _gScore[_active] + Vector3.Distance(g.GetPosition(_active), pt) * _surfaceCost[g.NodeSurface[t]];
                    if (_stamp[t] == _search && ng >= _gScore[t]) return;
                    _stamp[t] = _search; _gScore[t] = ng; _parent[t] = _active;
                    _heap.Push(t, ng + HeuristicWeight * Vector3.Distance(pt, _hTarget));
                    return;
                case Stage.Trace:
                    if (_trace >= 0 && _length < _scratch.Length)
                    { _scratch[_length++] = _trace; _trace = _parent[_trace]; return; }
                    _stage = Stage.Done;
                    return;
            }
        }

        public bool CopyResult(int[] path, out int count, out bool reachedGoal)
        {
            using (TraceMarker.Auto())
            {
                count = 0; reachedGoal = false;
                if (IsSearching || path == null || path.Length == 0 || _length == 0) return false;
                count = Mathf.Min(_length, path.Length);
                for (int i = 0; i < count; i++) path[i] = _scratch[_length - 1 - i];
                reachedGoal = _reached && count == _length;
                return true;
            }
        }

        // Compatibility entry point for editor/debug consumers. Gameplay uses Step.
        public bool FindPath(int start, int goal, int[] path, out int count, out bool reachedGoal, bool groundOnly = false,
            System.Collections.Generic.HashSet<int> blockedNodes = null)
        {
            BeginSearch(start, goal, groundOnly, blockedNodes);
            while (IsSearching) Step(long.MaxValue);
            return CopyResult(path, out count, out reachedGoal);
        }

        /// <summary>Binary min-heap with lazy deletion (duplicates allowed, filtered by the closed set).</summary>
        sealed class MinHeap
        {
            readonly int[] _items;
            readonly float[] _keys;
            public int Count { get; private set; }

            public MinHeap(int capacity)
            {
                _items = new int[capacity];
                _keys = new float[capacity];
            }

            public void Clear() => Count = 0;

            public void Push(int item, float key)
            {
                if (Count == _items.Length) return;
                int i = Count++;
                while (i > 0)
                {
                    int p = (i - 1) >> 1;
                    if (_keys[p] <= key) break;
                    _items[i] = _items[p];
                    _keys[i] = _keys[p];
                    i = p;
                }
                _items[i] = item;
                _keys[i] = key;
            }

            public int Pop()
            {
                int top = _items[0];
                int last = --Count;
                if (last == 0) return top;
                int item = _items[last];
                float key = _keys[last];
                int i = 0;
                while (true)
                {
                    int c = (i << 1) + 1;
                    if (c >= last) break;
                    if (c + 1 < last && _keys[c + 1] < _keys[c]) c++;
                    if (_keys[c] >= key) break;
                    _items[i] = _items[c];
                    _keys[i] = _keys[c];
                    i = c;
                }
                _items[i] = item;
                _keys[i] = key;
                return top;
            }
        }
    }
}
