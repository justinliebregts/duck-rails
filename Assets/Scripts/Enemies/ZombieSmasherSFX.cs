using System.Collections;
using System.Collections.Generic;
using Game.AI;
using Photon.Pun;
using UnityEngine;
using Game.Audio;

/// <summary>Inspector-assigned vocals. Call PlayWhimper from the damage handler when a hit is accepted.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Enemy))]
public sealed class ZombieSmasherSFX : MonoBehaviour, IEnemyHitVocal
{
    [Header("Sound events")]
    [SerializeField] SoundEvent idleSound;
    [SerializeField] SoundEvent aggroSound;
    [SerializeField] SoundEvent attackSound;
    [SerializeField] SoundEvent killSound;
    [SerializeField] SoundEvent whimperSound;
    [SerializeField] SoundEvent landingSound;
    [SerializeField] SoundEvent jumpSound;

    [Header("Timing")]
    [Tooltip("Random silence between idle vocals while wandering or searching.")]
    [SerializeField] Vector2 idleInterval = new Vector2(4f, 8f);
    [Tooltip("Delay from the attack starting to the kill sound starting. Clips can overlap; zero plays both immediately. Death is still immediate.")]
    [SerializeField, Min(0f)] float killSoundDelay = 0.1f;
    [SerializeField, Min(0f)] float whimperCooldown = 0.3f;
    [SerializeField, Min(0f)] float landingCooldown = 0.5f;

    Enemy enemy;
    PhotonView view;
    KillTrigger[] killTriggers;
    readonly List<AudioHandle> voices = new List<AudioHandle>(4);
    Enemy.State previousState;
    float nextIdleTime, nextWhimperTime;
    float nextLandingBroadcast, nextLandingPlayback;
    bool playingKillSequence;
    AudioHandle landingVoice;
    AudioHandle jumpVoice;

    void Awake()
    {
        enemy = GetComponent<Enemy>();
        view = GetComponent<PhotonView>();
        killTriggers = GetComponentsInChildren<KillTrigger>(true);
    }

    void OnEnable()
    {
        previousState = Enemy.State.Wander;
        nextWhimperTime = 0f;
        nextLandingBroadcast = nextLandingPlayback = 0f;
        ScheduleIdle();
        foreach (var trigger in killTriggers) trigger.PlayerKilled += PlayAttackAndKill;
    }

    void OnDisable()
    {
        foreach (var trigger in killTriggers)
            if (trigger != null) trigger.PlayerKilled -= PlayAttackAndKill;
        StopAllCoroutines();
        playingKillSequence = false;
        StopVoices();
    }

    void LateUpdate()
    {
        if (!enemy.isActiveAndEnabled) return;
        var state = enemy.CurrentState;
        if (state != previousState)
        {
            previousState = state;
            if (state == Enemy.State.Chase && !playingKillSequence && !jumpVoice.IsPlaying)
            {
                StopVoices();
                Play(aggroSound);
            }
            ScheduleIdle();
        }
        if (state != Enemy.State.Chase && !playingKillSequence && !IsPlaying && Time.time >= nextIdleTime)
        {
            Play(idleSound);
            ScheduleIdle();
        }
    }

    void ScheduleIdle()
    {
        float min = Mathf.Max(0.1f, Mathf.Min(idleInterval.x, idleInterval.y));
        float max = Mathf.Max(min, Mathf.Max(idleInterval.x, idleInterval.y));
        nextIdleTime = Time.time + Random.Range(min, max);
    }

    // Track every lease, including whimpers overlapping the attack/kill sequence.
    bool IsPlaying
    {
        get
        {
            for (int i = voices.Count - 1; i >= 0; i--)
                if (!voices[i].IsPlaying) voices.RemoveAt(i);
            return voices.Count > 0;
        }
    }

    void StopVoices()
    {
        foreach (var handle in voices) handle.Stop();
        voices.Clear();
    }

