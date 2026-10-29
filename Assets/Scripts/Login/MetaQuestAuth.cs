using System;
using System.Collections;
using System.Security.Cryptography;
using Oculus.Platform;
using Oculus.Platform.Models;
using UnityEngine;

public static class MetaQuestAuth
{
    public sealed class Identity
    {
        public string AppScopedId;
        public string OrgScopedId;
        public string DisplayName;
        public string Nonce;
    }

    public sealed class Result
    {
        public bool Success;
        public bool NotEntitled;
        public string Message;
        public Identity Identity;
    }

    public static IEnumerator SignIn(Action<Result> completed)
    {
#if UNITY_EDITOR
        if (PlatformSettings.UseStandalonePlatform && string.IsNullOrEmpty(StandalonePlatformSettings.OculusPlatformTestUserAccessToken))
        {
            completed(Retry("Editor test user is not logged in. Use Meta > Platform > Edit Settings."));
            yield break;
        }
#endif

        Result result = null;

        if (!Core.IsInitialized())
        {
            yield return Initialize(value => result = value);
            if (result != null)
            {
                completed(result);
                yield break;
            }
        }

        yield return CheckEntitlement(value => result = value);
        if (result != null)
        {
            completed(result);
            yield break;
        }

        User user = null;
        yield return GetLoggedInUser(
            value => user = value,
            value => result = value);
        if (result != null)
        {
            completed(result);
            yield break;
        }

        string orgScopedId = null;
        yield return GetOrgScopedId(
            user.ID,
            value => orgScopedId = value,
            value => result = value);
        if (result != null)
        {
            completed(result);
            yield break;
        }

#if UNITY_EDITOR
        UnityEngine.Debug.Log("[SignIn] Editor Meta organization-scoped user ID: " + orgScopedId);
#endif

        string nonce = null;
        yield return GetUserProof(
            value => nonce = value,
            value => result = value);
        if (result != null)
        {
            completed(result);
            yield break;
        }

        completed(new Result
        {
            Success = true,
            Identity = new Identity
            {
                AppScopedId = user.ID.ToString(),
                OrgScopedId = orgScopedId,
                DisplayName = DisplayName(user),
                Nonce = nonce
            }
        });
    }

    static IEnumerator Initialize(Action<Result> failed)
    {
        Message<PlatformInitialize> message = null;
        bool finished = false;

        try
        {
            // No app id argument, same as Raider. Platform Settings then choose the account:
            // in the editor, Use Standalone Platform signs in the test user from that panel.
            Core.AsyncInitialize().OnComplete(value =>
            {
                message = value;
                finished = true;
            });
        }
        catch (Exception exception)
        {
            failed(Retry(exception.Message));
            yield break;
        }

        while (!finished)
            yield return null;

        if (message == null || message.IsError || message.Data == null || message.Data.Result != PlatformInitializeResult.Success)
        {
            failed(Retry(ErrorText(message, "Meta sign-in failed to start.")));
            yield break;
        }

        failed(null);
    }

    static IEnumerator CheckEntitlement(Action<Result> failed)
    {
        Message message = null;
        bool finished = false;

        try
        {
            Entitlements.IsUserEntitledToApplication().OnComplete(value =>
            {
                message = value;
                finished = true;
            });
        }
        catch (Exception exception)
        {
            failed(Retry(exception.Message));
            yield break;
        }

        while (!finished)
            yield return null;

        if (message == null || message.IsError)
        {
            // A failed check (including transport/service errors) is not proof of
            // missing ownership. Retry, and never grant access until it succeeds.
            failed(Retry(ErrorText(message, "Could not verify game ownership.")));
            yield break;
        }

        failed(null);
    }

