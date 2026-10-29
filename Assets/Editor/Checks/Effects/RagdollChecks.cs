using System;
using System.Reflection;
using Game.AI;
using Game.Animation;
using Photon.Pun;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class RagdollChecks
{
    sealed class Sender : Photon.Realtime.Player
    {
        public Sender() : base("Ragdoll fixture", 2, false) { }
    }
    public static void Run()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Run in batch mode.");
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        LaunchChecks();
        var prefab = AssetDatabase.LoadAssetAtPath<Ragdoll>("Assets/Prefabs/Player/DuckSkin_Ragdoll.prefab");
        var source = UnityEngine.Object.Instantiate(prefab);
        source.gameObject.SetActive(false);
        var ragdoll = UnityEngine.Object.Instantiate(prefab);
        ragdoll.gameObject.SetActive(false);
        ragdoll.sourceHip = source.targetHip;
        ragdoll.leftHandSource = source.leftHandTarget;
        ragdoll.rightHandSource = source.rightHandTarget;
        var bones = ragdoll.GetComponentsInChildren<Transform>(true);
        var offsets = new Vector3[bones.Length];
        for (int i = 0; i < bones.Length; i++) offsets[i] = bones[i].localPosition;
        Debug.Log("Ragdoll hip: " + ragdoll.targetHip.name + "; bones: " + bones.Length);
        var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
        floor.transform.position = new Vector3(100, -0.5f, 100);
        floor.transform.localScale = new Vector3(40, 1, 40);
        var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wall.transform.position = new Vector3(110, 10, 100);
        wall.transform.localScale = new Vector3(1, 20, 40);
        var previousSimulationMode = Physics.simulationMode;
        try
        {
            Physics.simulationMode = SimulationMode.Script;
            for (int death = 0; death < 3; death++)
            {
                source.transform.position = new Vector3(100 + death * 2, 3, 100);
                source.targetHip.rotation = Quaternion.Euler(0, death * 35, 0);
                // An impossible tracked hand position must never stretch the ragdoll.
                source.leftHandTarget.position += Vector3.right * 1000;
                source.leftHandTarget.rotation = Quaternion.Euler(75, 20, 110);
                ragdoll.PoseToPlayer(source.transform, new RagdollLaunch {
                    impact = death == 1 ? new Vector3(11, 6.5f, 0) : Vector3.zero
                });
                Check(Quaternion.Angle(ragdoll.targetHip.rotation, source.targetHip.rotation) < 0.01f, "hip rotation");
                for (int i = 0; i < bones.Length; i++)
                    if (bones[i] != ragdoll.transform && bones[i] != ragdoll.targetHip)
                        Check(Vector3.Distance(bones[i].localPosition, offsets[i]) < 0.001f, "bone length reset: " + bones[i].name);
                foreach (var joint in ragdoll.GetComponentsInChildren<CharacterJoint>())
                    Check(joint.enableProjection, "projection enabled");
                for (int step = 0; step < 240; step++)
                {
                    Physics.Simulate(1f / 72f);
                    foreach (var body in ragdoll.GetComponentsInChildren<Rigidbody>())
                    {
                        Check(Vector3.Distance(body.position, ragdoll.targetHip.position) < 3f, "body escaped: " + body.name);
                        Check(body.position.x < 110.6f, "launched body passed through wall: " + body.name);
                    }
                }
                ragdoll.gameObject.SetActive(false);
                ragdoll.transform.SetParent(source.transform, true);
            }
        }
        finally { Physics.simulationMode = previousSimulationMode; }
        Debug.Log("Ragdoll checks passed: three deaths, distant spawn, impossible hand target, bone resets, hip rotation, Smasher launch into wall and 720 physics steps.");
    }

    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    static void Set(object target, string name, object value) => target.GetType().GetField(name, Private).SetValue(target, value);

    static PhotonStream WriteStream()
    {
        var stream = new PhotonStream(true, null);
        Set(stream, "writeData", new System.Collections.Generic.List<object>());
        return stream;
    }

    static void LaunchChecks()
    {
        string[] names = { "ZombieMinion", "ZombieSpider", "ZombieBomber", "ZombieSmasher" };
        float[] force = { 3.5f, 5f, 8f, 11f }, lift = { 1.5f, 1.5f, 4f, 6.5f };
        for (int i = 0; i < names.Length; i++)
        {
            var enemy = AssetDatabase.LoadAssetAtPath<Enemy>("Assets/Prefabs/Enemies/" + names[i] + ".prefab");
            float min = float.PositiveInfinity, max = 0;
            for (int n = 0; n < 100; n++)
            {
                Vector3 impact = enemy.PlayerDeathLaunch(Vector3.left * 3, Vector3.zero);
                Vector3 rotation = enemy.PlayerDeathRotation(impact);
                float spin = new[] { 3f, 6f, 9f, 12f }[i];
                Check(rotation.magnitude >= spin * .879f && rotation.magnitude <= spin * 1.121f, names[i] + " spin bounds");
                Check(impact.x < 0 && impact.z == 0, "push away from attacker");
                Check(-impact.x >= force[i] * .879f && -impact.x <= force[i] * 1.121f, names[i] + " force bounds");
                Check(impact.y >= lift[i] * .879f && impact.y <= lift[i] * 1.121f, names[i] + " lift bounds");
                min = Mathf.Min(min, -impact.x); max = Mathf.Max(max, -impact.x);
            }
            Check(max > min, "slight variation sampled");
            Check(enemy.PlayerDeathLaunch(Vector3.zero, Vector3.zero).sqrMagnitude > 0, "overlapping origin fallback");
        }

        var prefab = AssetDatabase.LoadAssetAtPath<Ragdoll>("Assets/Prefabs/Player/DuckSkin_Ragdoll.prefab");
        var source = UnityEngine.Object.Instantiate(prefab); source.Park();
        source.transform.position = new Vector3(100, 3, 100);
        var corpse = UnityEngine.Object.Instantiate(prefab); corpse.Park();
        var impactVelocity = new Vector3(11, 6.5f, 0);
        corpse.leftHandTarget = null; corpse.rightHandTarget = null;
        corpse.PoseToPlayer(source.transform, new RagdollLaunch { body = Vector3.forward * 1000, impact = impactVelocity });
        foreach (var rb in corpse.GetComponentsInChildren<Rigidbody>())
            Check(Vector3.Distance(rb.linearVelocity, impactVelocity + Vector3.forward * 8) < .001f, "tracking clamped separately from impact");
        corpse.leftHandTarget = corpse.targetHip; // Exercise the hand override on a known body.
        corpse.PoseToPlayer(source.transform, new RagdollLaunch { impact = impactVelocity });
        foreach (var rb in corpse.GetComponentsInChildren<Rigidbody>())
            Check(Vector3.Distance(rb.linearVelocity, impactVelocity) < .001f, "hands retain shared impact");
        corpse.Park(); corpse.PoseToPlayer(source.transform);
        foreach (var rb in corpse.GetComponentsInChildren<Rigidbody>())
            Check(rb.linearVelocity == Vector3.zero, "pooled corpse clears previous launch");

        // Exercise the real conditional stream layout and remote death transition.
        var localGo = new GameObject("Local death state"); localGo.SetActive(false);
        var player = localGo.AddComponent<GR.Player>(); GR.Player.instance = player;
        Set(player, "isDead", true);
        typeof(GR.Player).GetProperty("DeathImpactState").SetValue(player, impactVelocity);
        Vector3 rotationVelocity = new Vector3(0, .3f, -3);
        typeof(GR.Player).GetProperty("DeathRotationState").SetValue(player, rotationVelocity);
        var ownerGo = new GameObject("Owner stream"); ownerGo.SetActive(false);
        var view = ownerGo.AddComponent<PhotonView>();
        typeof(PhotonView).GetProperty("IsMine").SetValue(view, true);
        var owner = ownerGo.AddComponent<PhotonPlayer>();
        var remoteGo = new GameObject("Remote stream"); remoteGo.SetActive(false); remoteGo.AddComponent<PhotonView>();
        var remote = remoteGo.AddComponent<PhotonPlayer>();
        var body = remoteGo.AddComponent<RemotePlayerBody>();
        var left = remoteGo.AddComponent<TwoBoneLimb>();
        var right = remoteGo.AddComponent<TwoBoneLimb>();
        Set(left, "endBone", source.leftHandTarget); Set(right, "endBone", source.rightHandTarget);
        Set(left, "midBone", source.leftHandTarget.parent); Set(left, "upperBone", source.leftHandTarget.parent.parent);
        Set(right, "midBone", source.rightHandTarget.parent); Set(right, "upperBone", source.rightHandTarget.parent.parent);
        left.CaptureRest(); right.CaptureRest();
        Set(body, "chest", source.targetHip); Set(body, "head", source.targetHip);
        Set(body, "leftArm", left); Set(body, "rightArm", right);
        Set(remote, "body", body); Set(remote, "deathRagdollPrefab", prefab);
        var send = WriteStream();
        owner.OnPhotonSerializeView(send, default);
        object[] packet = send.ToArray();
        Check(packet.Length == 5 && ReferenceEquals(packet[2], player.DeathImpactState)
            && ReferenceEquals(packet[3], player.DeathRotationState), "dead stream uses cached boxed vectors");
        // Production pose reception requires a real actor identity and current timestamp.
        // A default message is deliberately rejected before it can create a ragdoll.
        var sender = new Sender();
        remote.photonView.OwnerActorNr = sender.ActorNumber;
        int timestamp = unchecked((int)(uint)(long)(PhotonNetwork.Time * 1000d));
        var message = new PhotonMessageInfo(sender, timestamp, remote.photonView);
        remote.OnPhotonSerializeView(new PhotonStream(false, packet), message);
        var remoteCorpse = (Ragdoll)typeof(PhotonPlayer).GetField("_remoteRagdoll", Private).GetValue(remote);
        foreach (var rb in remoteCorpse.GetComponentsInChildren<Rigidbody>())
            Check(Vector3.Distance(rb.linearVelocity, impactVelocity) < .001f, "remote/late arrival receives exact launch");
        var hip = remoteCorpse.targetHip.GetComponent<Rigidbody>();
        var leftUpper = UpperArm(remoteCorpse.leftHandTarget, hip);
        var rightUpper = UpperArm(remoteCorpse.rightHandTarget, hip);
        Check(leftUpper != null && rightUpper != null && leftUpper != hip && rightUpper != hip, "upper arm fixture references");
        Vector3 armSpin = Vector3.ClampMagnitude(rotationVelocity, 10f) * .2f;
        Vector3 armTwist = Vector3.Cross(Vector3.up, armSpin) * .5f;
        foreach (var rb in remoteCorpse.GetComponentsInChildren<Rigidbody>())
            Check(Vector3.Distance(rb.angularVelocity, rb == hip ? rotationVelocity : rb == leftUpper ? armSpin + armTwist : rb == rightUpper ? armSpin - armTwist : Vector3.zero) < .001f, "remote kick affects only torso and upper arms");
        hip.linearVelocity = Vector3.zero;
        remote.OnPhotonSerializeView(new PhotonStream(false, packet), message);
        Check(hip.linearVelocity == Vector3.zero, "duplicate dead state does not relaunch");
        Set(player, "isDead", false);
        send = WriteStream(); owner.OnPhotonSerializeView(send, default);
        Check(send.ToArray().Length == 3, "living stream has no extra payload");
        UnityEngine.Object.DestroyImmediate(remoteCorpse.gameObject);
        UnityEngine.Object.DestroyImmediate(remoteGo); UnityEngine.Object.DestroyImmediate(ownerGo);
        UnityEngine.Object.DestroyImmediate(localGo); GR.Player.instance = null;
        UnityEngine.Object.DestroyImmediate(corpse.gameObject); UnityEngine.Object.DestroyImmediate(source.gameObject);
        Debug.Log("Death launch checks passed: four prefab profiles, variation, separate tracking clamp, hands, reuse, dead-only stream, remote launch and duplicate rejection.");
    }

    static Rigidbody UpperArm(Transform hand, Rigidbody torso)
    {
        // Rig helper transforms can change the parent count; the shoulder is
        // identified by its physical connection to the torso.
        foreach (var joint in hand.GetComponentsInParent<CharacterJoint>(true))
            if (joint.connectedBody == torso) return joint.GetComponent<Rigidbody>();
        return null;
    }

    static void Check(bool ok, string message)
    {
        if (!ok) throw new Exception("Ragdoll check failed: " + message);
    }
}
