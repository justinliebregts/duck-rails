using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

public static class GameSessionClient
{
    public const string Platform = "OCULUS";

    public sealed class Result
    {
        public bool Success;
        public bool Retry;
        public string Message;
        public SessionData Session;
    }

    [Serializable]
    public class SessionData
    {
        public int session_id;
        public string access_token;
        public string refresh_token;
        public string gr_id;
        public string player_id_separated;
        public string display_name;
        public string platform;
    }

    [Serializable]
    class AccountResponse
    {
        public string message;
        public string error;
        public string[] messages;
        public SessionData data;
    }

    public static IEnumerator Register(string backendBaseUrl, MetaQuestAuth.Identity identity, Action<Result> completed)
    {
        string version = Application.version;
        if (string.IsNullOrEmpty(version))
            version = "0.0.0";

        string device = "Unknown";
        if (OVRManager.instance != null)
            device = OVRManager.systemHeadsetType.ToString();

        var form = new List<IMultipartFormSection>
        {
            new MultipartFormDataSection("player_id", identity.OrgScopedId),
            new MultipartFormDataSection("platform_id", identity.AppScopedId),
            new MultipartFormDataSection("platform_token", identity.Nonce),
            new MultipartFormDataSection("platform", Platform),
            new MultipartFormDataSection("name", identity.DisplayName),
            new MultipartFormDataSection("region", "-"),
            new MultipartFormDataSection("version", version),
            new MultipartFormDataSection("session", "duckrails"),
            new MultipartFormDataSection("device", device)
        };

        string url = backendBaseUrl.TrimEnd('/') + "/player/v2/social-account/oculus";
        using (UnityWebRequest request = UnityWebRequest.Post(url, form))
        {
#if UNITY_EDITOR
            // The backend verifies the Meta proof and organization ID against its Editor allowlist.
            request.SetRequestHeader("X-Meta-Editor-Login", "1");
#else
            string attestationToken = null;
            string attestationError = null;
            yield return MetaQuestAuth.GetIntegrityToken(
                value => attestationToken = value,
                value => attestationError = value);
            if (string.IsNullOrEmpty(attestationToken))
            {
                completed(Retry(string.IsNullOrEmpty(attestationError) ? "Could not read the Quest attestation token." : attestationError));
                yield break;
            }

            request.SetRequestHeader("Authorization", "Bearer " + attestationToken);
#endif
            yield return request.SendWebRequest();

            long code = request.responseCode;
            AccountResponse body = Parse(request.downloadHandler.text);
            string serverMessage = ServerMessage(body);

            if (request.result == UnityWebRequest.Result.ConnectionError || code == 0 || code >= 500)
            {
                string retryMessage = serverMessage;
                if (string.IsNullOrEmpty(retryMessage))
                    retryMessage = string.IsNullOrEmpty(request.error) ? "Could not reach the account server." : request.error;
                completed(Retry(retryMessage));
                yield break;
            }

            if (request.result != UnityWebRequest.Result.Success)
            {
                completed(Stop(code, serverMessage));
                yield break;
            }

            if (body == null || body.data == null || string.IsNullOrEmpty(body.data.access_token))
            {
                completed(Stop(code, "Sign-in response was missing a session."));
                yield break;
            }

            completed(new Result
            {
                Success = true,
                Session = body.data
            });
        }
    }

    static AccountResponse Parse(string json)
    {
        if (string.IsNullOrEmpty(json))
            return null;

        try
        {
            return JsonUtility.FromJson<AccountResponse>(json);
        }
        catch (Exception exception)
        {
            {
                if (DuckzLog.AllowWarning(out var logNotice1))
                    logNotice1.Write("Account response could not be read: " + exception.Message);
            }
            return null;
        }
    }

    static string ServerMessage(AccountResponse body)
    {
        if (body == null)
            return null;
        if (body.messages != null && body.messages.Length > 0 && !string.IsNullOrEmpty(body.messages[0]))
            return body.messages[0];
        if (!string.IsNullOrEmpty(body.message))
            return body.message;
        if (!string.IsNullOrEmpty(body.error))
            return body.error;
        return null;
    }

    static Result Retry(string message)
    {
        return new Result { Retry = true, Message = message };
    }

    static Result Stop(long code, string message)
    {
        if (string.IsNullOrEmpty(message))
            message = "Sign-in failed (" + code + ").";
        return new Result { Message = message };
    }
}
