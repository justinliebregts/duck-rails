using System;
using System.IO;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Production defaults and build parity. Runtime movement has no test/recording switch.
[InitializeOnLoad]
public static class MovementProductionPlayChecks
{
    const string Key = "DuckZ.MovementProductionPlayChecks";
    const BindingFlags All = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
    static int checks;
    static void Check(bool ok, string message) { checks++; if (!ok) throw new Exception(message); }
    static void Call(NewPlayer p, string name) => typeof(NewPlayer).GetMethod(name, All).Invoke(p, null);
    static T Field<T>(NewPlayer p, string name) => (T)typeof(NewPlayer).GetField(name, All).GetValue(p);

    static MovementProductionPlayChecks()
    {
        EditorApplication.playModeStateChanged += state => {
            if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Key, false)) return;
            SessionState.SetBool(Key, false);
            try { RunCases(); EditorApplication.Exit(0); }
            catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
        };
    }

    public static void Run()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Isolated batch only");
        SessionState.SetBool(Key, true);
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        EditorApplication.EnterPlaymode();
    }

    static void RunCases()
    {
        checks = 0;
        var originalPlayer = GR.Player.instance; var originalVR = NewPlayer.instance;
        var originalHelicopter = HelicopterManager.instance;
        try
        {
            HelicopterManager.instance = null;
            using (var rig = new MovementTestRig())
            {
                var p = rig.p;
                Check(p.rb.interpolation == RigidbodyInterpolation.Interpolate, "Awake did not enable production smoothing");
                p.enabled = true;
                p.ApplyPassengerDisplacement(Vector3.right * .1f);
                Check(Field<bool>(p, "movementPoseQueued"), "Lifecycle fixture did not queue a pose");
                p.enabled = false;
                Check(!Field<bool>(p, "movementPoseQueued") && !Field<bool>(p, "movementPoseAwaitingRender"), "Disable retained a queued pose");
                Check(Field<int>(p, "movementRenderCount") == 0, "Disable retained contact history");
                Call(p, "RestoreMovementPoses"); Call(p, "RestoreZeroStepRenderedPoses");
                p.enabled = true;
                Check(p.rb.interpolation == RigidbodyInterpolation.Interpolate, "Re-enable changed movement mode");
                Check(Vector3.Distance(Field<Vector3>(p, "lastPosition"), p.rb.position) < .00001f, "Re-enable seeded velocity from displayed pose");
                var render = typeof(NewPlayer).GetMethod("ContactRenderPosition", All);
                p.leftHandColliding = true; render.Invoke(p, new object[] { p.transform.position });
                Check(Field<int>(p, "movementRenderCount") > 0, "Pause fixture did not establish contact history");
                typeof(NewPlayer).GetMethod("OnApplicationPause", All).Invoke(p, new object[] { true });
                Check(Field<int>(p, "movementRenderCount") == 0, "Pause retained old display history");
                typeof(NewPlayer).GetMethod("OnApplicationPause", All).Invoke(p, new object[] { false });
                Check(p.rb.interpolation == RigidbodyInterpolation.Interpolate, "Resume disabled smoothing");
                p.enabled = false;
            }
            // Removing a disabled player from either queue must preserve other players.
            using (var first = new MovementTestRig())
            using (var middle = new MovementTestRig())
            using (var last = new MovementTestRig())
            {
                first.p.enabled = middle.p.enabled = last.p.enabled = true;
                first.p.ApplyPassengerDisplacement(Vector3.right * .1f);
                middle.p.ApplyPassengerDisplacement(Vector3.up * .2f);
                last.p.ApplyPassengerDisplacement(Vector3.forward * .3f);
                middle.p.enabled = false;
                Check(!Field<bool>(middle.p, "movementPoseQueued"), "Disabled middle player remained queued");
                Check(Field<bool>(first.p, "movementPoseQueued") && Field<bool>(last.p, "movementPoseQueued"), "Disable removed another pending player");
                Call(first.p, "RestoreMovementPoses");
                Check(Field<bool>(first.p, "movementPoseAwaitingRender") && Field<bool>(last.p, "movementPoseAwaitingRender"), "Other players did not reach render restore");
                first.p.enabled = false;
                Check(!Field<bool>(first.p, "movementPoseAwaitingRender") && Field<bool>(last.p, "movementPoseAwaitingRender"), "Disable corrupted render-restore queue");
                Call(last.p, "RestoreZeroStepRenderedPoses");
                Check(!Field<bool>(last.p, "movementPoseAwaitingRender"), "Render restore failed to drain after disable");
                Check(Vector3.Distance(last.p.transform.position, last.p.rb.position) < .00001f, "Other player's pose changed during disable");
                last.p.enabled = false;
            }
            VerifyAssembly(typeof(NewPlayer).Assembly);
            string folder = Path.GetFullPath(Path.Combine(Application.dataPath, "../.."));
            foreach (string name in new[] { "MovementDiagnosticsRelease.dll", "MovementDiagnosticsDevelopment.dll" })
            {
                string path = Path.Combine(folder, name);
                Check(File.Exists(path), "Missing compiled build variant: " + path);
                var assembly = Assembly.LoadFile(path); VerifyAssembly(assembly);
                foreach (string method in new[] { "Awake", "Movement", "Turn", "Warp", "InitializeValues", "ApplyPassengerDisplacement",
                    "InitializeMovementPresentation", "OnEnable", "OnDisable", "OnApplicationPause", "MovementAnchorPosition",
                    "ContactRenderPosition", "BeginPhysicalMovement", "EndPhysicalMovement", "QueueMovementPoseRestore",
                    "RestoreMovementPoses", "RestoreZeroStepRenderedPoses", "SyncTransformsPreservingMovementPose" })
                    CompareMethods(typeof(NewPlayer), assembly.GetType("NewPlayer"), method);
                CompareMethods(typeof(GR.Player), assembly.GetType("GR.Player"), "HeadCenteredGameplayPosition");
                CompareMethods(typeof(GR.Player), assembly.GetType("GR.Player"), "TryGetHeadPivotHeadPosition");
                CompareMethods(typeof(ItemSounds), assembly.GetType("ItemSounds"), "LateUpdate");
                CompareMethods(typeof(DuckzAnalytics), assembly.GetType("DuckzAnalytics"), "TryLocalHead");
            }
            Debug.Log("MovementProductionPlayChecks PASS: " + checks + " assertions; automatic smoothing, disable/re-enable, pause, no recording APIs and identical movement IL in Editor/development/release.");
        }
        finally { GR.Player.instance = originalPlayer; NewPlayer.instance = originalVR; HelicopterManager.instance = originalHelicopter; }
    }

    static void VerifyAssembly(Assembly assembly)
    {
        Check(assembly.GetType("MovementJitterRecorder") == null, "Recorder remains in " + assembly.GetName().Name);
        var type = assembly.GetType("NewPlayer");
        foreach (string name in new[] { "HeadPivotLocomotionTest", "MovementDiagnosticsEnabled", "movementTrace" })
            Check(type.GetField(name, All) == null, "Temporary state remains: " + name);
        foreach (var method in type.GetMethods(All))
            Check(!method.Name.StartsWith("Trace", StringComparison.Ordinal) && method.Name != "ApplyMovementInterpolationTest", "Temporary method remains: " + method.Name);
    }

    static void CompareMethods(Type reference, Type candidate, string name)
    {
        foreach (var method in reference.GetMethods(All))
        {
            if (method.Name != name) continue;
            MethodInfo match = null;
            foreach (var other in candidate.GetMethods(All))
                if (other.Name == name && other.ToString() == method.ToString()) { match = other; break; }
            Check(match != null, "Build variant lacks " + method);
            Check(Normalize(method) == Normalize(match), "Build variant changes " + method);
        }
    }

    // Metadata tokens differ across builds; resolve their meanings before comparing IL.
    static string Normalize(MethodInfo method)
    {
        byte[] il = method.GetMethodBody().GetILAsByteArray();
        var codes = new System.Collections.Generic.Dictionary<short, OpCode>();
        foreach (var field in typeof(OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static))
            if (field.GetValue(null) is OpCode code) codes[code.Value] = code;
        var result = new StringBuilder();
        for (int i = 0; i < il.Length;)
        {
            short value = il[i++]; if (value == 0xfe) value = (short)(0xfe00 | il[i++]);
            var code = codes[value]; result.Append(code.Name).Append(':');
            int count;
            switch (code.OperandType)
            {
                case OperandType.InlineNone: count = 0; break;
                case OperandType.ShortInlineBrTarget: case OperandType.ShortInlineI: case OperandType.ShortInlineVar: count = 1; break;
                case OperandType.InlineVar: count = 2; break;
                case OperandType.InlineI8: case OperandType.InlineR: count = 8; break;
                case OperandType.InlineSwitch: count = 4 + 4 * BitConverter.ToInt32(il, i); break;
                case OperandType.InlineField: case OperandType.InlineMethod: case OperandType.InlineTok: case OperandType.InlineType:
                    result.Append(method.Module.ResolveMember(BitConverter.ToInt32(il, i))); i += 4; continue;
                case OperandType.InlineString:
                    result.Append(method.Module.ResolveString(BitConverter.ToInt32(il, i))); i += 4; continue;
                default: count = 4; break;
            }
            for (int j = 0; j < count; j++) result.Append(il[i++]).Append(',');
        }
        return result.ToString();
    }
}
