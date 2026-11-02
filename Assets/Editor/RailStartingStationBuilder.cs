#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using TMPro;
using Object = UnityEngine.Object;

public static class RailStartingStationBuilder
{
    public const string PrefabPath = "Assets/DuckRails/StartingStation.prefab";

    // Keep this asset separate from the random block catalogue. Preserve later hand-authored edits.
    static GameObject GetPrefab()
    {
        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (existing != null) return existing;
        var root = new GameObject("Starting Station");
        try
        {
            Material Mat(string name) => AssetDatabase.LoadAssetAtPath<Material>("Assets/DuckRails/Materials/" + name + ".mat");
            Transform Point(string name, Vector3 position)
            {
                var t = new GameObject(name).transform; t.SetParent(root.transform, false); t.localPosition = position; return t;
            }
            GameObject Box(string name, Vector3 position, Vector3 size, string material, bool collision = true)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.name = name; go.layer = 6;
                go.transform.SetParent(root.transform, false); go.transform.localPosition = position; go.transform.localScale = size;
                go.GetComponent<Renderer>().sharedMaterial = Mat(material);
                if (!collision) Object.DestroyImmediate(go.GetComponent<Collider>());
                return go;
            }
            var block = root.AddComponent<RailBlock>();
            block.id = "starting-station-v1"; block.displayName = "Departure Station";
            block.length = RailRoute.StationLength; block.tags = new[] { "fixed-start", "safe" };
            block.entry = Point("Track Entry", Vector3.zero);
            block.exit = Point("Track Exit", Vector3.forward * block.length);
            block.stop = Point("Train Start", Vector3.forward * (block.length / 2));
            block.track = new[] { Vector3.zero, Vector3.forward * block.length };
            block.variations = Array.Empty<RailSpawnSet>();
            Box("Station ground", new Vector3(0, -.5f, 80), new Vector3(96, 1, 160), "Ground");
            foreach (float x in new[] { -1.5f, 1.5f })
                Box("Track rail", new Vector3(x, .06f, 80), new Vector3(.12f, .12f, 160), "Metal", false);
            for (int z = 2; z < 160; z += 4)
                Box("Sleeper", new Vector3(0, .02f, z), new Vector3(3.8f, .04f, .35f), "Metal", false);
            foreach (float side in new[] { -1f, 1f })
            {
                Box("Boarding platform", new Vector3(side * 7, .4f, 80), new Vector3(8, .8f, 40), "Station");
                Box("Platform safety edge", new Vector3(side * 3.2f, .81f, 80), new Vector3(.3f, .02f, 40), "Safety");
                for (int step = 0; step < 4; step++)
                    Box("Platform access step", new Vector3(side * 7, .1f * (step + 1), 57 + step), new Vector3(6, .2f * (step + 1), 1), "Station");
                foreach (float z in new[] { 66f, 94f })
                    Box("Canopy post", new Vector3(side * 10, 2.5f, z), new Vector3(.3f, 3.4f, .3f), "Metal");
                Box("Platform canopy", new Vector3(side * 7, 4.3f, 80), new Vector3(9, .25f, 32), "Metal");
                foreach (float z in new[] { 70f, 90f })
                {
                    Box("Bench seat", new Vector3(side * 9, 1.3f, z), new Vector3(1, .2f, 4), "Safety");
                    Box("Bench base", new Vector3(side * 9, 1, z), new Vector3(.6f, .4f, 3), "Metal");
                }
            }
            Box("Station hall back", new Vector3(24, 2, 80), new Vector3(.4f, 4, 24), "Station");
            foreach (float z in new[] { 68f, 92f })
                Box("Station hall side", new Vector3(18, 2, z), new Vector3(12, 4, .4f), "Station");
            Box("Station hall roof", new Vector3(18, 4.2f, 80), new Vector3(13, .3f, 25), "Metal");
            Point("Future Store Area", new Vector3(-22, 0, 80));
            var label = Point("Departure station sign", new Vector3(12, 3, 80)).gameObject.AddComponent<TextMeshPro>();
            label.text = "DUCK RAILS\nDEPARTURE STATION"; label.fontSize = 3; label.alignment = TextAlignmentOptions.Center;
            label.rectTransform.sizeDelta = new Vector2(10, 3); label.transform.localRotation = Quaternion.Euler(0, 90, 0);
            return PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        finally { Object.DestroyImmediate(root); }
    }

    public static void Install(RailRoute route)
    {
        if (route.startingStation == null)
        {
            var station = (GameObject)PrefabUtility.InstantiatePrefab(GetPrefab(), route.transform);
            route.startingStation = station.GetComponent<RailBlock>();
        }
        route.startingStation.transform.position = Vector3.forward * (RailRoute.StartZ - RailRoute.StationLength);
    }

    [MenuItem("Duck Rails/Authoring/Install Starting Station")]
    public static void InstallInExpedition()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode first.");
        for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
            if (UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save open scene edits first.");
        var setup = EditorSceneManager.GetSceneManagerSetup();
        try
        {
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/Expedition.unity");
            Install(Object.FindFirstObjectByType<RailRoute>());
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
        }
        finally { EditorSceneManager.RestoreSceneManagerSetup(setup); }
    }
}
#endif
