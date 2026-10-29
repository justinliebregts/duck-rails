using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Keeps the town scene loaded beside Login and Main so its geometry and lightmaps exist once.
/// </summary>
public static class GameScenes
{
    public const string Login = "Login";
    public const string Environment = "Quackville";

    static bool _loadingEnvironment;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Init()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
        EnsureEnvironmentIfNeeded(SceneManager.GetActiveScene().name);
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        EnsureEnvironmentIfNeeded(scene.name);
    }

    public static IEnumerator EnsureEnvironment()
    {
        while (_loadingEnvironment)
            yield return null;

        if (!IsLoaded(Environment))
        {
            _loadingEnvironment = true;
            AsyncOperation load = SceneManager.LoadSceneAsync(Environment, LoadSceneMode.Additive);
            if (load == null)
            {
                _loadingEnvironment = false;
                yield break;
            }

            while (!load.isDone)
                yield return null;

            _loadingEnvironment = false;
        }

        ActivateEnvironment();
    }

    static void EnsureEnvironmentIfNeeded(string sceneName)
    {
        if (sceneName != Login && sceneName != PhotonSession.PlayScene && sceneName != Environment)
            return;
        if (IsLoaded(Environment))
        {
            ActivateEnvironment();
            return;
        }
        if (_loadingEnvironment)
            return;

        _loadingEnvironment = true;
        AsyncOperation load = SceneManager.LoadSceneAsync(Environment, LoadSceneMode.Additive);
        if (load == null)
        {
            _loadingEnvironment = false;
            return;
        }

        load.completed += _ =>
        {
            _loadingEnvironment = false;
            ActivateEnvironment();
        };
    }

    public static void ActivateEnvironment()
    {
        Scene environment = SceneManager.GetSceneByName(Environment);
        if (environment.IsValid() && environment.isLoaded)
            SceneManager.SetActiveScene(environment);
    }

    public static bool IsLoaded(string sceneName)
    {
        Scene scene = SceneManager.GetSceneByName(sceneName);
        return scene.IsValid() && scene.isLoaded;
    }
}
