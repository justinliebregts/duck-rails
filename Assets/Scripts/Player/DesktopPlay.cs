using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Management;

/// <summary>
/// In the editor, swaps the headset rig for <see cref="DesktopPlayer"/> before VR scripts wake up.
/// Quest builds leave the VR rig alone.
/// </summary>
[DefaultExecutionOrder(-32000)]
public class DesktopPlay : MonoBehaviour
{
    public const string PrefKey = "Duck.DesktopPlay";

    [SerializeField] GameObject desktopPlayerPrefab;

    public static bool UseDesktop
    {
        get
        {
#if UNITY_EDITOR
            return UnityEditor.EditorPrefs.GetBool(PrefKey, true);
#elif UNITY_STANDALONE
            return true;
#else
            return false;
#endif
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void StopEditorXr()
    {
#if UNITY_EDITOR || UNITY_STANDALONE
        if (!UseDesktop)
            return;

        var settings = XRGeneralSettings.Instance;
        if (settings == null)
            return;

        settings.InitManagerOnStart = false;
        if (settings.Manager != null && settings.Manager.isInitializationComplete)
            settings.Manager.DeinitializeLoader();
#endif
    }

    void Start()
    {
        if (!UseDesktop) {
            return;
        }

        var origin = Vector3.zero;
        var rotation = Quaternion.identity;
        var duck = GameObject.Find("DuckPlayer");
        if (duck != null)
        {
            origin = duck.transform.position;
            rotation = duck.transform.rotation;
        }

        DisableVrObjects();

        if (desktopPlayerPrefab == null || FindAnyObjectByType<DesktopPlayer>() != null)
            return;

        var desktopPlayer = Instantiate(desktopPlayerPrefab, origin, rotation);
        // Keep the rig and camera in the scene that owns their lifetime.
        SceneManager.MoveGameObjectToScene(desktopPlayer, gameObject.scene);
    }

    static void DisableVrObjects()
    {
        var duck = GameObject.Find("DuckPlayer");
        if (duck != null)
            duck.SetActive(false);

        var behaviours = FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < behaviours.Length; i++)
        {
            var behaviour = behaviours[i];
            if (behaviour == null)
                continue;

            var typeName = behaviour.GetType().Name;
            if (typeName == "OVRManager" || typeName == "OVRCameraRig")
                behaviour.gameObject.SetActive(false);
        }
    }
}
