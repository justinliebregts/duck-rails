using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// Coalesce coplanar, single-material terrain cells. Keep the original triangle diagonal
// on every unmerged cell and share all edge vertices with finer neighbours.
static class RailCampgroundTerrain
{
    public static Mesh Build()
    {
        const int width = 96, depth = 160;
        var heights = new float[width + 1, depth + 1];
        var banks = new bool[width, depth];
        for (int z = 0; z <= depth; z++)
            for (int x = 0; x <= width; x++)
                heights[x,z] = RailCampgroundGreybox.Height(x-48,z);
        for (int z = 0; z < depth; z++)
            for (int x = 0; x < width; x++)
                banks[x,z] = x < 48 && (heights[x,z]+heights[x+1,z]+heights[x,z+1]+heights[x+1,z+1])*.25f < -.03f;
        var tiles = new List<(int x,int z,int size)>();
        var corners = new HashSet<Vector2Int>();
        void Divide(int x,int z,int size)
        {
            bool planar = true;
            float baseY = heights[x,z], dx = (heights[x+size,z]-baseY)/size, dz = (heights[x,z+size]-baseY)/size;
            for (int j=0; j<=size && planar; j++)
                for (int i=0; i<=size && planar; i++)
                    planar = Mathf.Abs(heights[x+i,z+j]-(baseY+dx*i+dz*j))<.00001f &&
                        (i==size || j==size || banks[x+i,z+j]==banks[x,z]);
            if (size>1 && !planar)
            {
                int half=size/2;
                Divide(x,z,half); Divide(x+half,z,half); Divide(x,z+half,half); Divide(x+half,z+half,half);
                return;
            }
            tiles.Add((x,z,size));
            corners.Add(new(x,z)); corners.Add(new(x,z+size)); corners.Add(new(x+size,z+size)); corners.Add(new(x+size,z));
        }
        for (int z=0;z<depth;z+=32)
            for (int x=0;x<width;x+=32) Divide(x,z,32);
        var verts = new List<Vector3>();
        var lookup = new Dictionary<Vector2Int,int>();
        int Vertex(Vector2Int p)
        {
            if (lookup.TryGetValue(p,out int id)) return id;
            id=verts.Count;
            lookup.Add(p,id);
            verts.Add(new Vector3(p.x-48,heights[p.x,p.y],p.y));
            return id;
        }
        var indices = new[]{new List<int>(),new List<int>()};
        var rim = new List<int>();
        foreach (var (x,z,size) in tiles)
        {
            rim.Clear();
            void Edge(int a,int b,int dx,int dz)
            {
                for(int i=0;i<size;i++) {
                    var p=new Vector2Int(a+i*dx,b+i*dz);
                    if(corners.Contains(p)) rim.Add(Vertex(p));
                }
            }
            Edge(x,z,0,1); Edge(x,z+size,1,0); Edge(x+size,z+size,0,-1); Edge(x+size,z,-1,0);
            var tris=indices[banks[x,z]?1:0];
            if(rim.Count==4) tris.AddRange(new[]{rim[0],rim[1],rim[2],rim[0],rim[2],rim[3]});
            else {
                int center=Vertex(new(x+size/2,z+size/2));
                for(int i=0;i<rim.Count;i++) { tris.Add(center); tris.Add(rim[i]); tris.Add(rim[(i+1)%rim.Count]); }
            }
        }
        var mesh=new Mesh{name="Sculpted-ground"};
        mesh.SetVertices(verts);
        mesh.SetUVs(0,verts.Select(v=>new Vector2(v.x,v.z)/4).ToList());
        mesh.SetUVs(1,verts.Select(v=>new Vector2((v.x+48)/96,v.z/160)).ToList());
        mesh.subMeshCount=2;
        mesh.SetTriangles(indices[0],0); mesh.SetTriangles(indices[1],1);
        mesh.RecalculateNormals(); mesh.RecalculateBounds();
        return mesh;
    }
}
