#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Game.AI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

public static class RailQuarryMineChecks
{
    public static void PrepareRuntimeProof()
    {
        if(EditorApplication.isPlaying||Lightmapping.isRunning)throw new Exception("Finish bake/play first");
        var prefabs=RailCatalogLightingAuthoring.Prefabs.Select(p=>AssetDatabase.LoadAssetAtPath<GameObject>(p).GetComponent<RailBlock>()).ToArray();
        var catalog=prefabs.Where(b=>b.id!="bridge-finale-v1").ToArray();var expected=new float[catalog.Length];
        for(int i=0;i<catalog.Length;i++){EditorSceneManager.OpenScene("Assets/DuckRails/Lighting/Blocks/"+catalog[i].id+"/"+catalog[i].id+"-bake.unity");LightProbes.GetInterpolatedProbe(new Vector3(-26,2,61),null,out var sh);expected[i]=sh[0,0];}
        var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        var guard=new GameObject("Offline sign-in guard");guard.SetActive(false);guard.AddComponent<LoginFlow>();
        var proof=new GameObject("Mine catalogue integration proof").AddComponent<RailCatalogLightingProof>();proof.catalog=catalog;proof.expectedRedL0=expected;proof.outputDirectory="output/quarry-mountain/runtime-proof";
        proof.stationPrefab=AssetDatabase.LoadAssetAtPath<GameObject>(RailStartingStationBuilder.PrefabPath).GetComponent<RailBlock>();proof.finalePrefab=prefabs.Single(b=>b.id=="bridge-finale-v1");
        var seen=new HashSet<string>();var seeds=new List<int>();while(seen.Count<catalog.Length){int best=Enumerable.Range(0,1000).OrderByDescending(s=>RailRoute.Choose("proof",catalog,s).blocks.Count(id=>!seen.Contains(id))).First();seeds.Add(best);foreach(var id in RailRoute.Choose("proof",catalog,best).blocks)seen.Add(id);}
        proof.seeds=seeds.ToArray();EditorSceneManager.SaveScene(scene,RailQuarryMine.Folder+"/RuntimeProof.unity");
    }
    const string Evidence="docs/validation/quarry-mountain";
    static readonly List<string> lines=new();
    static void Check(bool yes,string text){lines.Add((yes?"PASS ":"FAIL ")+text);}
    static void Walk(Vector3 p,string label)
    {
        Check(Physics.Raycast(p+Vector3.up*.2f,Vector3.down,out var h,.5f,1<<6)&&Mathf.Abs(h.point.y-p.y)<.08f,"Floor "+label);
        Check(!Physics.CheckCapsule(p+Vector3.up*.5f,p+Vector3.up*1.65f,.32f,1<<6),"Body clearance "+label);
    }
    static void MineWalk(float x,float z,string label){
        Walk(new Vector3(x,Mathf.Min(RailQuarryMine.MineFloor(x,z),RailQuarryMine.Height(x,z)),z),label);
    }
    [MenuItem("Duck Rails/Authoring/Quarry Mine/Check geometry and navigation")]
    public static void Run()
    {
        if(EditorApplication.isPlaying||Lightmapping.isRunning)throw new Exception("Use Edit Mode, finish bake");
        if(UnityEngine.SceneManagement.SceneManager.GetActiveScene().path!=RailQuarryMine.BakeScene)EditorSceneManager.OpenScene(RailQuarryMine.BakeScene);
        Physics.SyncTransforms();lines.Clear();Directory.CreateDirectory(Evidence);var b=Object.FindFirstObjectByType<RailBlock>();var root=RailBlock.FindEnvironment(b.transform,RailQuarryMine.RootName);
        try
        {
            Check(b.id==RailQuarryMine.Id&&b.length==160&&root,"Mine identity and authored root");
            Check(b.track.Length==81&&b.track.Select((p,i)=>p==new Vector3(0,0,i*2)).All(v=>v),"Straight original track samples");
            Check(b.entry.localPosition==Vector3.zero&&b.exit.localPosition==new Vector3(0,0,160)&&b.stop.localPosition==new Vector3(0,0,80),"Entry exit and stop retained");
            foreach(float z in Enumerable.Range(0,81).Select(i=>i*2f))Walk(new Vector3(0,0,z==160?159.9f:z),"Rail "+z);
            foreach(float x in new[]{47.72f})foreach(float z in new[]{1f,7f,40f,80f,120f,153f,159f})Walk(new Vector3(x,0,z),"Flat edge "+x+","+z);
            foreach(float z in new[]{1f,5f,155f,159f})foreach(float x in new[]{-40f,-20f,0f,20f,40f})Walk(new Vector3(x,0,z),"Flat seam "+x+","+z);
            foreach(float z in new[]{55f,75f,95f,111f})Check(RailQuarryMine.Height(-47,z)>10,"Elevated west boundary "+z);
            for(int r=0;r<RailQuarryMine.Routes.Length;r++)
            {
                var path=RailQuarryMine.Routes[r];
                for(int i=1;i<path.Length;i++)
                {
                    var a=path[i-1];var c=path[i];int steps=Mathf.CeilToInt(Vector2.Distance(a,c));
                    for(int j=0;j<=steps;j++){var q=Vector2.Lerp(a,c,j/(float)steps);MineWalk(q.x,q.y,"Tunnel "+r+" / "+i+" / "+j);}
                    var start=new Vector3(a.x,-5.3f,a.y);var end=new Vector3(c.x,-5.3f,c.y);
                    Check(!Physics.SphereCast(start,.3f,(end-start).normalized,out _,Vector3.Distance(start,end),1<<6),"Tunnel continuous sweep "+r+" / "+i);
                }
            }
            MineWalk(11,76,"Office doorway");MineWalk(11,79.5f,"Office interior");
            foreach(var haul in new[]{RailQuarryMine.Haul,RailQuarryMine.PitRoad}){
            for(int i=1;i<haul.Length;i++)
            {
                var d=haul[i]-haul[i-1];Check(Mathf.Abs(d.y)/new Vector2(d.x,d.z).magnitude<=.09f,(haul==RailQuarryMine.Haul?"Rim road grade ":"Lower road grade ")+i);
                int steps=Mathf.CeilToInt(d.magnitude);for(int j=1;j<steps;j++){var q=Vector3.Lerp(haul[i-1],haul[i],j/(float)steps);Walk(q,"Haul "+i+" / "+j);}
            }
            }
            Check(!root.Find("West rock ledges/Summit lookout deck"),"Stairs finish on natural rock");
            var climb=RailQuarryMine.Climb;
            for(int i=1;i<climb.Length;i++){
                var a=climb[i-1];var end=climb[i];
                for(int j=1;j<=20;j++){
                    var foot=Vector3.Lerp(a,end,j/20f);
                    Check(Physics.Raycast(foot+Vector3.up*.65f,Vector3.down,out var hit,1.25f,1<<6) && Mathf.Abs(hit.point.y-foot.y)<.35f,"Switchback floor "+i+"/"+j);
                }
            }
            Check(root.Find("Surface machinery and gravel/Crane")
                && root.Find("Surface machinery and gravel/Northwest dewatering station/Diesel dewatering pump") && !root.GetComponentsInChildren<Transform>().Any(t=>t.name=="Core rack"),"Crane and pump station installed");
            foreach(var c in root.Find("Surface machinery and gravel").GetComponentsInChildren<Collider>())Check(c.bounds.max.x<=-7||c.bounds.min.x>=7||c.bounds.min.y>=6.3f,"Machinery clear of railway "+c.name);
            foreach(var t in b.GetComponentsInChildren<Transform>().Where(t=>t.name=="Track rail"||t.name=="Sleeper"))Check(!t.GetComponent<Collider>(),"Visual-only rails "+t.position.z);
            Check(b.GetComponentsInChildren<Light>().Length==RailQuarryMine.LightCount&&b.GetComponentsInChildren<Light>().All(l=>l.lightmapBakeType==LightmapBakeType.Baked),"Authored baked fixtures");
            foreach(var l in b.GetComponentsInChildren<Light>()){
                if(l.name=="Aqua water bounce"){
                    Check(l.type==LightType.Point && l.color.g>l.color.r*2 && l.lightmapBakeType==LightmapBakeType.Baked,"Aqua bounce over east sump");
                    continue;
                }
                var fixtures=l.transform.parent.Cast<Transform>().Select(t=>t.GetComponent<Renderer>()).Where(r=>r).ToArray();
                var housing=fixtures.Where(r=>r.name==l.name+" housing").OrderBy(r=>(r.bounds.center-l.transform.position).sqrMagnitude).First();
                var box=housing.bounds;box.Expand(.055f);
                Check(fixtures.Any(r=>r!=housing && r.name!="Lamp diffuser" && box.Intersects(r.bounds)),"Housing touches fixture support "+l.name);
            }
            Check(root.Find("Summit shaft hoist") && root.Find("Mine seep puddles"),"Summit hoist and underground puddles installed");
            foreach(float x in new[]{-42.8f,-42f,-41.2f})foreach(float z in new[]{105.2f,106,106.8f}){
                var lip=new Vector3(x,15.5f,z);
                Check(Physics.SphereCast(lip,.32f,Vector3.down,out var hit,23,1<<6) && Mathf.Abs(hit.point.y-RailQuarryMine.MineFloor(x,z))<.11f,"Continuous shaft fall onto mine floor "+lip);
            }
            for(int i=0;i<=24;i++){
                var point=Vector2.Lerp(new(-42,106),new(-25,99),i/24f);
                MineWalk(point.x,point.y,"Shaft bottom to west tunnel "+i);
            }
            var fixturesInMine=root.Find("Tunnel timber supports").GetComponentsInChildren<Light>();
            Check(fixturesInMine.Length==10 && fixturesInMine.All(l=>l.name=="Gallery lamp" && Mathf.Abs(l.color.r-l.color.b)<.09f && l.intensity<=34 && l.transform.parent.Find("Opal diffuser")),"Ten dim neutral camping lanterns in the mine");
            Check(root.Find("Underground office/Timber office screen/Office noticeboard") && root.Find("Excavated terrain/Office rock wall returns") && !root.GetComponentsInChildren<Transform>().Any(t=>t.name=="Fixture anchor"),"Office timber screen and solid rock returns with clear cave ceiling");
            foreach(var lantern in fixturesInMine){
                var mantle=lantern.transform.position;
                Check(!Physics.CheckSphere(mantle,.14f,1<<6),"Lantern mantle clear of rock "+mantle);
                bool visible=Enumerable.Range(0,16).Any(i=>{
                    float angle=i*Mathf.PI/8;
                    var eye=mantle+new Vector3(2.5f*Mathf.Sin(angle),1,2.5f*Mathf.Cos(angle));
                    return RailQuarryMine.Tunnel(eye.x,eye.z) && !Physics.CheckSphere(eye,.2f,1<<6)
                        && !Physics.Linecast(eye,mantle,1<<6);
                });
                Check(visible,"Lantern visible from gallery "+mantle);
            }
            var shaftLights=root.Find("Summit shaft hoist").GetComponentsInChildren<Light>().Where(l=>l.name.StartsWith("Shaft marker")).ToArray();
            Check(shaftLights.Length==3 && shaftLights.All(l=>l.color.b>l.color.r && l.intensity<=12),"Shaft markers dim blue-white");
            var pitLights=root.Find("Quarry stair and pit lights").GetComponentsInChildren<Light>();
            Check(pitLights.Count(l=>l.type==LightType.Spot && l.color.b>l.color.r)==3 && pitLights.Count(l=>l.name=="Step lamp")==4,"Three pit floods and one stone-set light at each flight");
            var entrance=root.Find("Southwest portal frame");
            Check(entrance && entrance.Cast<Transform>().Count(t=>t.name=="Portal upright")==2,"Timber portal at southwest cave entrance");
            // The existing rock cluster occupies the south edge of the opening.
            // Sweep both sides of the remaining aisle from the desk to the pit.
            for(int j=0;j<=40;j++)foreach(float z in new[]{79.25f,80.0f}){
                float x=11+j*.21f;MineWalk(x,z,"Office opening toward pit "+x+" "+z);
            }
            var screen=root.Find("Underground office/Timber office screen");
            foreach(float side in new[]{-1f,1f}){
                var eye=screen.TransformPoint(new Vector3(0,1.4f,side*.8f));
                Check(Physics.Raycast(eye,Vector3.forward*-side,out var face,1.1f,1<<6)
                    && face.collider.transform.IsChildOf(screen),"Closed timber screen face "+side);
            }
            var rear=root.Find("Excavated terrain/Office rock wall returns");
            Check(rear && Physics.Raycast(new(10,-4.5f,87),Vector3.back,out var outsideWall,1.2f,1<<6)
                && outsideWall.collider.transform==rear,"Office rock wall has an outward facing back");
            foreach(var receiver in root.Find("Excavated terrain").GetComponentsInChildren<MeshRenderer>().Where(r=>r.transform!=rear))
                Check(receiver.stitchLightmapSeams && receiver.scaleInLightmap>=.85f,"Continuous terrain lightmap charts "+receiver.name);
            Check(RailQuarryMine.ValidatePlacements(b),"Spawn footprints, graph reach and full-crew pool coverage");
            foreach(var lamp in fixturesInMine){
                var meshes=lamp.transform.parent.GetComponentsInChildren<MeshFilter>();
                Check(meshes.Length==2 && meshes.Sum(f=>f.sharedMesh.triangles.Length/3)<=120 &&
                    meshes.All(f=>f.GetComponent<Renderer>().sharedMaterial.renderQueue<2501),
                    "Opaque low-poly mine lantern "+lamp.transform.position);
            }
            var yard=root.Find("Surface machinery and gravel");
            var mouths=root.Find("Daylight portal rock");
            Check(mouths && mouths.childCount==4 && mouths.Cast<Transform>().All(t=>t.childCount>=5),"Four dressed mine mouths");
            Check(root.Find("Mine wall rubble and spalls").childCount>180,"Underground rock scatter");
            var trees=yard.Find("West ridge mixed conifers");
            Check(trees.Cast<Transform>().Any(t=>t.name=="Tall ridge fir") && trees.Cast<Transform>().Any(t=>t.name=="Thick trunk fir"),"Mixed heights and mature trunks on west ridge");
            void CheckMouth(Vector2[] route,string label){
                // Sweep three lines across the actual daylight transition. Probe the roof from inside
                // and the floor from above, selecting the generated terrain collider only.
                bool crossed=false;
                for(int i=1;i<route.Length && !crossed;i++){
                    var a=route[i-1];var b=route[i];var along=(b-a).normalized;var side=new Vector2(-along.y,along.x);
                    for(float d=.25f;d<=(b-a).magnitude;d+=.25f){
                        var q=a+along*d;
                        if(RailQuarryMine.Height(q.x,q.y)>=RailQuarryMine.MineRoof(q.x,q.y))continue;
                        foreach(float lateral in new[]{-1f,0,1f})foreach(float offset in new[]{-2f,-1f,0f,1f,2f}){
                            var t=q+along*offset+side*lateral;
                            float floor=Mathf.Min(RailQuarryMine.Height(t.x,t.y),RailQuarryMine.MineFloor(t.x,t.y)),roof=RailQuarryMine.MineRoof(t.x,t.y);
                            var eye=new Vector3(t.x,floor+.55f,t.y);
                            bool Hit(Vector3 dir,float distance)=>Physics.RaycastAll(eye,dir,distance,1<<6).Any(h=>h.collider.transform.parent.name=="Excavated terrain");
                            Check(Hit(Vector3.down,1f),"Continuous entrance floor "+label+" "+t);
                            if(RailQuarryMine.Height(t.x,t.y)>roof+.65f)Check(Hit(Vector3.up,roof-eye.y+.36f),"Closed entrance roof "+label+" "+t);
                        }
                        crossed=true;break;
                    }
                }
                Check(crossed,"Daylight transition "+label);
            }
            CheckMouth(new[]{new Vector2(-25,61),new Vector2(-25,45)},"west");
            for(int i=0;i<3;i++)CheckMouth(RailQuarryMine.Routes[i],"pit "+i);
            var wheels=yard.GetComponentsInChildren<MeshRenderer>().Where(t=>t.name=="Iron wheel").ToArray();
            Check(wheels.Length==12 && wheels.All(t=>Mathf.Abs(Mathf.Abs(t.bounds.center.z-137)-1.5f)<.02f && Mathf.Abs(t.bounds.min.y-.12f)<.02f),"All ore wagon treads sit on the three-metre siding gauge");
            foreach(var post in yard.Find("Yellow haul road fence").Cast<Transform>().Where(t=>t.name=="Safety post")){
                var foot=post.position-Vector3.up*.48f;
                var hit=Physics.RaycastAll(foot+Vector3.up*.55f,Vector3.down,1.25f,1<<6).Where(h=>h.collider.transform.parent.name=="Excavated terrain").OrderBy(h=>h.distance).FirstOrDefault();
                Check(hit.collider && Mathf.Abs(hit.point.y-foot.y)<.2f,"Supported fence post "+foot);
            }
            foreach(var bar in yard.Find("Yellow haul road fence").Cast<Transform>().Where(t=>t.name=="Safety rail"))
                Check(Mathf.Abs(bar.forward.y)<.20f,"Road fence follows the lip "+bar.position);
            var hut=yard.Find("Northwest dewatering station/Timber pump house");
            Check(hut.Find("Auxiliary pump and manifold") && hut.Find("Open door"),"Open pump room with auxiliary machinery");
            var entry=hut.TransformPoint(new Vector3(1.64f,1.46f,-2.38f));
            Check(!Physics.CheckCapsule(entry-Vector3.up*.60f,entry+Vector3.up*.60f,.34f,1<<6),"Walk-through pump house doorway");
            var pumpMeshes=yard.Find("Northwest dewatering station").GetComponentsInChildren<MeshFilter>();
            int hoseTriangles=pumpMeshes.Where(m=>new[]{"Discharge hose","Hose joint","Hose rib","Suction flange"}.Contains(m.name)).Sum(m=>m.sharedMesh.triangles.Length/3);
            int boltTriangles=pumpMeshes.Where(m=>m.name=="Flange bolt").Sum(m=>m.sharedMesh.triangles.Length/3);
            Check(hoseTriangles<=3008 && boltTriangles<=320,"Pump hose and flange "+hoseTriangles+"/6016 triangles, bolts "+boltTriangles+"/640");
            foreach(var g in new[]{hut,yard.Find("Weighbridge cabin and scale")}){
                bool pump=g==hut;var axis=pump?Vector3.forward:Vector3.right;
                foreach(float sign in new[]{-1f,1f}){
                    var eye=g.position+Vector3.up*(pump?4.17f:3.29f)+axis*sign*4;
                    var hits=Physics.RaycastAll(eye,-axis*sign,2.25f,1<<6);
                    Check(hits.Any(h=>h.collider.name.EndsWith("gable ends")),"Closed gable at "+g.name+" "+sign);
                }
            }
            var window=hut.TransformPoint(new Vector3(-1.53f,2.38f,-2.50f));
            Check(!Physics.Raycast(window-Vector3.forward*.45f,Vector3.forward,.95f,1<<6),"Clear sightline through pump house window");
            var grove=yard.Find("Southeast old growth");
            var stoneBounds=yard.GetComponentsInChildren<MeshRenderer>().Where(r=>r.name=="Fractured stone" && r.bounds.center.x>16 && r.bounds.center.z<46).Select(r=>r.bounds).ToArray();
            foreach(var t in grove.GetComponentsInChildren<MeshRenderer>().Where(r=>r.name.StartsWith("Old growth")))
                Check(!stoneBounds.Any(stone=>stone.Intersects(t.bounds)),"Southeast tree clear of rock "+t.bounds.center);
            var fence=yard.Find("Timber boundary fences");
            Check(fence && fence.GetComponentsInChildren<Transform>().Where(t=>t.name=="Boundary post").All(t=>Mathf.Abs(Mathf.Abs(t.position.x)-47)<.001f),"Side fences on standard one-metre inset");
            var terrain=root.Find("Excavated terrain").GetComponentsInChildren<MeshFilter>();
            int faces=terrain.Sum(m=>m.sharedMesh.triangles.Length/3);
            Check(faces<40000,"Terrain triangle count "+faces+" compared with 44662 before revision");
            var floorMesh=yard.Find("Sump water").GetComponent<MeshFilter>();
            var ring=floorMesh.sharedMesh.vertices.Select(v=>floorMesh.transform.TransformPoint(v)).Where(v=>new Vector2(v.x-23,v.z-88).magnitude>2);
            foreach(var v in ring.Distinct())Check(RailQuarryMine.Height(v.x,v.z)>v.y+.04f,"Water boundary buried under bank "+v);
            var carts=yard.GetComponentsInChildren<Transform>().Where(t=>t.name=="Ore skip");
            foreach(var cart in carts)foreach(var chunk in cart.Cast<Transform>().Where(t=>t.name=="Ore chunk")){
                var vertices=chunk.GetComponent<MeshFilter>().sharedMesh.vertices.Select(v=>cart.InverseTransformPoint(chunk.TransformPoint(v)));
                Check(vertices.All(v=>Mathf.Abs(v.x)<1.26f && Mathf.Abs(v.z)<1.64f),"Contained ore load "+chunk.position);
            }
            var mainTies=b.GetComponentsInChildren<MeshRenderer>().Where(r=>r.name=="Sleeper").Select(r=>r.bounds).ToArray();
            foreach(var tie in yard.GetComponentsInChildren<MeshRenderer>().Where(r=>r.name=="Switch timber" || r.name=="Siding sleeper"))
                Check(!mainTies.Any(a=>a.Intersects(tie.bounds)),"Siding timber clear of main sleepers "+tie.bounds.center);
            var gate=yard.Find("Quarry arrival sign");
            Check(gate.Find("QUACKRRY solid CutePixel letters").GetComponent<MeshFilter>().sharedMesh.bounds.size.z>.28f,"Raised QUACKRRY lettering");
            Check(gate && Mathf.Abs(gate.position.x)<.01f && gate.position.z<8 && gate.GetComponentsInChildren<Light>().Length==2,"South entrance spans the railway with twin lanterns");
            Check(gate.GetComponentsInChildren<Renderer>().Where(r=>r.bounds.min.x<3 && r.bounds.max.x> -3).All(r=>r.bounds.min.y>6.3f),"Sign and badge clear the full 6.3 m overhead envelope");
            Check(root.Find("Tunnel boring machine/Cutter head") && yard.Find("Yellow haul road fence"),"Boring machine and fenced haul road installed");
            Check(yard.GetComponentsInChildren<Transform>().Count(t=>t.name=="Ore skip")==3,"Three ore skips on northeast siding");
            Check(yard.GetComponentsInChildren<Transform>().Count(t=>t.name=="Old growth bole")==5,"Five mature trees at southeast entrance");
            foreach(float x in new[]{31f,33f,36f,39f,42f})foreach(float z in new[]{68f,75f,82f})
                Check(Mathf.Abs(RailQuarryMine.Height(x,z)-1.5f)<.12f,"Solid excavator shelf "+x+","+z);
            // Measure the actual intersections rather than only checking connectivity via a shared endpoint.
            var cross=RailQuarryMine.Crosscut;
            foreach(var branch in RailQuarryMine.Routes.Take(3)){
                int intersections=0;
                for(int j=1;j<cross.Length;j++)for(int k=1;k<branch.Length;k++){
                    Vector2 u=cross[j]-cross[j-1],v=branch[k]-branch[k-1],off=branch[k-1]-cross[j-1];
                    float det=u.x*v.y-u.y*v.x;if(Mathf.Abs(det)<.001f)continue;
                    float t=(off.x*v.y-off.y*v.x)/det,w=(off.x*u.y-off.y*u.x)/det;
                    if(t<0 || t>1 || w<0 || w>1)continue;
                    intersections++;
                    var q=cross[j-1]+t*u;
                    foreach(var dir in new[]{u.normalized,-u.normalized,v.normalized,-v.normalized}){
                        var at=q+dir*1.6f;MineWalk(at.x,at.y,"Open crosscut arm "+q);
                    }
                }
                Check(intersections==1,"One distinct crosscut junction on original branch "+branch[1]);
            }
            var graph=CrawlGraph.Load(b.graph);Check(graph!=null&&graph.NodeCount>100,"Prebaked graph present");
            var nodes=new List<int>();foreach(var route in RailQuarryMine.Routes)foreach(var p in route){int n=graph.FindNearestGroundNode(new Vector3(p.x,-5.8f,p.y),2);Check(n>=0,"Graph node at tunnel waypoint "+p);if(n>=0)nodes.Add(n);}
            if(nodes.Count>0)
            {
                var seen=new HashSet<int>{nodes[0]};var queue=new Queue<int>();queue.Enqueue(nodes[0]);while(queue.Count>0){int n=queue.Dequeue();for(int e=graph.EdgeStart[n];e<graph.EdgeStart[n+1];e++){int next=graph.EdgeTarget[e];if(graph.GetNormal(next).y>.5f&&seen.Add(next))queue.Enqueue(next);}}
                foreach(var point in new[]{RailQuarryMine.Climb.Last()+Vector3.up*.25f,new Vector3(41,1.75f,75),new Vector3(-16,-5.75f,131),new Vector3(-42,-5.8f,106),new Vector3(0,.25f,80)}){
                    int node=graph.FindNearestGroundNode(point,2);
                    Check(node>=0 && seen.Contains(node),"Ground route from original mine to "+point);
                }
                Check(nodes.All(seen.Contains),"Ground graph connects all branches and the crosscut");
            }
            Check(b.enemySurfaceRailExclusion.size == Vector3.zero,"No permanent railway navigation exclusion");
            foreach(float z in new[]{63f,82f,101f}){MineWalk(0,z,"Under-track crossing "+z);Check(Physics.Raycast(new Vector3(0,-4,z),Vector3.up,out var hit,2,1<<6)&&Mathf.Abs(hit.point.y-RailQuarryMine.MineRoof(0,z))<.12f,"Tunnel roof below intact railway "+z);}
            var old=b.transform.position;b.transform.position=new Vector3(0,0,390);foreach(float x in new[]{-2f,2f}){var p=new Vector3(x,.5f,470);Check(b.ConstrainEnemyPosition(p)==p,"Translated surface crossing remains allowed "+x);var q=new Vector3(x,-5.5f,470);Check(b.ConstrainEnemyPosition(q)==q,"Translated underground remains allowed "+x);}b.transform.position=old;Physics.SyncTransforms();
            foreach(var f in root.GetComponentsInChildren<MeshFilter>())Check(f.sharedMesh.uv.Length==f.sharedMesh.vertexCount&&f.sharedMesh.uv2.Length==f.sharedMesh.vertexCount,"Texture/lightmap UVs "+f.name);
            if(lines.Any(l=>l.StartsWith("FAIL")))throw new Exception(string.Join("\n",lines.Where(l=>l.StartsWith("FAIL")).Take(30)));
            Debug.Log("MINE CHECKS: "+lines.Count+" passed");
        }
        finally{File.WriteAllLines(Evidence+"/geometry-checks.txt",lines);}
    }
    [MenuItem("Duck Rails/Authoring/Quarry Mine/Capture mine")]
    public static void Capture()
    {
        if(EditorApplication.isPlaying||Lightmapping.isRunning)throw new Exception("Finish play/bake first");
        EditorSceneManager.OpenScene(RailQuarryMine.BakeScene);Directory.CreateDirectory("output/quarry-mountain");
        var go=new GameObject("Temporary review camera");var cam=go.AddComponent<Camera>();cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=new Color(.1f,.13f,.16f);cam.farClipPlane=500;
        var fill=new GameObject("Temporary review fill").AddComponent<Light>();fill.type=LightType.Directional;fill.intensity=2.4f;fill.transform.rotation=Quaternion.Euler(38,-31,0);
        try
        {
            RenderSettings.fog=false;RenderSettings.ambientMode=UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight=new Color(.38f,.41f,.48f);cam.orthographic=true;cam.orthographicSize=56;
            Shot("surface",new Vector3(20,88,-52),new Vector3(0,2,80),53);cam.orthographic=false;Shot("east-pit-crane",new Vector3(8,22,29),new Vector3(30,-2,78),65);Shot("west-climb",new Vector3(3,8,40),new Vector3(-25,7,75),69);
            Shot("summit-hoist",new Vector3(-31.0f,22.6f,95.1f),new Vector3(-40.3f,16.65f,105.7f),55);
            Shot("shaft-drop",new Vector3(-42.6f,17.8f,107.2f),new Vector3(-42,-5.55f,105.9f),67);
            Shot("pump-house",new Vector3(-32.4f,3.8f,131.8f),new Vector3(-32.9f,3.44f,140.1f),59);
            Shot("southeast-grove",new Vector3(15,13,2),new Vector3(36,3,22),57);
            Shot("quarry-pool",new Vector3(20.1f,11.8f,78),new Vector3(23,-6.22f,88),65);
            Shot("crane-detail",new Vector3(46,8,61),new Vector3(34.6f,5,77),55);
            Shot("south-gate",new Vector3(1,6,-17),new Vector3(0,5,6),53);
            Shot("loading-yard",new Vector3(6,16,111),new Vector3(27,2,142),55);
            Shot("west-ledges",new Vector3(8,16,44),new Vector3(-27,8,81),58);
            Shot("pump-station",new Vector3(-6.5f,9,119.5f),new Vector3(-23.3f,2.6f,135.7f),53);
            Shot("ore-wagons",new Vector3(30,4.9f,124.5f),new Vector3(25.8f,1.4f,138),57);
            Shot("west-portal",new Vector3(-25,-3.7f,45),new Vector3(-25,-4.4f,58),70);
            Shot("pit-portals",new Vector3(30,3.7f,78),new Vector3(8,-3.6f,78),76);
            // Section views place the near plane below the roofs or inside the end wall.
            cam.orthographic=true;cam.orthographicSize=49;
            Shot("mine-plan",new Vector3(-1,-3.55f,88),new Vector3(-1,-6,88.001f),55);
            cam.orthographic=false;cam.nearClipPlane=6.05f;
            Shot("cutter-head",new Vector3(-14.6f,-3.35f,143.8f),new Vector3(-13,-3.8f,135.5f),43);
            cam.nearClipPlane=.3f;fill.enabled=false;Shot("borer",new Vector3(-17,-3.4f,122),new Vector3(-12.9f,-3.75f,133),72);
            Shot("east-pit-night",new Vector3(9.5f,17.5f,46),new Vector3(26,-2.2f,82),56);
            Shot("west-stairs-night",new Vector3(7,16,47),new Vector3(-27,8,81),59);
            Shot("shaft-landing",new Vector3(-36.5f,-4.5f,104.3f),new Vector3(-43,-5.1f,106.8f),77);
            Shot("blue-shaft",new Vector3(-42.6f,17.8f,107.2f),new Vector3(-42,-5.55f,105.9f),67);
            Shot("mine-puddles",new Vector3(-15.2f,-4.17f,90.6f),new Vector3(-11.4f,-5.88f,91.9f),72);
            Shot("lantern-gallery",new Vector3(-23,-4.64f,92.3f),new Vector3(-23.45f,-5.35f,87.7f),66);
            Shot("tunnel-rockwork",new Vector3(-11.91f,-4.68f,109.08f),new Vector3(-14.18f,-5.28f,105.5f),76);
            Shot("office-screen",new Vector3(14.7f,-4.32f,78.23f),new Vector3(10.8f,-4.48f,83.7f),65);
            Shot("field-lantern",new Vector3(9.95f,-5.32f,78.84f),new Vector3(9.6f,-5.67f,80.10f),44);
            Shot("office",new Vector3(11,-4.5f,73),new Vector3(11,-4.5f,81),80);Shot("under-track",new Vector3(-9,-4.5f,89.5f),new Vector3(8,-4.5f,75.3f),85);
        }
        finally{Object.DestroyImmediate(go);Object.DestroyImmediate(fill.gameObject);EditorSceneManager.OpenScene(RailQuarryMine.BakeScene);}
        void Shot(string n,Vector3 eye,Vector3 target,float fov){cam.transform.position=eye;cam.transform.rotation=Quaternion.LookRotation(target-eye);cam.fieldOfView=fov;var rt=new RenderTexture(1600,1100,24);var old=RenderTexture.active;var tex=new Texture2D(1600,1100,TextureFormat.RGB24,false);try{cam.targetTexture=rt;cam.Render();RenderTexture.active=rt;tex.ReadPixels(new Rect(0,0,1600,1100),0,0);tex.Apply();File.WriteAllBytes("output/quarry-mountain/"+n+".png",tex.EncodeToPNG());}finally{cam.targetTexture=null;RenderTexture.active=old;Object.DestroyImmediate(tex);rt.Release();Object.DestroyImmediate(rt);}}
    }
}
#endif
