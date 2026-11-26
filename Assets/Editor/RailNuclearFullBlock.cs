#if UNITY_EDITOR
using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

public static class RailNuclearFullBlock
{
    public const string RootName = "Full plant site";
    const string Folder = RailNuclearPlant.Folder + "/FullBlock";
    const string Model = Folder + "/Plant site.fbx";
    [Serializable] class Site { public Swatch[] materials; public Fixture[] lights; public Label[] labels; }
    [Serializable] class Swatch { public string name, texture, color; public float emission; }
    [Serializable] class Fixture { public string name; public float x,y,z,power,dx,dy,dz; public bool spot; public float[] color; }
    [Serializable] class Label { public string text; public float x,y,z,size,yaw; }
    public static Vector3 AcrossTrack(Vector3 p) => new Vector3(-p.x, p.y, p.z);

    static void MirrorHierarchy(Transform t)
    {
        t.localPosition = AcrossTrack(t.localPosition);
        var r = t.localRotation;
        t.localRotation = new Quaternion(r.x,-r.y,-r.z,r.w);
        foreach (Transform child in t) MirrorHierarchy(child);
    }

    static Mesh MakeDrum()
    {
        const int sides = 8;
        var vertices = new System.Collections.Generic.List<Vector3>();
        var uvs = new System.Collections.Generic.List<Vector2>();
        var triangles = new System.Collections.Generic.List<int>();
        float[] heights = {0f,.065f,.955f,1.02f};
        float[] radii = {.39f,.42f,.42f,.39f};
        Vector3 Rim(int i,int ring) {
            float angle = (i+.5f)*2*Mathf.PI/sides;
            return new Vector3(Mathf.Cos(angle)*radii[ring],heights[ring],Mathf.Sin(angle)*radii[ring]);
        }
        for(int i=0;i<sides;i++) {
            int v=vertices.Count;
            for(int j=0;j<heights.Length;j++) {
                vertices.Add(Rim(i,j)); vertices.Add(Rim(i+1,j));
                uvs.Add(new Vector2(i/(float)sides,heights[j]/1.02f));
                uvs.Add(new Vector2((i+1)/(float)sides,heights[j]/1.02f));
                if(j==0)continue;
                int lo=v+(j-1)*2;
                triangles.AddRange(new[]{lo,lo+2,lo+3,lo,lo+3,lo+1});
            }
        }
        foreach(int ring in new[]{0,3}) {
            int v=vertices.Count;
            for(int i=0;i<sides;i++) {
                var point=Rim(i,ring); vertices.Add(point);
                uvs.Add(new Vector2(.04f+point.x*.035f,.25f+point.z*.08f));
            }
            for(int i=1;i<sides-1;i++)
                if(ring==0)triangles.AddRange(new[]{v,v+i,v+i+1});
                else triangles.AddRange(new[]{v,v+i+1,v+i});
        }
        var mesh=new Mesh{name="Eight sided waste drum"};
        mesh.SetVertices(vertices);mesh.SetUVs(0,uvs);mesh.SetTriangles(triangles,0);
        mesh.RecalculateNormals();mesh.RecalculateBounds();Unwrapping.GenerateSecondaryUVSet(mesh);
        string path=Folder+"/Eight sided waste drum.asset";
        var existing=AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if(existing) {EditorUtility.CopySerialized(mesh,existing);Object.DestroyImmediate(mesh);return existing;}
        AssetDatabase.CreateAsset(mesh,path);
        return mesh;
    }

    static void ReplaceDrums(Transform root)
    {
        var mesh=MakeDrum();
        var skin=AssetDatabase.LoadAssetAtPath<Material>(Folder+"/Waste drum paint.mat");
        foreach(var body in root.GetComponentsInChildren<MeshFilter>().Where(f=>f.name=="Ribbed barrel").ToArray()) {
            body.sharedMesh=mesh;
            body.GetComponent<MeshCollider>().sharedMesh=mesh;
            body.GetComponent<MeshRenderer>().sharedMaterial=skin;
            var label=body.transform.parent.Find("Radiation marking");
            if(label)Object.DestroyImmediate(label.gameObject);
        }
        foreach(var spill in root.GetComponentsInChildren<Transform>().Where(t=>t.name=="Small waste spill"))
            Object.DestroyImmediate(spill.gameObject);
    }

