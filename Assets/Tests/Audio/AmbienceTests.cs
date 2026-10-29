using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Game.Audio;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

public sealed class AmbienceTests
{
    readonly List<Object> created = new List<Object>();
    AudioManager manager;
    Game.Audio.AudioSettings settings;
    AudioListener listener;
    AudioClip clip;

    [SetUp]
    public void SetUp()
    {
        foreach (var existing in Object.FindObjectsByType<AudioManager>(FindObjectsSortMode.None))
            Object.DestroyImmediate(existing.gameObject);
        AudioListener.pause = false;
        Time.timeScale = 1f;
        settings = Keep(ScriptableObject.CreateInstance<Game.Audio.AudioSettings>());
        settings.poolSize = 4;
        settings.ambienceVoiceLimit = 2;
        listener = Go("Listener").AddComponent<AudioListener>();
        var root = Go("Manager");
        root.SetActive(false);
        manager = root.AddComponent<AudioManager>();
        Set(manager, "settings", settings);
        Set(manager, "listener", listener);
        root.SetActive(true);
        clip = Keep(AudioClip.Create("Ambience test", 480000, 1, 48000, false));
    }

    [TearDown]
    public void TearDown()
    {
        AudioListener.pause = false;
        Time.timeScale = 1f;
        // Release sources before destroying their clips/settings.
        if (manager != null) Object.DestroyImmediate(manager.gameObject);
        for (int i = created.Count - 1; i >= 0; i--)
            if (created[i] != null) Object.DestroyImmediate(created[i]);
        created.Clear();
    }

    [Test]
    public void AmbienceCapIncludesOneShotsAndLeavesGameplayCapacity()
    {
        var sound = Sound();
        Assert.That(Ambient(sound).IsPlaying, Is.True);
        Assert.That(manager.PlayAmbience(sound, Go("One-shot").transform, null, false).IsPlaying, Is.True);
        Assert.That(Ambient(sound).IsPlaying, Is.False);
        Assert.That(manager.ActiveAmbienceCount, Is.EqualTo(2));
        Assert.That(manager.Play(Sound(), Vector3.zero).IsPlaying, Is.True);
        Assert.That(manager.Play(Sound(), Vector3.zero).IsPlaying, Is.True);
        Assert.That(manager.ActiveCount, Is.EqualTo(4));
    }

    [Test]
    public void GameplayReclaimsAmbienceEvenWhenItsNumericPriorityIsLowerImportance()
    {
        var first = Ambient(Sound(20), new Vector3(10, 0, 0));
        var second = Ambient(Sound(20));
        var protectedLoop = manager.PlayLoop(Sound(), Go("Gameplay loop").transform);
        var effect = manager.Play(Sound(), Vector3.zero);
        Assert.That(manager.Play(Sound(250), Vector3.zero).IsPlaying, Is.True);
        Assert.That(first.IsPlaying, Is.False);
        Assert.That(second.IsPlaying && protectedLoop.IsPlaying && effect.IsPlaying, Is.True);
        Assert.That(manager.ActiveAmbienceCount, Is.EqualTo(1));
        first.Stop(); // A stale handle must not stop the new gameplay voice.
        Assert.That(manager.ActiveCount, Is.EqualTo(4));
    }

    [Test]
    public void AmbienceNeverStealsGameplayAndGameplayLoopsStayProtected()
    {
        var sound = Sound(250);
        for (int i = 0; i < 4; i++) Assert.That(manager.PlayLoop(sound, Go("Loop").transform).IsPlaying, Is.True);
        Assert.That(Ambient(Sound(0)).IsPlaying, Is.False);
        Assert.That(manager.Play(Sound(0), Vector3.zero).IsPlaying, Is.False);
        Assert.That(manager.ActiveAmbienceCount, Is.Zero);
    }

    [Test]
    public void NormalPriorityStealingStillWorks()
    {
        var weak = Sound(240);
        for (int i = 0; i < 4; i++) manager.Play(weak, Vector3.zero);
        Assert.That(manager.Play(Sound(10), Vector3.zero).IsPlaying, Is.True);
        Assert.That(manager.ActiveCount, Is.EqualTo(4));
    }

    [Test]
    public void ZeroCapAndMissingListenerRejectAmbience()
    {
        settings.ambienceVoiceLimit = 0;
        Assert.That(Ambient(Sound()).IsPlaying, Is.False);
        settings.ambienceVoiceLimit = 99;
        Assert.That(manager.AmbienceCapacity, Is.EqualTo(3));
        listener.enabled = false;
        manager.SetListener(null);
        Assert.That(Ambient(Sound()).IsPlaying, Is.False);
    }

