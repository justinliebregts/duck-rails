using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Game.AI
{
    public enum CrawlSurface : byte { Floor = 0, Wall = 1, Ceiling = 2 }

    /// <summary>
    /// Baked crawl surface graph: one node per free grid cell that touches geometry (floor, wall or ceiling).
    /// Flat arrays only. Nodes are sorted by chunk, then cell key, so a chunk's nodes are contiguous.
    /// Clusters = connected node groups inside one chunk; they form the coarse graph for long searches.
    /// </summary>
    public sealed class CrawlGraph
    {
        const int Magic = 0x4C575243; // "CRWL"
        public const int FormatVersion = 1;
        public const float FloorMinY = 0.7f;
        public const float CeilingMaxY = -0.7f;

        // 26 neighbour directions; DirIndex maps (dx+1) + 3(dy+1) + 9(dz+1) -> 0..25, or -1 for the centre.
        public static readonly int[] DirX = new int[26];
        public static readonly int[] DirY = new int[26];
        public static readonly int[] DirZ = new int[26];
        static readonly int[] DirIndex = new int[27];

        static CrawlGraph()
        {
            int d = 0;
            for (int i = 0; i < 27; i++)
            {
                int dx = i % 3 - 1, dy = (i / 3) % 3 - 1, dz = i / 9 - 1;
                if (dx == 0 && dy == 0 && dz == 0) { DirIndex[i] = -1; continue; }
                DirX[d] = dx; DirY[d] = dy; DirZ[d] = dz;
                DirIndex[i] = d++;
            }
        }

        public static int DirectionIndex(int dx, int dy, int dz) => DirIndex[(dx + 1) + 3 * (dy + 1) + 9 * (dz + 1)];

        public Vector3 Origin;
        public float CellSize;
        public int DimX, DimY, DimZ;
        public int ChunkCells;
        public int ChunksX, ChunksY, ChunksZ;

        public int NodeCount;
        public int[] NodeKey;
        public float[] NodePos;          // xyz interleaved, surface point
        public int[] NodeNormal;         // packed, see PackNormal
        public byte[] NodeSurface;       // CrawlSurface
        public int[] NodeCluster;
        public int[] EdgeStart;          // NodeCount + 1
        public int[] EdgeTarget;
        public int[] ChunkStart;         // ChunkCount + 1

        public int ClusterCount;
        public float[] ClusterCenter;    // xyz interleaved
        public int[] ClusterComponent;
        public int[] ClusterNode;        // representative node (closest to centre)
        public int[] ClusterChunk;
        public int[] ClusterEdgeStart;   // ClusterCount + 1
        public int[] ClusterEdgeTarget;

        Bounds[] _allowedBounds;
        Matrix4x4[] _boundaryWorldToLocal;
        float _boundaryInset;
        bool[] _allowedNodes;
        public bool IsNodeAllowed(int node) => node >= 0 && node < NodeCount && (_allowedNodes == null || _allowedNodes[node]);

        public bool ContainsCrawlPosition(Vector3 position)
        {
            if (_allowedBounds == null) return true;
            return InsideBoundsUnion(position)
                && InsideBoundsUnion(position + Vector3.right * _boundaryInset)
                && InsideBoundsUnion(position - Vector3.right * _boundaryInset)
                && InsideBoundsUnion(position + Vector3.forward * _boundaryInset)
                && InsideBoundsUnion(position - Vector3.forward * _boundaryInset);
        }

        bool InsideBoundsUnion(Vector3 position)
        {
            for (int i = 0; i < _allowedBounds.Length; i++)
            {
                Vector3 point = _boundaryWorldToLocal == null ? position : _boundaryWorldToLocal[i].MultiplyPoint3x4(position);
                if (_allowedBounds[i].Contains(point)) return true;
            }
            return false;
        }

        public bool ContainsCrawlSegment(Vector3 from, Vector3 to)
        {
            if (_allowedBounds == null) return true;
            int steps = Mathf.Max(1, Mathf.CeilToInt(Vector3.Distance(from, to) / 0.1f));
            for (int i = 0; i <= steps; i++)
                if (!ContainsCrawlPosition(Vector3.Lerp(from, to, i / (float)steps))) return false;
            return true;
        }

        // Apply to the in-memory graph, preserving the baked asset and node IDs.
        public void RestrictToBounds(Bounds[] bounds, float inset = 0f, Matrix4x4[] worldToLocal = null)
        {
            _allowedBounds = bounds;
            _boundaryWorldToLocal = worldToLocal;
            _boundaryInset = inset;
            _allowedNodes = new bool[NodeCount];
            for (int c = 0; c < ClusterCount; c++) ClusterNode[c] = -1;
            for (int n = 0; n < NodeCount; n++)
            {
                _allowedNodes[n] = ContainsCrawlPosition(GetPosition(n));
                if (_allowedNodes[n] && ClusterNode[NodeCluster[n]] < 0) ClusterNode[NodeCluster[n]] = n;
            }
            var edges = new System.Collections.Generic.List<int>();
            var starts = new int[NodeCount + 1];
            var clusterEdges = new System.Collections.Generic.SortedSet<long>();
            for (int n = 0; n < NodeCount; n++)
            {
                starts[n] = edges.Count;
                if (!_allowedNodes[n]) continue;
                for (int e = EdgeStart[n]; e < EdgeStart[n + 1]; e++)
                {
                    int to = EdgeTarget[e];
                    if (!_allowedNodes[to] || !ContainsCrawlSegment(GetPosition(n), GetPosition(to))) continue;
                    edges.Add(to);
                    int a = NodeCluster[n], b = NodeCluster[to];
                    if (a != b) clusterEdges.Add(((long)a << 32) | (uint)b);
                }
            }
            starts[NodeCount] = edges.Count;
            EdgeStart = starts; EdgeTarget = edges.ToArray();
            ClusterEdgeStart = new int[ClusterCount + 1];
            ClusterEdgeTarget = new int[clusterEdges.Count];
            int index = 0;
            foreach (long edge in clusterEdges)
            {
                ClusterEdgeStart[(int)(edge >> 32) + 1]++;
                ClusterEdgeTarget[index++] = (int)(edge & 0xffffffff);
            }
            for (int c = 1; c <= ClusterCount; c++) ClusterEdgeStart[c] += ClusterEdgeStart[c - 1];
        }

        public int ChunkCount => ChunksX * ChunksY * ChunksZ;
        public int EdgeCount => EdgeTarget != null ? EdgeTarget.Length : 0;
        public float ChunkSize => ChunkCells * CellSize;

        public long MemoryBytes =>
            (long)NodeCount * (4 + 12 + 4 + 1 + 4 + 4) + (long)EdgeCount * 4 + (long)(ChunkCount + 1) * 4 +
            (long)ClusterCount * (12 + 4 + 4 + 4 + 4) + (ClusterEdgeTarget != null ? ClusterEdgeTarget.Length * 4L : 0L);

        // ------------------------------------------------------------------ node accessors

        public Vector3 GetPosition(int node)
        {
            int i = node * 3;
            return new Vector3(NodePos[i], NodePos[i + 1], NodePos[i + 2]);
        }

        public Vector3 GetNormal(int node) => UnpackNormal(NodeNormal[node]);

        public CrawlSurface GetSurface(int node) => (CrawlSurface)NodeSurface[node];

        public const float MaxGroundSlopeDegrees = 80f;
        static readonly float GroundMinY = Mathf.Cos(MaxGroundSlopeDegrees * Mathf.Deg2Rad);

        // Physics normals have full precision; include the boundary despite float rounding.
        public static bool IsWalkableGround(Vector3 normal) => normal.y >= GroundMinY - 0.000001f;
        // Baked normals use signed bytes. Allow their half-quantization error here;
        // the full-precision movement sweep still enforces the actual slope limit.
        public bool IsGroundNode(int node) => IsNodeAllowed(node) && GetNormal(node).y >= GroundMinY - 0.5f / 127f;

        public int GetComponent(int node) => ClusterComponent[NodeCluster[node]];

        public Vector3 GetClusterCenter(int cluster)
        {
            int i = cluster * 3;
            return new Vector3(ClusterCenter[i], ClusterCenter[i + 1], ClusterCenter[i + 2]);
        }

        public static CrawlSurface Classify(Vector3 normal) =>
            normal.y >= FloorMinY ? CrawlSurface.Floor : normal.y <= CeilingMaxY ? CrawlSurface.Ceiling : CrawlSurface.Wall;

        public static int PackNormal(Vector3 n)
        {
            int x = Mathf.Clamp(Mathf.RoundToInt(n.x * 127f), -127, 127) & 0xFF;
            int signedY = Mathf.Clamp(Mathf.RoundToInt(n.y * 127f), -127, 127);
            // Preserve the side of vertical for near-vertical slopes when baking.
            if (signedY == 0 && Mathf.Abs(n.y) > 0.00001f) signedY = n.y > 0f ? 1 : -1;
            int y = signedY & 0xFF;
            int z = Mathf.Clamp(Mathf.RoundToInt(n.z * 127f), -127, 127) & 0xFF;
            return x | (y << 8) | (z << 16);
        }

        public static Vector3 UnpackNormal(int packed)
        {
            Vector3 n = new Vector3((sbyte)(packed & 0xFF), (sbyte)((packed >> 8) & 0xFF), (sbyte)((packed >> 16) & 0xFF));
            return n.normalized;
        }

        // ------------------------------------------------------------------ cells

        public int KeyOf(int x, int y, int z) => x + DimX * (y + DimY * z);

        public void CoordsOf(int key, out int x, out int y, out int z)
        {
            x = key % DimX;
            int t = key / DimX;
            y = t % DimY;
            z = t / DimY;
        }

        public bool InBounds(int x, int y, int z) => x >= 0 && y >= 0 && z >= 0 && x < DimX && y < DimY && z < DimZ;

        public void CellOf(Vector3 p, out int x, out int y, out int z)
        {
            Vector3 l = (p - Origin) / CellSize;
            x = Mathf.FloorToInt(l.x);
            y = Mathf.FloorToInt(l.y);
            z = Mathf.FloorToInt(l.z);
        }

        public Vector3 CellCenter(int x, int y, int z) => Origin + new Vector3(x + 0.5f, y + 0.5f, z + 0.5f) * CellSize;

        public int ChunkOfCell(int x, int y, int z) =>
            x / ChunkCells + ChunksX * (y / ChunkCells + ChunksY * (z / ChunkCells));

        public int ChunkOfKey(int key)
        {
            CoordsOf(key, out int x, out int y, out int z);
            return ChunkOfCell(x, y, z);
        }

        /// <summary>Node at an exact cell, or -1.</summary>
        public int FindNodeAtCell(int x, int y, int z)
        {
            if (!InBounds(x, y, z)) return -1;
            int chunk = ChunkOfCell(x, y, z);
            int key = KeyOf(x, y, z);
            int lo = ChunkStart[chunk], hi = ChunkStart[chunk + 1] - 1;
            while (lo <= hi)
            {
                int mid = (lo + hi) >> 1;
                int k = NodeKey[mid];
                if (k == key) return mid;
                if (k < key) lo = mid + 1; else hi = mid - 1;
            }
            return -1;
        }

        /// <summary>Closest node to p by searching grid shells outward; -1 if nothing within maxRadius.</summary>
        public int FindNearestNode(Vector3 p, float maxRadius) => FindNearestNode(p, maxRadius, null);

        /// <summary>Closest node of the given surface within maxRadius, or -1.</summary>
        public int FindNearestNode(Vector3 p, float maxRadius, CrawlSurface surface) =>
            FindNearestNode(p, maxRadius, (CrawlSurface?)surface);

        public int FindNearestGroundNode(Vector3 p, float maxRadius) =>
            FindNearestNode(p, maxRadius, null, true);

        int FindNearestNode(Vector3 p, float maxRadius, CrawlSurface? surface, bool groundOnly = false)
        {
            using var sample = NearestNodeMarker.Auto();
            CellOf(p, out int cx, out int cy, out int cz);
            int maxShell = Mathf.Max(0, Mathf.CeilToInt(maxRadius / CellSize));
            float maxSq = maxRadius * maxRadius;
            int best = -1;
            float bestSq = float.MaxValue;
            int stopShell = int.MaxValue;

            for (int r = 0; r <= maxShell && r <= stopShell; r++)
            {
                for (int dz = -r; dz <= r; dz++)
                for (int dy = -r; dy <= r; dy++)
                for (int dx = -r; dx <= r; dx++)
                {
                    if (Mathf.Abs(dx) != r && Mathf.Abs(dy) != r && Mathf.Abs(dz) != r) continue;
                    int node = FindNodeAtCell(cx + dx, cy + dy, cz + dz);
                    if (!IsNodeAllowed(node)) continue;
                    if (surface.HasValue && GetSurface(node) != surface.Value) continue;
                    if (groundOnly && !IsGroundNode(node)) continue;
                    float sq = (GetPosition(node) - p).sqrMagnitude;
                    if (sq < bestSq && sq <= maxSq) { bestSq = sq; best = node; }
                }
                if (best >= 0 && stopShell == int.MaxValue) stopShell = r + 1;   // one more shell can still hold a closer node
            }
            return best;
        }

        static readonly Unity.Profiling.ProfilerMarker NearestNodeMarker = new Unity.Profiling.ProfilerMarker("EnemyPath.NearestNode");

        /// <summary>Random upward-facing node of the cluster, or -1 if none is found.</summary>
        public int RandomGroundNodeInCluster(int cluster)
        {
            int chunk = ClusterChunk[cluster];
            int start = ChunkStart[chunk], end = ChunkStart[chunk + 1];
            for (int tries = 0; tries < 24; tries++)
            {
                int node = UnityEngine.Random.Range(start, end);
                if (NodeCluster[node] == cluster && IsGroundNode(node)) return node;
            }
            int rep = ClusterNode[cluster];
            return IsGroundNode(rep) ? rep : -1;
        }

        /// <summary>Random node of the cluster (no allocation). Falls back to the representative node.</summary>
        public int RandomNodeInCluster(int cluster)
        {
            int chunk = ClusterChunk[cluster];
            int start = ChunkStart[chunk], end = ChunkStart[chunk + 1];
            for (int tries = 0; tries < 16; tries++)
            {
                int node = UnityEngine.Random.Range(start, end);
                if (NodeCluster[node] == cluster && IsNodeAllowed(node)) return node;
            }
            return ClusterNode[cluster];
        }

        /// <summary>
        /// Walks graph edges away from the player and returns a node to path to, or -1.
        /// Backward steps are rejected so the route stays on the walkable graph.
        /// </summary>
        public int FindRetreatNode(int start, Vector3 origin, Vector3 away, float distance, bool groundOnly, HashSet<int> visited)
        {
            if (!IsNodeAllowed(start) || visited == null || away.sqrMagnitude < 1e-6f) return -1;
            if (groundOnly && !IsGroundNode(start)) return -1;

            Vector3 awayDirection = away.normalized;
            float travel = distance < 0.5f ? 0.5f : distance;
            visited.Clear();
            visited.Add(start);
            int node = start;
            int best = -1;
            float bestScore = 0.5f;
            const int MaxSteps = 64;
            for (int step = 0; step < MaxSteps; step++)
            {
                Vector3 from = GetPosition(node);
                float currentAlong = Vector3.Dot(from - origin, awayDirection);
                int pick = -1;
                float pickScore = float.NegativeInfinity;
                int edgeEnd = EdgeStart[node + 1];
                for (int edge = EdgeStart[node]; edge < edgeEnd; edge++)
                {
                    int next = EdgeTarget[edge];
                    if (!IsNodeAllowed(next) || visited.Contains(next)) continue;
                    if (groundOnly && !IsGroundNode(next)) continue;
                    Vector3 nextPosition = GetPosition(next);
                    float along = Vector3.Dot(nextPosition - origin, awayDirection);
                    if (along < currentAlong - 0.2f) continue;
                    float score = along * 3f + (nextPosition - origin).magnitude;
                    bool better = score > pickScore + 0.05f;
                    bool tie = pick >= 0 && !better && score >= pickScore - 0.05f && UnityEngine.Random.Range(0, 2) == 0;
                    if (pick < 0 || better || tie)
                    {
                        pickScore = score;
                        pick = next;
                    }
                }
                if (pick < 0) break;

                visited.Add(pick);
                node = pick;
                Vector3 point = GetPosition(node);
                float nodeAlong = Vector3.Dot(point - origin, awayDirection);
                if (nodeAlong < -0.25f) break;
                float nodeScore = nodeAlong * 3f + (point - origin).magnitude;
                if (nodeScore > bestScore)
                {
                    bestScore = nodeScore;
                    best = node;
                }
                if (nodeAlong >= travel) break;
            }
            return best;
        }

        /// <summary>Uniformly random cluster of the given component within radius of center (reservoir sampling), or -1.</summary>
        public int RandomClusterNear(Vector3 center, float radius, int component, int excludeCluster)
        {
            float rSq = radius * radius;
            int chosen = -1, seen = 0;
            for (int c = 0; c < ClusterCount; c++)
            {
                if (c == excludeCluster || ClusterComponent[c] != component || ClusterNode[c] < 0) continue;
                if ((GetClusterCenter(c) - center).sqrMagnitude > rSq) continue;
                seen++;
                if (UnityEngine.Random.Range(0, seen) == 0) chosen = c;
            }
            return chosen;
        }

        // ------------------------------------------------------------------ serialization

        public static CrawlGraph Load(TextAsset asset)
        {
            if (asset == null) return null;
            return Load(asset.bytes);
        }

        public static CrawlGraph Load(byte[] data)
        {
            int o = 0;
            if (data == null || data.Length < 8 || ReadInt(data, ref o) != Magic)
            {
                {
                    if (DuckzLog.AllowError(out var logNotice1))
                        logNotice1.Write("[CrawlGraph] Not a crawl graph file.");
                }
                return null;
            }
            int version = ReadInt(data, ref o);
            if (version != FormatVersion)
            {
                {
                    if (DuckzLog.AllowError(out var logNotice2))
                        logNotice2.Write($"[CrawlGraph] Format version {version} != {FormatVersion}. Rebake the graph.");
                }
                return null;
            }

            CrawlGraph g = new CrawlGraph();
            g.Origin = new Vector3(ReadFloat(data, ref o), ReadFloat(data, ref o), ReadFloat(data, ref o));
            g.CellSize = ReadFloat(data, ref o);
            g.DimX = ReadInt(data, ref o); g.DimY = ReadInt(data, ref o); g.DimZ = ReadInt(data, ref o);
            g.ChunkCells = ReadInt(data, ref o);
            g.ChunksX = ReadInt(data, ref o); g.ChunksY = ReadInt(data, ref o); g.ChunksZ = ReadInt(data, ref o);
            g.NodeCount = ReadInt(data, ref o);
            int edgeCount = ReadInt(data, ref o);
            g.ClusterCount = ReadInt(data, ref o);
            int clusterEdgeCount = ReadInt(data, ref o);

            int n = g.NodeCount, c = g.ClusterCount;
            g.NodeKey = ReadInts(data, ref o, n);
            g.NodePos = ReadFloats(data, ref o, n * 3);
            g.NodeNormal = ReadInts(data, ref o, n);
            g.NodeSurface = ReadBytes(data, ref o, n);
            g.NodeCluster = ReadInts(data, ref o, n);
            g.EdgeStart = ReadInts(data, ref o, n + 1);
            g.EdgeTarget = ReadInts(data, ref o, edgeCount);
            g.ChunkStart = ReadInts(data, ref o, g.ChunkCount + 1);
            g.ClusterCenter = ReadFloats(data, ref o, c * 3);
            g.ClusterComponent = ReadInts(data, ref o, c);
            g.ClusterNode = ReadInts(data, ref o, c);
            g.ClusterChunk = ReadInts(data, ref o, c);
            g.ClusterEdgeStart = ReadInts(data, ref o, c + 1);
            g.ClusterEdgeTarget = ReadInts(data, ref o, clusterEdgeCount);
            return g;
        }

        public void Write(Stream stream)
        {
            using (BinaryWriter w = new BinaryWriter(stream))
            {
                w.Write(Magic);
                w.Write(FormatVersion);
                w.Write(Origin.x); w.Write(Origin.y); w.Write(Origin.z);
                w.Write(CellSize);
                w.Write(DimX); w.Write(DimY); w.Write(DimZ);
                w.Write(ChunkCells);
                w.Write(ChunksX); w.Write(ChunksY); w.Write(ChunksZ);
                w.Write(NodeCount);
                w.Write(EdgeTarget.Length);
                w.Write(ClusterCount);
                w.Write(ClusterEdgeTarget.Length);

                WriteArray(w, NodeKey);
                WriteArray(w, NodePos);
                WriteArray(w, NodeNormal);
                w.Write(NodeSurface);
                WriteArray(w, NodeCluster);
                WriteArray(w, EdgeStart);
                WriteArray(w, EdgeTarget);
                WriteArray(w, ChunkStart);
                WriteArray(w, ClusterCenter);
                WriteArray(w, ClusterComponent);
                WriteArray(w, ClusterNode);
                WriteArray(w, ClusterChunk);
                WriteArray(w, ClusterEdgeStart);
                WriteArray(w, ClusterEdgeTarget);
            }
        }

        static void WriteArray(BinaryWriter w, int[] a)
        {
            byte[] b = new byte[a.Length * 4];
            Buffer.BlockCopy(a, 0, b, 0, b.Length);
            w.Write(b);
        }

        static void WriteArray(BinaryWriter w, float[] a)
        {
            byte[] b = new byte[a.Length * 4];
            Buffer.BlockCopy(a, 0, b, 0, b.Length);
            w.Write(b);
        }

        static int ReadInt(byte[] d, ref int o) { int v = BitConverter.ToInt32(d, o); o += 4; return v; }
        static float ReadFloat(byte[] d, ref int o) { float v = BitConverter.ToSingle(d, o); o += 4; return v; }

        static int[] ReadInts(byte[] d, ref int o, int count)
        {
            int[] a = new int[count];
            Buffer.BlockCopy(d, o, a, 0, count * 4);
            o += count * 4;
            return a;
        }

        static float[] ReadFloats(byte[] d, ref int o, int count)
        {
            float[] a = new float[count];
            Buffer.BlockCopy(d, o, a, 0, count * 4);
            o += count * 4;
            return a;
        }

        static byte[] ReadBytes(byte[] d, ref int o, int count)
        {
            byte[] a = new byte[count];
            Buffer.BlockCopy(d, o, a, 0, count);
            o += count;
            return a;
        }
    }
}
