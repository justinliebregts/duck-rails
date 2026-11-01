#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class RailSubwayCompoundChecks
{
    const string Evidence = "docs/validation/subway-compound-v2";
    [MenuItem("Duck Rails/Compound/Validate Subway Blockout")]
    public static void Validate()
    {
        var log=new StringBuilder(); int failed=0;
        Action<bool,string> check=(ok,name)=> { log.AppendLine((ok?"PASS ":"FAIL ")+name); if(!ok) failed++; };
        var world=UnityEngine.Object.FindAnyObjectByType<RailWorld>();
        if(world==null || world.gameObject.scene.path!="Assets/Scenes/Compound.unity") throw new InvalidOperationException("Open Compound first.");
        var root=world.compound.transform.Find("SubwayCompound");
        if(root==null) throw new InvalidOperationException("Install subway prefab first.");
        Physics.SyncTransforms(); int mask=LayerMask.GetMask("Environment");
        check(PrefabUtility.IsPartOfPrefabInstance(root),"Environment remains a prefab instance");
        check(root.childCount==7 && root.Cast<Transform>().All(t=>PrefabUtility.IsAnyPrefabInstanceRoot(t.gameObject)),"Seven independently editable nested modules");
        check(root.GetComponentsInChildren<Transform>().All(t=>GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject)==0),"No missing scripts");
        check(root.GetComponentsInChildren<MeshRenderer>().All(r=>r.sharedMaterial!=null && r.sharedMaterial.shader!=null),"All geometry has materials and shaders");
        check(root.GetComponentsInChildren<Collider>().All(c=>c.gameObject.layer==LayerMask.NameToLayer("Environment")),"Solid geometry uses DuckPlayer Environment collision layer");
        for(int i=0;i<12;i++)
        {
            var p=world.compoundSpawn.position+new Vector3((i%4-1.5f)*1.5f,0,i/4*1.5f);
            check(Physics.Raycast(p+Vector3.up,Vector3.down,out var h,2,mask) && Mathf.Abs(h.point.y)<.02f,"Spawn "+i+" has level floor");
            check(!Physics.CheckCapsule(p+Vector3.up*.25f,p+Vector3.up*.9f,.22f,mask,QueryTriggerInteraction.Ignore),"Spawn "+i+" has body clearance");
        }
        // Sample the walkable routes using actual scene collision, not mesh names alone.
        Action<Vector3,Vector3,string> corridor=(a,b,name)=>
        {
            bool clear=true;
            for(int i=0;i<=20;i++) { var p=Vector3.Lerp(a,b,i/20f); if(Physics.CheckCapsule(p+Vector3.up*.3f,p+Vector3.up*1.2f,.23f,mask,QueryTriggerInteraction.Ignore)) clear=false; }
            check(clear,name);
        };
        corridor(new Vector3(-2.5f,0,-6.7f),new Vector3(-2.5f,0,4.5f),"Left route around fountain clear");
        corridor(new Vector3(2.5f,0,-6.7f),new Vector3(2.5f,0,4.5f),"Right route around fountain clear");
        corridor(new Vector3(0,0,3),new Vector3(0,0,8),"Departure approach clear");
        var shop=root.Find("05 Walk-through supplies store");
        corridor(shop.TransformPoint(new Vector3(-1.4f,0,-3)),shop.TransformPoint(new Vector3(-1.4f,0,1)),"Shop front walk-through clear");
        corridor(shop.TransformPoint(new Vector3(-3.5f,0,0)),shop.TransformPoint(new Vector3(-1,0,0)),"Shop side opening clear");
        var shell=root.Find("01 Terminal shell");
        check(Mathf.Abs(shell.Find("Continuous walkable floor").GetComponent<BoxCollider>().size.x-18)<.01f,"Occupied floor narrowed to 18 metres");
        check(shell.Cast<Transform>().Count(t=>t.name.EndsWith("structural pillar"))==6,"Six structural pillars in coherent grid");
        foreach(float side in new[]{-1f,1f})
        {
            bool sealedSide=true;
            foreach(float y in new[]{.25f,.5f,1.5f,3f,5f,6.85f})
                for(float z=-12;z<=10;z+=.5f)
                    if(!Physics.Raycast(new Vector3(side*8.3f,y,z),Vector3.right*side,1,mask)) sealedSide=false;
            check(sealedSide,"Track edge sealed at walking and climbing heights "+side);
            check(Physics.Raycast(new Vector3(side*8.25f,.31f,-4.1f),Vector3.right*side,out var gap,1,mask) && gap.collider.name=="Continuous see-through track guard","Low viewing slit reaches see-through screen rather than solid panel "+side);
            var track=shell.Find(side<0?"Left inaccessible track audio anchor":"Right inaccessible track audio anchor");
            check(track.Cast<Transform>().Where(t=>t.name=="Rail below boarding edge").All(t=>t.position.y<-.7f),"Rails recessed below original boarding edge "+side);
        }
        var info=root.Find("04 Station information nook");
        foreach(var bay in new[]{info,shop})
        {
            var roof=bay.Find("Rough corrugated canopy").GetComponent<Collider>().bounds;
            check(Physics.Raycast(new Vector3(roof.center.x,roof.max.y+.1f,roof.center.z),Vector3.up,out var upper,5,mask)&&upper.distance>2.8f,"Open upper station volume above "+bay.name);
        }
        for(int i=0;i<5;i++) check(Physics.Raycast(new Vector3(-2.8f+i*1.4f,3,-11),Vector3.back,3,mask),"Stair barricade sealed across width "+i);
        check(world.boardingTrigger.isTrigger && world.boardingTrigger.bounds.Contains(new Vector3(0,.5f,8)),"Boarding trigger includes low duck body");
        check(!world.boardingTrigger.bounds.Contains(world.compoundSpawn.position),"Spawn is outside departure trigger");
        var keypad=world.compound.GetComponentInChildren<RailPrivateTerminal>();
        check(keypad!=null && keypad.create!=null && keypad.join!=null && keypad.start!=null && keypad.leave!=null,"Existing private terminal bindings retained");
        check(keypad!=null && keypad.create.onPressed.GetPersistentEventCount()>0 && keypad.join.onPressed.GetPersistentEventCount()>0,"Existing keypad events retained");
        check(world.departureBoard!=null && world.privateTerminal!=null && world.compoundSpawn!=null,"World presentation references retained");
        var textures=AssetDatabase.FindAssets("t:Texture2D",new[]{RailSubwayCompoundBuilder.Folder}).Select(AssetDatabase.GUIDToAssetPath);
        check(textures.All(p=>((TextureImporter)AssetImporter.GetAtPath(p)).filterMode==FilterMode.Point),"Pixel textures use Point filtering");
        log.AppendLine("Renderers: "+root.GetComponentsInChildren<Renderer>().Length+"; colliders: "+root.GetComponentsInChildren<Collider>().Length+"; provisional live lights: "+root.GetComponentsInChildren<Light>().Length);
        log.AppendLine("Headset, twelve-client crowding, acoustics and performance acceptance remain pending.");
        Directory.CreateDirectory(Evidence); File.WriteAllText(Evidence+"/editmode-checks.txt",log.ToString());
        if(failed>0) throw new Exception(failed+" subway checks failed.\n"+log);
        Debug.Log(log.ToString());
    }

    [MenuItem("Duck Rails/Compound/Render Subway Views")]
    public static void RenderViews()
    {
        Directory.CreateDirectory("output/compound-blockout-v2");
        Render("forward",new Vector3(0,2.2f,-7.8f),new Vector3(0,2.5f,3),85);
        Render("reverse",new Vector3(0,2.2f,5),new Vector3(0,2.3f,-9.5f),80);
        Render("low-eye",new Vector3(0,.5f,-3.5f),new Vector3(0,1.1f,5),90);
        Render("left-boarding-edge",new Vector3(-6.4f,1.1f,-5.2f),new Vector3(-9.4f,1.2f,-2),85);
        Render("upper-volume",new Vector3(0,1.8f,-4),new Vector3(-3,5,3),85);
    }
    static void Render(string name,Vector3 pos,Vector3 target,float fov)
    {
        var go=new GameObject("Temporary subway capture"); var c=go.AddComponent<Camera>();
        var rt=new RenderTexture(1600,1000,24); var tex=new Texture2D(1600,1000,TextureFormat.RGB24,false);
        var previous=RenderTexture.active;
        try
        {
            c.transform.position=pos; c.transform.LookAt(target); c.fieldOfView=fov; c.nearClipPlane=.05f; c.farClipPlane=70;
            c.clearFlags=CameraClearFlags.SolidColor; c.backgroundColor=new Color(.02f,.025f,.03f); c.useOcclusionCulling=false;
            c.targetTexture=rt; c.Render(); RenderTexture.active=rt;
            tex.ReadPixels(new Rect(0,0,1600,1000),0,0); tex.Apply();
            File.WriteAllBytes("output/compound-blockout-v2/"+name+".png",tex.EncodeToPNG());
        }
        finally
        {
            RenderTexture.active=previous; c.targetTexture=null;
            UnityEngine.Object.DestroyImmediate(tex); UnityEngine.Object.DestroyImmediate(rt); UnityEngine.Object.DestroyImmediate(go);
        }
    }
}
#endif
