using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Oculus.Platform;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;

public class MppmStandaloneUsersWindow : EditorWindow
{
    const string MenuPath = "Duck Rails/Multiplayer Play Mode Users";
    const string MetaGraph = "https://meta.graph.meta.com";
    const string OculusGraph = "https://graph.oculus.com";

    string tag = "Player2";
    string email = "";
    string password = "";
    string status = "";
    Vector2 scroll;

    [MenuItem(MenuPath)]
    public static void Open()
    {
        GetWindow<MppmStandaloneUsersWindow>("Play Mode Users");
    }

    void OnGUI()
    {
        scroll = EditorGUILayout.BeginScrollView(scroll);
        EditorGUILayout.HelpBox(
            "The main editor keeps the account from Meta > Platform > Edit Settings. An additional editor uses a saved user only when its scenario Tag matches that user.\n\n" +
            "The Two Ducks scenario already assigns Player2 to its second editor. Restart additional editors after changing accounts. Each client needs a different account allowed by the Duck Rails backend.",
            MessageType.Info);

        tag = EditorGUILayout.TextField("Tag", tag);
        email = EditorGUILayout.TextField("Test user email", email);
        password = EditorGUILayout.PasswordField("Test user password", password);

        using (new EditorGUI.DisabledScope(string.IsNullOrWhiteSpace(tag) || string.IsNullOrWhiteSpace(email) || string.IsNullOrEmpty(password)))
        {
            if (GUILayout.Button("Sign in and save"))
                SignInAndSave();
        }

        if (!string.IsNullOrEmpty(status))
            EditorGUILayout.HelpBox(status, MessageType.None);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Saved users", EditorStyles.boldLabel);
        MppmStandaloneCredentials.User[] users = MppmStandaloneCredentials.Users();
        if (users.Length == 0)
            EditorGUILayout.LabelField("None yet.");

        for (int i = 0; i < users.Length; i++)
        {
            MppmStandaloneCredentials.User user = users[i];
            if (user == null)
                continue;

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(user.tag, GUILayout.Width(140));
            EditorGUILayout.LabelField(string.IsNullOrEmpty(user.email) ? "(no email)" : user.email);
            if (GUILayout.Button("Remove", GUILayout.Width(70)))
            {
                MppmStandaloneCredentials.Remove(user.tag);
                status = "Removed " + user.tag + ".";
            }
            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.EndScrollView();
    }

    void SignInAndSave()
    {
        string savedTag = MppmStandaloneCredentials.Normalize(tag);
        string savedEmail = email.Trim();
        if (!IsTagName(savedTag))
        {
            status = "Use a tag like Player2: letters, numbers, _ or -.";
            return;
        }

        status = "Signing in...";
        Repaint();

        try
        {
            string token = RequestAccessToken(savedEmail, password);
            if (string.IsNullOrEmpty(token))
            {
                status = "Meta did not return an access token.";
                return;
            }

            MppmStandaloneCredentials.Save(savedTag, savedEmail, token);
            EnsureProjectTag(savedTag);
            password = "";
            status = "Saved " + savedEmail + " for tag " + savedTag + ". Assign that tag to the additional editor.";
        }
        catch (Exception exception)
        {
            status = exception.Message;
        }
    }

    static string RequestAccessToken(string userEmail, string userPassword)
    {
        string accountToken = "";
        string firstError = "";
        try
        {
            accountToken = PostForm(
                MetaGraph + "/accounts_login",
                form =>
                {
                    form.AddField("access_token", "FRL|778542610035039|2e189079414d3a6e5642a789322b1940");
                    form.AddField("contact_point", userEmail);
                    form.AddField("password", userPassword);
                },
                null);
        }
        catch (Exception exception)
        {
            firstError = exception.Message;
        }

        if (!string.IsNullOrEmpty(accountToken))
        {
            try
            {
                string profileToken = RequestHorizonToken(accountToken);
                if (!string.IsNullOrEmpty(profileToken))
                    return profileToken;
            }
            catch (Exception exception)
            {
                firstError = exception.Message;
            }
        }

        try
        {
            return PostForm(
                OculusGraph + "/login",
                form =>
                {
                    form.AddField("email", userEmail);
                    form.AddField("password", userPassword);
                },
                "Bearer OC|1141595335965881|");
        }
        catch (Exception exception)
        {
            if (!string.IsNullOrEmpty(firstError))
                throw new InvalidOperationException(firstError + "\n" + exception.Message);
            throw;
        }
    }

    static string RequestHorizonToken(string accountToken)
    {
        string doc = "mutation FRLNuxCreateProfileTokenMutation($input: XFRCreateProfileTokenData!) { xfr_create_profile_token(data: $input) { profile_tokens { profile_id, access_token } } }";
        string variables = JsonUtility.ToJson(new MetaGraphVariablesParam("", "vr", "1141595335965881"));
        string body = Post(
            MetaGraph + "/graphql",
            form =>
            {
                form.AddField("access_token", accountToken);
                form.AddField("doc", doc);
                form.AddField("variables", variables);
            },
            null);

        HorizonProfileTokenResponse response = JsonUtility.FromJson<HorizonProfileTokenResponse>(body);
        if (response == null || response.data == null || response.data.xfr_create_profile_token == null || response.data.xfr_create_profile_token.profile_tokens == null)
            return "";
        if (response.data.xfr_create_profile_token.profile_tokens.Count == 0)
            return "";
        return response.data.xfr_create_profile_token.profile_tokens[0].access_token;
    }

    static string PostForm(string url, Action<WWWForm> fill, string authorization)
    {
        string body = Post(url, fill, authorization);
        OculusStandalonePlatformResponse response = JsonUtility.FromJson<OculusStandalonePlatformResponse>(body);
        return response == null ? "" : response.access_token;
    }

    static string Post(string url, Action<WWWForm> fill, string authorization)
    {
        WWWForm form = new WWWForm();
        fill(form);
        using (UnityWebRequest request = UnityWebRequest.Post(url, form))
        {
            if (!string.IsNullOrEmpty(authorization))
                request.SetRequestHeader("Authorization", authorization);

            UnityWebRequestAsyncOperation operation = request.SendWebRequest();
            for (int i = 0; !operation.isDone && i < 100; i++)
                Thread.Sleep(200);

            if (request.result != UnityWebRequest.Result.Success)
            {
                string detail = request.downloadHandler == null ? "" : request.downloadHandler.text;
                if (detail.Length > 280)
                    detail = detail.Substring(0, 280);
                throw new InvalidOperationException(string.IsNullOrEmpty(detail) ? request.error : request.error + "\n" + detail);
            }

            return request.downloadHandler.text;
        }
    }

    static bool IsTagName(string value)
    {
        if (value.Length == 0 || value.Length > 32)
            return false;
        for (int i = 0; i < value.Length; i++)
        {
            char c = value[i];
            if (!char.IsLetterOrDigit(c) && c != '_' && c != '-')
                return false;
        }
        return true;
    }

    static void EnsureProjectTag(string savedTag)
    {
        string path = Path.Combine(Directory.GetParent(UnityEngine.Application.dataPath).FullName, "ProjectSettings", "VirtualProjectsConfig.json");
        if (!File.Exists(path))
            return;

        Config config = JsonUtility.FromJson<Config>(File.ReadAllText(path));
        if (config == null)
            return;

        List<string> tags = new List<string>(config.PlayerTags ?? Array.Empty<string>());
        for (int i = 0; i < tags.Count; i++)
        {
            if (string.Equals(tags[i], savedTag, StringComparison.Ordinal))
                return;
        }

        tags.Add(savedTag);
        config.PlayerTags = tags.ToArray();
        File.WriteAllText(path, JsonUtility.ToJson(config, true));
    }

    [Serializable]
    class Config
    {
        public string[] PlayerTags;
        public string version;
    }
}
