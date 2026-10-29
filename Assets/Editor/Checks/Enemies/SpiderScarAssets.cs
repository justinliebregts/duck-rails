using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

// Explicit authoring command; never runs automatically or modifies the blood assets.
public static class SpiderScarAssets
{
    public static void Build()
    {
        const string texturePath = "Assets/Materials/SpiderScar/SpiderScar_Alpha.png";
        const string materialPath = "Assets/Materials/SpiderScar/SpiderScar.mat";
        const string prefabPath = "Assets/Resources/Effects/SpiderScarDecalPFX.prefab";
        System.IO.Directory.CreateDirectory("Assets/Resources/Effects");
        AssetDatabase.Refresh();
        var importer = (TextureImporter)AssetImporter.GetAtPath(texturePath);
        importer.textureType = TextureImporterType.Default;
        importer.alphaSource = TextureImporterAlphaSource.FromInput;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = true;
        importer.isReadable = false;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.filterMode = FilterMode.Bilinear;
        importer.anisoLevel = 1;
        importer.maxTextureSize = 256;
        importer.textureCompression = TextureImporterCompression.Compressed;
        importer.SetPlatformTextureSettings(new TextureImporterPlatformSettings {
            name = "Android", overridden = true, maxTextureSize = 256,
            format = TextureImporterFormat.ASTC_6x6, compressionQuality = 50
        });
        importer.SaveAndReimport();
        var blood = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Blood/Blood.mat");
        var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        if (material == null) { material = new Material(blood); AssetDatabase.CreateAsset(material, materialPath); }
        else material.CopyPropertiesFromMaterial(blood);
        material.name = "SpiderScar";
        material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath));
        foreach (string property in new[] { "_BaseColor", "_Color", "_TintColor" })
            if (material.HasProperty(property)) material.SetColor(property, Color.black);
        EditorUtility.SetDirty(material);

        var source = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Effects/BloodSplatterDecalPFX.prefab");
        var go = Object.Instantiate(source);
        go.name = "SpiderScarDecalPFX";
        for (int i = go.transform.childCount - 1; i >= 0; i--) Object.DestroyImmediate(go.transform.GetChild(i).gameObject);
        go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        go.transform.localScale = Vector3.one;
        var ps = go.GetComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.playOnAwake = false; main.loop = false; main.duration = 10f;
        main.startLifetime = 10f; main.startSpeed = 0f; main.startSize3D = false; main.startSize = 1f;
        main.startRotation3D = false; main.startRotation = 0f; main.startColor = Color.white;
        main.gravityModifier = 0f; main.maxParticles = 1;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        main.stopAction = ParticleSystemStopAction.None;
        var emission = ps.emission; emission.enabled = true; emission.rateOverTime = 0f; emission.rateOverDistance = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 1) });
        var shape = ps.shape; shape.enabled = false;
        var color = ps.colorOverLifetime; color.enabled = false;
        var colorSpeed = ps.colorBySpeed; colorSpeed.enabled = false;
        var size = ps.sizeOverLifetime; size.enabled = false;
        var sizeSpeed = ps.sizeBySpeed; sizeSpeed.enabled = false;
        var rotation = ps.rotationOverLifetime; rotation.enabled = false;
        var rotationSpeed = ps.rotationBySpeed; rotationSpeed.enabled = false;
        var velocity = ps.velocityOverLifetime; velocity.enabled = false;
        var limit = ps.limitVelocityOverLifetime; limit.enabled = false;
        var inherit = ps.inheritVelocity; inherit.enabled = false;
        var force = ps.forceOverLifetime; force.enabled = false;
        var noise = ps.noise; noise.enabled = false;
        var collision = ps.collision; collision.enabled = false;
        var trigger = ps.trigger; trigger.enabled = false;
        var sub = ps.subEmitters; sub.enabled = false;
        var trails = ps.trails; trails.enabled = false;
        var lights = ps.lights; lights.enabled = false;
        var sheet = ps.textureSheetAnimation; sheet.enabled = false;
        var renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = material;
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.alignment = ParticleSystemRenderSpace.Local;
        renderer.pivot = Vector3.zero;
        renderer.flip = Vector3.zero;
        renderer.minParticleSize = 0f; renderer.maxParticleSize = 1f;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.lightProbeUsage = LightProbeUsage.Off;
        renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        PrefabUtility.SaveAsPrefabAsset(go, prefabPath);
        Object.DestroyImmediate(go);
        AssetDatabase.SaveAssets();
        Debug.Log("SpiderScarAssets: assets saved");
    }
}
