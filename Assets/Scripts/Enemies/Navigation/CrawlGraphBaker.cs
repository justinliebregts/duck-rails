using UnityEngine;

namespace Game.AI
{
    /// <summary>
    /// Bake settings for the crawl graph. Place one in the level, size the box around the play area,
    /// add a seed (e.g. an enemy spawn point) and press Bake in the inspector. Does nothing at runtime.
    /// The box is world-axis aligned and centred on this transform.
    /// </summary>
    public class CrawlGraphBaker : MonoBehaviour
    {
        [Header("Volume")]
        public Vector3 size = new Vector3(200f, 40f, 200f);
        [Min(0.1f)] public float cellSize = 0.5f;
        [Min(0.01f)] public float agentRadius = 0.2f;

        [Header("Surfaces")]
        public LayerMask surfaceMask = ~0;
        public bool allowCeilings = true;

        [Header("Pruning")]
        [Tooltip("Only nodes connected to these points are kept. Leave empty to keep every island above Min Island Nodes.")]
        public Transform[] seeds;
        [Min(1)] public int minIslandNodes = 50;

        [Header("Output")]
        public TextAsset graphAsset;

        [Header("Rebake Region")]
        public Bounds rebakeRegion = new Bounds(Vector3.zero, new Vector3(20f, 20f, 20f));

        [Header("Gizmos")]
        public bool drawNodes = true;
        public bool drawEdges;
        [Min(1f)] public float gizmoRadius = 10f;

        [TextArea(3, 10)] public string lastBakeReport;

        public Bounds BakeBounds => new Bounds(transform.position, size);

#if UNITY_EDITOR
        CrawlGraph _preview;
        TextAsset _previewSource;

        public void InvalidatePreview()
        {
            _preview = null;
            _previewSource = null;
        }

        CrawlGraph GetPreview()
        {
            if (graphAsset == null) return null;
            if (_previewSource != graphAsset)
            {
                _previewSource = graphAsset;
                _preview = CrawlGraph.Load(graphAsset);
            }
            return _preview;
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.3f, 0.8f, 1f, 0.6f);
            Gizmos.DrawWireCube(transform.position, size);
            Gizmos.color = new Color(1f, 0.85f, 0.2f, 0.8f);
            Gizmos.DrawWireCube(rebakeRegion.center, rebakeRegion.size);

            if (!drawNodes) return;
            CrawlGraph g = GetPreview();
            UnityEditor.SceneView view = UnityEditor.SceneView.lastActiveSceneView;
            if (g == null || view == null || view.camera == null) return;

            Vector3 eye = view.camera.transform.position;
            Vector3 focus = view.pivot;
            float rSq = gizmoRadius * gizmoRadius;
            Vector3 c = (eye - focus).sqrMagnitude < rSq ? eye : focus;

            Vector3 ext = Vector3.one * gizmoRadius;
            g.CellOf(c - ext, out int x0, out int y0, out int z0);
            g.CellOf(c + ext, out int x1, out int y1, out int z1);
            int cx0 = Mathf.Max(0, x0 / g.ChunkCells), cx1 = Mathf.Min(g.ChunksX - 1, Mathf.Max(0, x1) / g.ChunkCells);
            int cy0 = Mathf.Max(0, y0 / g.ChunkCells), cy1 = Mathf.Min(g.ChunksY - 1, Mathf.Max(0, y1) / g.ChunkCells);
            int cz0 = Mathf.Max(0, z0 / g.ChunkCells), cz1 = Mathf.Min(g.ChunksZ - 1, Mathf.Max(0, z1) / g.ChunkCells);

            Color floor = new Color(0.2f, 0.9f, 0.3f), wall = new Color(0.3f, 0.5f, 1f), ceiling = new Color(1f, 0.3f, 0.3f);
            for (int cz = cz0; cz <= cz1; cz++)
            for (int cy = cy0; cy <= cy1; cy++)
            for (int cx = cx0; cx <= cx1; cx++)
            {
                int chunk = cx + g.ChunksX * (cy + g.ChunksY * cz);
                for (int i = g.ChunkStart[chunk]; i < g.ChunkStart[chunk + 1]; i++)
                {
                    Vector3 p = g.GetPosition(i);
                    if ((p - c).sqrMagnitude > rSq) continue;
                    CrawlSurface s = g.GetSurface(i);
                    Gizmos.color = s == CrawlSurface.Floor ? floor : s == CrawlSurface.Wall ? wall : ceiling;
                    Gizmos.DrawLine(p, p + g.GetNormal(i) * (g.CellSize * 0.4f));
                    if (!drawEdges) continue;
                    Gizmos.color = new Color(1f, 1f, 1f, 0.25f);
                    for (int e = g.EdgeStart[i]; e < g.EdgeStart[i + 1]; e++)
                    {
                        int t = g.EdgeTarget[e];
                        if (t > i) Gizmos.DrawLine(p, g.GetPosition(t));
                    }
                }
            }
        }
#endif
    }
}
