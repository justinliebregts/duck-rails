using System;
using Game.Animation;
using UnityEditor;
using UnityEngine;

// Exercises the real prefab chains and network pose codec without entering a room.
public static class PlayerArmLengthChecks
{
    const float Upper = 0.2878544f;
    const float Forearm = 0.2637434f;
    const float LengthTolerance = 0.00002f;
    static readonly string[] Players = {
        "Assets/Prefabs/Player/DuckPlayer.prefab",
        "Assets/Prefabs/Player/DesktopPlayer.prefab",
        "Assets/Prefabs/Photon/PhotonPlayer.prefab"
    };

    // Batch entry point also used by the command-line validation run.
    public static void RunAll()
    {
        int failures = 0;
        foreach (Action check in new Action[] { Run, PlayerPalmAlignmentChecks.Run,
            ControllerPalmCalibrationChecks.Run, ControllerPalmInteractionChecks.Run,
            TrackedHandInputChecks.Run, PlayerRagdollLengthChecks.Run, RagdollChecks.Run,
            PlayerArmGeometryChecks.Run })
        {
            try { check(); }
            catch (Exception e) { failures++; Debug.LogError(check.Method.DeclaringType.Name + ": " + e); }
        }
        if (failures != 0) throw new Exception("Stretch arm validation failures: " + failures);
        Debug.Log("STRETCH ARM VALIDATION PASS");
    }

