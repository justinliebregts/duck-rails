using Liv.Lck.Tablet;
using UnityEditor;
using UnityEngine;

namespace Liv.Lck
{
    [CustomEditor(typeof(LckSplitNotification))]
    public class LckSplitNotificationEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            LckSplitNotification notification = (LckSplitNotification)target;

            EditorGUILayout.Space();

            if (GUILayout.Button("Show Android UI"))
            {
                notification.AndroidUI.SetActive(true);
                notification.DesktopUI.SetActive(false);
            }

            if (GUILayout.Button("Show Desktop UI"))
            {
                notification.DesktopUI.SetActive(true);
                notification.AndroidUI.SetActive(false);
            }
        }
    }
}