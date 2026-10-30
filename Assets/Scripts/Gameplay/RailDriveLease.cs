using UnityEngine;

/// <summary>Master-owned, expiring single-hand grant. Updates cannot acquire an unowned lever.</summary>
public sealed class RailDriveLease
{
    public int Actor { get; private set; }
    public string Token { get; private set; } = "";
    public float Progress { get; private set; }
    public double Expires { get; private set; }
    public void Reset() { Actor = 0; Token = ""; Progress = 0; Expires = 0; }
    // 0 = new grip, 1 = held/heartbeat, 2 = release. Tokens distinguish successive grips.
    public bool Apply(int actor, string token, int operation, float progress, double now)
    {
        if (actor <= 0 || string.IsNullOrEmpty(token) || float.IsNaN(progress) || float.IsInfinity(progress)) return false;
        if (operation == 0)
        {
            if (Actor != 0) return false;
            Actor = actor; Token = token; Progress = 0; Expires = now + 1.5;
            return true;
        }
        if (Actor != actor || Token != token) return false;
        if (operation == 2) { Reset(); return true; }
        if (operation != 1) return false;
        Progress = Mathf.Clamp01(progress); Expires = now + 1.5;
        return true;
    }
    public static float Travel(float forwardMetres) => Mathf.Clamp01(forwardMetres / .28f);
    // Separate detents prevent small hand tremors from toggling the train.
    public static bool ForwardDetent(bool forward, float travel) => forward ? travel > .2f : travel >= .8f;
}
