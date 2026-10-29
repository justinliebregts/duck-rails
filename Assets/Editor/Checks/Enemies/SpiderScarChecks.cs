using System;
using System.Reflection;
using Game.Animation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

public static class SpiderScarChecks
{
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    static object Call(object target, string name, params object[] args) => target.GetType().GetMethod(name, Private).Invoke(target, args);
    static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
    static void Drain(SpiderScarPool pool, int ticks = 12)
    {
        for (int i = 0; i < ticks; i++) { Call(pool, "LateUpdate"); Check(pool.PlacementRaycasts <= 2, "Shared physics budget exceeded"); }
    }
    static BoxCollider Wall(Vector3 center, Vector3 size)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.transform.position = center; go.transform.localScale = size;
        Physics.SyncTransforms(); return go.GetComponent<BoxCollider>();
    }
    public static void Run()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var prefab = Resources.Load<GameObject>("Effects/SpiderScarDecalPFX");
        Check(prefab != null && !ShaderUtil.ShaderHasError(prefab.GetComponent<ParticleSystemRenderer>().sharedMaterial.shader), "Prefab/shader missing");
        var camera = new GameObject("Camera", typeof(Camera)).GetComponent<Camera>();
        camera.tag = "MainCamera"; camera.transform.position = new Vector3(0,0,2);
        var pool = new GameObject("Pool").AddComponent<SpiderScarPool>(); Call(pool, "Awake");
        var wall = Wall(Vector3.zero, new Vector3(4,4,.2f));
        var owner = new GameObject("Spider").AddComponent<SpiderGait>(); owner.root = owner.transform;
        typeof(SpiderGait).GetField("_scarPool", Private).SetValue(owner, pool);
        typeof(SpiderGait).GetField("surfaceMask", Private).SetValue(owner, (LayerMask)1);
        pool.Observe(owner, wall, new Vector3(0,0,-1), new Vector3(0,0,.12f), .42f);
        Call(pool,"LateUpdate");
        Check(pool.ScarsPlaced == 0 && pool.PlacementRaycasts == 1, "Centre hit must not place before fit");
        Call(pool,"LateUpdate"); Check(pool.ScarsPlaced == 0 && pool.PlacementRaycasts == 2, "First pair must remain pending");
        Call(pool,"LateUpdate"); Check(pool.ScarsPlaced == 1 && pool.PlacementRaycasts == 2 && pool.FitRaycasts == 4, "Second pair should complete five-query placement");
        Drain(pool);
        var particles = new ParticleSystem.Particle[1];
        var first = pool.GetComponentsInChildren<ParticleSystem>()[0]; first.GetParticles(particles);
        float expectedSize = prefab.GetComponent<ParticleSystem>().main.startSize.constant;
        Check(Mathf.Abs(particles[0].startSize - expectedSize) < .001f && Mathf.Abs(particles[0].rotation) < .001f, $"Particle must use sampled prefab size and emitter rotation: count={first.particleCount}, size={particles[0].startSize}, expected={expectedSize}, rotation={particles[0].rotation}");
        Check(first.isPaused, "Particle must remain paused");
        int before = pool.FitRaycasts;
        pool.ObserveKill(owner,new Vector3(0,0,-1),new Vector3(0,0,.6f),1); Drain(pool);
        Check(pool.ActiveCount==1 && pool.FitRaycasts==before, "Nearby scar should refresh without repeating fit");
        pool.ObserveKill(owner,new Vector3(1,0,.3f),new Vector3(1,0,.6f),1); Drain(pool);
        Check(pool.ActiveCount==1,"Open-air kill must not scar");
        var narrow = Wall(new Vector3(5,0,0),new Vector3(.15f,3,.2f));
        before=pool.FitRaycasts;
        pool.Observe(owner,narrow,new Vector3(5,0,-1),new Vector3(5,0,.12f),.42f); Drain(pool);
        Check(pool.ActiveCount==1 && pool.FitRejected==1 && pool.FitRaycasts-before <= 2,"Thin rail should reject early");
        pool.Observe(owner,narrow,new Vector3(5,0,-1),new Vector3(5,0,.12f),.42f); Call(pool,"LateUpdate");
        Check(pool.PlacementRaycasts==0,"Rejected spot should avoid repeated detection rays");
        // Deterministic fixture: a 0.7 square fits an 0.8 square at 0 degrees,
        // but its corners overhang at 45 degrees. No mesh thickness heuristic.
        var patch = Wall(new Vector3(-5,0,0),new Vector3(.8f,.8f,.2f));
        typeof(SpiderScarPool).GetField("prefabRotation",Private).SetValue(pool,new ParticleSystem.MinMaxCurve(Mathf.PI/4));
        var other = new GameObject("Other spider").AddComponent<SpiderGait>();
        pool.Observe(other,patch,new Vector3(-5,0,-1),new Vector3(-5,0,.12f),1f/2.4f); Drain(pool);
        Check(pool.ActiveCount==1,"Rotated corners must reject despite unrotated footprint fitting");
        typeof(SpiderScarPool).GetField("prefabRotation",Private).SetValue(pool,new ParticleSystem.MinMaxCurve(0));
        var third = new GameObject("Third spider").AddComponent<SpiderGait>();
        pool.Observe(third,patch,new Vector3(-5,0,-1),new Vector3(-5,0,.12f),1f/2.4f); Drain(pool);
        Check(pool.ActiveCount==2,"Unrotated 0.7 prefab footprint should fit 0.8 surface");
        var roof=Wall(new Vector3(0,5,0),new Vector3(4,.2f,4));
        owner.ReceiveKillScar(new Vector3(0,4,0),new Vector3(0,5.6f,0)); Drain(pool);
        Check(pool.ActiveCount==3,"Received rooftop kill must pass fit checks");
        // Changed surfaces invalidate pending geometry rather than using stale probes.
        pool.Observe(other,wall,new Vector3(1,1,-1),new Vector3(1,1,.12f),.42f); Call(pool,"LateUpdate");
        wall.transform.position += Vector3.up*.1f; Physics.SyncTransforms(); Drain(pool);
        Check(pool.ActiveCount==3,"Moved pending surface must cancel fit");
        var renderers=pool.GetComponentsInChildren<ParticleSystemRenderer>();
        foreach(var r in renderers) r.transform.hasChanged=false;
        Drain(pool,1); foreach(var r in renderers) Check(!r.transform.hasChanged,"Static scar transform rewritten");
        camera.transform.position=Vector3.one*100;
        Call(pool,"BeforeCamera",default(ScriptableRenderContext),camera);
        foreach(var r in renderers) Check(!r.enabled,"Distant camera failed to cull");
        camera.transform.position=new Vector3(0,0,2);
        Call(pool,"BeforeCamera",default(ScriptableRenderContext),camera);
        Check(renderers[0].enabled,"Nearby camera failed to restore visibility");
        // Explicit placement below isolates pool lifecycle from already-tested fits.
        for(int i=0;i<20;i++) Call(pool,"Place",wall.transform,new Vector3(i,1,0),Vector3.forward,1f,Time.time,expectedSize,0f);
        Drain(pool); Check(pool.ActiveCount==16 && renderers.Length==16,"Pool capacity changed");
        var tick=(Action)Delegate.CreateDelegate(typeof(Action),pool,typeof(SpiderScarPool).GetMethod("LateUpdate",Private));
        for(int i=0;i<32;i++) tick();
        long start=GC.GetAllocatedBytesForCurrentThread();
        for(int i=0;i<128;i++) { pool.Observe(third,patch,new Vector3(-5,0,-1),new Vector3(-5,0,.12f),.42f); tick(); }
        long bytes=GC.GetAllocatedBytesForCurrentThread()-start;
        Check(bytes==0,$"Warmed fit/update cycles allocated {bytes} bytes");
        var slots=(Array)typeof(SpiderScarPool).GetField("slots",Private).GetValue(pool);
        foreach(var slot in slots) slot.GetType().GetField("expiry").SetValue(slot,-1f);
        Drain(pool); Check(pool.ActiveCount==0,"Scars must expire");
        Debug.Log($"SpiderScarChecks: ALL PASSED; allocation sample={bytes} bytes");
    }
}

