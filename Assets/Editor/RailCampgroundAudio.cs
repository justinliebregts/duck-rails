using System;
using System.Collections.Generic;
using System.Linq;
using Game.Audio;
using UnityEditor;
using UnityEngine;

static class RailCampgroundAudio
{
    static SurfaceType Surface(string name) => AssetDatabase.LoadAssetAtPath<SurfaceType>("Assets/Audio/Surfaces/"+name+".asset")
        ?? throw new InvalidOperationException("Missing surface "+name);

    public static void Configure(GameObject root, Transform finish, Vector4[] ferns)
    {
        var ground=root.GetComponentsInChildren<MeshCollider>().Single(c=>c.name=="Sculpted ground");
        foreach(var c in root.GetComponentsInChildren<Collider>(true).Where(c=>!c.isTrigger && c.gameObject.layer==6 && c!=ground))
        {
            string name=c.name;
            string material;
            if(name.EndsWith("_solid",StringComparison.Ordinal)) material=name.Split('_')[^2];
            else if(name=="Foundation" || name=="Dark camper window") material="Stone";
            else if(name=="Roof" || name.StartsWith("Camper ") || name=="Trailer tow bar" || name=="Chair frame" ||
                name=="Rubbish bin") material="Metal";
            else material="Wood";
            var tag=c.GetComponent<AudioSurface>() ?? c.gameObject.AddComponent<AudioSurface>();
            tag.type=Surface(material);
            PrefabUtility.RecordPrefabInstancePropertyModifications(tag);
        }
        var map=ground.GetComponent<GroundAudioSurface>() ?? ground.gameObject.AddComponent<GroundAudioSurface>();
        BakeGroundMap(root,ground,map);
        var sound=AssetDatabase.LoadAssetAtPath<SoundEvent>("Assets/Audio/Events/Impact/Foliage_CornRustle.asset");
        if(!sound || ferns==null || ferns.Length==0) throw new InvalidOperationException("Missing fern placement or corn sound");
        foreach(var cell in ferns.GroupBy(p=>new Vector2Int(Mathf.FloorToInt(p.x/10),Mathf.FloorToInt(p.z/10))))
        {
            var volume=new GameObject("Fern rustle "+cell.Key).AddComponent<FoliageVolume>();
            volume.transform.SetParent(finish,false);
            volume.stalks=cell.ToArray();
            volume.radius=.86f;
            volume.surface=Surface("Foliage");
            volume.rustle=sound;
            var bounds=new Bounds((Vector3)volume.stalks[0],Vector3.zero);
            foreach(var p in volume.stalks) { bounds.Encapsulate((Vector3)p-Vector3.up*.11f); bounds.Encapsulate((Vector3)p+Vector3.up*p.w); }
            bounds.Expand(new Vector3(2*volume.radius,0,2*volume.radius));
            volume.bounds=bounds;
        }
    }

    static void BakeGroundMap(GameObject root,MeshCollider ground,GroundAudioSurface map)
    {
        const int resolution=16;
        var bounds=ground.sharedMesh.bounds;
        map.origin=new(bounds.min.x,bounds.min.z);
        map.samplesPerMetre=resolution;
        map.width=Mathf.RoundToInt(bounds.size.x*resolution);
        int rows=Mathf.RoundToInt(bounds.size.z*resolution);
        var pixels=new byte[map.width*rows];
        var meshes=root.GetComponentsInChildren<MeshFilter>();
        // Sample visible overlays in vertical order. Tracks stay visual-only for locomotion.
        void Paint(MeshFilter filter,int submesh,byte value)
        {
            var mesh=filter.sharedMesh;
            var matrix=ground.transform.worldToLocalMatrix*filter.transform.localToWorldMatrix;
            var points=mesh.vertices.Select(v=>matrix.MultiplyPoint3x4(v)).Select(p=>(new Vector2(p.x,p.z)-map.origin)*resolution).ToArray();
            var indices=mesh.GetTriangles(submesh);
            for(int i=0;i<indices.Length;i+=3)
            {
                var a=points[indices[i]];var b=points[indices[i+1]];var c=points[indices[i+2]];
                float Cross(Vector2 v,Vector2 w)=>v.x*w.y-v.y*w.x;
                var ab=b-a;var bc=c-b;var ca=a-c;
                float area=Cross(ab,c-a);
                if(Mathf.Abs(area)<1e-5f) continue;
                float sign=Mathf.Sign(area);
                var lo=Vector2.Min(a,Vector2.Min(b,c));var hi=Vector2.Max(a,Vector2.Max(b,c));
                int x0=Mathf.Max(0,Mathf.CeilToInt(lo.x-.5f)),x1=Mathf.Min(map.width-1,Mathf.FloorToInt(hi.x-.5f));
                int z0=Mathf.Max(0,Mathf.CeilToInt(lo.y-.5f)),z1=Mathf.Min(rows-1,Mathf.FloorToInt(hi.y-.5f));
                for(int z=z0;z<=z1;z++) for(int x=x0;x<=x1;x++)
                {
                    var p=new Vector2(x+.5f,z+.5f);
                    if(sign*Cross(ab,p-a)>=-.0001f && sign*Cross(bc,p-b)>=-.0001f && sign*Cross(ca,p-c)>=-.0001f)
                        pixels[z*map.width+x]=value;
                }
            }
        }
        Paint(ground.GetComponent<MeshFilter>(),1,1);
        foreach(var f in meshes.Where(f=>f.name.StartsWith("Gravel trails") || f.name.StartsWith("Track ballast"))) Paint(f,0,1);
        foreach(var f in meshes.Where(f=>f.name=="Sleeper")) Paint(f,0,2);
        foreach(var f in meshes.Where(f=>f.name=="Track rail")) Paint(f,0,3);
        map.palette=new[]{Surface("Grass"),Surface("Dirt"),Surface("Wood"),Surface("Metal")};
        map.type=map.palette[0];
        map.rowOffsets=new int[rows+1];
        var spans=new List<int>();
        for(int z=0;z<rows;z++)
        {
            map.rowOffsets[z]=spans.Count;
            byte last=0;
            for(int x=0;x<map.width;x++)
            {
                byte current=pixels[z*map.width+x];
                if(current==last) continue;
                spans.Add((x<<8)|current);
                last=current;
            }
        }
        map.rowOffsets[rows]=spans.Count;
        map.spans=spans.ToArray();
    }
}
