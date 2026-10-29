using System;
using System.Collections;
using System.Diagnostics;
using System.Reflection;
using UnityEngine;

public static class AccountSignIn
{
    public sealed class Outcome
    {
        public bool SignedIn;
        public bool Quit;
    }

    public static IEnumerator Run(GameConfig config, Action<string> status, float retryDelaySeconds, Action<Outcome> completed)
    {
        if (config == null)
            config = GameConfig.Current;

#if UNITY_EDITOR
        if (IsAdditionalEditor())
        {
            string playerId = "editor-" + Process.GetCurrentProcess().Id;
            UserPlayerPrefs.SetCurrentUser(playerId);
            PlayerSession session = PlayerSession.CreateLocal(playerId, "Player 2");
            DuckzLog.Log("Signed in as " + session.DisplayName + ".");
            DuckzLog.Log("Editor player ID: " + session.PlayerId);
            completed(new Outcome { SignedIn = true });
            yield break;
        }
#endif

        if (config == null || string.IsNullOrEmpty(config.backendBaseUrl))
        {
            ReportFailure(status, "Game config is missing a backend URL.", "Unable to start sign-in.", "configuration", true);
            completed(new Outcome());
            yield break;
        }

        while (true)
        {
            Report(status, "Signing in...");

            MetaQuestAuth.Result auth = null;
            yield return MetaQuestAuth.SignIn(value => auth = value);

            if (auth.NotEntitled)
            {
                ReportFailure(status, auth.Message, "Game ownership required.", "entitlement");
                completed(new Outcome { Quit = true });
                yield break;
            }

            if (!auth.Success)
            {
                ReportFailure(status, auth.Message, "Sign-in failed. Retrying...", "meta-auth");
                yield return new WaitForSeconds(retryDelaySeconds);
                continue;
            }

            UpdateUserPlayerPrefsScope(auth.Identity);

            GameSessionClient.Result account = null;
            yield return GameSessionClient.Register(config.backendBaseUrl, auth.Identity, value => account = value);

            if (account.Retry)
            {
                ReportFailure(status, account.Message, "Sign-in unavailable. Retrying...", "backend-retry");
                yield return new WaitForSeconds(retryDelaySeconds);
                continue;
            }

            if (!account.Success)
            {
                ReportFailure(status, account.Message, "Sign-in failed. Please restart.", "backend-failure", true);
                completed(new Outcome());
                yield break;
            }

            PlayerSession session = PlayerSession.Create(account.Session, auth.Identity.OrgScopedId, auth.Identity.DisplayName);
            DuckzLog.Log("Signed in as " + session.DisplayName + ".");
#if UNITY_EDITOR
            DuckzLog.Log("Player ID: " + session.PlayerId);
#endif
            session.RefreshAdmin();
            session.RefreshCosmetics();
            DuckzWallet wallet = DuckzWallet.Ensure();
            if (wallet != null)
                wallet.Refresh();
            DuckzIap.Ensure().RestoreDurablePurchases();
            EquippedLoadout.Ensure();
            completed(new Outcome { SignedIn = true });
            yield break;
        }
    }

    static void UpdateUserPlayerPrefsScope(MetaQuestAuth.Identity identity)
    {
        if (identity == null)
            return;
        if (!string.IsNullOrEmpty(identity.OrgScopedId))
            UserPlayerPrefs.SetCurrentUser(identity.OrgScopedId);
        else if (!string.IsNullOrEmpty(identity.AppScopedId))
            UserPlayerPrefs.SetCurrentUser(identity.AppScopedId);
    }

    static void ReportFailure(Action<string> status, string message, string displayMessage, string category, bool fatal = false)
    {
        string reason = string.IsNullOrWhiteSpace(message) ? "No failure reason was returned." : message;
        string diagnostic = $"[SignIn] {displayMessage} Stage: {category}. Reason: {reason}";
#if UNITY_EDITOR
        // Keep the reason visible on every Editor attempt, even with warnings
        // filtered out or the shared logger suppressing repeated messages.
        UnityEngine.Debug.Log(diagnostic);
#endif
        if (fatal)
        {
            if (DuckzLog.AllowError(out var logNotice1, category))
                logNotice1.Write(diagnostic);
        }
        else if (DuckzLog.AllowWarning(out var logNotice2, category))
            logNotice2.Write(diagnostic);
        status?.Invoke(displayMessage);
    }

    static void Report(Action<string> status, string message, string displayMessage = null)
    {
        if (string.IsNullOrEmpty(message))
            message = "Signing in...";

        DuckzLog.Log(message);
        status?.Invoke(displayMessage ?? message);
    }

#if UNITY_EDITOR
    static bool IsAdditionalEditor()
    {
        var type = Type.GetType("Unity.Multiplayer.PlayMode.CurrentPlayer, UnityEngine.MultiplayerModule");
        var property = type?.GetProperty("IsMainEditor", BindingFlags.Public | BindingFlags.Static);
        return property?.GetValue(null) is bool mainEditor && !mainEditor;
    }
#endif
}
