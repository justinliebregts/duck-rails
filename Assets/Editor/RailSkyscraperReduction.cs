#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.AI;
using Game.AI.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>November 14 source-geometry revision. Deliberately precedes a new atlas review.</summary>
public static class RailSkyscraperReduction
{
    public const string Evidence = "docs/validation/skyscraper-reduction";
    const string Folder = "Assets/DuckRails/SkyscraperWorks/Reduction";
    const string State = "ArtSource/SkyscraperAtlas/production-state.json";
    const string Recovery = "output/skyscraper-reduction/recovery";
    static readonly int[] Floors = { 5, 3, 4, 5 };
    static readonly List<string> lines = new();
    static int meshId;
    static Mesh container;

    static void Require(bool ok, string message)
    { if (!ok) throw new InvalidOperationException(message); }
    static void Guard()
    {
        Require(Application.dataPath.Replace('\\', '/') == "X:/Work/duck-rails/Assets" &&
            !EditorApplication.isPlaying && !Lightmapping.isRunning, "Use idle Duck Rails Edit Mode.");
        var stage = PrefabStageUtility.GetCurrentPrefabStage();
        Require(stage == null || !stage.scene.isDirty, "Save current prefab edits first.");
        for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
            Require(!UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty, "Save current scene edits first.");
        if (stage != null) StageUtility.GoToMainStage();
    }
    static int Triangles(GameObject root) => root.GetComponentsInChildren<MeshRenderer>(true)
        .Where(r => r.enabled && r.GetComponent<MeshFilter>() && r.GetComponent<MeshFilter>().sharedMesh)
        .Sum(r => r.GetComponent<MeshFilter>().sharedMesh.triangles.Length / 3);
    static Transform Site(GameObject root) => root.transform.Find("Env/" + RailSkyscraper.RootName);
    static Mesh SaveMesh(Mesh mesh, string label)
    {
        mesh.name = (++meshId).ToString("D3") + " " + label;
        AssetDatabase.AddObjectToAsset(mesh, container);
        return mesh;
    }
    static void Delete(GameObject go) => Object.DestroyImmediate(go);
    static bool RemovedFloorPosition(Vector3 p) =>
        (p.x > 11 && p.x < 39 && p.z > 53 && p.z < 79 && p.y > 11.5f) ||
        (p.x > -39 && p.x < -11 && p.z > 85 && p.z < 111 && p.y > 15.5f);
    static float RoofShift(Vector3 p) => p.x > 11 && p.x < 39 && p.z > 53 && p.z < 79 ? -8 :
        p.x > -39 && p.x < -11 && p.z > 85 && p.z < 111 ? -4 : 0;

