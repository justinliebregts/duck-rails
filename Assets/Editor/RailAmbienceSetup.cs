using System;
using System.IO;
using System.Linq;
using Game.Audio;
using UnityEditor;
using UnityEngine;

public static class RailAmbienceSetup
{
    const string AudioFolder = "Assets/Audio/Ambience/";
    const string EventFolder = "Assets/Audio/Events/Ambience/";
    const string SettingsPath = "Assets/Resources/RailAmbienceSettings.asset";

    [Serializable] sealed class Manifest { public Track[] tracks; }
    [Serializable] sealed class Track { public string id; public bool bed; public float volume; }

    [MenuItem("Duck Rails/Audio/Select ambience settings")]
    public static void SelectSettings() => Selection.activeObject = AssetDatabase.LoadAssetAtPath<RailAmbienceSettings>(SettingsPath);

    [MenuItem("Duck Rails/Audio/Install approved ambience defaults")]
    public static void Install()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Install ambience in Edit Mode.");
        var manifest = JsonUtility.FromJson<Manifest>(File.ReadAllText(AudioFolder + "ArtlistSources.json"));
        foreach (var track in manifest.tracks)
        {
            string path = AudioFolder + track.id + ".wav";
            AssetDatabase.ImportAsset(path);
            var importer = (AudioImporter)AssetImporter.GetAtPath(path);
            importer.forceToMono = !track.bed;
            importer.loadInBackground = true;
            var samples = importer.defaultSampleSettings;
            samples.loadType = track.bed ? AudioClipLoadType.Streaming : AudioClipLoadType.CompressedInMemory;
            samples.compressionFormat = AudioCompressionFormat.Vorbis;
            samples.quality = 0.55f;
            samples.sampleRateSetting = AudioSampleRateSetting.OverrideSampleRate;
            samples.sampleRateOverride = 24000;
            samples.preloadAudioData = false;
            importer.defaultSampleSettings = samples;
            importer.SetOverrideSampleSettings("Android", samples);
            importer.SetOverrideSampleSettings("Standalone", samples);
            importer.SaveAndReimport();
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            if (clip == null || clip.length <= 0f) throw new InvalidOperationException("Invalid WAV " + path);
            var sound = AssetDatabase.LoadAssetAtPath<SoundEvent>(EventFolder + track.id + ".asset");
            if (sound == null)
            {
                sound = ScriptableObject.CreateInstance<SoundEvent>();
                AssetDatabase.CreateAsset(sound, EventFolder + track.id + ".asset");
            }
            var serialized = new SerializedObject(sound);
            var clips = serialized.FindProperty("clips");
            clips.arraySize = 1;
            clips.GetArrayElementAtIndex(0).objectReferenceValue = clip;
            serialized.FindProperty("pitchRange").vector2Value = Vector2.one;
            serialized.FindProperty("volumeRange").vector2Value = new Vector2(track.volume * 0.85f, track.volume);
            serialized.FindProperty("spatialBlend").floatValue = track.bed ? 0f : 1f;
            serialized.FindProperty("minDistance").floatValue = 3f;
            serialized.FindProperty("maxDistance").floatValue = 35f;
            serialized.FindProperty("cullDistance").floatValue = 40f;
            serialized.FindProperty("priority").intValue = track.bed ? 210 : 190;
            serialized.FindProperty("maxInstances").intValue = 2;
            serialized.FindProperty("useOcclusion").boolValue = !track.bed;
            serialized.FindProperty("reverbZoneMix").floatValue = track.bed ? 0f : 0.45f;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(sound);
        }

