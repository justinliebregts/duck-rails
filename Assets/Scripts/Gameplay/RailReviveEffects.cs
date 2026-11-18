using System.Collections.Generic;
using UnityEngine;

/// <summary>Local presentation of committed rescue state, on every client.</summary>
public sealed class RailReviveEffects
{
    sealed class Presentation
    {
        public int sequence;
        public GameObject root;
        public AudioSource progress;
        public Game.Audio.AudioHandle complete;
        public ParticleSystem particles;
        public RailReviveFollow follow;
        public double pendingAt;
    }

    readonly Dictionary<string, Presentation> players = new Dictionary<string, Presentation>();
    readonly AudioClip progressClip = Resources.Load<AudioClip>("RailRevive/RevivingPlayerSFX");
    readonly Game.Audio.SoundEvent completeSound = Resources.Load<Game.Audio.SoundEvent>("RailRevive/ReviveSFX");
    readonly GameObject particlePrefab = Resources.Load<GameObject>("RailRevive/Revived");
    string run;

    public static float Progress(RailParticipant player, double now) => player.down && player.reviveAt > 0
        ? Mathf.Clamp01((float)(((player.reviveLostAt > 0 ? player.reviveLostAt : now) - player.reviveAt) / RailRunState.ReviveSeconds)) : 0;

    public static bool IsNewCompletion(int previous, RailParticipant player, double now) =>
        player.reviveSequence > previous && !player.down && player.revivedAt > 0 &&
        now >= player.revivedAt && now - player.revivedAt < 3;

    public void Present(RailRunState state, Transform parent, double now)
    {
        if (run != state.run) { Reset(); run = state.run; }
        foreach (var player in state.players)
        {
            if (!players.TryGetValue(player.id, out var fx))
            {
                // Baseline a late join/reconnect: never replay historical completion effects.
                fx = new Presentation { sequence = player.reviveSequence };
                players.Add(player.id, fx);
            }
            if (IsNewCompletion(fx.sequence, player, now)) fx.pendingAt = player.revivedAt;
            fx.sequence = player.reviveSequence;
            bool connected = RailExpedition.IsConnected(player.actor);
            bool positioned = RailExpedition.Position(player.actor, out var head);
            bool progressing = connected && positioned && player.down && player.reviveAt > 0 && player.reviveLostAt <= 0;
            bool completing = connected && positioned && !player.down && fx.pendingAt > 0 && now - fx.pendingAt < 3;
            if (fx.pendingAt > 0 && (player.down || now - fx.pendingAt >= 3)) fx.pendingAt = 0;
            if (fx.root == null && (progressing || completing)) Create(fx, parent);
            if (fx.root == null) continue;
            fx.follow.Actor = player.actor;
            if (positioned) fx.root.transform.position = head - Vector3.up * .5f;
            if (progressing)
            {
                // Raider Legends' authored pitch ramp: 0.25 -> 1.25.
                fx.progress.pitch = .25f + Progress(player, now);
                if (!fx.progress.isPlaying && progressClip != null) fx.progress.Play();
            }
            else fx.progress.Stop();
            if (completing)
            {
                fx.pendingAt = 0;
                if (completeSound != null && Game.Audio.AudioManager.instance != null)
                    fx.complete = Game.Audio.AudioManager.instance.PlayAttached(completeSound, fx.root.transform);
                if (fx.particles != null) { fx.particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear); fx.particles.Play(true); }
            }
            if (!connected)
            {
                fx.complete.Stop();
                if (fx.particles != null) fx.particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
        }
    }

    void Create(Presentation fx, Transform parent)
    {
        fx.root = new GameObject("Revive feedback");
        fx.root.transform.SetParent(parent, false);
        fx.follow = fx.root.AddComponent<RailReviveFollow>();
        fx.progress = Source(fx.root, progressClip, true, 40);
        if (particlePrefab != null)
        {
            var particles = Object.Instantiate(particlePrefab, fx.root.transform, false);
            fx.particles = particles.GetComponent<ParticleSystem>();
            // Keep every layer attached to the revived duck, including aboard the moving train.
            foreach (var ps in particles.GetComponentsInChildren<ParticleSystem>(true))
            {
                var main = ps.main; main.simulationSpace = ParticleSystemSimulationSpace.Local;
            }
        }
    }

    static AudioSource Source(GameObject root, AudioClip clip, bool loop, float range)
    {
        var source = root.AddComponent<AudioSource>();
        source.playOnAwake = false; source.clip = clip; source.loop = loop;
        source.spatialBlend = 1; source.dopplerLevel = 0;
        source.minDistance = 10; source.maxDistance = range; source.rolloffMode = AudioRolloffMode.Linear;
        var settings = Resources.Load<Game.Audio.AudioSettings>("GameAudioSettings");
        if (settings != null) source.outputAudioMixerGroup = settings.effects;
        return source;
    }

    public void StopProgress()
    {
        foreach (var fx in players.Values) if (fx.progress != null) fx.progress.Stop();
    }

    public void Reset()
    {
        foreach (var fx in players.Values) if (fx.root != null)
        {
            fx.root.SetActive(false);
            Object.Destroy(fx.root);
        }
        players.Clear(); run = null;
    }
}
