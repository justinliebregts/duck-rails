using UnityEditor;
using UnityEngine;

namespace Game.AI.Editor
{
    [CustomEditor(typeof(CrawlGraphBaker))]
    public class CrawlGraphBakerEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            CrawlGraphBaker baker = (CrawlGraphBaker)target;

            EditorGUILayout.Space();
            using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
            {
                if (GUILayout.Button("Bake", GUILayout.Height(28)))
                    CrawlGraphBake.Bake(baker, false);

                using (new EditorGUI.DisabledScope(baker.graphAsset == null))
                {
                    if (GUILayout.Button("Rebake Region"))
                        CrawlGraphBake.Bake(baker, true);
                }

                if (GUILayout.Button("Move Region To Scene View Pivot") && SceneView.lastActiveSceneView != null)
                {
                    Undo.RecordObject(baker, "Move Rebake Region");
                    baker.rebakeRegion.center = SceneView.lastActiveSceneView.pivot;
                    SceneView.RepaintAll();
                }
            }
        }
    }
}
