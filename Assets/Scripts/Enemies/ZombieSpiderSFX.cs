using System.Collections;
using System.Collections.Generic;
using Game.AI;
using Photon.Pun;
using UnityEngine;
using Game.Audio;

/// <summary>Inspector-assigned vocals. Call PlayWhimper from the damage handler when a hit is accepted.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Enemy))]
public sealed class ZombieSpiderSFX : MonoBehaviour, IEnemyHitVocal
{
    [Header("Sound events")]
    [SerializeField] SoundEvent idleSound;
    [SerializeField] SoundEvent aggroSound;
    [SerializeField] SoundEvent attackSound;
    [SerializeField] SoundEvent killSound;
    [SerializeField] SoundEvent whimperSound;

    [Header("Timing")]
    [Tooltip("Random silence between idle vocals while wandering or searching.")]
    [SerializeField] Vector2 idleInterval = new Vector2(4f, 8f);
    [Tooltip("Delay from the attack starting to the kill sound starting. Clips can overlap; zero plays both immediately. Death is still immediate.")]
    [SerializeField, Min(0f)] float killSoundDelay = 0.1f;
    [SerializeField, Min(0f)] float whimperCooldown = 0.3f;

    Enemy enemy;
    PhotonView view;
    KillTrigger[] killTriggers;
    readonly List<AudioHandle> voices = new List<AudioHandle>(4);
    Enemy.State previousState;
    float nextIdleTime, nextWhimperTime;
    bool playingKillSequence;

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
            if (state == Enemy.State.Chase && !playingKillSequence)
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

    void Play(SoundEvent sound)
    {
        if (sound == null) return;
        var manager = AudioManager.instance;
        if (manager == null) return;
        // Prune completed leases even during a long chase with repeated reactions.
        _ = IsPlaying;
        var handle = manager.PlayAttached(sound, transform, this);
        if (handle.IsPlaying) voices.Add(handle);
    }

    void PlayAttackAndKill() => BroadcastReaction(false);

    /// <summary>Call once where player damage is accepted, not on every client's replicated hit.</summary>
    public void PlayWhimper() => BroadcastReaction(true);

    void BroadcastReaction(bool whimper)
    {
        if (!isActiveAndEnabled) return;
        // Contact is detected by the victim's local KillTrigger, which need not own this enemy.
        // State vocals already follow replicated Enemy.CurrentState; only reactions need an RPC.
        if (PhotonNetwork.InRoom && view != null && view.ViewID != 0)
            view.RPC(nameof(ReceiveSpiderReaction), RpcTarget.All, whimper);
        else
            ReceiveSpiderReaction(whimper);
    }

    [PunRPC]
    void ReceiveSpiderReaction(bool whimper)
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
