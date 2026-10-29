using System;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class RagdollOwnerCollisionChecks
{
    public static void Run()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Batch checks only.");
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var previous = Physics.simulationMode;
        try
        {
            Physics.simulationMode = SimulationMode.Script;
            CheckOwner(false); // Local Desktop/VR: corpse initially inside player hierarchy.
            CheckOwner(true);  // Photon: separate corpse, bound to remote avatar hierarchy.
            CheckArms();
            Debug.Log("RagdollOwnerCollisionChecks PASSED: owner root/child/controller ignored, environment/other player/items preserved, three activations, unchanged launch and physics motion.");
        }
        finally { Physics.simulationMode = previous; }
    }

    static void CheckOwner(bool remote)
    {
        var owner = new GameObject("Owner");
        owner.transform.position = new Vector3(remote ? 100 : 0, 3, 0);
        var ownerRoot = owner.AddComponent<SphereCollider>(); ownerRoot.radius = 1;
        var child = new GameObject("Owner child"); child.transform.SetParent(owner.transform, false);
        var ownerChild = child.AddComponent<BoxCollider>();
        var controller = owner.AddComponent<UnityEngine.CharacterController>();
        var item = new GameObject("Droppable item"); item.transform.SetParent(owner.transform, false);
        item.transform.localPosition = Vector3.up * 10;
        item.AddComponent<Grabbable>(); var itemCollider = item.AddComponent<BoxCollider>();

        var root = new GameObject("Corpse"); root.SetActive(false);
        if (!remote) root.transform.SetParent(owner.transform, false);
        var bone = new GameObject("Body"); bone.transform.SetParent(root.transform, false);
        var body = bone.AddComponent<Rigidbody>(); body.useGravity = true;
        var shape = bone.AddComponent<SphereCollider>(); shape.radius = .2f;
        var secondShape = bone.AddComponent<BoxCollider>(); secondShape.size = Vector3.one * .1f;
        var ragdoll = root.AddComponent<Ragdoll>();
        ragdoll.targetHip = bone.transform;
        ragdoll.Prepare(owner.transform);

        var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
        floor.name = "Environment floor"; floor.transform.position = owner.transform.position + Vector3.down * 3.5f;
        floor.transform.localScale = new Vector3(20, 1, 20);
        var other = GameObject.CreatePrimitive(PrimitiveType.Cube);
        other.name = "Other player"; other.transform.position = owner.transform.position + Vector3.right * 3;
        other.transform.localScale = new Vector3(1, 10, 10);
        var launch = new RagdollLaunch { body = Vector3.right, impact = new Vector3(3, 2, 0) };
        for (int death = 0; death < 3; death++)
        {
            launch.rotation = death == 1 ? new Vector3(0, .4f, -3) : Vector3.zero;
            ragdoll.PoseToPlayer(owner.transform, launch);
            Require(Vector3.Distance(body.angularVelocity, launch.rotation) < .001f, "torso kick and pooled reset");
            Require(Physics.GetIgnoreCollision(shape, ownerRoot), "owner root");
            Require(Physics.GetIgnoreCollision(shape, ownerChild), "owner child");
            Require(Physics.GetIgnoreCollision(shape, controller), "owner CharacterController");
            Require(!Physics.GetIgnoreCollision(shape, floor.GetComponent<Collider>()), "environment retained");
            Require(!Physics.GetIgnoreCollision(shape, other.GetComponent<Collider>()), "other player retained");
            Require(!Physics.GetIgnoreCollision(shape, itemCollider), "droppable item retained");
            Require(!Physics.GetIgnoreCollision(shape, secondShape), "corpse not captured as its own owner");
            Require(Vector3.Distance(body.linearVelocity, launch.body + launch.impact) < .001f, "launch unchanged");
            Physics.Simulate(.02f);
            Require(Mathf.Abs(body.linearVelocity.x - 4) < .001f, "owner overlap cannot deflect launch");
            for (int step = 0; step < 150; step++) Physics.Simulate(.02f);
            Require(body.position.y >= -.01f && body.position.y < .4f, "landed on environment");
            Require(body.position.x < owner.transform.position.x + 2.5f, "other player blocks corpse");
            ragdoll.Park();
            if (!remote) root.transform.SetParent(owner.transform, true);
        }
        UnityEngine.Object.DestroyImmediate(root);
        UnityEngine.Object.DestroyImmediate(owner);
        UnityEngine.Object.DestroyImmediate(floor);
        UnityEngine.Object.DestroyImmediate(other);
    }

    static void CheckArms()
    {
        Vector3 firstLeft = default, firstRight = default;
        for (int peer = 0; peer < 2; peer++)
        {
            var owner = new GameObject("Arm owner");
            owner.AddComponent<Rigidbody>().isKinematic = true;
            owner.transform.rotation = Quaternion.Euler(0, peer * 90, 0);
            var root = new GameObject("Arm corpse"); root.SetActive(false);
            if (peer == 0) root.transform.SetParent(owner.transform, false);
            var chest = new GameObject("Chest"); chest.transform.SetParent(root.transform, false);
            var torso = chest.AddComponent<Rigidbody>();
            var left = MakeArm(chest.transform, torso, "Left");
            var right = MakeArm(chest.transform, torso, "Right");
            var ragdoll = root.AddComponent<Ragdoll>();
            ragdoll.targetHip = chest.transform;
            ragdoll.leftHandTarget = left.transform.GetChild(0);
            ragdoll.rightHandTarget = right.transform.GetChild(0);
            ragdoll.Prepare(owner.transform);
            for (int death = 0; death < 3; death++)
            {
                Vector3 rotation = death == 2 ? Vector3.zero : new Vector3(12, 1, 0);
                ragdoll.PoseToPlayer(owner.transform, new RagdollLaunch { rotation = rotation });
                Require(torso.angularVelocity == rotation, "arm kicks leave torso unchanged");
                Require(left.linearVelocity == Vector3.zero && right.linearVelocity == Vector3.zero, "arm kicks do not change launch velocity");
                if (death == 2)
                    Require(left.angularVelocity == Vector3.zero && right.angularVelocity == Vector3.zero, "arm kick clears on next non-zombie death");
                else
                {
                    Require(left.angularVelocity.magnitude > 0 && left.angularVelocity.magnitude <= 2.24f, "left arm kick bounded");
                    Require(right.angularVelocity.magnitude > 0 && right.angularVelocity.magnitude <= 2.24f, "right arm kick bounded");
                    Require(left.angularVelocity != right.angularVelocity, "arms have different kick directions");
                    if (peer == 0) { firstLeft = left.angularVelocity; firstRight = right.angularVelocity; }
                    else Require(left.angularVelocity == firstLeft && right.angularVelocity == firstRight, "same replicated rotation gives same arm kicks regardless of avatar heading");
                }
                ragdoll.Park();
            }
            UnityEngine.Object.DestroyImmediate(root);
            UnityEngine.Object.DestroyImmediate(owner);
        }
        Debug.Log("Arm kick checks PASSED: inactive cache, bounded asymmetric kicks, shared peer result, pooled reset, unchanged torso and linear velocities.");
    }

    static Rigidbody MakeArm(Transform parent, Rigidbody torso, string name)
    {
        var arm = new GameObject(name); arm.transform.SetParent(parent, false);
        var body = arm.AddComponent<Rigidbody>();
        arm.AddComponent<CharacterJoint>().connectedBody = torso;
        var hand = new GameObject("Hand"); hand.transform.SetParent(arm.transform, false);
        hand.AddComponent<Rigidbody>();
        return body;
    }

    static void Require(bool condition, string message)
    {
        if (!condition) throw new Exception("Ragdoll collision check failed: " + message);
    }
}
