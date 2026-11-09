using System;
using System.Collections;
using System.Security.Cryptography;
using Oculus.Platform;
using Oculus.Platform.Models;
using UnityEngine;

public static class MetaQuestAuth
{
    static ulong ageCategoryRequestedUserId;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetAgeCategoryRequest()
    {
        ageCategoryRequestedUserId = 0;
    }

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

    public static IEnumerator SignIn(Action<Result> completed, Action<string> progress = null)
    {
#if UNITY_EDITOR
        if (PlatformSettings.UseStandalonePlatform && !MppmStandaloneCredentials.TryApplyForCurrentPlayer(out string editorError))
        {
            completed(Retry(editorError));
            yield break;
        }
        if (PlatformSettings.UseStandalonePlatform && string.IsNullOrEmpty(StandalonePlatformSettings.OculusPlatformTestUserAccessToken))
        {
            completed(Retry("Editor test user is not logged in. Use Meta > Platform > Edit Settings."));
            yield break;
        }
#endif

        Result result = null;

        if (!Core.IsInitialized())
        {
            progress?.Invoke("Starting Meta platform...");
            yield return Initialize(value => result = value);
            if (result != null)
            {
                completed(result);
                yield break;
            }
        }

        progress?.Invoke("Checking Meta entitlement...");
        yield return CheckEntitlement(value => result = value);
        if (result != null)
        {
            completed(result);
            yield break;
        }

        User user = null;
        progress?.Invoke("Reading Meta account...");
        yield return GetLoggedInUser(
            value => user = value,
            value => result = value);
        if (result != null)
        {
            completed(result);
            yield break;
        }

        RequestAgeCategory(user.ID);

        string orgScopedId = null;
        progress?.Invoke("Reading Meta organization identity...");
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
        progress?.Invoke("Getting Meta sign-in proof...");
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

    static void RequestAgeCategory(ulong userId)
    {
        if (ageCategoryRequestedUserId == userId)
            return;

        // Best effort once per signed-in user/session, including failed attempts.
        // Never wait for this request or repeat it during backend login retries.
        ageCategoryRequestedUserId = userId;
        try
        {
            Request<UserAccountAgeCategory> request = UserAgeCategory.Get();
            if (request == null)
            {
                if (DuckzLog.AllowWarning(out var notice))
                    notice.Write("Meta age category request unavailable. Login will continue.");
                return;
            }

            request.OnComplete(OnAgeCategoryReceived);
        }
        catch (Exception exception)
        {
            if (DuckzLog.AllowWarning(out var notice))
                notice.Write("Meta age category request failed. Login will continue. " + exception.Message);
        }
    }

    static void OnAgeCategoryReceived(Message<UserAccountAgeCategory> message)
    {
        if (message == null || message.IsError || message.Data == null)
        {
            if (DuckzLog.AllowWarning(out var notice))
                notice.Write(ErrorText(message, "Meta age category unavailable.") + " Gameplay is unaffected.");
            return;
        }

        // Includes Unknown; no category changes gameplay or account access.
        DuckzLog.Log("Meta age category: " + message.Data.AgeCategory);
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

        yield return WaitForCallback(() => finished, "Initialize");
        if (!finished)
        {
            failed(Retry("Meta Initialize timed out after 30 seconds."));
            yield break;
        }

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

        yield return WaitForCallback(() => finished, "CheckEntitlement");
        if (!finished)
        {
            failed(Retry("Meta CheckEntitlement timed out after 30 seconds."));
            yield break;
        }

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

        yield return WaitForCallback(() => finished, "GetLoggedInUser");
        if (!finished)
        {
            failed(Retry("Meta GetLoggedInUser timed out after 30 seconds."));
            yield break;
        }

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

        yield return WaitForCallback(() => finished, "GetOrgScopedId");
        if (!finished)
        {
            failed(Retry("Meta GetOrgScopedId timed out after 30 seconds."));
            yield break;
        }

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

        yield return WaitForCallback(() => finished, "GetUserProof");
        if (!finished)
        {
            failed(Retry("Meta GetUserProof timed out after 30 seconds."));
            yield break;
        }

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

        yield return WaitForCallback(() => finished, "GetIntegrityToken");
        if (!finished)
        {
            failed("Meta GetIntegrityToken timed out after 30 seconds.");
            yield break;
        }

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

    // Late callbacks update only the abandoned invocation's locals.
    public static IEnumerator WaitForCallback(Func<bool> finished, string stage, float timeout = 30f)
    {
        double started = Time.realtimeSinceStartupAsDouble;
        Debug.Log("[SignIn] Meta " + stage + " started");
        while (!finished() && Time.realtimeSinceStartupAsDouble - started < timeout)
            yield return null;
        Debug.Log("[SignIn] Meta " + stage + (finished() ? " callback received after " : " timed out after ") +
            (Time.realtimeSinceStartupAsDouble - started).ToString("F1") + "s");
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
