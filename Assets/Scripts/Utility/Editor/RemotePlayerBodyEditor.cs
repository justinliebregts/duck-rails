using System.Collections.Generic;
using Game.Animation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[CustomEditor(typeof(RemotePlayerBody))]
public sealed class RemotePlayerBodyEditor : Editor
{
    static RemotePlayerBodyEditor activePreview;
    readonly List<TransformPose> savedPose = new List<TransformPose>();
    BodyPose pose;
    bool previewing;
    bool rotateHands;
    bool simulateSmoothing = true;
    double lastPreviewTime;
    bool advanced;
    bool editRegions;
    RemotePlayerBody Body => (RemotePlayerBody)target;

    struct TransformPose
    {
        public Transform transform;
        public Vector3 position, scale;
        public Quaternion rotation;
    }

    void OnEnable()
    {
        EditorApplication.update += Tick;
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
        AssemblyReloadEvents.beforeAssemblyReload += StopPreview;
        EditorSceneManager.sceneSaving += OnSceneSaving;
        PrefabStage.prefabSaving += OnPrefabSaving;
        Undo.undoRedoPerformed += StopPreview;
    }

    void OnDisable()
    {
        StopPreview();
        EditorApplication.update -= Tick;
        EditorApplication.playModeStateChanged -= OnPlayModeChanged;
        AssemblyReloadEvents.beforeAssemblyReload -= StopPreview;
        EditorSceneManager.sceneSaving -= OnSceneSaving;
        PrefabStage.prefabSaving -= OnPrefabSaving;
        Undo.undoRedoPerformed -= StopPreview;
    }