    // The original hall was on the east, and both towers were on the west.
    static Vector3 SitePoint(Vector3 p)
    {
        if(p.x>=0)return AcrossTrack(p);
        if(p.z<=71)return p+new Vector3(0,0,36);
        if(p.z>=89)return p+new Vector3(52,0,9);
        return p+new Vector3(-4,0,21);
    }


    static void PrepareModel(Site site)
    {
        var importer = (ModelImporter)AssetImporter.GetAtPath(Model);
        importer.importAnimation = false;
        importer.importLights = false;
        importer.importCameras = false;
        importer.generateSecondaryUV = true;
        importer.isReadable = true;
        foreach (var swatch in site.materials)
        {
            var path = Folder + "/" + swatch.name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!material) {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(material,path);
            }
            if (!ColorUtility.TryParseHtmlString("#"+swatch.color, out Color tint)) throw new InvalidDataException(swatch.name);
            material.SetColor("_BaseColor", tint);
            material.SetFloat("_Smoothness", .16f);
            if (!string.IsNullOrEmpty(swatch.texture))
                material.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(Folder + "/Textures/" + swatch.texture + ".png"));
            if(swatch.emission > 0) {
                material.EnableKeyword("_EMISSION");material.SetColor("_EmissionColor",tint*swatch.emission);
                material.globalIlluminationFlags=MaterialGlobalIlluminationFlags.BakedEmissive;
            }
            EditorUtility.SetDirty(material);
            importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material),swatch.name),material);
        }
        importer.SaveAndReimport();
    }

    static void ApplySiteMaterials(Transform root)
    {
        foreach (var renderer in root.GetComponentsInChildren<MeshRenderer>(true))
        {
            var originals = renderer.sharedMaterials;
            for (int i = 0; i < originals.Length; i++)
            {
                if (!originals[i]) continue;
                if(renderer.name=="Flat ground") {
                    var t=renderer.transform.localPosition;
                    string ground=(Mathf.Abs(t.x)<32 && t.z>=24 && t.z<=120)?"Concrete":"Aggregate";
                    originals[i]=AssetDatabase.LoadAssetAtPath<Material>(Folder+"/"+ground+".mat");
                    continue;
                }
                string swatch = originals[i].name switch
                {
                    "Plant concrete" => "Plant ivory",
                    "Plant steel" => "Galvanized steel",
                    "Plant teal" => "Oxidized enamel",
                    "Plant grass" => "Plant ground",
                    "Dark gravel" => "Aggregate",
                    "Weathered timber" => "Cedar",
                    "Basin water" => "Settling water",
                    "Dark glass" => "Cold glazing",
                    "Insulator ivory" => "Ceramic",
                    "Green displays" => "Lit glazing",
                    "Beacon red" => "Red marker",
                    "Small waste spill" => "Groundcover",
                    "Waste drum ochre" => "Work equipment",
                    _ => null
                };
                if (swatch != null) originals[i] = AssetDatabase.LoadAssetAtPath<Material>(Folder + "/" + swatch + ".mat");
            }
            renderer.sharedMaterials = originals;
        }
    }
    static void RevisePlayableStructures(Transform root)
    {
        Transform[] parts = root.GetComponentsInChildren<Transform>(true);
        foreach (var t in parts) {
            if (!t || t == root) continue;
            string parent=t.parent.name;
            if (t.parent==root && Mathf.Abs(Mathf.Abs(t.localPosition.x)-10)<.01f &&
                (t.name=="Yard lamp post" || t.name=="Baked plant light" || t.name=="Light fixture") &&
                new[]{16,40,119,145}.Any(z=>Mathf.Abs(t.localPosition.z-z)<.01f)) {
                var pos=t.localPosition;pos.x=Mathf.Sign(pos.x)*8.06f;t.localPosition=pos;
            }
            if(parent=="Transformer yard" && (t.name=="Fence rail" || t.name=="Fence post") &&
                Mathf.Abs(t.localPosition.x-14)<.01f) {
                var pos=t.localPosition;pos.x=16.12f;t.localPosition=pos;
            }
            bool oldFront = t.name=="End wall" &&
                ((parent=="Gatehouse" && Mathf.Abs(t.position.z-35)<.1f) ||
                 (parent=="Maintenance" && Mathf.Abs(t.position.z-37)<.1f) ||
                 (parent=="Pump hut" && Mathf.Abs(t.position.z-140)<.1f));
            bool oldYardFixture=t.name=="Baked plant light" || t.name=="Light fixture";
            var yardPosition=new Vector2(t.localPosition.x,t.localPosition.z);
            bool floatingYardLamp=oldYardFixture && (
                t.parent==root && (yardPosition==new Vector2(27,130) || yardPosition==new Vector2(-26,130) ||
                    yardPosition==new Vector2(-26,103) ||
                    yardPosition==new Vector2(-20,28) || yardPosition==new Vector2(20,42)) ||
                parent=="Dense waste barrel yard" && yardPosition==new Vector2(25,25));
            bool tower=parent=="Tower A damp" || parent=="Tower B drained";
            bool obsoleteTowerPart=tower && (t.name.StartsWith("Sign ") || oldYardFixture ||
                t.name=="Shallow basin water" || t.name=="Retained overhead fill" || t.name=="Fill slats" ||
                t.name=="Utility pipe" || t.name=="Interior support" || t.name=="Red warning beacon");
            bool oldProp = obsoleteTowerPart || (parent=="Pump hut" && oldYardFixture) || floatingYardLamp || t.name=="Tree trunk" || t.name=="Tree crown" ||
                (parent=="Turbine hall" && (oldYardFixture || t.name=="Sign CONTROL" || t.name=="Roof" || t.name=="Stair handrail" || t.name=="Sign TURBINE HALL")) ||
                (parent=="Dense waste barrel yard" && t.name=="Sign WASTE STORAGE") ||
                (parent=="Reactor containment exterior" && t.name!="Roof warning beacon") ||
                ((parent=="Gatehouse" || parent=="Maintenance" || parent=="Pump hut") && (t.name=="Desk" || t.name=="Shelving")) ||
                ((t.name=="Fence rail" || t.name=="Fence post") && Mathf.Abs(t.position.x)>43);
            if(oldFront || oldProp) Object.DestroyImmediate(t.gameObject);
        }
        root.Find("Reactor containment exterior").localPosition=new Vector3(30,0,72);
        root.Find("Reactor containment exterior/Roof warning beacon").localPosition=new Vector3(0,16.395f,0);
        var hall=root.Find("Turbine hall");
        foreach (Transform part in hall) {
            if (part.name=="Gallery edge rail") {
                part.localPosition=new Vector3(-32.20f,4.05f,73);
                part.localScale=new Vector3(.12f,35.64f,.12f);
            }
            if (part.name=="Gallery baluster") {
                var pos=part.localPosition;
                pos.x=-32.20f;pos.z=Mathf.Clamp(pos.z,55.2f,90.8f);
                part.localPosition=pos;
            }
        }
        foreach (float z in new[]{67f,81.25f}) {
            var row=hall.Cast<Transform>().Where(t=>t.name=="Turbine unit" && Mathf.Abs(t.localPosition.z-z)<1f).ToArray();
            var first=row[0];
            var second=row.Length==1?Object.Instantiate(first,hall):row[1];
            second.name="Turbine unit";
            first.localPosition=new Vector3(-19.55f,0,z);
            second.localPosition=new Vector3(-26.45f,0,z);
        }
        foreach(string building in new[]{"Pump hut","Gatehouse","Maintenance"}) {
            var sign=root.Find(building+"/Sign "+building.ToUpperInvariant());
            if(!sign)continue;
            var backing=sign.Find("Backing");
            var size=backing.localScale;size.x=3.45f;backing.localScale=size;
            var label=sign.GetComponentInChildren<TMP_Text>();label.rectTransform.sizeDelta=new Vector2(3.24f,.54f);
            label.fontSizeMax=4.4f;
            float x=building=="Pump hut"?-26.78f:building=="Gatehouse"?-23.78f:26.78f;
            float z=building=="Pump hut"?145:building=="Gatehouse"?40:42;
            sign.localPosition=new Vector3(x,3.14f,z);
            sign.localRotation=Quaternion.Euler(0,building=="Maintenance"?90:-90,0);
        }
        var yard=root.Find("Dense waste barrel yard");
        int[,,] heights = {
            {{1,3,2,0,0},{1,2,1,0,0},{1,1,0,0,0}},
            {{0,1,2,2,1},{2,3,1,1,2},{1,2,1,0,1}},
            {{2,0,1,1,0},{3,2,2,1,1},{1,2,1,2,1}}
        };
        foreach(var t in yard.Cast<Transform>().Where(x=>x.name.StartsWith("Waste drum tier ")).ToArray()) {
            var q=t.localPosition;
            if(q.x<26.9f)continue;
            int c=Mathf.RoundToInt((q.x-27)/1.02f),cluster=Mathf.FloorToInt((q.z-17.9f)/5);
            int row=Mathf.RoundToInt((q.z-18-cluster*5)/1.02f);
            int tier=Mathf.RoundToInt((q.y-.02f)/1.02f);
            if(tier>=heights[cluster,row,c])Object.DestroyImmediate(t.gameObject);
        }
        // Remove coincident faces between the yard apron and crossing. The path
        // gets its own continuous strip, with separate apron pieces either side.
        var apron=root.Find("Transformer yard/Transformer apron");
        if(apron)Object.DestroyImmediate(apron.gameObject);
        var path=root.Cast<Transform>().First(t=>t.name=="East access path" && Mathf.Abs(t.position.z-124)<.2f);
        // Offset from longitudinal walkway junctions and the underlying ground.
        path.localPosition=new Vector3(path.localPosition.x,-.105f,path.localPosition.z);
    }

    // The apron, paths and original ground tiles used overlapping boxes. Their
    // cube UVs stretched one square island across the entire 148 m walk.
    static void BuildPaving(Transform root, Transform site)
    {
        var names = new HashSet<string> {
            "Flat ground", "Rail ballast", "Long walking path", "East access path",
            "West access path", "Hall arrival path", "Hall rear exit path", "Loading apron",
            "Ground slab", "Floor"
        };
        var slabs = root.GetComponentsInChildren<MeshRenderer>()
            .Where(r => names.Contains(r.name) && r.bounds.max.y < .1f)
            .OrderByDescending(r => r.bounds.max.y).ToArray();
        // These elevated meshes supply their own pavement and edge faces.
        var cutouts = new[] {
            Rect.MinMaxRect(-37.75f,10.25f,-8.25f,29.75f),
            Rect.MinMaxRect(15,121,40,122.5f), Rect.MinMaxRect(15,125.5f,40,141)
        };
        var concrete = AssetDatabase.LoadAssetAtPath<Material>(Folder+"/Concrete.mat");
        var xs = new SortedSet<float>();
        var zs = new SortedSet<float>();
        foreach (var r in slabs) {
            // Keep aggregate on designated yards and ballast, with no arbitrary tile boundaries.
            if (r.name=="Flat ground") r.sharedMaterial=concrete;
            var b = r.bounds;
            xs.Add(b.min.x); xs.Add(b.max.x); zs.Add(b.min.z); zs.Add(b.max.z);
            r.enabled = false; // Retain the original collision and named authoring footprint.
        }
        foreach (var rect in cutouts) {
            xs.Add(rect.xMin); xs.Add(rect.xMax); zs.Add(rect.yMin); zs.Add(rect.yMax);
        }
        var xEdges = xs.ToArray();
        var zEdges = zs.ToArray();
        var materials = slabs.Select(r => r.sharedMaterial).Distinct().ToArray();
        var triangles = materials.Select(_ => new List<int>()).ToArray();
        var vertices = new List<Vector3>();
        var uvs = new List<Vector2>();
        var uv2 = new List<Vector2>();
        var indices = new Dictionary<Vector2,int>();
        int Corner(float x,float z) {
            var coord = new Vector2(x,z);
            if (indices.TryGetValue(coord,out int i)) return i;
            i = vertices.Count;
            indices.Add(coord,i);
            vertices.Add(new Vector3(x,0,z));
            uvs.Add(coord/3f);
            uv2.Add(new Vector2((x+48)/160f,z/160f));
            return i;
        }
        for (int zi=1; zi<zEdges.Length; zi++)
        for (int xi=1; xi<xEdges.Length; xi++) {
            float x0=xEdges[xi-1], x1=xEdges[xi], z0=zEdges[zi-1], z1=zEdges[zi];
            if (x1-x0 < .001f || z1-z0 < .001f) continue;
            var center = new Vector2((x0+x1)/2,(z0+z1)/2);
            if (cutouts.Any(r => r.Contains(center))) continue;
            var top = slabs.FirstOrDefault(r => {
                var b=r.bounds;
                return center.x>b.min.x && center.x<b.max.x && center.y>b.min.z && center.y<b.max.z;
            });
            if (!top) continue;
            int a=Corner(x0,z0), b=Corner(x0,z1), c=Corner(x1,z1), d=Corner(x1,z0);
            triangles[Array.IndexOf(materials,top.sharedMaterial)].AddRange(new[]{a,b,c,a,c,d});
        }
        // Keep the entire UV chart on one receiver so lightmap padding and mip
        // filtering never sample an unrelated object between two path strips.
        string meshPath=Folder+"/Continuous site paving.asset";
        var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
        if (!mesh) {
            mesh=new Mesh { name="Continuous site paving" };
            AssetDatabase.CreateAsset(mesh,meshPath);
        }
        var combinedVertices=new List<Vector3>();
        var combinedUvs=new List<Vector2>();
        var combinedUv2=new List<Vector2>();
        var draws=new List<int[]>();
        for (int slot=0; slot<materials.Length; slot++) {
            var remap=new Dictionary<int,int>();
            var draw=new List<int>();
            foreach (int index in triangles[slot]) {
                if (!remap.TryGetValue(index,out int vertex)) {
                    vertex=combinedVertices.Count;
                    remap.Add(index,vertex);
                    combinedVertices.Add(vertices[index]);
                    combinedUvs.Add(uvs[index]);
                    combinedUv2.Add(uv2[index]);
                }
                draw.Add(vertex);
            }
            draws.Add(draw.ToArray());
        }
        mesh.Clear();
        mesh.SetVertices(combinedVertices);mesh.SetUVs(0,combinedUvs);mesh.SetUVs(1,combinedUv2);
        mesh.subMeshCount=draws.Count;
        for (int i=0; i<draws.Count; i++) mesh.SetTriangles(draws[i],i);
        mesh.RecalculateNormals(); mesh.RecalculateBounds();
        mesh.UploadMeshData(false); EditorUtility.SetDirty(mesh);
        var surface=new GameObject(mesh.name);
        surface.transform.SetParent(site,false);surface.layer=6;
        surface.AddComponent<MeshFilter>().sharedMesh=mesh;
        var renderer=surface.AddComponent<MeshRenderer>();
        renderer.sharedMaterials=materials;renderer.scaleInLightmap=1.3f;
        GameObjectUtility.SetStaticEditorFlags(surface,StaticEditorFlags.ContributeGI);
    }

    static void DressTruckAndDrums(Transform yard)
    {
        foreach (var body in yard.GetComponentsInChildren<MeshFilter>().Where(f=>f.name=="Ribbed barrel")) {
            var drum=body.transform.parent;
            var p=drum.localPosition;
            int seed=Mathf.RoundToInt(p.x*100)*73856093 ^ Mathf.RoundToInt(p.z*100)*19349663 ^ Mathf.RoundToInt(p.y*100)*83492791;
            var random=new System.Random(seed);
            float heading=(float)random.NextDouble()*360;
            if (drum.name=="Spilled waste drum") {
                drum.localRotation=Quaternion.Euler(0,heading,90)*Quaternion.Euler(0,45*random.Next(8),0);
                drum.localPosition=new Vector3(p.x,.01f+.42f*Mathf.Cos(Mathf.PI/8),p.z);
            } else drum.localRotation=Quaternion.Euler(0,heading,0);
        }

        var truck=yard.Find("Truck with spilled load");
        // Extend the underside to the chassis while keeping the load and sideboards seated.
        var bed=truck.Find("Flatbed");
        bed.localPosition=new Vector3(0,1.0625f,-.8f);
        bed.localScale=new Vector3(3,.325f,5.2f);
        var cab=truck.Find("Cab");
        cab.localScale=new Vector3(2.7f,1.8f,2);
        cab.localPosition=new Vector3(0,1.8f,2.4f);
        var rubber=AssetDatabase.LoadAssetAtPath<Material>(Folder+"/Rubber.mat");
        // Reuse the bevelled octagonal profile along the axle.
        var profile=yard.GetComponentsInChildren<MeshFilter>().First(f=>f.name=="Ribbed barrel").sharedMesh;
        foreach (Transform part in truck) {
            var p=part.localPosition;
            if (part.name=="Cargo sideboard") part.localPosition=new Vector3(p.x,1.525f,p.z);
            if (part.name!="Tyre") continue;
            const float width=.37f;
            float radiusScale=.54f/(.42f*Mathf.Cos(Mathf.PI/8));
            part.localPosition=new Vector3(Mathf.Sign(p.x)*1.5f-width/2,.55f,p.z);
            part.localRotation=Quaternion.Euler(0,0,-90);
            part.localScale=new Vector3(radiusScale,width/1.02f,radiusScale);
            part.GetComponent<MeshFilter>().sharedMesh=profile;
            part.GetComponent<MeshRenderer>().sharedMaterial=rubber;
            var box=part.GetComponent<BoxCollider>();
            if (box) Object.DestroyImmediate(box);
            var collider=part.GetComponent<MeshCollider>();
            if (!collider) collider=part.gameObject.AddComponent<MeshCollider>();
            collider.sharedMesh=profile;
        }
    }

    static void ChainLink(Transform site)
    {
        const string materialPath="Assets/DuckRails/Compound/SubwayTerminal/Materials/Subway mesh fence.mat";
        var material=AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        if(!material)throw new InvalidDataException(materialPath);
        foreach(var span in new[]{new Vector2(0,24),new Vector2(60,72),new Vector2(108,160)}) {
            float length=span.y-span.x, height=2.15f;
            string path=Folder+"/Chain link "+length+"m.asset";
            var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if(!mesh) {
                mesh=new Mesh { name="Compound chain link "+length+"m" };
                mesh.vertices=new[]{Vector3.zero,new Vector3(0,0,length),new Vector3(0,height,length),new Vector3(0,height,0)};
                mesh.triangles=new[]{0,1,2,0,2,3};
                mesh.uv=new[]{Vector2.zero,new Vector2(length,0),new Vector2(length,height),new Vector2(0,height)};
                mesh.uv2=new[]{Vector2.zero,Vector2.right,Vector2.one,Vector2.up};
                mesh.RecalculateNormals();mesh.RecalculateBounds();AssetDatabase.CreateAsset(mesh,path);
            }
            foreach(float x in new[]{-47f,47f}) {
                var panel=new GameObject("Compound chain-link panel");panel.layer=6;
                panel.transform.SetParent(site,false);
                panel.transform.localPosition=new Vector3(x-Mathf.Sign(x)*.075f,.14f,span.x);
                panel.AddComponent<MeshFilter>().sharedMesh=mesh;
                var r=panel.AddComponent<MeshRenderer>();r.sharedMaterial=material;r.scaleInLightmap=.18f;
                GameObjectUtility.SetStaticEditorFlags(panel,StaticEditorFlags.ContributeGI|StaticEditorFlags.BatchingStatic);
                var col=panel.AddComponent<BoxCollider>();col.center=new Vector3(Mathf.Sign(x)*.075f,1.01f,length/2);
                col.size=new Vector3(.12f,2.32f,length);
            }
        }
    }

    // The maintenance shed and gatehouse keep their original interior lamp at half strength.
    static void DimBuildingLamps(Transform root)
    {
        foreach(string building in new[]{"Gatehouse","Maintenance"})
            root.Find(building+"/Baked plant light").GetComponent<Light>().intensity=14;
    }

    // The refueling deck rose with the 1 m pool; carry its loot candidates with it.
    static void RaisePoolDeckMarkers(GameObject block)
    {
        foreach(var marker in block.GetComponentsInChildren<RailSpawnMarker>(true)) {
            var p=marker.transform.position;
            if(p.x>24.8f && p.x<35.2f && p.z>66 && p.z<78 && p.y>.85f && p.y<1.12f)
                marker.transform.position=p+Vector3.up*.22f;
        }
    }

    // Each box tops out at the visible surface. It reaches 0.1 m under the floor so
    // footsteps and hand hits on the basin bed still count as water.
    public static readonly (string name,Vector3 surface,Vector2 size)[] Waters = {
        ("Settling pond",new Vector3(-26,2.33f,130),new Vector2(26.38f,18.38f)),
        ("Refueling pool",new Vector3(30,1,72),new Vector2(7.48f,9.31f)),
        ("Flume step 1",new Vector3(27,1.23f,142.5f),new Vector2(22.4f,3.1f)),
        ("Flume step 2",new Vector3(27,2.13f,145.6f),new Vector2(22.4f,3.1f)),
        ("Flume step 3",new Vector3(27,3.03f,148.7f),new Vector2(22.4f,3.1f)),
        ("Flume step 4",new Vector3(27,3.93f,151.8f),new Vector2(22.4f,3.1f))
    };

    static void BuildWater(Transform site)
    {
        T Load<T>(string path) where T:Object => AssetDatabase.LoadAssetAtPath<T>(path) ?? throw new FileNotFoundException(path);
        var surface=Load<Game.Audio.SurfaceType>("Assets/Audio/Surfaces/Water.asset");
        var enter=Load<Game.Audio.SoundEvent>("Assets/Audio/Events/Water/Water_Enter.asset");
        var exit=Load<Game.Audio.SoundEvent>("Assets/Audio/Events/Water/Water_Exit.asset");
        var bubbles=Load<Game.Audio.SoundEvent>("Assets/Audio/Events/Water/Water_Bubbles.asset");
        foreach(var water in Waters) {
            var volume=new GameObject("Water volume "+water.name).AddComponent<Game.Audio.WaterVolume>();
            volume.transform.SetParent(site,false);
            volume.transform.position=water.surface+Vector3.down*.55f;
            volume.size=new Vector3(water.size.x,1.1f,water.size.y);
            volume.waterSurface=surface;volume.enterSound=enter;volume.exitSound=exit;volume.bubblesSound=bubbles;
        }
    }

    [MenuItem("Duck Rails/Authoring/Nuclear Plant/Install full site on existing prefab")]
    public static void Install()
    {
        if(EditorApplication.isPlaying || Lightmapping.isRunning) throw new InvalidOperationException("Install in Edit Mode with the lightmapper idle.");
        var site = JsonUtility.FromJson<Site>(File.ReadAllText("ArtSource/NuclearFullBlock/site.json"));
        var instance = PrefabUtility.LoadPrefabContents(RailNuclearPlant.PrefabPath);
        try {
            var root = RailBlock.FindEnvironment(instance.transform,RailNuclearPlant.RootName);
            if(!root) throw new InvalidOperationException("Missing authored plant.");
            var previous=root.Find(RootName);
            PrepareModel(site);
            if(previous) Object.DestroyImmediate(previous.gameObject);
            else {
                ApplySiteMaterials(root);
                ReplaceDrums(root);
                foreach(Transform child in root)
                {
                    switch(child.name)
                    {
                        case "Tower A damp": case "Tower B drained":
                            child.localPosition=SitePoint(child.localPosition);break;
                        case "Reactor containment exterior":
                            child.localPosition=new Vector3(26,0,72);break;
                        case "Cooling basin apron": case "Cooling basin water":
                            child.gameObject.SetActive(false);break;
                        case "West access path": case "East access path":
                            if(Mathf.Approximately(child.localPosition.z,94))
                                child.localPosition+=Vector3.forward*9;
                            break;
                        case "Turbine hall": case "Hall arrival path": case "Hall rear exit path":
                            MirrorHierarchy(child);break;
                        case "Baked plant light": case "Light fixture":
                            if(child.localPosition.z >= 55 && child.localPosition.z <= 113)
                                child.localPosition=child.localPosition.x<0 ? new Vector3(-26,child.localPosition.y,103) : new Vector3(26,child.localPosition.y,84);
                            break;
                        case "Old parking": case "Abandoned car body": case "Abandoned car cabin": case "Tree crown": case "Tree trunk":
                            child.gameObject.SetActive(false);break;
                    }
                }
                foreach(var marker in instance.GetComponentsInChildren<RailSpawnMarker>(true))
                    marker.transform.position = SitePoint(marker.transform.position);
            }
            RevisePlayableStructures(root);
            var siteRoot = new GameObject(RootName).transform;
            siteRoot.SetParent(root,false);
            DressTruckAndDrums(root.Find("Dense waste barrel yard"));
            BuildPaving(root,siteRoot);
            ChainLink(siteRoot);
            var meshRoot = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Model),siteRoot);
            meshRoot.transform.localRotation=Quaternion.Euler(0,180,0);
            // Keep model mesh references live and make the physical dressing hand-editable.
            PrefabUtility.UnpackPrefabInstance(meshRoot,PrefabUnpackMode.OutermostRoot,InteractionMode.AutomatedAction);
            foreach(var r in meshRoot.GetComponentsInChildren<MeshRenderer>())
            {
                var g=r.gameObject;
                var mesh=g.GetComponent<MeshFilter>().sharedMesh;
                if(!mesh || !r.sharedMaterial || mesh.uv2.Length!=mesh.vertexCount) throw new InvalidDataException(g.name);
                GameObjectUtility.SetStaticEditorFlags(g,StaticEditorFlags.ContributeGI | StaticEditorFlags.BatchingStatic);
                g.layer=6;
                r.scaleInLightmap=.45f; r.receiveGI=ReceiveGI.Lightmaps;
                if(g.name.EndsWith(" solid",StringComparison.Ordinal)) g.AddComponent<MeshCollider>().sharedMesh=mesh;
                // Opaque water must not black out the basin floor in the bake.
                if(r.sharedMaterial.name is "Settling water" or "Refueling water")
                    r.shadowCastingMode=ShadowCastingMode.Off;
            }
            DimBuildingLamps(root);
            BuildWater(siteRoot);
            RaisePoolDeckMarkers(instance);
            var font=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/CutePixel SDF.asset");
            foreach(var data in site.labels) {
                var label=new GameObject(data.text).AddComponent<TextMeshPro>();
                label.transform.SetParent(siteRoot,false);label.transform.localRotation=Quaternion.Euler(0,data.yaw,0);
                label.transform.localPosition=new Vector3(data.x,data.y,data.z)+label.transform.localRotation*Vector3.back*.02f;
                label.font=font;label.fontSharedMaterial=font.material;label.text=data.text;
                label.fontSize=data.size*6;label.alignment=TextAlignmentOptions.Center;
                label.rectTransform.sizeDelta=new Vector2(data.size*16,data.size*2);
                label.color=new Color(1f,.9f,.73f);label.ForceMeshUpdate();
            }
            foreach(var data in site.lights) {
                var l=new GameObject(data.name).AddComponent<Light>();l.transform.SetParent(siteRoot,false);
                l.transform.localPosition=new Vector3(data.x,data.y,data.z);l.type=LightType.Point;l.lightmapBakeType=LightmapBakeType.Baked;
                l.range=data.power>600?17:11;l.intensity=data.power*.043f;l.shadows=LightShadows.Soft;
                l.color=new Color(data.color[0],data.color[1],data.color[2]);
                if(data.spot) {
                    l.type=LightType.Spot;l.spotAngle=60;l.innerSpotAngle=30;l.range=40;l.intensity=2200;
                    if (data.name=="Hall wall flood") { l.spotAngle=87; l.innerSpotAngle=49; l.range=20; l.intensity=185; }
                    if (data.name=="Hall facade spotlight") { l.spotAngle=68; l.innerSpotAngle=34; l.range=14; l.intensity=140; }
                    if (data.name=="Pond corner flood") { l.spotAngle=82; l.innerSpotAngle=43; l.range=20; l.intensity=130; }
                    if (data.name=="Transformer floodlight") { l.spotAngle=78; l.innerSpotAngle=48; l.range=20; l.intensity=340; }
                    if (data.name=="Loading apron floodlight") { l.spotAngle=100; l.innerSpotAngle=70; l.range=21; l.intensity=220; }
                    l.transform.localRotation=Quaternion.LookRotation(new Vector3(data.dx,data.dy,data.dz));
                }
            }
            PrefabUtility.SaveAsPrefabAsset(instance,RailNuclearPlant.PrefabPath,out bool saved);
            if(!saved)throw new IOException("Failed to save nuclear prefab.");
        }finally{PrefabUtility.UnloadPrefabContents(instance);}
        AssetDatabase.SaveAssets();
    }
}
#endif
