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
                SetStatus(message.IsError ? "Could not open the store. Please try again." : "Thanks for sharing your feedback!");
                if (message.IsError) Debug.LogWarning("[Rate Game] " + message.GetError().Message);
            });
        }
        catch (Exception exception)
        {
            SetStatus("Could not open the store. Please try again.");
            Debug.LogWarning("[Rate Game] " + exception.Message);
        }
#else
        string appId = PlatformSettings.MobileAppID;
        if (string.IsNullOrEmpty(appId))
        {
            SetStatus("Store app ID is not configured.");
            return;
        }
        Application.OpenURL("https://www.meta.com/experiences/" + appId + "/");
        SetStatus("Store opened. Find Ratings and Reviews.");
#endif
    }

    void SetStatus(string message)
    {
        if (status != null) status.text = message;
    }
}
