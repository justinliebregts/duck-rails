using System;
using System.Reflection;
using Game.Animation;
using Photon.Pun;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

public static class PlayerRagdollLengthChecks
{
    const string PlayerCorpse = "Assets/Prefabs/Player/DuckPlayer_Ragdoll.prefab";
    const string OriginalCorpse = "Assets/Prefabs/Player/DuckSkin_Ragdoll.prefab";
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    const float HumanReach = 0.6067576f;
    const float OriginalReach = 0.5515978f;
    static float maxPhysicsDrift;
    static float maxImpactSeparation;

    sealed class Sender : Photon.Realtime.Player
    {
        public Sender() : base("Arm length check", 123, false) { }
    }

    [MenuItem("Tools/DuckZ/Validation/Effects/Validate Player Ragdoll Lengths")]
    public static void Run()
    {
        Require(!EditorApplication.isPlaying, "Run outside Play mode");
        maxPhysicsDrift = 0f;
        maxImpactSeparation = 0f;
        var original = AssetDatabase.LoadAssetAtPath<Ragdoll>(OriginalCorpse);
        var human = AssetDatabase.LoadAssetAtPath<Ragdoll>(PlayerCorpse);
        CheckLengths(original, OriginalReach);
        CheckLengths(human, HumanReach);
        CheckVariantPhysics(original, human);
        var companion = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/DuckCompanion.prefab");
        Require(DeathPrefab(companion) == original, "companion keeps original ragdoll");
        var photonPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Photon/PhotonPlayer.prefab");
        Require(DeathPrefab(photonPrefab) == human, "Photon uses player ragdoll variant");

        // An isolated physics world: never simulate or replace the user's current scene.
        var scene = EditorSceneManager.NewPreviewScene();
        try
        {
            var physics = scene.GetPhysicsScene();
            Require(physics.IsValid() && !physics.Equals(Physics.defaultPhysicsScene), "isolated physics world");
            foreach (string path in new[] {
                "Assets/Prefabs/Player/DuckPlayer.prefab", "Assets/Prefabs/Player/DesktopPlayer.prefab"
            }) VerifyLocal(path, scene, physics);
            VerifyPhoton(photonPrefab, human, scene, physics);
            VerifyImpacts(photonPrefab, human, scene, physics);
        }
        finally { EditorSceneManager.ClosePreviewScene(scene); }
        Debug.Log("PlayerRagdollLengthChecks PASS: local VR/desktop, Photon death/respawn/reuse, all four skins, 5400 isolated physics steps, matching bones/colliders, original companion corpse. Maximum dynamic reach drift: " + maxPhysicsDrift + " m; impact arm anchor separation: " + maxImpactSeparation + " m.");
    }

    static Ragdoll DeathPrefab(GameObject root) => (Ragdoll)new SerializedObject(root.GetComponent<PhotonPlayer>())
        .FindProperty("deathRagdollPrefab").objectReferenceValue;

    static void VerifyLocal(string path, Scene scene, PhysicsScene physics)
    {
        var root = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(path), scene);
        PrefabUtility.UnpackPrefabInstance(root, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        root.SetActive(false);
        Ragdoll corpse = root.GetComponentInChildren<Ragdoll>(true);
        try
        {
            var body = root.GetComponentInChildren<RemotePlayerBody>(true);
            body.InitializeIKPreview();
            Require(corpse != null && corpse.sourceHip == body.Chest
                && corpse.leftHandSource == body.LeftHand && corpse.rightHandSource == body.RightHand,
                path + " source references preserved");
            InitializePreviewPhysics(corpse);
            CheckLengths(corpse, HumanReach);
            for (int i = 0; i < 3; i++)
            {
                body.ApplyIKPreview(Packet(i).ToBodyPose());
                corpse.PoseToPlayer(root.transform, new RagdollLaunch { impact = new Vector3(1, 2, 0) });
                CheckPose(corpse, body, path);
                Simulate(corpse, physics, path);
                corpse.Park();
            }
        }
        finally
        {
            if (corpse != null && !corpse.transform.IsChildOf(root.transform)) Object.DestroyImmediate(corpse.gameObject);
            Object.DestroyImmediate(root);
        }
    }

