using UnityEditor;
using UnityEngine;
using Liv.Lck.Tablet;

namespace Liv.Lck
{
    [CustomEditor(typeof(LckNotificationController))]
    public class LckNotificationControllerEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            LckNotificationController controller = (LckNotificationController)target;

            EditorGUILayout.Space();

            EditorGUILayout.LabelField("Editor Tools", EditorStyles.boldLabel);
            if (GUILayout.Button("Refresh Notification Gameobjects"))
            {
                controller.InitializeNotifications();
            }
        }
    }
}