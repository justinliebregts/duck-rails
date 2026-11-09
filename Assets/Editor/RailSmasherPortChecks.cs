using System;
using System.Reflection;
using Game.AI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

// Disposable batch project only: real physics against Rails prefabs, no scene saves.
public static class RailSmasherPortChecks
{
    const BindingFlags P = BindingFlags.Instance | BindingFlags.NonPublic;
    static void Set(object o, string n, object v) => o.GetType().GetField(n, P).SetValue(o, v);
    static object Call(object o, string n, params object[] args) => o.GetType().GetMethod(n, P).Invoke(o, args);
    static void Check(bool ok, string label) { if (!ok) throw new Exception(label); }
    public static void Run()
    {
        if (!Application.isBatchMode) throw new Exception("Disposable batch only");
        try { Restrictions(); Skyscraper(); RailEnemyBoardingChecks.Run(); RailTrackCrossingChecks.Run(); Debug.Log("RAILS SMASHER PORT PASS"); EditorApplication.Exit(0); }
        catch(Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
    }
    static void Restrictions()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var floor = new GameObject("Floor"); floor.layer=6; floor.transform.position=new Vector3(0,-.5f,0); floor.AddComponent<BoxCollider>().size=new Vector3(100,1,100);
        var enemy = SmasherTallBuildingChecks.MakeActor(out var jump);
        var bounds=enemy.gameObject.AddComponent<RailEnemyBounds>();
        Set(bounds,"home",new Bounds(Vector3.zero,new Vector3(40,40,40))); Set(bounds,"assigned",true); Call(jump,"Awake");
        Physics.SyncTransforms();
        Check((bool)Call(jump,"Supported",Vector3.zero,(LayerMask)(1<<6)),"Ordinary ground allowed");
        Check(!(bool)Call(jump,"Supported",new Vector3(21,0,0),(LayerMask)(1<<6)),"Outside home landing rejected");
        Check(!(bool)Call(jump,"ClearSegment",new Vector3(19,3,0),new Vector3(21,3,0),(LayerMask)(1<<6)),"Out-of-block arc rejected");
        var old=RailTrain.Instance; var trainObject=new GameObject("Train exclusion"); trainObject.SetActive(false); var train=trainObject.AddComponent<RailTrain>();
        typeof(RailTrain).GetProperty("Instance").SetValue(null,train);
        try {
            Check(!(bool)Call(jump,"Supported",Vector3.zero,(LayerMask)(1<<6)),"Train landing rejected");
            Check(!(bool)Call(jump,"ClearSegment",new Vector3(-5,1,0),new Vector3(5,1,0),(LayerMask)(1<<6)),"Train crossing arc rejected");
            train.transform.SetPositionAndRotation(new Vector3(10,0,10),Quaternion.Euler(0,50,0));
            Check(!(bool)Call(jump,"Supported",new Vector3(10,0,10),(LayerMask)(1<<6)),"Moved rotated train rejected");
            Check((bool)Call(jump,"Supported",Vector3.zero,(LayerMask)(1<<6)),"Former train location allowed");
        } finally { typeof(RailTrain).GetProperty("Instance").SetValue(null,old); Object.DestroyImmediate(trainObject); }
        var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Rails/RailSmasher.prefab");
        Check(prefab.GetComponent<RailEnemyBoarding>()==null,"Smasher has no boarding opt-in");
        var so=new SerializedObject(prefab.GetComponent<ZombieSmasherJump>());
        Check(so.FindProperty("maxJumpHeight").floatValue==12 && so.FindProperty("maxJumpDistance").floatValue==16,"Rails variant inherits new limits");
        Debug.Log("RAILS RESTRICTIONS PASS: home bounds, arc rejection, translated/rotated train, inherited limits, no boarding opt-in");
    }
    static void Skyscraper()
    {
        EditorSceneManager.OpenScene(RailSkyscraper.BakeScene,OpenSceneMode.Single); Physics.SyncTransforms();
        var block=Object.FindFirstObjectByType<RailBlock>();
        var enemy=SmasherTallBuildingChecks.MakeActor(out var jump); var ground=enemy.GetComponent<GroundLocomotion>();
        var bounds=enemy.gameObject.AddComponent<RailEnemyBounds>(); Set(bounds,"home",block.enemyBounds); Set(bounds,"assigned",true); Call(jump,"Awake");
        var manager=SmasherTallBuildingChecks.Manager(); int successes=0; float highest=0;
        var candidates = new System.Collections.Generic.List<(Vector3 source, Vector3 target)>();
        foreach(var tower in RailSkyscraper.Towers)
        foreach(float level in new[]{4f,8f,12f,16f})
        foreach(float x in new[]{9f,14f,17f})
        {
            candidates.Add((tower+new Vector3(x,0,-4),tower+new Vector3(x,level,1.2f)));
        }
        for(int t=0;t<2;t++) foreach(float y in new[]{4f,8f,16f}) foreach(float x in new[]{9f,14f,17f})
            candidates.Add((RailSkyscraper.Towers[t]+new Vector3(x,y,23.5f),RailSkyscraper.Towers[t+2]+new Vector3(x,Mathf.Min(16,y+8),.8f)));
        foreach(var pair in candidates)
        {
            Vector3 source=pair.source, target=pair.target;
            if(!Physics.Raycast(source+Vector3.up*.2f,Vector3.down,out var street,.4f,1<<6) || !Physics.Raycast(target+Vector3.up*.2f,Vector3.down,out var roof,.4f,1<<6)) continue;
            // Ground approaches and elevated jumps between the front/rear towers.
            if(roof.point.y-street.point.y>11.5f) continue;
            enemy.Teleport(ground.RootFromCapsuleBottom(ground.CapsuleBottomOnSurface(street.point,street.normal)),Quaternion.identity);
            enemy.SetRemoteState(Enemy.State.Chase); Set(enemy,"_lastSeenPos",roof.point+Vector3.up*1.7f);
            Set(jump,"_checkArmed",true); Set(jump,"_nextCheck",Time.time); Physics.SyncTransforms();
            if(!(bool)Call(jump,"TryPlan",enemy,(LayerMask)(1<<6))) continue;
            bool launched=jump.TickJump(enemy,manager,1f/60);
            for(int i=0;i<240 && jump.IsJumping;i++) jump.TickJump(enemy,manager,1f/60);
            Check(launched&&!jump.IsJumping&&Mathf.Abs(enemy.Root.position.y-roof.point.y)<.15f,"Actual skyscraper landing");
            successes++; highest=Mathf.Max(highest,roof.point.y); Debug.Log("SKYSCRAPER LANDING PASS "+enemy.Root.position);
        }
        Check(successes>=4 && highest>=15.9f,"At least four authored skyscraper approaches including 16m level: "+successes+" highest="+highest);
        Debug.Log("SKYSCRAPER PASS approaches="+successes+" highest="+highest);
    }
}
