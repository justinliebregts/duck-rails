using UnityEngine;

/// <summary>Compatibility for older fixtures. The standalone test panel has been retired.</summary>
public sealed class RailDiagnosticPanel : MonoBehaviour
{
    public static RailDiagnosticPanel Instance { get; private set; }
    public bool IsOpen => false;
    public static bool KeyboardMode => (SimulatedRigInput.Instance != null && SimulatedRigInput.Instance.IsSimulating) ||
        (GR.Player.instance != null && GR.Player.instance.GetComponent<DesktopPlayer>() != null);
    public static bool BlocksDesktopInput => false;
    void Awake() { Instance = this; }
    void OnDestroy() { if (Instance == this) Instance = null; }
    public void Close() { }
    public void Toggle() { Object.FindFirstObjectByType<PauseMenu>()?.OpenFromDiagnostics(); }
    public void Notify(string text) { DuckzLog.Log(text, this); }
}
