using Liv.Lck.Audio;
using Liv.Lck.Audio.FMOD;
using Liv.Lck.Audio.Wwise;
using Liv.Lck.Settings;
using Liv.Lck.Util;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using static Liv.Lck.Util.LckVersionChecker;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Liv.Lck
{
    /// <summary>
    /// LCK Setup Wizard EditorWindow for guiding developers through LCK integration.
    /// This is a partial class - helper methods are in LckSetupWizard.Helpers.cs.
    /// </summary>
    public partial class LckSetupWizard : EditorWindow
    {
        private WizardPage _currentPage = WizardPage.Welcome;

        private InteractionToolkitType _selectedToolkit = InteractionToolkitType.None;
        private UnityXRInteractionMethodType _selectedUnityXRMethod = UnityXRInteractionMethodType.None;
        private AudioSystemType _selectedAudioSystem = AudioSystemType.None;

        private VisualElement _contentArea;

        // Version check state
        private VersionCheckResult _latestVersionInfo;
        private bool _showUpdatePage = false;

        private const string LogoPath = "Packages/tv.liv.lck/Editor/SetupWizard/Icons/liv-logo.png";
        private const string StyleSheetPath = "Packages/tv.liv.lck/Editor/SetupWizard/LckSetupWizard.uss";
        private const string ChangelogPath = "Packages/tv.liv.lck/CHANGELOG.md";
        private const string HasBeenShown = "LCKWizardHasBeenShown";

        // Navigation button references for styling updates
        private Dictionary<WizardPage, Button> _navButtons = new Dictionary<WizardPage, Button>();

        // Reference to the root element (for embedded mode)
        private VisualElement _rootElement;

        [InitializeOnLoadMethod]
        public static void StartUpWindow()
        {
            // Defer all settings access to delayCall since InitializeOnLoadMethod
            // runs during assembly loading when asset operations aren't allowed yet
            EditorApplication.delayCall += OnEditorStartup;
        }

        public static void OnEditorStartup()
        {
            try
            {
                // Check if wizard should show - settings access is now safe in delayCall
                var settings = LckSettings.Instance;
                if (settings == null || settings.ShowSetupWizard == false)
                    return;

                // SessionState persists through recompiles/domain reloads
                // but is cleared when the Editor is closed.
                bool alreadyOpenedThisSession = SessionState.GetBool(HasBeenShown, false);
                if (alreadyOpenedThisSession)
                    return;

                // Mark as shown this session to prevent multiple shows
                SessionState.SetBool(HasBeenShown, true);

                // Track version for "What's New" display purposes
                string lastShownVersion = settings.LastShownOverviewVersion;
                string currentVersion = LckSettings.Version;
                if (lastShownVersion != currentVersion)
                {
                    settings.LastShownOverviewVersion = currentVersion;
                    AssetDatabase.SaveAssetIfDirty(settings);
                }

                // Check for updates first, then decide which page to show
                LckUpdatePage.CheckForUpdatesWithCallback(result =>
                {
                    try
                    {
                        if (result.Success)
                        {
                            string latestVersion = result.LatestVersion;

                            // Check if update is available and not dismissed
                            var currentSettings = LckSettings.Instance;
                            if (currentSettings != null &&
                                LckVersionChecker.IsUpdateAvailable(currentVersion, latestVersion) &&
                                currentSettings.DismissedUpdateVersion != latestVersion)
                            {
                                // Reset notification bar dismissal for new version
                                if (currentSettings.DismissedNotificationBarVersion != latestVersion)
                                {
                                    currentSettings.DismissedNotificationBarVersion = "";
                                    EditorUtility.SetDirty(currentSettings);
                                }

                                // Show Update page
                                SettingsService.OpenProjectSettings("Project/LCK/5 Updates");
                                return;
                            }
                        }

                        // No update available or check failed - show Overview
                        SettingsService.OpenProjectSettings("Project/LCK/1 Overview");
                    }
                    catch (System.Exception ex)
                    {
                        Debug.LogError($"[LckSetupWizard] Error handling update check result: {ex.Message}");
                        // Fallback to Overview page
                        SettingsService.OpenProjectSettings("Project/LCK/1 Overview");
                    }
                });
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[LckSetupWizard] Failed during editor startup: {ex.Message}");
            }
        }

        private static void CheckForUpdateAndShowWindow()
        {
            LckVersionChecker.CheckForUpdate(result =>
            {
                try
                {
                    LckSetupWizard window = GetWindow<LckSetupWizard>();
                    window.titleContent = new GUIContent("LCK Setup Wizard");
                    window.minSize = new Vector2(400, 300);
                    window.maxSize = new Vector2(700, 10000);

                    window._latestVersionInfo = result;

                    // Determine if we should show the update page
                    if (result.Success)
                    {
                        string currentVersion = LckSettings.Version;
                        string latestVersion = result.LatestVersion;
                        bool isUpdateAvailable = LckVersionChecker.IsUpdateAvailable(currentVersion, latestVersion);

                        var settings = LckSettings.Instance;
                        string dismissedVersion = settings != null ? settings.DismissedUpdateVersion : "";

                        if (isUpdateAvailable)
                        {
                            // Check if user has dismissed this specific version
                            if (dismissedVersion != latestVersion)
                            {
                                window._showUpdatePage = true;
                                window._currentPage = WizardPage.UpdateAvailable;
                            }
                        }
                    }

                    window.UpdateContentArea();
                }
                catch (System.Exception ex)
                {
                    Debug.LogError($"[LckSetupWizard] Error showing wizard window: {ex.Message}");
                }
            });
        }

        public static void ShowWindow()
        {
            // Also check for updates when opened via menu
            CheckForUpdateAndShowWindow();
        }

        public void CreateGUI()
        {
            _rootElement = rootVisualElement;
            BuildUI(rootVisualElement);
        }

        /// <summary>
        /// Builds the wizard UI into the given root element.
        /// Can be called from CreateGUI (for EditorWindow) or from SettingsProvider.
        /// </summary>
        private void BuildUI(VisualElement root)
        {
            // Initialize audio system selection from project's scripting defines
            InitializeAudioSystemFromDefines();

            // Load the stylesheet
            var styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(StyleSheetPath);
            if (styleSheet != null)
            {
                root.styleSheets.Add(styleSheet);
            }
            else
            {
                Debug.LogError($"[LckSetupWizard] Could not load stylesheet from: {StyleSheetPath}. Make sure the file exists and Unity has imported it.");
            }

            // Apply root styling
            root.AddToClassList("lck-wizard-root");

            // Apply monospace font - try multiple sources
            Font monoFont = null;

            // Try loading from Unity's editor fonts
            string[] fontPaths = new[]
            {
                "Fonts/RobotoMono/RobotoMono-Regular.ttf",
                "Fonts/Consolas/consola.ttf",
                "Fonts/LucidaConsole.ttf"
            };

            foreach (var path in fontPaths)
            {
                monoFont = EditorGUIUtility.Load(path) as Font;
                if (monoFont != null) break;
            }

            // Fallback to OS fonts
            if (monoFont == null)
            {
                string[] osFonts = new[] { "Consolas", "Courier New", "Lucida Console", "Monaco", "monospace" };
                foreach (var fontName in osFonts)
                {
                    try
                    {
                        monoFont = Font.CreateDynamicFontFromOSFont(fontName, 13);
                        if (monoFont != null) break;
                    }
                    catch { }
                }
            }

            if (monoFont != null)
            {
                root.style.unityFont = new StyleFont(monoFont);
            }

            // Main Layout Section (no header - logo is in sidebar like Unreal Engine)
            VisualElement mainLayout = new VisualElement();
            mainLayout.AddToClassList("lck-main-layout");

            // B1. Navigation Column
            mainLayout.Add(DrawNavigationColumn());

            // B2. Content Area
            mainLayout.Add(DrawContentArea());

            root.Add(mainLayout);

            UpdateContentArea();
        }

        // --- UI Toolkit Drawing Methods ---

        private VisualElement DrawNavigationColumn()
        {
            _navButtons.Clear();

            VisualElement navColumn = new VisualElement();
            navColumn.AddToClassList("lck-nav-sidebar");

            // Logo at top of sidebar (like Unreal Engine layout) - clickable link to liv.tv
            VisualElement logoContainer = new VisualElement();
            logoContainer.AddToClassList("lck-sidebar-logo-container");

            Texture2D logo = AssetDatabase.LoadAssetAtPath<Texture2D>(LogoPath);
            if (logo != null)
            {
                Image logoImage = new Image();
                logoImage.image = logo;
                logoImage.AddToClassList("lck-sidebar-logo");
                logoImage.RegisterCallback<ClickEvent>(evt => Application.OpenURL("https://liv.tv"));
                logoContainer.Add(logoImage);
            }
            navColumn.Add(logoContainer);

            // Update Available (special styling)
            AddNavButton(navColumn, "Update Available", WizardPage.UpdateAvailable, "d_Download", isUpdate: true);

            AddNavButton(navColumn, "Welcome", WizardPage.Welcome, "d_Favorite Icon");
            AddNavButton(navColumn, "Link Tracking ID", WizardPage.TrackingID, "d_FilterByLabel");
            AddNavButton(navColumn, "Select Toolkit", WizardPage.InteractionSetup, "d_CustomTool");
            AddNavButton(navColumn, "Interaction Method", WizardPage.UnityXRInteractionMethod, "d_AvatarPivot", isIndented: true);
            AddNavButton(navColumn, "Tablet Setup", WizardPage.UnityXRSetup, "d_RawImage Icon", isIndented: true);
            AddNavButton(navColumn, "Meta XR Setup", WizardPage.MetaXRSetup, "d_ViewToolOrbit", isIndented: true);
            AddNavButton(navColumn, "Custom Setup", WizardPage.CustomSetup, "d_SettingsIcon", isIndented: true);
            AddNavButton(navColumn, "Default Tablet", WizardPage.DefaultTablet, "d_Prefab Icon", isIndented: true);
            AddNavButton(navColumn, "Direct Tablet", WizardPage.DirectTablet, "d_RectTransformBlueprint", isIndented: true);

            // Audio section
            Label audioHeader = new Label("AUDIO");
            audioHeader.AddToClassList("lck-nav-section-header");
            navColumn.Add(audioHeader);

            AddNavButton(navColumn, "Audio Setup", WizardPage.AudioSetup, "d_Profiler.Audio");
            AddNavButton(navColumn, "FMOD Setup", WizardPage.FMODSetup, "d_AudioSource Icon", isIndented: true);
            AddNavButton(navColumn, "Wwise Setup", WizardPage.WwiseSetup, "d_AudioSource Icon", isIndented: true);
            AddNavButton(navColumn, "Custom Audio", WizardPage.OtherAudioSetup, "d_AudioMixerController Icon", isIndented: true);

            // Finalize section
            Label finalizeHeader = new Label("FINALIZE");
            finalizeHeader.AddToClassList("lck-nav-section-header");
            navColumn.Add(finalizeHeader);

            AddNavButton(navColumn, "Project Validation", WizardPage.ProjectValidation, "d_Valid");
            AddNavButton(navColumn, "Additional Help", WizardPage.AdditionalHelp, "d__Help");

            return navColumn;
        }

        private void AddNavButton(VisualElement container, string text, WizardPage page, string iconName = null, bool isUpdate = false, bool isIndented = false)
        {
            Button button = new Button(() =>
            {
                _currentPage = page;
                UpdateContentArea();
            });

            button.name = $"NavButton_{page}";
            button.AddToClassList("lck-nav-button");

            // Add icon if specified
            if (!string.IsNullOrEmpty(iconName))
            {
                var iconContent = EditorGUIUtility.IconContent(iconName);
                if (iconContent != null && iconContent.image != null)
                {
                    Image icon = new Image();
                    icon.image = iconContent.image;
                    icon.AddToClassList("lck-nav-icon");
                    icon.pickingMode = PickingMode.Ignore;
                    button.Add(icon);
                }
            }

            // Add text label
            Label label = new Label(text);
            label.AddToClassList("lck-nav-label");
            label.pickingMode = PickingMode.Ignore;
            button.Add(label);

            if (isUpdate)
            {
                button.AddToClassList("lck-nav-button--update");
            }

            if (isIndented)
            {
                button.AddToClassList("lck-nav-button--indented");
            }

            _navButtons[page] = button;
            container.Add(button);
        }

        private VisualElement DrawContentArea()
        {
            // Create a container for the content area and footer
            VisualElement contentContainer = new VisualElement();
            contentContainer.style.flexGrow = 1;
            contentContainer.style.flexDirection = FlexDirection.Column;

            // Create a ScrollView as the main content container
            ScrollView scrollView = new ScrollView();
            scrollView.AddToClassList("lck-content-scroll");

            // Create the content area inside the scroll view
            _contentArea = new VisualElement();
            _contentArea.AddToClassList("lck-content-area");

            scrollView.Add(_contentArea);
            contentContainer.Add(scrollView);

            // Create persistent footer with "Don't show wizard on startup" toggle
            VisualElement footer = new VisualElement();
            footer.AddToClassList("lck-wizard-footer");
            footer.style.flexDirection = FlexDirection.Row;
            footer.style.justifyContent = Justify.FlexEnd;
            footer.style.alignItems = Align.Center;
            footer.style.paddingRight = 24;
            footer.style.paddingLeft = 24;
            footer.style.height = 44;
            footer.style.flexShrink = 0;
            footer.style.backgroundColor = new Color(0.078f, 0.078f, 0.086f); // #141416

            Toggle dontShowAgainToggle = new Toggle("Don't show this wizard on startup");
            dontShowAgainToggle.AddToClassList("lck-toggle");

            var settings = LckSettings.Instance;
            dontShowAgainToggle.value = settings != null ? !settings.ShowSetupWizard : false;
            dontShowAgainToggle.RegisterCallback<ChangeEvent<bool>>(e =>
            {
                try
                {
                    var currentSettings = LckSettings.Instance;
                    if (currentSettings != null)
                    {
                        currentSettings.ShowSetupWizard = !e.newValue;
                        AssetDatabase.SaveAssetIfDirty(currentSettings);
                    }
                }
                catch (System.Exception ex)
                {
                    Debug.LogError($"[LckSetupWizard] Failed to save settings: {ex.Message}");
                }
            });
            footer.Add(dontShowAgainToggle);

            contentContainer.Add(footer);

            return contentContainer;
        }

        // --- 5. State Update Logic ---

        private void UpdateContentArea()
        {
            _contentArea.Clear();

            switch (_currentPage)
            {
                case WizardPage.UpdateAvailable:
                    DrawUpdateAvailablePage(_contentArea);
                    break;
                case WizardPage.Welcome:
                    DrawWelcomePage(_contentArea);
                    break;
                case WizardPage.TrackingID:
                    DrawTrackingIDPage(_contentArea);
                    DrawPageControls(_contentArea, WizardPage.Welcome, WizardPage.InteractionSetup);
                    break;
                case WizardPage.InteractionSetup:
                    DrawInteractionSetupPage(_contentArea);
                    DrawPageControls(_contentArea, WizardPage.TrackingID, WizardPage.AudioSetup);
                    break;
                case WizardPage.UnityXRInteractionMethod:
                    DrawUnityXRInteractionMethodPage(_contentArea);
                    DrawPageControls(_contentArea, WizardPage.InteractionSetup, null);
                    break;
                case WizardPage.UnityXRSetup:
                    DrawUnityXRSetupPage(_contentArea);
                    DrawPageControls(_contentArea, WizardPage.UnityXRInteractionMethod, WizardPage.AudioSetup);
                    break;
                case WizardPage.MetaXRSetup:
                    DrawMetaXRSetupPage(_contentArea);
                    DrawPageControls(_contentArea, WizardPage.InteractionSetup, WizardPage.AudioSetup);
                    break;
                case WizardPage.CustomSetup:
                    DrawCustomSetupPage(_contentArea);
                    DrawPageControls(_contentArea, WizardPage.InteractionSetup, WizardPage.AudioSetup);
                    break;
                case WizardPage.AudioSetup:
                    DrawAudioSetupPage(_contentArea);
                    DrawPageControls(_contentArea, GetPreviousPageForAudioSetup(), null);
                    break;
                case WizardPage.FMODSetup:
                    DrawFMODSetupPage(_contentArea);
                    DrawPageControls(_contentArea, WizardPage.AudioSetup, WizardPage.ProjectValidation);
                    break;
                case WizardPage.WwiseSetup:
                    DrawWwiseSetupPage(_contentArea);
                    DrawPageControls(_contentArea, WizardPage.AudioSetup, WizardPage.ProjectValidation);
                    break;
                case WizardPage.OtherAudioSetup:
                    DrawOtherAudioSetupPage(_contentArea);
                    DrawPageControls(_contentArea, WizardPage.AudioSetup, WizardPage.ProjectValidation);
                    break;
                case WizardPage.ProjectValidation:
                    DrawValidationPage(_contentArea);
                    DrawPageControls(_contentArea, GetPreviousPageForValidation(), WizardPage.AdditionalHelp);
                    break;
                case WizardPage.AdditionalHelp:
                    DrawAdditionalHelpPage(_contentArea);
                    DrawPageControls(_contentArea, WizardPage.ProjectValidation, null);
                    break;
            }

            UpdateNavigationStyles();
            UpdateNavigationButtonVisibility();
        }

        private void UpdateNavigationStyles()
        {
            foreach (var kvp in _navButtons)
            {
                WizardPage page = kvp.Key;
                Button button = kvp.Value;

                if (button == null) continue;

                // Remove active class from all buttons
                button.RemoveFromClassList("lck-nav-button--active");

                // Add active class to current page button
                if (_currentPage == page)
                {
                    button.AddToClassList("lck-nav-button--active");
                }

                // Update Project Validation button icon color based on validation status
                if (page == WizardPage.ProjectValidation)
                {
                    UpdateValidationNavIconColor(button);
                }
            }
        }

        private void UpdateValidationNavIconColor(Button button)
        {
            // Find the icon element within the button
            var icon = button.Q<Image>(className: "lck-nav-icon");
            if (icon == null) return;

            // Get overall validation status and apply appropriate icon and tint color
            var status = GetOverallValidationStatus();
            Color tintColor;
            string iconName;

            if (status == ValidationSeverity.Required)
            {
                // Red warning icon for required failures
                ColorUtility.TryParseHtmlString("#FF4D4D", out tintColor);
                iconName = "d_console.warnicon.sml";
            }
            else if (status == ValidationSeverity.Suggested)
            {
                // Orange warning icon for suggested failures
                ColorUtility.TryParseHtmlString("#FFB84D", out tintColor);
                iconName = "d_console.warnicon.sml";
            }
            else
            {
                // Green tick for all passed
                ColorUtility.TryParseHtmlString("#D4FF2A", out tintColor);
                iconName = "d_Progress";
            }

            // Update icon image
            var iconContent = EditorGUIUtility.IconContent(iconName);
            if (iconContent != null && iconContent.image != null)
            {
                icon.image = iconContent.image;
            }

            icon.tintColor = tintColor;
        }

        private void UpdateNavigationButtonVisibility()
        {
            var selectedToolkit = GetSelectedToolkit();
            var selectedUnityXRMethod = GetSelectedUnityXRMethod();
            var selectedAudioSystem = GetSelectedAudioSystem();

            // Show UpdateAvailable page only if there's an update and user hasn't dismissed it
            SetButtonVisibility(WizardPage.UpdateAvailable, _showUpdatePage);

            // Show Unity XR interaction method page only if Unity XR is selected
            SetButtonVisibility(WizardPage.UnityXRInteractionMethod,
                selectedToolkit == InteractionToolkitType.UnityXR);

            // Show Unity XR setup page only if Unity XR is selected AND method is chosen
            SetButtonVisibility(WizardPage.UnityXRSetup,
                selectedToolkit == InteractionToolkitType.UnityXR && selectedUnityXRMethod != UnityXRInteractionMethodType.None);

            SetButtonVisibility(WizardPage.MetaXRSetup,
                selectedToolkit == InteractionToolkitType.MetaXR);
            SetButtonVisibility(WizardPage.CustomSetup,
                selectedToolkit == InteractionToolkitType.Custom);

            // Show audio setup pages based on selection
            SetButtonVisibility(WizardPage.FMODSetup,
                selectedAudioSystem == AudioSystemType.FMOD);
            SetButtonVisibility(WizardPage.WwiseSetup,
                selectedAudioSystem == AudioSystemType.Wwise);
            SetButtonVisibility(WizardPage.OtherAudioSetup,
                selectedAudioSystem == AudioSystemType.Other);

            // Hide future pages
            SetButtonVisibility(WizardPage.DefaultTablet, false);
            SetButtonVisibility(WizardPage.DirectTablet, false);
        }

        private void SetButtonVisibility(WizardPage page, bool visible)
        {
            if (_navButtons.TryGetValue(page, out Button button) && button != null)
            {
                button.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            }
        }

        private WizardPage GetPreviousPageForValidation()
        {
            switch (GetSelectedAudioSystem())
            {
                case AudioSystemType.FMOD:
                    return WizardPage.FMODSetup;
                case AudioSystemType.Wwise:
                    return WizardPage.WwiseSetup;
                case AudioSystemType.Other:
                    return WizardPage.OtherAudioSetup;
                default:
                    return WizardPage.AudioSetup;
            }
        }

        private WizardPage GetPreviousPageForAudioSetup()
        {
            switch (GetSelectedToolkit())
            {
                case InteractionToolkitType.UnityXR:
                    return WizardPage.UnityXRSetup;
                case InteractionToolkitType.MetaXR:
                    return WizardPage.MetaXRSetup;
                case InteractionToolkitType.Custom:
                    return WizardPage.CustomSetup;
                default:
                    return WizardPage.InteractionSetup;
            }
        }

        private InteractionToolkitType GetSelectedToolkit()
        {
            return _selectedToolkit;
        }

        private void SetSelectedToolkit(InteractionToolkitType toolkit)
        {
            _selectedToolkit = toolkit;
        }

        private UnityXRInteractionMethodType GetSelectedUnityXRMethod()
        {
            return _selectedUnityXRMethod;
        }

        private void SetSelectedUnityXRMethod(UnityXRInteractionMethodType method)
        {
            _selectedUnityXRMethod = method;
        }

        private AudioSystemType GetSelectedAudioSystem()
        {
            return _selectedAudioSystem;
        }

        private void SetSelectedAudioSystem(AudioSystemType audioSystem)
        {
            _selectedAudioSystem = audioSystem;
        }

        /// <summary>
        /// Detects the currently configured audio system based on scripting define symbols.
        /// </summary>
        private AudioSystemType DetectAudioSystemFromDefines()
        {
            if (ScriptingDefineUtils.HasDefine(LckFMODScriptingDefines.UseFMOD))
            {
                return AudioSystemType.FMOD;
            }
            if (ScriptingDefineUtils.HasDefine(LckWwiseScriptingDefines.UseWwise))
            {
                return AudioSystemType.Wwise;
            }
            if (ScriptingDefineUtils.HasDefine(LckUnityAudioScriptingDefines.NotUnityAudio))
            {
                return AudioSystemType.Other;
            }
            // Default to Unity Audio if no audio-specific defines are set
            return AudioSystemType.DefaultUnityAudio;
        }

        /// <summary>
        /// Initializes the selected audio system based on current project scripting defines.
        /// </summary>
        private void InitializeAudioSystemFromDefines()
        {
            if (_selectedAudioSystem == AudioSystemType.None)
            {
                _selectedAudioSystem = DetectAudioSystemFromDefines();
            }
        }

        // --- 6. Page Navigation Controls ---

        /// <summary>
        /// Creates a horizontal control bar with Previous and Next buttons.
        /// Previous on left, Next on right.
        /// </summary>
        private void DrawPageControls(VisualElement parent, WizardPage? previousPage, WizardPage? nextPage)
        {
            VisualElement controlBar = new VisualElement();
            controlBar.AddToClassList("lck-page-controls");

            if (previousPage.HasValue)
            {
                Button prevButton = new Button(() =>
                {
                    _currentPage = previousPage.Value;
                    UpdateContentArea();
                });
                prevButton.text = "Previous";
                prevButton.AddToClassList("lck-button");
                prevButton.AddToClassList("lck-button--secondary");
                controlBar.Add(prevButton);
            }
            else
            {
                // Add spacer to push Next button to the right
                VisualElement spacer = new VisualElement();
                spacer.style.flexGrow = 1;
                controlBar.Add(spacer);
            }

            if (nextPage.HasValue)
            {
                Button nextButton = new Button(() =>
                {
                    _currentPage = nextPage.Value;
                    UpdateContentArea();
                });
                nextButton.text = "Next";
                nextButton.AddToClassList("lck-button");
                nextButton.AddToClassList("lck-button--accent");
                controlBar.Add(nextButton);
            }

            parent.Add(controlBar);
        }

        private void AddPageTitle(VisualElement parent, string title, bool centered = false)
        {
            Label titleLabel = new Label(title);
            titleLabel.AddToClassList("lck-page-title");
            if (centered)
            {
                titleLabel.AddToClassList("lck-page-title--center");
            }
            parent.Add(titleLabel);
        }

        private void AddPageSubtitle(VisualElement parent, string subtitle, bool centered = false)
        {
            Label subtitleLabel = new Label(subtitle);
            subtitleLabel.AddToClassList("lck-page-subtitle");
            if (centered)
            {
                subtitleLabel.AddToClassList("lck-page-subtitle--center");
            }
            parent.Add(subtitleLabel);
        }

        private void DrawWelcomePage(VisualElement parent)
        {
            // Hero section
            VisualElement hero = new VisualElement();
            hero.AddToClassList("lck-hero");

            Label heroTitle = new Label("Welcome to LIV Camera Kit");
            heroTitle.AddToClassList("lck-hero-title");
            hero.Add(heroTitle);

            Label heroDescription = new Label(
                "This setup wizard will help you get started and configure your project to use LCK for recording and streaming."
            );
            heroDescription.AddToClassList("lck-hero-description");
            hero.Add(heroDescription);

            // Start button
            Button startButton = new Button(() =>
            {
                _currentPage = WizardPage.TrackingID;
                UpdateContentArea();
            });
            startButton.text = "Get Started";
            startButton.AddToClassList("lck-button");
            startButton.AddToClassList("lck-button--accent");
            startButton.AddToClassList("lck-button--large");
            hero.Add(startButton);

            parent.Add(hero);

            // Changelog section - only show the latest version
            var changelogEntries = ParseChangelog(1);
            if (changelogEntries.Count > 0)
            {
                var latestEntry = changelogEntries[0];

                // Page title styled like other pages
                AddPageTitle(parent, $"What's New in {latestEntry.Version}");

                VisualElement versionCard = new VisualElement();
                versionCard.AddToClassList("lck-card");
                LckSettingsPageBase.ApplyCardFallbackStylesStatic(versionCard);

                if (latestEntry.Added.Count > 0)
                {
                    VisualElement addedHeaderRow = new VisualElement();
                    addedHeaderRow.style.flexDirection = FlexDirection.Row;
                    addedHeaderRow.style.alignItems = Align.Center;
                    addedHeaderRow.style.marginBottom = 5;

                    var addedIconContent = EditorGUIUtility.IconContent("d_Toolbar Plus");
                    if (addedIconContent != null && addedIconContent.image != null)
                    {
                        Image addedIcon = new Image();
                        addedIcon.image = addedIconContent.image;
                        addedIcon.style.width = 16;
                        addedIcon.style.height = 16;
                        addedIcon.style.marginRight = 6;
                        addedIcon.tintColor = new Color(0.831f, 1f, 0.165f); // LIV Green #D4FF2A
                        addedHeaderRow.Add(addedIcon);
                    }

                    Label addedHeader = new Label("Added");
                    addedHeader.style.fontSize = 14;
                    addedHeader.style.unityFontStyleAndWeight = FontStyle.Bold;
                    addedHeader.style.color = Color.white;
                    addedHeaderRow.Add(addedHeader);

                    versionCard.Add(addedHeaderRow);

                    foreach (var item in latestEntry.Added)
                    {
                        Label itemLabel = new Label($"• {item}");
                        itemLabel.AddToClassList("lck-body-text");
                        itemLabel.style.marginBottom = 2;
                        versionCard.Add(itemLabel);
                    }
                }

                if (latestEntry.Changed.Count > 0)
                {
                    VisualElement changedHeaderRow = new VisualElement();
                    changedHeaderRow.style.flexDirection = FlexDirection.Row;
                    changedHeaderRow.style.alignItems = Align.Center;
                    changedHeaderRow.style.marginTop = 8;
                    changedHeaderRow.style.marginBottom = 5;

                    var changedIconContent = EditorGUIUtility.IconContent("d_refresh");
                    if (changedIconContent != null && changedIconContent.image != null)
                    {
                        Image changedIcon = new Image();
                        changedIcon.image = changedIconContent.image;
                        changedIcon.style.width = 16;
                        changedIcon.style.height = 16;
                        changedIcon.style.marginRight = 6;
                        changedIcon.tintColor = new Color(1f, 0.72f, 0.3f); // Orange
                        changedHeaderRow.Add(changedIcon);
                    }

                    Label changedHeader = new Label("Changed");
                    changedHeader.style.fontSize = 14;
                    changedHeader.style.unityFontStyleAndWeight = FontStyle.Bold;
                    changedHeader.style.color = Color.white;
                    changedHeaderRow.Add(changedHeader);

                    versionCard.Add(changedHeaderRow);

                    foreach (var item in latestEntry.Changed)
                    {
                        Label itemLabel = new Label($"• {item}");
                        itemLabel.AddToClassList("lck-body-text");
                        itemLabel.style.marginBottom = 2;
                        versionCard.Add(itemLabel);
                    }
                }

                if (latestEntry.Fixed.Count > 0)
                {
                    VisualElement fixedHeaderRow = new VisualElement();
                    fixedHeaderRow.style.flexDirection = FlexDirection.Row;
                    fixedHeaderRow.style.alignItems = Align.Center;
                    fixedHeaderRow.style.marginTop = 8;
                    fixedHeaderRow.style.marginBottom = 5;

                    var fixedIconContent = EditorGUIUtility.IconContent("d_FilterSelectedOnly");
                    if (fixedIconContent != null && fixedIconContent.image != null)
                    {
                        Image fixedIcon = new Image();
                        fixedIcon.image = fixedIconContent.image;
                        fixedIcon.style.width = 16;
                        fixedIcon.style.height = 16;
                        fixedIcon.style.marginRight = 6;
                        fixedIcon.tintColor = new Color(0.42f, 0.65f, 1f); // Blue
                        fixedHeaderRow.Add(fixedIcon);
                    }

                    Label fixedHeader = new Label("Fixed");
                    fixedHeader.style.fontSize = 14;
                    fixedHeader.style.unityFontStyleAndWeight = FontStyle.Bold;
                    fixedHeader.style.color = Color.white;
                    fixedHeaderRow.Add(fixedHeader);

                    versionCard.Add(fixedHeaderRow);

                    foreach (var item in latestEntry.Fixed)
                    {
                        Label itemLabel = new Label($"• {item}");
                        itemLabel.AddToClassList("lck-body-text");
                        itemLabel.style.marginBottom = 2;
                        versionCard.Add(itemLabel);
                    }
                }

                parent.Add(versionCard);
            }
        }

        private class ChangelogEntry
        {
            public string Version;
            public string Date;
            public List<string> Added = new List<string>();
            public List<string> Changed = new List<string>();
            public List<string> Fixed = new List<string>();
        }

        private List<ChangelogEntry> ParseChangelog(int maxEntries = 2)
        {
            var entries = new List<ChangelogEntry>();

            try
            {
                var changelogAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(ChangelogPath);
                if (changelogAsset == null) return entries;

                string content = changelogAsset.text;
                string[] lines = content.Split('\n');

                ChangelogEntry currentEntry = null;
                string currentSection = null;

                foreach (string rawLine in lines)
                {
                    string line = rawLine.Trim();

                    // Version header: ## [1.4.3] - 2025-12-16
                    var versionMatch = Regex.Match(line, @"^##\s*\[(.+?)\]\s*-?\s*(.*)$");
                    if (versionMatch.Success)
                    {
                        // Skip PENDING versions
                        if (versionMatch.Groups[2].Value.Trim().ToUpper() == "PENDING")
                        {
                            currentEntry = null;
                            continue;
                        }

                        if (entries.Count >= maxEntries)
                            break;

                        currentEntry = new ChangelogEntry
                        {
                            Version = versionMatch.Groups[1].Value.Trim(),
                            Date = versionMatch.Groups[2].Value.Trim()
                        };
                        entries.Add(currentEntry);
                        currentSection = null;
                        continue;
                    }

                    if (currentEntry == null) continue;

                    // Section header: ### Added, ### Changed, ### Fixed
                    if (line.StartsWith("### "))
                    {
                        currentSection = line.Substring(4).Trim().ToLower();
                        continue;
                    }

                    // List item: - Item text
                    if (line.StartsWith("- ") && currentSection != null)
                    {
                        string item = line.Substring(2).Trim();
                        if (string.IsNullOrEmpty(item)) continue;

                        switch (currentSection)
                        {
                            case "added":
                                currentEntry.Added.Add(item);
                                break;
                            case "changed":
                                currentEntry.Changed.Add(item);
                                break;
                            case "fixed":
                                currentEntry.Fixed.Add(item);
                                break;
                        }
                    }
                }
            }
            catch (Exception)
            {
                // Silently handle parsing errors
            }

            return entries;
        }

        private void DrawTrackingIDPage(VisualElement parent)
        {
            AddPageTitle(parent, "Link Tracking ID");

            Label description = new Label(
                "LCK uses a Tracking ID to help you collect analytics data such as" +
                " number of unique devices with captures and total hours recorded.\n\n" +
                "A Tracking ID must be setup in order to record and stream with LCK."
            );
            description.AddToClassList("lck-body-text");
            parent.Add(description);

            Button openDashboardButton = new Button(() =>
            {
                Application.OpenURL("https://dashboard.liv.tv/dev");
            });
            openDashboardButton.text = "Open LCK Dashboard";
            openDashboardButton.AddToClassList("lck-button");
            openDashboardButton.AddToClassList("lck-button--primary");
            openDashboardButton.AddToClassList("lck-button--small");
            parent.Add(openDashboardButton);

            Label dashboardSteps = new Label(
                "To find your unique Tracking ID:\n" +
                "1. Open LCK Dashboard and select 'Go to LCK Portal'\n" +
                "2. Select the 'Games' tab\n" +
                "3. Select your project\n" +
                "4. Agree to our terms\n" +
                "5. Your Tracking ID for copy pasting will be visible in green text"
            );
            dashboardSteps.AddToClassList("lck-body-text");
            dashboardSteps.style.marginTop = 15;
            parent.Add(dashboardSteps);

            // Tracking ID input label
            Label trackingIDLabel = new Label("Paste your Tracking ID Here");
            trackingIDLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            trackingIDLabel.style.marginBottom = 5;
            parent.Add(trackingIDLabel);

            // Tracking ID input field on new line
            TextField trackingIDField = new TextField();
            var trackingSettings = LckSettings.Instance;
            trackingIDField.value = trackingSettings != null ? trackingSettings.TrackingId : "";
            if (trackingIDField.value != string.Empty)
            {
                VisualElement textInputChild = trackingIDField.Q(name: "unity-text-input");
                ColorUtility.TryParseHtmlString("#D4FF2A", out Color livGreen);
                textInputChild.style.borderBottomColor = livGreen;
            }
            trackingIDField.RegisterCallback<ChangeEvent<string>>(e =>
            {
                try
                {
                    VisualElement textInputChild = trackingIDField.Q(name: "unity-text-input");

                    if (trackingIDField.text != string.Empty)
                    {
                        ColorUtility.TryParseHtmlString("#D4FF2A", out Color livGreen);
                        textInputChild.style.borderBottomColor = livGreen;
                    }
                    else
                    {
                        textInputChild.style.borderBottomColor = Color.red;
                    }

                    var currentSettings = LckSettings.Instance;
                    if (currentSettings != null)
                    {
                        currentSettings.TrackingId = trackingIDField.text;
                    }
                }
                catch (System.Exception ex)
                {
                    Debug.LogError($"[LckSetupWizard] Failed to save tracking ID: {ex.Message}");
                }
            });
            parent.Add(trackingIDField);
        }

        private void DrawInteractionSetupPage(VisualElement parent)
        {
            AddPageTitle(parent, "Select Interaction Toolkit");

            Label description = new Label(
                "Choose which XR interaction system your project uses. " +
                "This will guide you through the appropriate tablet setup."
            );
            description.AddToClassList("lck-body-text");
            parent.Add(description);

            VisualElement optionsContainer = new VisualElement();
            optionsContainer.style.flexDirection = FlexDirection.Column;
            optionsContainer.style.marginTop = 10;

            // Unity XR Option
            optionsContainer.Add(CreateToolkitOptionRow(
                "Unity XR Interaction Toolkit",
                "Standard Unity XR framework, can be used with ray based Near-Far interactors, close up with XR Poke interactors or directly using collider based interactions.",
                InteractionToolkitType.UnityXR,
                WizardPage.UnityXRInteractionMethod,
                "Packages/tv.liv.lck/Runtime/Art/Textures/DirectTabletClick.jpg"
            ));

            // Meta XR Option
            optionsContainer.Add(CreateToolkitOptionRow(
                "Meta XR Interaction SDK",
                "Meta's interaction SDK (formerly Oculus Interaction). Can be used with hand tracking, ray based interaction or direct interaction components.",
                InteractionToolkitType.MetaXR,
                WizardPage.MetaXRSetup,
                "Packages/tv.liv.lck/Runtime/Art/Textures/MetaTabletClick.png"
            ));

            // Custom Option
            optionsContainer.Add(CreateToolkitOptionRow(
                "Custom Implementation",
                "No specific toolkit, or custom interaction system. You'll set up tablet grabbing and button interactions manually.",
                InteractionToolkitType.Custom,
                WizardPage.CustomSetup,
                "Packages/tv.liv.lck/Runtime/Art/Textures/CustomTablet.png"
            ));

            parent.Add(optionsContainer);
        }

        private void DrawUnityXRInteractionMethodPage(VisualElement parent)
        {
            AddPageTitle(parent, "Unity XR Interaction Method");

            Label description = new Label(
                "Choose which interaction method your project uses."
            );
            description.AddToClassList("lck-body-text");
            parent.Add(description);

            VisualElement optionsContainer = new VisualElement();
            optionsContainer.style.flexDirection = FlexDirection.Column;
            optionsContainer.style.marginTop = 10;

            // Ray-based Option
            optionsContainer.Add(CreateUnityXRMethodOptionRow(
                "Ray-based Interactions",
                "Uses Ray based XR Near-Far interactors and XR Poke Interactors. Users can point at the tablet from a distance or poke buttons directly.",
                UnityXRInteractionMethodType.RayBased,
                "Packages/tv.liv.lck/Runtime/Art/Textures/RayTabletClick.png"
            ));

            // Direct Touch Option
            optionsContainer.Add(CreateUnityXRMethodOptionRow(
                "Direct Touch with Colliders",
                "Uses colliders on the player hands and on the tablet buttons for close up touch-based interactions.",
                UnityXRInteractionMethodType.DirectTouch,
                "Packages/tv.liv.lck/Runtime/Art/Textures/DirectTabletClick.jpg"
            ));

            parent.Add(optionsContainer);
        }

        private void DrawUnityXRSetupPage(VisualElement parent)
        {
            AddPageTitle(parent, "Unity XR Interaction Toolkit Setup");

            var selectedMethod = GetSelectedUnityXRMethod();

            if (selectedMethod == UnityXRInteractionMethodType.RayBased)
            {
                Label description = new Label(
                    "Configure your tablet for ray-based interactions."
                );
                description.AddToClassList("lck-body-text");
                parent.Add(description);

                // LCK Tablet (Ray-based)
                VisualElement rayTabletContainer = new VisualElement();
                rayTabletContainer.style.marginBottom = 20;

                Label rayTabletPathLabel = new Label(
                    "The LCK Tablet prefab is located at:\n" +
                    "Packages/tv.liv.lck/Runtime/Prefabs/Tablets/XR Interaction Toolkit Tablets/"
                );
                rayTabletPathLabel.AddToClassList("lck-body-text");
                rayTabletContainer.Add(rayTabletPathLabel);

                Button selectRayTabletButton = new Button(() =>
                {
                    SelectPrefabInProject("Packages/tv.liv.lck/Runtime/Prefabs/Tablets/XR Interaction Toolkit Tablets/LCK Tablet.prefab");
                });
                selectRayTabletButton.text = "Show in Project";
                selectRayTabletButton.AddToClassList("lck-button");
                selectRayTabletButton.AddToClassList("lck-button--primary");
                selectRayTabletButton.AddToClassList("lck-button--small");
                selectRayTabletButton.style.marginBottom = 10;
                rayTabletContainer.Add(selectRayTabletButton);

                Label variantExplanation = new Label(
                    "We suggest using a Prefab Variant of this tablet as the one you want to use throughout your project. " +
                    "Using a variant allows you to make any changes you want to the tablet while still keeping an original default copy of the prefab in the LCK package. " +
                    "Also when updating the LCK package you will be able to keep any changes made on your prefab variant."
                );
                variantExplanation.AddToClassList("lck-body-text");
                rayTabletContainer.Add(variantExplanation);

                Button createRayTabletVariantButton = new Button(() =>
                {
                    CreatePrefabVariant("Packages/tv.liv.lck/Runtime/Prefabs/Tablets/XR Interaction Toolkit Tablets/LCK Tablet.prefab", "Assets/LCK Tablet Variant.prefab");
                });
                createRayTabletVariantButton.text = "Create Prefab Variant";
                createRayTabletVariantButton.AddToClassList("lck-button");
                createRayTabletVariantButton.AddToClassList("lck-button--accent");
                createRayTabletVariantButton.AddToClassList("lck-button--small");
                createRayTabletVariantButton.style.marginBottom = 10;
                rayTabletContainer.Add(createRayTabletVariantButton);

                parent.Add(rayTabletContainer);
            }
            else if (selectedMethod == UnityXRInteractionMethodType.DirectTouch)
            {
                Label description = new Label(
                    "Configure your tablet for direct touch interactions."
                );
                description.AddToClassList("lck-body-text");
                parent.Add(description);

                // LCK Tablet For Direct (Direct touch)
                VisualElement directTabletContainer = new VisualElement();
                directTabletContainer.style.marginBottom = 20;

                Label directTabletPathLabel = new Label(
                    "The LCK Tablet For Direct prefab is located at:\n" +
                    "Packages/tv.liv.lck/Runtime/Prefabs/Tablets/XR Interaction Toolkit Tablets/"
                );
                directTabletPathLabel.AddToClassList("lck-body-text");
                directTabletContainer.Add(directTabletPathLabel);

                Button selectDirectTabletButton = new Button(() =>
                {
                    SelectPrefabInProject("Packages/tv.liv.lck/Runtime/Prefabs/Tablets/XR Interaction Toolkit Tablets/LCK Tablet For Direct.prefab");
                });
                selectDirectTabletButton.text = "Show in Project";
                selectDirectTabletButton.AddToClassList("lck-button");
                selectDirectTabletButton.AddToClassList("lck-button--primary");
                selectDirectTabletButton.AddToClassList("lck-button--small");
                selectDirectTabletButton.style.marginBottom = 10;
                directTabletContainer.Add(selectDirectTabletButton);

                Label variantExplanation = new Label(
                    "We suggest using a Prefab Variant of this tablet as the one you want to use throughout your project. " +
                    "Using a variant allows you to make any changes you want to the tablet while still keeping an original default copy of the prefab in the LCK package. " +
                    "Also when updating the LCK package you will be able to keep any changes made on your prefab variant."
                );
                variantExplanation.AddToClassList("lck-body-text");
                directTabletContainer.Add(variantExplanation);

                Button createDirectTabletVariantButton = new Button(() =>
                {
                    CreatePrefabVariant("Packages/tv.liv.lck/Runtime/Prefabs/Tablets/XR Interaction Toolkit Tablets/LCK Tablet For Direct.prefab", "Assets/LCK Tablet For Direct Variant.prefab");
                });
                createDirectTabletVariantButton.text = "Create Prefab Variant";
                createDirectTabletVariantButton.AddToClassList("lck-button");
                createDirectTabletVariantButton.AddToClassList("lck-button--accent");
                createDirectTabletVariantButton.AddToClassList("lck-button--small");
                createDirectTabletVariantButton.style.marginBottom = 10;
                directTabletContainer.Add(createDirectTabletVariantButton);

                parent.Add(directTabletContainer);
            }
        }

        private void DrawMetaXRSetupPage(VisualElement parent)
        {
            AddPageTitle(parent, "Meta XR Interaction SDK Setup");

            Label description = new Label(
                "Configure your tablet for Meta XR Interaction SDK."
            );
            description.AddToClassList("lck-body-text");
            parent.Add(description);

            // LCK Meta Tablet
            VisualElement metaTabletContainer = new VisualElement();
            metaTabletContainer.style.marginBottom = 20;

            Label metaTabletPathLabel = new Label(
                "The LCK Meta Tablet prefab is located at:\n" +
                "Packages/tv.liv.lck/Runtime/Prefabs/Tablets/Meta XR Interaction SDK Tablets/"
            );
            metaTabletPathLabel.AddToClassList("lck-body-text");
            metaTabletContainer.Add(metaTabletPathLabel);

            Button selectMetaTabletButton = new Button(() =>
            {
                SelectPrefabInProject("Packages/tv.liv.lck/Runtime/Prefabs/Tablets/Meta XR Interaction SDK Tablets/LCK Meta Tablet.prefab");
            });
            selectMetaTabletButton.text = "Show in Project";
            selectMetaTabletButton.AddToClassList("lck-button");
            selectMetaTabletButton.AddToClassList("lck-button--primary");
            selectMetaTabletButton.AddToClassList("lck-button--small");
            selectMetaTabletButton.style.marginBottom = 10;
            metaTabletContainer.Add(selectMetaTabletButton);

            Label variantExplanation = new Label(
                "We suggest using a Prefab Variant of this tablet as the one you want to use throughout your project. " +
                "Using a variant allows you to make any changes you want to the tablet while still keeping an original default copy of the prefab in the LCK package. " +
                "Also when updating the LCK package you will be able to keep any changes made on your prefab variant."
            );
            variantExplanation.AddToClassList("lck-body-text");
            metaTabletContainer.Add(variantExplanation);

            Button createMetaTabletVariantButton = new Button(() =>
            {
                CreatePrefabVariant("Packages/tv.liv.lck/Runtime/Prefabs/Tablets/Meta XR Interaction SDK Tablets/LCK Meta Tablet.prefab", "Assets/LCK Meta Tablet Variant.prefab");
            });
            createMetaTabletVariantButton.text = "Create Prefab Variant";
            createMetaTabletVariantButton.AddToClassList("lck-button");
            createMetaTabletVariantButton.AddToClassList("lck-button--accent");
            createMetaTabletVariantButton.AddToClassList("lck-button--small");
            createMetaTabletVariantButton.style.marginBottom = 10;
            metaTabletContainer.Add(createMetaTabletVariantButton);

            parent.Add(metaTabletContainer);
        }

        private void DrawCustomSetupPage(VisualElement parent)
        {
            AddPageTitle(parent, "Custom Tablet Setup");

            Label description = new Label(
                "Configure your tablet for a custom interaction system."
            );
            description.AddToClassList("lck-body-text");
            parent.Add(description);

            // Base LCK Tablet
            VisualElement baseTabletContainer = new VisualElement();
            baseTabletContainer.style.marginBottom = 20;

            Label baseTabletPathLabel = new Label(
                "The Base LCK Tablet prefab is located at:\n" +
                "Packages/tv.liv.lck/Runtime/Prefabs/Tablets/"
            );
            baseTabletPathLabel.AddToClassList("lck-body-text");
            baseTabletContainer.Add(baseTabletPathLabel);

            Button selectBaseTabletButton = new Button(() =>
            {
                SelectPrefabInProject("Packages/tv.liv.lck/Runtime/Prefabs/Tablets/Base LCK Tablet.prefab");
            });
            selectBaseTabletButton.text = "Show in Project";
            selectBaseTabletButton.AddToClassList("lck-button");
            selectBaseTabletButton.AddToClassList("lck-button--primary");
            selectBaseTabletButton.AddToClassList("lck-button--small");
            selectBaseTabletButton.style.marginBottom = 10;
            baseTabletContainer.Add(selectBaseTabletButton);

            Label variantExplanation = new Label(
                "We suggest using a Prefab Variant of this tablet as the one you want to use throughout your project. " +
                "Using a variant allows you to make any changes you want to the tablet while still keeping an original default copy of the prefab in the LCK package. " +
                "Also when updating the LCK package you will be able to keep any changes made on your prefab variant."
            );
            variantExplanation.AddToClassList("lck-body-text");
            baseTabletContainer.Add(variantExplanation);

            Button createBaseTabletVariantButton = new Button(() =>
            {
                CreatePrefabVariant("Packages/tv.liv.lck/Runtime/Prefabs/Tablets/Base LCK Tablet.prefab", "Assets/Base LCK Tablet Variant.prefab");
            });
            createBaseTabletVariantButton.text = "Create Prefab Variant";
            createBaseTabletVariantButton.AddToClassList("lck-button");
            createBaseTabletVariantButton.AddToClassList("lck-button--accent");
            createBaseTabletVariantButton.AddToClassList("lck-button--small");
            createBaseTabletVariantButton.style.marginBottom = 0;
            baseTabletContainer.Add(createBaseTabletVariantButton);

            parent.Add(baseTabletContainer);

            Label setupSteps = new Label(
                "You may need to:\n" +
                "• Add your own interaction components to the tablet buttons\n" +
                "• Add a script for grabbing the tablet handles\n" +
                "• Implement tablet spawning in your scene"
            );
            setupSteps.AddToClassList("lck-body-text");
            parent.Add(setupSteps);
        }

        private void DrawAudioSetupPage(VisualElement parent)
        {
            AddPageTitle(parent, "Select Audio System");

            Label description = new Label(
                "Choose which audio system your project uses. " +
                "This will help configure LCK to work with your audio setup."
            );
            description.AddToClassList("lck-body-text");
            parent.Add(description);

            VisualElement optionsContainer = new VisualElement();
            optionsContainer.style.flexDirection = FlexDirection.Column;
            optionsContainer.style.marginTop = 10;

            // Default Unity Audio Option - Special handling to skip setup page
            optionsContainer.Add(CreateDefaultUnityAudioOptionRow(
                "Default Unity Audio",
                "Use the standard Unity audio system using Unity's built-in audio engine."
            ));

            // FMOD Option
            optionsContainer.Add(CreateAudioOptionRow(
                "FMOD",
                "FMOD audio middleware for advanced audio features and real-time mixing.",
                AudioSystemType.FMOD,
                WizardPage.FMODSetup
            ));

            // Wwise Option
            optionsContainer.Add(CreateAudioOptionRow(
                "Wwise",
                "Audiokinetic Wwise audio middleware for interactive audio and sound design.",
                AudioSystemType.Wwise,
                WizardPage.WwiseSetup
            ));

            // Custom Audio Option
            optionsContainer.Add(CreateAudioOptionRow(
                "Custom Audio Setup",
                "Custom audio system or other third-party audio solution.",
                AudioSystemType.Other,
                WizardPage.OtherAudioSetup
            ));

            parent.Add(optionsContainer);

            // Add spacing
            VisualElement spacer = new VisualElement();
            spacer.style.height = 5;
            parent.Add(spacer);

            // Reset to default section
            Label resetDescription = new Label(
                "If you need to reset the audio configuration back to default Unity settings, " +
                "you can use the button below. This will restore LCK's audio capture to use Unity's built-in audio system."
            );
            resetDescription.AddToClassList("lck-body-text");
            parent.Add(resetDescription);

            Button resetButton = new Button(() =>
            {
                LckAudioMenu.ResetAudioConfigurationToDefaults();
            });
            resetButton.text = "Reset Audio to Default";
            resetButton.AddToClassList("lck-button");
            resetButton.AddToClassList("lck-button--accent");
            resetButton.AddToClassList("lck-button--small");
            parent.Add(resetButton);
        }

        private void DrawFMODSetupPage(VisualElement parent)
        {
            AddPageTitle(parent, "FMOD Audio Setup");

            string fmodVersionString = GetFMODVersionString();
            bool fmodVersionDetected = fmodVersionString != "Version Unknown";

            if (fmodVersionDetected)
            {
                // FMOD version detected - show version info and appropriate button
                bool isFMOD203OrAbove = IsFMODVersion203OrAbove(fmodVersionString);

                Label versionLabel = new Label($"FMOD Detected Version: {fmodVersionString}");
                versionLabel.style.fontSize = 14;
                versionLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
                versionLabel.style.color = new Color(0.831f, 1f, 0.165f);
                versionLabel.style.marginTop = 10;
                versionLabel.style.marginBottom = 10;
                parent.Add(versionLabel);

                string textUnderDetectedVersion = "LCK uses a scripting define for FMOD support, which can be found in your project settings. " +
                "Press the button below to automatically add the necessary scripting define.";

                Label compatLabel = new Label(textUnderDetectedVersion);
                compatLabel.AddToClassList("lck-body-text");
                parent.Add(compatLabel);

                // Single configure button based on detected version
                Button configureButton = new Button(() =>
                {
                    if (isFMOD203OrAbove)
                    {
                        LckFMODAudioMenu.ConfigureForFMOD2_03AndAbove();
                    }
                    else
                    {
                        LckFMODAudioMenu.ConfigureForFMODPre2_03();
                    }
                });
                configureButton.text = isFMOD203OrAbove
                    ? "Configure for FMOD 2.03+"
                    : "Configure for FMOD (Pre-2.03)";
                configureButton.AddToClassList("lck-button");
                configureButton.AddToClassList("lck-button--accent");
                configureButton.style.alignSelf = Align.FlexStart;
                configureButton.style.marginBottom = 15;
                configureButton.style.marginLeft = 0;
                parent.Add(configureButton);
            }
            else
            {
                // FMOD version not detected - show both configuration options
                Label notDetectedLabel = new Label("FMOD Version Not Detected");
                notDetectedLabel.style.fontSize = 14;
                notDetectedLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
                notDetectedLabel.style.color = new Color(0.9f, 0.6f, 0.2f);
                notDetectedLabel.style.marginTop = 10;
                notDetectedLabel.style.marginBottom = 10;
                parent.Add(notDetectedLabel);

                Label description = new Label(
                    "Unable to detect FMOD version automatically. LCK uses a scripting define for FMOD support, which can be found in your project settings. " +
                "Please select your current FMOD version below."
                );
                description.AddToClassList("lck-body-text");
                parent.Add(description);

                // FMOD 2.03+ Section
                Label fmod203Label = new Label("For FMOD 2.03 and Above:");
                fmod203Label.AddToClassList("lck-body-text");
                fmod203Label.style.unityFontStyleAndWeight = FontStyle.Bold;
                parent.Add(fmod203Label);

                Label fmod203Description = new Label(
                    "Use this option if you have FMOD version 2.03.00 or newer."
                );
                fmod203Description.AddToClassList("lck-body-text");
                parent.Add(fmod203Description);

                Button configure203Button = new Button(() =>
                {
                    LckFMODAudioMenu.ConfigureForFMOD2_03AndAbove();
                });
                configure203Button.text = "Configure for FMOD 2.03+";
                configure203Button.AddToClassList("lck-button");
                configure203Button.AddToClassList("lck-button--accent");
                configure203Button.style.alignSelf = Align.FlexStart;
                configure203Button.style.marginBottom = 25;
                configure203Button.style.marginLeft = 0;
                parent.Add(configure203Button);

                // FMOD Pre-2.03 Section
                Label fmodPre203Label = new Label("For FMOD Pre-2.03:");
                fmodPre203Label.AddToClassList("lck-body-text");
                fmodPre203Label.style.unityFontStyleAndWeight = FontStyle.Bold;
                parent.Add(fmodPre203Label);

                Label fmodPre203Description = new Label(
                    "Use this option if you have FMOD version 2.02.xx or older."
                );
                fmodPre203Description.AddToClassList("lck-body-text");
                parent.Add(fmodPre203Description);

                Button configurePre203Button = new Button(() =>
                {
                    LckFMODAudioMenu.ConfigureForFMODPre2_03();
                });
                configurePre203Button.text = "Configure for FMOD (Pre-2.03)";
                configurePre203Button.AddToClassList("lck-button");
                configurePre203Button.AddToClassList("lck-button--accent");
                configurePre203Button.style.alignSelf = Align.FlexStart;
                configurePre203Button.style.marginBottom = 15;
                configurePre203Button.style.marginLeft = 0;
                parent.Add(configurePre203Button);
            }

            Label lckRequires = new Label(
                "LCK also requires an LckAudioMarker component to be on an active Game Object in the scene. " +
                "We suggest adding the component to the same Game Object that has the FMOD Studio Listener component."
            );
            lckRequires.AddToClassList("lck-body-text");
            parent.Add(lckRequires);

            // LckAudioMarker detection UI
            VisualElement audioMarkerContainer = new VisualElement();
            audioMarkerContainer.style.flexDirection = FlexDirection.Row;
            audioMarkerContainer.style.alignItems = Align.Center;
            audioMarkerContainer.style.marginBottom = 15;

            Label audioMarkerStatusLabel = new Label();
            audioMarkerStatusLabel.style.marginRight = 10;
            audioMarkerContainer.Add(audioMarkerStatusLabel);

            Button searchAgainButton = new Button();
            searchAgainButton.text = "Search Again";
            searchAgainButton.AddToClassList("lck-button");
            searchAgainButton.AddToClassList("lck-button--secondary");
            searchAgainButton.AddToClassList("lck-button--small");
            searchAgainButton.style.alignSelf = Align.FlexStart;
            audioMarkerContainer.Add(searchAgainButton);

            Action updateAudioMarkerStatus = () =>
            {
#if UNITY_2022_1_OR_NEWER
                bool found = UnityEngine.Object.FindAnyObjectByType<LckAudioMarker>(FindObjectsInactive.Include) != null;
#else
                bool found = UnityEngine.Object.FindObjectOfType<LckAudioMarker>(true) != null;
#endif
                if (found)
                {
                    audioMarkerStatusLabel.text = "LckAudioMarker found in scene";
                    audioMarkerStatusLabel.style.color = new Color(0.831f, 1f, 0.165f);
                    searchAgainButton.style.display = DisplayStyle.None;
                }
                else
                {
                    audioMarkerStatusLabel.text = "LckAudioMarker not found in current scene";
                    audioMarkerStatusLabel.style.color = new Color(0.9f, 0.6f, 0.2f);
                    searchAgainButton.style.display = DisplayStyle.Flex;
                }
            };

            searchAgainButton.clicked += updateAudioMarkerStatus;
            updateAudioMarkerStatus();

            parent.Add(audioMarkerContainer);

            // Combine with Unity Audio section
            Label combineAudioLabel = new Label(
                "LCK can also combine Unity audio capture with FMOD audio, if your project uses both audio solutions select the toggle below."
            );
            combineAudioLabel.AddToClassList("lck-body-text");
            parent.Add(combineAudioLabel);

            var currentOptions = LckAudioConfigurationUtils.FMODConfigurer.GetCurrentOptions();
            Toggle combineAudioToggle = new Toggle("Combine FMOD With Unity Audio");
            combineAudioToggle.value = currentOptions.CombineWithUnityAudio;
            combineAudioToggle.style.marginBottom = 15;
            combineAudioToggle.style.marginLeft = 0;
            combineAudioToggle.RegisterValueChangedCallback(evt =>
            {
                var options = LckAudioConfigurationUtils.FMODConfigurer.GetCurrentOptions();
                options.CombineWithUnityAudio = evt.newValue;
                options.ShouldCapture = true;
                LckAudioConfigurationUtils.FMODConfigurer.Configure(options, shouldValidate: false);
            });
            parent.Add(combineAudioToggle);
        }

        private bool IsFMODVersion203OrAbove(string versionString)
        {
            if (versionString == "Version Unknown") return false;

            try
            {
                // Parse version string format: "X.YY.ZZ"
                var parts = versionString.Split('.');
                if (parts.Length != 3) return false;

                if (!int.TryParse(parts[0], out int major)) return false;
                if (!int.TryParse(parts[1], out int minor)) return false;

                // Check if version is 2.03 or above
                if (major > 2) return true;
                if (major == 2 && minor >= 3) return true;

                return false;
            }
            catch
            {
                return false;
            }
        }

        private string GetFMODVersionString()
        {
            try
            {
                var fmodAssembly = Assembly.Load("FMODUnity");
                if (fmodAssembly == null) return "Version Unknown";

                var versionType = fmodAssembly.GetType("FMOD.VERSION");
                if (versionType == null) return "Version Unknown";

                var numberField = versionType.GetField("number", BindingFlags.Public | BindingFlags.Static);
                if (numberField == null) return "Version Unknown";

                var versionNumber = (int)numberField.GetValue(null);

                // FMOD version format: 0x00AABBCC where AA=major, BB=minor, CC=patch
                int major = (int)((versionNumber >> 16) & 0xFF);
                int minor = (int)((versionNumber >> 8) & 0xFF);
                int patch = (int)(versionNumber & 0xFF);

                return $"{major}.{minor:D2}.{patch:D2}";
            }
            catch
            {
                return "Version Unknown";
            }
        }

        private void DrawWwiseSetupPage(VisualElement parent)
        {
            AddPageTitle(parent, "Wwise Audio Setup");

            Label description = new Label(
                "LCK uses a scripting define for Wwise support, which can be found in your project settings. " +
                "Press the button below to automatically add the necessary scripting define."
            );
            description.AddToClassList("lck-body-text");
            parent.Add(description);

            Button configureButton = new Button(() =>
            {
                Liv.Lck.Audio.Wwise.LckWwiseAudioMenu.ConfigureForWwiseAudioCapture();
            });
            configureButton.text = "Configure for Wwise";
            configureButton.AddToClassList("lck-button");
            configureButton.AddToClassList("lck-button--accent");
            configureButton.style.alignSelf = Align.FlexStart;
            configureButton.style.marginBottom = 15;
            configureButton.style.marginLeft = 0;
            parent.Add(configureButton);

            Label lckRequires = new Label(
                "LCK also requires an LckAudioMarker component to be on an active Game Object in the scene. " +
                "We suggest adding the component to the same Game Object that has the Wwise AkAudioListener component."
            );
            lckRequires.AddToClassList("lck-body-text");
            parent.Add(lckRequires);

            // LckAudioMarker detection UI
            VisualElement audioMarkerContainer = new VisualElement();
            audioMarkerContainer.style.flexDirection = FlexDirection.Row;
            audioMarkerContainer.style.alignItems = Align.Center;
            audioMarkerContainer.style.marginBottom = 15;

            Label audioMarkerStatusLabel = new Label();
            audioMarkerStatusLabel.style.marginRight = 10;
            audioMarkerContainer.Add(audioMarkerStatusLabel);

            Button searchAgainButton = new Button();
            searchAgainButton.text = "Search Again";
            searchAgainButton.AddToClassList("lck-button");
            searchAgainButton.AddToClassList("lck-button--secondary");
            searchAgainButton.AddToClassList("lck-button--small");
            searchAgainButton.style.alignSelf = Align.FlexStart;
            audioMarkerContainer.Add(searchAgainButton);

            Action updateAudioMarkerStatus = () =>
            {
#if UNITY_2022_1_OR_NEWER
                bool found = UnityEngine.Object.FindAnyObjectByType<LckAudioMarker>(FindObjectsInactive.Include) != null;
#else
                bool found = UnityEngine.Object.FindObjectOfType<LckAudioMarker>(true) != null;
#endif
                if (found)
                {
                    audioMarkerStatusLabel.text = "LckAudioMarker found in scene";
                    audioMarkerStatusLabel.style.color = new Color(0.831f, 1f, 0.165f);
                    searchAgainButton.style.display = DisplayStyle.None;
                }
                else
                {
                    audioMarkerStatusLabel.text = "LckAudioMarker not found in current scene";
                    audioMarkerStatusLabel.style.color = new Color(0.9f, 0.6f, 0.2f);
                    searchAgainButton.style.display = DisplayStyle.Flex;
                }
            };

            searchAgainButton.clicked += updateAudioMarkerStatus;
            updateAudioMarkerStatus();

            parent.Add(audioMarkerContainer);
        }

        private void DrawOtherAudioSetupPage(VisualElement parent)
        {
            AddPageTitle(parent, "Custom Audio Setup");

            Label description = new Label(
                "LCK uses a scripting define for custom audio support, which can be found in your project settings. " +
                "Use the button below to automatically add the necessary scripting define."
            );
            description.AddToClassList("lck-body-text");
            parent.Add(description);

            Button configureButton = new Button(() =>
            {
                ScriptingDefineUtils.AddDefine("LCK_NOT_UNITY_AUDIO");
            });
            configureButton.text = "Remove Unity Audio Capture From LCK";
            configureButton.AddToClassList("lck-button");
            configureButton.AddToClassList("lck-button--accent");
            configureButton.style.alignSelf = Align.FlexStart;
            configureButton.style.marginBottom = 15;
            parent.Add(configureButton);

            Label lckRequires = new Label(
                "If you are not using a Unity AudioListener, LCK also requires an LckAudioMarker component to be on an active Game Object in the scene."
            );
            lckRequires.AddToClassList("lck-body-text");
            parent.Add(lckRequires);

            // LckAudioMarker detection UI
            VisualElement audioMarkerContainer = new VisualElement();
            audioMarkerContainer.style.flexDirection = FlexDirection.Row;
            audioMarkerContainer.style.alignItems = Align.Center;
            audioMarkerContainer.style.marginBottom = 15;

            Label audioMarkerStatusLabel = new Label();
            audioMarkerStatusLabel.style.marginRight = 10;
            audioMarkerContainer.Add(audioMarkerStatusLabel);

            Button searchAgainButton = new Button();
            searchAgainButton.text = "Search Again";
            searchAgainButton.AddToClassList("lck-button");
            searchAgainButton.AddToClassList("lck-button--secondary");
            searchAgainButton.AddToClassList("lck-button--small");
            searchAgainButton.style.alignSelf = Align.FlexStart;
            audioMarkerContainer.Add(searchAgainButton);

            Action updateAudioMarkerStatus = () =>
            {
#if UNITY_2022_1_OR_NEWER
                bool foundAudioListener = UnityEngine.Object.FindAnyObjectByType<AudioListener>(FindObjectsInactive.Include) != null;
                bool foundLckAudioMarker = UnityEngine.Object.FindAnyObjectByType<LckAudioMarker>(FindObjectsInactive.Include) != null;
#else
                bool foundAudioListener = UnityEngine.Object.FindObjectOfType<AudioListener>(true) != null;
                bool foundLckAudioMarker = UnityEngine.Object.FindObjectOfType<LckAudioMarker>(true) != null;
#endif

                if (foundAudioListener)
                {
                    audioMarkerStatusLabel.text = "Unity AudioListener found in scene";
                    audioMarkerStatusLabel.style.color = new Color(0.831f, 1f, 0.165f);
                    searchAgainButton.style.display = DisplayStyle.None;
                }
                else if (foundLckAudioMarker)
                {
                    audioMarkerStatusLabel.text = "LckAudioMarker found in scene";
                    audioMarkerStatusLabel.style.color = new Color(0.831f, 1f, 0.165f);
                    searchAgainButton.style.display = DisplayStyle.None;
                }
                else
                {
                    audioMarkerStatusLabel.text = "No AudioListener or LckAudioMarker found in current scene";
                    audioMarkerStatusLabel.style.color = new Color(0.9f, 0.6f, 0.2f);
                    searchAgainButton.style.display = DisplayStyle.Flex;
                }
            };

            searchAgainButton.clicked += updateAudioMarkerStatus;
            updateAudioMarkerStatus();

            parent.Add(audioMarkerContainer);

            Label helpNotes = new Label(
                "To get started, you can implement the interface ILckAudioSource into your scripts to manually supply game audio to LCK.\n\n" +
                "Additional Notes:\n" +
                "• LCK will still collect microphone audio separately from custom sources.\n" +
                "• LCK expects all audio in stereo, interleaved floats, at 48kHz.\n" +
                "• To use this approach, implement ILckAudioSource in a component and place it:\n" +
                "  - next to a Unity AudioListener, or\n" +
                "  - next to an LckAudioMarker."
            );
            helpNotes.AddToClassList("lck-body-text");
            parent.Add(helpNotes);
        }

        private void DrawValidationPage(VisualElement parent)
        {
            AddPageTitle(parent, "Project Validation");

            // Add spacing after title
            VisualElement spacer = new VisualElement();
            spacer.style.height = 5;
            parent.Add(spacer);

            // Required section header
            Label requiredHeader = new Label("Required");
            requiredHeader.style.fontSize = 14;
            requiredHeader.style.unityFontStyleAndWeight = FontStyle.Bold;
            requiredHeader.style.marginBottom = 5;
            requiredHeader.style.marginTop = 5;
            parent.Add(requiredHeader);

            // Required validation: Minimum API Level
            AndroidSdkVersions currentMin = PlayerSettings.Android.minSdkVersion;
            int currentLevel = (int)currentMin;
            int requiredLevel = LckSettings.RequiredAndroidApiLevel;
            VisualElement element1 = CreateValidationElement($"Minimum Api Level is {requiredLevel}+ (Android)", () => { PlayerSettings.Android.minSdkVersion = (AndroidSdkVersions)LckSettings.RequiredAndroidApiLevel; UpdateContentArea(); }, currentLevel >= requiredLevel, "Apply Fix", ValidationSeverity.Required);
            parent.Add(element1);

            // Suggested section header
            Label suggestedHeader = new Label("Suggested");
            suggestedHeader.style.fontSize = 14;
            suggestedHeader.style.unityFontStyleAndWeight = FontStyle.Bold;
            suggestedHeader.style.marginBottom = 5;
            suggestedHeader.style.marginTop = 15;
            parent.Add(suggestedHeader);

            // Suggested validation: ARM64 Architecture
            bool isArm64Enabled = PlayerSettings.Android.targetArchitectures.HasFlag(AndroidArchitecture.ARM64);
            VisualElement element2 = CreateValidationElement("Target Architecture is ARM64 (Android)", () => { PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64; UpdateContentArea(); }, isArm64Enabled);
            parent.Add(element2);

            // Suggested validation: LCK Tablet Layer
            bool hasLckTabletLayer = HasLayer("LCK Tablet");
            VisualElement element3 = CreateValidationElement("LCK Tablet Layer Added", () => { AddLayer("LCK Tablet"); UpdateContentArea(); }, hasLckTabletLayer);
            parent.Add(element3);

            // Suggested validation: AudioListener or LckAudioMarker in scene
#if UNITY_2022_1_OR_NEWER
            bool hasAudioListenerOrMarker = UnityEngine.Object.FindAnyObjectByType<AudioListener>(FindObjectsInactive.Include) != null
                || UnityEngine.Object.FindAnyObjectByType<LckAudioMarker>(FindObjectsInactive.Include) != null;
#else
            bool hasAudioListenerOrMarker = UnityEngine.Object.FindObjectOfType<AudioListener>(true) != null
                || UnityEngine.Object.FindObjectOfType<LckAudioMarker>(true) != null;
#endif
            string audioMarkerText = hasAudioListenerOrMarker
                ? "Found AudioListener or LckAudioMarker in scene"
                : "No AudioListener or LckAudioMarker found in scene";
            VisualElement element4 = CreateValidationElement(audioMarkerText, () => { UpdateContentArea(); }, hasAudioListenerOrMarker, "Search Again");
            parent.Add(element4);

            // Check if all validations pass and trigger celebration
            bool allPassed = currentLevel >= requiredLevel && isArm64Enabled && hasLckTabletLayer && hasAudioListenerOrMarker;
            if (allPassed)
            {
                // Add success message
                VisualElement successBox = new VisualElement();
                successBox.AddToClassList("lck-success-box");
                successBox.style.marginTop = 20;
#if !UNITY_2021_1_OR_NEWER
                // Fallback styling for Unity 2020.3
                successBox.style.backgroundColor = new Color(0.859f, 1f, 0.482f); // #DBFF7B
                successBox.style.borderTopLeftRadius = 8;
                successBox.style.borderTopRightRadius = 8;
                successBox.style.borderBottomLeftRadius = 8;
                successBox.style.borderBottomRightRadius = 8;
                successBox.style.paddingTop = 12;
                successBox.style.paddingBottom = 12;
                successBox.style.paddingLeft = 16;
                successBox.style.paddingRight = 16;
#endif

                Label successText = new Label("All validations passed! Your project is ready for LCK.");
                successText.AddToClassList("lck-success-box-text");
#if !UNITY_2021_1_OR_NEWER
                successText.style.fontSize = 13;
                successText.style.color = new Color(0.039f, 0.039f, 0.043f); // #0A0A0B
                successText.style.whiteSpace = WhiteSpace.Normal;
                successText.style.unityFontStyleAndWeight = FontStyle.Bold;
#endif
                successBox.Add(successText);
                parent.Add(successBox);

                // Trigger confetti celebration after a short delay (on root element for full window coverage)
                parent.schedule.Execute(() => TriggerConfettiCelebration(_rootElement)).StartingIn(100);
            }
        }

        private void TriggerConfettiCelebration(VisualElement parent)
        {
            // Create confetti container overlay
            VisualElement confettiContainer = new VisualElement();
            confettiContainer.name = "confetti-container";
            confettiContainer.style.position = Position.Absolute;
            confettiContainer.style.top = 0;
            confettiContainer.style.left = 0;
            confettiContainer.style.right = 0;
            confettiContainer.style.bottom = 0;
            confettiContainer.pickingMode = PickingMode.Ignore;
            confettiContainer.style.overflow = Overflow.Hidden;

            // LIV brand colors for confetti
            Color[] confettiColors = new Color[]
            {
                new Color(0.831f, 1f, 0.165f),      // LIV Green #D4FF2A
                new Color(0.42f, 0.36f, 0.91f), // Purple #6B5CE7
                new Color(1f, 1f, 1f),          // White
                new Color(1f, 0.72f, 0.3f),     // Orange
                new Color(0.42f, 0.65f, 1f),    // Blue
            };

            System.Random random = new System.Random();
            int particleCount = 60;

            for (int i = 0; i < particleCount; i++)
            {
                VisualElement particle = new VisualElement();
                particle.pickingMode = PickingMode.Ignore;

                // Random size between 6 and 12
                int size = random.Next(6, 13);
                particle.style.width = size;
                particle.style.height = size;

                // Random initial rotation
                float initialRotation = random.Next(0, 360);
#if UNITY_2021_2_OR_NEWER
                particle.style.rotate = new Rotate(initialRotation);
#endif

                // Random color from our palette
                Color particleColor = confettiColors[random.Next(confettiColors.Length)];
                particle.style.backgroundColor = particleColor;

                // Start above the top, at random horizontal positions
                particle.style.position = Position.Absolute;
                particle.style.left = Length.Percent(random.Next(0, 100));
                particle.style.top = Length.Percent(-10); // Start above viewport

                // Random border radius (some round, some square)
                if (random.Next(2) == 0)
                {
                    particle.style.borderTopLeftRadius = size / 2;
                    particle.style.borderTopRightRadius = size / 2;
                    particle.style.borderBottomLeftRadius = size / 2;
                    particle.style.borderBottomRightRadius = size / 2;
                }

                confettiContainer.Add(particle);

                // Random fall parameters
                float horizontalDrift = (float)(random.NextDouble() * 2 - 1) * 0.5f; // Slight horizontal drift
                float fallSpeed = (float)(random.NextDouble() * 0.8 + 0.4); // Random fall speed
                int rotationSpeed = random.Next(-360, 360);
                int delay = random.Next(0, 1500); // Staggered start

                AnimateConfettiFalling(particle, delay, horizontalDrift, fallSpeed, initialRotation, rotationSpeed, particleColor);
            }

            parent.Add(confettiContainer);

            // Remove confetti container after animation completes
            parent.schedule.Execute(() =>
            {
                if (confettiContainer.parent != null)
                {
                    confettiContainer.RemoveFromHierarchy();
                }
            }).StartingIn(6000);
        }

        private void AnimateConfettiFalling(VisualElement particle, int delay, float horizontalDrift, float fallSpeed, float initialRotation, int rotationSpeed, Color originalColor)
        {
            int totalDuration = 2000;
            int steps = 220;
            int stepDuration = totalDuration / steps;
            int currentStep = 0;
            float currentY = -10f;
            float currentX = particle.style.left.value.value;

            particle.schedule.Execute(() =>
            {
                particle.schedule.Execute(() =>
                {
                    currentStep++;
                    float progress = (float)currentStep / steps;

                    // Update position - fall down with slight horizontal drift (1.3x speed)
                    currentY += fallSpeed * 1.3f;
                    currentX += horizontalDrift * Mathf.Sin(progress * Mathf.PI * 4); // Gentle swaying motion

                    particle.style.left = Length.Percent(currentX);
                    particle.style.top = Length.Percent(currentY);

                    // Continuous rotation
#if UNITY_2021_2_OR_NEWER
                    particle.style.rotate = new Rotate(initialRotation + (rotationSpeed * progress));
#endif

                    // Fade out as particles approach bottom (start fading at 50%, fully faded at 100%)
                    if (currentY > 50f)
                    {
                        float fadeProgress = (currentY - 50f) / 50f;
                        float alpha = Mathf.Clamp01(1f - fadeProgress);
                        particle.style.backgroundColor = new Color(originalColor.r, originalColor.g, originalColor.b, alpha);
                    }

                }).Every(stepDuration).Until(() => currentStep >= steps || currentY > 100f);
            }).StartingIn(delay);
        }

        private void DrawAdditionalHelpPage(VisualElement parent)
        {
            AddPageTitle(parent, "Additional Help");

            Label description = new Label(
                "Need more help getting started with LCK? Check out our documentation or join our Discord community."
            );
            description.AddToClassList("lck-body-text");
            description.style.marginBottom = 20;
            parent.Add(description);

            // Documentation button
            Button docsButton = new Button(() =>
            {
                Application.OpenURL("https://liv.mintlify.app/");
            });
            docsButton.text = "Open Documentation";
            docsButton.AddToClassList("lck-button");
            docsButton.AddToClassList("lck-button--primary");
            docsButton.style.alignSelf = Align.FlexStart;
            docsButton.style.marginBottom = 15;
            parent.Add(docsButton);

            // Discord button with icon
            Button discordButton = new Button(() =>
            {
                Application.OpenURL("https://discord.com/invite/liv");
            });
            discordButton.AddToClassList("lck-button");
            discordButton.AddToClassList("lck-button--secondary");
            discordButton.AddToClassList("lck-button-with-icon");
            discordButton.style.alignSelf = Align.FlexStart;
            discordButton.style.marginBottom = 15;

            // Add Discord icon (hidden on Unity 2020.3 due to sizing issues)
#if UNITY_2021_2_OR_NEWER
            Texture2D discordIcon = AssetDatabase.LoadAssetAtPath<Texture2D>("Packages/tv.liv.lck/Editor/SetupWizard/Icons/Discord-Symbol-White.png");
            if (discordIcon != null)
            {
                Image iconImage = new Image();
                iconImage.image = discordIcon;
                iconImage.scaleMode = ScaleMode.ScaleToFit;
                iconImage.AddToClassList("lck-button-icon");
                iconImage.AddToClassList("lck-button-icon--discord");
                iconImage.pickingMode = PickingMode.Ignore;
                discordButton.Add(iconImage);
            }
#endif

            Label discordLabel = new Label("Join Our Discord");
            discordLabel.pickingMode = PickingMode.Ignore;
            discordButton.Add(discordLabel);

            parent.Add(discordButton);
        }

        private void DrawUpdateAvailablePage(VisualElement parent)
        {
            AddPageTitle(parent, "Update Available");

            // Description (moved to below header)
            AddPageSubtitle(parent,
                "A new version of LIV Camera Kit is available. We recommend updating to get the latest features and bug fixes.");

            string latestVersion = _latestVersionInfo?.LatestVersion ?? "Unknown";
            string currentVersion = LckSettings.Version;

            // Version comparison box (centered)
            VisualElement versionBox = new VisualElement();
            versionBox.AddToClassList("lck-version-box");
            versionBox.style.alignItems = Align.Center;

            Label currentVersionLabel = new Label($"Current Version: {currentVersion}");
            currentVersionLabel.AddToClassList("lck-version-current");
            versionBox.Add(currentVersionLabel);

            Label arrowLabel = new Label("\u2193"); // Down arrow
            arrowLabel.AddToClassList("lck-version-arrow");
            versionBox.Add(arrowLabel);

            // Download version button (white for external link)
            Button downloadVersionButton = new Button(() =>
            {
                Application.OpenURL("https://dashboard.liv.tv/dev/login");
            });
            downloadVersionButton.text = $"Download Version {latestVersion}";
            downloadVersionButton.AddToClassList("lck-button");
            downloadVersionButton.AddToClassList("lck-button--primary");
            versionBox.Add(downloadVersionButton);

            parent.Add(versionBox);

            // Button row
            VisualElement buttonRow = new VisualElement();
            buttonRow.AddToClassList("lck-button-row");

            // View Update Instructions button (white for external link)
            Button updateButton = new Button(() =>
            {
                Application.OpenURL("https://liv.mintlify.app/unity/upgrade-guide");
            });
            updateButton.text = "View Update Instructions";
            updateButton.AddToClassList("lck-button");
            updateButton.AddToClassList("lck-button--primary");
            buttonRow.Add(updateButton);

            // Continue button (purple for action)
            Button continueButton = new Button(() =>
            {
                _currentPage = WizardPage.Welcome;
                UpdateContentArea();
            });
            continueButton.text = "Continue to Setup";
            continueButton.AddToClassList("lck-button");
            continueButton.AddToClassList("lck-button--accent");
            buttonRow.Add(continueButton);

            parent.Add(buttonRow);
        }
    }
}