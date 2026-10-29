using System.Collections.Generic;
using System.Linq;
using UnityEditor;

namespace Game.Audio.Editor
{
    [CustomEditor(typeof(SurfaceSoundSet))]
    public sealed class SurfaceSoundSetEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            var set = (SurfaceSoundSet)target;
            var fallback = set.Resolve((SurfaceType)null);
            var missing = new List<string>();
            foreach (var guid in AssetDatabase.FindAssets("t:SurfaceType"))
            {
                var type = AssetDatabase.LoadAssetAtPath<SurfaceType>(AssetDatabase.GUIDToAssetPath(guid));
                if (type != null && set.Resolve(type) == fallback) missing.Add(type.name);
            }
            EditorGUILayout.Space();
            if (fallback == null)
                EditorGUILayout.HelpBox("No fallback: untagged surfaces will be silent.", MessageType.Warning);
            if (missing.Count > 0)
                EditorGUILayout.HelpBox("Using the fallback for: " + string.Join(", ", missing.OrderBy(n => n)), MessageType.Info);
        }
    }
}
