using Liv.Lck.Settings;
using Liv.Lck.Util;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Liv.Lck
{
    /// <summary>
    /// Base class for all LCK settings pages providing common styling and utilities.
    /// </summary>
    public abstract class LckSettingsPageBase : SettingsProvider
    {
        /// <summary>
        /// Override in derived classes to indicate if this is the Overview page.
        /// The Overview page always shows the update notification bar.
        /// </summary>
        protected virtual bool IsOverviewPage => false;

        /// <summary>
        /// Override in derived classes to indicate if this is the Update page.
        /// The Update page always shows the update notification bar (like Overview).
        /// </summary>
        protected virtual bool IsUpdatePage => false;

        // Exported package paths
        protected const string StyleSheetPath = "Packages/tv.liv.lck/Editor/SetupWizard/LckSetupWizard.uss";
        protected const string LogoPath = "Packages/tv.liv.lck/Editor/SetupWizard/Icons/liv-logo.png";
        protected const string ChangelogPath = "Packages/tv.liv.lck/CHANGELOG.md";

        // Development package paths (fallback)
        protected const string DevStyleSheetPath = "Packages/gg.obi.unity.qck/Editor/SetupWizard/LckSetupWizard.uss";
        protected const string DevLogoPath = "Packages/gg.obi.unity.qck/Editor/SetupWizard/Icons/liv-logo.png";
        protected const string DevChangelogPath = "Packages/gg.obi.unity.qck/CHANGELOG.md";

        protected VisualElement _rootElement;

        protected LckSettingsPageBase(string path, SettingsScope scopes, System.Collections.Generic.IEnumerable<string> keywords = null)
            : base(path, scopes, keywords)
        {
        }

        private VisualElement _updateNotificationBar;

        public override void OnActivate(string searchContext, VisualElement rootElement)
        {
            base.OnActivate(searchContext, rootElement);
            _rootElement = rootElement;

            // Load stylesheet (try exported path first, then development path)
            var styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(StyleSheetPath);
            if (styleSheet == null)
            {
                styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(DevStyleSheetPath);
            }
            if (styleSheet != null)
            {
                rootElement.styleSheets.Add(styleSheet);
            }

            // Create an inner container for our styling (don't apply to rootElement directly
            // as that affects all Project Settings pages)
            VisualElement lckContainer = new VisualElement();
            lckContainer.AddToClassList("lck-wizard-root");
            lckContainer.style.flexGrow = 1;
            rootElement.Add(lckContainer);

            // Store the container as our working root
            _rootElement = lckContainer;

            // Apply monospace font
            ApplyMonospaceFont(lckContainer);

            // Add update notification bar container at the top
            _updateNotificationBar = new VisualElement();
            _updateNotificationBar.name = "update-notification-bar";
            lckContainer.Add(_updateNotificationBar);

            // Subscribe to update available event to refresh notification bar
            LckUpdatePage.OnUpdateAvailable -= OnUpdateAvailableHandler;
            LckUpdatePage.OnUpdateAvailable += OnUpdateAvailableHandler;

            // Trigger update check (if not already done by startup checker) and show notification bar
            LckUpdatePage.CheckForUpdates();
            UpdateNotificationBar();

            // Build the page content
            BuildUI(lckContainer);
        }

        public override void OnDeactivate()
        {
            base.OnDeactivate();

            // Unsubscribe from update event
            LckUpdatePage.OnUpdateAvailable -= OnUpdateAvailableHandler;
        }

        private void OnUpdateAvailableHandler()
        {
            UpdateNotificationBar();
        }

        /// <summary>
        /// Updates the notification bar visibility and content.
        /// Shows on all pages if update available.
        /// After dismiss: only persists on Overview page (and Update page).
        /// </summary>
        protected void UpdateNotificationBar()
        {
            if (_updateNotificationBar == null) return;

            _updateNotificationBar.Clear();

            try
            {
                var versionInfo = LckUpdatePage.GetLatestVersionInfo();
                if (versionInfo == null || !versionInfo.Success) return;

                string latestVersion = versionInfo.LatestVersion;
                string currentVersion = LckSettings.Version;

                // Check if update is available (use direct comparison as fallback)
                bool updateAvailable = LckUpdatePage.IsUpdateAvailable ||
                    LckVersionChecker.IsUpdateAvailable(currentVersion, latestVersion);
                if (!updateAvailable) return;

                // Overview and Update pages always show the notification bar
                // On other pages, respect the dismissed notification bar setting
                bool alwaysShow = IsOverviewPage || IsUpdatePage;
                var settings = LckSettings.Instance;
                if (!alwaysShow && settings != null && settings.DismissedNotificationBarVersion == latestVersion)
                {
                    return;
                }

            // Create notification bar
            VisualElement bar = new VisualElement();
            bar.style.flexDirection = FlexDirection.Row;
            bar.style.alignItems = Align.Center;
            bar.style.justifyContent = Justify.SpaceBetween;
            bar.style.backgroundColor = new Color(0.15f, 0.2f, 0.1f);
            bar.style.borderBottomWidth = 1;
            bar.style.borderBottomColor = new Color(0.831f, 1f, 0.165f);
            bar.style.paddingTop = 10;
            bar.style.paddingBottom = 10;
            bar.style.paddingLeft = 16;
            bar.style.paddingRight = 16;

            // Left side: icon and text
            VisualElement leftContainer = new VisualElement();
            leftContainer.style.flexDirection = FlexDirection.Row;
            leftContainer.style.alignItems = Align.Center;

            var iconContent = EditorGUIUtility.IconContent("d_Import");
            if (iconContent != null && iconContent.image != null)
            {
                Image icon = new Image();
                icon.image = iconContent.image;
                icon.style.width = 16;
                icon.style.height = 16;
                icon.style.marginRight = 8;
                icon.tintColor = new Color(0.831f, 1f, 0.165f);
                icon.RegisterCallback<ClickEvent>(evt => SettingsService.OpenProjectSettings("Project/LCK/5 Updates"));
                icon.AddToClassList("lck-clickable");
                leftContainer.Add(icon);
            }

            Label textLabel = new Label($"LCK {latestVersion} is available (you have {LckSettings.Version})");
            textLabel.style.color = new Color(0.831f, 1f, 0.165f);
            textLabel.style.fontSize = 12;
            leftContainer.Add(textLabel);

            bar.Add(leftContainer);

            // Right side: buttons
            VisualElement rightContainer = new VisualElement();
            rightContainer.style.flexDirection = FlexDirection.Row;
            rightContainer.style.alignItems = Align.Center;

            // Show "View Update" button on all pages except the Update page itself
            if (!IsUpdatePage)
            {
                Button viewButton = new Button(() =>
                {
                    SettingsService.OpenProjectSettings("Project/LCK/5 Updates");
                });
                viewButton.text = "View Update";
                viewButton.AddToClassList("lck-button");
                viewButton.AddToClassList("lck-button--small");
                viewButton.AddToClassList("lck-button--accent");
                viewButton.style.marginRight = 8;
                rightContainer.Add(viewButton);
            }

            // Show "Dismiss" button on all pages except Overview
            // (Overview always shows the bar, so dismiss wouldn't make sense there)
            if (!IsOverviewPage)
            {
                Button dismissButton = new Button(() =>
                {
                    try
                    {
                        // Dismiss both the notification bar and the update page for this version
                        var dismissSettings = LckSettings.Instance;
                        if (dismissSettings != null)
                        {
                            dismissSettings.DismissedNotificationBarVersion = latestVersion;
                            dismissSettings.DismissedUpdateVersion = latestVersion;
                            EditorUtility.SetDirty(dismissSettings);
                            AssetDatabase.SaveAssetIfDirty(dismissSettings);
                        }
                        UpdateNotificationBar();
                    }
                    catch (System.Exception ex)
                    {
                        Debug.LogError($"[LckSettingsPageBase] Failed to dismiss notification: {ex.Message}");
                    }
                });
                dismissButton.text = "Dismiss";
                dismissButton.AddToClassList("lck-button");
                dismissButton.AddToClassList("lck-button--small");
                dismissButton.AddToClassList("lck-button--secondary");
                rightContainer.Add(dismissButton);
            }

            bar.Add(rightContainer);
            _updateNotificationBar.Add(bar);
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[LckSettingsPageBase] Failed to update notification bar: {ex.Message}");
            }
        }

        /// <summary>
        /// Gets the logo texture, trying exported path first then development path.
        /// </summary>
        protected Texture2D GetLogoTexture()
        {
            var logo = AssetDatabase.LoadAssetAtPath<Texture2D>(LogoPath);
            if (logo == null)
            {
                logo = AssetDatabase.LoadAssetAtPath<Texture2D>(DevLogoPath);
            }
            return logo;
        }

        /// <summary>
        /// Gets the changelog text asset, trying exported path first then development path.
        /// </summary>
        protected TextAsset GetChangelogAsset()
        {
            var changelog = AssetDatabase.LoadAssetAtPath<TextAsset>(ChangelogPath);
            if (changelog == null)
            {
                changelog = AssetDatabase.LoadAssetAtPath<TextAsset>(DevChangelogPath);
            }

            return changelog;
        }

        protected abstract void BuildUI(VisualElement root);

        protected void ApplyMonospaceFont(VisualElement root)
        {
            Font monoFont = null;

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
        }

        protected void AddPageTitle(VisualElement parent, string title)
        {
            Label titleLabel = new Label(title);
            titleLabel.AddToClassList("lck-page-title");
#if !UNITY_2021_1_OR_NEWER
            titleLabel.style.fontSize = 28;
            titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            titleLabel.style.color = Color.white;
            titleLabel.style.marginBottom = 8;
#endif
            parent.Add(titleLabel);
        }

        protected void AddPageSubtitle(VisualElement parent, string subtitle)
        {
            Label subtitleLabel = new Label(subtitle);
            subtitleLabel.AddToClassList("lck-page-subtitle");
#if !UNITY_2021_1_OR_NEWER
            subtitleLabel.style.fontSize = 13;
            subtitleLabel.style.color = new Color(0.63f, 0.63f, 0.63f); // #A0A0A0
            subtitleLabel.style.marginBottom = 16;
            subtitleLabel.style.whiteSpace = WhiteSpace.Normal;
#endif
            parent.Add(subtitleLabel);
        }

        protected void AddSectionHeader(VisualElement parent, string header, bool isFirst = false)
        {
            Label headerLabel = new Label(header.ToUpper());
            headerLabel.AddToClassList("lck-section-header");
            if (isFirst)
            {
                headerLabel.AddToClassList("lck-section-header--first");
            }
#if !UNITY_2021_1_OR_NEWER
            headerLabel.style.fontSize = 12;
            headerLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            headerLabel.style.color = Color.white;
            headerLabel.style.marginTop = isFirst ? 0 : 24;
            headerLabel.style.marginBottom = 12;
#endif
            parent.Add(headerLabel);
        }

        protected void AddBodyText(VisualElement parent, string text)
        {
            Label label = new Label(text);
            label.AddToClassList("lck-body-text");
#if !UNITY_2021_1_OR_NEWER
            label.style.fontSize = 13;
            label.style.color = new Color(0.63f, 0.63f, 0.63f); // #A0A0A0
            label.style.whiteSpace = WhiteSpace.Normal;
            label.style.marginBottom = 12;
#endif
            parent.Add(label);
        }

        protected Button CreatePrimaryButton(string text, System.Action onClick)
        {
            Button button = new Button(onClick);
            button.text = text;
            button.AddToClassList("lck-button");
            button.AddToClassList("lck-button--primary");
            ApplyButtonFallbackStyles(button, Color.white, new Color(0.039f, 0.039f, 0.043f)); // #FFFFFF bg, #0A0A0B text
            return button;
        }

        protected Button CreateSecondaryButton(string text, System.Action onClick)
        {
            Button button = new Button(onClick);
            button.text = text;
            button.AddToClassList("lck-button");
            button.AddToClassList("lck-button--secondary");
            ApplyButtonFallbackStyles(button, Color.clear, Color.white, true);
            return button;
        }

        protected Button CreateAccentButton(string text, System.Action onClick)
        {
            Button button = new Button(onClick);
            button.text = text;
            button.AddToClassList("lck-button");
            button.AddToClassList("lck-button--accent");
            ApplyButtonFallbackStyles(button, new Color(0.42f, 0.36f, 0.91f), Color.white); // #6B5CE7
            return button;
        }

        private void ApplyButtonFallbackStyles(Button button, Color bgColor, Color textColor, bool hasBorder = false)
        {
#if !UNITY_2021_1_OR_NEWER
            button.style.height = 36;
            button.style.paddingLeft = 20;
            button.style.paddingRight = 20;
            button.style.borderTopLeftRadius = 6;
            button.style.borderTopRightRadius = 6;
            button.style.borderBottomLeftRadius = 6;
            button.style.borderBottomRightRadius = 6;
            button.style.fontSize = 14;
            button.style.unityFontStyleAndWeight = FontStyle.Bold;
            button.style.backgroundColor = bgColor;
            button.style.color = textColor;
            button.style.borderTopWidth = hasBorder ? 1 : 0;
            button.style.borderBottomWidth = hasBorder ? 1 : 0;
            button.style.borderLeftWidth = hasBorder ? 1 : 0;
            button.style.borderRightWidth = hasBorder ? 1 : 0;
            if (hasBorder)
            {
                button.style.borderTopColor = new Color(0.227f, 0.227f, 0.239f); // #3A3A3D
                button.style.borderBottomColor = new Color(0.227f, 0.227f, 0.239f);
                button.style.borderLeftColor = new Color(0.227f, 0.227f, 0.239f);
                button.style.borderRightColor = new Color(0.227f, 0.227f, 0.239f);
            }
#endif
        }

        protected VisualElement CreateCard()
        {
            VisualElement card = new VisualElement();
            card.AddToClassList("lck-card");

            // Apply inline fallback styles for Unity 2020.3 compatibility
            // (USS parsing may skip rules with unsupported properties in older Unity versions)
            ApplyCardFallbackStyles(card);

            return card;
        }

        /// <summary>
        /// Applies inline fallback styles for cards to ensure compatibility with Unity 2020.3.
        /// </summary>
        protected void ApplyCardFallbackStyles(VisualElement card)
        {
            ApplyCardFallbackStylesStatic(card);
        }

        /// <summary>
        /// Static version of ApplyCardFallbackStyles for use in non-SettingsProvider classes.
        /// </summary>
        public static void ApplyCardFallbackStylesStatic(VisualElement card)
        {
#if !UNITY_2021_1_OR_NEWER
            // Apply essential card styling inline for older Unity versions
            card.style.backgroundColor = new Color(0.118f, 0.118f, 0.129f); // #1E1E21
            card.style.borderTopLeftRadius = 8;
            card.style.borderTopRightRadius = 8;
            card.style.borderBottomLeftRadius = 8;
            card.style.borderBottomRightRadius = 8;
            card.style.paddingTop = 16;
            card.style.paddingBottom = 16;
            card.style.paddingLeft = 16;
            card.style.paddingRight = 16;
            card.style.marginBottom = 12;
            card.style.borderTopWidth = 1;
            card.style.borderBottomWidth = 1;
            card.style.borderLeftWidth = 1;
            card.style.borderRightWidth = 1;
            card.style.borderTopColor = new Color(0.165f, 0.165f, 0.176f); // #2A2A2D
            card.style.borderBottomColor = new Color(0.165f, 0.165f, 0.176f);
            card.style.borderLeftColor = new Color(0.165f, 0.165f, 0.176f);
            card.style.borderRightColor = new Color(0.165f, 0.165f, 0.176f);
#endif
        }

        /// <summary>
        /// Applies inline fallback styles for validation rows to ensure compatibility with Unity 2020.3.
        /// </summary>
        public static void ApplyValidationRowFallbackStylesStatic(VisualElement row)
        {
#if !UNITY_2021_1_OR_NEWER
            // Apply essential validation row styling inline for older Unity versions
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.backgroundColor = new Color(0.118f, 0.118f, 0.129f); // #1E1E21
            row.style.paddingTop = 14;
            row.style.paddingBottom = 14;
            row.style.paddingLeft = 16;
            row.style.paddingRight = 16;
            row.style.borderTopLeftRadius = 8;
            row.style.borderTopRightRadius = 8;
            row.style.borderBottomLeftRadius = 8;
            row.style.borderBottomRightRadius = 8;
            row.style.marginBottom = 8;
            row.style.borderTopWidth = 1;
            row.style.borderBottomWidth = 1;
            row.style.borderLeftWidth = 1;
            row.style.borderRightWidth = 1;
            row.style.borderTopColor = new Color(0.165f, 0.165f, 0.176f); // #2A2A2D
            row.style.borderBottomColor = new Color(0.165f, 0.165f, 0.176f);
            row.style.borderLeftColor = new Color(0.165f, 0.165f, 0.176f);
            row.style.borderRightColor = new Color(0.165f, 0.165f, 0.176f);
#endif
        }

        protected VisualElement CreateInfoBox(string text)
        {
            VisualElement box = new VisualElement();
            box.AddToClassList("lck-info-box");
            ApplyInfoBoxFallbackStyles(box);

            Label label = new Label(text);
            label.AddToClassList("lck-info-box-text");
            ApplyBoxTextFallbackStyles(label, new Color(0.63f, 0.63f, 0.63f)); // #A0A0A0
            box.Add(label);

            return box;
        }

        protected VisualElement CreateWarningBox(string text)
        {
            VisualElement box = new VisualElement();
            box.AddToClassList("lck-warning-box");
            ApplyWarningBoxFallbackStyles(box);

            Label label = new Label(text);
            label.AddToClassList("lck-warning-box-text");
            ApplyBoxTextFallbackStyles(label, new Color(1f, 0.722f, 0.302f)); // #FFB84D
            box.Add(label);

            return box;
        }

        protected VisualElement CreateSuccessBox(string text)
        {
            VisualElement box = new VisualElement();
            box.AddToClassList("lck-success-box");
            ApplySuccessBoxFallbackStyles(box);

            Label label = new Label(text);
            label.AddToClassList("lck-success-box-text");
            ApplyBoxTextFallbackStyles(label, new Color(0.039f, 0.039f, 0.043f), true); // #0A0A0B
            box.Add(label);

            return box;
        }

        private void ApplyInfoBoxFallbackStyles(VisualElement box)
        {
#if !UNITY_2021_1_OR_NEWER
            box.style.backgroundColor = new Color(0.102f, 0.102f, 0.227f); // #1A1A3A
            box.style.borderTopLeftRadius = 8;
            box.style.borderTopRightRadius = 8;
            box.style.borderBottomLeftRadius = 8;
            box.style.borderBottomRightRadius = 8;
            box.style.paddingTop = 12;
            box.style.paddingBottom = 12;
            box.style.paddingLeft = 16;
            box.style.paddingRight = 16;
            box.style.marginBottom = 16;
            box.style.borderLeftWidth = 3;
            box.style.borderLeftColor = new Color(0.42f, 0.36f, 0.91f); // #6B5CE7
#endif
        }

        private void ApplyWarningBoxFallbackStyles(VisualElement box)
        {
#if !UNITY_2021_1_OR_NEWER
            box.style.backgroundColor = new Color(0.227f, 0.165f, 0.102f); // #3A2A1A
            box.style.borderTopLeftRadius = 8;
            box.style.borderTopRightRadius = 8;
            box.style.borderBottomLeftRadius = 8;
            box.style.borderBottomRightRadius = 8;
            box.style.paddingTop = 12;
            box.style.paddingBottom = 12;
            box.style.paddingLeft = 16;
            box.style.paddingRight = 16;
            box.style.marginBottom = 16;
            box.style.borderLeftWidth = 3;
            box.style.borderLeftColor = new Color(1f, 0.722f, 0.302f); // #FFB84D
#endif
        }

        private void ApplySuccessBoxFallbackStyles(VisualElement box)
        {
#if !UNITY_2021_1_OR_NEWER
            box.style.backgroundColor = new Color(0.859f, 1f, 0.482f); // #DBFF7B
            box.style.borderTopLeftRadius = 8;
            box.style.borderTopRightRadius = 8;
            box.style.borderBottomLeftRadius = 8;
            box.style.borderBottomRightRadius = 8;
            box.style.paddingTop = 12;
            box.style.paddingBottom = 12;
            box.style.paddingLeft = 16;
            box.style.paddingRight = 16;
            box.style.marginBottom = 16;
#endif
        }

        private void ApplyBoxTextFallbackStyles(Label label, Color textColor, bool bold = false)
        {
#if !UNITY_2021_1_OR_NEWER
            label.style.fontSize = 13;
            label.style.color = textColor;
            label.style.whiteSpace = WhiteSpace.Normal;
            if (bold)
            {
                label.style.unityFontStyleAndWeight = FontStyle.Bold;
            }
#endif
        }

        protected void AddSpacer(VisualElement parent, string size = "md")
        {
            VisualElement spacer = new VisualElement();
            spacer.AddToClassList($"lck-spacer-{size}");
            parent.Add(spacer);
        }

        protected VisualElement CreateContentArea()
        {
            ScrollView scrollView = new ScrollView();
            scrollView.AddToClassList("lck-content-scroll");
#if !UNITY_2021_1_OR_NEWER
            scrollView.style.flexGrow = 1;
            scrollView.style.backgroundColor = new Color(0.075f, 0.078f, 0.078f); // #131414
#endif

            VisualElement contentArea = new VisualElement();
            contentArea.AddToClassList("lck-content-area");
#if !UNITY_2021_1_OR_NEWER
            contentArea.style.flexGrow = 1;
            contentArea.style.paddingTop = 24;
            contentArea.style.paddingBottom = 24;
            contentArea.style.paddingLeft = 24;
            contentArea.style.paddingRight = 24;
            contentArea.style.backgroundColor = new Color(0.075f, 0.078f, 0.078f); // #131414
#endif
            scrollView.Add(contentArea);

            return scrollView;
        }

        // Shared color constants
        protected static readonly Color AccentPurple = new Color(0.42f, 0.36f, 0.91f); // #6B5CE7
        protected static readonly Color BorderDefault = new Color(0.25f, 0.25f, 0.28f);
        protected static readonly Color InputBackground = new Color(0.12f, 0.12f, 0.14f);

        protected void StyleTextField(TextField textField)
        {
            // Style the text input element
            var textInput = textField.Q<VisualElement>("unity-text-input");
            if (textInput != null)
            {
                StyleInputElement(textInput);
            }

            // Focus styling - purple highlight
            textField.RegisterCallback<FocusInEvent>(evt =>
            {
                var input = textField.Q<VisualElement>("unity-text-input");
                if (input != null)
                {
                    SetInputBorderColor(input, AccentPurple);
                }
            });

            textField.RegisterCallback<FocusOutEvent>(evt =>
            {
                var input = textField.Q<VisualElement>("unity-text-input");
                if (input != null)
                {
                    SetInputBorderColor(input, BorderDefault);
                }
            });
        }

        protected void StylePropertyFieldInputs(VisualElement propertyField)
        {
            // Find all text inputs within the PropertyField
            var textInputs = propertyField.Query<VisualElement>("unity-text-input").ToList();
            foreach (var textInput in textInputs)
            {
                StyleInputElement(textInput);

                // Get the parent TextField to register focus events
                var parentField = textInput.parent;
                while (parentField != null && !(parentField is TextField))
                {
                    parentField = parentField.parent;
                }

                if (parentField is TextField textField)
                {
                    textField.RegisterCallback<FocusInEvent>(evt =>
                    {
                        SetInputBorderColor(textInput, AccentPurple);
                    });

                    textField.RegisterCallback<FocusOutEvent>(evt =>
                    {
                        SetInputBorderColor(textInput, BorderDefault);
                    });
                }
            }

            // Find and style all dropdown/popup field inputs
            StyleDropdownsInElement(propertyField);
        }

        /// <summary>
        /// Styles dropdown fields (EnumField, PopupField) within an element.
        /// </summary>
        protected void StyleDropdownsInElement(VisualElement container)
        {
            // Find popup field inputs (covers EnumField, PopupField, etc.)
            var popupInputs = container.Query<VisualElement>(className: "unity-base-popup-field__input").ToList();
            foreach (var popupInput in popupInputs)
            {
                StyleDropdownInput(popupInput);

                // Get the parent popup field for focus events
                var parentField = popupInput.parent;
                while (parentField != null && !parentField.ClassListContains("unity-base-popup-field"))
                {
                    parentField = parentField.parent;
                }

                if (parentField != null)
                {
                    parentField.RegisterCallback<FocusInEvent>(evt =>
                    {
                        SetInputBorderColor(popupInput, AccentPurple);
                    });

                    parentField.RegisterCallback<FocusOutEvent>(evt =>
                    {
                        SetInputBorderColor(popupInput, BorderDefault);
                    });

                    // Also handle mouse hover for visual feedback
                    popupInput.RegisterCallback<MouseEnterEvent>(evt =>
                    {
                        if (!parentField.focusController?.focusedElement?.Equals(parentField) ?? true)
                        {
                            SetInputBorderColor(popupInput, new Color(0.35f, 0.35f, 0.38f));
                        }
                    });

                    popupInput.RegisterCallback<MouseLeaveEvent>(evt =>
                    {
                        if (!parentField.focusController?.focusedElement?.Equals(parentField) ?? true)
                        {
                            SetInputBorderColor(popupInput, BorderDefault);
                        }
                    });
                }
            }
        }

        private void StyleDropdownInput(VisualElement input)
        {
            input.style.backgroundColor = InputBackground;
            input.style.borderTopLeftRadius = 6;
            input.style.borderTopRightRadius = 6;
            input.style.borderBottomLeftRadius = 6;
            input.style.borderBottomRightRadius = 6;
            input.style.borderTopWidth = 1;
            input.style.borderBottomWidth = 1;
            input.style.borderLeftWidth = 1;
            input.style.borderRightWidth = 1;
            SetInputBorderColor(input, BorderDefault);
            input.style.paddingTop = 6;
            input.style.paddingBottom = 6;
            input.style.paddingLeft = 12;
            input.style.paddingRight = 8;
            input.style.minHeight = 28;

            // Style the text label inside the dropdown
            var textElement = input.Q<TextElement>();
            if (textElement != null)
            {
                textElement.style.color = Color.white;
            }

            // Style the arrow
            var arrow = input.Q<VisualElement>(className: "unity-base-popup-field__arrow");
            if (arrow != null)
            {
                arrow.style.unityBackgroundImageTintColor = new Color(0.7f, 0.7f, 0.7f);
            }
        }

        private void StyleInputElement(VisualElement input)
        {
            input.style.backgroundColor = InputBackground;
            input.style.borderTopLeftRadius = 6;
            input.style.borderTopRightRadius = 6;
            input.style.borderBottomLeftRadius = 6;
            input.style.borderBottomRightRadius = 6;
            input.style.borderTopWidth = 1;
            input.style.borderBottomWidth = 1;
            input.style.borderLeftWidth = 1;
            input.style.borderRightWidth = 1;
            SetInputBorderColor(input, BorderDefault);
            input.style.paddingTop = 8;
            input.style.paddingBottom = 8;
            input.style.paddingLeft = 12;
            input.style.paddingRight = 12;
        }

        private void SetInputBorderColor(VisualElement input, Color color)
        {
            input.style.borderTopColor = color;
            input.style.borderBottomColor = color;
            input.style.borderLeftColor = color;
            input.style.borderRightColor = color;
        }
    }
}
