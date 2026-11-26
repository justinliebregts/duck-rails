using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// Three open portals share one floor, roof and wall contour, including the T junction.
internal static class RailCanyonGallery
{
    public const string Name = "Connecting gallery sandstone shell";
    const int West = -20, East = 10;
    public static bool ReplacesCell(int x, int z) => x >= West && x < East && z >= RailCanyon.TunnelEnd;

    public static Mesh Build(bool exterior)
    {
        int south = RailCanyon.TunnelEnd;
        var mouth = Enumerable.Range(-19, 20).Where(x => RailCanyon.Cave(x + .5f, south + .5f)).ToArray();
        int left = mouth.First(), right = mouth.Last() + 1;
        var panels = new List<Vector2[]>();
        var edges = new HashSet<(Vector2 a, Vector2 b)>();
        void Panel(Vector2 a, Vector2 b, Vector2 c, Vector2 d)
        {
            var points = new[] { a, b, c, d };
            panels.Add(points);
            for (int i = 0; i < 4; i++)
            {
                var edge = (points[i], points[(i + 1) % 4]);
                if (!edges.Remove((edge.Item2, edge.Item1))) edges.Add(edge);
            }
        }

        Vector2[] previous = null;
        for (int x = West; x <= East; x++)
        {
            float z = RailCanyon.GalleryZ(x), front = z - 2.05f, back = z + 2.05f;
            // Round the inside corners as the south branch opens into the gallery.
            float distance = Mathf.Max(left - x, x - right, 0);
            front -= 1.55f * Mathf.Pow(Mathf.Clamp01(1 - distance / 2.5f), 2);
            if (x == West || x == East)
            {
                float inside = x == West ? x + .5f : x - .5f;
                var opening = Enumerable.Range(south, 19).Where(row => RailCanyon.Cave(inside, row + .5f)).ToArray();
                front = opening.First();
                back = opening.Last() + 1;
            }
            var row = Enumerable.Range(0, 4).Select(i => new Vector2(x, Mathf.Lerp(front, back, i / 3f))).ToArray();
            if (previous != null)
            {
                for (int i = 0; i < 3; i++) Panel(previous[i], previous[i + 1], row[i + 1], row[i]);
                if (x > left && x <= right)
                    Panel(new Vector2(x - 1, south), previous[0], row[0], new Vector2(x, south));
            }
            previous = row;
        }

        bool End(Vector2 p) => p.x == West || p.x == East || p.y == south;
        var walls = edges.Where(e => !(e.a.x == e.b.x && (e.a.x == West || e.a.x == East))
            && !(e.a.y == south && e.b.y == south)).ToList();
        var normals = new Dictionary<Vector2, Vector2>();
        foreach (var (a, b) in walls)
        {
            var d = (b - a).normalized;
            var outward = new Vector2(-d.y, d.x);
            normals[a] = normals.GetValueOrDefault(a) + outward;
            normals[b] = normals.GetValueOrDefault(b) + outward;
        }
        var vertices = new List<Vector3>();
        var indices = new List<int>();
        var lookup = new Dictionary<Vector3, int>();
        int Index(Vector3 p)
        {
            if (lookup.TryGetValue(p, out int id)) return id;
            id = vertices.Count;
            lookup.Add(p, id);
            vertices.Add(p);
            return id;
        }
        for (int skin = 0; skin <= (exterior ? 1 : 0); skin++)
        {
            Vector3 Point(Vector2 p, int level)
            {
                var outward = normals.GetValueOrDefault(p).normalized;
                if (p.x == West || p.x == East) outward.x = 0;
                if (p.y == south) outward.y = 0;
                outward.Normalize();
                float inset = End(p) ? 0 : level == 0 ? .22f : level == 3 ? .42f : 0;
                var pos = p + outward * (.35f * skin - inset);
                float height = level == 0 ? -.30f * skin : level == 1 ? .4f : level == 2 ? 2.18f : 2.7f + .30f * skin;
                return new Vector3(pos.x, RailCanyon.CaveFloor(pos.x, pos.y) + height, pos.y);
            }
            void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
            {
                int i = Index(a), j = Index(b), k = Index(c), l = Index(d);
                indices.AddRange(skin == 0 ? new[]{i,j,k,i,k,l} : new[]{i,k,j,i,l,k});
            }
            foreach (var p in panels)
            {
                Quad(Point(p[0],0), Point(p[1],0), Point(p[2],0), Point(p[3],0));
                Quad(Point(p[3],3), Point(p[2],3), Point(p[1],3), Point(p[0],3));
            }
            foreach (var (a,b) in walls)
                for (int level = 0; level < 3; level++)
                    Quad(Point(a,level), Point(a,level+1), Point(b,level+1), Point(b,level));
        }
        return RailCanyon.FinishTunnelMesh(Name, vertices, indices);
    }
}
