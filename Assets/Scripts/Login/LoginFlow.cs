using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

public class LoginFlow : MonoBehaviour
{
    [SerializeField] GameConfig config;
    [SerializeField] TMP_Text statusText;
    [SerializeField] float retryDelaySeconds = 2f;
    [SerializeField] bool previewSpots;
    [SerializeField] KeyCode previewSpotKey = KeyCode.N;
    string _displayedStatus;
    string _requestedStatus;

    public bool PreviewSpots
    {
        get
        {
#if UNITY_EDITOR
            return previewSpots;
#else
            return false;
#endif
        }
    }

    public KeyCode PreviewSpotKey => previewSpotKey;

    public static bool Previewing
    {
        get
        {
#if UNITY_EDITOR
            LoginFlow[] flows = FindObjectsByType<LoginFlow>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < flows.Length; i++)
            {
                if (flows[i].previewSpots)
                    return true;
            }
#endif
            return false;
        }
    }

    void Start()
    {
        if (PreviewSpots)
        {
            StartCoroutine(HoldAtLogin());
            return;
        }

        StartCoroutine(Run());
    }

    IEnumerator HoldAtLogin()
    {
        DuckzLog.Log("Login spot preview: staying on the login scene.");
        Scene play = SceneManager.GetSceneByName(PhotonSession.PlayScene);
        if (!play.isLoaded)
            yield break;

        AsyncOperation unload = SceneManager.UnloadSceneAsync(play);
        while (unload != null && !unload.isDone)
            yield return null;
    }

    IEnumerator Run()
    {
        PhotonSession session = PhotonSession.Ensure();
        SetStatus(session.RecoveringConnection ? "Reconnecting..." : "Signing in...");
        while (session.ReturningToLogin)
            yield return null;

        if (session.RecoveringConnection)
        {
            DuckzLog.Log("Connection lost. Reconnecting...");
            SetStatus("Reconnecting...");
            yield return new WaitForSecondsRealtime(retryDelaySeconds);
            if (PlayerSession.Instance != null)
            {
                yield return session.ConnectAndJoin(SetStatus);
                yield break;
            }
        }

        AccountSignIn.Outcome outcome = null;
        yield return AccountSignIn.Run(config, SetStatus, retryDelaySeconds, value => outcome = value);

        if (outcome != null && outcome.Quit)
            Quit();
        else if (outcome != null && outcome.SignedIn && !PreviewSpots)
            yield return session.ConnectAndJoin(SetStatus);
    }

    void SetStatus(string message)
    {
        _requestedStatus = message;
        RefreshStatus();
    }

    void Update()
    {
        if (_requestedStatus != null)
            RefreshStatus();
    }

    void RefreshStatus()
    {
        // Reachability detects a missing network, not whether a service is online.
        // Only change presentation; requests and retries keep their existing timing.
        string message = Application.internetReachability == NetworkReachability.NotReachable
            ? "No network connection. Internet connection required."
            : _requestedStatus;
        if (_displayedStatus == message)
            return;
        _displayedStatus = message;
        DuckzLog.Log(message);
        if (statusText != null)
        {
            statusText.textWrappingMode = TextWrappingModes.NoWrap;
            statusText.text = message;
        }

        LoginSpot[] spots = FindObjectsByType<LoginSpot>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < spots.Length; i++)
            spots[i].SetStatus(message);
    }

    static void Quit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
