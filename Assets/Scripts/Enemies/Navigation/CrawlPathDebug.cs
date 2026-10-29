using UnityEngine;

namespace Game.AI
{
    /// <summary>
    /// Editor/debug helper: select it and move the From/To transforms to see the crawl path.
    /// Logs nodes explored and search time whenever the endpoints move.
    /// </summary>
    public class CrawlPathDebug : MonoBehaviour
    {
        [SerializeField] TextAsset graphAsset;
        [SerializeField] Transform from;
        [SerializeField] Transform to;
        [SerializeField] float snapRadius = 3f;

#if UNITY_EDITOR
        CrawlGraph _graph;
        TextAsset _loaded;
        CrawlPathfinder _pathfinder;
        readonly int[] _path = new int[512];
        int _count;
        bool _reached;
        Vector3 _lastFrom, _lastTo;

        void OnDrawGizmosSelected()
        {
            if (graphAsset == null || from == null || to == null) return;
            if (_loaded != graphAsset)
            {
                _loaded = graphAsset;
                _graph = CrawlGraph.Load(graphAsset);
                _pathfinder = _graph != null ? new CrawlPathfinder(_graph) : null;
                _lastFrom = _lastTo = new Vector3(float.NaN, 0f, 0f);
            }
            if (_pathfinder == null) return;

            if (from.position != _lastFrom || to.position != _lastTo)
            {
                _lastFrom = from.position;
                _lastTo = to.position;
                int a = _graph.FindNearestNode(_lastFrom, snapRadius);
                int b = _graph.FindNearestNode(_lastTo, snapRadius);
                System.Diagnostics.Stopwatch sw = System.Diagnostics.Stopwatch.StartNew();
                bool ok = _pathfinder.FindPath(a, b, _path, out _count, out _reached);
                sw.Stop();
                Debug.Log($"[CrawlPathDebug] start {a} goal {b} ok {ok} nodes {_count} reachedGoal {_reached} " +
                          $"expanded {_pathfinder.LastExpanded} time {sw.Elapsed.TotalMilliseconds:0.000} ms", this);
            }

            Gizmos.color = _reached ? Color.green : Color.yellow;
            for (int i = 1; i < _count; i++)
                Gizmos.DrawLine(_graph.GetPosition(_path[i - 1]), _graph.GetPosition(_path[i]));
            if (_count > 0)
            {
                Gizmos.DrawWireSphere(_graph.GetPosition(_path[0]), 0.15f);
                Gizmos.DrawWireSphere(_graph.GetPosition(_path[_count - 1]), 0.15f);
            }
        }
#endif
    }
}
