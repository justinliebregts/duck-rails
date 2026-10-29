using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class DuckCompanionVisualChecks
{
    const string Folder = "Assets/Skins/DuckSkin_Companion/";
    static void Check(bool condition, string description)
    {
        if (!condition) throw new InvalidOperationException(description);
    }
    [MenuItem("Tools/DuckZ/Validation/Companion/Validate Companion Appearance")]
    public static void Run()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/DuckCompanion.prefab");
        var player = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Photon/PhotonPlayer.prefab");
        var source = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "DuckSkin_Companion.fbx")
            .GetComponentInChildren<SkinnedMeshRenderer>();
        var eyes = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "Eyes_Companion.fbx");
        Check(PrefabUtility.GetPrefabAssetType(prefab) == PrefabAssetType.Variant, "Companion must retain its shared rig variant.");
        for (int peer = 0; peer < 2; peer++)
        {
            var instance = UnityEngine.Object.Instantiate(prefab);
            try
            {
                var body = instance.GetComponentsInChildren<SkinnedMeshRenderer>(true).First(r => r.name == "Duck_Body");
                Check(body.sharedMesh == source.sharedMesh, "Companion body mesh missing.");
                Check(body.sharedMaterial == AssetDatabase.LoadAssetAtPath<Material>(Folder + "DuckSkin_Companion.mat"), "Body material mismatch.");
                Check(body.bones.Select(b => b.name).SequenceEqual(source.bones.Select(b => b.name)), "Bone ordering mismatch.");
                Check(body.bones.All(b => b.IsChildOf(instance.transform)), "Mesh references a different skeleton.");
                foreach (string name in new[] { "Eye_L", "Eye_R", "Lip_T", "Lip_B" })
                {
                    var part = instance.GetComponentsInChildren<MeshFilter>(true).First(r => r.name == name);
                    var original = player.GetComponentsInChildren<MeshFilter>(true).First(r => r.name == name);
                    bool eye = name.StartsWith("Eye_");
                    var mesh = eye ? eyes.GetComponentsInChildren<MeshFilter>(true).First(r => r.name == name).sharedMesh : original.sharedMesh;
                    Check(part.sharedMesh == mesh, "Face mesh mismatch: " + name);
                    Check(part.transform.localPosition == original.transform.localPosition
                        && Quaternion.Angle(part.transform.localRotation, original.transform.localRotation) < 0.001f
                        && part.transform.localScale == original.transform.localScale, "Face animation transform changed: " + name);
                    var material = AssetDatabase.LoadAssetAtPath<Material>(Folder + (eye ? "Eyes_Companion.mat" : "Lips_Companion.mat"));
                    Check(part.GetComponent<Renderer>().sharedMaterial == material, "Face material mismatch: " + name);
                    Check(original.GetComponent<Renderer>().sharedMaterial != material, "Player material was modified: " + name);
                }
                var originalBody = player.GetComponentsInChildren<SkinnedMeshRenderer>(true).First(r => r.name == "Duck_Body");
                Check(originalBody.sharedMesh != source.sharedMesh, "Regular player body was modified.");
                // Exercise a rig-driven pose change without replacing IK/facial transforms.
                var before = new Mesh(); var after = new Mesh();
                try
                {
                    body.BakeMesh(before);
                    var hand = body.bones.First(b => b.name == "hand.L");
                    hand.localRotation *= Quaternion.Euler(20, 0, 0);
                    body.BakeMesh(after);
                    var a = before.vertices; var b = after.vertices;
                    Check(a.Length == b.Length && a.Where((v, i) => (v - b[i]).sqrMagnitude > 0.000001f).Any(), "Existing hand bone does not deform new mesh.");
                }
                finally { UnityEngine.Object.DestroyImmediate(before); UnityEngine.Object.DestroyImmediate(after); }
                DuckSkinVisuals.Attach(body.transform.parent.gameObject, DuckSkinSource.Manual).Apply(0);
                Check(body.sharedMesh == source.sharedMesh, "Manual initialization overwrote companion skin.");
                ValidateCorpse(instance.transform, body);
                ValidateCorpse(instance.transform, body, true);
                ValidateKillerEyes(instance);
            }
            finally { UnityEngine.Object.DestroyImmediate(instance); }
        }
        Debug.Log("COMPANION APPEARANCE CHECKS PASSED: two instances, living/corpse meshes and materials, independent corpse bones, face transforms, player isolation and manual initialization.");
    }

    static void ValidateKillerEyes(GameObject instance)
    {
        var duck = instance.GetComponent<DuckCompanion>() ?? instance.AddComponent<DuckCompanion>();
        var update = typeof(DuckCompanion).GetMethod("UpdateKillerEyes",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var eyes = instance.GetComponentsInChildren<Renderer>(true)
            .Where(r => r.name == "Eye_L" || r.name == "Eye_R").ToArray();
        int emission = Shader.PropertyToID("_EmissionColor");
        Color[] defaults = eyes.Select(r => r.sharedMaterial.GetColor(emission)).ToArray();
        var block = new MaterialPropertyBlock();
        update.Invoke(duck, new object[] { true });
        for (int i = 0; i < eyes.Length; i++)
        {
            eyes[i].GetPropertyBlock(block);
            Check(block.GetColor(emission) == Color.red, "Killer eye emission must be RGB 255,0,0.");
            Check(eyes[i].sharedMaterial.GetColor(emission) == defaults[i], "Shared eye material changed.");
        }
        update.Invoke(duck, new object[] { false });
        for (int i = 0; i < eyes.Length; i++)
        {
            eyes[i].GetPropertyBlock(block);
            Check(block.GetColor(emission) == defaults[i], "Normal eye emission was not restored.");
        }
    }

    static void ValidateCorpse(Transform companion, SkinnedMeshRenderer livingBody, bool redEyes = false)
    {
        var asset = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player/DuckSkin_Ragdoll.prefab");
        var corpse = UnityEngine.Object.Instantiate(asset);
        var regular = UnityEngine.Object.Instantiate(asset);
        try
        {
            // The isolated fixture imports scripts from a precompiled assembly;
            // add the component if the original script GUID cannot resolve there.
            var ragdoll = corpse.GetComponent<Ragdoll>() ?? corpse.AddComponent<Ragdoll>();
            ragdoll.ApplyCompanionAppearance(companion, redEyes);
            var body = corpse.GetComponentsInChildren<SkinnedMeshRenderer>(true).OrderByDescending(r => r.bones.Length).First();
            Check(body.sharedMesh == livingBody.sharedMesh && body.sharedMaterials.SequenceEqual(livingBody.sharedMaterials),
                "Corpse does not match companion body.");
            Check(body.bones.All(b => b != null && b.IsChildOf(corpse.transform)), "Corpse borrows living bones.");
            Check(body.rootBone != null && body.rootBone.IsChildOf(corpse.transform), "Corpse root bone is not independent.");
            foreach (string name in new[] { "Eye_L", "Eye_R", "Lip_T", "Lip_B" })
            {
                var from = companion.GetComponentsInChildren<MeshFilter>(true).First(m => m.name == name);
                var to = corpse.GetComponentsInChildren<MeshFilter>(true).First(m => m.name == name);
                var untouched = regular.GetComponentsInChildren<MeshFilter>(true).First(m => m.name == name);
                Check(to.sharedMesh == from.sharedMesh && to.GetComponent<Renderer>().sharedMaterials.SequenceEqual(from.GetComponent<Renderer>().sharedMaterials),
                    "Corpse face mismatch: " + name);
                Check(untouched.GetComponent<Renderer>().sharedMaterial != to.GetComponent<Renderer>().sharedMaterial,
                    "Shared player ragdoll material changed: " + name);
                if (redEyes && name.StartsWith("Eye_"))
                {
                    var eyeProperties = new MaterialPropertyBlock();
                    to.GetComponent<Renderer>().GetPropertyBlock(eyeProperties);
                    Check(eyeProperties.GetColor("_EmissionColor") == Color.red, "Killer corpse lost red eye emission.");
                }
            }
            // The appearance must survive the live duck moving/respawning.
            companion.gameObject.SetActive(false);
            var baked = new Mesh();
            try { body.BakeMesh(baked); Check(baked.vertexCount == livingBody.sharedMesh.vertexCount, "Corpse cannot deform independently."); }
            finally { UnityEngine.Object.DestroyImmediate(baked); companion.gameObject.SetActive(true); }
            var block = new MaterialPropertyBlock();
            body.GetPropertyBlock(block);
            Check(block.GetFloat("_DuckTintEnabled") == 0, "Corpse inherited a human player's tint.");
        }
        finally { UnityEngine.Object.DestroyImmediate(corpse); UnityEngine.Object.DestroyImmediate(regular); }
    }
}