    static IEnumerator GetLoggedInUser(Action<User> succeeded, Action<Result> failed)
    {
        Message<User> message = null;
        bool finished = false;

        try
        {
            Users.GetLoggedInUser().OnComplete(value =>
            {
                message = value;
                finished = true;
            });
        }
        catch (Exception exception)
        {
            failed(Retry(exception.Message));
            yield break;
        }

        while (!finished)
            yield return null;

        if (message == null || message.IsError || message.Data == null || message.Data.ID == 0)
        {
            string body = message == null || string.IsNullOrEmpty(message.data) ? "empty response" : message.data;
            if (DuckzLog.AllowWarning(out var logNotice1))
                logNotice1.Write("GetLoggedInUser failed. id=" + (message?.Data == null ? "null" : message.Data.ID.ToString()) + " body=" + body);
            failed(Retry(ErrorText(message, "Could not read the Meta account. " + body)));
            yield break;
        }

        succeeded(message.Data);
        failed(null);
    }

    static IEnumerator GetOrgScopedId(ulong appScopedId, Action<string> succeeded, Action<Result> failed)
    {
        Message<OrgScopedID> message = null;
        bool finished = false;

        try
        {
            Users.GetOrgScopedID(appScopedId).OnComplete(value =>
            {
                message = value;
                finished = true;
            });
        }
        catch (Exception exception)
        {
            failed(Retry(exception.Message));
            yield break;
        }

        while (!finished)
            yield return null;

        string orgScopedId = message != null && !message.IsError && message.Data != null
            ? message.Data.ID.ToString()
            : null;

        if (string.IsNullOrEmpty(orgScopedId) || orgScopedId.Length < 8)
        {
            failed(Retry(ErrorText(message, "Could not read the organization account id.")));
            yield break;
        }

        succeeded(orgScopedId);
        failed(null);
    }

    static IEnumerator GetUserProof(Action<string> succeeded, Action<Result> failed)
    {
        Message<UserProof> message = null;
        bool finished = false;

        try
        {
            Users.GetUserProof().OnComplete(value =>
            {
                message = value;
                finished = true;
            });
        }
        catch (Exception exception)
        {
            failed(Retry(exception.Message));
            yield break;
        }

        while (!finished)
            yield return null;

        string nonce = message != null && !message.IsError && message.Data != null
            ? message.Data.Value
            : null;

        if (string.IsNullOrEmpty(nonce))
        {
            failed(Retry(ErrorText(message, "Could not read the Meta sign-in proof.")));
            yield break;
        }

        succeeded(nonce);
        failed(null);
    }

    public static IEnumerator GetIntegrityToken(Action<string> succeeded, Action<string> failed)
    {
        Request<string> request = null;
        try
        {
            request = DeviceApplicationIntegrity.GetIntegrityToken(ChallengeNonce());
        }
        catch (Exception exception)
        {
            failed(exception.Message);
            yield break;
        }

        if (request == null)
        {
            failed("Meta sign-in is not ready for attestation.");
            yield break;
        }

        Message<string> message = null;
        bool finished = false;
        request.OnComplete(value =>
        {
            message = value;
            finished = true;
        });

        while (!finished)
            yield return null;

        string token = message != null && !message.IsError ? message.Data : null;
        if (!string.IsNullOrEmpty(token) && token.Length >= 2 && token[0] == '"' && token[token.Length - 1] == '"')
            token = token.Substring(1, token.Length - 2);

        if (string.IsNullOrEmpty(token))
        {
            failed(ErrorText(message, "Could not read the Quest attestation token."));
            yield break;
        }

        succeeded(token);
    }

    static string ChallengeNonce()
    {
        byte[] randomBytes = new byte[16];
        using (RandomNumberGenerator rng = RandomNumberGenerator.Create())
            rng.GetBytes(randomBytes);

        return Convert.ToBase64String(randomBytes).Replace('+', '-').Replace('/', '_').TrimEnd('=');
    }

    static string DisplayName(User user)
    {
        if (!string.IsNullOrEmpty(user.DisplayName))
            return user.DisplayName;
        if (!string.IsNullOrEmpty(user.OculusID))
            return user.OculusID;
        return "player";
    }

    static Result Retry(string message)
    {
        return new Result { Message = message };
    }

    static string ErrorText(Message message, string fallback)
    {
        string error = message?.GetError()?.Message;
        return string.IsNullOrEmpty(error) ? fallback : fallback + " Meta: " + error;
    }
}
