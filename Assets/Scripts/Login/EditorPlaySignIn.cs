#if UNITY_EDITOR
using System.Collections;
using UnityEngine;

static class EditorPlaySignIn
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Start()
    {
        if (LoginFlow.Previewing || Object.FindAnyObjectByType<LoginFlow>(FindObjectsInactive.Include) != null)
            return;

        // Start the session before authentication so background execution and the
        // Compound status/input gate are available throughout sign-in.
        PhotonSession.Ensure().SetEditorSignInStatus("Signing in...");
        PhotonSession.MarkConnectPending();

        var runner = new GameObject(nameof(EditorPlaySignIn));
        runner.hideFlags = HideFlags.HideInHierarchy;
        Object.DontDestroyOnLoad(runner);
        runner.AddComponent<Runner>();
    }

    sealed class Runner : MonoBehaviour
    {
        void Start()
        {
            StartCoroutine(Run());
        }

        IEnumerator Run()
        {
            double started = Time.realtimeSinceStartupAsDouble;
            var session = PhotonSession.Ensure();
            AccountSignIn.Outcome outcome = new AccountSignIn.Outcome { SignedIn = PlayerSession.Instance != null };
            if (!outcome.SignedIn)
                yield return AccountSignIn.Run(null, session.SetEditorSignInStatus, 2f, value => outcome = value);

            double signedIn = Time.realtimeSinceStartupAsDouble;

            if (outcome != null && outcome.Quit)
            {
                PhotonSession.ClearConnectPending();
                UnityEditor.EditorApplication.isPlaying = false;
            }
            else if (outcome != null && outcome.SignedIn && !LoginFlow.Previewing)
            {
                yield return session.ConnectAndJoin(Debug.Log);
                Debug.Log($"[EditorPlaySignIn] Ready in {Time.realtimeSinceStartupAsDouble - started:F2}s " +
                    $"(sign-in {signedIn - started:F2}s, connection/world {Time.realtimeSinceStartupAsDouble - signedIn:F2}s).");
            }
            else
                PhotonSession.ClearConnectPending();

            Destroy(gameObject);
        }
    }
}
#endif
