#if UNITY_EDITOR
using System;
using Photon.Pun;
using Unity.Multiplayer.PlayMode;
using UnityEngine;

/// <summary>
/// Editor-only input source, constructed exclusively by a tagged local DesktopPlayer.
/// No MonoBehaviour callbacks, network state, scene searches, or Unity random-state changes.
/// Assign the case-sensitive LobbyTest tag before starting Multiplayer Play Mode.
/// </summary>
internal sealed class DesktopLobbySimulator
{
    internal Vector2 DirectionInterval = new Vector2(0.5f, 3f);
    internal Vector2 JumpInterval = new Vector2(1f, 4f);
    internal float MaxYawSpeed = 90f;
    internal float MaxPitchSpeed = 15f;

    readonly System.Random _random = new System.Random(Guid.NewGuid().GetHashCode());
    Vector2 _move, _lookSpeed, _targetLookSpeed;
    float _nextDirection, _nextJump;
    bool _running;
    object _room;
    PhotonPlayer _avatar;

    internal static bool IsTaggedInstance()
    {
        // Default Play Mode and the main Editor always remain manually controlled.
        if (CurrentPlayer.IsMainEditor) return false;

        foreach (var tag in CurrentPlayer.Tags)
            if (tag == "LobbyTest") return true;
        return false;
    }

    internal void Sample(MonoBehaviour desktop, out Vector2 move, out Vector2 lookDegrees, out bool jump)
    {
        move = lookDegrees = Vector2.zero;
        jump = false;
        var local = GR.Player.instance;
        if (!DesktopPlay.UseDesktop || local == null || local.gameObject != desktop.gameObject
            || !PhotonNetwork.InRoom || local.IsDead)
        {
            Reset();
            return;
        }

        if (!ReferenceEquals(_room, PhotonNetwork.CurrentRoom))
        {
            Reset();
            _room = PhotonNetwork.CurrentRoom;
        }
        if (_avatar == null) _avatar = PhotonPlayer.Local;
        if (_avatar == null || !_avatar.IsLocalHuman)
        {
            _running = false;
            _avatar = null;
            return;
        }

        float now = Time.time;
        if (!_running)
        {
            _running = true;
            _nextDirection = now;
            _nextJump = now + Range(JumpInterval);
            _lookSpeed = Vector2.zero;
        }
        if (now >= _nextDirection)
        {
            // Nine keyboard combinations, including idle. DesktopPlayer normalizes diagonals.
            _move = new Vector2(_random.Next(-1, 2), _random.Next(-1, 2));
            _targetLookSpeed = new Vector2(Signed(MaxYawSpeed), Signed(MaxPitchSpeed));
            _nextDirection = now + Range(DirectionInterval);
        }
        if (now >= _nextJump)
        {
            jump = true; // One frame only; the real controller still requires grounded contact.
            _nextJump = now + Range(JumpInterval);
        }
        _lookSpeed = Vector2.MoveTowards(_lookSpeed, _targetLookSpeed, 180f * Time.deltaTime);
        move = _move;
        lookDegrees = _lookSpeed * Time.deltaTime;
    }

    void Reset()
    {
        _running = false;
        _room = null;
        _avatar = null;
    }

    float Range(Vector2 interval) => Mathf.Lerp(interval.x, interval.y, (float)_random.NextDouble());
    float Signed(float magnitude) => ((float)_random.NextDouble() * 2f - 1f) * magnitude;
}
#endif
