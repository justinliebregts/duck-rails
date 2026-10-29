using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(HelicopterGroundDust))]
public sealed class HelicopterGroundDustEditor : Editor
{
    public override void OnInspectorGUI()
    {
        var dust = (HelicopterGroundDust)target;
        if (DrawDefaultInspector() && Application.isPlaying)
            dust.RefreshPreviewSettings();

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Dust Preview (Play Mode)", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Enter Play mode and select a dust instance in the scene. Play overrides the helicopter's " +
            "control for this effect. Changes made during Play mode are temporary.",
            MessageType.Info);

        bool canPreview = Application.isPlaying && !EditorUtility.IsPersistent(dust)
            && dust.gameObject.scene.IsValid() && dust.isActiveAndEnabled;
        using (new EditorGUI.DisabledScope(!canPreview))
        {
            EditorGUI.BeginChangeCheck();
            float height = EditorGUILayout.Slider("Preview Height (m)", dust.PreviewHeight,
                0f, Mathf.Max(0.01f, dust.activationHeight));
            if (EditorGUI.EndChangeCheck())
                dust.PreviewAtHeight(height);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Play at Ground"))
                    dust.PreviewAtHeight(0f);
                if (GUILayout.Button("Stop / Let Settle"))
                    dust.PreviewAtHeight(Mathf.Max(0.01f, dust.activationHeight));
            }
            if (GUILayout.Button("Return to Helicopter Control"))
                dust.EndPreview();

            EditorGUILayout.LabelField("Control", dust.PreviewActive ? "Manual preview" : "Helicopter");
            EditorGUILayout.LabelField("Live Particles", dust.PreviewParticleCount.ToString());
        }
    }

    public override bool RequiresConstantRepaint() => Application.isPlaying;
}
