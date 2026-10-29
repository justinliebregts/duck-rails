using Liv.Lck.Settings;
using Liv.Lck.Util;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Liv.Lck
{
    /// <summary>
    /// Update Available page showing when a new LCK version is available.
    /// This page is dynamically shown/hidden based on update availability.
    /// </summary>
    public class LckUpdatePage : LckSettingsPageBase
    {
        protected override bool IsUpdatePage => true;

        private static LckVersionChecker.VersionCheckResult _latestVersionInfo;
        private static bool _checkInProgress = false;
        private static bool _updateAvailable = false;
        private static System.Collections.Generic.List<System.Action<LckVersionChecker.VersionCheckResult>> _pendingCallbacks
            = new System.Collections.Generic.List<System.Action<LckVersionChecker.VersionCheckResult>>();

        /// <summary>
        /// Event fired when an update check completes and an update is available.
        /// Subscribe to this to refresh UI when update status changes.
        /// </summary>
        public static event System.Action OnUpdateAvailable;

        private VisualElement _contentArea;

        [SettingsProvider]
        public static SettingsProvider CreateSettingsProvider()
        {
            var provider = new LckUpdatePage(
                "Project/LCK/5 Updates",
                SettingsScope.Project,
                new HashSet<string>(new[] { "LCK", "LIV", "Update", "Version", "Download" })
            );
            provider.label = "Updates";
            return provider;
        }

        public LckUpdatePage(string path, SettingsScope scopes, IEnumerable<string> keywords = null)
            : base(path, scopes, keywords)
        {
        }

        public override void OnActivate(string searchContext, VisualElement rootElement)
        {
            base.OnActivate(searchContext, rootElement);

            // Check for updates when page is activated
            if (!_checkInProgress && _latestVersionInfo == null)
            {
                CheckForUpdates();
            }
        }

        /// <summary>
        /// Check if update page should be visible in settings.
        /// Called by other pages to determine navigation.
        /// </summary>
        public static bool ShouldShowUpdatePage()
        {
            try
            {
                if (_latestVersionInfo == null || !_latestVersionInfo.Success)
                    return false;

                string currentVersion = LckSettings.Version;
                string latestVersion = _latestVersionInfo.LatestVersion;

                // Check if update is available
                if (!LckVersionChecker.IsUpdateAvailable(currentVersion, latestVersion))
                    return false;

                // Check if user dismissed this version
                var settings = LckSettings.Instance;
                if (settings != null && settings.DismissedUpdateVersion == latestVersion)
                    return false;

                return true;
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[LckUpdatePage] Error checking update page visibility: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Initiates an update check if one hasn't been done yet.
        /// Safe to call multiple times - won't restart if results exist or check in progress.
        /// </summary>
        public static void CheckForUpdates()
        {
            // Don't start a new check if we already have results
            if (_latestVersionInfo != null) return;

            CheckForUpdatesWithCallback(null);
        }

        /// <summary>
        /// Forces a new update check, even if results already exist.
        /// Note: If a check is in progress, this will wait for it to complete
        /// and use those results rather than starting a new check.
        /// </summary>
        public static void ForceCheckForUpdates()
        {
            // Only clear if no check is in progress
            if (!_checkInProgress)
            {
                _latestVersionInfo = null;
                _updateAvailable = false;
            }
            CheckForUpdatesWithCallback(null);
        }

        /// <summary>
        /// Initiates an update check with an optional callback for additional processing.
        /// The callback is called after the shared state is updated.
        /// If results already exist, callback is called immediately.
        /// If a check is in progress, callback is queued and called when check completes.
        /// </summary>
        public static void CheckForUpdatesWithCallback(System.Action<LckVersionChecker.VersionCheckResult> callback)
        {
            // If we already have results, call callback immediately and fire event
            if (_latestVersionInfo != null)
            {
                callback?.Invoke(_latestVersionInfo);
                // Fire event if update is available (delayed to allow UI to initialize)
                if (_updateAvailable)
                {
                    EditorApplication.delayCall += () => OnUpdateAvailable?.Invoke();
                }
                return;
            }

            // If a check is in progress, queue the callback
            if (_checkInProgress)
            {
                if (callback != null)
                {
                    _pendingCallbacks.Add(callback);
                }
                return;
            }

            _checkInProgress = true;
            if (callback != null)
            {
                _pendingCallbacks.Add(callback);
            }

            LckVersionChecker.CheckForUpdate(result =>
            {
                _checkInProgress = false;
                _latestVersionInfo = result;

                if (result.Success)
                {
                    string currentVersion = LckSettings.Version;
                    _updateAvailable = LckVersionChecker.IsUpdateAvailable(currentVersion, result.LatestVersion);
                }
                else
                {
                    _updateAvailable = false;
                }

                // Call all pending callbacks
                foreach (var pendingCallback in _pendingCallbacks)
                {
                    pendingCallback?.Invoke(result);
                }
                _pendingCallbacks.Clear();

                // Fire event if update is available (delayed to allow UI to initialize first)
                if (_updateAvailable)
                {
                    EditorApplication.delayCall += () => OnUpdateAvailable?.Invoke();
                }
            });
        }

        /// <summary>
        /// Gets the latest version info if available.
        /// </summary>
        public static LckVersionChecker.VersionCheckResult GetLatestVersionInfo() => _latestVersionInfo;

        /// <summary>
        /// Returns true if an update check is currently in progress.
        /// </summary>
        public static bool IsCheckInProgress => _checkInProgress;

        /// <summary>
        /// Returns true if an update check found a newer version (raw check, ignores dismissal).
        /// Use this for notification bar display.
        /// </summary>
        public static bool IsUpdateAvailable => _updateAvailable;

        /// <summary>
        /// Returns true if update is available AND user hasn't dismissed it.
        /// Use this for automatic navigation to update page.
        /// </summary>
        public static bool ShouldNavigateToUpdatePage => _updateAvailable && ShouldShowUpdatePage();

        protected override void BuildUI(VisualElement root)
        {
            var scrollView = CreateContentArea();
            _contentArea = scrollView.Q<VisualElement>(className: "lck-content-area");

            // Check if we have update info
            if (_latestVersionInfo == null)
            {
                DrawCheckingState(_contentArea);
            }
            else if (!_latestVersionInfo.Success)
            {
                DrawErrorState(_contentArea);
            }
            else if (!_updateAvailable)
            {
                // Only show "up to date" if there truly is no update available
                DrawUpToDateState(_contentArea);
            }
            else
            {
                // Show update info if an update exists (regardless of dismissal status)
                DrawUpdateAvailable(_contentArea);
            }

            root.Add(scrollView);
        }

        private void DrawCheckingState(VisualElement parent)
        {
            AddPageTitle(parent, "Checking for Updates...");
            AddBodyText(parent, "Please wait while we check for the latest LCK version.");

            AddSpacer(parent, "lg");

            var infoBox = CreateInfoBox("Contacting LIV servers to check for updates.");
            parent.Add(infoBox);

            // Auto-refresh when check completes
            if (_checkInProgress)
            {
                parent.schedule.Execute(() =>
                {
                    if (!_checkInProgress)
                    {
                        RefreshContent();
                    }
                }).Every(500).Until(() => !_checkInProgress);
            }
        }

        private void DrawUpToDateState(VisualElement parent)
        {
            AddPageTitle(parent, "You're Up to Date");

            string currentVersion = LckSettings.Version;
            AddBodyText(parent, $"You are running LCK version {currentVersion}, which is the latest version available.");

            AddSpacer(parent, "lg");

            var successBox = CreateSuccessBox("No updates available. You have the latest version of LIV Camera Kit.");
            parent.Add(successBox);

            AddSpacer(parent, "md");

            Button checkButton = CreateSecondaryButton("Check Again", () =>
            {
                ForceCheckForUpdates();
                RefreshContent();
            });
            parent.Add(checkButton);
        }

        private void DrawErrorState(VisualElement parent)
        {
            AddPageTitle(parent, "Update Check Failed");
            AddBodyText(parent, "Unable to check for updates. Please verify your internet connection.");

            AddSpacer(parent, "lg");

            string errorMessage = _latestVersionInfo?.Error ?? "Unknown error";
            var warningBox = CreateWarningBox($"Error: {errorMessage}");
            parent.Add(warningBox);

            AddSpacer(parent, "md");

            Button retryButton = CreateAccentButton("Retry", () =>
            {
                ForceCheckForUpdates();
                RefreshContent();
            });
            parent.Add(retryButton);
        }

        private void DrawUpdateAvailable(VisualElement parent)
        {
            AddPageTitle(parent, "Update Available");
            AddBodyText(parent,
                "A new version of LIV Camera Kit is available. We recommend updating to get the latest features and bug fixes.");

            AddSpacer(parent, "lg");

            string latestVersion = _latestVersionInfo?.LatestVersion ?? "Unknown";
            string currentVersion = LckSettings.Version;
            string downloadUrl = "https://dashboard.liv.tv/dev/login";

            // Version comparison - horizontal layout
            VisualElement versionRow = new VisualElement();
            versionRow.style.flexDirection = FlexDirection.Row;
            versionRow.style.alignItems = Align.Center;
            versionRow.style.justifyContent = Justify.Center;
            versionRow.style.marginBottom = 24;

            // Current version
            VisualElement currentBox = new VisualElement();
            currentBox.style.backgroundColor = new Color(0.12f, 0.12f, 0.14f);
            currentBox.style.borderTopLeftRadius = 8;
            currentBox.style.borderTopRightRadius = 8;
            currentBox.style.borderBottomLeftRadius = 8;
            currentBox.style.borderBottomRightRadius = 8;
            currentBox.style.paddingTop = 16;
            currentBox.style.paddingBottom = 16;
            currentBox.style.paddingLeft = 24;
            currentBox.style.paddingRight = 24;
            currentBox.style.alignItems = Align.Center;

            Label currentLabel = new Label("CURRENT");
            currentLabel.style.fontSize = 10;
            currentLabel.style.color = new Color(0.6f, 0.6f, 0.6f);
            currentLabel.style.marginBottom = 4;
            currentBox.Add(currentLabel);

            Label currentVersionLabel = new Label(currentVersion);
            currentVersionLabel.style.fontSize = 20;
            currentVersionLabel.style.color = new Color(0.7f, 0.7f, 0.7f);
            currentVersionLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            currentBox.Add(currentVersionLabel);

            versionRow.Add(currentBox);

            // Arrow
            Label arrowLabel = new Label("\u2192"); // Right arrow
            arrowLabel.style.fontSize = 24;
            arrowLabel.style.color = new Color(0.5f, 0.5f, 0.5f);
            arrowLabel.style.marginLeft = 16;
            arrowLabel.style.marginRight = 16;
            versionRow.Add(arrowLabel);

            // New version
            VisualElement newBox = new VisualElement();
            newBox.style.backgroundColor = new Color(0.15f, 0.2f, 0.1f);
            newBox.style.borderTopLeftRadius = 8;
            newBox.style.borderTopRightRadius = 8;
            newBox.style.borderBottomLeftRadius = 8;
            newBox.style.borderBottomRightRadius = 8;
            newBox.style.borderTopWidth = 1;
            newBox.style.borderBottomWidth = 1;
            newBox.style.borderLeftWidth = 1;
            newBox.style.borderRightWidth = 1;
            newBox.style.borderTopColor = new Color(0.831f, 1f, 0.165f);
            newBox.style.borderBottomColor = new Color(0.831f, 1f, 0.165f);
            newBox.style.borderLeftColor = new Color(0.831f, 1f, 0.165f);
            newBox.style.borderRightColor = new Color(0.831f, 1f, 0.165f);
            newBox.style.paddingTop = 16;
            newBox.style.paddingBottom = 16;
            newBox.style.paddingLeft = 24;
            newBox.style.paddingRight = 24;
            newBox.style.alignItems = Align.Center;

            Label newLabel = new Label("AVAILABLE");
            newLabel.style.fontSize = 10;
            newLabel.style.color = new Color(0.831f, 1f, 0.165f);
            newLabel.style.marginBottom = 4;
            newBox.Add(newLabel);

            Button newVersionButton = new Button(() => Application.OpenURL(downloadUrl));
            newVersionButton.text = latestVersion;
            newVersionButton.style.fontSize = 20;
            newVersionButton.style.color = new Color(0.831f, 1f, 0.165f);
            newVersionButton.style.unityFontStyleAndWeight = FontStyle.Bold;
            newVersionButton.style.backgroundColor = new Color(0, 0, 0, 0);
            newVersionButton.style.borderTopWidth = 0;
            newVersionButton.style.borderBottomWidth = 0;
            newVersionButton.style.borderLeftWidth = 0;
            newVersionButton.style.borderRightWidth = 0;
            newVersionButton.style.paddingTop = 0;
            newVersionButton.style.paddingBottom = 0;
            newVersionButton.style.paddingLeft = 0;
            newVersionButton.style.paddingRight = 0;
            newVersionButton.style.marginTop = 0;
            newVersionButton.style.marginBottom = 0;
            newBox.Add(newVersionButton);

            versionRow.Add(newBox);

            parent.Add(versionRow);

            // Update Steps Section
            AddSectionHeader(parent, "How to Update");

            var stepsCard = CreateCard();

            AddUpdateStep(stepsCard, "1", "Download the new package",
                "Visit the LIV Dashboard to download the latest LCK package zip file.",
                "Download from LIV Dashboard", "https://dashboard.liv.tv/dev/login");

            AddUpdateStep(stepsCard, "2", "Close the Unity Editor",
                "Ensure the Unity Editor is completely closed before proceeding. This prevents file locking issues during the update.");

            AddUpdateStep(stepsCard, "3", "Delete the existing LCK package",
                "Navigate to your project's Packages folder and delete the existing LCK package folder (tv.liv.lck).");

            AddUpdateStep(stepsCard, "4", "Extract the new package",
                "Unzip the downloaded package file directly into your project's Packages folder.");

            AddUpdateStep(stepsCard, "5", "Reopen your project",
                "Open your Unity project. Unity will import the new package automatically. Check the Console for any errors or warnings.");

            parent.Add(stepsCard);

            AddSpacer(parent, "md");

            // Action buttons
            VisualElement buttonRow = new VisualElement();
            buttonRow.AddToClassList("lck-button-row");

            // Download button
            Button downloadButton = new Button(() =>
            {
                Application.OpenURL("https://dashboard.liv.tv/dev/login");
            });
            downloadButton.text = $"Download Version {latestVersion}";
            downloadButton.AddToClassList("lck-button");
            downloadButton.AddToClassList("lck-button--primary");
            downloadButton.style.marginRight = 8;
            buttonRow.Add(downloadButton);

            // Update instructions button
            Button instructionsButton = new Button(() =>
            {
                Application.OpenURL("https://liv.mintlify.app/unity/upgrade-guide");
            });
            instructionsButton.text = "Full Documentation";
            instructionsButton.AddToClassList("lck-button");
            instructionsButton.AddToClassList("lck-button--secondary");
            buttonRow.Add(instructionsButton);

            parent.Add(buttonRow);

            AddSpacer(parent, "lg");

            // Continue to setup link
            var card = CreateCard();
            Label continueLabel = new Label("Ready to continue?");
            continueLabel.style.color = Color.white;
            continueLabel.style.marginBottom = 8;
            continueLabel.style.fontSize = 14;
            continueLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            card.Add(continueLabel);

            AddBodyText(card, "You can continue using LCK while on this version. Update when convenient.");

            Button continueButton = CreateAccentButton("Continue to Setup", () =>
            {
                SettingsService.OpenProjectSettings("Project/LCK/1 Overview");
            });
            card.Add(continueButton);

            parent.Add(card);
        }

        private void AddUpdateStep(VisualElement parent, string stepNumber, string title, string description, string linkText = null, string linkUrl = null)
        {
            VisualElement stepContainer = new VisualElement();
            stepContainer.style.flexDirection = FlexDirection.Row;
            stepContainer.style.alignItems = Align.FlexStart;
            stepContainer.style.marginBottom = 16;

            // Step number circle
            VisualElement numberCircle = new VisualElement();
            numberCircle.style.width = 28;
            numberCircle.style.height = 28;
            numberCircle.style.borderTopLeftRadius = 14;
            numberCircle.style.borderTopRightRadius = 14;
            numberCircle.style.borderBottomLeftRadius = 14;
            numberCircle.style.borderBottomRightRadius = 14;
            numberCircle.style.backgroundColor = AccentPurple;
            numberCircle.style.alignItems = Align.Center;
            numberCircle.style.justifyContent = Justify.Center;
            numberCircle.style.marginRight = 12;
            numberCircle.style.flexShrink = 0;

            Label numberLabel = new Label(stepNumber);
            numberLabel.style.color = Color.white;
            numberLabel.style.fontSize = 14;
            numberLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            numberLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            numberCircle.Add(numberLabel);

            stepContainer.Add(numberCircle);

            // Step content
            VisualElement contentContainer = new VisualElement();
            contentContainer.style.flexGrow = 1;

            Label titleLabel = new Label(title);
            titleLabel.style.color = Color.white;
            titleLabel.style.fontSize = 14;
            titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            titleLabel.style.marginBottom = 4;
            contentContainer.Add(titleLabel);

            Label descLabel = new Label(description);
            descLabel.AddToClassList("lck-body-text");
            descLabel.style.marginBottom = linkText != null ? 8 : 0;
            descLabel.style.whiteSpace = WhiteSpace.Normal;
            contentContainer.Add(descLabel);

            // Optional link button (white/primary for external links)
            if (!string.IsNullOrEmpty(linkText) && !string.IsNullOrEmpty(linkUrl))
            {
                Button linkButton = new Button(() => Application.OpenURL(linkUrl));
                linkButton.text = linkText;
                linkButton.AddToClassList("lck-button");
                linkButton.AddToClassList("lck-button--small");
                linkButton.AddToClassList("lck-button--primary");
                contentContainer.Add(linkButton);
            }

            stepContainer.Add(contentContainer);
            parent.Add(stepContainer);
        }

        private void RefreshContent()
        {
            if (_rootElement != null)
            {
                _rootElement.Clear();

                // Re-add stylesheet
                var styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(StyleSheetPath);
                if (styleSheet == null)
                {
                    styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(DevStyleSheetPath);
                }
                if (styleSheet != null)
                {
                    _rootElement.styleSheets.Add(styleSheet);
                }

                _rootElement.AddToClassList("lck-wizard-root");
                ApplyMonospaceFont(_rootElement);
                BuildUI(_rootElement);
            }
        }
    }
}
