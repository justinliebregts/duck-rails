using UnityEngine.SceneManagement;

/// <summary>Self-contained scenes: Login never preloads gameplay geometry.</summary>
public static class GameScenes
{
    public const string Login = "Login";
    public const string Compound = "Compound";
    public static bool IsLoaded(string name)
    {
        var scene = SceneManager.GetSceneByName(name);
        return scene.IsValid() && scene.isLoaded;
    }
}
