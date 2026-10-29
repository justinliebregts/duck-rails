using System;
using System.IO;
using Game.Audio;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>Reproducible source for the authored flare, launch audio wiring, and focused checks.</summary>
[InitializeOnLoad]
public static class DeathFlareSetup
{
    const string PlayCheckKey = "DeathFlare.PlayChecks";
    static DeathFlare liveFlare;
    static double playStarted;
    static bool checkedDelay, checkedAudio, checkedDescent;
    static int lastCheckSecond;

    static DeathFlareSetup()
    {
        EditorApplication.playModeStateChanged += state =>
        {
            if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(PlayCheckKey, false)) return;
            Application.runInBackground = true;
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.layer = LayerMask.NameToLayer("Environment");
            floor.transform.position = new Vector3(0, -0.5f, 0);
            floor.transform.localScale = new Vector3(30, 1, 30);
            new GameObject("Flare check listener").AddComponent<AudioListener>();
            Physics.SyncTransforms();
            liveFlare = DeathFlare.SpawnAfterDeath(AssetDatabase.LoadAssetAtPath<DeathFlare>(PrefabPath), Vector3.up * 0.5f);
            playStarted = Time.unscaledTimeAsDouble;
            Time.timeScale = 0; // The death delay and flight both use real time.
            EditorApplication.update += CheckPlayFrame;
        };
    }

    // Batch entry point, without -quit. Verifies the real lifetime and AudioManager path.
    public static void RunPlayChecks()
    {
        Check(Application.isBatchMode, "play checks require a separate batch editor");
        SessionState.SetBool(PlayCheckKey, true);
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        new GameObject("Fixture sign-in guard").AddComponent<LoginFlow>().enabled = false;
        EditorApplication.EnterPlaymode();
    }

    static void CheckPlayFrame()
    {
        try
        {
            double deathAge = Time.unscaledTimeAsDouble - playStarted;
            double age = deathAge - DeathFlare.DeathLaunchDelay;
            if (deathAge > 0.15 && deathAge < 1.9)
            {
                checkedDelay = true;
                Check(liveFlare.transform.position == Vector3.up * 0.5f, "no motion during death delay");
                Check(!liveFlare.GetComponentInChildren<Light>().enabled, "no light during death delay");
                foreach (var particles in liveFlare.GetComponentsInChildren<ParticleSystem>())
                    Check(particles.particleCount == 0 && !particles.isPlaying, "no particles during death delay");
                var audioField = typeof(DeathFlare).GetField("launchAudio", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                Check(!((AudioHandle)audioField.GetValue(liveFlare)).IsPlaying, "no audio during death delay");
            }
            if ((int)age / 5 > lastCheckSecond)
            {
                lastCheckSecond = (int)age / 5;
                Debug.Log($"[DeathFlare] Play age {age:F2}, position {(liveFlare != null ? liveFlare.transform.position.ToString("F3") : "destroyed")}, timeScale {Time.timeScale}");
            }
            if (!checkedAudio && age > 0.15)
            {
                checkedAudio = true;
                Check(checkedDelay, "observed two-second death delay");
                var field = typeof(DeathFlare).GetField("launchAudio", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                Check(((AudioHandle)field.GetValue(liveFlare)).IsPlaying, "pooled AudioManager launch sound");
                Check(liveFlare.GetComponentInChildren<ParticleSystem>().particleCount > 0, "live particles");
                Time.timeScale = 0; // Motion and the lifetime must still complete when game time pauses.
            }
            if (!checkedDescent && age > 5)
            {
                checkedDescent = true;
                Check(liveFlare != null && liveFlare.transform.position.y > 0.5f && liveFlare.transform.position.y < 24.5f,
                    "live descent while paused: " + (liveFlare != null ? liveFlare.transform.position.ToString("F3") : "destroyed"));
            }
            if (age < DeathFlare.Lifetime - 0.1) Check(liveFlare != null, "survives until lifetime expires");
            if (age < DeathFlare.Lifetime + 0.2) return;
            Check(liveFlare == null, "destroyed at lifetime expiry while paused");
            Debug.Log($"[DeathFlare] Play checks passed: silent/invisible two-second death delay, pooled launch audio, live particles, ascent/descent, full {DeathFlare.Lifetime}-second lifetime after launch, cleanup at timeScale zero.");
            FinishPlayChecks(0);
        }
        catch (Exception exception) { Debug.LogException(exception); FinishPlayChecks(1); }
    }

    static void FinishPlayChecks(int code)
    {
        Time.timeScale = 1;
        SessionState.SetBool(PlayCheckKey, false);
        EditorApplication.update -= CheckPlayFrame;
        EditorApplication.Exit(code);
    }
    public const string PrefabPath = "Assets/Prefabs/Effects/DeathFlare.prefab";
    const string MaterialPath = "Assets/Materials/Effects/DeathFlareGlow.mat";
    const string FlareShaderName = "DuckZ/Particles/Death Flare No Fog";
    const string TexturePath = "Assets/Materials/Effects/DeathFlareSoft.png";
    const string ClipPath = "Assets/Audio/Player/DeathFlare/DeathFlare.ogg";
    const string EventPath = "Assets/Audio/Events/Player_DeathFlareLaunch.asset";

    [MenuItem("Tools/DuckZ/Authoring/Effects/Rebuild Death Flare")]
    public static void Build()
    {
        Directory.CreateDirectory("Assets/Materials/Effects");
        Check(File.Exists(ClipPath), "launch audio missing: " + ClipPath);
        BuildTexture();
        AssetDatabase.Refresh();
        var importer = (TextureImporter)AssetImporter.GetAtPath(TexturePath);
        importer.alphaIsTransparency = true;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.mipmapEnabled = false;
        importer.SaveAndReimport();
        var audioImporter = (AudioImporter)AssetImporter.GetAtPath(ClipPath);
        audioImporter.forceToMono = true;
        var audioSettings = audioImporter.defaultSampleSettings;
        audioSettings.preloadAudioData = true;
        audioImporter.defaultSampleSettings = audioSettings;
        audioImporter.SaveAndReimport();

        SoundEvent sound = AssetDatabase.LoadAssetAtPath<SoundEvent>(EventPath);
        if (sound == null) { sound = ScriptableObject.CreateInstance<SoundEvent>(); AssetDatabase.CreateAsset(sound, EventPath); }
        var so = new SerializedObject(sound);
        so.FindProperty("clips").arraySize = 1;
        so.FindProperty("clips").GetArrayElementAtIndex(0).objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>(ClipPath);
        so.FindProperty("pitchRange").vector2Value = Vector2.one;
        so.FindProperty("volumeRange").vector2Value = new Vector2(0.9f, 0.9f);
        so.FindProperty("minDistance").floatValue = 12f;
        so.FindProperty("maxDistance").floatValue = 180f;
        so.FindProperty("cullDistance").floatValue = 200f;
        so.FindProperty("priority").intValue = 48;
        so.FindProperty("maxInstances").intValue = 8;
        so.ApplyModifiedPropertiesWithoutUndo();

        Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        Shader shader = Shader.Find(FlareShaderName);
        Check(shader != null, "fog-free flare shader");
        if (material == null) { material = new Material(shader); AssetDatabase.CreateAsset(material, MaterialPath); }
        material.shader = shader;
        material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath));
        material.SetColor("_BaseColor", Color.white);
        material.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.SetOverrideTag("RenderType", "Transparent");
        material.renderQueue = (int)RenderQueue.Transparent;
        EditorUtility.SetDirty(material);

        var root = new GameObject("DeathFlare");
        try
        {
            var flare = root.AddComponent<DeathFlare>();
            Particles(root, "Red Halo", material, 22, 0.18f, 2.4f, 0f, new Color(1f, 0.015f, 0.008f, 0.26f), false);
            Particles(root, "White Hot Core", material, 28, 0.12f, 0.45f, 0.04f, new Color(1f, 0.68f, 0.42f, 1f), false);
            Particles(root, "Falling Red Embers", material, 65, 1.25f, 0.13f, 1.2f, new Color(1f, 0.055f, 0.012f, 1f), true);
            Particles(root, "Launch Trail", material, 100, 0.8f, 0.3f, 0.1f, new Color(1f, 0.12f, 0.02f, 0.8f), true, false);
            var lightObject = new GameObject("Red Point Light");
            lightObject.transform.SetParent(root.transform, false);
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.015f, 0.008f);
            light.intensity = 7f;
            light.range = 4f;
            light.enabled = false;
            light.shadows = LightShadows.None;
            light.renderMode = LightRenderMode.ForcePixel;
            var flareSettings = new SerializedObject(flare);
            flareSettings.FindProperty("launchSound").objectReferenceValue = sound;
            flareSettings.FindProperty("flareLight").objectReferenceValue = light;
            flareSettings.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }

        var prefab = AssetDatabase.LoadAssetAtPath<DeathFlare>(PrefabPath);
        Assign("Assets/Prefabs/Player/DuckPlayer.prefab", typeof(GR.Player), prefab);
        Assign("Assets/Prefabs/Player/DesktopPlayer.prefab", typeof(GR.Player), prefab);
        Assign("Assets/Prefabs/Photon/PhotonPlayer.prefab", typeof(PhotonPlayer), prefab);
        Check(SoundRegistry.Refresh(out string error), error);
        AssetDatabase.SaveAssets();
        Validate();
        RenderPreview();
        Debug.Log("[DeathFlare] Assets built, wired and validated.");
    }

    static void Assign(string path, Type type, DeathFlare flare)
    {
        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path).GetComponentInChildren(type, true);
        if (existing != null && new SerializedObject(existing).FindProperty("deathFlarePrefab").objectReferenceValue == flare) return;
        var root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            var component = root.GetComponentInChildren(type, true);
            Check(component != null, path + " component");
            var so = new SerializedObject(component);
            so.FindProperty("deathFlarePrefab").objectReferenceValue = flare;
            so.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    static void Particles(GameObject root, string name, Material material, float rate, float life,
        float size, float speed, Color color, bool world, bool loop = true)
    {
        var child = new GameObject(name);
        child.transform.SetParent(root.transform, false);
        var ps = child.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.duration = loop ? DeathFlare.Lifetime : 1.25f;
        main.loop = loop;
        main.playOnAwake = true;
        main.useUnscaledTime = true;
        main.startLifetime = life;
        main.startSize = size;
        main.startSpeed = speed;
        main.startColor = color;
        main.gravityModifier = world ? 0.08f : 0f;
        main.simulationSpace = world ? ParticleSystemSimulationSpace.World : ParticleSystemSimulationSpace.Local;
        main.maxParticles = 160;
        main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
        var emission = ps.emission; emission.rateOverTime = rate;
        var shape = ps.shape; shape.enabled = true; shape.shapeType = ParticleSystemShapeType.Sphere; shape.radius = world ? 0.07f : 0.015f;
        var colors = ps.colorOverLifetime;
        colors.enabled = true;
        var gradient = new Gradient();
        gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(new Color(1f, 0.15f, 0.05f), 1) },
            new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(1, 0.08f), new GradientAlphaKey(0, 1) });
        colors.color = gradient;
        var renderer = child.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = material;
        renderer.maxParticleSize = 1f;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        ps.useAutoRandomSeed = false;
        ps.randomSeed = (uint)(name.Length * 997);
    }

    static void BuildTexture()
    {
        var texture = new Texture2D(64, 64, TextureFormat.RGBA32, false);
        for (int y = 0; y < 64; y++)
            for (int x = 0; x < 64; x++)
            {
                float radius = new Vector2((x - 31.5f) / 31.5f, (y - 31.5f) / 31.5f).magnitude;
                float alpha = Mathf.Pow(Mathf.Clamp01(1f - radius), 2.4f);
                texture.SetPixel(x, y, new Color(1, 1, 1, alpha));
            }
        texture.Apply();
        File.WriteAllBytes(TexturePath, texture.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(texture);
    }

    [MenuItem("Tools/DuckZ/Authoring/Effects/Open Death Flare Prefab")]
    public static void OpenPrefab()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        Selection.activeObject = prefab;
        EditorGUIUtility.PingObject(prefab);
        AssetDatabase.OpenAsset(prefab);
    }

    public static void Validate()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<DeathFlare>(PrefabPath);
        Check(prefab != null, "prefab exists");
        Check(prefab.GetComponentsInChildren<ParticleSystem>().Length == 4, "four authored particle systems");
        var light = prefab.GetComponentInChildren<Light>();
        Check(light.type == LightType.Point && light.color.r > light.color.g * 10, "attached red point light");
        var sound = AssetDatabase.LoadAssetAtPath<SoundEvent>(EventPath);
        Check(sound.TrySelect(null, out var selection) && selection.Clip.length > 0
            && selection.Clip == AssetDatabase.LoadAssetAtPath<AudioClip>(ClipPath), "launch sound clip");
        foreach (string path in new[] { "Assets/Prefabs/Player/DuckPlayer.prefab", "Assets/Prefabs/Player/DesktopPlayer.prefab", "Assets/Prefabs/Photon/PhotonPlayer.prefab", "Assets/Resources/DuckCompanion.prefab" })
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Component component = asset.GetComponent<GR.Player>();
            if (component == null) component = asset.GetComponent<PhotonPlayer>();
            Check(new SerializedObject(component).FindProperty("deathFlarePrefab").objectReferenceValue == prefab, path + " flare reference");
        }
        // Physics fixtures only run in the separate batch editor, never over a user's open scene.
        if (!Application.isBatchMode) return;
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        // NewScene unloads unused assets, including prefab components held by managed references.
        prefab = AssetDatabase.LoadAssetAtPath<DeathFlare>(PrefabPath);
        GameObject floor = null;
        try
        {
            // Ordinary scenes use default physics; editor preview scenes do not.
            floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.layer = LayerMask.NameToLayer("Environment");
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(floor, scene);
            floor.transform.position = new Vector3(10000, -0.5f, 10000);
            floor.transform.localScale = new Vector3(10, 1, 10);
            Physics.SyncTransforms();
            CheckMotion(prefab, scene, new Vector3(10000, 0.5f, 10000), 0.13f, 24.5f);
            floor.transform.position = new Vector3(10000, 5f, 10000);
            Physics.SyncTransforms();
            CheckMotion(prefab, scene, new Vector3(10000, 6f, 10000), 5.63f, 30f);
            CheckMotion(prefab, scene, new Vector3(10000, 0.5f, 10000), float.NaN, 4.37f);
        }
        finally { if (floor != null) UnityEngine.Object.DestroyImmediate(floor); }
        Check(DeathFlare.Spawn(prefab, Vector3.zero, DeathFlare.Lifetime) == null, "expired packet does not spawn");
        Debug.Log("[DeathFlare] Checks passed: player/remote/companion references, audio, floor/roof landing, ceiling obstruction, expired event.");
    }

    static void CheckMotion(DeathFlare prefab, UnityEngine.SceneManagement.Scene scene, Vector3 position, float landingY, float apexY)
    {
        var flare = UnityEngine.Object.Instantiate(prefab, position, Quaternion.identity);
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(flare.gameObject, scene);
        // Use a post-launch age to avoid playing sound in Edit mode.
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        typeof(DeathFlare).GetMethod("Initialize", flags).Invoke(flare, new object[] { 1.25f });
        Check(Mathf.Abs(flare.transform.position.y - apexY) < 0.04f, "launch height/ceiling " + flare.transform.position.y);
        var advance = typeof(DeathFlare).GetMethod("Advance", flags);
        // Exercise landing independently of lifetime; a full-height flare may expire before reaching the floor.
        for (float age = 1.3f; age < 30; age += 0.05f) advance.Invoke(flare, new object[] { age });
        if (!float.IsNaN(landingY)) Check(Mathf.Abs(flare.transform.position.y - landingY) < 0.04f, "first surface landing " + flare.transform.position.y);
        UnityEngine.Object.DestroyImmediate(flare.gameObject);
    }

    public static void RenderPreview()
    {
        var preview = new PreviewRenderUtility();
        try
        {
            var root = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath));
            preview.AddSingleGO(root);
            var particles = root.GetComponentsInChildren<ParticleSystem>();
            foreach (var ps in particles) ps.Simulate(0, false, true);
            for (int step = 0; step < 75; step++)
            {
                root.transform.position = Vector3.up * (step / 74f * 5f);
                foreach (var ps in particles) ps.Simulate(1f / 60f, false, false);
            }
            preview.camera.transform.position = new Vector3(0, 4.2f, -9);
            preview.camera.transform.LookAt(new Vector3(0, 3.6f, 0));
            preview.camera.clearFlags = CameraClearFlags.SolidColor;
            preview.camera.backgroundColor = new Color(0.025f, 0.035f, 0.06f);
            preview.camera.fieldOfView = 42;
            preview.camera.nearClipPlane = 0.05f;
            preview.camera.farClipPlane = 100;
            preview.BeginStaticPreview(new Rect(0, 0, 900, 900));
            preview.Render(true);
            Texture2D image = preview.EndStaticPreview();
            Directory.CreateDirectory(".utmp/death-flare");
            File.WriteAllBytes(".utmp/death-flare/DeathFlare-preview.png", image.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(image);
        }
        finally { preview.Cleanup(); }
    }

    static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("Death flare check failed: " + message);
    }
}
