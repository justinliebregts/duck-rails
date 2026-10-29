#if UNITY_EDITOR
using System.Collections;
using UnityEngine;

static class EditorPlaySignIn
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Start()
    {
        if (PlayerSession.Instance != null)
            return;

        if (LoginFlow.Previewing || Object.FindAnyObjectByType<LoginFlow>(FindObjectsInactive.Include) != null)
            return;

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
            AccountSignIn.Outcome outcome = null;
            yield return AccountSignIn.Run(null, Debug.Log, 2f, value => outcome = value);

            if (outcome != null && outcome.Quit)
            {
                PhotonSession.ClearConnectPending();
                UnityEditor.EditorApplication.isPlaying = false;
            }
            else if (outcome != null && outcome.SignedIn && !LoginFlow.Previewing)
                yield return PhotonSession.Ensure().ConnectAndJoin(Debug.Log);
            else
                PhotonSession.ClearConnectPending();

            Destroy(gameObject);
        }
    }
}
#endif