    [MenuItem("Tools/DuckZ/Validation/Rendering/Validate Player Arm Lengths")]
    public static void Run()
    {
        foreach (string path in Players)
        {
            GameObject root = LoadRig(path);
            try
            {
                var body = root.GetComponentInChildren<RemotePlayerBody>(true);
                Require(body != null, path + " body");
                foreach (TwoBoneLimb arm in new[] { body.LeftArm, body.RightArm })
                {
                    Transform upper = Bone(arm, "upperBone");
                    Transform mid = Bone(arm, "midBone");
                    Transform end = arm.EndBone;
                    Near(Vector3.Distance(upper.position, mid.position), Upper, path + " upper arm");
                    Near(Vector3.Distance(mid.position, end.position), Forearm, path + " forearm");
                    Vector3 midOffset = mid.localPosition, endOffset = end.localPosition;
                    Vector3 handScale = end.lossyScale;
                    arm.CaptureRest();
                    arm.CaptureRest();
                    Near(arm.TotalReach, Upper + Forearm, path + " repeat initialization");
                    Require(mid.localPosition == midOffset && end.localPosition == endOffset,
                        path + " initialization must not stretch bones");

                    Vector3 shoulder = upper.position;
                    Vector3 pole = shoulder + new Vector3(0.7f, -0.6f, -1f);
                    VerifyStretch(arm, upper, mid, endOffset, handScale, path);
                    VerifySingularTargets(arm, upper, mid, endOffset, handScale, path);
                    foreach (Vector3 offset in new[] {
                        new Vector3(0, 0, 0.76f), // Beyond the previous 1.3x cap, within 1.4x reach.
                        new Vector3(0.1f, -0.45f, 0.1f),
                        new Vector3(0.1f, 0.2f, 0.15f),
                        new Vector3(0.45f, 0.1f, 0.1f)
                    })
                    {
                        Vector3 target = shoulder + offset;
                        arm.Solve(target, Quaternion.identity, pole, RemotePlayerBody.PlayerArmMaxStretch);
                        Require(Vector3.Distance(end.position, target) < 0.0002f,
                            path + " reachable target " + offset);
                    }
                    arm.Solve(shoulder + Vector3.forward * 10f, Quaternion.identity, pole, RemotePlayerBody.PlayerArmMaxStretch);
                    Require(Vector3.Distance(shoulder, end.position) <= (Upper + Forearm) * 1.4f + LengthTolerance,
                        path + " far controller must stay bounded");
                    Require(mid.localPosition == midOffset
                        && Vector3.Distance(end.lossyScale, handScale) < LengthTolerance,
                        path + " solving preserves elbow offset and hand size");
                }
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        VerifyUnchangedNPCs();
        VerifyNetworkPoses(Players[0]);
        VerifyNetworkPoses(Players[1]);
        Debug.Log("PlayerArmLengthChecks PASS: VR/desktop/Photon 55.160 cm rest arms, 1.4x visual stretch; reachable and bounded targets; unchanged enemies/companion; network codec pose agreement.");
    }

    static void VerifyStretch(TwoBoneLimb arm, Transform upper, Transform mid, Vector3 wristOffset, Vector3 handScale, string path)
    {
        Vector3 shoulder = upper.position;
        float reach = Upper + Forearm;
        Vector3 upperScale = upper.localScale;
        Vector3 pole = shoulder + new Vector3(.7f, -.6f, -1f);
        int colliders = upper.root.GetComponentsInChildren<Collider>(true).Length;
        int rigidbodies = upper.root.GetComponentsInChildren<Rigidbody>(true).Length;
        foreach (var direction in new[] { Vector3.forward, Vector3.up, Vector3.down, Vector3.left, Vector3.right, new Vector3(.2f, .4f, -.7f).normalized })
        foreach (float ratio in new[] { 1f, 1.05f, 1.1f, 1.2f, 1.25f, 1.3f, 1.35f, 1.4f, 2f, .75f, 1.4f, .5f })
        foreach (float roll in new[] { 0f, 90f, 180f, 270f })
        {
            Vector3 target = shoulder + direction * (reach * ratio);
            arm.Solve(target, Quaternion.Euler(25f, 40f, roll), pole, RemotePlayerBody.PlayerArmMaxStretch);
            Vector3 expected = shoulder + direction * (reach * Mathf.Min(ratio, 1.4f));
            Require(Vector3.Distance(arm.EndPos, expected) < .0011f, path + " endpoint at ratio " + ratio);
            Require(Vector3.Distance(mid.TransformPoint(wristOffset), arm.EndPos) < .0011f, path + " connected forearm/wrist " + ratio);
            Require(Vector3.Distance(arm.EndBone.lossyScale, handScale) < LengthTolerance, path + " hand scale at roll " + roll);
            var matrix = arm.EndBone.localToWorldMatrix;
            Require(Mathf.Abs(Vector3.Dot(matrix.GetColumn(0), matrix.GetColumn(1))) < .00001f
                && Mathf.Abs(Vector3.Dot(matrix.GetColumn(1), matrix.GetColumn(2))) < .00001f, path + " no hand shear");
            if (ratio < 1f) Require(upper.localScale == upperScale, path + " stretch resets on return");
        }
        arm.CaptureRest();
        Near(arm.TotalReach, reach, path + " initialization never captures stretched reach");
        Require(colliders == upper.root.GetComponentsInChildren<Collider>(true).Length
            && rigidbodies == upper.root.GetComponentsInChildren<Rigidbody>(true).Length, path + " no extra physics components");
        Vector3 near = shoulder + Vector3.forward * (reach * .7f), far = shoulder + Vector3.forward * (reach * 1.4f);
        for (int i = 0; i < 64; i++) arm.Solve((i & 1) == 0 ? near : far, Quaternion.identity, pole, RemotePlayerBody.PlayerArmMaxStretch);
        long bytes = GC.GetAllocatedBytesForCurrentThread();
        var timer = System.Diagnostics.Stopwatch.StartNew();
        // Exclude the benchmark timer's allocation, not allocations in the arm solve.
        bytes = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 20000; i++) arm.Solve((i & 1) == 0 ? near : far, Quaternion.identity, pole, RemotePlayerBody.PlayerArmMaxStretch);
        bytes = GC.GetAllocatedBytesForCurrentThread() - bytes;
        timer.Stop();
        Require(bytes == 0, path + " no recurring solve allocations: " + bytes);
        Debug.Log(path + " visual arm: " + (timer.Elapsed.TotalMilliseconds / 20d).ToString("F2") + " microseconds/solve; 0 allocated bytes");
    }

    static void VerifySingularTargets(TwoBoneLimb arm, Transform upper, Transform mid,
        Vector3 wristOffset, Vector3 handScale, string path)
    {
        float reach = arm.TotalReach;
        foreach (Vector3 direction in new[] { Vector3.forward, Vector3.up, Vector3.down })
        foreach (float distance in new[] { 0f, .000001f, .01f, Upper - Forearm,
            Upper - Forearm + .001f, reach - .0011f, reach - .0001f,
            reach, reach + .0001f, reach * 1.4f - .0001f, reach * 1.4f + .0001f, 10f, .3f })
        {
            Vector3 shoulder = upper.position;
            // Collinear pole and target are deliberately adversarial, including vertical.
            arm.Solve(shoulder + direction * distance, Quaternion.Euler(0, 180, 180),
                shoulder + direction, RemotePlayerBody.PlayerArmMaxStretch);
            Require(float.IsFinite(arm.EndBone.position.x) && float.IsFinite(mid.position.y), path + " finite singular target");
            Require(Vector3.Distance(mid.TransformPoint(wristOffset), arm.EndPos) < .0011f,
                path + " singular wrist connection at " + distance + " toward " + direction);
            Require(Vector3.Distance(shoulder, arm.EndPos) <= reach * 1.4f + .00002f,
                path + " singular target respects maximum");
            Require(Vector3.Distance(arm.EndBone.lossyScale, handScale) < LengthTolerance,
                path + " singular target preserves hand size");
        }
    }

    static void VerifyUnchangedNPCs()
    {
        string[] paths = {
            "Assets/Prefabs/Enemies/ZombieBomber.prefab",
            "Assets/Prefabs/Enemies/ZombieMinion.prefab",
            "Assets/Prefabs/Enemies/ZombieSmasher.prefab",
            "Assets/Prefabs/Enemies/ZombieSpider.prefab",
            "Assets/Resources/DuckCompanion.prefab"
        };
        // Baseline measured before the player-only prefab overrides.
        float[][] reaches = {
            new[] { 0.4423f, 0.4351f }, new[] { 0.7880f, 0.7880f },
            new[] { 0.8858f, 0.8858f }, new[] { 0.8553f, 0.8553f, 0.9210f, 0.9210f },
            new[] { 0.5516f, 0.5516f }
        };
        for (int i = 0; i < paths.Length; i++)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(paths[i]);
            try
            {
                var limbs = root.GetComponentsInChildren<TwoBoneLimb>(true);
                Require(limbs.Length == reaches[i].Length, paths[i] + " chain count");
                for (int j = 0; j < limbs.Length; j++)
                {
                    limbs[j].CaptureRest();
                    Require(Mathf.Abs(limbs[j].TotalReach - reaches[i][j]) < 0.0001f,
                        paths[i] + " original reach " + j + ": " + limbs[j].TotalReach);
                }
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
    }

    static void VerifyNetworkPoses(string localPath)
    {
        GameObject localRoot = LoadRig(localPath);
        GameObject remoteRoot = null;
        try
        {
            remoteRoot = LoadRig(Players[2]);
            var local = localRoot.GetComponentInChildren<RemotePlayerBody>(true);
            var remote = remoteRoot.GetComponentInChildren<RemotePlayerBody>(true);
            local.InitializeIKPreview();
            remote.InitializeIKPreview();
            for (int i = 0; i < 5; i++)
            {
                var pose = new PosePacket {
                    HeadPos = new Vector3(1, i == 1 ? 0.65f : 1.6f, 2),
                    HeadRot = Quaternion.Euler(i == 1 ? 45 : 0, i * 45, 0),
                    LHandRot = Quaternion.Euler(15, -20, 40),
                    RHandRot = Quaternion.Euler(-25, 30, -35)
                };
                Vector3 offset = i == 0 ? new Vector3(.25f, -.3f, .45f)
                    : i == 1 ? new Vector3(.25f, -.65f, .15f) // Crouched ground contact.
                    : i == 2 ? new Vector3(.12f, -.1f, .12f) // Near face/chest.
                    : i == 3 ? new Vector3(.4f, .15f, .15f)
                    : new Vector3(.8f, -.2f, .8f); // Outside arm reach, inside codec range.
                Quaternion yaw = Quaternion.Euler(0, i * 45, 0);
                pose.RHandPos = pose.HeadPos + yaw * offset;
                offset.x = -offset.x;
                pose.LHandPos = pose.HeadPos + yaw * offset;
                byte[] bytes = PosePacket.Serialize(pose);
                Require(bytes.Length == 39, "network packet stays 39 bytes");
                var received = ((PosePacket)PosePacket.Deserialize(bytes)).ToBodyPose();
                local.ApplyIKPreview(pose.ToBodyPose());
                remote.ApplyIKPreview(received);
                foreach (bool left in new[] { true, false })
                {
                    TwoBoneLimb a = left ? local.LeftArm : local.RightArm;
                    TwoBoneLimb b = left ? remote.LeftArm : remote.RightArm;
                    Near(a.TotalReach, b.TotalReach, localPath + " network length parity");
                    Require(Vector3.Distance(a.EndPos, b.EndPos) < 0.002f,
                        localPath + " network hand parity, pose " + i);
                    Require(Vector3.Distance(a.EndBone.TransformPoint(RemotePlayerBody.PlayerPalmPoint),
                        b.EndBone.TransformPoint(RemotePlayerBody.PlayerPalmPoint)) < 0.002f,
                        localPath + " network palm parity, pose " + i);
                    // Desktop has its own existing elbow bias/twist tuning; preserve it.
                    if (localPath == Players[0])
                        Require(Vector3.Distance(Bone(a, "midBone").position, Bone(b, "midBone").position) < 0.002f,
                            localPath + " network elbow parity, pose " + i);
                }
                if (i == 1)
                {
                    Require(Vector3.Distance(local.LeftHand.TransformPoint(RemotePlayerBody.PlayerPalmPoint), pose.LHandPos) < 0.0002f
                        && Vector3.Distance(local.RightHand.TransformPoint(RemotePlayerBody.PlayerPalmPoint), pose.RHandPos) < 0.0002f,
                        localPath + " palms reach floor targets");
                }
            }
        }
        finally
        {
            if (remoteRoot != null) PrefabUtility.UnloadPrefabContents(remoteRoot);
            PrefabUtility.UnloadPrefabContents(localRoot);
        }
    }

    static Transform Bone(TwoBoneLimb limb, string field) =>
        (Transform)new SerializedObject(limb).FindProperty(field).objectReferenceValue;

    static GameObject LoadRig(string path)
    {
        var root = PrefabUtility.LoadPrefabContents(path);
        // Runtime instances can reparent skin bones. Nested imported prefab instances
        // in an edit-mode fixture must first be unpacked (never save this fixture).
        foreach (var child in root.GetComponentsInChildren<Transform>(true))
            if (PrefabUtility.IsAnyPrefabInstanceRoot(child.gameObject))
                PrefabUtility.UnpackPrefabInstance(child.gameObject, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        return root;
    }

    static void Near(float actual, float expected, string message) =>
        Require(Mathf.Abs(actual - expected) < LengthTolerance, message + ": " + actual + " vs " + expected);

    static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("PlayerArmLengthChecks: " + message);
    }
}
