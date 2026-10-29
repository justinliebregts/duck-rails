using System;
using System.Reflection;
using Photon.Pun;
using Game.Animation;
using UnityEngine;
using UnityEditor.SceneManagement;

public static class DuckSidewaysClimbChecks
{
    const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
    static object Call(DuckCompanion d, string n, params object[] a) => typeof(DuckCompanion).GetMethod(n, Flags).Invoke(d, a);
    static void Set(DuckCompanion d, string n, object v) => typeof(DuckCompanion).GetField(n, Flags).SetValue(d, v);
    static object Get(DuckCompanion d, string n) => typeof(DuckCompanion).GetField(n, Flags).GetValue(d);
    static void Check(bool value, string message) { if (!value) throw new Exception(message); Debug.Log("PASS: " + message); }
    public static void Run()
    {
        Check(Application.isBatchMode, "isolated fixture");
        EditorSceneManager.OpenScene("Assets/FenceLevel.unity");
        const BindingFlags Static = BindingFlags.Static | BindingFlags.NonPublic;
        typeof(PhotonNetwork).GetField("offlineMode", Static).SetValue(null, true);
        typeof(PhotonNetwork).GetField("offlineModeRoom", Static).SetValue(null,
            new Photon.Realtime.Room("Sideways fixture", new Photon.Realtime.RoomOptions(), true));
        typeof(PhotonNetwork).GetField("frametime", Static).SetValue(null, 100d);
        var go = new GameObject("Sideways climber");
        go.AddComponent<PhotonView>();
        var duck = go.AddComponent<DuckCompanion>();
        Call(duck, "Awake");
        Set(duck, "environment", (LayerMask)(1 << 6));
        go.GetComponent<CharacterController>().enabled = true;
        Physics.SyncTransforms();
        int candidates = 0;
        foreach (var collider in UnityEngine.Object.FindObjectsByType<MeshCollider>(FindObjectsSortMode.None))
        {
            if (collider.sharedMesh == null || collider.name.IndexOf("security", StringComparison.OrdinalIgnoreCase) < 0) continue;
            var mesh = collider.sharedMesh; var vertices = mesh.vertices; var triangles = mesh.triangles;
            for (int tri = 0; tri < triangles.Length; tri += 3)
            {
                Vector3 a = collider.transform.TransformPoint(vertices[triangles[tri]]);
                Vector3 b = collider.transform.TransformPoint(vertices[triangles[tri + 1]]);
                Vector3 c = collider.transform.TransformPoint(vertices[triangles[tri + 2]]);
                Vector3 normal = Vector3.Cross(b - a, c - a).normalized;
                if (Mathf.Abs(normal.y) > .15f) continue;
                Vector3 midpoint = (a + b + c) / 3;
                for (int side = -1; side <= 1; side += 2)
                {
                    Vector3 n = normal * side;
                    if (!Physics.Raycast(midpoint + n * .4f + Vector3.up, Vector3.down, out var floor, 10, 1 << 6)
                        || floor.normal.y < .7f) continue;
                    Vector3 start = floor.point + Vector3.up * .04f;
                    if (!(bool)Call(duck, "ClearBody", start)) continue;
                    // Offset the goal along the wall: sideways movement must help
                    // reach a genuine exit rather than merely shuffle in place.
                    Vector3 goal = start - n * 3 + Vector3.Cross(Vector3.up, n) * 2;
                    if (!Physics.Raycast(goal + Vector3.up * 2, Vector3.down, out var endFloor, 5, 1 << 6)) continue;
                    goal = endFloor.point + Vector3.up * .04f;
                    float eyeHeight = (float)Get(duck, "eyeHeight");
                    if (!Physics.Raycast(start + Vector3.up * eyeHeight, -n, out var contact, .85f, 1 << 6)
                        || contact.collider != collider) continue;
                    go.transform.position = start;
                    Physics.SyncTransforms();
                    Call(duck, "ResetNavigationAfterAuthorityChange");
                    Set(duck, "fenceAttemptUntil", 0d);
                    Set(duck, "failedFenceUntil", 0d);
                    candidates++;
                    double beforeAttempt = PhotonNetwork.Time;
                    if (!(bool)Call(duck, "TrySidewaysClimb", -n, goal)) continue;
                    Vector3 end = (Vector3)Get(duck, "sidewaysEnd");
                    Check(Vector3.Distance(start, end) <= 2.001f, "industrial sideways climb bounded to two metres");
                    double deadline = (double)Get(duck, "fenceAttemptUntil");
                    Check(deadline >= beforeAttempt + 5 && deadline <= PhotonNetwork.Time + 5,
                        "sideways fence climb shares original five-second budget");
                    var phaseField = typeof(DuckCompanion).GetField("killerPhase", Flags);
                    phaseField.SetValue(duck, Enum.Parse(phaseField.FieldType, "Approaching"));
                    double chaseDeadline = PhotonNetwork.Time + 10;
                    Set(duck, "killerDeadline", chaseDeadline);
                    var held = new GameObject("Held weapon fixture");
                    held.transform.SetParent(duck.RightHand, false);
                    Set(duck, "killerWeapon", held);
                    var hitbox = held.AddComponent<BoxCollider>(); hitbox.isTrigger = true;
                    Set(duck, "killerHitbox", hitbox);
                    Call(duck, "Pose", (object)null);
                    Vector3 carriedHand = duck.RightHand.position;
                    Call(duck, "PoseSidewaysHand", duck.RightHand, 1f, .5f);
                    Check(duck.RightHand.position == carriedHand, "armed right hand cannot plant on sideways wall");
                    Vector3 firstFreeHand = duck.LeftHand.position;
                    for (int frame = 0; frame < 200 && (bool)Get(duck, "sidewaysClimbing"); frame++)
                    {
                        Call(duck, "AdvanceTraversal", .025f);
                        Call(duck, "Pose", (object)null);
                        Call(duck, "LateUpdate");
                    }
                    Check(Vector3.Distance(firstFreeHand, duck.LeftHand.position) > .1f,
                        "free left hand moves during armed sideways climb");
                    Check(Get(duck, "killerWeapon") == held && held.transform.parent == duck.RightHand
                        && !(bool)Get(duck, "killerWeaponDropped") && (double)Get(duck, "killerDeadline") == chaseDeadline,
                        "sideways climb keeps held weapon and original chase deadline");
                    var fingers = (FingerCurl)Get(duck, "expressiveFingers");
                    var packet = PosePacket.Capture(duck.Head, duck.LeftHand, duck.RightHand, fingers);
                    var received = (PosePacket)PosePacket.Deserialize(PosePacket.Serialize(packet));
                    Check(Vector3.Distance(received.LHandPos, duck.LeftHand.position) < .002f
                        && Vector3.Distance(received.RHandPos, duck.RightHand.position) < .002f
                        && received.Fingers.rightIndex > .8f, "network pose retains climbing hand and closed weapon grip");
                    var peerObject = new GameObject("Armed remote peer");
                    peerObject.AddComponent<PhotonView>();
                    var peer = peerObject.AddComponent<DuckCompanion>(); Call(peer, "Awake");
                    var armedStream = new PhotonStream(true, null);
                    typeof(PhotonStream).GetMethod("SetWriteStream", Flags).Invoke(armedStream,
                        new object[] { new System.Collections.Generic.List<object>(), 0 });
                    Call(duck, "SerializeKiller", armedStream);
                    Call(peer, "SerializeKiller", new PhotonStream(false, armedStream.ToArray()));
                    Check(peer.KillerSequenceActive && (double)Get(peer, "killerDeadline") == chaseDeadline
                        && !(bool)Get(peer, "killerWeaponDropped"), "remote peer receives armed chase without a weapon drop");
                    UnityEngine.Object.DestroyImmediate(peerObject);
                    Debug.Log($"SIDEWAYS motor start={start} end={end} actual={go.transform.position} state={duck.State} event={duck.LastEvent}");
                    Check(Vector3.Distance(go.transform.position, end) < .1f, "actual industrial collider permits the planned sideways motor movement");
                    Check(duck.State == DuckCompanion.Activity.Jumping || duck.State == DuckCompanion.Activity.Climbing,
                        "sideways movement connects to revalidated onward traversal");
                    Check((double)Get(duck, "fenceAttemptUntil") == deadline, "onward traversal does not restart attempt budget");
                    Vector3 landing = (Vector3)Get(duck, "sidewaysLanding");
                    for (int frame = 0; frame < 400
                        && (duck.State == DuckCompanion.Activity.Jumping || duck.State == DuckCompanion.Activity.Climbing); frame++)
                        Call(duck, "AdvanceTraversal", .025f);
                    Check(Vector3.Distance(go.transform.position, landing) < .3f,
                        "real industrial sideways move completes its onward exit");
                    Debug.Log($"INDUSTRIAL SIDEWAYS start={start} end={end} goal={goal} candidates={candidates}");
                    // A takeover abandons local mid-air plans; the replicated
                    // attempt deadline still limits retries under the new master.
                    Set(duck, "sidewaysClimbing", true);
                    Call(duck, "ResetNavigationAfterAuthorityChange");
                    Check(!(bool)Get(duck, "sidewaysClimbing") && (double)Get(duck, "fenceAttemptUntil") == deadline,
                        "takeover clears local sideways motion while retaining attempt deadline");
                    Set(duck, "fenceAttemptUntil", PhotonNetwork.Time - 1);
                    go.transform.position = start;
                    Check(!(bool)Call(duck, "TrySidewaysClimb", -n, goal), "expired attempt cannot launch another sideways climb");
                    Set(duck, "failedFenceUntil", PhotonNetwork.Time + 30); Set(duck, "failedFenceCrossing", start);
                    Check(!(bool)Call(duck, "TrySidewaysClimb", -n, goal), "local thirty-second fence avoidance blocks sideways retries");
                    Debug.Log("ALL SIDEWAYS CLIMB CHECKS PASSED");
                    return;
                }
            }
        }
        throw new Exception("No viable industrial sideways climb found; candidates=" + candidates);
    }
}