    [MenuItem("Duck Rails/Authoring/Skyscraper Works/Apply approved floor and geometry reduction")]
    public static void Install()
    {
        Guard();
        Require(!AssetDatabase.IsValidFolder(Folder), "Reduction already authored. Edit the saved result; do not rerun.");
        var saved = AssetDatabase.LoadAssetAtPath<GameObject>(RailSkyscraper.PrefabPath);
        Require(saved.transform.Find(RailSkyscraperAtlasProduction.Visual), "Expected the integrated atlas before restoration.");
        Directory.CreateDirectory(Evidence); Directory.CreateDirectory(Recovery);
        File.Copy(RailSkyscraper.PrefabPath, Recovery + "/before.prefab", false);
        File.Copy(RailSkyscraper.BakeScene, Recovery + "/before-bake.unity", false);
        File.Copy(AssetDatabase.GetAssetPath(saved.GetComponent<RailBlock>().graph), Recovery + "/before.bytes", false);
        File.Copy(State, Recovery + "/production-state.json", false);
        AssetDatabase.CreateFolder("Assets/DuckRails/SkyscraperWorks", "Reduction");
        container = new Mesh { name = "Skyscraper reduction mesh container" };
        AssetDatabase.CreateAsset(container, Folder + "/Meshes.asset"); meshId = 0; lines.Clear();
        string guid = AssetDatabase.AssetPathToGUID(RailSkyscraper.PrefabPath);
        var root = PrefabUtility.LoadPrefabContents(RailSkyscraper.PrefabPath);
        try
        {
            int atlasCount = Triangles(root);
            var site = Site(root);
            var skyline = site.GetComponentsInChildren<Transform>().Where(t => t.name == "Unfinished skyline pillar" || t.name == "Exposed skyline beam")
                .ToDictionary(t => t, t => t.localToWorldMatrix);
            var crossings = site.GetComponentsInChildren<Transform>().Where(t => t.name == "Continuous walking deck")
                .ToDictionary(t => t, t => t.localToWorldMatrix);
            var payload = JsonUtility.FromJson<RailSkyscraperAtlasReview.Payload>(File.ReadAllText("ArtSource/SkyscraperAtlas/unity-meshes.json"));
            foreach (string key in payload.sourceKeys)
            {
                var t = root.transform; foreach (string n in key.Split('/')) t = t.GetChild(int.Parse(n));
                t.GetComponent<MeshRenderer>().enabled = true;
            }
            Delete(root.transform.Find(RailSkyscraperAtlasProduction.Visual).gameObject);
            int sourceCount = Triangles(root);
            lines.Add($"Restored original repeating-material geometry: {atlasCount} -> {sourceCount} triangles.");

            foreach (int i in new[] { 1, 2 })
            {
                var tower = site.Find("Tower " + (char)('A' + i));
                int last = Floors[i] - 1;
                for (int f = last + 1; f < 5; f++)
                {
                    var floor = tower.Find($"Level {f} at {f * 4}m");
                    // Preserve corner columns as the now-open structural frame; remove the floor and its contents.
                    foreach (Transform t in floor.Cast<Transform>().ToArray())
                    {
                        bool corner = t.name == "Concrete pillar" && (t.localPosition.x < 2 || t.localPosition.x > 24) &&
                            (t.localPosition.z < 2 || t.localPosition.z > 22);
                        if (corner) { t.SetParent(tower, true); t.name = "Retained open-frame pillar"; }
                        else Delete(t.gameObject);
                    }
                    Delete(floor.gameObject);
                }
                var top = tower.Find($"Level {last} at {last * 4}m");
                foreach (var t in top.Cast<Transform>().Where(t => t.name.StartsWith("Stair ") || t.name.StartsWith("Tower worklight")).ToArray()) Delete(t.gameObject);
                // Close the half-metre slab gaps in the four supporting corner columns.
                foreach (var t in tower.GetComponentsInChildren<Transform>().Where(t =>
                    t.name == "Retained open-frame pillar" || (t.parent == top && t.name == "Concrete pillar" &&
                    (t.localPosition.x < 2 || t.localPosition.x > 24) && (t.localPosition.z < 2 || t.localPosition.z > 22))))
                { t.localPosition += Vector3.up * .25f; t.localScale += Vector3.up * .5f; }
                // The removed slabs no longer tie the corner frame together. Use the existing skyline beam style.
                foreach (float y in Enumerable.Range(last + 1, 4 - last).Select(f => f * 4f))
                    foreach (var pair in new[] { (new Vector3(1,y,1),new Vector3(25,y,1)), (new Vector3(1,y,23),new Vector3(25,y,23)),
                        (new Vector3(1,y,1),new Vector3(1,y,23)), (new Vector3(25,y,1),new Vector3(25,y,23)) })
                    {
                        var template = tower.Cast<Transform>().First(t => t.name == "Exposed skyline beam");
                        var beam = Object.Instantiate(template.gameObject, tower).transform;
                        beam.name = "Open-frame tie beam"; beam.localPosition = (pair.Item1 + pair.Item2) * .5f;
                        beam.localRotation = Quaternion.FromToRotation(Vector3.up, pair.Item2 - pair.Item1);
                        beam.localScale = new Vector3(.4f, Vector3.Distance(pair.Item1, pair.Item2), .4f);
                    }
                lines.Add($"Tower {(char)('A'+i)} retained {Floors[i]} floors; outgoing stairs/fixtures removed, original skyline retained.");
            }
            int afterFloors = Triangles(root);
            lines.Add($"After floor/stair removal and frame ties: {afterFloors} triangles.");
            var b = root.GetComponent<RailBlock>();
            foreach (RailSpawnKind kind in Enum.GetValues(typeof(RailSpawnKind)))
            {
                var all = RailSpawnPool.Candidates(b, kind);
                var remove = all.Where(t => RemovedFloorPosition(root.transform.InverseTransformPoint(t.position))).ToArray();
                RailSpawnPool.SetPoints(b.spawnCandidates, kind, all.Except(remove).ToArray());
                foreach (var variation in b.variations ?? Array.Empty<RailSpawnSet>())
                    RailSpawnPool.SetPoints(variation, kind, RailSpawnPool.Points(variation, kind).Except(remove).ToArray());
                foreach (var t in remove) Delete(t.gameObject);
                lines.Add($"{kind}: {all.Length} -> {all.Length-remove.Length} candidates; per-run settings unchanged.");
            }
            int details = 0;
            foreach (var floor in site.GetComponentsInChildren<Transform>().Where(t => t.name.StartsWith("Level ") && int.Parse(t.name.Split(' ')[1]) >= 2).ToArray())
            {
                // Keep the top rail, every guard post and all shaft/bridge/stair rails. Simplify exterior upper midrails only.
                foreach (var t in floor.Cast<Transform>().Where(t => t.name == "Edge midrail" &&
                    (Mathf.Abs(t.localPosition.x) < .01f || Mathf.Abs(t.localPosition.x-26) < .01f ||
                     Mathf.Abs(t.localPosition.z) < .01f || Mathf.Abs(t.localPosition.z-24) < .01f)).ToArray())
                { Delete(t.gameObject); details++; }
                var pipes = floor.Cast<Transform>().Where(t => t.name == "Loose pipe bundle").OrderBy(t => t.localPosition.x).ToArray();
                if (pipes.Length == 4) foreach (var t in pipes.Skip(1).Take(2)) { Delete(t.gameObject); details++; }
            }
            lines.Add($"Simplified {details} upper-floor detail cubes; {afterFloors-Triangles(root)} triangles removed.");
            TrimSlabFaces(site);
            LowerRoofFixtures(root);
            ResizeReverb(root);
            var lighting = root.GetComponent<RailBakedBlockLighting>();
            lighting.bindings = lighting.bindings.Where(x => x.renderer).ToArray();
            var baker = root.GetComponentInChildren<CrawlGraphBaker>(true);
            baker.seeds = RailSpawnPool.Candidates(b, RailSpawnKind.Enemy);
            Require(skyline.All(k => k.Key && k.Key.localToWorldMatrix == k.Value), "Original skyline changed.");
            Require(crossings.All(k => k.Key && k.Key.localToWorldMatrix == k.Value), "Bridge/crane deck changed.");
            lines.Add($"Preserved {skyline.Count} original skyline transforms and {crossings.Count} crossing decks exactly.");
            PrefabUtility.SaveAsPrefabAsset(root, RailSkyscraper.PrefabPath);
            lines.Add($"Review geometry: {Triangles(root)} triangles, {root.GetComponentsInChildren<MeshRenderer>().Count(r=>r.enabled)} mesh renderers.");
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        AssetDatabase.SaveAssets();
        Require(AssetDatabase.AssetPathToGUID(RailSkyscraper.PrefabPath) == guid, "Production GUID changed.");
        // Archive the retired fingerprint, rather than blessing different geometry as the old atlas.
        File.Move(State, "ArtSource/SkyscraperAtlas/production-state-before-reduction.json");
        File.WriteAllLines(Evidence + "/install.txt", lines);
        RailSkyscraperAtlasProduction.ValidateSaved();
        Debug.Log(string.Join("\n", lines));
    }

    static void TrimSlabFaces(Transform site)
    {
        int removed = 0, changed = 0;
        foreach (var floor in site.GetComponentsInChildren<Transform>().Where(t => t.name.StartsWith("Level ")))
        {
            var slabs = floor.GetComponentsInChildren<MeshFilter>().Where(f => f.name == "Open floor slab" || f.name == "Rear stairwell landing").ToArray();
            foreach (var f in slabs)
            {
                var mesh = f.sharedMesh; var verts = mesh.vertices; var norms = mesh.normals;
                var indices = mesh.triangles; var keep = new List<int>();
                for (int n = 0; n < indices.Length; n += 3)
                {
                    var ids = indices.Skip(n).Take(3).ToArray();
                    // Only entire side triangles fully buried in a neighbouring slab qualify.
                    bool hidden = Mathf.Abs(f.transform.TransformDirection(norms[ids[0]]).y) < .01f && slabs.Any(other => other != f && ids.All(id =>
                    {
                        var box = other.GetComponent<BoxCollider>();
                        var p = other.transform.InverseTransformPoint(f.transform.TransformPoint(verts[id]) + f.transform.TransformDirection(norms[id]) * .005f) - box.center;
                        var h = box.size * .5f + Vector3.one * .0001f;
                        return Mathf.Abs(p.x) <= h.x && Mathf.Abs(p.y) <= h.y && Mathf.Abs(p.z) <= h.z;
                    }));
                    if (hidden) removed++; else keep.AddRange(ids);
                }
                if (keep.Count == indices.Length) continue;
                var used = keep.Distinct().OrderBy(i => i).ToArray(); var map = used.Select((old, index) => (old,index)).ToDictionary(x => x.old,x => x.index);
                var reduced = new Mesh { vertices = used.Select(i=>verts[i]).ToArray(), normals = used.Select(i=>norms[i]).ToArray(),
                    uv = used.Select(i=>mesh.uv[i]).ToArray(), uv2 = used.Select(i=>mesh.uv2[i]).ToArray(), triangles = keep.Select(i=>map[i]).ToArray() };
                reduced.RecalculateBounds(); f.sharedMesh = SaveMesh(reduced, "Slab exposed faces"); changed++;
            }
        }
        lines.Add($"Removed {removed} buried slab-side triangles from {changed} slabs; exposed tops/undersides and box collision retained.");
    }

    static void LowerRoofFixtures(GameObject root)
    {
        var yard = root.transform.Find("Env/Approved yard polish"); int moved = 0;
        foreach (string name in new[] { "Rooftop tripod light stands", "Bench supports and brighter worklights" })
        {
            var group = yard.Find(name);
            foreach (var f in group.GetComponentsInChildren<MeshFilter>())
            {
                var mesh = Object.Instantiate(f.sharedMesh); var v = mesh.vertices; bool changed = false;
                for (int i=0;i<v.Length;i++)
                {
                    var world = f.transform.TransformPoint(v[i]); float dy = world.y > 15.5f ? RoofShift(world) : 0;
                    if (dy == 0) continue;
                    v[i] = f.transform.InverseTransformPoint(world + Vector3.up * dy); changed = true;
                }
                if (changed) { mesh.vertices = v; mesh.RecalculateBounds(); f.sharedMesh = SaveMesh(mesh, "Lowered roof fixtures"); }
                else Object.DestroyImmediate(mesh);
            }
            foreach (var t in group.GetComponentsInChildren<Transform>().Where(t => t.GetComponent<Collider>() || t.GetComponent<Light>()))
            { float dy=t.position.y>15.5f?RoofShift(t.position):0; if(dy!=0){t.position+=Vector3.up*dy;moved++;} }
        }
        lines.Add($"Lowered NW/SE rooftop stand geometry and {moved} collider/light objects by 4/8 m onto the new top floors.");
    }

    public static void PrepareBakeAndNavigation()
    {
        Guard();
        var scene = EditorSceneManager.OpenScene(RailSkyscraper.BakeScene);
        var b = Object.FindFirstObjectByType<RailBlock>();
        Require(b && b.id == RailSkyscraper.Id, "Wrong bake scene.");
        Require(!b.transform.Find(RailSkyscraperAtlasProduction.Visual), "Stale atlas in bake scene.");
        var baker = b.GetComponentInChildren<CrawlGraphBaker>(true);
        Physics.SyncTransforms();
        Require(CrawlGraphBake.Bake(baker, false), "Navigation bake failed.");
        EditorSceneManager.SaveScene(scene);
        RailCatalogLightingAuthoring.BakeOpenAndKeepOpen();
    }

    public static void ResizeReverb(GameObject root)
    {
        foreach (var z in root.GetComponentsInChildren<Game.Audio.BoxReverbZone>(true))
        {
            float height = z.name == "Tower B" ? 11 : z.name == "Tower C" ? 15 : 19;
            if (height == 19) continue;
            var so = new SerializedObject(z); var size = so.FindProperty("size");
            var v = size.vector3Value; v.y = height; size.vector3Value = v; so.ApplyModifiedPropertiesWithoutUndo();
            var p = z.transform.localPosition; p.y = height * .5f; z.transform.localPosition = p;
        }
    }

    [MenuItem("Duck Rails/Authoring/Skyscraper Works/Validate reduced source geometry")]
    public static void Validate() => ValidateTo(Evidence);

    // Later scoped art revisions can reuse route checks without overwriting historical evidence.
    public static void ValidateTo(string evidence)
    {
        Guard();
        var scene = EditorSceneManager.OpenScene(RailSkyscraper.BakeScene);
        var root = Object.FindFirstObjectByType<RailBlock>().gameObject; var b = root.GetComponent<RailBlock>();
        var site = Site(root); var checks = new List<string>();
        void Check(bool ok, string message) => checks.Add((ok ? "PASS " : "FAIL ") + message);
        Physics.SyncTransforms();
        Check(!root.transform.Find(RailSkyscraperAtlasProduction.Visual) && !File.Exists(State), "Retired atlas absent; original authoring workflow active");
        Check(site.GetComponentsInChildren<Transform>().Count(t=>t.name.StartsWith("Level ")) == 17, "17 playable floor groups");
        Check(root.GetComponentsInChildren<Transform>(true).All(t=>GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject)==0), "No missing scripts");
        Check(b.coalCount == 6 && b.goldCount == 2 && b.enemyCount == 5 && b.spawnCandidatesMigrated, "Per-run authoring settings/migration unchanged");
        var graph = CrawlGraph.Load(b.graph); var floorNodes = new List<int>();
        foreach (int i in Enumerable.Range(0,4))
        {
            var tower = site.Find("Tower " + (char)('A'+i));
            for (int f=0;f<5;f++)
            {
                var floor = tower.Find($"Level {f} at {f*4}m");
                Check((floor != null) == (f < Floors[i]), $"Tower {(char)('A'+i)} floor {f} presence");
                var p = tower.position + new Vector3(14,f*4,12);
                bool hit = Physics.Raycast(p+Vector3.up*.12f,Vector3.down,out var h,.3f,1<<6,QueryTriggerInteraction.Ignore);
                Check(hit == (f < Floors[i]), $"Tower {(char)('A'+i)} floor {f} actual support/absence");
                if (f >= Floors[i]) { Check(graph.FindNearestGroundNode(p,.8f)<0,"No obsolete navigation at removed floor " + i+"/"+f); continue; }
                var node=graph.FindNearestGroundNode(p+Vector3.up*.1f,1.2f); floorNodes.Add(node);
                Check(node>=0,"Ground graph at retained floor " + i+"/"+f);
                if(f==Floors[i]-1) Check(!floor.Cast<Transform>().Any(t=>t.name.StartsWith("Stair ")),"No outgoing stairs above top floor "+i);
                if(f>=Floors[i]-1)continue;
                foreach(var hole in RailSkyscraper.Holes.Take(2)) for(int n=0;n<10;n++)
                    foreach(var step in new[]{new Vector3(hole.x+1.2f,f*4+(n+1)*.2f,hole.y+1.18f+n*.36f),new Vector3(hole.x+3.8f,f*4+2+(n+1)*.2f,hole.y+4.42f-n*.36f)})
                    {
                        p=tower.TransformPoint(step);
                        Check(Physics.Raycast(p+Vector3.up*.12f,Vector3.down,out h,.35f,1<<6,QueryTriggerInteraction.Ignore)&&Mathf.Abs(h.point.y-p.y)<.08f,"Stair tread support "+i+"/"+f+"/"+n);
                        Check(!Physics.CheckCapsule(p+Vector3.up*.5f,p+Vector3.up*1.65f,.3f,1<<6,QueryTriggerInteraction.Ignore),"Stair body clearance "+i+"/"+f+"/"+n);
                    }
            }
        }
        if(floorNodes.All(n=>n>=0))
        {
            var seen=new HashSet<int>{floorNodes[0]};var queue=new Queue<int>();queue.Enqueue(floorNodes[0]);
            while(queue.Count>0){int n=queue.Dequeue();for(int e=graph.EdgeStart[n];e<graph.EdgeStart[n+1];e++){int next=graph.EdgeTarget[e];if(graph.IsGroundNode(next)&&seen.Add(next))queue.Enqueue(next);}}
            Check(floorNodes.All(seen.Contains),"All 17 floors connected using ground-only navigation");
        }
        for(int z=0;z<=160;z+=2) Check(!Physics.CheckBox(new Vector3(0,3.2f,z),new Vector3(2.9f,3.1f,.7f),Quaternion.identity,1<<6,QueryTriggerInteraction.Ignore),"Train clearance z="+z);
        foreach(var deck in site.GetComponentsInChildren<Transform>().Where(t=>t.name=="Continuous walking deck"))
            for(float z=0;z<=deck.localScale.z;z+=1) foreach(float x in new[]{-.6f,0,.6f})
            {
                var p=deck.parent.TransformPoint(new Vector3(x,0,z));
                Check(Physics.Raycast(p+Vector3.up*.2f,Vector3.down,out var h,.4f,1<<6,QueryTriggerInteraction.Ignore)&&Mathf.Abs(h.point.y-p.y)<.09f,"Bridge/crane support "+deck.parent.name+" "+x+"/"+z);
            }
        var surfaces=root.GetComponentsInChildren<Collider>().Where(c=>c.enabled&&!c.isTrigger&&c.gameObject.layer==6).ToArray();
        foreach(RailSpawnKind kind in Enum.GetValues(typeof(RailSpawnKind)))
        {
            foreach(var t in RailSpawnPool.Candidates(b,kind))
            {
                Check(!RemovedFloorPosition(t.position),"No candidate on removed floor: "+t.name);
                Check(PrefabUtility.GetCorrespondingObjectFromSource(t.gameObject)!=null,"Marker reference: "+t.name);
                if(kind==RailSpawnKind.Enemy){Check(graph.FindNearestGroundNode(t.position,.8f)>=0,"Enemy ground navigation: "+t.name);continue;}
                var bounds=RailLootPlacementWindow.ItemBounds(kind==RailSpawnKind.Gold);bounds.center+=t.position;
                foreach(float x in new[]{bounds.min.x,bounds.max.x}) foreach(float z in new[]{bounds.min.z,bounds.max.z})
                    Check(RailLootPlacementWindow.FloorHit(surfaces,new Ray(new Vector3(x,bounds.min.y+.1f,z),Vector3.down),out var hit)&&hit.distance<.3f,"Loot footprint support: "+t.name);
                Check(!Physics.CheckBox(bounds.center,bounds.extents*.96f,Quaternion.identity,1<<6,QueryTriggerInteraction.Ignore),"Loot clearance: "+t.name);
            }
        }
        for(int crew=1;crew<=8;crew++)for(int seed=0;seed<100;seed++)
        {
            var enemies=RailEnemySpawnBalance.Select(b,seed,crew);int quota=RailEnemySpawnBalance.PerQuadrant(crew);
            Check(enemies.Length==quota*4&&enemies.Distinct().Count()==enemies.Length&&Enumerable.Range(0,4).All(q=>enemies.Count(t=>RailEnemySpawnBalance.Quadrant(b,t)==q)==quota)&&
                enemies.All(a=>enemies.Where(c=>c!=a).All(c=>Vector2.Distance(new Vector2(a.position.x,a.position.z),new Vector2(c.position.x,c.position.z))>=15)),"Enemy selection/quadrants/spacing "+crew+"/"+seed);
            Check(RailSpawnPool.Select(b,RailSpawnKind.Coal,seed,RailResourceBalance.CoalCount(crew)).Length==RailResourceBalance.CoalCount(crew)&&
                RailSpawnPool.Select(b,RailSpawnKind.Gold,seed,RailResourceBalance.GoldCount(crew)).Length==RailResourceBalance.GoldCount(crew),"Crew-scaled loot capacity "+crew+"/"+seed);
        }
        foreach(var foot in root.GetComponentsInChildren<Transform>().Where(t=>t.name=="Tripod rubber foot"))
        {
            var own=foot.GetComponent<BoxCollider>();
            var ray=new Ray(new Vector3(foot.position.x,own.bounds.min.y+.01f,foot.position.z),Vector3.down);
            Check(surfaces.Any(c=>c!=own&&c.Raycast(ray,out var h,.08f)),"Tripod support excluding self "+foot.position);
        }
        var lighting=root.GetComponent<RailBakedBlockLighting>();
        Check(lighting.bindings.Length>0&&lighting.bindings.All(x=>x.renderer&&x.color)&&lighting.bindings.Select(x=>x.renderer).Distinct().Count()==lighting.bindings.Length,"Valid unique saved lightmap bindings");
        Check(root.GetComponentsInChildren<Light>().Length==52,"52 retained baked lights");
        foreach(var f in site.GetComponentsInChildren<MeshFilter>()) Check(f.sharedMesh&&f.sharedMesh.uv.Length==f.sharedMesh.vertexCount&&f.sharedMesh.uv2.Length==f.sharedMesh.vertexCount,"UV channels "+f.name);
        Check(Physics.CheckBox(site.Find("Tower A/Level 0 at 0m/Concrete pillar").position,new Vector3(.1f,.1f,.1f),Quaternion.identity,1<<6,QueryTriggerInteraction.Ignore),"Collision-query positive control");
        var fail=checks.Where(s=>s.StartsWith("FAIL")).ToArray();
        Directory.CreateDirectory(evidence);
        File.WriteAllLines(evidence+"/checks.txt",checks);
        File.WriteAllText(evidence+"/summary.txt",$"{checks.Count-fail.Length}/{checks.Count} authoring checks pass. {Triangles(root)} triangles; {graph.NodeCount} graph nodes; {lighting.bindings.Length} baked bindings.\n"+string.Join("\n",fail));
        Require(fail.Length==0,string.Join("\n",fail.Take(30)));
        Debug.Log($"Skyscraper reduction: {checks.Count} checks passed. No Play Mode/headset validation.");
    }
}
#endif
