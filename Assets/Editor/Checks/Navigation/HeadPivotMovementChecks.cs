using System;
using System.Reflection;
using UnityEngine;
using Object = UnityEngine.Object;

// Regression checks for the production VR head-pivot movement.
public static class HeadPivotMovementChecks
{
    const float Step=1f/72;
    const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    static int checks;
    static void Check(bool ok,string message) { checks++;if(!ok)throw new Exception(message); }
    static void Near(Vector3 a,Vector3 b,string message) => Check((a-b).sqrMagnitude<1e-6f,message+": "+a+" vs "+b);
    static void Set(NewPlayer p,string name,object value)=>typeof(NewPlayer).GetField(name,Private).SetValue(p,value);
    static T Get<T>(NewPlayer p,string name)=>(T)typeof(NewPlayer).GetField(name,Private).GetValue(p);
    static void Configure(MovementTestRig rig,Vector3 offset)
    {
        var p=rig.p; p.rb.interpolation=RigidbodyInterpolation.Interpolate;
        p.headCollider.transform.localPosition=offset+Vector3.up*1.6f;
        p.leftHandTransform.localPosition=offset+new Vector3(-.3f,1,0);
        p.rightHandTransform.localPosition=offset+new Vector3(.3f,1,0);
        p.InitializeValues();rig.move(Step);Set(p,"lastBodyMidpoint",p.bodyMidpoint.position);
    }
    public static void Run()
    {
        if(!Application.isBatchMode)throw new InvalidOperationException("Isolated batch only");
        checks=0;
        var mode=Physics.simulationMode;var sync=Physics.autoSyncTransforms;
        var oldPlayer=GR.Player.instance;var oldVR=NewPlayer.instance;var oldHeli=HelicopterManager.instance;
        var services=new GameObject("Head pivot services");services.SetActive(false);
        GR.Player.instance=services.AddComponent<GR.Player>();HelicopterManager.instance=null;
        Physics.simulationMode=SimulationMode.Script;Physics.autoSyncTransforms=false;
        try {
            foreach(var offset in new[]{Vector3.zero,new Vector3(2,0,-1),new Vector3(-3,0,2)})
            using(var rig=new MovementTestRig()) {
                Configure(rig,offset);var p=rig.p;
                Near(rig.root.transform.position,Vector3.zero,"Room-scale offset recentered the root");
                foreach(float angle in new[]{0f,45f,-45f,90f,-180f,360f}) {
                    Vector3 head=p.headCollider.transform.position;
                    p.Turn(angle);Near(p.headCollider.transform.position,head,"Snap turn orbited the head");
                    Near(p.rb.position,rig.root.transform.position,"Turn physics pose stale");
                    rig.move(Step);
                    Near(p.headCollider.transform.position,head,"Movement after turn displaced the head");
                    Near(Get<Vector3>(p,"denormalizedVelocityAverage"),Vector3.zero,"Pivot translation became launch velocity");
                }
                Vector3 smoothHead=p.headCollider.transform.position;
                for(int i=0;i<720;i++) { p.Turn(.5f);rig.move(Step); }
                Near(p.headCollider.transform.position,smoothHead,"Full smooth turn drifted around the old root");
                Near(Get<Vector3>(p,"denormalizedVelocityAverage"),Vector3.zero,"Smooth turning accumulated launch velocity");
                long allocatedBefore=GC.GetAllocatedBytesForCurrentThread();
                for(int i=0;i<1000;i++) { p.Turn(i%2==0?.5f:-.5f);rig.move(Step); }
                long allocated=GC.GetAllocatedBytesForCurrentThread()-allocatedBefore;
                Check(allocated==0,"Head-pivot movement allocated "+allocated+" bytes");
                Vector3 target=new Vector3(5,2,-7);
                p.Warp(target,Quaternion.Euler(0,110,0));
                Near(new Vector3(p.headCollider.transform.position.x,2,p.headCollider.transform.position.z),target,"Warp missed player destination with room-scale offset");
                Check(Mathf.Abs(Mathf.DeltaAngle(p.headCollider.transform.eulerAngles.y,110))<.01f,"Warp heading incorrect");
                var headBefore=p.headCollider.transform.position;var foot=p.PassengerFoot;
                p.rb.linearVelocity=new Vector3(1,2,3);
                Vector3 carry=new Vector3(.2f,.1f,-.3f);p.ApplyPassengerDisplacement(carry);
                Near(p.headCollider.transform.position,headBefore+carry,"Carry displaced head incorrectly");
                Near(p.PassengerFoot,foot+carry,"Carry displaced foot incorrectly");
                Near(p.rb.linearVelocity,new Vector3(1,2,3),"Carry changed player velocity");
            }
            HandPush(); BodySweep(); RespawnHands();
            Debug.Log("HeadPivotMovementChecks PASS: "+checks+" assertions; room-scale offsets, zero/snap/smooth turning, no pivot-launch impulses, warp destination/heading, carry, hand pushes and offset body sweep.");
        } finally {
            Object.DestroyImmediate(services);GR.Player.instance=oldPlayer;NewPlayer.instance=oldVR;HelicopterManager.instance=oldHeli;
            Physics.simulationMode=mode;Physics.autoSyncTransforms=sync;
        }
    }
    // One batch entry point for the respawn fix and adjacent movement regressions.
    public static void RunRespawnRegression()
    {
        PlayerMovementTimingChecks.Run();
        Run();
        HelicopterCarryChecks.Run();
        Debug.Log("Respawn regression suite: PASS");
    }

