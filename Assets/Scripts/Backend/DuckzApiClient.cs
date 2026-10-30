using System;
using System.Collections;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

public static class DuckzApiClient
{
    public sealed class Response<T>
    {
        public bool Success;
        public long Code;
        public string Message;
        public string Body;
        public T Data;
    }

    [Serializable]
    class Envelope<T>
    {
        public bool success;
        public string message;
        public string error;
        public string[] messages;
        public T data;
    }

    [Serializable]
    class RefreshData
    {
        public string access_token;
        public string refresh_token;
    }

    const int MaxAttempts = 4;
    const float RetryDelaySeconds = 1.5f;

    static HMACSHA256 hmac;
    static string hmacSigningId;
    static bool refreshing;

    public static bool IsReady =>
        PlayerSession.Instance != null && PlayerSession.Instance.TryGetAccessToken(out _);

    public static IEnumerator Get<T>(string path, Action<Response<T>> completed)
    {
        yield return Send("GET", path, null, false, completed);
    }

    // Signed requests carry X-Nonce/X-Signature for the backend's NonceRepeatCheck. Retries reuse
    // the same body (and so the same request_id) with a fresh nonce.
    public static IEnumerator PostSigned<T>(string path, object body, Action<Response<T>> completed)
    {
        yield return Send("POST", path, JsonUtility.ToJson(body), true, completed);
    }

    static IEnumerator Send<T>(string method, string path, string json, bool signed, Action<Response<T>> completed)
    {
        var result = new Response<T>();
        bool refreshedOnce = false;

        for (int attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            PlayerSession session = PlayerSession.Instance;
            GameConfig config = GameConfig.Current;
            if (session == null || !session.TryGetAccessToken(out string token) || config == null)
            {
                result.Message = "Not signed in.";
                completed(result);
                yield break;
            }

            using (UnityWebRequest request = BuildRequest(method, config.backendBaseUrl, path, json))
            {
                request.SetRequestHeader("Authorization", "Bearer " + token);
                if (signed)
                    Sign(request, session, config);

                yield return request.SendWebRequest();

                result.Code = request.responseCode;
                result.Body = request.downloadHandler.text;
                Envelope<T> envelope = Parse<T>(result.Body);
                result.Message = ServerMessage(envelope) ?? request.error;
                result.Data = envelope != null ? envelope.data : default;

                if (request.result == UnityWebRequest.Result.Success)
                {
                    result.Success = true;
                    completed(result);
                    yield break;
                }

                if (result.Code == 401 && !refreshedOnce)
                {
                    refreshedOnce = true;
                    bool refreshed = false;
                    yield return RefreshTokens(ok => refreshed = ok);
                    if (refreshed)
                        continue;
                }

                bool transient = request.result == UnityWebRequest.Result.ConnectionError || result.Code == 0 || result.Code >= 500;
                if (!transient)
                    break;
            }

            if (attempt < MaxAttempts)
                yield return new WaitForSecondsRealtime(RetryDelaySeconds * attempt);
        }

        completed(result);
    }

    static UnityWebRequest BuildRequest(string method, string baseUrl, string path, string json)
    {
        string url = baseUrl.TrimEnd('/') + "/" + path.TrimStart('/');
        var request = new UnityWebRequest(url, method) { downloadHandler = new DownloadHandlerBuffer() };
        request.timeout = 20;
        if (json != null)
        {
            request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
            request.SetRequestHeader("Content-Type", "application/json");
        }
        return request;
    }

    static void Sign(UnityWebRequest request, PlayerSession session, GameConfig config)
    {
        if (hmac == null || hmacSigningId != session.SigningId)
        {
            byte[] derived;
            using (var deriver = new HMACSHA256(Encoding.UTF8.GetBytes(config.requestSigningSecret ?? "")))
                derived = deriver.ComputeHash(Encoding.UTF8.GetBytes(session.SigningId));
            hmac?.Dispose();
            hmac = new HMACSHA256(derived);
            hmacSigningId = session.SigningId;
        }

        string nonce = Guid.NewGuid().ToString("N");
        byte[] nonceBytes = Encoding.UTF8.GetBytes(nonce);
        byte[] body = request.uploadHandler?.data ?? Array.Empty<byte>();
        byte[] combined = new byte[nonceBytes.Length + body.Length];
        Buffer.BlockCopy(nonceBytes, 0, combined, 0, nonceBytes.Length);
        Buffer.BlockCopy(body, 0, combined, nonceBytes.Length, body.Length);

        byte[] hash = hmac.ComputeHash(combined);
        request.SetRequestHeader("X-Nonce", nonce);
        request.SetRequestHeader("X-Signature", BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant());
    }

    static IEnumerator RefreshTokens(Action<bool> completed)
    {
        if (refreshing)
        {
            while (refreshing)
                yield return null;
            completed(true);
            yield break;
        }

        PlayerSession session = PlayerSession.Instance;
        GameConfig config = GameConfig.Current;
        if (session == null || config == null || string.IsNullOrEmpty(session.RefreshToken))
        {
            completed(false);
            yield break;
        }

        refreshing = true;
        var form = new WWWForm();
        form.AddField("refresh_token", session.RefreshToken);
        string url = config.backendBaseUrl.TrimEnd('/') + "/session/" + session.SessionId;
        using (UnityWebRequest request = UnityWebRequest.Post(url, form))
        {
            yield return request.SendWebRequest();
            refreshing = false;

            Envelope<RefreshData> envelope = Parse<RefreshData>(request.downloadHandler.text);
            if (request.result != UnityWebRequest.Result.Success || envelope?.data == null || string.IsNullOrEmpty(envelope.data.access_token))
            {
                {
                    if (DuckzLog.AllowWarning(out var logNotice1))
                        logNotice1.Write("Session refresh failed: " + (ServerMessage(envelope) ?? request.error));
                }
                completed(false);
                yield break;
            }

            session.UpdateTokens(envelope.data.access_token, envelope.data.refresh_token);
            completed(true);
        }
    }

    static Envelope<T> Parse<T>(string json)
    {
        if (string.IsNullOrEmpty(json))
            return null;
        try
        {
            return JsonUtility.FromJson<Envelope<T>>(json);
        }
        catch (Exception exception)
        {
            {
                if (DuckzLog.AllowWarning(out var logNotice2))
                    logNotice2.Write("Backend response could not be read: " + exception.Message);
            }
            return null;
        }
    }

    static string ServerMessage<T>(Envelope<T> body)
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
}
