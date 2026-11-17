using Game.Audio;
using UnityEngine;

/// <summary>Local listener ambience using the existing, gameplay-reclaimable audio pool.</summary>
public sealed class RailAmbientDirector : MonoBehaviour
{
    RailRoute route;
    RailAmbienceSettings settings;
    RailBlock block;
    RailAmbienceProfile profile;
    RailAmbienceArea area;
    string run;
    readonly AudioHandle[] beds = new AudioHandle[2];
    readonly SoundEvent[] bedSounds = new SoundEvent[2];
    AudioHandle detail;
    SoundEvent lastDetail, lastScare;
    bool detailWasPlaying;
    Transform detailOrigin;
    float nextCheck, nextDetail, nextScare, nextBedRetry;

    public void Rebuild(RailRoute source)
    {
        StopAll();
        route = source;
        settings = Resources.Load<RailAmbienceSettings>("RailAmbienceSettings");
        run = source.LoadedRun;
        block = null;
        profile = null;
        area = null;
        lastDetail = lastScare = null;
        detailWasPlaying = false;
        nextCheck = nextBedRetry = 0;
        if (detailOrigin == null)
        {
            var point = new GameObject("Ambient detail position");
            point.transform.SetParent(transform, false);
            detailOrigin = point.transform;
        }
        if (settings != null)
        {
            nextDetail = After(settings.detailInterval);
            nextScare = After(settings.scareInterval);
        }
    }

    void Update()
    {
        if (Time.time < nextCheck) return;
        nextCheck = Time.time + 0.25f;
        var session = PhotonSession.Instance;
        bool active = settings != null && route != null && route.isActiveAndEnabled && route.Ready &&
            route.LoadedRun == run && PhotonSession.RunId == run && session != null && session.WorldReady &&
            !session.HasRoomRequest && !session.AwaitingCrewDestination && PhotonSession.Phase == RailPhase.Running;
        var manager = AudioManager.instance;
        var listener = manager != null ? manager.ListenerTransform : null;
        if (!active || listener == null)
        {
            StopAll();
            block = null;
            profile = null;
            area = null;
            if (settings != null)
            {
                nextDetail = After(settings.detailInterval);
                nextScare = After(settings.scareInterval);
            }
            return;
        }

        var nextBlock = BlockAt(listener.position);
        var nextProfile = nextBlock != null ? settings.Find(nextBlock.id) : null;
        var nextArea = nextProfile != null
            ? nextProfile.AreaAt(nextBlock.transform.InverseTransformPoint(listener.position), nextBlock == block ? area : null)
            : null;
        if (nextBlock != block || nextArea != area)
        {
            detail.Stop(0.5f);
            block = nextBlock;
            profile = nextProfile;
            area = nextArea;
            nextDetail = After(settings.detailInterval);
            nextBedRetry = 0f;
        }
        UpdateBeds(manager, listener);
        if (detail.IsPlaying)
        {
            detailWasPlaying = true;
            return;
        }
        if (detailWasPlaying)
        {
            detailWasPlaying = false;
            nextDetail = After(settings.detailInterval);
            nextScare = Mathf.Max(nextScare, nextDetail);
        }
        if (profile == null) return;

        if (Time.time >= nextScare)
        {
            nextScare = After(settings.scareInterval);
            if (profile.allowScares && PlayDetail(manager, listener, settings.rareScares, ref lastScare))
            {
                nextDetail = After(settings.detailInterval);
                return;
            }
        }
        if (Time.time >= nextDetail)
        {
            PlayDetail(manager, listener, area != null ? area.details : profile.outdoorDetails, ref lastDetail);
            nextDetail = After(settings.detailInterval);
        }
    }

    RailBlock BlockAt(Vector3 position)
    {
        // Actual assembled roots handle route shuffling and translated special blocks.
        if (Contains(route.startingStation, position)) return route.startingStation;
        foreach (var candidate in route.TravelBlocks)
            if (Contains(candidate, position)) return candidate;
        return Contains(route.finale, position) ? route.finale : null;
    }

    static bool Contains(RailBlock candidate, Vector3 position)
    {
        if (candidate == null || !candidate.isActiveAndEnabled) return false;
        var local = candidate.transform.InverseTransformPoint(position);
        return local.z >= 0f && local.z < candidate.length && Mathf.Abs(local.x) <= candidate.playerHalfWidth + 3f;
    }

    void UpdateBeds(AudioManager manager, Transform listener)
    {
        var desired = profile == null ? null : area != null ? area.beds : profile.outdoorBeds;
        for (int i = 0; i < beds.Length; i++)
        {
            var sound = desired != null && i < desired.Length ? desired[i] : null;
            if (bedSounds[i] != sound)
            {
                beds[i].Stop(settings.crossfadeSeconds);
                beds[i] = default;
                bedSounds[i] = sound;
            }
            if (sound != null && !beds[i].IsPlaying && Time.time >= nextBedRetry)
                beds[i] = manager.PlayAmbience(sound, listener, this, true, settings.crossfadeSeconds);
        }
        if (Time.time >= nextBedRetry) nextBedRetry = Time.time + 1f;
    }

    bool PlayDetail(AudioManager manager, Transform listener, SoundEvent[] choices, ref SoundEvent previous)
    {
        var sound = Choose(choices, previous);
        if (sound == null) return false;
        Vector2 offset = Random.insideUnitCircle.normalized * Random.Range(7f, 15f);
        var local = block.transform.InverseTransformPoint(listener.position);
        local += new Vector3(offset.x, Random.Range(0.5f, 2f), offset.y);
        if (area != null)
        {
            // Keep house creaks and drips in the room that made them eligible.
            local = area.bounds.ClosestPoint(local);
        }
        else
        {
            local.x = Mathf.Clamp(local.x, -block.playerHalfWidth, block.playerHalfWidth);
            local.z = Mathf.Clamp(local.z, 0.5f, block.length - 0.5f);
        }
        detailOrigin.position = block.transform.TransformPoint(local);
        detail = manager.PlayAmbienceDetail(sound, detailOrigin, this, settings.detailDuration, settings.detailFadeSeconds);
        if (!detail.IsPlaying) return false;
        previous = sound;
        return true;
    }

    public static SoundEvent Choose(SoundEvent[] choices, SoundEvent previous)
    {
        if (choices == null) return null;
        SoundEvent selected = null;
        int count = 0;
        foreach (var sound in choices)
            if (sound != null && sound != previous && Random.Range(0, ++count) == 0) selected = sound;
        if (selected != null) return selected;
        foreach (var sound in choices) if (sound != null) return sound;
        return null;
    }

    static float After(Vector2 interval) => Time.time + Random.Range(
        Mathf.Max(1f, Mathf.Min(interval.x, interval.y)), Mathf.Max(1f, Mathf.Max(interval.x, interval.y)));

    void StopAll()
    {
        for (int i = 0; i < beds.Length; i++)
        {
            beds[i].Stop(settings != null ? settings.crossfadeSeconds : 0f);
            beds[i] = default;
            bedSounds[i] = null;
        }
        detail.Stop(0.5f);
        detail = default;
        detailWasPlaying = false;
    }

    void OnDisable() => StopAll();
}
