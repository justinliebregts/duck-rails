#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;

/// <summary>
/// Buttons for the block lighting review loop on the open bake scene: preview baked lighting,
/// find lights trapped inside meshes, add baked lights, and rebake. Atlas-integrated blocks use
/// the lights-only procedure in docs/atlas-lights-only-rebake.md; other blocks use the standard
/// Bake Open Block path. Nothing is approved automatically.
/// </summary>
[InitializeOnLoad]
public sealed class RailBlockLightingTool : EditorWindow
{
    const string LightingRoot = "Assets/DuckRails/Lighting/Blocks";
    const string AddedLights = "Added baked lights";
    const string PendingFile = "Temp/DuckRailsLightsOnlyRebake.json";

    // ---------- Atlas-integrated block registry ----------
    sealed class AtlasBlock
    {
        public string prefab, visual;
        public Func<string> stateFile;
        public Func<GameObject, string> fingerprint;
        public Action validate;
    }
    static readonly AtlasBlock[] Atlases =
    {
        new AtlasBlock { prefab = RailConvenienceStoreAtlasProduction.Prefab, visual = RailConvenienceStoreAtlasProduction.Visual,
            stateFile = () => "ArtSource/ConvenienceStoreAtlas/production-state.json",
            fingerprint = RailConvenienceStoreAtlasProduction.Fingerprint, validate = RailConvenienceStoreAtlasProduction.ValidateSaved },
        new AtlasBlock { prefab = RailSkyscraperAtlasProduction.Prefab, visual = RailSkyscraperAtlasProduction.Visual,
            stateFile = () => "ArtSource/SkyscraperAtlasLowPoly/production-state.json",
            fingerprint = RailSkyscraperAtlasProduction.Fingerprint, validate = RailSkyscraperAtlasProduction.ValidateSaved },
        new AtlasBlock { prefab = RailContainerYardAtlasProduction.Prefab, visual = RailContainerYardAtlasProduction.Visual,
            stateFile = () => "ArtSource/ContainerYardAtlas/production-state.json",
            fingerprint = RailContainerYardAtlasProduction.Fingerprint, validate = RailContainerYardAtlasProduction.ValidateSaved },
        new AtlasBlock { prefab = RailFarmSilosAtlasProduction.Prefab, visual = RailFarmSilosAtlasProduction.Visual,
            stateFile = () => File.Exists(RailFarmPolishAtlas.Art + "production-state.json")
                ? RailFarmPolishAtlas.Art + "production-state.json" : "ArtSource/FarmSilosAtlas/production-state.json",
            fingerprint = RailFarmSilosAtlasProduction.Fingerprint, validate = RailFarmSilosAtlasProduction.ValidateSaved },
        new AtlasBlock { prefab = RailMotelAtlasProduction.Prefab, visual = RailMotelAtlasProduction.Visual,
            stateFile = () => "ArtSource/MotelAtlasLowPoly/production-state.json",
            fingerprint = RailMotelAtlasProduction.Fingerprint, validate = RailMotelAtlasProduction.ValidateSaved },
    };

    // ---------- Pending lights-only rebake (survives domain reloads via Temp/) ----------
    [Serializable] class Snap { public string id; public int index; public Vector4 scaleOffset; }
    [Serializable] class Pending
    {
        public string prefab, scene, phase, message, oldFingerprint, newFingerprint;
        public int lightmapCount;
        public string[] assetGuids = Array.Empty<string>();
        public string[] noShadow = Array.Empty<string>();
        public Snap[] snaps = Array.Empty<Snap>();
    }
    static Pending pending;
    static bool bakeCompleted, bakeStarted, sawRunning;
    static double bakeStartTime;

    static RailBlockLightingTool()
    {
        try { if (File.Exists(PendingFile)) pending = JsonUtility.FromJson<Pending>(File.ReadAllText(PendingFile)); }
        catch { pending = null; }
        Lightmapping.bakeCompleted += () => bakeCompleted = true;
        EditorApplication.update += Tick;
    }
    static void SavePending()
    {
        Directory.CreateDirectory("Temp");
        if (pending == null) { if (File.Exists(PendingFile)) File.Delete(PendingFile); }
        else File.WriteAllText(PendingFile, JsonUtility.ToJson(pending, true));
        foreach (var w in Resources.FindObjectsOfTypeAll<RailBlockLightingTool>()) w.Repaint();
    }

    // ---------- Light check results ----------
    sealed class LightResult { public Light light; public int blocked, samples; public string blocker, verdict; }
    List<LightResult> results = new List<LightResult>();
    float probeDistance = 1f;
    bool showOnlyFlagged = true;
    Vector2 scroll;
    readonly List<Renderer> noShadowDuringBake = new List<Renderer>();
    int pickBlock;

