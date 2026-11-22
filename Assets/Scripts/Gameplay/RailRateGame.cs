using System;
using Oculus.Platform;
using TMPro;
using UnityEngine;
using Application = UnityEngine.Application;

/// <summary>User-initiated, local-only store review action. Never records a review as submitted.</summary>
public sealed class RailRateGame : MonoBehaviour
{
    public TMP_Text status;
    float nextRequest;

    public void OpenRatingPage()
    {
        if (!Application.isPlaying || Time.unscaledTime < nextRequest || RailWorld.InputBlocked) return;
        nextRequest = Time.unscaledTime + 3f;
#if UNITY_ANDROID && !UNITY_EDITOR
        if (!Core.IsInitialized())
        {
            SetStatus("Store not ready. Please try again.");
            return;
        }
        SetStatus("Opening Meta rating page...");
        try
        {
            // Do not gate this on CanLaunchRateAndReview: it can incorrectly return false.
            RateAndReview.RateAndReviewLauncher().OnComplete(message =>
            {
                if (this == null) return;
                if (!message.IsError)
                {
                    SetStatus("Thanks for sharing your feedback!");
                    return;
                }
                // Meta refuses the native flow for some users (error 1002, "Not allowed to launch
                // rate and review flow for this user"); fall back to the store listing page.
                if (DuckzLog.AllowWarning(out var notice, "rate-launcher"))
                    notice.Write("[Rate Game] " + message.GetError().Message);
                OpenStoreListing();
            });
        }
        catch (Exception exception)
        {
            if (DuckzLog.AllowWarning(out var notice, "rate-exception"))
                notice.Write("[Rate Game] " + exception.Message);
            OpenStoreListing();
        }
#else
        OpenStoreListing();
#endif
    }

    void OpenStoreListing()
    {
        string appId = PlatformSettings.MobileAppID;
        if (string.IsNullOrEmpty(appId))
        {
            SetStatus("Store app ID is not configured.");
            return;
        }
        Application.OpenURL("https://www.meta.com/experiences/" + appId + "/");
        SetStatus("Store opened. Find Ratings and Reviews.");
    }

    void SetStatus(string message)
    {
        if (status != null) status.text = message;
    }
}