    [UnityTest]
    public IEnumerator NearerAmbienceFadesOutTheLeastRelevantWithoutExceedingCap()
    {
        var sound = Sound();
        var far = Ambient(sound, new Vector3(90, 0, 0));
        var near = Ambient(sound, new Vector3(10, 0, 0));
        Assert.That(Ambient(sound, new Vector3(5, 0, 0)).IsPlaying, Is.False);
        Assert.That(manager.ActiveAmbienceCount, Is.EqualTo(2)); // Fading still consumes capacity.
        yield return new WaitForSecondsRealtime(0.4f);
        Assert.That(far.IsPlaying, Is.False);
        Assert.That(near.IsPlaying, Is.True);
        Assert.That(Ambient(sound, new Vector3(5, 0, 0)).IsPlaying, Is.True);
        Assert.That(manager.ActiveAmbienceCount, Is.EqualTo(2));
    }

    [UnityTest]
    public IEnumerator EventLimitAllowsANearerReplacementButNotAnExtraVoice()
    {
        var sound = Sound();
        Set(sound, "maxInstances", 1);
        var far = Ambient(sound, new Vector3(90, 0, 0));
        Assert.That(Ambient(sound).IsPlaying, Is.False);
        yield return new WaitForSecondsRealtime(0.4f);
        Assert.That(far.IsPlaying, Is.False);
        Assert.That(Ambient(sound).IsPlaying, Is.True);
        Assert.That(manager.ActiveAmbienceCount, Is.EqualTo(1));
    }

    [UnityTest]
    public IEnumerator SimilarDistancesDoNotChurnAndFadesDoNotRestart()
    {
        var sound = Sound();
        var a = Ambient(sound, new Vector3(40, 0, 0));
        var b = Ambient(sound, new Vector3(45, 0, 0));
        Assert.That(Ambient(sound, new Vector3(39, 0, 0)).IsPlaying, Is.False);
        yield return new WaitForSecondsRealtime(0.35f);
        Assert.That(a.IsPlaying && b.IsPlaying, Is.True);
        b.Stop(0.3f);
        yield return new WaitForSecondsRealtime(0.2f);
        b.Stop(0.3f);
        yield return new WaitForSecondsRealtime(0.2f);
        Assert.That(b.IsPlaying, Is.False);
        Assert.That(manager.ActiveAmbienceCount, Is.EqualTo(1));
    }

    [UnityTest]
    public IEnumerator FadeInAndOcclusionOptOutUseTheExistingPool()
    {
        var sound = Sound();
        Set(sound, "useOcclusion", false);
        manager.PlayAmbience(sound, Go("Origin").transform, null, true, 0.2f);
        AudioSource source = null;
        foreach (var candidate in manager.GetComponentsInChildren<AudioSource>())
            if (candidate.clip != null) source = candidate;
        Assert.That(source, Is.Not.Null);
        Assert.That(source.volume, Is.Zero);
        Assert.That(source.GetComponent<AudioLowPassFilter>().enabled, Is.False);
        yield return new WaitForSecondsRealtime(0.3f);
        Assert.That(source.volume, Is.EqualTo(1f).Within(0.01f));
        Assert.That(source.GetComponent<AudioLowPassFilter>().enabled, Is.False);
    }

    [UnityTest]
    public IEnumerator DistanceHysteresisStopsAndRestartsTheEmitter()
    {
        var emitter = Emitter();
        yield return new WaitForSecondsRealtime(0.3f);
        Assert.That(emitter.IsPlaying, Is.True);
        listener.transform.position = Vector3.right * 11f;
        yield return new WaitForSecondsRealtime(0.3f);
        Assert.That(emitter.IsPlaying, Is.True); // 10m entry, 13m exit.
        listener.transform.position = Vector3.right * 14f;
        yield return new WaitForSecondsRealtime(0.5f);
        Assert.That(emitter.IsPlaying, Is.False);
        Assert.That(manager.ActiveAmbienceCount, Is.Zero);
        listener.transform.position = Vector3.zero;
        yield return new WaitForSecondsRealtime(1.5f);
        Assert.That(emitter.IsPlaying, Is.True);
        emitter.enabled = false;
        Assert.That(manager.ActiveAmbienceCount, Is.Zero);
    }

