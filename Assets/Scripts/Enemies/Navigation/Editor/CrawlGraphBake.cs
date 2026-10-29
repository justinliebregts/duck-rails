using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Unity.Collections;
using Unity.Jobs;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Game.AI.Editor
{
    /// <summary>
    /// Editor-only crawl graph bake. Steps: chunk prefilter, free/near cell overlap batch, 26-ray surface batch,
    /// batched edge linecasts, blocker reject, seed flood-fill prune, per-chunk clusters, sort + write .bytes.
    /// Region rebake reloads the existing file, resamples chunks inside the region and re-tests edges
    /// for those chunks plus a one-chunk border.
    /// </summary>
    public static class CrawlGraphBake
    {
        const int ChunkCells = 16;
        const int EdgeBatch = 262144;
        const string Title = "Crawl Graph Bake";
        const float CornerNormalDot = 0.35f;
        // Match the shared ground motor's riser limit. Clearance covers the largest
        // current ground prefab (0.35 radius / 0.8 height at 1.2 scale).
        const float StairRise = 0.3f;
        const float StairRadius = 0.42f;
        const float StairHeight = 0.96f;

        static readonly Vector3[] RayDirs = BuildRayDirs();
        static readonly Vector3[] TreadOffsets = {
            Vector3.right, Vector3.left, Vector3.forward, Vector3.back,
            new Vector3(1, 0, 1), new Vector3(-1, 0, 1),
            new Vector3(1, 0, -1), new Vector3(-1, 0, -1)
        };

        static Vector3[] BuildRayDirs()
        {
            Vector3[] dirs = new Vector3[26];
            for (int d = 0; d < 26; d++)
                dirs[d] = new Vector3(CrawlGraph.DirX[d], CrawlGraph.DirY[d], CrawlGraph.DirZ[d]).normalized;
            return dirs;
        }

        static int Opposite(int d) => CrawlGraph.DirectionIndex(-CrawlGraph.DirX[d], -CrawlGraph.DirY[d], -CrawlGraph.DirZ[d]);

        sealed class Work
        {
            public readonly CrawlGraph grid;
            public List<int> keys = new List<int>();
            public List<Vector3> pos = new List<Vector3>();
            public List<Vector3> normal = new List<Vector3>();
            public List<uint> mask = new List<uint>();
            public readonly Dictionary<int, int> index = new Dictionary<int, int>();

            public Work(CrawlGraph grid) { this.grid = grid; }

            public void Add(int key, Vector3 p, Vector3 n, uint m)
            {
                keys.Add(key); pos.Add(p); normal.Add(n); mask.Add(m);
            }

            public void RebuildIndex()
            {
                index.Clear();
                for (int i = 0; i < keys.Count; i++) index[keys[i]] = i;
            }

            public void Filter(bool[] keep)
            {
                List<int> k = new List<int>(keys.Count);
                List<Vector3> p = new List<Vector3>(keys.Count);
                List<Vector3> n = new List<Vector3>(keys.Count);
                List<uint> m = new List<uint>(keys.Count);
                for (int i = 0; i < keys.Count; i++)
                {
                    if (!keep[i]) continue;
                    k.Add(keys[i]); p.Add(pos[i]); n.Add(normal[i]); m.Add(mask[i]);
                }
                keys = k; pos = p; normal = n; mask = m;
            }

            public bool TryNeighbor(int key, int d, out int neighborKey, out int neighbor)
            {
                grid.CoordsOf(key, out int x, out int y, out int z);
                int nx = x + CrawlGraph.DirX[d], ny = y + CrawlGraph.DirY[d], nz = z + CrawlGraph.DirZ[d];
                neighbor = -1;
                neighborKey = -1;
                if (!grid.InBounds(nx, ny, nz)) return false;
                neighborKey = grid.KeyOf(nx, ny, nz);
                return index.TryGetValue(neighborKey, out neighbor);
            }
        }

        // ------------------------------------------------------------------ entry

        public static bool Bake(CrawlGraphBaker baker, bool regionOnly)
        {
            Stopwatch sw = Stopwatch.StartNew();
            CrawlGraph grid = MakeGrid(baker);
            if (grid == null) return false;

            Work w = new Work(grid);
            bool[] resample = new bool[grid.ChunkCount];
            bool[] retest = new bool[grid.ChunkCount];

            if (regionOnly)
            {
                CrawlGraph old = baker.graphAsset != null ? CrawlGraph.Load(baker.graphAsset) : null;
                if (old == null || !SameGrid(old, grid))
                {
                    EditorUtility.DisplayDialog(Title, "No existing graph with matching volume and cell size. Run a full Bake first.", "OK");
                    return false;
                }
                Decode(old, w);
                MarkRegion(grid, baker.rebakeRegion, resample, retest);
            }
            else
            {
                for (int i = 0; i < resample.Length; i++) { resample[i] = true; retest[i] = true; }
            }

            RemoveChunks(w, resample);
            Physics.SyncTransforms();
            CrawlGraphBlocker[] blockers = CollectBlockers();

            int pruned;
            int componentsKept;
            int blocked = 0;
            try
            {
                if (!SampleChunks(w, baker, resample, blockers, ref blocked)) return Cancelled();
                if (blockers.Length > 0)
                    blocked += RemoveBlockedNodes(w, blockers);
                w.RebuildIndex();
                if (!ComputeEdges(w, baker, retest)) return Cancelled();
                if (blockers.Length > 0)
                    CutBlockedEdges(w, blockers);
                EditorUtility.DisplayProgressBar(Title, "Pruning and clustering", 0.95f);
                pruned = Prune(w, baker, out componentsKept);
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            CrawlGraph g = Build(w);
            string path = WriteAsset(baker, g, out long fileBytes);

            int floors = 0, walls = 0, ceilings = 0;
            for (int i = 0; i < g.NodeCount; i++)
            {
                CrawlSurface s = g.GetSurface(i);
                if (s == CrawlSurface.Floor) floors++; else if (s == CrawlSurface.Wall) walls++; else ceilings++;
            }

            string report =
                $"{(regionOnly ? "Region rebake" : "Full bake")} {DateTime.Now:g}  ({sw.Elapsed.TotalSeconds:0.0}s)\n" +
                $"Nodes {g.NodeCount:N0}  (floor {floors:N0}, wall {walls:N0}, ceiling {ceilings:N0})\n" +
                $"Edges {g.EdgeCount:N0}  Clusters {g.ClusterCount:N0}  Components {componentsKept}  Pruned {pruned:N0}  Blocked {blocked:N0}\n" +
                $"Runtime ~{g.MemoryBytes / (1024f * 1024f):0.0} MB  File {fileBytes / (1024f * 1024f):0.0} MB\n" +
                $"Grid {g.DimX}x{g.DimY}x{g.DimZ} @ {g.CellSize}m, {g.ChunkCount:N0} chunks\n{path}";

            Undo.RecordObject(baker, "Bake Crawl Graph");
            baker.graphAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(path);
            baker.lastBakeReport = report;
            baker.InvalidatePreview();
            EditorUtility.SetDirty(baker);
            Debug.Log("[CrawlGraph] " + report, baker);
            return true;
        }

        static bool Cancelled()
        {
            Debug.LogWarning("[CrawlGraph] Bake cancelled.");
            return false;
        }

        // ------------------------------------------------------------------ setup

        static CrawlGraph MakeGrid(CrawlGraphBaker b)
        {
            Bounds bounds = b.BakeBounds;
            CrawlGraph g = new CrawlGraph
            {
                Origin = bounds.min,
                CellSize = b.cellSize,
                DimX = Mathf.Max(1, Mathf.CeilToInt(bounds.size.x / b.cellSize)),
                DimY = Mathf.Max(1, Mathf.CeilToInt(bounds.size.y / b.cellSize)),
                DimZ = Mathf.Max(1, Mathf.CeilToInt(bounds.size.z / b.cellSize)),
                ChunkCells = ChunkCells,
            };
            g.ChunksX = (g.DimX + ChunkCells - 1) / ChunkCells;
            g.ChunksY = (g.DimY + ChunkCells - 1) / ChunkCells;
            g.ChunksZ = (g.DimZ + ChunkCells - 1) / ChunkCells;

            if ((long)g.DimX * g.DimY * g.DimZ >= int.MaxValue)
            {
                EditorUtility.DisplayDialog(Title, "Volume too large for this cell size. Increase the cell size or shrink the box.", "OK");
                return null;
            }
            return g;
        }

        static bool SameGrid(CrawlGraph a, CrawlGraph b) =>
            (a.Origin - b.Origin).sqrMagnitude < 1e-6f && Mathf.Approximately(a.CellSize, b.CellSize) &&
            a.DimX == b.DimX && a.DimY == b.DimY && a.DimZ == b.DimZ && a.ChunkCells == b.ChunkCells;

        static void Decode(CrawlGraph old, Work w)
        {
            for (int i = 0; i < old.NodeCount; i++)
            {
                int key = old.NodeKey[i];
                old.CoordsOf(key, out int x, out int y, out int z);
                uint m = 0;
                for (int e = old.EdgeStart[i]; e < old.EdgeStart[i + 1]; e++)
                {
                    old.CoordsOf(old.NodeKey[old.EdgeTarget[e]], out int tx, out int ty, out int tz);
                    int d = CrawlGraph.DirectionIndex(tx - x, ty - y, tz - z);
                    if (d >= 0) m |= 1u << d;
                }
                w.Add(key, old.GetPosition(i), old.GetNormal(i), m);
            }
        }

        static void MarkRegion(CrawlGraph g, Bounds region, bool[] resample, bool[] retest)
        {
            g.CellOf(region.min, out int x0, out int y0, out int z0);
            g.CellOf(region.max, out int x1, out int y1, out int z1);
            int cx0 = Mathf.Clamp(x0, 0, g.DimX - 1) / g.ChunkCells, cx1 = Mathf.Clamp(x1, 0, g.DimX - 1) / g.ChunkCells;
            int cy0 = Mathf.Clamp(y0, 0, g.DimY - 1) / g.ChunkCells, cy1 = Mathf.Clamp(y1, 0, g.DimY - 1) / g.ChunkCells;
            int cz0 = Mathf.Clamp(z0, 0, g.DimZ - 1) / g.ChunkCells, cz1 = Mathf.Clamp(z1, 0, g.DimZ - 1) / g.ChunkCells;

            for (int cz = cz0 - 1; cz <= cz1 + 1; cz++)
            for (int cy = cy0 - 1; cy <= cy1 + 1; cy++)
            for (int cx = cx0 - 1; cx <= cx1 + 1; cx++)
            {
                if (cx < 0 || cy < 0 || cz < 0 || cx >= g.ChunksX || cy >= g.ChunksY || cz >= g.ChunksZ) continue;
                int chunk = cx + g.ChunksX * (cy + g.ChunksY * cz);
                retest[chunk] = true;
                if (cx >= cx0 && cx <= cx1 && cy >= cy0 && cy <= cy1 && cz >= cz0 && cz <= cz1)
                    resample[chunk] = true;
            }
        }

        static void RemoveChunks(Work w, bool[] chunks)
        {
            bool[] keep = new bool[w.keys.Count];
            for (int i = 0; i < keep.Length; i++) keep[i] = !chunks[w.grid.ChunkOfKey(w.keys[i])];
            w.Filter(keep);
        }

        // ------------------------------------------------------------------ blockers

        static CrawlGraphBlocker[] CollectBlockers()
        {
            CrawlGraphBlocker[] found = UnityEngine.Object.FindObjectsByType<CrawlGraphBlocker>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            int count = 0;
            for (int i = 0; i < found.Length; i++)
                if (found[i].enabled) count++;
            if (count == found.Length) return found;

            CrawlGraphBlocker[] active = new CrawlGraphBlocker[count];
            int n = 0;
            for (int i = 0; i < found.Length; i++)
                if (found[i].enabled) active[n++] = found[i];
            return active;
        }

        static bool ChunkInsideBlocker(CrawlGraphBlocker[] blockers, Vector3 min, Vector3 max)
        {
            for (int i = 0; i < blockers.Length; i++)
                if (blockers[i].ContainsBounds(min, max)) return true;
            return false;
        }

        static bool NodeInsideBlocker(CrawlGraphBlocker[] blockers, Vector3 point, Vector3 cellCenter)
        {
            for (int i = 0; i < blockers.Length; i++)
            {
                CrawlGraphBlocker blocker = blockers[i];
                if (blocker.Contains(point) || blocker.Contains(cellCenter)) return true;
            }
            return false;
        }

        static bool SegmentHitsBlocker(CrawlGraphBlocker[] blockers, Vector3 a, Vector3 b)
        {
            for (int i = 0; i < blockers.Length; i++)
                if (blockers[i].IntersectsSegment(a, b)) return true;
            return false;
        }

        /// <summary>Drops every node already in the working set that sits inside a blocker. Covers nodes kept by a region rebake.</summary>
        static int RemoveBlockedNodes(Work w, CrawlGraphBlocker[] blockers)
        {
            int n = w.keys.Count;
            if (n == 0) return 0;
            bool[] keep = new bool[n];
            int removed = 0;
            CrawlGraph g = w.grid;
            for (int i = 0; i < n; i++)
            {
                g.CoordsOf(w.keys[i], out int x, out int y, out int z);
                bool inside = NodeInsideBlocker(blockers, w.pos[i], g.CellCenter(x, y, z));
                keep[i] = !inside;
                if (inside) removed++;
            }
            if (removed > 0) w.Filter(keep);
            return removed;
        }

        /// <summary>Clears links whose surface segment or cell-center segment crosses a blocker.</summary>
        static void CutBlockedEdges(Work w, CrawlGraphBlocker[] blockers)
        {
            CrawlGraph g = w.grid;
            for (int a = 0; a < w.keys.Count; a++)
            {
                uint mask = w.mask[a];
                if (mask == 0) continue;
                int key = w.keys[a];
                g.CoordsOf(key, out int x, out int y, out int z);
                Vector3 point = w.pos[a];
                Vector3 cell = g.CellCenter(x, y, z);
                for (int d = 0; d < 26; d++)
                {
                    if ((mask & (1u << d)) == 0) continue;
                    if (!w.TryNeighbor(key, d, out int neighborKey, out int neighbor)) continue;
                    if (neighborKey < key) continue;

                    g.CoordsOf(neighborKey, out int nx, out int ny, out int nz);
                    bool hit = SegmentHitsBlocker(blockers, point, w.pos[neighbor])
                        || SegmentHitsBlocker(blockers, cell, g.CellCenter(nx, ny, nz));
                    if (!hit) continue;

                    w.mask[a] &= ~(1u << d);
                    w.mask[neighbor] &= ~(1u << Opposite(d));
                }
            }
        }

        // ------------------------------------------------------------------ sampling

        static bool SampleChunks(Work w, CrawlGraphBaker b, bool[] resample, CrawlGraphBlocker[] blockers, ref int blocked)
        {
            CrawlGraph g = w.grid;
            int maxCells = ChunkCells * ChunkCells * ChunkCells;
            QueryParameters qp = new QueryParameters(b.surfaceMask, false, QueryTriggerInteraction.Ignore, false);
            // Cap free radius: agentRadius alone leaves <1 cell of valid band, so slope creases and
            // building links disappear and get seed-pruned. 2% of cell keeps centers just outside geometry.
            float freeRadius = Mathf.Min(b.agentRadius, g.CellSize * 0.02f);
            // Short rays miss thin seams (warehouse rack strips): overlap says near, but the
            // surface sits just past one cell. Allow a little extra and keep the hit in-band.
            float sampleRay = g.CellSize * 1.25f;
            int todo = 0;
            for (int i = 0; i < resample.Length; i++) if (resample[i]) todo++;

            NativeArray<OverlapSphereCommand> overlapCmds = new NativeArray<OverlapSphereCommand>(maxCells * 2, Allocator.Persistent);
            NativeArray<ColliderHit> overlapHits = new NativeArray<ColliderHit>(maxCells * 2, Allocator.Persistent);
            NativeArray<RaycastCommand> rayCmds = new NativeArray<RaycastCommand>(maxCells * 26, Allocator.Persistent);
            NativeArray<RaycastHit> rayHits = new NativeArray<RaycastHit>(maxCells * 26, Allocator.Persistent);
            int[] nearCells = new int[maxCells];

            try
            {
                int done = 0;
                for (int cz = 0; cz < g.ChunksZ; cz++)
                for (int cy = 0; cy < g.ChunksY; cy++)
                for (int cx = 0; cx < g.ChunksX; cx++)
                {
                    int chunk = cx + g.ChunksX * (cy + g.ChunksY * cz);
                    if (!resample[chunk]) continue;
                    done++;
                    if ((done & 15) == 0 &&
                        EditorUtility.DisplayCancelableProgressBar(Title, $"Sampling chunk {done:N0} / {todo:N0}", 0.7f * done / todo))
                        return false;

                    int x0 = cx * ChunkCells, y0 = cy * ChunkCells, z0 = cz * ChunkCells;
                    int x1 = Mathf.Min(x0 + ChunkCells, g.DimX), y1 = Mathf.Min(y0 + ChunkCells, g.DimY), z1 = Mathf.Min(z0 + ChunkCells, g.DimZ);
                    Vector3 min = g.Origin + new Vector3(x0, y0, z0) * g.CellSize;
                    Vector3 max = g.Origin + new Vector3(x1, y1, z1) * g.CellSize;
                    if (ChunkInsideBlocker(blockers, min, max)) continue;
                    if (!Physics.CheckBox((min + max) * 0.5f, (max - min) * 0.5f + Vector3.one * g.CellSize,
                            Quaternion.identity, b.surfaceMask, QueryTriggerInteraction.Ignore))
                        continue;

                    int sx = x1 - x0, sy = y1 - y0;
                    int n = sx * sy * (z1 - z0);
                    for (int i = 0; i < n; i++)
                    {
                        Vector3 c = g.CellCenter(x0 + i % sx, y0 + (i / sx) % sy, z0 + i / (sx * sy));
                        overlapCmds[2 * i] = new OverlapSphereCommand(c, freeRadius, qp);
                        overlapCmds[2 * i + 1] = new OverlapSphereCommand(c, g.CellSize, qp);
                    }
                    OverlapSphereCommand.ScheduleBatch(overlapCmds.GetSubArray(0, n * 2), overlapHits.GetSubArray(0, n * 2), 32, 1, default(JobHandle)).Complete();

                    int near = 0;
                    for (int i = 0; i < n; i++)
                    {
                        if (overlapHits[2 * i].collider == null && overlapHits[2 * i + 1].collider != null)
                            nearCells[near++] = i;
                    }
                    if (near == 0) continue;

                    for (int k = 0; k < near; k++)
                    {
                        int i = nearCells[k];
                        Vector3 c = g.CellCenter(x0 + i % sx, y0 + (i / sx) % sy, z0 + i / (sx * sy));
                        for (int d = 0; d < 26; d++)
                            rayCmds[k * 26 + d] = new RaycastCommand(c, RayDirs[d], qp, sampleRay);
                    }
                    RaycastCommand.ScheduleBatch(rayCmds.GetSubArray(0, near * 26), rayHits.GetSubArray(0, near * 26), 64, 1, default(JobHandle)).Complete();

                    for (int k = 0; k < near; k++)
                    {
                        int i = nearCells[k];
                        Vector3 c = g.CellCenter(x0 + i % sx, y0 + (i / sx) % sy, z0 + i / (sx * sy));
                        float bestDist = float.MaxValue, bestWallDist = float.MaxValue;
                        Vector3 bestPoint = Vector3.zero, bestNormal = Vector3.up;
                        Vector3 wallPoint = Vector3.zero, wallNormal = Vector3.up;
                        for (int d = 0; d < 26; d++)
                        {
                            RaycastHit h = rayHits[k * 26 + d];
                            if (h.collider == null || h.distance > g.CellSize * 1.15f) continue;
                            if (h.distance < bestDist) { bestDist = h.distance; bestPoint = h.point; bestNormal = h.normal; }
                            if (CrawlGraph.Classify(h.normal) == CrawlSurface.Wall && h.distance < bestWallDist)
                            {
                                bestWallDist = h.distance;
                                wallPoint = h.point;
                                wallNormal = h.normal;
                            }
                        }
                        // 26 rays can miss geometry between their directions; aim one ray at the near collider's AABB.
                        if (bestDist == float.MaxValue)
                        {
                            Collider nearCol = overlapHits[2 * i + 1].collider;
                            if (nearCol != null)
                            {
                                Vector3 aim = nearCol.bounds.ClosestPoint(c) - c;
                                float aimLen = aim.magnitude;
                                if (aimLen > 1e-4f &&
                                    Physics.Raycast(c, aim / aimLen, out RaycastHit ah, sampleRay, b.surfaceMask, QueryTriggerInteraction.Ignore) &&
                                    ah.distance <= g.CellSize * 1.15f)
                                {
                                    bestDist = ah.distance;
                                    bestPoint = ah.point;
                                    bestNormal = ah.normal;
                                    if (CrawlGraph.Classify(ah.normal) == CrawlSurface.Wall && ah.distance < bestWallDist)
                                    {
                                        bestWallDist = ah.distance;
                                        wallPoint = ah.point;
                                        wallNormal = ah.normal;
                                    }
                                }
                            }
                            if (bestDist == float.MaxValue) continue;
                        }

                        // One node/cell: floor often wins at wall bases. Prefer a clear wall hit when close.
                        if (bestWallDist < float.MaxValue &&
                            CrawlGraph.Classify(bestNormal) != CrawlSurface.Wall &&
                            bestWallDist <= g.CellSize * 0.7f &&
                            bestWallDist <= bestDist + g.CellSize * 0.2f)
                        {
                            bestDist = bestWallDist;
                            bestPoint = wallPoint;
                            bestNormal = wallNormal;
                        }

                        // A riser must not steal the only node in a tread's cell.
                        // Reuse the downward ray from the batch for the center;
                        // only blocked centers need the nearby clearance probes.
                        RaycastHit tread = rayHits[k * 26 + CrawlGraph.DirectionIndex(0, -1, 0)];
                        if (tread.collider != null && tread.distance <= g.CellSize * 1.15f &&
                            tread.normal.y >= CrawlGraph.FloorMinY &&
                            TryTreadSample(tread, g.CellSize, b.surfaceMask, out Vector3 treadPoint, out Vector3 treadNormal))
                        {
                            bestPoint = treadPoint;
                            bestNormal = treadNormal;
                        }

                        if (!b.allowCeilings && bestNormal.y <= CrawlGraph.CeilingMaxY)
                            continue;
                        if (NodeInsideBlocker(blockers, bestPoint, c))
                        {
                            blocked++;
                            continue;
                        }

                        w.Add(g.KeyOf(x0 + i % sx, y0 + (i / sx) % sy, z0 + i / (sx * sy)), bestPoint, bestNormal, 0u);
                    }
                }
                return true;
            }
            finally
            {
                overlapCmds.Dispose(); overlapHits.Dispose(); rayCmds.Dispose(); rayHits.Dispose();
            }
        }

        // ------------------------------------------------------------------ edges

        static bool ComputeEdges(Work w, CrawlGraphBaker b, bool[] retest)
        {
            CrawlGraph g = w.grid;
            QueryParameters qp = new QueryParameters(b.surfaceMask, false, QueryTriggerInteraction.Ignore, false);
            // Two casts per pair (cell-center + surface-offset). Accept if either is clear.
            // When both hit and the faces turn, link them only if the exterior corner of the two planes is clear.
            int pairCap = EdgeBatch / 2;
            NativeArray<RaycastCommand> cmds = new NativeArray<RaycastCommand>(EdgeBatch, Allocator.Persistent);
            NativeArray<RaycastHit> hits = new NativeArray<RaycastHit>(EdgeBatch, Allocator.Persistent);
            int[] pairA = new int[pairCap], pairB = new int[pairCap];
            byte[] pairDir = new byte[pairCap];
            int count = 0;
            float skin = Mathf.Max(0.05f, b.agentRadius * 0.5f);

            try
            {
                for (int a = 0; a < w.keys.Count; a++)
                {
                    int key = w.keys[a];
                    if (!retest[g.ChunkOfKey(key)]) continue;
                    if ((a & 4095) == 0 &&
                        EditorUtility.DisplayCancelableProgressBar(Title, $"Testing edges {a:N0} / {w.keys.Count:N0}", 0.7f + 0.25f * a / w.keys.Count))
                        return false;

                    g.CoordsOf(key, out int x, out int y, out int z);
                    Vector3 ca = g.CellCenter(x, y, z);
                    Vector3 sa = w.pos[a] + w.normal[a] * skin;
                    for (int d = 0; d < 26; d++)
                    {
                        if (!w.TryNeighbor(key, d, out int nkey, out int nb))
                        {
                            w.mask[a] &= ~(1u << d);
                            continue;
                        }
                        if (nkey < key && retest[g.ChunkOfKey(nkey)]) continue;   // the other node tests this pair

                        Vector3 cb = g.CellCenter(x + CrawlGraph.DirX[d], y + CrawlGraph.DirY[d], z + CrawlGraph.DirZ[d]);
                        Vector3 cellDelta = cb - ca;
                        float cellMag = cellDelta.magnitude;
                        Vector3 sb = w.pos[nb] + w.normal[nb] * skin;
                        Vector3 surfDelta = sb - sa;
                        float surfMag = surfDelta.magnitude;

                        int baseCmd = count * 2;
                        cmds[baseCmd] = new RaycastCommand(ca, cellDelta / cellMag, qp, cellMag);
                        // Identical surface points: probe outward along normal (already skinned into free space).
                        if (surfMag < 1e-5f)
                            cmds[baseCmd + 1] = new RaycastCommand(sa, w.normal[a], qp, 0.001f);
                        else
                            cmds[baseCmd + 1] = new RaycastCommand(sa, surfDelta / surfMag, qp, surfMag);

                        pairA[count] = a; pairB[count] = nb; pairDir[count] = (byte)d;
                        if (++count == pairCap)
                        {
                            FlushEdges(w, cmds, hits, pairA, pairB, pairDir, count, skin, qp);
                            count = 0;
                        }
                    }
                }
                FlushEdges(w, cmds, hits, pairA, pairB, pairDir, count, skin, qp);
                return true;
            }
            finally
            {
                cmds.Dispose(); hits.Dispose();
            }
        }

        static void FlushEdges(Work w, NativeArray<RaycastCommand> cmds, NativeArray<RaycastHit> hits,
            int[] pairA, int[] pairB, byte[] pairDir, int count, float skin, QueryParameters qp)
        {
            if (count == 0) return;
            int cmdCount = count * 2;
            RaycastCommand.ScheduleBatch(cmds.GetSubArray(0, cmdCount), hits.GetSubArray(0, cmdCount), 64, 1, default(JobHandle)).Complete();

            int[] cornerOf = null;
            int cornerCount = 0;
            for (int i = 0; i < count; i++)
            {
                int d = pairDir[i];
                uint bitA = 1u << d, bitB = 1u << Opposite(d);
                bool cellClear = hits[i * 2].collider == null;
                bool surfClear = hits[i * 2 + 1].collider == null;
                bool faceChange = CornerCandidate(w, pairA[i], pairB[i]);
                int a = pairA[i], b = pairB[i];
                // Stair edges need supported intermediate treads, not just an empty
                // cell-center ray above a gap or a line through the stair risers.
                if (w.normal[a].y >= CrawlGraph.FloorMinY && w.normal[b].y >= CrawlGraph.FloorMinY &&
                    Mathf.Abs(w.pos[a].y - w.pos[b].y) > 0.02f)
                {
                    bool clear = StairSegment(w.pos[a], w.pos[b], qp.layerMask);
                    if (clear) { w.mask[a] |= bitA; w.mask[b] |= bitB; }
                    else { w.mask[a] &= ~bitA; w.mask[b] &= ~bitB; }
                    continue;
                }
                // A clear cell ray can slip through a hollow shell. A real face change still
                // has to see the exterior corner, or the straight edge tunnels.
                if ((cellClear || surfClear) && !(cellClear && !surfClear && faceChange))
                {
                    w.mask[pairA[i]] |= bitA;
                    w.mask[pairB[i]] |= bitB;
                    continue;
                }

                if (!faceChange)
                {
                    w.mask[pairA[i]] &= ~bitA;
                    w.mask[pairB[i]] &= ~bitB;
                    continue;
                }

                if (cornerOf == null) cornerOf = new int[count];
                cornerOf[cornerCount++] = i;
            }

            if (cornerCount == 0) return;
            QueryParameters backQp = qp;
            backQp.hitBackfaces = true;
            int queued = 0;
            for (int c = 0; c < cornerCount; c++)
            {
                int i = cornerOf[c];
                int a = pairA[i], b = pairB[i];
                int d = pairDir[i];
                uint bitA = 1u << d, bitB = 1u << Opposite(d);
                if (!TryExteriorHinge(w, a, b, skin, out Vector3 hinge))
                {
                    w.mask[a] &= ~bitA;
                    w.mask[b] &= ~bitB;
                    continue;
                }

                Vector3 na = w.normal[a], nb = w.normal[b];
                Vector3 pa = w.pos[a] + na * skin;
                Vector3 pb = w.pos[b] + nb * skin;
                QueueSegment(cmds, queued * 2, pa, hinge, na, backQp);
                QueueSegment(cmds, queued * 2 + 1, pb, hinge, nb, backQp);
                cornerOf[queued++] = i;
            }
            if (queued == 0) return;
            RaycastCommand.ScheduleBatch(cmds.GetSubArray(0, queued * 2), hits.GetSubArray(0, queued * 2), 64, 1, default(JobHandle)).Complete();
            for (int c = 0; c < queued; c++)
            {
                int i = cornerOf[c];
                int d = pairDir[i];
                uint bitA = 1u << d, bitB = 1u << Opposite(d);
                int a = pairA[i], b = pairB[i];
                bool clear = hits[c * 2].collider == null && hits[c * 2 + 1].collider == null;
                if (clear)
                {
                    w.mask[a] |= bitA;
                    w.mask[b] |= bitB;
                }
                else
                {
                    w.mask[a] &= ~bitA;
                    w.mask[b] &= ~bitB;
                }
            }
        }

        static bool CornerCandidate(Work w, int a, int b)
        {
            Vector3 na = w.normal[a], nb = w.normal[b];
            if (Vector3.Dot(na, nb) >= CornerNormalDot) return false;
            Vector3 outward = na + nb;
            return outward.sqrMagnitude >= 0.01f;
        }

        // Editor only: bounded local probes, never a runtime route scan. Trace from
        // the low end so every riser must be within the motor's step-up capability.
        internal static bool StairSegment(Vector3 from, Vector3 to, int mask)
        {
            if (from.y > to.y) { Vector3 swap = from; from = to; to = swap; }
            Vector3 flat = Vector3.ProjectOnPlane(to - from, Vector3.up);
            float length = flat.magnitude;
            if (length < 0.02f || length > 1.5f) return false;
            int samples = Mathf.CeilToInt(length / 0.05f);
            Vector3 previous = from;
            Vector3 previousBottom = default;
            for (int s = 0; s <= samples; s++)
            {
                Vector3 p = from + flat * (s / (float)samples);
                p.y = previous.y + StairRise + 0.01f;
                if (!Physics.Raycast(p, Vector3.down, out RaycastHit hit, StairRise * 2 + 0.02f,
                        mask, QueryTriggerInteraction.Ignore) || hit.normal.y < CrawlGraph.FloorMinY ||
                    Mathf.Abs(hit.point.y - previous.y) > StairRise + 0.002f) return false;
                // A capsule can straddle a tread, but must not always be lifted by
                // the full step height (that falsely hits rails above landings).
                if (!TryStairPose(hit.point, mask, out Vector3 bottom)) return false;
                if (s > 0)
                {
                    Vector3 highA = previousBottom, highB = bottom;
                    highA.y = highB.y = Mathf.Max(previousBottom.y, bottom.y);
                    if (!StairSweep(previousBottom, highA, mask) || !StairSweep(highA, highB, mask) ||
                        !StairSweep(highB, bottom, mask)) return false;
                }
                previousBottom = bottom;
                previous = hit.point;
            }
            return Mathf.Abs(previous.y - to.y) < 0.03f;
        }

        static bool TryTreadSample(RaycastHit tread, float cellSize, int mask, out Vector3 point, out Vector3 normal)
        {
            point = tread.point; normal = tread.normal;
            if (TryStairPose(point, mask, out _)) return true;
            // Keep the one-node-per-cell format, but move away from a railing when
            // the cell center lands too close to it. No global grid refinement.
            foreach (Vector3 offset in TreadOffsets)
            {
                Vector3 origin = tread.point + offset * (cellSize * 0.4f) + Vector3.up * (StairRise + 0.01f);
                if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, StairRise * 2 + 0.02f,
                        mask, QueryTriggerInteraction.Ignore) && hit.normal.y >= CrawlGraph.FloorMinY &&
                    Mathf.Abs(hit.point.y - tread.point.y) <= StairRise + 0.002f && TryStairPose(hit.point, mask, out _))
                {
                    point = hit.point; normal = hit.normal; return true;
                }
            }
            return false;
        }

        static bool TryStairPose(Vector3 floor, int mask, out Vector3 bottom)
        {
            bottom = default;
            for (int lift = 0; lift <= 6; lift++)
            {
                bottom = floor + Vector3.up * (StairRadius + 0.02f + lift * 0.05f);
                if (!Physics.CheckCapsule(bottom, bottom + Vector3.up * (StairHeight - StairRadius * 2),
                        StairRadius, mask, QueryTriggerInteraction.Ignore)) return true;
            }
            return false;
        }

        static bool StairSweep(Vector3 from, Vector3 to, int mask)
        {
            Vector3 delta = to - from;
            float distance = delta.magnitude;
            return distance < 0.0001f || !Physics.CapsuleCast(from,
                from + Vector3.up * (StairHeight - StairRadius * 2), StairRadius,
                delta / distance, distance, mask, QueryTriggerInteraction.Ignore);
        }

        static bool TryExteriorHinge(Work w, int a, int b, float skin, out Vector3 hinge)
        {
            hinge = default;
            Vector3 na = w.normal[a], nb = w.normal[b];
            Vector3 posA = w.pos[a], posB = w.pos[b];
            float align = Vector3.Dot(na, nb);
            float det = 1f - align * align;
            if (det < 1e-4f) return false;

            Vector3 mid = (posA + posB) * 0.5f;
            float rhsA = Vector3.Dot(na, posA - mid);
            float rhsB = Vector3.Dot(nb, posB - mid);
            float alongA = (rhsA - align * rhsB) / det;
            float alongB = (rhsB - align * rhsA) / det;
            Vector3 corner = mid + na * alongA + nb * alongB;
            float maxDist = w.grid.CellSize * 2f;
            float maxSq = maxDist * maxDist;
            if ((corner - posA).sqrMagnitude > maxSq || (corner - posB).sqrMagnitude > maxSq) return false;

            Vector3 outward = na + nb;
            if (outward.sqrMagnitude < 0.01f) return false;
            hinge = corner + outward.normalized * skin;
            return true;
        }

        static void QueueSegment(NativeArray<RaycastCommand> cmds, int index, Vector3 from, Vector3 to, Vector3 fallbackDir, QueryParameters qp)
        {
            Vector3 delta = to - from;
            float mag = delta.magnitude;
            if (mag < 1e-5f)
                cmds[index] = new RaycastCommand(from, fallbackDir, qp, 0.001f);
            else
                cmds[index] = new RaycastCommand(from, delta / mag, qp, mag);
        }

        // ------------------------------------------------------------------ prune

        /// <summary>Drops components not reachable from seeds (or smaller than minIslandNodes). Returns nodes removed.</summary>
        static int Prune(Work w, CrawlGraphBaker b, out int componentsKept)
        {
            int n = w.keys.Count;
            int[] comp = new int[n];
            for (int i = 0; i < n; i++) comp[i] = -1;
            int[] queue = new int[n];
            List<int> sizes = new List<int>();

            for (int s = 0; s < n; s++)
            {
                if (comp[s] >= 0) continue;
                int id = sizes.Count, head = 0, tail = 0;
                comp[s] = id; queue[tail++] = s;
                while (head < tail)
                {
                    int a = queue[head++];
                    uint m = w.mask[a];
                    for (int d = 0; d < 26; d++)
                    {
                        if ((m & (1u << d)) == 0) continue;
                        if (!w.TryNeighbor(w.keys[a], d, out int _, out int nb) || comp[nb] >= 0) continue;
                        comp[nb] = id; queue[tail++] = nb;
                    }
                }
                sizes.Add(tail);
            }

            bool[] keepComp = new bool[sizes.Count];
            bool anySeed = false;
            if (b.seeds != null)
            {
                for (int s = 0; s < b.seeds.Length; s++)
                {
                    if (b.seeds[s] == null) continue;
                    Vector3 p = b.seeds[s].position;
                    int best = -1;
                    float bestSq = 9f;
                    for (int i = 0; i < n; i++)
                    {
                        float sq = (w.pos[i] - p).sqrMagnitude;
                        if (sq < bestSq) { bestSq = sq; best = i; }
                    }
                    if (best >= 0) { keepComp[comp[best]] = true; anySeed = true; }
                    else Debug.LogWarning($"[CrawlGraph] Seed '{b.seeds[s].name}' has no node within 3 m.", b.seeds[s]);
                }
            }
            if (!anySeed)
                for (int c = 0; c < sizes.Count; c++) keepComp[c] = sizes[c] >= b.minIslandNodes;

            componentsKept = 0;
            for (int c = 0; c < keepComp.Length; c++) if (keepComp[c]) componentsKept++;

            bool[] keep = new bool[n];
            int removed = 0;
            for (int i = 0; i < n; i++)
            {
                keep[i] = keepComp[comp[i]];
                if (!keep[i]) removed++;
            }
            w.Filter(keep);
            w.RebuildIndex();
            return removed;
        }

        // ------------------------------------------------------------------ build

        static CrawlGraph Build(Work w)
        {
            CrawlGraph g = w.grid;
            int n = w.keys.Count;

            long[] sortKeys = new long[n];
            int[] order = new int[n];
            for (int i = 0; i < n; i++)
            {
                sortKeys[i] = ((long)g.ChunkOfKey(w.keys[i]) << 32) | (uint)w.keys[i];
                order[i] = i;
            }
            Array.Sort(sortKeys, order);
            int[] remap = new int[n];
            for (int i = 0; i < n; i++) remap[order[i]] = i;

            g.NodeCount = n;
            g.NodeKey = new int[n];
            g.NodePos = new float[n * 3];
            g.NodeNormal = new int[n];
            g.NodeSurface = new byte[n];
            g.NodeCluster = new int[n];
            g.EdgeStart = new int[n + 1];
            g.ChunkStart = new int[g.ChunkCount + 1];
            List<int> edges = new List<int>(n * 10);

            for (int i = 0; i < n; i++)
            {
                int old = order[i];
                int key = w.keys[old];
                Vector3 p = w.pos[old];
                g.NodeKey[i] = key;
                g.NodePos[i * 3] = p.x; g.NodePos[i * 3 + 1] = p.y; g.NodePos[i * 3 + 2] = p.z;
                g.NodeNormal[i] = CrawlGraph.PackNormal(w.normal[old]);
                g.NodeSurface[i] = (byte)CrawlGraph.Classify(w.normal[old]);
                g.ChunkStart[g.ChunkOfKey(key) + 1]++;

                g.EdgeStart[i] = edges.Count;
                uint m = w.mask[old];
                for (int d = 0; d < 26; d++)
                {
                    if ((m & (1u << d)) == 0) continue;
                    if (w.TryNeighbor(key, d, out int _, out int nb)) edges.Add(remap[nb]);
                }
            }
            g.EdgeStart[n] = edges.Count;
            g.EdgeTarget = edges.ToArray();
            for (int c = 0; c < g.ChunkCount; c++) g.ChunkStart[c + 1] += g.ChunkStart[c];

            BuildClusters(g);
            return g;
        }

        static void BuildClusters(CrawlGraph g)
        {
            int n = g.NodeCount;
            for (int i = 0; i < n; i++) g.NodeCluster[i] = -1;
            int[] queue = new int[n];
            List<float> centers = new List<float>();
            List<int> clusterNode = new List<int>(), clusterChunk = new List<int>();

            for (int chunk = 0; chunk < g.ChunkCount; chunk++)
            {
                int start = g.ChunkStart[chunk], end = g.ChunkStart[chunk + 1];
                for (int s = start; s < end; s++)
                {
                    if (g.NodeCluster[s] >= 0) continue;
                    int id = clusterNode.Count, head = 0, tail = 0;
                    g.NodeCluster[s] = id; queue[tail++] = s;
                    Vector3 sum = Vector3.zero;
                    while (head < tail)
                    {
                        int a = queue[head++];
                        sum += g.GetPosition(a);
                        for (int e = g.EdgeStart[a]; e < g.EdgeStart[a + 1]; e++)
                        {
                            int t = g.EdgeTarget[e];
                            if (t < start || t >= end || g.NodeCluster[t] >= 0) continue;
                            g.NodeCluster[t] = id; queue[tail++] = t;
                        }
                    }
                    Vector3 center = sum / tail;
                    int rep = queue[0];
                    float repSq = float.MaxValue;
                    for (int q = 0; q < tail; q++)
                    {
                        float sq = (g.GetPosition(queue[q]) - center).sqrMagnitude;
                        if (sq < repSq) { repSq = sq; rep = queue[q]; }
                    }
                    centers.Add(center.x); centers.Add(center.y); centers.Add(center.z);
                    clusterNode.Add(rep);
                    clusterChunk.Add(chunk);
                }
            }

            int cc = clusterNode.Count;
            g.ClusterCount = cc;
            g.ClusterCenter = centers.ToArray();
            g.ClusterNode = clusterNode.ToArray();
            g.ClusterChunk = clusterChunk.ToArray();

            // components over the whole graph
            int[] comp = new int[n];
            for (int i = 0; i < n; i++) comp[i] = -1;
            int compCount = 0;
            for (int s = 0; s < n; s++)
            {
                if (comp[s] >= 0) continue;
                int head = 0, tail = 0;
                comp[s] = compCount; queue[tail++] = s;
                while (head < tail)
                {
                    int a = queue[head++];
                    for (int e = g.EdgeStart[a]; e < g.EdgeStart[a + 1]; e++)
                    {
                        int t = g.EdgeTarget[e];
                        if (comp[t] >= 0) continue;
                        comp[t] = compCount; queue[tail++] = t;
                    }
                }
                compCount++;
            }
            g.ClusterComponent = new int[cc];
            for (int c = 0; c < cc; c++) g.ClusterComponent[c] = comp[g.ClusterNode[c]];

            // cluster adjacency from node edges that cross clusters
            List<long> pairs = new List<long>();
            for (int i = 0; i < n; i++)
            {
                int ci = g.NodeCluster[i];
                for (int e = g.EdgeStart[i]; e < g.EdgeStart[i + 1]; e++)
                {
                    int cj = g.NodeCluster[g.EdgeTarget[e]];
                    if (cj != ci) pairs.Add(((long)ci << 32) | (uint)cj);
                }
            }
            pairs.Sort();
            g.ClusterEdgeStart = new int[cc + 1];
            List<int> targets = new List<int>(pairs.Count / 2 + 1);
            long prev = -1;
            for (int p = 0; p < pairs.Count; p++)
            {
                if (pairs[p] == prev) continue;
                prev = pairs[p];
                g.ClusterEdgeStart[(int)(prev >> 32) + 1]++;
                targets.Add((int)(prev & 0xFFFFFFFF));
            }
            for (int c = 0; c < cc; c++) g.ClusterEdgeStart[c + 1] += g.ClusterEdgeStart[c];
            g.ClusterEdgeTarget = targets.ToArray();
        }

        // ------------------------------------------------------------------ output

        static string WriteAsset(CrawlGraphBaker baker, CrawlGraph g, out long fileBytes)
        {
            string path = baker.graphAsset != null ? AssetDatabase.GetAssetPath(baker.graphAsset) : DefaultPath(baker);
            string full = Path.GetFullPath(path);
            using (FileStream fs = File.Create(full)) g.Write(fs);
            fileBytes = new FileInfo(full).Length;
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            return path;
        }

        static string DefaultPath(CrawlGraphBaker baker)
        {
            string scenePath = baker.gameObject.scene.path;
            if (string.IsNullOrEmpty(scenePath)) return "Assets/CrawlGraph.bytes";
            string dir = Path.GetDirectoryName(scenePath).Replace('\\', '/');
            return $"{dir}/{baker.gameObject.scene.name}_CrawlGraph.bytes";
        }
    }
}