    public override void OnInspectorGUI()
    {
        // Rig references must remain fixed while their transforms are sampled.
        serializedObject.Update();
        using (new EditorGUI.DisabledScope(true))
            EditorGUILayout.PropertyField(serializedObject.FindProperty("m_Script"));
        DrawSimpleControls();
        advanced = EditorGUILayout.Foldout(advanced, "Advanced — rig and detailed tuning", true);
        if (advanced)
        {
        var property = serializedObject.GetIterator();
        bool enterChildren = true;
        while (property.NextVisible(enterChildren))
        {
            enterChildren = false;
            if (property.name == "m_Script") continue;
            bool rigReference = property.name == "head" || property.name == "chest"
                || property.name == "leftArm" || property.name == "rightArm";
            using (new EditorGUI.DisabledScope(property.name == "m_Script" || (previewing && rigReference)))
                EditorGUILayout.PropertyField(property, true);
        }
        }
        serializedObject.ApplyModifiedProperties();
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Scene View IK Preview", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Drag the cyan left-hand and orange right-hand handles in Scene view. Magenta markers show elbow poles. Simulate Smoothing uses gameplay elbow stabilization; hand targets remain immediate. Wrist-roll smoothing is not simulated. Stopping, deselecting, saving, Undo, or entering Play Mode restores the original pose. Inspector tuning values are retained.", MessageType.Info);
        bool canStart = !EditorApplication.isPlayingOrWillChangePlaymode
            && !EditorUtility.IsPersistent(Body) && Body.CanPreviewIK
            && !AnimationMode.InAnimationMode();
        using (new EditorGUI.DisabledScope(!previewing && !canStart))
        {
            if (GUILayout.Button(previewing ? "Stop IK Preview" : "Start IK Preview"))
            {
                if (previewing) StopPreview(); else StartPreview();
            }
        }
        if (!previewing && EditorUtility.IsPersistent(Body))
            EditorGUILayout.HelpBox("Open the prefab in Prefab Mode, or select a scene instance.", MessageType.Info);
        if (previewing)
        {
            simulateSmoothing = GUILayout.Toggle(simulateSmoothing, "Simulate Smoothing");
            editRegions = GUILayout.Toggle(editRegions, "Edit Influence Regions (Scene view)");
            if (editRegions)
                EditorGUILayout.HelpBox("Green: move the body region with its center arrows; drag its edge dots to resize. Yellow: drag the face radius. Turn this off to drag hands again.", MessageType.Info);
            rotateHands = GUILayout.Toggle(rotateHands, "Rotate hand targets (off = move)");
            if (GUILayout.Button("Reset Hand Targets"))
            {
                StopPreview();
                StartPreview();
            }
        }
    }

    void DrawSimpleControls()
    {
        EditorGUILayout.LabelField("Arm posing", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("proximityElbowBias"), new GUIContent("Enable Close-hand Bias"));
        using (new EditorGUI.DisabledScope(!serializedObject.FindProperty("proximityElbowBias").boolValue))
        {
            FlareSlider("Elbow Flare", "torsoElbowWeights");
            FlareSlider("Face Elbow Flare", "faceElbowWeights");
            var radiiProperty = serializedObject.FindProperty("torsoBiasRadii");
            var centerProperty = serializedObject.FindProperty("torsoBiasCenter");
            Vector3 radii = radiiProperty.vector3Value;
            Vector3 center = centerProperty.vector3Value;
            EditorGUI.BeginChangeCheck();
            float coverage = EditorGUILayout.Slider(new GUIContent("Body Coverage", "0 = upper chest, 1 = down toward hips. Extends the region downward while keeping its top fixed."), Mathf.InverseLerp(0.4f, 1.4f, radii.y * 2f), 0f, 1f);
            if (EditorGUI.EndChangeCheck())
            {
                float top = center.y + radii.y;
                radii.y = Mathf.Lerp(0.4f, 1.4f, coverage) * 0.5f;
                center.y = top - radii.y;
                radiiProperty.vector3Value = radii;
                centerProperty.vector3Value = center;
            }
            EditorGUI.BeginChangeCheck();
            float distance = EditorGUILayout.Slider(new GUIContent("Activation Distance", "Body region depth in meters. Resizes body width/depth and face distances proportionally."), radii.z, 0.1f, 0.8f);
            if (EditorGUI.EndChangeCheck())
            {
                float ratio = distance / Mathf.Max(0.001f, radii.z);
                radii.x = Mathf.Max(0.01f, radii.x * ratio);
                radii.z = distance;
                radiiProperty.vector3Value = radii;
                serializedObject.FindProperty("faceBiasDistance").floatValue *= ratio;
                serializedObject.FindProperty("faceFullBiasDistance").floatValue *= ratio;
            }
        }
        var pole = serializedObject.FindProperty("poleSmooth");
        EditorGUI.BeginChangeCheck();
        float softness = EditorGUILayout.Slider(new GUIContent("Smoothness", "0 = responsive, 1 = soft. Adjusts elbow response, influence response and maximum swivel speed together."), Mathf.InverseLerp(24f, 4f, pole.floatValue), 0f, 1f);
        if (EditorGUI.EndChangeCheck())
        {
            pole.floatValue = Mathf.Lerp(24f, 4f, softness);
            serializedObject.FindProperty("elbowInfluenceSmooth").floatValue = Mathf.Lerp(20f, 4f, softness);
            serializedObject.FindProperty("elbowMaxSpeed").floatValue = Mathf.Lerp(480f, 120f, softness);
        }
        EditorGUILayout.LabelField("Flare: tucked → outward     Smoothness: responsive → soft", EditorStyles.miniLabel);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("showElbowBiasRegions"), new GUIContent("Show Influence Regions"));
    }

    void FlareSlider(string label, string propertyName)
    {
        var property = serializedObject.FindProperty(propertyName);
        Vector3 weights = property.vector3Value;
        EditorGUI.BeginChangeCheck();
        float value = EditorGUILayout.Slider(new GUIContent(label, "Outward preference near this region. Backward and downward tuning is preserved in Advanced."), weights.x / 3f, 0f, 1f);
        if (EditorGUI.EndChangeCheck())
        {
            weights.x = value * 3f;
            property.vector3Value = weights;
        }
    }

    void StartPreview()
    {
        if (activePreview != null) activePreview.StopPreview();
        // Include external bone references as well as the component hierarchy.
        var transforms = new HashSet<Transform>(Body.GetComponentsInChildren<Transform>(true));
        foreach (var root in new[] { Body.Head, Body.Chest })
            foreach (var t in root.GetComponentsInChildren<Transform>(true)) transforms.Add(t);
        savedPose.Clear();
        AnimationMode.StartAnimationMode();
        activePreview = this;
        previewing = true;
        foreach (var t in transforms)
        {
            savedPose.Add(new TransformPose { transform = t, position = t.localPosition,
                rotation = t.localRotation, scale = t.localScale });
            RegisterTransform(t);
        }
        Body.InitializeIKPreview();
        lastPreviewTime = EditorApplication.timeSinceStartup;
        pose = new BodyPose { headPos = Body.EyePosition, headRot = Body.Head.rotation,
            lHandPos = Body.LeftHand.position, lHandRot = Body.LeftHand.rotation,
            rHandPos = Body.RightHand.position, rHandRot = Body.RightHand.rotation };
        Tick();
    }

    static void RegisterTransform(Transform transform)
    {
        var serialized = new SerializedObject(transform);
        foreach (var name in new[] { "m_LocalPosition", "m_LocalRotation", "m_LocalScale" })
        {
            var value = serialized.FindProperty(name);
            int count = name == "m_LocalRotation" ? 4 : 3;
            for (int i = 0; i < count; i++)
            {
                string path = name + "." + "xyzw"[i];
                var modification = new PropertyModification { target = transform, propertyPath = path,
                    value = value.FindPropertyRelative("xyzw"[i].ToString()).floatValue.ToString(System.Globalization.CultureInfo.InvariantCulture) };
                AnimationMode.AddPropertyModification(EditorCurveBinding.FloatCurve("", typeof(Transform), path), modification, true);
            }
        }
    }

    void StopPreview()
    {
        if (!previewing) return;
        previewing = false;
        AnimationMode.StopAnimationMode();
        foreach (var saved in savedPose)
        {
            if (!saved.transform) continue;
            saved.transform.localPosition = saved.position;
            saved.transform.localRotation = saved.rotation;
            saved.transform.localScale = saved.scale;
        }
        savedPose.Clear();
        if (activePreview == this) activePreview = null;
        SceneView.RepaintAll();
        Repaint();
    }

    void Tick()
    {
        if (!previewing) return;
        if (!target || !Body.CanPreviewIK || !AnimationMode.InAnimationMode()
            || EditorApplication.isPlayingOrWillChangePlaymode)
        {
            StopPreview();
            return;
        }
        double now = EditorApplication.timeSinceStartup;
        float dt = (float)(now - lastPreviewTime);
        lastPreviewTime = now;
        Body.ApplyIKPreview(pose, simulateSmoothing, dt);
        SceneView.RepaintAll();
    }

    void OnSceneGUI()
    {
        if (!previewing) return;
        if (editRegions)
        {
            DrawRegionHandles();
            return;
        }
        DrawHand("Left hand", Color.cyan, ref pose.lHandPos, ref pose.lHandRot);
        DrawHand("Right hand", new Color(1f, 0.55f, 0.1f), ref pose.rHandPos, ref pose.rHandRot);
        Handles.color = Color.magenta;
        DrawPole(Body.LeftPreviewPole, "Left elbow pole");
        DrawPole(Body.RightPreviewPole, "Right elbow pole");
    }

    void DrawRegionHandles()
    {
        if (!Body.Chest || !Body.Head) return;
        serializedObject.Update();
        var centerProperty = serializedObject.FindProperty("torsoBiasCenter");
        var radiiProperty = serializedObject.FindProperty("torsoBiasRadii");
        Quaternion yaw = Body.Chest.rotation;
        Vector3 center = Body.Chest.position + yaw * centerProperty.vector3Value;
        Vector3 radii = radiiProperty.vector3Value;
        Handles.color = Color.green;
        EditorGUI.BeginChangeCheck();
        center = Handles.PositionHandle(center, yaw);
        for (int axis = 0; axis < 3; axis++)
        {
            Vector3 direction = yaw * (axis == 0 ? Vector3.right : axis == 1 ? Vector3.up : Vector3.forward);
            for (int sign = -1; sign <= 1; sign += 2)
            {
                Vector3 endpoint = center + direction * radii[axis] * sign;
                Vector3 moved = Handles.Slider(endpoint, direction * sign, HandleUtility.GetHandleSize(endpoint) * 0.07f, Handles.DotHandleCap, 0f);
                radii[axis] = Mathf.Max(0.01f, Vector3.Dot(moved - center, direction) * sign);
            }
        }
        if (EditorGUI.EndChangeCheck())
        {
            centerProperty.vector3Value = Quaternion.Inverse(yaw) * (center - Body.Chest.position);
            radiiProperty.vector3Value = radii;
        }
        Handles.Label(center + Vector3.up * (radii.y + 0.05f), "Body influence — drag edges to resize");
        Handles.color = Color.yellow;
        var faceProperty = serializedObject.FindProperty("faceBiasDistance");
        EditorGUI.BeginChangeCheck();
        float radius = Handles.RadiusHandle(Quaternion.identity, Body.EyePosition, faceProperty.floatValue);
        if (EditorGUI.EndChangeCheck())
        {
            radius = Mathf.Max(0.01f, radius);
            serializedObject.FindProperty("faceFullBiasDistance").floatValue *= radius / Mathf.Max(0.001f, faceProperty.floatValue);
            faceProperty.floatValue = radius;
        }
        Handles.Label(Body.EyePosition + Vector3.up * (radius + 0.05f), "Face influence");
        if (serializedObject.ApplyModifiedProperties()) Repaint();
    }

    void DrawHand(string label, Color color, ref Unity.Mathematics.float3 position, ref Unity.Mathematics.quaternion rotation)
    {
        Handles.color = color;
        Vector3 p = position;
        Quaternion r = rotation;
        Handles.Label(p + Vector3.up * 0.06f, label);
        Handles.SphereHandleCap(0, p, Quaternion.identity, HandleUtility.GetHandleSize(p) * 0.06f, EventType.Repaint);
        if (rotateHands) rotation = Handles.RotationHandle(r, p);
        else position = Handles.PositionHandle(p, Tools.pivotRotation == PivotRotation.Local ? r : Quaternion.identity);
    }

    static void DrawPole(Vector3 position, string label)
    {
        Handles.SphereHandleCap(0, position, Quaternion.identity, HandleUtility.GetHandleSize(position) * 0.05f, EventType.Repaint);
        Handles.Label(position, label);
    }

    void OnPlayModeChanged(PlayModeStateChange state) => StopPreview();
    void OnSceneSaving(Scene scene, string path) => StopPreview();
    void OnPrefabSaving(GameObject root) => StopPreview();
    internal static void StopActivePreview() { if (activePreview != null) activePreview.StopPreview(); }
}

// Restore before asset serialization, including Save Project.
public sealed class IKPreviewSaveProcessor : AssetModificationProcessor
{
    static string[] OnWillSaveAssets(string[] paths)
    {
        RemotePlayerBodyEditor.StopActivePreview();
        return paths;
    }
}
