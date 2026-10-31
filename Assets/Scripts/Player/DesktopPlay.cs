using UnityEngine;

/// <summary>
/// Selects the input source for DuckPlayer. Both modes keep the same rig and interactions.
/// </summary>
[DefaultExecutionOrder(-32000)]
public class DesktopPlay : MonoBehaviour
{
    public const string PrefKey = "Duck.DesktopPlay";

    static bool? sessionSimulation;

    public static void SetSessionSimulation(bool? simulated) => sessionSimulation = simulated;

    public static bool UseDesktop
    {
        get
        {
#if UNITY_EDITOR
            return sessionSimulation ?? UnityEditor.EditorPrefs.GetBool(PrefKey, true);
#elif UNITY_STANDALONE
            return sessionSimulation ?? true;
#else
            return false;
#endif
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetSession() => sessionSimulation = null;

    void Awake()
    {
#if UNITY_EDITOR || UNITY_STANDALONE
        // Login has no player. Each gameplay scene already authors the same DuckPlayer.
        foreach (var rig in FindObjectsByType<NewPlayer>(FindObjectsSortMode.None))
        {
            if (rig.gameObject.scene != gameObject.scene || rig.GetComponent<SimulatedRigInput>() != null) continue;
            rig.gameObject.AddComponent<SimulatedRigInput>();
        }
#endif
    }
}
