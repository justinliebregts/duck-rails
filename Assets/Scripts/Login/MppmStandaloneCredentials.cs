using System;
using System.Collections.Generic;
using Oculus.Platform;
using UnityEngine;

/// <summary>
/// Meta test users for additional Multiplayer Play Mode editors.
/// The scenario tag on an instance selects which saved user that process signs in as.
/// </summary>
public static class MppmStandaloneCredentials
{
#if UNITY_EDITOR
    [Serializable]
    public class User
    {
        public string tag;
        public string email;
        public string token;
    }

    [Serializable]
    class Store
    {
        public User[] users = Array.Empty<User>();
    }

    public static bool TryApplyForCurrentPlayer(out string error)
    {
        error = null;
        if (Unity.Multiplayer.PlayMode.CurrentPlayer.IsMainEditor)
            return true;

        IReadOnlyList<string> tags = Unity.Multiplayer.PlayMode.CurrentPlayer.Tags;
        if (!TrySelectUser(tags, Users(), out User user, out error)) return false;

        StandalonePlatformSettings.OculusPlatformTestUserAccessToken = user.token;
        StandalonePlatformSettings.OculusPlatformTestUserEmail = user.email ?? "";
        return true;
    }

    public static bool TrySelectUser(IReadOnlyList<string> tags, User[] users, out User selected, out string error)
    {
        selected = null;
        error = "No saved Meta user matches this editor's tags. Open Duck Rails > Multiplayer Play Mode Users in the main editor, save Player2, and assign that tag in the scenario.";
        if (tags == null || users == null) return false;
        for (int i = 0; i < tags.Count; i++)
        {
            if (Normalize(tags[i]).Length == 0) continue;
            foreach (User candidate in users)
            {
                if (candidate == null || string.IsNullOrEmpty(candidate.token) ||
                    !string.Equals(Normalize(candidate.tag), Normalize(tags[i]), StringComparison.Ordinal)) continue;
                if (selected != null && selected != candidate)
                {
                    selected = null;
                    error = "More than one saved Meta user matches this editor's tags. Assign exactly one account tag, then restart the editor.";
                    return false;
                }
                selected = candidate;
            }
        }
        if (selected == null) return false;
        error = null;
        return true;
    }

    public static User[] Users()
    {
        return Load().users ?? Array.Empty<User>();
    }

    public static bool TryGet(string tag, out User user)
    {
        user = null;
        tag = Normalize(tag);
        if (tag.Length == 0)
            return false;

        User[] users = Users();
        for (int i = 0; i < users.Length; i++)
        {
            if (users[i] != null && string.Equals(Normalize(users[i].tag), tag, StringComparison.Ordinal) && !string.IsNullOrEmpty(users[i].token))
            {
                user = users[i];
                return true;
            }
        }

        return false;
    }

    public static void Save(string tag, string email, string token)
    {
        tag = Normalize(tag);
        List<User> users = new List<User>(Users());
        users.RemoveAll(user => user == null || string.Equals(Normalize(user.tag), tag, StringComparison.Ordinal));
        users.Add(new User { tag = tag, email = email ?? "", token = token ?? "" });
        Write(users);
    }

    public static void Remove(string tag)
    {
        tag = Normalize(tag);
        List<User> users = new List<User>(Users());
        users.RemoveAll(user => user == null || string.Equals(Normalize(user.tag), tag, StringComparison.Ordinal));
        Write(users);
    }

    public static string Normalize(string tag)
    {
        return (tag ?? "").Trim();
    }

    static Store Load()
    {
        string json = UnityEditor.EditorPrefs.GetString(PrefsKey(), "");
        if (string.IsNullOrEmpty(json))
            return new Store();

        Store store = JsonUtility.FromJson<Store>(json);
        if (store == null || store.users == null)
            return new Store();
        return store;
    }

    static void Write(List<User> users)
    {
        Store store = new Store { users = users.ToArray() };
        UnityEditor.EditorPrefs.SetString(PrefsKey(), JsonUtility.ToJson(store));
    }

    // EditorPrefs is per Windows user, not per editor process. Key off the real project
    // folder so a virtual player under Library/VP reads the same saved users.
    static string PrefsKey()
    {
        return PrefsKeyForAssets(UnityEngine.Application.dataPath);
    }

    public static string PrefsKeyForAssets(string assetsPath)
    {
        string assets = assetsPath.Replace('\\', '/');
        const string marker = "/Library/VP/";
        int index = assets.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        string root = index >= 0
            ? assets.Substring(0, index)
            : System.IO.Path.GetDirectoryName(assets);
        return "DuckRails.MppmUsers." + System.IO.Path.GetFileName(root);
    }
#endif
}
