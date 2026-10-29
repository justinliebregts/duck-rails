using System;
using Game.Animation;
using UnityEditor;
using UnityEngine;

// Exercises the real prefab chains and network pose codec without entering a room.
public static class PlayerArmLengthChecks
{
    const float Upper = 0.31663984f;
    const float Forearm = 0.29011774f;
    const float LengthTolerance = 0.00002f;
    static readonly string[] Players = {
        "Assets/Prefabs/Player/DuckPlayer.prefab",
        "Assets/Prefabs/Player/DesktopPlayer.prefab",
        "Assets/Prefabs/Photon/PhotonPlayer.prefab"
    };

    [MenuItem("Tools/DuckZ/Validation/Rendering/Validate Player Arm Lengths")]
    public static void Run()
    {
        foreach (string path in Players)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(path);
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
                    foreach (Vector3 offset in new[] {
                        new Vector3(0, 0, 0.59f), // Beyond the old reach, within the new reach.
                        new Vector3(0.1f, -0.45f, 0.1f),
                        new Vector3(0.1f, 0.2f, 0.15f),
                        new Vector3(0.45f, 0.1f, 0.1f)
                    })
                    {
                        Vector3 target = shoulder + offset;
                        arm.Solve(target, Quaternion.identity, pole);
                        Require(Vector3.Distance(end.position, target) < 0.0002f,
                            path + " reachable target " + offset);
                    }
                    arm.Solve(shoulder + Vector3.forward * 10f, Quaternion.identity, pole);
                    Require(Vector3.Distance(shoulder, end.position) <= Upper + Forearm + LengthTolerance,
                        path + " far controller must stay bounded");
                    Require(mid.localPosition == midOffset && end.localPosition == endOffset
                        && Vector3.Distance(end.lossyScale, handScale) < LengthTolerance,
                        path + " solving preserves offsets and hand size");
                }
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        VerifyUnchangedNPCs();
        VerifyNetworkPoses(Players[0]);
        VerifyNetworkPoses(Players[1]);
        Debug.Log("PlayerArmLengthChecks PASS: VR/desktop/Photon 60.676 cm arms; reachable and bounded targets; unchanged enemies/companion; network codec pose agreement.");
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
        GameObject localRoot = PrefabUtility.LoadPrefabContents(localPath);
        GameObject remoteRoot = null;
        try
        {
            remoteRoot = PrefabUtility.LoadPrefabContents(Players[2]);
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

    static void Near(float actual, float expected, string message) =>
        Require(Mathf.Abs(actual - expected) < LengthTolerance, message + ": " + actual + " vs " + expected);

    static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("PlayerArmLengthChecks: " + message);
    }
}
