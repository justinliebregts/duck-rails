using System;
using UnityEditor;
using UnityEngine;

/// <summary>Keep URP's Lit inspector and validation, adding the shared duck tint control.</summary>
public sealed class DuckBodyTintShaderGUI : ShaderGUI
{
    // URP's LitShader inspector is internal, so compose it rather than subclassing it.
    readonly ShaderGUI lit = (ShaderGUI)Activator.CreateInstance(Type.GetType(
        "UnityEditor.Rendering.Universal.ShaderGUI.LitShader, Unity.RenderPipelines.Universal.Editor", true));

    public override void OnGUI(MaterialEditor editor, MaterialProperty[] properties)
    {
        editor.ShaderProperty(FindProperty("_DuckTintStrength", properties),
            new GUIContent("Tint Strength", "0 = original texture, 1 = full assigned color. Default yellow always remains unchanged."));
        EditorGUILayout.Space();
        lit.OnGUI(editor, properties);
    }

    public override void ValidateMaterial(Material material) => lit.ValidateMaterial(material);

    public override void AssignNewShaderToMaterial(Material material, Shader oldShader, Shader newShader)
        => lit.AssignNewShaderToMaterial(material, oldShader, newShader);
}