    AudioHandle Play(SoundEvent sound)
    {
        if (sound == null) return default;
        var manager = AudioManager.instance;
        if (manager == null) return default;
        // Prune completed leases even during a long chase with repeated reactions.
        _ = IsPlaying;
        var handle = manager.PlayAttached(sound, transform, this);
        if (handle.IsPlaying) voices.Add(handle);
        return handle;
    }

    void PlayAttackAndKill() => BroadcastReaction(false);

    /// <summary>Called once at the start of a planned jump, never for walking off an edge.</summary>
    public void PlayJump()
    {
        if (enemy == null) enemy = GetComponent<Enemy>();
        if (!isActiveAndEnabled || !enemy.HasAuthority) return;
        ReceiveSmasherJump();
        if (PhotonNetwork.InRoom && view != null && view.ViewID != 0)
            view.RPC(nameof(ReceiveSmasherJump), RpcTarget.Others);
    }

    [PunRPC]
    void ReceiveSmasherJump()
    {
        if (!isActiveAndEnabled || enemy.IsDormant || jumpSound == null) return;
        jumpVoice.Stop();
        jumpVoice = Play(jumpSound);
        ScheduleIdle();
    }

    /// <summary>Called by the authority once a real jump/fall has landed.</summary>
    public void PlayLanding()
    {
        if (enemy == null) enemy = GetComponent<Enemy>();
        if (!isActiveAndEnabled || !enemy.HasAuthority) return;
        if (Time.time < nextLandingBroadcast) return;
        nextLandingBroadcast = Time.time + Mathf.Max(0.1f, landingCooldown);
        // Local playback must not depend on Photon RPC dispatch/cached behaviours.
        ReceiveSmasherLanding();
        if (PhotonNetwork.InRoom && view != null && view.ViewID != 0)
            view.RPC(nameof(ReceiveSmasherLanding), RpcTarget.Others);
    }

    [PunRPC]
    void ReceiveSmasherLanding()
    {
        if (!isActiveAndEnabled || enemy.IsDormant || landingSound == null) return;
        if (Time.time < nextLandingPlayback) return;
        nextLandingPlayback = Time.time + Mathf.Max(0.1f, landingCooldown);
        landingVoice.Stop();
        landingVoice = Play(landingSound);
        ScheduleIdle();
    }

    /// <summary>Call once where player damage is accepted, not on every client's replicated hit.</summary>
    public void PlayWhimper() => BroadcastReaction(true);

    void BroadcastReaction(bool whimper)
    {
        if (!isActiveAndEnabled) return;
        // Contact is detected by the victim's local KillTrigger, which need not own this enemy.
        // State vocals already follow replicated Enemy.CurrentState; only reactions need an RPC.
        if (PhotonNetwork.InRoom && view != null && view.ViewID != 0)
            view.RPC(nameof(ReceiveSmasherReaction), RpcTarget.All, whimper);
        else
            ReceiveSmasherReaction(whimper);
    }

    [PunRPC]
    void ReceiveSmasherReaction(bool whimper)
    {
        if (!isActiveAndEnabled) return;
        if (whimper)
        {
            if (Time.time < nextWhimperTime) return;
            nextWhimperTime = Time.time + Mathf.Max(0f, whimperCooldown);
            if (!playingKillSequence) StopVoices();
            Play(whimperSound);
            ScheduleIdle();
        }
        else
        {
            // Multiple players/colliders touching together must not stack identical vocals.
            if (!playingKillSequence) StartCoroutine(AttackAndKill());
        }
    }

    IEnumerator AttackAndKill()
    {
        playingKillSequence = true;
        StopVoices();
        Play(attackSound);
        float delay = Mathf.Max(0f, killSoundDelay);
        if (delay > 0f) yield return new WaitForSeconds(delay);
        Play(killSound);
        // Keep state vocals from interrupting either overlapping one-shot.
        while (IsPlaying) yield return null;
        playingKillSequence = false;
        ScheduleIdle();
    }
}