    [UnityTest]
    public IEnumerator OutdoorEmitterStopsInZoneAndReturnsAfterExit()
    {
        var zone = Go("Subway").AddComponent<AmbienceZone>();
        Set(zone, "size", new Vector3(4, 4, 4));
        var emitter = Emitter(zone, true);
        listener.transform.position = Vector3.right * 3f;
        yield return new WaitForSecondsRealtime(0.3f);
        Assert.That(emitter.IsPlaying, Is.True);
        listener.transform.position = Vector3.right * 1.8f;
        yield return new WaitForSecondsRealtime(0.3f);
        Assert.That(emitter.IsPlaying, Is.True); // Boundary slack prevents toggling.
        listener.transform.position = Vector3.zero;
        yield return new WaitForSecondsRealtime(0.5f);
        Assert.That(emitter.IsPlaying, Is.False);
        listener.transform.position = Vector3.right * 3f;
        yield return new WaitForSecondsRealtime(1.5f);
        Assert.That(emitter.IsPlaying, Is.True);
    }

    [UnityTest]
    public IEnumerator InsideZoneAndMissingListenerReleaseTheirVoices()
    {
        var zone = Go("Interior").AddComponent<AmbienceZone>();
        Set(zone, "size", new Vector3(4, 4, 4));
        var emitter = Emitter(zone, false);
        yield return new WaitForSecondsRealtime(0.3f);
        Assert.That(emitter.IsPlaying, Is.True);
        listener.enabled = false;
        yield return new WaitForSecondsRealtime(0.5f);
        Assert.That(emitter.IsPlaying, Is.False);
        listener.enabled = true;
        yield return new WaitForSecondsRealtime(1.5f);
        Assert.That(emitter.IsPlaying, Is.True);
        zone.enabled = false;
        yield return new WaitForSecondsRealtime(0.5f);
        Assert.That(emitter.IsPlaying, Is.False);
    }

    [Test]
    public void ZoneHonoursRotationScaleAndWorldSpaceHysteresis()
    {
        var zone = Go("Rotated zone").AddComponent<AmbienceZone>();
        zone.transform.SetPositionAndRotation(new Vector3(10, -7, 20), Quaternion.Euler(0, 45, 0));
        zone.transform.localScale = new Vector3(8, 2, 4);
        Set(zone, "size", Vector3.one);
        Assert.That(zone.Allows(zone.transform.position, false, false), Is.True);
        Vector3 point = zone.transform.TransformPoint(new Vector3(0.55f, 0, 0));
        Assert.That(zone.Allows(point, false, false), Is.False);
        Assert.That(zone.Allows(point, false, true), Is.True); // 0.4 world metres outside.
        point = zone.transform.TransformPoint(new Vector3(0.65f, 0, 0));
        Assert.That(zone.Allows(point, false, true), Is.False); // 1.2 world metres outside.
    }

    [UnityTest]
    public IEnumerator OwnerDestructionAndManagerDisableClearAccounting()
    {
        var owner = Emitter();
        yield return new WaitForSecondsRealtime(0.3f);
        Assert.That(manager.ActiveAmbienceCount, Is.EqualTo(1));
        Object.Destroy(owner.gameObject);
        yield return null;
        Assert.That(manager.ActiveAmbienceCount, Is.Zero);
        Ambient(Sound());
        manager.enabled = false;
        Assert.That(manager.ActiveCount, Is.Zero);
        Assert.That(manager.ActiveAmbienceCount, Is.Zero);
    }

    AudioHandle Ambient(SoundEvent sound, Vector3 position = default)
    {
        var origin = Go("Ambient origin").transform;
        origin.position = position;
        return manager.PlayAmbience(sound, origin, null, true);
    }

    AmbientEmitter Emitter(AmbienceZone zone = null, bool outside = false)
    {
        var go = Go("Emitter");
        go.SetActive(false);
        var emitter = go.AddComponent<AmbientEmitter>();
        Set(emitter, "sound", Sound());
        Set(emitter, "loop", true);
        Set(emitter, "activationDistance", 10f);
        Set(emitter, "fadeOut", 0.1f);
        Set(emitter, "zone", zone);
        Set(emitter, "outsideZone", outside);
        go.SetActive(true);
        return emitter;
    }

    SoundEvent Sound(int priority = 220)
    {
        var sound = Keep(ScriptableObject.CreateInstance<SoundEvent>());
        Set(sound, "clips", new[] { clip });
        Set(sound, "priority", priority);
        Set(sound, "maxInstances", 0);
        Set(sound, "pitchRange", Vector2.one);
        Set(sound, "maxDistance", 100f);
        return sound;
    }

    GameObject Go(string name) => Keep(new GameObject(name));
    T Keep<T>(T value) where T : Object { created.Add(value); return value; }
    static void Set(object target, string field, object value)
        => target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
}
