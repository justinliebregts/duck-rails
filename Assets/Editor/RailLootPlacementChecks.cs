#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class RailLootPlacementChecks
{
    [MenuItem("Duck Rails/Tests/Loot Placement")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
        var scene = EditorSceneManager.NewPreviewScene();
        var root = new GameObject("Loot placement fixture");
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, scene);
        try
        {
            var b = root.AddComponent<RailBlock>();
            b.variations = new[] { new RailSpawnSet(), new RailSpawnSet() };
            root.transform.SetPositionAndRotation(new Vector3(20, 3, -7), Quaternion.Euler(0, 30, 0));
            var position = new Vector3(23, 4, -5);
            var marker = RailLootPlacementWindow.Place(b, 1, false, false, position);
            Require(b.variations[1].coal.Length == 1 && b.variations[0].coal == null && Vector3.Distance(marker.position, position) < .001f && marker.GetComponent<CoalSpawner>() != null, "Coal registration / transformed parent");
            Undo.PerformUndo();
            Require((b.variations[1].coal == null || b.variations[1].coal.Length == 0) && root.GetComponentsInChildren<CoalSpawner>().Length == 0, "Coal undo object and reference");
            Undo.PerformRedo();
            Require(b.variations[1].coal.Length == 1 && b.variations[1].coal[0] != null, "Coal redo");
            marker = RailLootPlacementWindow.Place(b, 0, true, true, position);
            Require(b.variations[0].gold[0] == marker && b.variations[1].gold[0] == marker && marker.GetComponent<RailGoldSpawner>() != null, "Gold all variations");
            Undo.PerformUndo();
            Require(b.variations[0].gold.Length == 0 && b.variations[1].gold.Length == 0, "Gold undo");
            Undo.PerformRedo();
            Require(b.variations[0].gold[0] != null && b.variations[0].gold[0] == b.variations[1].gold[0], "Gold redo");
            foreach (bool gold in new[] { false, true })
            {
                var bounds = RailLootPlacementWindow.ItemBounds(gold);
                Require(bounds.size.y > 0 && bounds.size.x < 2, "Runtime item bounds");
                Debug.Log((gold ? "Gold" : "Coal") + " spawn bounds: " + bounds);
            }
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(floor, scene);
            floor.transform.SetParent(root.transform);
            floor.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            floor.layer = LayerMask.NameToLayer("Environment");
            var colliders = new[] { floor.GetComponent<Collider>() };
            Physics.SyncTransforms();
            var ray = new Ray(Vector3.up * 5, Vector3.down);
            Require(RailLootPlacementWindow.FloorHit(colliders, ray, out _), "Preview-scene Environment floor");
            Require(!RailLootPlacementWindow.FloorHit(colliders, new Ray(Vector3.right * 5, Vector3.left), out _), "Wall rejection");
            floor.layer = 0;
            Require(!RailLootPlacementWindow.FloorHit(colliders, ray, out _), "Wrong layer rejection");
            floor.layer = LayerMask.NameToLayer("Environment");
            colliders[0].isTrigger = true;
            Require(!RailLootPlacementWindow.FloorHit(colliders, ray, out _), "Trigger rejection");
            Debug.Log("LOOT PLACEMENT: 12 checks passed.");
        }
        finally
        {
            foreach (var c in root.GetComponentsInChildren<Component>()) Undo.ClearUndo(c);
            foreach (var t in root.GetComponentsInChildren<Transform>()) Undo.ClearUndo(t.gameObject);
            EditorSceneManager.ClosePreviewScene(scene);
        }
    }
    static void Require(bool value, string label) { if (!value) throw new Exception(label); }
}
#endif
