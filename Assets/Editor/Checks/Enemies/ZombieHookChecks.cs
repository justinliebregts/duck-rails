using System;
using System.Reflection;
using Game.AI;
using Game.Animation;
using Game.Audio;
using Photon.Pun;
using UnityEditor;
using UnityEngine;

public static class ZombieHookChecks
{
    const string PrefabPath = "Assets/Prefabs/Enemies/ZombieHook.prefab";

    [MenuItem("Tools/Checks/Enemies/Validate ZombieHook")]
    public static void Run()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        var attack = new SerializedObject(prefab.GetComponent<ZombieHookAttack>());
        var brain = new SerializedObject(prefab.GetComponent<Enemy>());
        Check(Reference<Transform>(brain, "eye") == Reference<Transform>(brain, "head"),
            "ZombieHook must look for elevated players from its head.");
        var head = Reference<Transform>(attack, "hookHead");
        var point = Reference<Transform>(attack, "hookPoint");
        Check(head.parent.name == "hand.R" && point.parent == head && point.localPosition.sqrMagnitude > 0f,
            "The detachable hook must be attached to the right wrist with a leading contact point.");
        var hookMesh = head.GetComponent<MeshFilter>().sharedMesh;
        var hookRenderer = head.GetComponent<MeshRenderer>();
        Check(hookMesh != null && AssetDatabase.IsSubAsset(hookMesh) && hookMesh.GetIndexCount(0) > 0
            && head.gameObject.activeSelf && hookRenderer.enabled && hookRenderer.sharedMaterial != null,
            "The imported wrist hook must have persistent, visible geometry.");
        var limbs = prefab.GetComponentsInChildren<TwoBoneLimb>(true);
        Check(limbs.Length == 2 && Reference<TwoBoneLimb>(attack, "arm").EndBone == head.parent, "Right arm binding");
        foreach (var limb in limbs) Check(limb.CanPreviewIK, "Incomplete IK chain on " + limb.name);
        foreach (var leg in prefab.GetComponentsInChildren<SpiderLeg>(true))
            Reference<Transform>(new SerializedObject(leg), "footTip");
        var chain = Reference<MeshFilter>(attack, "chain");
        var mesh = chain.sharedMesh;
        float range = attack.FindProperty("range").floatValue;
        Check(Mathf.Approximately(range, 20f) && brain.FindProperty("visionRange").floatValue >= range,
            "Full-range hook acquisition");
        Check(attack.FindProperty("flightTime").floatValue > 0f, "Hook flight time must be positive.");
        const int links = 64;
        Check(mesh.isReadable && mesh.subMeshCount == 1 && mesh.GetIndexCount(0) == links * 108
            && Mathf.Abs(mesh.bounds.size.z - range) < 0.001f, "Chain mesh or import settings");
        Check(!chain.gameObject.activeSelf && chain.GetComponent<Collider>() == null,
            "The retracted chain must be excluded from the prefab preview bounds.");
        // The runtime draws a prefix of this index buffer to pay out complete links.
        // Unity's mesh optimizer can reorder it without changing the total triangle count.
        var vertices = mesh.vertices;
        var indices = mesh.triangles;
        for (int link = 0; link < links; link++)
        {
            float min = float.PositiveInfinity, max = float.NegativeInfinity;
            for (int i = link * 108; i < (link + 1) * 108; i++)
            {
                float z = vertices[indices[i]].z;
                min = Mathf.Min(min, z);
                max = Mathf.Max(max, z);
            }
            Check(Mathf.Abs(min - link * ((range - 0.36f) / (links - 1))) < 0.001f
                && Mathf.Abs(max - min - 0.36f) < 0.001f, "Chain link ordering at link " + link);
        }
        foreach (string field in new[] { "throwSound", "solidSound", "fleshSound" })
        {
            var sound = new SerializedObject(Reference<SoundEvent>(attack, field));
            var clips = sound.FindProperty("clips");
            Check(clips.arraySize > 0 && clips.GetArrayElementAtIndex(0).objectReferenceValue != null, field);
        }
        Check(prefab.GetComponentsInChildren<PhotonView>(true).Length == 1
            && prefab.GetComponent<PhotonView>().ObservedComponents.Contains(prefab.GetComponent<EnemyNetSync>()),
            "Chain and hook must use the enemy's PhotonView.");
        CheckDefenderReach(prefab);
        Debug.Log("ZombieHook prefab, procedural rig, ordered chain mesh, impact sounds and Photon bindings verified.");
    }

    static void CheckDefenderReach(GameObject prefab)
    {
        // Use an inactive fixture so the check can run without entering or disturbing a room.
        const BindingFlags fields = BindingFlags.NonPublic | BindingFlags.Instance;
        var fixture = new GameObject("Hook reach check");
        fixture.SetActive(false);
        try
        {
            var hook = UnityEngine.Object.Instantiate(prefab, fixture.transform).GetComponent<ZombieHookAttack>();
            fixture.AddComponent<PhotonView>();
            var player = fixture.AddComponent<PhotonPlayer>();
            var body = fixture.AddComponent<RemotePlayerBody>();
            var head = new GameObject("Tracked head").transform;
            head.SetParent(fixture.transform);
            typeof(RemotePlayerBody).GetField("head", fields).SetValue(body, head);
            typeof(PhotonPlayer).GetField("body", fields).SetValue(player, body);
            var type = typeof(ZombieHookAttack);
            type.GetField("origin", fields).SetValue(hook, Vector3.zero);
            type.GetField("shotDirection", fields).SetValue(hook, Vector3.forward);
            type.GetField("shotLength", fields).SetValue(hook, 20f);
            var recordType = type.GetNestedType("Defense", BindingFlags.NonPublic);
            object record = Activator.CreateInstance(recordType);
            recordType.GetField("player").SetValue(record, player);
            recordType.GetField("firstReach").SetValue(record, float.PositiveInfinity);
            recordType.GetField("lastHeld").SetValue(record, double.NegativeInfinity);
            void Sweep(float from, float to, Vector3 trackedHead)
            {
                head.position = trackedHead;
                type.GetField("sweptDistance", fields).SetValue(hook, from);
                object[] args = { record, to, 1d };
                type.GetMethod("ObserveReach", fields).Invoke(hook, args);
                record = args[0];
            }
            bool Reached(float distance)
            {
                int bin = (int)type.GetMethod("ReachBin", fields).Invoke(hook, new object[] { distance });
                return ((ulong)recordType.GetField("reachable").GetValue(record) & (1UL << bin)) != 0;
            }
            Sweep(0f, 2f, Vector3.forward * 9f);
            Check(float.IsPositiveInfinity((float)recordType.GetField("firstReach").GetValue(record)),
                "A bystander beyond a wall cannot delay the wall impact.");
            Sweep(2f, 6f, Vector3.forward * 6f);
            Sweep(6f, 14f, Vector3.right * 20f);
            Sweep(14f, 20f, Vector3.forward * 19f);
            Check(Reached(5f) && Reached(20f) && !Reached(1f) && !Reached(10f),
                "Contact must be in a portion of the shot the player could reach while the tip passed.");
        }
        finally { UnityEngine.Object.DestroyImmediate(fixture); }
    }

    static T Reference<T>(SerializedObject component, string name) where T : UnityEngine.Object
    {
        var value = component.FindProperty(name)?.objectReferenceValue as T;
        Check(value != null, "Missing " + name);
        return value;
    }

    static void Check(bool ok, string message)
    {
        if (!ok) throw new InvalidOperationException(message);
    }
}
