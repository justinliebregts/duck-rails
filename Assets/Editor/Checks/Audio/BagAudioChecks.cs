using System;
using System.Linq;
using System.Reflection;
using Game.Audio;
using UnityEditor;
using UnityEngine;

public static class BagAudioChecks
{
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    [MenuItem("Tools/DuckZ/Validation/Audio/Validate Bag Overrides")]
    public static void Run()
    {
        if (!SoundRegistry.Refresh(out string error)) throw new Exception(error);
        var catalog = AssetDatabase.LoadAssetAtPath<StoreCatalog>("Assets/Resources/StoreCatalog.asset");
        var settings = AssetDatabase.LoadAssetAtPath<Game.Audio.AudioSettings>("Assets/Resources/GameAudioSettings.asset");
        var gorilla = catalog.FindBag(4);
        Require(gorilla != null, "Gorilla catalog entry missing.");
        var overrides = new[] { gorilla.bagGrabOverride, gorilla.bagStowOverride, gorilla.bagDropOverride };
        var names = new[] { "Bag_Gorilla_Grab", "Bag_Gorilla_PutAway", "Bag_Gorilla_Drop" };
        for (int i = 0; i < names.Length; i++)
        {
            var sound = overrides[i];
            Require(sound != null && sound.name == names[i], "Wrong Gorilla event: " + names[i]);
            Require(sound.TrySelect(null, out SoundSelection selection) && selection.Clip.name == names[i], "Missing/wrong clip: " + names[i]);
            Require(settings.sounds.Contains(sound), "Event missing from multiplayer registry.");
        }
        Require(gorilla.bagHoverOverride == null && gorilla.bagPutOverride == null, "Gorilla should retain default hover and insert.");
        GameObject root = PrefabUtility.LoadPrefabContents("Assets/Prefabs/Bags/Bag.prefab");
        try
        {
            var bag = root.GetComponent<BagObject>();
            var impacts = root.GetComponent<ItemSounds>();
            typeof(BagObject).GetField("impactSounds", Private).SetValue(bag, impacts);
            var defaults = Read(bag, impacts);
            foreach (var sound in defaults)
                Require(sound != null && sound.TrySelect(null, out _), "Default event or clip missing after folder move.");
            Apply(bag, 4);
            var actual = Read(bag, impacts);
            Require(actual[0] == overrides[0] && actual[1] == overrides[1] && actual[2] == overrides[2], "Gorilla overrides not resolved.");
            Require(actual[3] == defaults[3] && actual[4] == defaults[4], "Partial fallback failed.");
            foreach (byte id in new byte[] { 2, 1, 3, 0, 255 })
            {
                Apply(bag, 4);
                Apply(bag, id);
                Require(Read(bag, impacts).SequenceEqual(defaults), "Defaults not restored for bag " + id);
            }
            impacts.SetDropOverride(overrides[2]);
            Require(impacts.Get(ItemSound.Drop) == overrides[2], "Drop override failed.");
            impacts.SetDropOverride(null);
            Require(impacts.Get(ItemSound.Drop) == defaults[2], "Drop reset failed.");
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        Debug.Log("BagAudioChecks PASS: Gorilla clip bindings, registry, per-action fallbacks, bag switching, unknown IDs, and drop reset.");
    }

    static void Apply(BagObject bag, byte id)
    {
        typeof(BagObject).GetField("bagId", Private).SetValue(bag, id);
        typeof(BagObject).GetMethod("ApplyAudioOverrides", Private).Invoke(bag, null);
    }

    static SoundEvent[] Read(BagObject bag, ItemSounds impacts) => new[] {
        (SoundEvent)typeof(BagObject).GetProperty("GrabSound", Private).GetValue(bag),
        (SoundEvent)typeof(BagObject).GetProperty("StowSound", Private).GetValue(bag),
        impacts.Get(ItemSound.Drop), bag.HoverSound, bag.PutSound
    };

    static void Require(bool value, string message)
    {
        if (!value) throw new Exception(message);
    }
}
