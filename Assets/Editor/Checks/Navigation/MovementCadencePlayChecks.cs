using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Exercise Unity's real interpolation loop, including zero and two physics steps.
// Manual Physics.Simulate alone cannot validate the pose submitted for rendering.
// A and the same-position-write control reproduce the original failure; B must smooth it.
[InitializeOnLoad]
public static class MovementCadencePlayChecks
{
    const string Key = "DuckZ.MovementCadencePlayChecks";
    const float Step = 1f / 72, Speed = 4;
    static readonly float[] Intervals = { Step, Step - .001f, Step + .001f, Step };
    static MovementTestRig a, b;
    static Rigidbody control, samePoseWrite;
    static int frames, steps, zeros, doubles, samples, sequences;
    static int stepHistory;
    static double errorA, errorB, errorControl, errorSamePose;
    static float lastA, lastB, lastControl, lastSamePose, preOffset;
    static float turningStart, turningYaw, totalTurn;
    static double turningFixedStart;
    static bool finished;
    static MovementCadencePlayChecks()
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
    static void Start()
    {
        try {
            frames=steps=zeros=doubles=samples=sequences=stepHistory=0; finished=false;
            errorA=errorB=errorControl=errorSamePose=0;
            Physics.simulationMode=SimulationMode.FixedUpdate; Physics.autoSyncTransforms=false;
            Time.fixedDeltaTime=Step; Time.maximumDeltaTime=Step*2;
            Time.captureDeltaTime=Step;
            QualitySettings.vSyncCount=0; Application.targetFrameRate=144;
            var services=new GameObject("Cadence services"); services.SetActive(false);
            GR.Player.instance=services.AddComponent<GR.Player>(); HelicopterManager.instance=null;
            a=new MovementTestRig(); b=new MovementTestRig();
            b.p.rb.interpolation=RigidbodyInterpolation.Interpolate;
            a.p.rb.interpolation=RigidbodyInterpolation.None;
            b.p.Warp(Vector3.forward*3); // Separate real colliders; the comparison uses X travel.
            foreach(var rig in new[]{a,b}) { rig.p.headCollider.enabled=true;rig.p.bodyCollider.enabled=true; }
            a.p.rb.linearVelocity=b.p.rb.linearVelocity=Vector3.right*Speed;
            control=new GameObject("Native interpolation control").AddComponent<Rigidbody>();
            control.useGravity=false; control.interpolation=RigidbodyInterpolation.Interpolate;
            control.linearVelocity=Vector3.right*Speed;
            samePoseWrite=new GameObject("Interpolation with same-position Rigidbody write").AddComponent<Rigidbody>();
            samePoseWrite.useGravity=false; samePoseWrite.interpolation=RigidbodyInterpolation.Interpolate;
            samePoseWrite.linearVelocity=Vector3.right*Speed;
            Physics.SyncTransforms();
            new GameObject("Cadence player-loop probe").AddComponent<MovementCadencePlayProbe>();
        } catch(Exception e) { Fail(e); }
    }
    public static void FixedTick() { steps++; }
    public static void Move()
    {
        if(finished)return;
        try {
            preOffset=b.root.transform.position.x-b.p.rb.position.x;
            if(frames>=120 && frames<240) {
                // Fresh room-scale tracking alongside virtual travel must not reintroduce
                // a root position write or stop the free-flight smoothing.
                a.p.headCollider.transform.localPosition+=Vector3.forward*.005f;
                b.p.headCollider.transform.localPosition+=Vector3.forward*.005f;
                a.p.leftHandTransform.localPosition+=Vector3.forward*.005f;
                b.p.leftHandTransform.localPosition+=Vector3.forward*.005f;
                a.p.rightHandTransform.localPosition+=Vector3.forward*.005f;
                b.p.rightHandTransform.localPosition+=Vector3.forward*.005f;
            }
            if(frames>=240) {
                float angle=frames==270?45f:1f;
                b.p.Turn(angle);totalTurn+=angle;
            }
            a.move(Time.deltaTime); b.move(Time.deltaTime);
            samePoseWrite.position+=Vector3.zero;
        } catch(Exception e) { Fail(e); }
    }
    public static void Sample()
    {
        if(finished)return;
        try {
            float pa=a.p.headCollider.transform.position.x, pb=b.p.headCollider.transform.position.x;
            float pc=control.transform.position.x;
            if(frames>30 && frames<240) {
                if(steps==0) zeros++; if(steps==2)doubles++;
                stepHistory=((stepHistory<<2)|Math.Min(steps,3))&255;
                if(stepHistory==0x49)sequences++; // 1,0,2,1
                float expected=Speed*Time.deltaTime;
                errorA+=Math.Abs((pa-lastA)-expected); errorB+=Math.Abs((pb-lastB)-expected);
                errorControl+=Math.Abs((pc-lastControl)-expected); samples++;
                errorSamePose+=Math.Abs((samePoseWrite.transform.position.x-lastSamePose)-expected);
                if(frames<39)Debug.Log($"CADENCE frame={frames} steps={steps} dt={Time.deltaTime:F6} phase={Time.timeAsDouble-Time.fixedTimeAsDouble:F6} A={pa-lastA:F6} B={pb-lastB:F6} control={pc-lastControl:F6} nativeOffsetBeforeMovement={preOffset:F6}");
                if(Math.Abs(a.p.rb.position.x-b.p.rb.position.x)>.001f)throw new Exception("Interpolation changed authoritative free-flight travel");
                if(Math.Abs(b.p.rb.position.z-3)>.001f)throw new Exception("Room-scale tracking recentered the B root");
            }
            lastA=pa;lastB=pb;lastControl=pc;lastSamePose=samePoseWrite.transform.position.x;steps=0;
            if(++frames<240) {
                Time.captureDeltaTime=frames==30 ? Step-Mathf.Repeat((float)(Time.timeAsDouble-Time.fixedTimeAsDouble),Step)+.0001f : Intervals[frames%4];
                return;
            }
            if(frames==240) {
                turningStart=PhysicalHeadX();turningFixedStart=Time.fixedTimeAsDouble;
                turningYaw=b.p.rb.rotation.eulerAngles.y;totalTurn=0;
                Time.captureDeltaTime=Step;return;
            }
            if(frames<300)return;
            float turningTravel=PhysicalHeadX()-turningStart;
            float expectedTurningTravel=Speed*(float)(Time.fixedTimeAsDouble-turningFixedStart);
            Debug.Log($"TURN FLIGHT: physics head travel={turningTravel:F6}, expected={expectedTurningTravel:F6}");
            if(Math.Abs(turningTravel-expectedTurningTravel)>.005f)throw new Exception("Head-pivot smooth turning discarded free-flight physics travel");
            if(Mathf.Abs(Mathf.DeltaAngle(turningYaw+totalTurn,b.p.rb.rotation.eulerAngles.y))>.01f)
                throw new Exception("Native interpolation discarded snap/smooth turn angles");
            Debug.Log($"CADENCE RESULT: samples={samples}, zeros={zeros}, doubles={doubles}, 1/0/2/1 sequences={sequences}; mean travel error A={errorA/samples:F6} B={errorB/samples:F6} nativeControl={errorControl/samples:F6}");
            Debug.Log($"CADENCE WRITE ISOLATION: same-position Rigidbody write mean travel error={errorSamePose/samples:F6}");
            if(zeros<20 || doubles<20 || sequences<10)throw new Exception("Controlled cadence did not reproduce 1/0/2/1");
            if(errorControl>=errorA*.2)throw new Exception("Native control did not interpolate in this harness");
            if(errorSamePose<errorA*.8)throw new Exception("Same-pose write control did not reproduce the interpolation conflict");
            if(errorB>=errorA*.2)throw new Exception("CADENCE REGRESSION: corrected player still pauses/catches up despite native interpolation");
            Debug.Log("MovementCadencePlayChecks PASS: actual rendered-loop movement smooth under 1/0/2/1 cadence."); Finish(0);
        } catch(Exception e) { Fail(e); }
    }
    static float PhysicalHeadX()
    {
        Vector3 offset=b.p.rb.rotation*(Quaternion.Inverse(b.root.transform.rotation)
            *(b.p.headCollider.transform.position-b.root.transform.position));
        return b.p.rb.position.x+offset.x;
    }
    static void Fail(Exception e) { Debug.LogException(e); Finish(1); }
    static void Finish(int code) { finished=true;Time.captureDeltaTime=0;SessionState.SetBool(Key,false);EditorApplication.Exit(code); }
}
public sealed class MovementCadencePlayProbe : MonoBehaviour
{
    void FixedUpdate()=>MovementCadencePlayChecks.FixedTick();
    void Update()=>MovementCadencePlayChecks.Move();
    void LateUpdate()=>MovementCadencePlayChecks.Sample();
}
