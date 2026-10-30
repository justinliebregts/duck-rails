using System;
using System.Collections.Generic;
using System.Linq;
using Game.AI;
using UnityEngine;

/// <summary>Reindexes translated, already baked graphs. Never samples geometry or creates navigation edges.</summary>
public static class RailGraphAssembly
{
    public static CrawlGraph Combine(IReadOnlyList<CrawlGraph> sources, IReadOnlyList<Vector3> offsets)
    {
        if (sources.Count == 0 || sources.Count != offsets.Count || sources.Any(g => g == null)) throw new ArgumentException("Missing authored graph.");
        var first = sources[0];
        Vector3 min = sources.Select((g, i) => g.Origin + offsets[i]).Aggregate(Vector3.Min);
        Vector3 max = sources.Select((g, i) => g.Origin + offsets[i] + new Vector3(g.DimX, g.DimY, g.DimZ) * g.CellSize).Aggregate(Vector3.Max);
        var result = new CrawlGraph { Origin = min, CellSize = first.CellSize, ChunkCells = first.ChunkCells,
            DimX = Mathf.CeilToInt((max.x - min.x) / first.CellSize), DimY = Mathf.CeilToInt((max.y - min.y) / first.CellSize), DimZ = Mathf.CeilToInt((max.z - min.z) / first.CellSize) };
        result.ChunksX = (result.DimX + result.ChunkCells - 1) / result.ChunkCells;
        result.ChunksY = (result.DimY + result.ChunkCells - 1) / result.ChunkCells;
        result.ChunksZ = (result.DimZ + result.ChunkCells - 1) / result.ChunkCells;
        var records = new List<(int graph, int node, int key, int chunk)>();
        var clusterBase = new int[sources.Count]; var componentBase = new int[sources.Count]; int clusters = 0, components = 0;
        for (int s = 0; s < sources.Count; s++)
        {
            var g = sources[s];
            if (g.CellSize != result.CellSize || g.ChunkCells != result.ChunkCells) throw new ArgumentException("Block bake grids must match.");
            Vector3 cells = (g.Origin + offsets[s] - min) / result.CellSize;
            Vector3Int shift = Vector3Int.RoundToInt(cells);
            if ((cells - (Vector3)shift).sqrMagnitude > .001f || shift.x % g.ChunkCells != 0 || shift.y % g.ChunkCells != 0 || shift.z % g.ChunkCells != 0)
                throw new ArgumentException("Block placement must align to baked chunks.");
            clusterBase[s] = clusters; componentBase[s] = components; clusters += g.ClusterCount;
            components += g.ClusterComponent.Length == 0 ? 0 : g.ClusterComponent.Max() + 1;
            for (int n = 0; n < g.NodeCount; n++)
            {
                g.CoordsOf(g.NodeKey[n], out int x, out int y, out int z); x += shift.x; y += shift.y; z += shift.z;
                records.Add((s, n, result.KeyOf(x, y, z), result.ChunkOfCell(x, y, z)));
            }
        }
        records.Sort((a, b) => a.chunk != b.chunk ? a.chunk.CompareTo(b.chunk) : a.key.CompareTo(b.key));
        int count = records.Count; result.NodeCount = count; result.ClusterCount = clusters;
        result.NodeKey = new int[count]; result.NodePos = new float[count * 3]; result.NodeNormal = new int[count]; result.NodeSurface = new byte[count]; result.NodeCluster = new int[count];
        result.EdgeStart = new int[count + 1]; result.ChunkStart = new int[result.ChunkCount + 1];
        var map = sources.Select(g => new int[g.NodeCount]).ToArray();
        for (int n = 0; n < count; n++) map[records[n].graph][records[n].node] = n;
        var edges = new List<int>();
        for (int n = 0; n < count; n++)
        {
            var r = records[n]; var g = sources[r.graph]; Vector3 p = g.GetPosition(r.node) + offsets[r.graph];
            result.NodeKey[n] = r.key; result.NodePos[n * 3] = p.x; result.NodePos[n * 3 + 1] = p.y; result.NodePos[n * 3 + 2] = p.z;
            result.NodeNormal[n] = g.NodeNormal[r.node]; result.NodeSurface[n] = g.NodeSurface[r.node]; result.NodeCluster[n] = g.NodeCluster[r.node] + clusterBase[r.graph];
            result.ChunkStart[r.chunk + 1]++; result.EdgeStart[n] = edges.Count;
            for (int e = g.EdgeStart[r.node]; e < g.EdgeStart[r.node + 1]; e++) edges.Add(map[r.graph][g.EdgeTarget[e]]);
        }
        result.EdgeStart[count] = edges.Count; result.EdgeTarget = edges.ToArray();
        for (int c = 1; c < result.ChunkStart.Length; c++) result.ChunkStart[c] += result.ChunkStart[c - 1];
        result.ClusterCenter = new float[clusters * 3]; result.ClusterComponent = new int[clusters]; result.ClusterNode = new int[clusters]; result.ClusterChunk = new int[clusters]; result.ClusterEdgeStart = new int[clusters + 1];
        edges.Clear();
        for (int s = 0; s < sources.Count; s++) for (int c = 0; c < sources[s].ClusterCount; c++)
        {
            var g = sources[s]; int dest = clusterBase[s] + c; Vector3 p = g.GetClusterCenter(c) + offsets[s];
            result.ClusterCenter[dest * 3] = p.x; result.ClusterCenter[dest * 3 + 1] = p.y; result.ClusterCenter[dest * 3 + 2] = p.z;
            result.ClusterComponent[dest] = componentBase[s] + g.ClusterComponent[c];
            result.ClusterNode[dest] = g.ClusterNode[c] < 0 ? -1 : map[s][g.ClusterNode[c]];
            int representative = g.ClusterNode[c] >= 0 ? g.ClusterNode[c] : Array.FindIndex(g.NodeCluster, value => value == c);
            result.ClusterChunk[dest] = representative < 0 ? 0 : result.ChunkOfKey(result.NodeKey[map[s][representative]]);
            result.ClusterEdgeStart[dest] = edges.Count;
            for (int e = g.ClusterEdgeStart[c]; e < g.ClusterEdgeStart[c + 1]; e++) edges.Add(clusterBase[s] + g.ClusterEdgeTarget[e]);
        }
        result.ClusterEdgeStart[clusters] = edges.Count; result.ClusterEdgeTarget = edges.ToArray(); return result;
    }
}