    bool showHelp = true;
    const string HelpText =
        "PREVIEW\n" +
        "1. Open the block's bake scene (Lighting/Blocks/<block>/<block>-bake.unity).\n" +
        "2. Click Preview open map lighting.\n\n" +
        "FIND AND FIX BAD LIGHTS\n" +
        "1. Click Check baked lights for blocking meshes.\n" +
        "2. For each TRAPPED light, click Select and move it out of its lamp mesh (usually just below or in front of the lamp head).\n" +
        "3. Run the check again until the lights you moved show ok.\n" +
        "4. To add a light, click Add baked light (at the Scene view centre, or a copy of the selected light), then move it into place.\n\n" +
        "REBAKE\n" +
        "Atlas blocks (Convenience Store, Skyscraper, Container Yard, Farm Silos, Motel):\n" +
        "1. Click Rebake atlas block lights and wait for the bake to finish.\n" +
        "2. Look at the result, then click Approve to keep it or Discard bake to throw it away.\n" +
        "Every other block:\n" +
        "1. Click Rebake this block and wait for it to finish.\n\n" +
        "FINISH\n" +
        "Fly around and check the new lighting. If you like it, commit. If not, discard the bake (atlas blocks) or revert with git (other blocks).";

    [MenuItem("Duck Rails/Lighting/Block Lighting Tool")]
    static void Open() => GetWindow<RailBlockLightingTool>("Block Lighting");

