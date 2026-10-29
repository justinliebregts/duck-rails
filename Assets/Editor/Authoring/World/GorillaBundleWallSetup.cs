#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Clones the helicopter store's NinjaBundle into a GorillaBundle with gorilla keys and meshes.
/// Menu: Tools/DuckZ/Authoring/Store/Add Gorilla Bundle Wall. Also batch: -executeMethod GorillaBundleWallSetup.Run
/// </summary>
public static class GorillaBundleWallSetup
{
    const string HelicopterPath = "Assets/Prefabs/Photon/PhotonHelicopter.prefab";
    const string SkinPath = "Assets/Skins/DuckSkin_Gorilla/DuckSkin_Gorilla.fbx";
    const string BagPath = "Assets/Skins/Bag_Gorilla/Bag_Gorilla.fbx";
    const string WeaponPath = "Assets/Models/WeaponGorilla/WeaponGorilla.fbx";

    [MenuItem("Tools/DuckZ/Authoring/Store/Add Gorilla Bundle Wall")]
    public static void Run()
    {
        var root = PrefabUtility.LoadPrefabContents(HelicopterPath);
        try
        {
            Transform ninja = FindDeep(root.transform, "NinjaBundle");
            if (ninja == null)
                throw new System.Exception("NinjaBundle not found on PhotonHelicopter.");
            if (FindDeep(root.transform, "GorillaBundle") != null)
            {
                Debug.Log("GorillaBundle already exists on PhotonHelicopter; skipping clone.");
                return;
            }

            GameObject gorilla = Object.Instantiate(ninja.gameObject, ninja.parent);
            gorilla.name = "GorillaBundle";
            gorilla.transform.SetSiblingIndex(ninja.GetSiblingIndex() + 1);
            // Same spacing as Ninja vs Rambro along the wall (z).
            Vector3 pos = ninja.localPosition;
            Transform rambro = FindDeep(root.transform, "RambroBundle");
            float step = rambro != null ? ninja.localPosition.z - rambro.localPosition.z : -2.655f;
            if (Mathf.Abs(step) < 0.01f) step = -2.655f;
            pos.z = ninja.localPosition.z + step;
            gorilla.transform.localPosition = pos;
            gorilla.transform.localRotation = ninja.localRotation;
            gorilla.transform.localScale = ninja.localScale;

            RenameDeep(gorilla.transform, "Ninja", "Gorilla");
            RenameDeep(gorilla.transform, "ninja", "gorilla");

            foreach (StoreBundleLabel label in gorilla.GetComponentsInChildren<StoreBundleLabel>(true))
            {
                var so = new SerializedObject(label);
                so.FindProperty("bundleSku").stringValue = "duckz_gorilla_bundle";
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            foreach (StoreWallItem wall in gorilla.GetComponentsInChildren<StoreWallItem>(true))
            {
                var so = new SerializedObject(wall);
                SerializedProperty key = so.FindProperty("itemKey");
                key.stringValue = key.stringValue switch
                {
                    "skin_duck_ninja" => "skin_duck_gorilla",
                    "bag_ninja" => "bag_gorilla",
                    "weapon_ninja_knife" => "weapon_gorilla",
                    _ => key.stringValue
                };
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            ReplaceDisplayMesh(FindDeep(gorilla.transform, "GorillaDisplay"), SkinPath, 1f);
            ReplaceDisplayMesh(FindDeep(gorilla.transform, "BagGorillaDisplay"), BagPath, 1f);
            ReplaceDisplayMesh(FindDeep(gorilla.transform, "GorillaKnifeDisplay"), WeaponPath, 0.5f);

            PrefabUtility.SaveAsPrefabAsset(root, HelicopterPath);
            Debug.Log("Added GorillaBundle to PhotonHelicopter store wall.");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }

        if (Application.isBatchMode)
            EditorApplication.Exit(0);
    }

    static void ReplaceDisplayMesh(Transform display, string modelPath, float localScale)
    {
        if (display == null)
        {
            Debug.LogWarning($"[GorillaBundle] Missing display for {modelPath}");
            return;
        }

        var toDestroy = new List<GameObject>();
        for (int i = 0; i < display.childCount; i++)
            toDestroy.Add(display.GetChild(i).gameObject);
        foreach (GameObject child in toDestroy)
            Object.DestroyImmediate(child);

        GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
        if (source == null)
            throw new System.Exception("Missing model: " + modelPath);

        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(source, display);
        instance.name = source.name;
        instance.transform.localPosition = Vector3.zero;
        instance.transform.localRotation = Quaternion.identity;
        instance.transform.localScale = Vector3.one * localScale;
    }

    static void RenameDeep(Transform root, string from, string to)
    {
        if (root.name.Contains(from))
            root.name = root.name.Replace(from, to);
        foreach (Transform child in root)
            RenameDeep(child, from, to);
    }

    static Transform FindDeep(Transform root, string name)
    {
        if (root.name == name)
            return root;
        foreach (Transform child in root)
        {
            Transform found = FindDeep(child, name);
            if (found != null)
                return found;
        }
        return null;
    }
}
#endif