    static void VerifyPhoton(GameObject prefab, Ragdoll human, Scene scene, PhysicsScene physics)
    {
        var root = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
        PrefabUtility.UnpackPrefabInstance(root, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        root.SetActive(false);
        var corpse = ((GameObject)PrefabUtility.InstantiatePrefab(human.gameObject, scene)).GetComponent<Ragdoll>();
        PrefabUtility.UnpackPrefabInstance(corpse.gameObject, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        InitializePreviewPhysics(corpse);
        corpse.Park();
        try
        {
            var player = root.GetComponent<PhotonPlayer>();
            var body = root.GetComponentInChildren<RemotePlayerBody>(true);
            body.InitializeIKPreview();
            var sender = new Sender();
            player.photonView.OwnerActorNr = sender.ActorNumber;
            // Use the authored prefab as the prepared pool entry; avoid editor-time delayed Destroy.
            typeof(PhotonPlayer).GetField("_remoteRagdoll", Private).SetValue(player, corpse);
            long timestamp = (long)(PhotonNetwork.Time * 1000d);
            for (int i = 0; i < 3; i++)
            {
                var packet = (PosePacket)PosePacket.Deserialize(PosePacket.Serialize(Packet(i)));
                int stamp = unchecked((int)(uint)(timestamp + i * 2));
                var dead = new object[] { packet, true, new Vector3(1, 2, 0), Vector3.zero, 5 };
                player.OnPhotonSerializeView(new PhotonStream(false, dead), new PhotonMessageInfo(sender, stamp, player.photonView));
                Require(corpse.gameObject.activeSelf, "Photon death activates prepared corpse");
                CheckPose(corpse, body, "Photon death");
                Simulate(corpse, physics, "Photon death");
                player.OnPhotonSerializeView(new PhotonStream(false, new object[] { packet, false, 0 }),
                    new PhotonMessageInfo(sender, unchecked(stamp + 1), player.photonView));
                Require(!corpse.gameObject.activeSelf, "Photon respawn parks corpse");
                Require(ReferenceEquals(typeof(PhotonPlayer).GetField("_remoteRagdoll", Private).GetValue(player), corpse),
                    "Photon reuses prepared corpse");
            }
        }
        finally
        {
            // Keep OnDestroy from scheduling the already-cleaned-up corpse for delayed destruction.
            typeof(PhotonPlayer).GetField("_remoteRagdoll", Private).SetValue(root.GetComponent<PhotonPlayer>(), null);
            Object.DestroyImmediate(corpse.gameObject);
            Object.DestroyImmediate(root);
        }
    }

    static PosePacket Packet(int i) => new PosePacket {
        HeadPos = new Vector3(i * 3, 3, 0), HeadRot = Quaternion.Euler(0, i * 30, 0),
        LHandPos = new Vector3(i * 3 - .3f, 2.8f, .4f), LHandRot = Quaternion.Euler(10, 20, 30),
        RHandPos = new Vector3(i * 3 + .3f, 2.7f, .3f), RHandRot = Quaternion.Euler(-10, -20, -30)
    };

    static void VerifyImpacts(GameObject prefab, Ragdoll human, Scene scene, PhysicsScene physics)
    {
        var root = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
        PrefabUtility.UnpackPrefabInstance(root, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        root.SetActive(false);
        var corpse = ((GameObject)PrefabUtility.InstantiatePrefab(human.gameObject, scene)).GetComponent<Ragdoll>();
        PrefabUtility.UnpackPrefabInstance(corpse.gameObject, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
        SceneManager.MoveGameObjectToScene(floor, scene);
        floor.layer = LayerMask.NameToLayer("Environment");
        floor.transform.position = new Vector3(0, -.5f, 0);
        floor.transform.localScale = new Vector3(100, 1, 100);
        try
        {
            var body = root.GetComponentInChildren<RemotePlayerBody>(true);
            body.InitializeIKPreview();
            InitializePreviewPhysics(corpse);
            corpse.Park();
            corpse.sourceHip = body.Chest;
            corpse.leftHandSource = body.LeftHand;
            corpse.rightHandSource = body.RightHand;
            var skin = DuckSkinVisuals.Attach(body.gameObject, DuckSkinSource.Manual);
            var joints = corpse.GetComponentsInChildren<CharacterJoint>(true);
            var bodies = corpse.GetComponentsInChildren<Rigidbody>(true);
            for (byte id = 0; id < 4; id++)
            for (int impact = 0; impact < 3; impact++)
            {
                skin.Apply(id);
                corpse.ApplySkin(id);
                body.ApplyIKPreview(Packet(impact).ToBodyPose());
                var launch = impact == 0 ? default : new RagdollLaunch {
                    body = new Vector3(impact == 1 ? 3 : -5, 1, 0),
                    impact = new Vector3(1, 2, 1),
                    leftHand = new Vector3(0, 3, 4), rightHand = new Vector3(0, -2, -4),
                    rotation = new Vector3(1, 2, 3)
                };
                corpse.PoseToPlayer(root.transform, launch);
                CheckPose(corpse, body, "skin " + id + " impact " + impact);
                for (int step = 0; step < 360; step++)
                {
                    physics.Simulate(1f / 60f);
                    foreach (var joint in joints)
                    {
                        if (!IsArmJoint(joint)) continue;
                        float separation = Vector3.Distance(joint.transform.TransformPoint(joint.anchor),
                            joint.connectedBody.transform.TransformPoint(joint.connectedAnchor));
                        maxImpactSeparation = Mathf.Max(maxImpactSeparation, separation);
                        // Projection is a corrective threshold, not an exact per-step hard cap.
                        Require(separation < .05f, "impact arm joint remains connected: " + joint.name);
                    }
                    foreach (var rb in bodies)
                    {
                        Require(Vector3.Distance(rb.position, corpse.targetHip.position) < 2f,
                            "impact does not separate body: " + rb.name);
                        Require(rb.position.y > -.5f, "impact collider stays above floor: " + rb.name);
                    }
                }
                foreach (var rb in bodies)
                    Require(rb.linearVelocity.magnitude < 1f, "corpse settles after impact: " + rb.name);
                corpse.Park();
            }
        }
        finally
        {
            Object.DestroyImmediate(corpse.gameObject);
            Object.DestroyImmediate(root);
            Object.DestroyImmediate(floor);
        }
    }

    static void CheckPose(Ragdoll corpse, RemotePlayerBody body, string label)
    {
        CheckProjection(corpse);
        CheckLengths(corpse, HumanReach);
        Require(Vector3.Distance(corpse.leftHandTarget.position, body.LeftHand.position) < .001f
            && Vector3.Distance(corpse.rightHandTarget.position, body.RightHand.position) < .001f,
            label + " no hand jump at death");
        foreach (var joint in corpse.GetComponentsInChildren<CharacterJoint>())
            Require(Vector3.Distance(joint.transform.TransformPoint(joint.anchor),
                joint.connectedBody.transform.TransformPoint(joint.connectedAnchor)) < .001f,
                label + " joint anchors agree: " + joint.name);
    }

    static void Simulate(Ragdoll corpse, PhysicsScene physics, string label)
    {
        for (int i = 0; i < 120; i++)
        {
            physics.Simulate(1f / 72f);
            // Dynamic joints permit temporary solver error (2.2 cm arm projection threshold).
            // Exact rest lengths and hand alignment are checked again at every death/reuse.
            foreach (var hand in new[] { corpse.leftHandTarget, corpse.rightHandTarget })
            {
                float reach = Vector3.Distance(hand.position, hand.parent.position)
                    + Vector3.Distance(hand.parent.position, hand.parent.parent.position);
                maxPhysicsDrift = Mathf.Max(maxPhysicsDrift, Mathf.Abs(reach - HumanReach));
            }
            CheckLengths(corpse, HumanReach, .1f);
            foreach (var rb in corpse.GetComponentsInChildren<Rigidbody>())
                Require(Vector3.Distance(rb.position, corpse.targetHip.position) < 2f, label + " stable joints: " + rb.name);
        }
    }

    static void InitializePreviewPhysics(Ragdoll corpse)
    {
        // Edit-mode preview scenes leave automatic connected anchors at their serialized zero.
        // Reproduce Unity's runtime rest-pose anchoring on the disposable fixture only.
        foreach (var joint in corpse.GetComponentsInChildren<CharacterJoint>(true))
        {
            Require(joint.autoConfigureConnectedAnchor && joint.connectedBody != null, "runtime automatic anchor setup");
            Vector3 point = joint.transform.TransformPoint(joint.anchor);
            joint.autoConfigureConnectedAnchor = false;
            joint.connectedAnchor = joint.connectedBody.transform.InverseTransformPoint(point);
        }
    }

    static void CheckLengths(Ragdoll corpse, float expected, float tolerance = .00002f)
    {
        Require(corpse != null, "ragdoll exists");
        foreach (Transform hand in new[] { corpse.leftHandTarget, corpse.rightHandTarget })
        {
            Require(hand != null && hand.parent != null && hand.parent.parent != null, "hand chain exists");
            float reach = Vector3.Distance(hand.position, hand.parent.position)
                + Vector3.Distance(hand.parent.position, hand.parent.parent.position);
            Require(Mathf.Abs(reach - expected) < tolerance, corpse.name + " reach: " + reach + " expected " + expected);
        }
    }

    static void CheckVariantPhysics(Ragdoll original, Ragdoll human)
    {
        int projectionOverrides = 0;
        foreach (var modification in PrefabUtility.GetPropertyModifications(human.gameObject))
        {
            if (modification.target is CharacterJoint joint)
            {
                Require(IsArmJoint(joint) && modification.propertyPath == "m_ProjectionDistance",
                    "only player arm projection distance may override joint tuning");
                projectionOverrides++;
            }
            Require(!(modification.target is Rigidbody), "rigidbody tuning inherited unchanged");
        }
        Require(projectionOverrides == 6, "six player shoulder/elbow/wrist projection overrides");
        foreach (var joint in original.GetComponentsInChildren<CharacterJoint>(true))
            Require(joint.enableProjection && Mathf.Abs(joint.projectionDistance - .05f) < .000001f,
                "original/companion projection stays 5 cm: " + joint.name);
        CheckProjection(human);
        Require(original.GetComponentsInChildren<Rigidbody>(true).Length == human.GetComponentsInChildren<Rigidbody>(true).Length,
            "no extra physics bodies");
        foreach (var bone in human.GetComponentsInChildren<Transform>(true))
        {
            if (!bone.name.StartsWith("upper_arm.") && !bone.name.StartsWith("forearm.") && !bone.name.StartsWith("hand.")) continue;
            var source = PrefabUtility.GetCorrespondingObjectFromSource(bone);
            var a = source.GetComponent<CapsuleCollider>();
            var b = bone.GetComponent<CapsuleCollider>();
            float multiplier = bone.name.StartsWith("hand.") ? 1f : 1.1f;
            Require(Mathf.Abs(b.height - a.height * multiplier) < .00001f
                && Mathf.Abs(b.center.y - a.center.y * multiplier) < .00001f && b.radius == a.radius,
                bone.name + " collider length/width");
        }
    }

    static bool IsArmJoint(CharacterJoint joint) => joint.name.StartsWith("upper_arm.")
        || joint.name.StartsWith("forearm.") || joint.name.StartsWith("hand.");

    static void CheckProjection(Ragdoll corpse)
    {
        int armJoints = 0;
        foreach (var joint in corpse.GetComponentsInChildren<CharacterJoint>(true))
        {
            bool arm = IsArmJoint(joint);
            if (arm) armJoints++;
            Require(joint.enableProjection
                && Mathf.Abs(joint.projectionDistance - (arm ? .022f : .05f)) < .000001f,
                "player arm projection 2.2 cm; other joints unchanged: " + joint.name);
        }
        Require(armJoints == 6, "complete player arm joint chains");
    }

    static void Require(bool ok, string message)
    {
        if (!ok) throw new InvalidOperationException("PlayerRagdollLengthChecks: " + message);
    }
}
