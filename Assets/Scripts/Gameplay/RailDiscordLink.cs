using UnityEngine;

/// <summary>Local-only compound button that opens the community Discord invite in the system browser.</summary>
public sealed class RailDiscordLink : MonoBehaviour
{
    public const string DefaultInvite = "https://discord.gg/sfyvhahnDW";
    public string inviteUrl = DefaultInvite;
    float nextRequest;

    public void OpenInvite()
    {
        if (!Application.isPlaying || Time.unscaledTime < nextRequest || RailWorld.InputBlocked) return;
        nextRequest = Time.unscaledTime + 3f;
        if (string.IsNullOrEmpty(inviteUrl)) return;
        Application.OpenURL(inviteUrl);
    }
}
