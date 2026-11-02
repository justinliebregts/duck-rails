using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Verifies that accepted hand displacement persists across Unity player-loop frames.
// This proves persistence only; MovementCadencePlayChecks separately tests smoothing.
[InitializeOnLoad]
public static class MovementSnapbackPlayChecks
{
    static float physicalOriginB, retainedPhysicalB, lastDisplayedPushB;
    const string Key = "DuckZ.MovementSnapbackPlayChecks";
    const float Push = .1f;
    static MovementTestRig a, b;
    static Rigidbody[] probes;
    static readonly string[] Names = {
        "A Transform only", "B Transform only", "B recenter-style Rigidbody write + Transform",
        "B MovePosition + Transform", "B both Rigidbody writes + Transform", "B Rigidbody position control"
    };
    static readonly float[] retained = new float[6];
    static int frames, samples, pulseFrame, physicsSteps, pulseSteps, nextSteps;
    static float pulseDt, nextDt;
    static double started;
    static float originA, originB, appliedA, appliedB, retainedA, retainedB, retainedLaterA, retainedLaterB;
    static float minA = float.PositiveInfinity, minB = float.PositiveInfinity;
    static bool finished;

    static MovementSnapbackPlayChecks()
    {
        EditorApplication.playModeStateChanged += state => {
            if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(Key, false)) Start();
        };
    }
    public static void Run()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Isolated batch mode only");
        SessionState.SetBool(Key, true);
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        EditorApplication.EnterPlaymode();
    }
    static void Require(bool ok, string message) { if (!ok) throw new Exception(message); }
    static void Start()
    {
        try {
            frames=samples=pulseFrame=physicsSteps=0; finished=false; started=EditorApplication.timeSinceStartup;
            minA=minB=float.PositiveInfinity;
            Physics.simulationMode=SimulationMode.FixedUpdate; Physics.autoSyncTransforms=false;
            Time.fixedDeltaTime=1f/72; Time.maximumDeltaTime=Time.fixedDeltaTime*2;
            QualitySettings.vSyncCount=0; Application.targetFrameRate=144;
            // Exercise repeatable simulated 144/72 Hz intervals regardless of editor
            // import/JIT scheduling; the actual Unity physics/interpolation loop still runs.
            Time.captureDeltaTime=1f/144;
            var services=new GameObject("Snapback player services"); services.SetActive(false);
            GR.Player.instance=services.AddComponent<GR.Player>(); HelicopterManager.instance=null;
            a=new MovementTestRig(); b=new MovementTestRig();
            b.p.rb.interpolation=RigidbodyInterpolation.Interpolate;
            a.p.rb.interpolation=RigidbodyInterpolation.None;
            foreach(var rig in new[]{a,b}) {
                rig.p.velocityLimit=float.MaxValue; // No velocity launch can explain a reversed hand push.
                rig.p.rb.useGravity=false;
                rig.p.locomotionEnabledLayers=1<<6;
                rig.p.minimumRaycastDistance=.05f;
            }
            var floor=new GameObject("Snapback floor"); floor.layer=6;
            floor.AddComponent<BoxCollider>().size=new Vector3(100,.2f,100); floor.transform.position=Vector3.down*.1f;
            probes=new Rigidbody[Names.Length];
            for(int i=0;i<probes.Length;i++) {
                probes[i]=new GameObject(Names[i]).AddComponent<Rigidbody>();
                probes[i].useGravity=false;
                probes[i].interpolation=i==0?RigidbodyInterpolation.None:RigidbodyInterpolation.Interpolate;
            }
            Physics.SyncTransforms(); // Fixture setup only; never called between the write and its observation.
            new GameObject("Snapback player-loop probe").AddComponent<MovementSnapbackPlayProbe>();
        } catch(Exception e) { Fail(e); }
    }
    public static void FixedTick() { physicsSteps++; }
    public static void Move()
    {
        if(finished) return;
        try {
            Require(EditorApplication.timeSinceStartup-started<30,"Snapback play-loop timed out");
            int phase=frames%40;
            if(frames==240) { Application.targetFrameRate=72; Time.captureDeltaTime=1f/72; }
            if(phase==0) {
                foreach(var rig in new[]{a,b}) {
                    rig.p.leftHandTransform.localPosition=new Vector3(-.3f,.05f,0);
                    rig.p.rightHandTransform.localPosition=new Vector3(.3f,1,0);
                    rig.p.Warp(Vector3.zero);
                }
                foreach(var probe in probes) { probe.position=Vector3.zero; probe.transform.position=Vector3.zero; probe.linearVelocity=Vector3.zero; }
            }
            if(phase==11) {
                Require(Time.frameCount>pulseFrame,"Persistence was not sampled on a subsequent Unity frame");
                nextSteps=physicsSteps-pulseSteps; nextDt=Time.deltaTime;
                retainedA=a.root.transform.position.x-originA; retainedB=b.root.transform.position.x-originB;
                retainedPhysicalB=b.p.rb.position.x-physicalOriginB;
            }
            if(phase==15) {
                retainedLaterA=a.root.transform.position.x-originA; retainedLaterB=b.root.transform.position.x-originB;
                for(int i=0;i<probes.Length;i++) retained[i]=probes[i].transform.position.x;
                Require(a.p.rb.linearVelocity.sqrMagnitude<1e-8f && b.p.rb.linearVelocity.sqrMagnitude<1e-8f,"Unexpected velocity invalidated snapback isolation");
                Require(appliedA>.08f && appliedB>.08f,"Real hand contact failed to apply the test displacement");
                // Physical hand movement is immediate; its display now shares the
                // one-step smoothing delay. Both must retain the complete push.
                minA=Mathf.Min(minA,Mathf.Min(retainedA,retainedLaterA)); minB=Mathf.Min(minB,Mathf.Min(retainedPhysicalB,retainedLaterB)); samples++;
                Debug.Log($"SNAPBACK sample={samples} targetFPS={Application.targetFrameRate}: applied A={appliedA:F5}, B={appliedB:F5}; next Update retained A={retainedA:F5}, B={retainedB:F5}; five Updates later A={retainedLaterA:F5}, B={retainedLaterB:F5}; next physics steps={nextSteps}, dt={pulseDt:F6}/{nextDt:F6}; rb velocity=0; head-pivot turning.");
                for(int i=0;i<probes.Length;i++) Debug.Log($"WRITE ISOLATION {Names[i]}: retained {retained[i]:F5} of {Push:F2} m");
                Require(retained[0]>.08f && retained[5]>.08f,"Engine baseline/physics-write controls did not preserve the displacement");
            }
            if(phase==10) {
                originA=a.root.transform.position.x; originB=b.root.transform.position.x;
                physicalOriginB=b.p.rb.position.x;lastDisplayedPushB=0;
                a.p.leftHandTransform.localPosition-=Vector3.right*Push;
                b.p.leftHandTransform.localPosition-=Vector3.right*Push;
                pulseFrame=Time.frameCount;
                pulseSteps=physicsSteps; pulseDt=Time.deltaTime;
            }
            a.move(Time.deltaTime); b.move(Time.deltaTime);
            if(phase==10) {
                Require(a.p.leftHandColliding && b.p.leftHandColliding,
                    $"Push did not use actual hand collision: frame={frames}, dt={Time.deltaTime:R}, A={a.p.leftHandColliding}, B={b.p.leftHandColliding}, handA={a.p.leftHandTransform.position}, handB={b.p.leftHandTransform.position}");
                appliedA=a.root.transform.position.x-originA; appliedB=b.p.rb.position.x-physicalOriginB;
            }
            if(phase>=10 && phase<=15) {
                float shown=b.root.transform.position.x-originB;
                Require(shown>=lastDisplayedPushB-.0001f && shown<=Push+.001f,"Smoothed hand push reversed or overshot");
                lastDisplayedPushB=shown;
            }
            for(int i=0;i<probes.Length;i++) {
                var rb=probes[i];
                if(i==2 || i==4) rb.position+=Vector3.zero;
                if(i==3 || i==4) rb.MovePosition(rb.position);
                if(phase==10) {
                    if(i==5) rb.position+=Vector3.right*Push;
                    else rb.transform.position+=Vector3.right*Push;
                }
            }
        } catch(Exception e) { Fail(e); }
    }
    public static void Sample()
    {
        if(finished) return;
        try {
            if(++frames<480) return;
            Require(samples==12,"Missing snapback scenarios");
            Require(minA>.08f,"Noninterpolated control lost a zero-velocity hand push");
            // This is intentionally a true regression assertion, not an expected-failure PASS.
            Require(minB>.08f,$"SNAPBACK REGRESSION: B discarded hand-generated displacement before the next Update. Minimum retained A={minA:F5} m, B={minB:F5} m, expected >=0.08 m from a 0.10 m push.");
            Debug.Log("MovementSnapbackPlayChecks PASS: hand displacement persisted across real player-loop frames at 72/144 FPS, with gravity/launch/carry/turn excluded.");
            Finish(0);
        } catch(Exception e) { Fail(e); }
    }
    static void Fail(Exception e) { Debug.LogException(e); Finish(1); }
    static void Finish(int code) { finished=true; Time.captureDeltaTime=0; SessionState.SetBool(Key,false); EditorApplication.Exit(code); }
}

public sealed class MovementSnapbackPlayProbe : MonoBehaviour
{
    void FixedUpdate() => MovementSnapbackPlayChecks.FixedTick();
    void Update() => MovementSnapbackPlayChecks.Move();
    void LateUpdate() => MovementSnapbackPlayChecks.Sample();
}