    static void RespawnHands()
    {
        // A flat roof isolates initial clearance and landing from the bus mesh.
        var roof = new GameObject("Respawn roof"); roof.layer = 6;
        roof.transform.position = new Vector3(0, 4.4f, 0);
        roof.AddComponent<BoxCollider>().size = new Vector3(20, .2f, 20);
        int cases = 0;
        Vector3 cleanPush = Vector3.zero;
        try {
            foreach (float headHeight in new[] { .7f, 1.6f })
            foreach (float rightHeight in new[] { 0f, .8f })
            foreach (var offset in new[] { Vector3.zero, new Vector3(2, 0, -1) })
            foreach (float yaw in new[] { 0f, 90f, 180f })
            foreach (bool staleContacts in new[] { false, true })
            using (var rig = new MovementTestRig()) {
                var p = rig.p;
                p.headCollider.transform.localPosition = offset + Vector3.up * headHeight;
                p.leftHandTransform.localPosition = offset + new Vector3(-.3f, 0, 0);
                p.rightHandTransform.localPosition = offset + new Vector3(.3f, rightHeight, 0);
                p.maxArmLength = 1.5f;
                p.minimumRaycastDistance = .05f;
                p.locomotionEnabledLayers = 1 << 6;
                p.velocityLimit = float.MaxValue; // Measure hand pushes without launching.
                p.wasLeftHandTouching = p.wasRightHandTouching = p.wasHeadTouching = staleContacts;
                p.rb.linearVelocity = new Vector3(3, -8, 2);
                p.Warp(new Vector3(0, 4.8f, 0), Quaternion.Euler(0, yaw, 0));
                Check(!p.IsGrounded(), "Respawn retained stale grounding");
                Near(p.rb.linearVelocity, Vector3.zero, "Respawn retained falling velocity");
                Check(p.lastLeftHandPosition.y - p.minimumRaycastDistance > 4.5f
                    && p.lastRightHandPosition.y - p.minimumRaycastDistance > 4.5f,
                    "Low hand did not clear the roof at spawn");
                p.rb.useGravity = true;
                bool landed = false;
                for (int frame = 0; frame < 180; frame++) {
                    Physics.SyncTransforms(); rig.move(Step);
                    Check(p.lastLeftHandPosition.y > 4.5f && p.lastRightHandPosition.y > 4.5f,
                        "Hand anchor crossed into the roof during landing");
                    if (p.leftHandColliding) { landed = true; break; }
                    Physics.SyncTransforms(); Physics.Simulate(Step);
                }
                Check(landed, "Floor-level hand never landed on the roof");
                Vector3 before = p.headCollider.transform.position;
                // Pull the planted left hand inward. Moving it farther out while
                // already reach-limited lifts its clamped anchor off the roof.
                Vector3 pushDirection = p.transform.right;
                p.leftHandTransform.position += pushDirection * .1f;
                p.rightHandTransform.position += pushDirection * .1f;
                rig.move(Step);
                Vector3 displacement = p.headCollider.transform.position - before;
                // Arm-reach clamping can reduce a tracked 10 cm push. Require movement
                // and identical behavior to the same pose without pre-death contacts.
                Check(Vector3.Dot(displacement, -pushDirection) > .01f,
                    "Player could not push away after respawning with low hands: case=" + cases
                    + ", head=" + headHeight + ", right=" + rightHeight + ", offset=" + offset + ", yaw=" + yaw
                    + ", displacement=" + displacement.ToString("F4")
                    + ", contacts=" + p.leftHandColliding + "/" + p.rightHandColliding);
                if (staleContacts) Near(displacement, cleanPush, "Old contacts changed post-respawn hand movement");
                else cleanPush = displacement;
                cases++;
            }
            Debug.Log("RespawnHands PASS: " + cases + " standing/crouched, one/both low hands, room-scale offset and yaw cases; landing and hand-push recovery");
        } finally { Object.DestroyImmediate(roof); }
    }
    static void HandPush()
    {
        var floor=new GameObject("Offset hand floor");floor.layer=6;
        floor.AddComponent<BoxCollider>().size=new Vector3(100,.2f,100);floor.transform.position=Vector3.down*.1f;
        try {
            foreach(var offset in new[]{Vector3.zero,new Vector3(2,0,1)})
            using(var rig=new MovementTestRig()) {
                Configure(rig,offset);var p=rig.p;p.velocityLimit=float.MaxValue;p.locomotionEnabledLayers=1<<6;
                p.minimumRaycastDistance=.05f;
                p.leftHandTransform.localPosition=offset+new Vector3(-.3f,.05f,0);
                Physics.SyncTransforms();rig.move(Step);
                Vector3 origin=p.headCollider.transform.position;
                p.leftHandTransform.localPosition-=Vector3.right*.1f;rig.move(Step);
                Check(p.leftHandColliding,"No contact in offset hand-push fixture");
                Near(p.headCollider.transform.position,origin+Vector3.right*.1f,"Room offset changed hand displacement");
                Physics.SyncTransforms();Physics.Simulate(Step);
                Near(p.headCollider.transform.position,origin+Vector3.right*.1f,"Offset hand push did not persist");
            }
        } finally { Object.DestroyImmediate(floor); }
    }
    static void BodySweep()
    {
        var wall=new GameObject("Room-scale crossing wall");wall.layer=7;
        wall.AddComponent<BoxCollider>().size=new Vector3(.2f,20,20);
        try {
            foreach(float offset in new[]{0f,2f})
            using(var rig=new MovementTestRig()) {
                Configure(rig,new Vector3(offset,0,0));var p=rig.p;
                p.Warp(new Vector3(-1,0,0));rig.move(Step);
                Set(p,"envMask",(LayerMask)(1<<7));Set(p,"lastBodyMidpoint",p.bodyMidpoint.position);
                Physics.SyncTransforms();
                p.headCollider.transform.localPosition+=Vector3.right*2;
                rig.move(Step);
                Check(p.headCollider.transform.position.x<-.1f,"Body sweep placed an offset headset through the wall");
                Check(p.bodyMidpoint.position.x<-.1f,"Body sweep left midpoint through the wall");
            }
        } finally { Object.DestroyImmediate(wall); }
    }
}
