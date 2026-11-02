using System;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Executes the actual production Movement method
// in Unity's player loop, with gravity, launch thresholds and physical colliders.
// Editor-only CSV measures poses directly; no runtime recorder or instrumentation.
[InitializeOnLoad]
public static class HeadPivotContactGravityPlayChecks
{
    const string Key="DuckZ.HeadPivotContactGravityPlayChecks";
    const string Output="C:/duckz/.utmp/timestep-audit/contact-gravity.csv";
    const float Step=1f/72;
    const int Frames=480;
    const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    static readonly string[] Variants={"VR", "no-interpolation", "no-gravity", "no-body", "no-launch"};
    static readonly string[] Cases={"stationary", "slow-lowering", "threshold-lowering", "deep-lowering", "hop-release", "two-hand-lowering", "body-sweep", "head-wall", "warp-after-contact"};
    static readonly float[] Intervals={Step,Step-.001f,Step+.001f,Step};
    static readonly StringBuilder csv=new StringBuilder(4*1024*1024);
    static MovementTestRig rig;
    static Vector3 rootStart, rbStart, velocityStart;
    static Vector3 rootEnd, rbEnd, velocityEnd;
    static int handContacts;
    static GameObject wall;
    static int frame,steps,contacts,assertions,zeroSteps,twoSteps,launchFrames,handFrames;
    static float minSeparation,impulse;
    static Vector3 contactNormal,lastHead,input;
    static bool finished;
    static int VariantIndex=>frame/(Frames*Cases.Length*2);
    static int Variant=>VariantIndex==0?0:VariantIndex+1;
    static int Scenario=>(frame/Frames)%Cases.Length;
    static bool Irregular=>(frame/(Frames*Cases.Length))%2==1;
    static void Set(string field,object value)=>typeof(NewPlayer).GetField(field,Private).SetValue(rig.p,value);
    static void Check(bool ok,string message) { assertions++;if(!ok)throw new Exception(message); }
    static HeadPivotContactGravityPlayChecks()
    {
        EditorApplication.playModeStateChanged+=s=> {
            if(s==PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(Key,false))Start();
        };
    }
    public static void Run()
    {
        if(!Application.isBatchMode)throw new InvalidOperationException("Isolated batch only");
        SessionState.SetBool(Key,true);
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        EditorApplication.EnterPlaymode();
    }
    static void Start()
    {
        try {
            frame=steps=assertions=0;finished=false;csv.Clear();
            csv.AppendLine("variant,cadence,case,phase,dt,steps,handY,handZ,headY,headZ,dy,dz,rootStartY,rootStartZ,rbStartY,rbStartZ,rootEndY,rootEndZ,rbEndY,rbEndZ,velocityStartY,velocityStartZ,velocityEndY,velocityEndZ,acceptedCorrectionY,acceptedCorrectionZ,handContacts,physicalContacts,minSeparation,normalY,normalZ,impulse");
            Physics.simulationMode=SimulationMode.FixedUpdate;Physics.autoSyncTransforms=false;
            Time.fixedDeltaTime=Step;Time.maximumDeltaTime=2*Step;Time.captureDeltaTime=Step;
            Application.targetFrameRate=-1;QualitySettings.vSyncCount=0;
            var services=new GameObject("Contact investigation services");services.SetActive(false);
            GR.Player.instance=services.AddComponent<GR.Player>();HelicopterManager.instance=null;
            rig=new MovementTestRig();
            var p=rig.p;
            p.rb.collisionDetectionMode=CollisionDetectionMode.ContinuousSpeculative;
            p.headCollider.enabled=true;p.bodyCollider.enabled=true;
            // Authored DuckPlayer capsule center and separate midpoint, rather than
            // the deliberately simplified geometry used by the pure solver checks.
            p.bodyCollider.center=new Vector3(0,-.225f,0);
            var midpoint=new GameObject("Authored body midpoint").transform;
            midpoint.SetParent(p.bodyCollider.transform,false);midpoint.localPosition=p.bodyCollider.center;
            p.bodyMidpoint=midpoint;Set("headBodyOffset",new Vector3(0,0,-.06f));
            p.maxArmLength=1.5f;p.minimumRaycastDistance=.05f;p.defaultSlideFactor=.03f;p.defaultPrecision=.995f;
            p.leftHandOffset=p.rightHandOffset=new Vector3(0,-.025f,0);
            p.locomotionEnabledLayers=1<<6;
            rig.root.AddComponent<HeadPivotContactCollisionProbe>();
            var floor=new GameObject("Flat environment floor");floor.layer=6;
            floor.AddComponent<BoxCollider>().size=new Vector3(100,.2f,100);floor.transform.position=Vector3.down*.1f;
            wall=new GameObject("Tracking motion wall");wall.AddComponent<BoxCollider>().size=new Vector3(.2f,20,20);wall.SetActive(false);
            Physics.SyncTransforms();
            new GameObject("Contact investigation loop").AddComponent<HeadPivotContactGravityPlayProbe>();
        } catch(Exception e) { Fail(e); }
    }
    static void ResetCase()
    {
        var p=rig.p;
        p.rb.interpolation=Variant==1 || Variant==2?RigidbodyInterpolation.None:RigidbodyInterpolation.Interpolate;
        p.trackingSpace.localPosition=Vector3.zero;
        p.headCollider.transform.localPosition=new Vector3(0,1.2f,0);
        p.leftHandTransform.localPosition=new Vector3(-.3f,.075f,0);
        p.rightHandTransform.localPosition=new Vector3(.3f,1,0);
        p.Warp(Scenario==6 || Scenario==7?Vector3.left:Vector3.zero,Quaternion.identity);
        wall.SetActive(Scenario==6 || Scenario==7);wall.layer=Scenario==6?7:6;
        Set("envMask",(LayerMask)(Scenario==6?1<<7:Scenario==7?0:1<<6));
        p.locomotionEnabledLayers=(1<<6)|(Scenario==6?1<<7:0);
        p.rb.useGravity=Variant!=3;p.bodyCollider.enabled=Variant!=4;
        p.rb.linearVelocity=Vector3.zero;p.velocityLimit=.4f;p.maxJumpSpeed=6.5f;p.jumpMultiplier=1.1f;
        if(Variant==5)p.velocityLimit=float.MaxValue;
        p.bodyCollider.transform.position=p.headCollider.transform.position+new Vector3(0,0,-.06f);
        p.InitializeValues();Set("lastBodyMidpoint",p.bodyMidpoint.position);
        lastHead=p.headCollider.transform.position;Physics.SyncTransforms();
        contacts=0;minSeparation=impulse=0;contactNormal=Vector3.zero;
        zeroSteps=twoSteps=launchFrames=handFrames=0;
    }
    public static void FixedTick()=>steps++;
    public static void Collision(Collision c)
    {
        for(int i=0;i<c.contactCount;i++) {
            var point=c.GetContact(i);contacts++;
            minSeparation=Mathf.Min(minSeparation,point.separation);contactNormal+=point.normal;
        }
        impulse+=c.impulse.magnitude;
    }
    public static void Move()
    {
        if(finished)return;
        try {
            int phase=frame%Frames;if(phase==0)ResetCase();
            var p=rig.p;
            float y=.075f,z=0;
            if(phase>=60) {
                int k=(phase-60)%180;
                if(Scenario==1)y+=Mathf.Min(k,180-k)*.003f;
                if(Scenario==2 || Scenario==5)y+=Mathf.Min(k,180-k)*.004f;
                if(Scenario==3)y+=Mathf.Min(k,180-k)*.009f;
                if(Scenario==4) {
                    int hop=(phase-60)%100;
                    if(hop<12) { y-=hop*.025f;z-=hop*.025f; }
                    else if(hop<65) { y+=.3f;z=-.275f; }
                    else { float t=(hop-65)/34f;y+=.3f*(1-t);z=-.275f*(1-t); }
                }
            }
            input=new Vector3(-.3f,y,z);p.leftHandTransform.localPosition=input;
            if(Scenario==5)p.rightHandTransform.localPosition=new Vector3(.3f,y,z);
            if(phase==120 && (Scenario==6 || Scenario==7))p.headCollider.transform.localPosition+=Vector3.right*2;
            if(phase==120 && Scenario==8)p.Warp(new Vector3(2,2,1),Quaternion.Euler(0,110,0));
            // Sound/haptic services are absent in this isolated geometry fixture.
            Set("leftHandNotCollidingTime",-100f);Set("rightHandNotCollidingTime",-100f);
            rootStart=p.transform.position;rbStart=p.rb.position;velocityStart=p.rb.linearVelocity;
            long beforeMovement=GC.GetAllocatedBytesForCurrentThread();
            rig.move(Time.deltaTime);
            long movementBytes=GC.GetAllocatedBytesForCurrentThread()-beforeMovement;
            if(phase>100 && (Scenario==0 || Scenario==1 || Scenario==2 || Scenario==5))
                Check(movementBytes==0,"Warmed contact movement allocated managed memory");
            rootEnd=p.transform.position;rbEnd=p.rb.position;velocityEnd=p.rb.linearVelocity;
            handContacts=(p.leftHandColliding?1:0)|(p.rightHandColliding?2:0);
        } catch(Exception e) { Fail(e); }
    }
    static void Number(float x) { csv.Append(',').Append(x.ToString("R",CultureInfo.InvariantCulture)); }
    public static void Sample()
    {
        if(finished)return;
        try {
            var p=rig.p;int phase=frame%Frames;
            Vector3 head=p.headCollider.transform.position;
            if(Variant==0 && Irregular && Scenario==1 && phase==61) {
                // The first 3 mm lowering input follows a stationary planted hand.
                // Pose-publication drift used to lose contact on this zero-step frame.
                Check(steps==0,"Contact precision fixture missed the zero-step boundary");
                Check((handContacts&1)!=0,"VR lost planted-hand contact on the first 3 mm lowering input");
                Check(Mathf.Abs((rbEnd-rbStart).y+.003f)<.00001f,"VR skipped the first 3 mm hand correction");
            }
            if(phase==120 && Variant!=1 && (Scenario==6 || Scenario==7)) {
                Check(head.x<-.1f,"Smoothed view crossed a blocking wall");
                Vector3 physicalHead=p.rb.position+p.rb.rotation*(Quaternion.Inverse(p.transform.rotation)*(head-p.transform.position));
                // The unchanged noninterpolated control flushes Transform writes at
                // the next physics boundary; B's explicit physical pose must be safe now.
                if(p.rb.interpolation==RigidbodyInterpolation.Interpolate)
                    Check(physicalHead.x<-.1f,"Physical head crossed a blocking wall");
            }
            if(phase==120 && Scenario==8) {
                Check(Vector2.Distance(new Vector2(head.x,head.z),new Vector2(2,1))<.001f,"Contact smoothing undid a warp");
                Check(p.rb.linearVelocity.sqrMagnitude<.0001f,"Contact smoothing restored pre-warp momentum");
            }
            Check(float.IsFinite(head.y)&&float.IsFinite(head.z),"Non-finite head pose");
            Check(p.rb.linearVelocity.magnitude<30,"Unbounded velocity");
            if(phase>60) {
                if(steps==0)zeroSteps++;if(steps==2)twoSteps++;
                if(handContacts!=0)handFrames++;
                if(handContacts!=0 && velocityEnd.magnitude>.01f)launchFrames++;
            }
            csv.Append(Variants[VariantIndex]).Append(',').Append(Irregular?"irregular":"regular").Append(',').Append(Cases[Scenario]).Append(',').Append(phase);
            Number(Time.deltaTime);Number(steps);Number(input.y);Number(input.z);Number(head.y);Number(head.z);
            Number(head.y-lastHead.y);Number(head.z-lastHead.z);
            Number(rootStart.y);Number(rootStart.z);Number(rbStart.y);Number(rbStart.z);
            Number(rootEnd.y);Number(rootEnd.z);Number(rbEnd.y);Number(rbEnd.z);
            Number(velocityStart.y);Number(velocityStart.z);Number(velocityEnd.y);Number(velocityEnd.z);
            Number((rbEnd-rbStart).y);Number((rbEnd-rbStart).z);
            Number(handContacts);Number(contacts);Number(minSeparation);Number(contactNormal.y);Number(contactNormal.z);Number(impulse);csv.AppendLine();
            lastHead=head;steps=contacts=0;minSeparation=impulse=0;contactNormal=Vector3.zero;
            if(phase==Frames-1) {
                Check(handFrames>0,"Fixture never established hand contact");
                if(Irregular)Check(zeroSteps>30 && twoSteps>30,"Irregular cadence not exercised");
                else Check(zeroSteps==0 && twoSteps==0,"Regular cadence not exercised");
                if(Scenario==4 && Variant!=5)Check(launchFrames>0,"Hop fixture never launched");
                Debug.Log($"CONTACT REGRESSION {Variants[VariantIndex]} / {(Irregular?"irregular":"regular")} / {Cases[Scenario]} complete; zero={zeroSteps} two={twoSteps} hand={handFrames} launch={launchFrames}");
            }
            if(++frame>=Frames*Variants.Length*Cases.Length*2) {
                Directory.CreateDirectory(Path.GetDirectoryName(Output));File.WriteAllText(Output,csv.ToString());
                Debug.Log($"HeadPivotContactGravityPlayChecks completed: {frame} frames, {assertions} assertions. Pose comparisons are in: {Output}");
                Finish(0);return;
            }
            int nextPhase=frame%Frames;
            Time.captureDeltaTime=!Irregular?Step:nextPhase==30?
                Step-Mathf.Repeat((float)(Time.timeAsDouble-Time.fixedTimeAsDouble),Step)+.0001f:Intervals[frame%4];
        } catch(Exception e) { Fail(e); }
    }
    static void Fail(Exception e) { Debug.LogException(e);Finish(1); }
    static void Finish(int code)
    {
        finished=true;Time.captureDeltaTime=0;SessionState.SetBool(Key,false);
        EditorApplication.Exit(code);
    }
}
public sealed class HeadPivotContactGravityPlayProbe : MonoBehaviour
{
    void FixedUpdate()=>HeadPivotContactGravityPlayChecks.FixedTick();
    void Update()=>HeadPivotContactGravityPlayChecks.Move();
    void LateUpdate()=>HeadPivotContactGravityPlayChecks.Sample();
}
public sealed class HeadPivotContactCollisionProbe : MonoBehaviour
{
    void OnCollisionEnter(Collision c)=>HeadPivotContactGravityPlayChecks.Collision(c);
    void OnCollisionStay(Collision c)=>HeadPivotContactGravityPlayChecks.Collision(c);
}