    // ---------- Context ----------
    static string OpenBlockPrefab()
    {
        var scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || string.IsNullOrEmpty(scene.path)) return null;
        return RailCatalogLightingAuthoring.Prefabs.FirstOrDefault(p => BakePath(p) == scene.path);
    }
    static string Folder(string prefab) => LightingRoot + "/" + Path.GetFileNameWithoutExtension(prefab);
    static string BakePath(string prefab) => Folder(prefab) + "/" + Path.GetFileNameWithoutExtension(prefab) + "-bake.unity";
    static string BakeDataFolder(string prefab) => Folder(prefab) + "/" + Path.GetFileNameWithoutExtension(prefab) + "-bake";
    static AtlasBlock Atlas(string prefab)
    {
        var a = Atlases.FirstOrDefault(x => x.prefab == prefab);
        if (a == null) return null;
        var root = AssetDatabase.LoadAssetAtPath<GameObject>(prefab);
        return root && root.transform.Find(a.visual) ? a : null;
    }
    static RailBakedBlockLighting SceneBlock() => Object.FindFirstObjectByType<RailBakedBlockLighting>(FindObjectsInactive.Include);

    static bool Excluded(Transform t, Transform root)
    {
        if (t.GetComponentInParent<Helicopter>(true) != null) return true;
        for (var p = t; p != null && p != root; p = p.parent)
            if (p.name == "Bridge hinge" || p.name.StartsWith("Spawn variation") || p.name == "Exit rail" || p.name == "DAWN") return true;
        return false;
    }
    static bool IsDerived(Transform t, RailBakedBlockLighting block, AtlasBlock atlas)
    {
        if (atlas != null) { var v = block.transform.Find(atlas.visual); if (v && t.IsChildOf(v)) return true; }
        var chunks = block.GetComponent<RailSceneryChunks>();
        return chunks && chunks.chunkRoot && t.IsChildOf(chunks.chunkRoot.transform);
    }

    // ---------- GUI ----------
    Vector2 windowScroll;
    void OnGUI()
    {
        windowScroll = EditorGUILayout.BeginScrollView(windowScroll);
        try { DrawWindow(); }
        finally { EditorGUILayout.EndScrollView(); }
    }

    void DrawWindow()
    {
        var prefab = OpenBlockPrefab();
        var atlas = prefab != null ? Atlas(prefab) : null;
        bool busy = EditorApplication.isPlayingOrWillChangePlaymode || Lightmapping.isRunning;

        showHelp = EditorGUILayout.Foldout(showHelp, "How to use", true);
        if (showHelp) EditorGUILayout.HelpBox(HelpText, MessageType.None);

        EditorGUILayout.LabelField("Open bake scene", EditorStyles.boldLabel);
        if (prefab == null)
        {
            EditorGUILayout.HelpBox("Open a block bake scene (Assets/DuckRails/Lighting/Blocks/<block>/<block>-bake.unity).", MessageType.Info);
            var all = RailCatalogLightingAuthoring.Prefabs;
            var names = all.Select(Path.GetFileNameWithoutExtension).ToArray();
            using (new EditorGUILayout.HorizontalScope())
            {
                pickBlock = EditorGUILayout.Popup(Mathf.Clamp(pickBlock, 0, Math.Max(0, names.Length - 1)), names);
                using (new EditorGUI.DisabledScope(busy || names.Length == 0 || !File.Exists(BakePath(all[pickBlock]))))
                    if (GUILayout.Button("Open bake scene", GUILayout.Width(130)) && EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                        EditorSceneManager.OpenScene(BakePath(all[pickBlock]));
            }
            DrawPending();
            return;
        }
        EditorGUILayout.LabelField(Path.GetFileNameWithoutExtension(prefab),
            atlas != null ? "Atlas-integrated: lights-only rebake" : "Standard bake");
        if (busy) EditorGUILayout.HelpBox(Lightmapping.isRunning ? "Bake running…" : "Leave Play Mode to use these tools.", MessageType.Warning);

        using (new EditorGUI.DisabledScope(busy))
        {
            // 1. Preview
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("1. Preview", EditorStyles.boldLabel);
            if (GUILayout.Button("Preview open map lighting", GUILayout.Height(26))) Run(() => Preview(atlas));
            EditorGUILayout.LabelField(atlas != null
                ? "Binds the atlas chunks to the block lightmap (unsaved; nothing is written)."
                : "Turns on Scene view lighting; this block's originals already carry the bake.", EditorStyles.miniLabel);

            // 2. Trapped lights
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("2. Find trapped lights", EditorStyles.boldLabel);
            probeDistance = EditorGUILayout.Slider(new GUIContent("Probe distance (m)", "A ray that hits a mesh closer than this counts as blocked."), probeDistance, .1f, 4f);
            if (GUILayout.Button("Check baked lights for blocking meshes", GUILayout.Height(26))) Run(() => results = CheckLights(atlas, probeDistance));
            DrawResults();

            // 3. Edit lights
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("3. Edit lights", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Move or retune existing Light objects freely. New lights go under '" + AddedLights + "' (last child of the block).", EditorStyles.wordWrappedMiniLabel);
            if (GUILayout.Button(Selection.activeGameObject && Selection.activeGameObject.GetComponent<Light>()
                    ? "Add baked light (copy of selected)" : "Add baked light at Scene view pivot")) Run(AddLight);
            if (atlas != null) DrawNoShadowList();

            // 4. Rebake
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("4. Rebake", EditorStyles.boldLabel);
            if (atlas != null)
            {
                using (new EditorGUI.DisabledScope(pending != null && pending.phase != "Failed"))
                    if (GUILayout.Button("Rebake atlas block lights", GUILayout.Height(28))) Run(() => StartLightsOnly(prefab, atlas, noShadowDuringBake));
            }
            else
            {
                if (GUILayout.Button("Rebake this block (Bake Open Block)", GUILayout.Height(28))) Run(() => StartStandard(prefab));
                var status = RailCatalogLightingAuthoring.Status;
                EditorGUILayout.LabelField("Bake status: " + status, EditorStyles.miniLabel);
            }
        }
        DrawPending();
    }

    void DrawResults()
    {
        if (results.Count == 0) return;
        int trapped = results.Count(r => r.verdict == "TRAPPED"), check = results.Count(r => r.verdict == "check");
        EditorGUILayout.LabelField($"{results.Count} baked lights: {trapped} trapped, {check} worth a look", EditorStyles.miniBoldLabel);
        showOnlyFlagged = EditorGUILayout.ToggleLeft("Show only flagged", showOnlyFlagged);
        scroll = EditorGUILayout.BeginScrollView(scroll, GUILayout.MaxHeight(220));
        foreach (var r in results)
        {
            if (!r.light || (showOnlyFlagged && r.verdict == "ok")) continue;
            using (new EditorGUILayout.HorizontalScope())
            {
                var old = GUI.color;
                GUI.color = r.verdict == "TRAPPED" ? new Color(1, .55f, .5f) : r.verdict == "check" ? new Color(1, .9f, .5f) : old;
                GUILayout.Label(r.verdict, GUILayout.Width(62));
                GUI.color = old;
                GUILayout.Label($"{r.blocked}/{r.samples}", GUILayout.Width(44));
                GUILayout.Label(new GUIContent(r.light.name + (r.blocker != null ? "  ← " + r.blocker : ""), HPath(r.light.transform)));
                if (GUILayout.Button("Select", GUILayout.Width(52)))
                {
                    Selection.activeGameObject = r.light.gameObject;
                    if (SceneView.lastActiveSceneView) SceneView.lastActiveSceneView.FrameSelected();
                }
            }
        }
        EditorGUILayout.EndScrollView();
    }

    void DrawNoShadowList()
    {
        EditorGUILayout.LabelField("Bake-only: no shadows from these meshes (e.g. lantern glass)", EditorStyles.miniLabel);
        noShadowDuringBake.RemoveAll(r => !r);
        foreach (var r in noShadowDuringBake.ToArray())
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.ObjectField(r, typeof(Renderer), true);
                if (GUILayout.Button("×", GUILayout.Width(22))) noShadowDuringBake.Remove(r);
            }
        if (GUILayout.Button("Add selected renderers"))
            foreach (var r in Selection.gameObjects.Select(g => g.GetComponent<MeshRenderer>()).Where(r => r && !noShadowDuringBake.Contains(r)))
                noShadowDuringBake.Add(r);
    }

    void DrawPending()
    {
        if (pending == null) return;
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Lights-only rebake: " + Path.GetFileNameWithoutExtension(pending.prefab), EditorStyles.boldLabel);
        var type = pending.phase == "AwaitingApproval" ? MessageType.Info : pending.phase == "Baking" ? MessageType.None : MessageType.Error;
        EditorGUILayout.HelpBox(pending.phase + ": " + pending.message, type);
        if (pending.phase == "Baking") return;
        using (new EditorGUI.DisabledScope(Lightmapping.isRunning || EditorApplication.isPlaying))
        using (new EditorGUILayout.HorizontalScope())
        {
            if (pending.phase == "AwaitingApproval")
            {
                if (GUILayout.Button("Preview result")) Run(() => Preview(Atlas(pending.prefab)));
                if (GUILayout.Button("Approve: record new fingerprint")) Run(Approve);
            }
            if (pending.phase != "Failed" || Directory.Exists(BakeDataFolder(pending.prefab)))
                if (GUILayout.Button("Discard bake (restore from git)")) Run(Discard);
            if (pending.phase == "Failed" && GUILayout.Button("Dismiss")) { pending = null; SavePending(); }
        }
    }

    static void Run(Action action)
    {
        try { action(); }
        catch (Exception ex)
        {
            Debug.LogException(ex);
            EditorUtility.DisplayDialog("Block Lighting", ex.Message, "OK");
        }
    }
    static string HPath(Transform t) => t.parent ? HPath(t.parent) + "/" + t.name : t.name;
    static string PathRelative(Transform t, Transform root) => AnimationUtility.CalculateTransformPath(t, root);

    // ---------- Scriptable entry points (same as the buttons) ----------
    public static void PreviewOpenScene()
    {
        var prefab = OpenBlockPrefab() ?? throw new InvalidOperationException("Open a block bake scene first.");
        Preview(Atlas(prefab));
    }
    public static string CheckOpenScene(float probeDistance = 1f)
    {
        var prefab = OpenBlockPrefab() ?? throw new InvalidOperationException("Open a block bake scene first.");
        var list = CheckLights(Atlas(prefab), probeDistance);
        return $"{Path.GetFileNameWithoutExtension(prefab)}: {list.Count} baked lights, probe {probeDistance:0.##} m\n" +
            string.Join("\n", list.Where(r => r.verdict != "ok").Select(r =>
                $"{r.verdict} {r.blocked}/{r.samples} {r.light.type} {HPath(r.light.transform)}" + (r.blocker != null ? " <- " + r.blocker : "")));
    }

    // ---------- 1. Preview ----------
    static void Preview(AtlasBlock atlas)
    {
        var block = SceneBlock() ?? throw new InvalidOperationException("No RailBakedBlockLighting in the open scene.");
        if (atlas != null)
        {
            var maps = LightmapSettings.lightmaps.ToList();
            int bound = 0;
            foreach (var b in block.bindings)
            {
                if (!b.renderer || !b.color) continue;
                int i = maps.FindIndex(m => m.lightmapColor == b.color);
                if (i < 0) { i = maps.Count; maps.Add(new LightmapData { lightmapColor = b.color }); }
                b.renderer.lightmapIndex = i; b.renderer.lightmapScaleOffset = b.scaleOffset; bound++;
            }
            LightmapSettings.lightmaps = maps.ToArray();
            Debug.Log($"Block Lighting preview: bound {bound} renderers to {maps.Count} lightmap(s). Unsaved; reopen the scene to drop it.");
        }
        foreach (SceneView view in SceneView.sceneViews) { view.sceneLighting = true; view.Repaint(); }
    }

    // ---------- 2. Trapped light check ----------
    static readonly Vector3[] GridDirections = BuildGrid();
    static Vector3[] BuildGrid()
    {
        var list = new List<Vector3>();
        for (int x = -1; x <= 1; x++) for (int y = -1; y <= 1; y++) for (int z = -1; z <= 1; z++)
            if (x != 0 || y != 0 || z != 0) list.Add(new Vector3(x, y, z).normalized);
        return list.ToArray();
    }

    static List<LightResult> CheckLights(AtlasBlock atlas, float distance)
    {
        var block = SceneBlock() ?? throw new InvalidOperationException("No RailBakedBlockLighting in the open scene.");
        var bound = new HashSet<Renderer>(block.bindings.Where(b => b.renderer).Select(b => b.renderer));
        // Colliders = what casts shadows in the bake: enabled originals plus disabled atlas originals.
        var casters = block.GetComponentsInChildren<MeshRenderer>(true).Where(r =>
            r.shadowCastingMode != ShadowCastingMode.Off && !Excluded(r.transform, block.transform) &&
            !IsDerived(r.transform, block, atlas) && (r.enabled && r.gameObject.activeInHierarchy || bound.Contains(r)) &&
            r.GetComponent<MeshFilter>() && r.GetComponent<MeshFilter>().sharedMesh).ToArray();
        var lights = block.GetComponentsInChildren<Light>(false)
            .Where(l => l.enabled && l.lightmapBakeType != LightmapBakeType.Realtime && l.type != LightType.Directional).ToArray();

        var preview = EditorSceneManager.NewPreviewScene();
        bool backfaces = Physics.queriesHitBackfaces;
        var names = new Dictionary<Collider, string>();
        var output = new List<LightResult>();
        try
        {
            Physics.queriesHitBackfaces = true;
            int n = 0;
            foreach (var r in casters)
            {
                if ((n++ & 63) == 0 && EditorUtility.DisplayCancelableProgressBar("Block Lighting", "Building blockers", (float)n / casters.Length)) return output;
                var go = new GameObject(r.name) { hideFlags = HideFlags.HideAndDontSave };
                SceneManager.MoveGameObjectToScene(go, preview);
                go.transform.SetPositionAndRotation(r.transform.position, r.transform.rotation);
                go.transform.localScale = r.transform.lossyScale;
                var c = go.AddComponent<MeshCollider>();
                c.sharedMesh = r.GetComponent<MeshFilter>().sharedMesh;
                names[c] = PathRelative(r.transform, block.transform);
            }
            Physics.SyncTransforms();
            var physics = preview.GetPhysicsScene();
            foreach (var l in lights)
            {
                var dirs = l.type == LightType.Spot ? ConeDirections(l) : GridDirections;
                int blocked = 0; var hitNames = new Dictionary<string, int>();
                foreach (var d in dirs)
                    if (physics.Raycast(l.transform.position, d, out var hit, distance, ~0, QueryTriggerInteraction.Ignore))
                    {
                        blocked++;
                        if (names.TryGetValue(hit.collider, out var name)) hitNames[name] = hitNames.TryGetValue(name, out var k) ? k + 1 : 1;
                    }
                float f = (float)blocked / dirs.Length;
                string verdict = l.type == LightType.Spot
                    ? (f >= .9f ? "TRAPPED" : f >= .5f ? "check" : "ok")
                    : (blocked >= 24 ? "TRAPPED" : blocked >= 14 ? "check" : "ok");
                output.Add(new LightResult { light = l, blocked = blocked, samples = dirs.Length, verdict = verdict,
                    blocker = hitNames.Count > 0 ? hitNames.OrderByDescending(p => p.Value).First().Key : null });
            }
        }
        finally
        {
            Physics.queriesHitBackfaces = backfaces;
            EditorSceneManager.ClosePreviewScene(preview);
            EditorUtility.ClearProgressBar();
        }
        output.Sort((a, b) => ((float)b.blocked / b.samples).CompareTo((float)a.blocked / a.samples));
        Debug.Log($"Block Lighting check: {lights.Length} baked lights against {casters.Length} shadow casters, probe {distance:0.##} m. " +
            $"Trapped: {output.Count(r => r.verdict == "TRAPPED")}. " +
            string.Join("; ", output.Where(r => r.verdict != "ok").Select(r => $"{r.verdict} {HPath(r.light.transform)} {r.blocked}/{r.samples}")));
        return output;
    }
    static Vector3[] ConeDirections(Light l)
    {
        var list = new List<Vector3> { l.transform.forward };
        float half = l.spotAngle * .5f;
        foreach (float frac in new[] { 1 / 3f, 2 / 3f, 1f })
            for (int az = 0; az < 360; az += 45)
                list.Add(l.transform.rotation * (Quaternion.Euler(0, 0, az) * Quaternion.Euler(half * frac, 0, 0) * Vector3.forward));
        return list.ToArray();
    }

    // ---------- 3. Add light ----------
    static void AddLight()
    {
        var block = SceneBlock() ?? throw new InvalidOperationException("No RailBakedBlockLighting in the open scene.");
        var root = block.transform;
        var container = root.Find(AddedLights);
        if (!container)
        {
            container = new GameObject(AddedLights).transform;
            Undo.RegisterCreatedObjectUndo(container.gameObject, "Add baked lights group");
            container.SetParent(root, false);
            container.SetAsLastSibling();
        }
        else if (container.GetSiblingIndex() != root.childCount - 1)
            throw new InvalidOperationException($"'{AddedLights}' must stay the last child of the block root; atlas guards find originals by child position.");

        var template = Selection.activeGameObject ? Selection.activeGameObject.GetComponent<Light>() : null;
        var go = new GameObject("Baked light " + (container.childCount + 1));
        Undo.RegisterCreatedObjectUndo(go, "Add baked light");
        go.transform.SetParent(container, false);
        var light = go.AddComponent<Light>();
        if (template)
        {
            EditorUtility.CopySerialized(template, light);
            go.transform.SetPositionAndRotation(template.transform.position + Vector3.up * .25f, template.transform.rotation);
        }
        else
        {
            light.type = LightType.Point; light.color = new Color(1, .78f, .52f); light.intensity = 8; light.range = 8;
            light.shadows = LightShadows.Soft;
            var view = SceneView.lastActiveSceneView;
            go.transform.position = view ? view.pivot : root.position;
        }
        light.lightmapBakeType = LightmapBakeType.Baked;
        Selection.activeGameObject = go;
    }

    // ---------- 4a. Standard rebake ----------
    static void StartStandard(string prefab)
    {
        if (!EditorUtility.DisplayDialog("Rebake " + Path.GetFileNameWithoutExtension(prefab),
                "This saves the open bake scene, bakes it, then exports bindings and baked-fixture metadata into the block prefab (applies the scene's prefab overrides, including your light edits)." +
                "\n\nUndo with git if you don't like the result.", "Save and bake", "Cancel")) return;
        RailCatalogLightingAuthoring.BakeOpenAndKeepOpen();
    }

    // ---------- 4b. Lights-only rebake (atlas blocks) ----------
    static string Git(params string[] args)
    {
        var psi = new ProcessStartInfo("git", string.Join(" ", args.Select(a => "\"" + a + "\"")))
        { WorkingDirectory = Directory.GetCurrentDirectory(), RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true };
        using (var p = Process.Start(psi))
        {
            string o = p.StandardOutput.ReadToEnd(), e = p.StandardError.ReadToEnd();
            p.WaitForExit();
            if (p.ExitCode != 0) throw new InvalidOperationException("git " + string.Join(" ", args) + " failed: " + e);
            return o;
        }
    }
    static bool IsLightOnly(GameObject go) => go.GetComponents<Component>().All(c => c is Transform || c is Light ||
        (c != null && c.GetType().Name == "UniversalAdditionalLightData"));

    /// <summary>Applies only Light/Transform edits on lights from the bake-scene instance into the prefab.</summary>
    static string ApplyLightOverrides(GameObject instance, string prefab)
    {
        int applied = 0; var skipped = new List<string>();
        var container = instance.transform.Find(AddedLights);
        foreach (var added in PrefabUtility.GetAddedGameObjects(instance))
        {
            var go = added.instanceGameObject;
            bool ok = go.GetComponentsInChildren<Transform>(true).All(t => IsLightOnly(t.gameObject)) &&
                container && (go.transform == container || go.transform.IsChildOf(container));
            if (!ok) throw new InvalidOperationException($"'{HPath(go.transform)}' was added outside '{AddedLights}' or contains non-light components. New lights must go under '{AddedLights}' (last child of the block).");
        }
        if (container && container.GetSiblingIndex() != instance.transform.childCount - 1)
            throw new InvalidOperationException($"'{AddedLights}' must be the last child of the block root.");
        foreach (var added in PrefabUtility.GetAddedGameObjects(instance))
            if (added.instanceGameObject && PrefabUtility.IsAddedGameObjectOverride(added.instanceGameObject))
        { PrefabUtility.ApplyAddedGameObject(added.instanceGameObject, prefab, InteractionMode.AutomatedAction); applied++; }
        if (PrefabUtility.GetAddedComponents(instance).Any(c => c.instanceComponent is Light))
            throw new InvalidOperationException("A Light component was added to an existing object. Put new lights under '" + AddedLights + "' instead.");
        foreach (var o in PrefabUtility.GetObjectOverrides(instance))
        {
            var target = o.instanceObject;
            bool lightEdit = target is Light || (target != null && target.GetType().Name == "UniversalAdditionalLightData") ||
                (target is Transform t && t.GetComponent<Light>() && t != instance.transform);
            if (lightEdit) { PrefabUtility.ApplyObjectOverride(target, prefab, InteractionMode.AutomatedAction); applied++; }
            else if (target is Component comp) skipped.Add(comp.GetType().Name + " on " + comp.name);
        }
        AssetDatabase.SaveAssets();
        var saved = AssetDatabase.LoadAssetAtPath<GameObject>(prefab).transform;
        var savedContainer = saved.Find(AddedLights);
        if (savedContainer && savedContainer.GetSiblingIndex() != saved.childCount - 1)
            throw new InvalidOperationException($"Prefab '{AddedLights}' is not the last child; fix the order before baking.");
        return $"{applied} light edit(s) applied to the prefab" + (skipped.Count > 0 ? $"; {skipped.Count} other scene override(s) left in the scene (not baked): " + string.Join(", ", skipped.Take(6)) : "");
    }

    static void StartLightsOnly(string prefab, AtlasBlock atlas, List<Renderer> noShadow)
    {
        if (Application.dataPath.Replace('\\', '/') != "X:/Work/duck-rails/Assets") throw new InvalidOperationException("Use the main Duck Rails editor.");
        if (Lightmapping.isRunning || EditorApplication.isPlaying) throw new InvalidOperationException("Finish Play Mode or the current bake first.");
        var stage = PrefabStageUtility.GetCurrentPrefabStage();
        if (stage != null && stage.assetPath == prefab) throw new InvalidOperationException("Close Prefab Mode for this block first (save your light edits there).");
        var active = SceneManager.GetActiveScene();
        for (int i = 0; i < SceneManager.sceneCount; i++)
            if (SceneManager.GetSceneAt(i) != active && SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save or close other modified scenes first.");
        string dataFolder = BakeDataFolder(prefab);
        if (Git("status", "--porcelain", "--", dataFolder).Trim().Length > 0)
            throw new InvalidOperationException(dataFolder + " has uncommitted changes. Commit or restore it first so a bad bake can be discarded with git.");
        if (!EditorUtility.DisplayDialog("Lights-only rebake: " + Path.GetFileNameWithoutExtension(prefab),
                "1. Applies your Light/Transform edits on lights to the prefab.\n2. Reopens the bake scene from disk (drops the preview and any other unsaved scene edits).\n3. Checks the approved atlas, snapshots every original's lightmap placement, and bakes with the originals temporarily enabled.\n4. Keeps the bake only if every placement is identical; you then approve the new fingerprint with a separate button.",
                "Bake", "Cancel")) return;

        var sceneBlock = SceneBlock() ?? throw new InvalidOperationException("No RailBakedBlockLighting in the open scene.");
        var instance = PrefabUtility.GetOutermostPrefabInstanceRoot(sceneBlock.gameObject);
        if (!instance) throw new InvalidOperationException("The bake scene's block is not a prefab instance.");
        string applyReport = ApplyLightOverrides(instance, prefab);
        var noShadowIds = noShadow.Where(r => r).Select(r => PrefabUtility.GetCorrespondingObjectFromSource(r) ?? r)
            .Select(r => PathRelative(r.transform, r.transform.root)).ToArray();

        var scene = EditorSceneManager.OpenScene(BakePath(prefab), OpenSceneMode.Single);
        atlas.validate(); // fingerprint unchanged, sourceKeys resolve, atlas intact
        var block = SceneBlock();
        var visual = block.transform.Find(atlas.visual);
        var byRenderer = block.bindings.Where(b => b.renderer).GroupBy(b => b.renderer).ToDictionary(g => g.Key, g => g.First());
        var maps = LightmapSettings.lightmaps;
        var snaps = new List<Snap>(); var toEnable = new List<Renderer>();
        foreach (var r in block.GetComponentsInChildren<MeshRenderer>(true))
        {
            if (r.transform.IsChildOf(visual) || r.lightmapIndex < 0 || r.lightmapIndex >= maps.Length) continue;
            if (!byRenderer.TryGetValue(r, out var b) || b.color != maps[r.lightmapIndex].lightmapColor || b.scaleOffset != r.lightmapScaleOffset)
                throw new InvalidOperationException("Starting state mismatch: " + HPath(r.transform) + " does not match its prefab binding. Use the full atlas path.");
            snaps.Add(new Snap { id = GlobalObjectId.GetGlobalObjectIdSlow(r).ToString(), index = r.lightmapIndex, scaleOffset = r.lightmapScaleOffset });
            toEnable.Add(r);
        }
        if (snaps.Count == 0) throw new InvalidOperationException("No lightmapped originals found in the bake scene.");
        var settingsPath = AssetDatabase.GetAssetPath(Lightmapping.lightingSettings);
        if (settingsPath != Folder(prefab) + "/Lighting.lighting") throw new InvalidOperationException("Scene lighting settings are " + settingsPath + ", expected the block's Lighting.lighting.");

        // Temporary bake setup; never saved.
        foreach (var r in toEnable) r.enabled = true;
        foreach (var r in visual.GetComponentsInChildren<Renderer>(true)) r.enabled = false;
        foreach (var r in block.GetComponentsInChildren<Renderer>(true)) if (Excluded(r.transform, block.transform)) r.enabled = false;
        int shadowOff = 0;
        foreach (var r in block.GetComponentsInChildren<MeshRenderer>(true))
            if (noShadowIds.Contains(PathRelative(r.transform, r.transform.root))) { r.shadowCastingMode = ShadowCastingMode.Off; shadowOff++; }

        pending = new Pending
        {
            prefab = prefab, scene = scene.path, phase = "Baking", snaps = snaps.ToArray(), lightmapCount = maps.Length,
            assetGuids = BakeAssetGuids(prefab), noShadow = noShadowIds,
            oldFingerprint = atlas.fingerprint(AssetDatabase.LoadAssetAtPath<GameObject>(prefab)),
            message = $"{applyReport}. {snaps.Count} originals snapshotted, {maps.Length} lightmap(s)" + (shadowOff > 0 ? $", {shadowOff} renderer(s) without shadows for this bake" : "") + ". Baking…"
        };
        SavePending();
        bakeCompleted = false; bakeStarted = true; sawRunning = false; bakeStartTime = EditorApplication.timeSinceStartup;
        if (!Lightmapping.BakeAsync()) { pending.phase = "Failed"; pending.message = "Unity did not start the bake."; SavePending(); EditorSceneManager.OpenScene(BakePath(prefab)); }
        Debug.Log("Lights-only rebake started: " + pending.message);
    }
    static string[] BakeAssetGuids(string prefab) => AssetDatabase.FindAssets("", new[] { BakeDataFolder(prefab) })
        .Select(g => g + ":" + AssetDatabase.GUIDToAssetPath(g)).OrderBy(s => s).ToArray();

    static void Tick()
    {
        if (pending == null || pending.phase != "Baking" || EditorApplication.isCompiling) return;
        if (Lightmapping.isRunning) { sawRunning = true; return; }
        if (bakeStarted && !bakeCompleted && !sawRunning && EditorApplication.timeSinceStartup - bakeStartTime < 10) return;
        if (!bakeStarted) { pending.phase = "Failed"; pending.message = "The editor reloaded during the bake; its result was not verified. Discard it."; SavePending(); return; }
        bakeStarted = false;
        try { FinishLightsOnly(bakeCompleted); }
        catch (Exception ex) { pending.phase = "Failed"; pending.message = ex.Message; SavePending(); Debug.LogException(ex); }
    }

    static void FinishLightsOnly(bool completed)
    {
        var atlas = Atlas(pending.prefab);
        EditorSceneManager.OpenScene(pending.scene, OpenSceneMode.Single); // drops the temporary setup
        AssetDatabase.Refresh();
        if (!completed) { pending.phase = "Failed"; pending.message = "Bake was cancelled. Nothing new to keep; Discard restores the folder from git if anything was written."; SavePending(); return; }
        var problems = new List<string>();
        var maps = LightmapSettings.lightmaps;
        if (maps.Length != pending.lightmapCount) problems.Add($"lightmap count {pending.lightmapCount} → {maps.Length}");
        if (!BakeAssetGuids(pending.prefab).SequenceEqual(pending.assetGuids)) problems.Add("bake asset GUIDs/files changed");
        int same = 0;
        foreach (var s in pending.snaps)
        {
            var r = GlobalObjectId.TryParse(s.id, out var gid) ? GlobalObjectId.GlobalObjectIdentifierToObjectSlow(gid) as Renderer : null;
            if (r && r.lightmapIndex == s.index && r.lightmapScaleOffset == s.scaleOffset) same++;
            else if (problems.Count < 8) problems.Add("moved: " + (r ? HPath(r.transform) : s.id));
        }
        // Baked-fixture list must match the prefab's Baked lights.
        string fixtures = "";
        var root = PrefabUtility.LoadPrefabContents(pending.prefab);
        try
        {
            var lighting = root.GetComponent<RailBakedBlockLighting>();
            var before = Fixtures(lighting);
            lighting.CaptureBakedFixtures();
            var after = Fixtures(lighting);
            if (!before.SequenceEqual(after))
            {
                PrefabUtility.SaveAsPrefabAsset(root, pending.prefab);
                fixtures = $" Baked fixtures {before.Length} → {after.Length} (saved to prefab).";
            }
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }

        if (problems.Count > 0 || same != pending.snaps.Length)
        {
            pending.phase = "LayoutChanged";
            pending.message = $"{same}/{pending.snaps.Length} placements identical; {string.Join(", ", problems)}. The atlas UVs no longer match: Discard this bake and use the full reconcile → rebake → regenerate path.";
        }
        else
        {
            pending.newFingerprint = atlas.fingerprint(AssetDatabase.LoadAssetAtPath<GameObject>(pending.prefab));
            pending.phase = "AwaitingApproval";
            pending.message = $"{same}/{pending.snaps.Length} placements identical, {maps.Length} lightmap(s), GUIDs unchanged.{fixtures} " +
                $"Fingerprint {Short(pending.oldFingerprint)} → {Short(pending.newFingerprint)}. Preview it, then Approve or Discard. Builds fail until approved.";
            Preview(atlas);
        }
        SavePending();
        Debug.Log("Lights-only rebake " + pending.phase + ": " + pending.message);
    }
    static Object[] Fixtures(RailBakedBlockLighting lighting)
    {
        var p = new SerializedObject(lighting).FindProperty("bakedFixtures");
        return Enumerable.Range(0, p.arraySize).Select(i => p.GetArrayElementAtIndex(i).objectReferenceValue).ToArray();
    }
    static string Short(string f) => string.IsNullOrEmpty(f) ? "?" : f.Substring(0, Math.Min(8, f.Length)) + "…";

    static void Approve()
    {
        var atlas = Atlas(pending.prefab) ?? throw new InvalidOperationException("Block is no longer atlas-integrated.");
        string current = atlas.fingerprint(AssetDatabase.LoadAssetAtPath<GameObject>(pending.prefab));
        if (current != pending.newFingerprint) throw new InvalidOperationException("The prefab or lightmap changed after the bake was verified. Discard and rebake.");
        if (!EditorUtility.DisplayDialog("Approve new lighting", $"Record fingerprint {current} in {atlas.stateFile()}?", "Approve", "Cancel")) return;
        string path = atlas.stateFile(), text = File.ReadAllText(path);
        if (!text.Contains(pending.oldFingerprint)) throw new InvalidOperationException(path + " does not hold the expected old fingerprint.");
        File.WriteAllText(path, text.Replace(pending.oldFingerprint, current));
        atlas.validate();
        Debug.Log($"Approved lights-only rebake for {Path.GetFileNameWithoutExtension(pending.prefab)}: {pending.oldFingerprint} → {current}. " +
                  "Record the run in the block's atlas README and docs/atlas-lights-only-rebake.md, and queue a headset playtest.");
        pending = null; SavePending();
    }

    static void Discard()
    {
        string folder = BakeDataFolder(pending.prefab);
        if (!EditorUtility.DisplayDialog("Discard bake", $"Restore {folder} from git (your prefab light edits are kept)?", "Discard", "Cancel")) return;
        Git("checkout", "--", folder);
        AssetDatabase.Refresh();
        EditorSceneManager.OpenScene(pending.scene, OpenSceneMode.Single);
        if (Atlas(pending.prefab) is AtlasBlock atlas) atlas.validate();
        Debug.Log("Discarded lights-only bake; " + folder + " restored from git.");
        pending = null; SavePending();
    }

    void OnSelectionChange() => Repaint();
    void OnHierarchyChange() => Repaint();
}
#endif