        var settings = AssetDatabase.LoadAssetAtPath<RailAmbienceSettings>(SettingsPath);
        if (settings == null)
        {
            settings = ScriptableObject.CreateInstance<RailAmbienceSettings>();
            AssetDatabase.CreateAsset(settings, SettingsPath);
        }
        settings.rareScares = Sounds("MysticSpirits", "GhostShriek");
        settings.profiles = new[]
        {
            Profile("starting-station-v1", Sounds("DistantWind"), Sounds("GentleLeaves"), false,
                Area("Supplies", -16.5f, 3, 86, 7, 6, 12, Sounds("LowElectricHum"), Sounds("HouseCreak")),
                Area("East rooms", 15.5f, 3, 86, 5, 6, 12, Sounds("Fluorescent"), Sounds("WaterDrops")),
                Area("Departure tunnel", 0, 3.5f, 116, 8, 7, 48, Sounds("HorridSignal"), Sounds("WaterDrops", "MetalCreak")),
                Area("Platform hall", 0, 4.5f, 80, 26, 9, 24, Sounds("LowElectricHum", "Fluorescent"), Sounds("MetalCreak", "WaterDrops"))),
            Profile("skyscraper-works-v1", Sounds("DistantWind", "HorridSignal"), Sounds("MetalCreak", "SubtleLeaves"), true,
                Area("Tower A", -25, 9.5f, 66, 24, 19, 22, Sounds("IndustrialEerie"), Sounds("MetalCreak")),
                Area("Tower B", 25, 9.5f, 66, 24, 19, 22, Sounds("IndustrialEerie"), Sounds("MetalCreak")),
                Area("Tower C", -25, 9.5f, 98, 24, 19, 22, Sounds("IndustrialEerie"), Sounds("MetalCreak")),
                Area("Tower D", 25, 9.5f, 98, 24, 19, 22, Sounds("IndustrialEerie"), Sounds("MetalCreak"))),
            Profile("container-yard-v1", Sounds("DistantWind", "IndustrialEerie"), Sounds("MetalCreak", "GentleLeaves"), true,
                Area("Repair shed", -33, 2.5f, 134, 17.5f, 5, 15.5f, Sounds("LowElectricHum", "IndustrialEerie"), Sounds("MetalCreak", "WaterDrops")),
                Area("West container passages", -25.5f, 1.4f, 80, 32, 2.8f, 61, Sounds("IndustrialEerie"), Sounds("MetalCreak")),
                Area("East container passages", 26.5f, 1.4f, 80.5f, 34, 2.8f, 63, Sounds("IndustrialEerie"), Sounds("MetalCreak"))),
            Profile("farm-silos-v1", Sounds("NightCicadas", "NightLeaves"), Sounds("SubtleLeaves", "GentleLeaves", "CrowCaw"), true,
                Area("Cellar", -27, -0.25f, 134, 17.5f, 2.9f, 19.5f, Sounds("Fluorescent", "HorridSignal"), Sounds("HouseCreak", "WaterDrops")),
                Area("Farmhouse", -27, 6.25f, 134, 17.5f, 9.3f, 19.5f, Sounds("SpookyAttic"), Sounds("HouseCreak")),
                Area("Main barn", 26, 3, 77.5f, 17.5f, 6, 22.5f, Sounds("SpookyAttic"), Sounds("HouseCreak", "MetalCreak")),
                Area("Feed barn", -25, 1.5f, 76, 17.5f, 3, 13.5f, Sounds("SpookyAttic"), Sounds("HouseCreak")),
                Area("Workshop", -26, 1.5f, 94, 15.5f, 3, 11.5f, Sounds("LowElectricHum"), Sounds("MetalCreak", "HouseCreak")),
                Area("Silo", 22, 9.7f, 98, 8.5f, 4.2f, 8.5f, Sounds("IndustrialEerie"), Sounds("MetalCreak"))),
            Profile("convenience-store-v1", Sounds("DistantWind", "Crickets"), Sounds("GentleLeaves", "CrowCaw"), true,
                Area("Quack Mart", 30, 2, 74, 19, 4, 23, Sounds("LowElectricHum", "Fluorescent"), Sounds("MetalCreak")),
                Area("Arcade", -25, 2, 76, 29, 4, 27, Sounds("MachineHum", "Fluorescent"), Sounds("MetalCreak")),
                Area("Car wash", 34, 1.9f, 45, 19, 3.8f, 17, Sounds("SlowLeak"), Sounds("WaterDrops", "MetalCreak")),
                Area("Restaurants", 25, 1.75f, 126, 29, 3.5f, 11, Sounds("LowElectricHum", "MachineHum"), Sounds("WaterDrops"))),
            Profile("roadside-motel-v1", Sounds("Crickets", "NightLeaves"), Sounds("SubtleLeaves", "CrowCaw"), true,
                Area("East guest wing", 34, 3.6f, 74, 11.5f, 7, 39, Sounds("SpookyAttic", "Fluorescent"), Sounds("HouseCreak")),
                Area("North guest wing", 25, 3.6f, 103, 29, 7, 11.5f, Sounds("SpookyAttic"), Sounds("HouseCreak")),
                Area("Change rooms", -32.5f, 1.85f, 100, 18, 3.4f, 17.5f, Sounds("SlowLeak", "Fluorescent"), Sounds("WaterDrops")),
                Area("Pool vendor", -16.5f, 1.85f, 100, 12, 3.4f, 17.5f, Sounds("MachineHum"), Sounds("WaterDrops"))),
            NuclearPlantProfile(),
            Profile("bridge-finale-v1", Sounds("DistantWind", "HorridSignal"), Sounds("MetalCreak"), true,
                Area("Gorge floor", 0, -6, 100, 96, 10, 38, Sounds("RiverRush"), Sounds("WaterDrops", "MetalCreak")),
                Area("Bridge and river banks", 0, 2, 100, 96, 6, 38, Sounds("GentleStream", "DistantWind"), Sounds("MetalCreak")),
                Area("Escape tunnel", 0, 3.4f, 140, 10.8f, 6.8f, 39, Sounds("HorridSignal", "SlowLeak"), Sounds("WaterDrops")))
        };
        EditorUtility.SetDirty(settings);
        AssetDatabase.SaveAssets();
        Debug.Log($"Installed {manifest.tracks.Length} ambience clips/events and {settings.profiles.Length} location profiles.");
    }

    public static RailAmbienceProfile NuclearPlantProfile() =>
        Profile("nuclear-power-plant-v1", Sounds("DistantWind"), Sounds("MetalCreak"), false,
            Area("Turbine hall", -26, 4, 73, 27.6f, 8, 35.6f, Sounds("MachineHum"), Sounds("MetalCreak")),
            Area("West cooling tower", -26, 18, 103, 18, 36, 18, Sounds("IndustrialEerie"), Sounds("MetalCreak")),
            Area("East cooling tower", 26, 18, 103, 18, 36, 18, Sounds("DistantWind", "LowElectricHum"), Sounds("MetalCreak")),
            Area("Containment", 30, 6, 72, 17, 12, 17, Sounds("LowElectricHum"), Sounds("WaterDrops", "MetalCreak")),
            Area("Receiving office", -32, 1.8f, 42, 12, 3.6f, 10, Sounds("Fluorescent"), Sounds("MetalCreak")),
            Area("Spill workshop", 33.5f, 1.8f, 43, 13, 3.6f, 12, Sounds("MachineHum"), Sounds("MetalCreak")),
            Area("Pump hut", -32, 1.8f, 145, 9.8f, 3.6f, 9.8f, Sounds("MachineHum"), Sounds("WaterDrops")),
            Area("Settling pond", -26, 4, 130, 30, 8, 21, Sounds("GentleStream"), Sounds("WaterDrops")),
            Area("Discharge flume", 26, 4, 149, 28, 9, 19, Sounds("RiverRush"), Sounds("WaterDrops")),
            Area("Transformer yard", 27, 2.5f, 130, 24, 5, 18, Sounds("LowElectricHum"), Sounds("MetalCreak")),
            Area("Waste loading apron", 25, 2, 25, 27, 5, 20, Sounds("SlowLeak"), Sounds("WaterDrops")));

    static SoundEvent[] Sounds(params string[] names) => names.Select(name =>
        AssetDatabase.LoadAssetAtPath<SoundEvent>(EventFolder + name + ".asset") ??
        throw new InvalidOperationException("Missing ambience event " + name)).ToArray();

    static RailAmbienceProfile Profile(string id, SoundEvent[] beds, SoundEvent[] details, bool scares, params RailAmbienceArea[] areas)
        => new RailAmbienceProfile { blockId = id, outdoorBeds = beds, outdoorDetails = details, allowScares = scares, areas = areas };

    static RailAmbienceArea Area(string name, float x, float y, float z, float sx, float sy, float sz, SoundEvent[] beds, SoundEvent[] details)
        => new RailAmbienceArea { name = name, bounds = new Bounds(new Vector3(x, y, z), new Vector3(sx, sy, sz)), beds = beds, details = details };
}
