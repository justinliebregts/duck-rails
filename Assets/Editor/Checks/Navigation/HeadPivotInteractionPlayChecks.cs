using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Combined cases that separate tests of turning/carry and cadence cannot cover.
[InitializeOnLoad]
public static class HeadPivotInteractionPlayChecks
{
    const string Key="DuckZ.HeadPivotInteractionPlayChecks";
    const float Step=1f/72, Speed=4;
    static readonly float[] Intervals={Step,Step-.001f,Step+.001f,Step};
    static readonly string[] Cases={"free travel", "continuous turn", "continuous carry", "continuous hand push",
        "turn and carry", "reversing snap turns", "turn then coast", "zero carry"};
    static readonly string[] Variants={"VR irregular", "VR regular", "no-interpolation irregular", "VR 144 Hz display"};
    static readonly List<string> failures=new List<string>();
    static MovementTestRig rig;
    static int frame,steps,zeros,doubles,samples;
    static double error,physicsStartTime;
    static Vector3 physicalStart,headStart,lastHead;
    static float maxTurnOrbit,turnPhysicalDrift,movementPhysicalDrift,carryPhysicalDrift,maxVelocityError,totalTurn;
    static bool finished;
    static HeadPivotInteractionPlayChecks()
    {
        EditorApplication.playModeStateChanged+=state=> {
            if(state==PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(Key,false))Start();
        };
    }
    public static void Run()
    {
        if(!Application.isBatchMode)throw new InvalidOperationException("Isolated batch only");
        SessionState.SetBool(Key,true);EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        EditorApplication.EnterPlaymode();
    }
    static void Start()
    {
        try {
            frame=steps=0;finished=false;failures.Clear();
            Physics.simulationMode=SimulationMode.FixedUpdate;Physics.autoSyncTransforms=false;
            Time.fixedDeltaTime=Step;Time.maximumDeltaTime=2*Step;Time.captureDeltaTime=Step;
            // Captured time below controls simulated render cadence; wall-clock throttling
            // only slows this isolated batch run and is not part of its assertions.
            Application.targetFrameRate=-1;QualitySettings.vSyncCount=0;
            var services=new GameObject("Interaction services");services.SetActive(false);
            GR.Player.instance=services.AddComponent<GR.Player>();HelicopterManager.instance=null;
            rig=new MovementTestRig();
            rig.p.headCollider.enabled=true;rig.p.bodyCollider.enabled=true;
            var floor=new GameObject("Interaction hand floor");floor.layer=6;
            floor.AddComponent<BoxCollider>().size=new Vector3(100,.2f,100);floor.transform.position=Vector3.down*.1f;
            Physics.SyncTransforms();
            new GameObject("Interaction loop probe").AddComponent<HeadPivotInteractionPlayProbe>();
        } catch(Exception e) { Fail(e); }
    }
    public static void FixedTick()=>steps++;
    public static void Move()
    {
        if(finished)return;
        try {
            int phase=frame%180,scenario=(frame/180)%Cases.Length,variant=Variant(frame/(180*Cases.Length));var p=rig.p;
            if(phase==0) {
                p.rb.interpolation=variant<2 || variant==5?RigidbodyInterpolation.Interpolate:RigidbodyInterpolation.None;
                rig.root.transform.rotation=Quaternion.identity;p.rb.rotation=Quaternion.identity;
                p.trackingSpace.localPosition=Vector3.zero;
                p.headCollider.transform.localPosition=new Vector3(2,1.6f,1);
                p.leftHandTransform.localPosition=new Vector3(1.7f,scenario==3?.05f:1,1);
                p.rightHandTransform.localPosition=new Vector3(2.3f,1,1);
                p.Warp(Vector3.zero);p.rb.useGravity=false;
                p.rb.linearVelocity=scenario==3?Vector3.zero:Vector3.right*Speed;
                p.velocityLimit=float.MaxValue;p.minimumRaycastDistance=.05f;
                p.locomotionEnabledLayers=scenario==3?1<<6:0;
                error=0;samples=zeros=doubles=0;maxTurnOrbit=0;
                turnPhysicalDrift=movementPhysicalDrift=carryPhysicalDrift=maxVelocityError=totalTurn=0;
            }
            if(phase>30) {
                if(scenario==1 || scenario==4 || scenario==5 || (scenario==6 && phase<100)) {
                    float angle=scenario==5?(phase%8==1?45:phase%8==2?-45:0):1;
                    Vector3 before=p.headCollider.transform.position,physicsBefore=PhysicalHead();p.Turn(angle);totalTurn+=angle;
                    maxTurnOrbit=Mathf.Max(maxTurnOrbit,Vector3.Distance(before,p.headCollider.transform.position));
                    turnPhysicalDrift+=PhysicalHead().x-physicsBefore.x;
                }
                if(scenario==2 || scenario==4) {
                    float before=PhysicalHead().x;p.ApplyPassengerDisplacement(Vector3.forward*.01f);
                    carryPhysicalDrift+=PhysicalHead().x-before;
                    // Helicopter.MoveCabin flushes after moving cabin and passenger.
                    NewPlayer.SyncTransformsPreservingMovementPose();
                }
                if(scenario==3)p.leftHandTransform.localPosition-=Vector3.right*.004f;
                if(scenario==7)p.ApplyPassengerDisplacement(Vector3.zero);
            }
            Vector3 beforeMove=PhysicalHead();rig.move(Time.deltaTime);
            if(phase>30) {
                movementPhysicalDrift+=PhysicalHead().x-beforeMove.x;
                maxVelocityError=Mathf.Max(maxVelocityError,Vector3.Distance(p.rb.linearVelocity,scenario==3?Vector3.zero:Vector3.right*Speed));
            }
        } catch(Exception e) { Fail(e); }
    }
    public static void Sample()
    {
        if(finished)return;
        try {
            int phase=frame%180,scenario=(frame/180)%Cases.Length,variant=Variant(frame/(180*Cases.Length));
            bool irregular=variant==0 || variant==2 || variant==4;
            string label=Variants[frame/(180*Cases.Length)]+" / "+Cases[scenario];
            Vector3 head=rig.p.headCollider.transform.position;
            if(phase==30) { physicalStart=PhysicalHead();headStart=head;physicsStartTime=Time.fixedTimeAsDouble; }
            if(phase>30) {
                if(steps==0)zeros++;if(steps==2)doubles++;
                float expected=scenario==3?.004f:Speed*Time.deltaTime;
                error+=Math.Abs((head.x-lastHead.x)-expected);samples++;
                if(scenario==3 && !rig.p.leftHandColliding)Record(label+": hand fixture lost contact");
            }
            if(phase==179) {
                float expectedPhysics=scenario==3?samples*.004f:Speed*(float)(Time.fixedTimeAsDouble-physicsStartTime);
                Vector3 actual=PhysicalHead()-physicalStart;
                Debug.Log($"INTERACTION {label}: samples={samples} zero={zeros} two={doubles} mean displayed travel error={error/samples:F6} m; physical X={actual.x:F6} expected={expectedPhysics:F6}; physical Z={actual.z:F6}; immediate turn orbit={maxTurnOrbit:F6}; X drift during turn={turnPhysicalDrift:F6}, carry={carryPhysicalDrift:F6}, Movement={movementPhysicalDrift:F6}; max velocity error={maxVelocityError:F6}");
                if(irregular && (zeros<15 || doubles<15))Record(label+": cadence not exercised");
                if((variant==1 || variant==3) && (zeros!=0 || doubles!=0))Record(label+": regular cadence not exercised");
                if(variant==5 && (zeros<50 || doubles!=0))Record(label+": high display rate cadence not exercised");
                // The noninterpolated control flushes accepted Transform motion at physics.
                float checkedTravel=scenario==3 && rig.p.rb.interpolation==RigidbodyInterpolation.None?head.x-headStart.x:actual.x;
                if(Math.Abs(checkedTravel-expectedPhysics)>.005f)Record(label+": lost forward travel");
                if(scenario==3 && rig.p.rb.interpolation==RigidbodyInterpolation.Interpolate) {
                    float displayLag=actual.x-(head.x-headStart.x);
                    float oneStepTravel=.004f*Mathf.Max(1,Step/Time.deltaTime);
                    if(displayLag<-.001f || displayLag>oneStepTravel+.003f)
                        Record(label+": displayed hand travel exceeded the one-step smoothing delay");
                }
                // The noninterpolated control intentionally exposes cadence stepping.
                if((variant<2 || variant==5) && error/samples>.005)Record(label+": displayed travel error exceeds 5 mm mean");
                if(maxTurnOrbit>.001f)Record(label+": turning immediately orbited the headset");
                if((scenario==2 || scenario==4) && Math.Abs(actual.z-samples*.01f)>.005f)Record(label+": carry displacement not preserved");
                if(Mathf.Abs(Mathf.DeltaAngle(rig.p.rb.rotation.eulerAngles.y,totalTurn))>.01f)Record(label+": turn angle lost");
                if(maxVelocityError>.001f)Record(label+": velocity changed");
            }
            lastHead=head;steps=0;
            if(++frame>=180*Cases.Length*Variants.Length) {
                Debug.Log("HeadPivotInteractionPlayChecks: failures="+failures.Count);
                foreach(string failure in failures)Debug.LogError("INTERACTION FAILURE: "+failure);
                Finish(failures.Count==0?0:1);return;
            }
            int nextPhase=frame%180;
            int nextVariant=Variant(frame/(180*Cases.Length));
            Time.captureDeltaTime=nextVariant==5?Step*.5f:nextVariant==1 || nextVariant==3?Step:
                nextPhase==30?Step-Mathf.Repeat((float)(Time.timeAsDouble-Time.fixedTimeAsDouble),Step)+.0001f:Intervals[nextPhase%4];
        } catch(Exception e) { Fail(e); }
    }
    static int Variant(int index)=>index<2?index:index+2;
    static Vector3 PhysicalHead()=>rig.p.rb.position+rig.p.rb.rotation*(Quaternion.Inverse(rig.root.transform.rotation)
        *(rig.p.headCollider.transform.position-rig.root.transform.position));
    static void Record(string message) { if(!failures.Contains(message))failures.Add(message); }
    static void Fail(Exception e) { Debug.LogException(e);Finish(1); }
    static void Finish(int code) { finished=true;Time.captureDeltaTime=0;SessionState.SetBool(Key,false);EditorApplication.Exit(code); }
}
public sealed class HeadPivotInteractionPlayProbe : MonoBehaviour
{
    void FixedUpdate()=>HeadPivotInteractionPlayChecks.FixedTick();
    void Update()=>HeadPivotInteractionPlayChecks.Move();
    void LateUpdate()=>HeadPivotInteractionPlayChecks.Sample();
}
