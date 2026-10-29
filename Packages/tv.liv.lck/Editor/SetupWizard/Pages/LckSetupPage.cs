using Liv.Lck.Audio;
using Liv.Lck.Audio.FMOD;
using Liv.Lck.Audio.Wwise;
using Liv.Lck.Settings;
using Liv.Lck.Util;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Liv.Lck
{
    /// <summary>
    /// Setup page for guided configuration of Tracking ID, Toolkit, and Audio.
    /// </summary>
    public class LckSetupPage : LckSettingsPageBase
    {
        public enum InteractionToolkitType
        {
            None,
            UnityXR,
            MetaXR,
            Custom
        }

        public enum UnityXRInteractionMethodType
        {
            None,
            RayBased,
            DirectTouch
        }

        public enum AudioSystemType
        {
            None,
            DefaultUnityAudio,
            FMOD,
            Wwise,
            Other
        }

        private InteractionToolkitType _selectedToolkit = InteractionToolkitType.None;
        private UnityXRInteractionMethodType _selectedUnityXRMethod = UnityXRInteractionMethodType.None;
        private AudioSystemType _selectedAudioSystem = AudioSystemType.None;

        private VisualElement _contentArea;
        private ScrollView _scrollView;
        private TextField _trackingIdField;
        private VisualElement _trackingIdValidationContainer;

        // Dynamic content containers (updated without full page rebuild)
        private VisualElement _toolkitCardsContainer;
        private VisualElement _unityXRMethodContainer;
        private VisualElement _toolkitSetupSection;
        private VisualElement _audioCardsContainer;
        private VisualElement _audioSetupSection;

        // Card references for updating selected state
        private Dictionary<InteractionToolkitType, VisualElement> _toolkitCards = new Dictionary<InteractionToolkitType, VisualElement>();
        private Dictionary<UnityXRInteractionMethodType, VisualElement> _methodCards = new Dictionary<UnityXRInteractionMethodType, VisualElement>();
        private Dictionary<AudioSystemType, VisualElement> _audioCards = new Dictionary<AudioSystemType, VisualElement>();

        [SettingsProvider]
        public static SettingsProvider CreateSettingsProvider()
        {
            var provider = new LckSetupPage(
                "Project/LCK/2 Setup",
                SettingsScope.Project,
                new HashSet<string>(new[] { "LCK", "LIV", "Setup", "Tracking", "Toolkit", "Audio" })
            );
            provider.label = "Setup";
            return provider;
        }

        public LckSetupPage(string path, SettingsScope scopes, IEnumerable<string> keywords = null)
            : base(path, scopes, keywords)
        {
        }

        public override void OnActivate(string searchContext, VisualElement rootElement)
        {
            // Initialize selections from project state
            InitializeAudioSystemFromDefines();
            base.OnActivate(searchContext, rootElement);
        }

        protected override void BuildUI(VisualElement root)
        {
            _scrollView = CreateContentArea() as ScrollView;
            _contentArea = _scrollView.Q<VisualElement>(className: "lck-content-area");

            AddPageTitle(_contentArea, "Setup Guide");
            AddBodyText(_contentArea, "Configure the essential settings for LCK integration in your project.");

            AddSpacer(_contentArea, "lg");

            // Section 1: Tracking ID
            DrawTrackingIDSection(_contentArea);

            AddSpacer(_contentArea, "lg");

            // Section 2: Interaction Toolkit
            DrawToolkitSection(_contentArea);

            AddSpacer(_contentArea, "lg");

            // Section 3: Audio System
            DrawAudioSection(_contentArea);

            AddSpacer(_contentArea, "lg");

            // Next steps
            AddSpacer(_contentArea, "md");
            AddSectionHeader(_contentArea, "Next Steps");

            VisualElement actionRow = new VisualElement();
            actionRow.style.flexDirection = FlexDirection.Row;
            actionRow.style.flexWrap = Wrap.Wrap;
            actionRow.style.marginTop = 8;

            Button validateButton = CreateAccentButton("Validate Project Setup", () =>
                SettingsService.OpenProjectSettings("Project/LCK/4 Validation"));
            validateButton.style.marginRight = 12;
            validateButton.style.marginBottom = 8;
            actionRow.Add(validateButton);

            Button configButton = CreateSecondaryButton("Advanced Configuration", () =>
                SettingsService.OpenProjectSettings("Project/LCK/3 Configuration"));
            configButton.style.marginBottom = 8;
            actionRow.Add(configButton);

            _contentArea.Add(actionRow);

            root.Add(_scrollView);
        }

        private void ScrollToElement(VisualElement element)
        {
            if (element == null || _scrollView == null) return;

            _scrollView.schedule.Execute(() =>
            {
                _scrollView.ScrollTo(element);
            }).StartingIn(50);
        }

        #region Game Identity Section

        private const string DefaultGameName = "MyGame";
        private const string DefaultFilenamePrefix = "MyGamePrefix";
        private const string DefaultAlbumName = "MyGameAlbum";

        private void DrawTrackingIDSection(VisualElement parent)
        {
            AddSectionHeader(parent, "1. Game Identity", true);

            AddBodyText(parent,
                "Configure your game's identity for LCK recordings and analytics.");

            var card = CreateCard();

            // Game Name
            Label gameNameLabel = new Label("Game Name");
            gameNameLabel.style.color = Color.white;
            gameNameLabel.style.marginBottom = 8;
            gameNameLabel.style.fontSize = 14;
            gameNameLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            card.Add(gameNameLabel);

            TextField gameNameField = new TextField();
            var initialSettings = LckSettings.Instance;
            gameNameField.value = initialSettings != null ? initialSettings.GameName : "";
            gameNameField.style.marginBottom = 4;
            StyleTextField(gameNameField);

            gameNameField.RegisterValueChangedCallback(evt =>
            {
                try
                {
                    string newName = evt.newValue.Trim();
                    var settings = LckSettings.Instance;
                    if (settings == null)
                    {
                        Debug.LogWarning("[LckSetupPage] LckSettings.Instance is null, cannot save game name.");
                        return;
                    }

                    string oldName = settings.GameName;
                    string currentPrefix = settings.RecordingFilenamePrefix;
                    string currentAlbum = settings.RecordingAlbumName;

                    settings.GameName = newName;

                    // Update filename prefix if it's still the default or based on old name
                    if (IsDefaultOrDerivedPrefix(currentPrefix, oldName) && !string.IsNullOrEmpty(newName))
                    {
                        settings.RecordingFilenamePrefix = newName.Replace(" ", "") + "Clip";
                    }

                    // Update album name if it's still the default or based on old name
                    if (IsDefaultOrDerivedAlbum(currentAlbum, oldName) && !string.IsNullOrEmpty(newName))
                    {
                        settings.RecordingAlbumName = newName;
                    }

                    EditorUtility.SetDirty(settings);
                    AssetDatabase.SaveAssetIfDirty(settings);
                }
                catch (System.Exception ex)
                {
                    Debug.LogError($"[LckSetupPage] Failed to save game name: {ex.Message}");
                }
            });

            card.Add(gameNameField);

            Label gameNameHint = new Label("This name appears in recordings and is used for file naming.");
            gameNameHint.AddToClassList("lck-body-text");
            gameNameHint.style.fontSize = 11;
            gameNameHint.style.marginBottom = 16;
            card.Add(gameNameHint);

            // Tracking ID section
            Label trackingHeader = new Label("Tracking ID");
            trackingHeader.style.color = Color.white;
            trackingHeader.style.marginBottom = 8;
            trackingHeader.style.marginTop = 8;
            trackingHeader.style.fontSize = 14;
            trackingHeader.style.unityFontStyleAndWeight = FontStyle.Bold;
            card.Add(trackingHeader);

            AddBodyText(card,
                "LCK uses a Tracking ID to provide you with stats from your game, including " +
                "number of recordings, total hours recorded, number of streams, photos taken, and more.");

            AddSpacer(card, "sm");

            // Instructions
            Label instructions = new Label("To find your unique Tracking ID:");
            instructions.AddToClassList("lck-body-text");
            instructions.style.marginBottom = 8;
            card.Add(instructions);

            AddNumberedStep(card, "1", "Visit the", "https://dev.liv.tv", "LIV Dashboard");
            AddNumberedStep(card, "2", "Sign in or create a developer account");
            AddNumberedStep(card, "3", "Create a new game or select existing");
            AddNumberedStep(card, "4", "Copy your Tracking ID from the dashboard");

            AddSpacer(card, "md");

            // Input field
            Label inputLabel = new Label("Tracking ID");
            inputLabel.style.color = Color.white;
            inputLabel.style.marginBottom = 4;
            card.Add(inputLabel);

            _trackingIdField = new TextField();
            var trackingSettings = LckSettings.Instance;
            _trackingIdField.value = trackingSettings != null ? trackingSettings.TrackingId : "";
            _trackingIdField.style.marginBottom = 8;
            StyleTextField(_trackingIdField);

            _trackingIdField.RegisterValueChangedCallback(evt =>
            {
                try
                {
                    var settings = LckSettings.Instance;
                    if (settings != null)
                    {
                        settings.TrackingId = evt.newValue.Trim();
                        EditorUtility.SetDirty(settings);
                        AssetDatabase.SaveAssetIfDirty(settings);
                    }
                    UpdateTrackingIdValidationStatus();
                }
                catch (System.Exception ex)
                {
                    Debug.LogError($"[LckSetupPage] Failed to save tracking ID: {ex.Message}");
                }
            });

            card.Add(_trackingIdField);

            // Validation status container
            _trackingIdValidationContainer = new VisualElement();
            card.Add(_trackingIdValidationContainer);
            UpdateTrackingIdValidationStatus();

            parent.Add(card);
        }

        private void UpdateTrackingIdValidationStatus()
        {
            if (_trackingIdValidationContainer == null) return;

            _trackingIdValidationContainer.Clear();

            try
            {
                var settings = LckSettings.Instance;
                bool hasTrackingId = settings != null && !string.IsNullOrEmpty(settings.TrackingId);

                if (hasTrackingId)
                {
                    var successBox = CreateSuccessBox("Tracking ID is configured");
                    _trackingIdValidationContainer.Add(successBox);
                }
                else
                {
                    var warningBox = CreateWarningBox("Tracking ID is required for LCK to function. Please configure your Tracking ID above.");
                    _trackingIdValidationContainer.Add(warningBox);
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[LckSetupPage] Error updating tracking ID validation: {ex.Message}");
                var warningBox = CreateWarningBox("Unable to check Tracking ID status.");
                _trackingIdValidationContainer.Add(warningBox);
            }
        }

        private bool IsDefaultOrDerivedPrefix(string prefix, string gameName)
        {
            if (string.IsNullOrEmpty(prefix)) return true;
            if (prefix == DefaultFilenamePrefix) return true;

            // Check if prefix is just the game name (with or without spaces)
            string gameNameNoSpaces = gameName.Replace(" ", "");
            if (prefix == gameName || prefix == gameNameNoSpaces) return true;

            // Check derived patterns
            if (prefix == gameNameNoSpaces + "Clip") return true;
            if (prefix == gameNameNoSpaces + "Prefix") return true;

            return false;
        }

        private bool IsDefaultOrDerivedAlbum(string album, string gameName)
        {
            if (string.IsNullOrEmpty(album)) return true;
            if (album == DefaultAlbumName) return true;
            if (album == gameName) return true;
            if (album == gameName + "Album") return true;

            return false;
        }

        private void AddNumberedStep(VisualElement parent, string number, string text, string url = null, string buttonText = null)
        {
            VisualElement stepContainer = new VisualElement();
            stepContainer.style.flexDirection = FlexDirection.Row;
            stepContainer.style.alignItems = Align.Center;
            stepContainer.style.marginBottom = 12;

            // Step number circle (purple)
            VisualElement numberCircle = new VisualElement();
            numberCircle.style.width = 24;
            numberCircle.style.height = 24;
            numberCircle.style.borderTopLeftRadius = 12;
            numberCircle.style.borderTopRightRadius = 12;
            numberCircle.style.borderBottomLeftRadius = 12;
            numberCircle.style.borderBottomRightRadius = 12;
            numberCircle.style.backgroundColor = AccentPurple;
            numberCircle.style.alignItems = Align.Center;
            numberCircle.style.justifyContent = Justify.Center;
            numberCircle.style.marginRight = 12;
            numberCircle.style.flexShrink = 0;

            Label numberLabel = new Label(number);
            numberLabel.style.color = Color.white;
            numberLabel.style.fontSize = 12;
            numberLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            numberLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            numberCircle.Add(numberLabel);

            stepContainer.Add(numberCircle);

            // Step content
            VisualElement contentContainer = new VisualElement();
            contentContainer.style.flexGrow = 1;

            // If we have both text and a button, show text followed by button inline
            if (url != null && buttonText != null)
            {
                VisualElement textRow = new VisualElement();
                textRow.style.flexDirection = FlexDirection.Row;
                textRow.style.alignItems = Align.Center;
                textRow.style.flexWrap = Wrap.Wrap;

                Label textLabel = new Label(text);
                textLabel.AddToClassList("lck-body-text");
                textLabel.style.marginBottom = 0;
                textLabel.style.marginRight = 8;
                textRow.Add(textLabel);

                Button linkButton = new Button(() => Application.OpenURL(url));
                linkButton.text = buttonText;
                linkButton.AddToClassList("lck-button");
                linkButton.AddToClassList("lck-button--small");
                linkButton.AddToClassList("lck-button--primary");
                textRow.Add(linkButton);

                contentContainer.Add(textRow);
            }
            else
            {
                Label textLabel = new Label(text);
                textLabel.AddToClassList("lck-body-text");
                textLabel.style.marginBottom = 0;
                contentContainer.Add(textLabel);
            }

            stepContainer.Add(contentContainer);
            parent.Add(stepContainer);
        }

        #endregion

        #region Toolkit Section

        private void DrawToolkitSection(VisualElement parent)
        {
            AddSectionHeader(parent, "2. Interaction Toolkit");

            AddBodyText(parent,
                "Choose which XR interaction system your project uses. " +
                "This determines which tablet prefab variant to use.");

            // Container for toolkit cards
            _toolkitCardsContainer = new VisualElement();
            _toolkitCards.Clear();
            parent.Add(_toolkitCardsContainer);

            // Unity XR Option
            AddToolkitOption(_toolkitCardsContainer,
                "Unity XR Interaction Toolkit",
                "Standard Unity XR framework with ray-based or direct touch interactions.",
                InteractionToolkitType.UnityXR,
                "Packages/tv.liv.lck/Runtime/Art/Textures/DirectTabletClick.jpg");

            // Meta XR Option
            AddToolkitOption(_toolkitCardsContainer,
                "Meta XR Interaction SDK",
                "Meta's interaction SDK (formerly Oculus Interaction) with hand tracking support.",
                InteractionToolkitType.MetaXR,
                "Packages/tv.liv.lck/Runtime/Art/Textures/MetaTabletClick.png");

            // Custom Option
            AddToolkitOption(_toolkitCardsContainer,
                "Custom / Other",
                "No specific toolkit, or custom interaction system. Manual setup required.",
                InteractionToolkitType.Custom,
                "Packages/tv.liv.lck/Runtime/Art/Textures/CustomTablet.png");

            // Container for Unity XR method options (shown when Unity XR is selected)
            _unityXRMethodContainer = new VisualElement();
            _methodCards.Clear();
            parent.Add(_unityXRMethodContainer);

            if (_selectedToolkit == InteractionToolkitType.UnityXR)
            {
                DrawUnityXRMethodOptions(_unityXRMethodContainer);
            }

            // Container for tablet setup section based on selection
            _toolkitSetupSection = new VisualElement();
            _toolkitSetupSection.name = "toolkit-setup-section";
            parent.Add(_toolkitSetupSection);

            UpdateToolkitSetupSection();
        }

        private void UpdateToolkitSetupSection()
        {
            _toolkitSetupSection.Clear();

            if (_selectedToolkit == InteractionToolkitType.UnityXR && _selectedUnityXRMethod != UnityXRInteractionMethodType.None)
            {
                DrawUnityXRTabletSetup(_toolkitSetupSection, _selectedUnityXRMethod);
            }
            else if (_selectedToolkit == InteractionToolkitType.MetaXR)
            {
                DrawMetaXRTabletSetup(_toolkitSetupSection);
            }
            else if (_selectedToolkit == InteractionToolkitType.Custom)
            {
                DrawCustomTabletSetup(_toolkitSetupSection);
            }
        }

        private void UpdateToolkitCardSelection(InteractionToolkitType newSelection)
        {
            // Update card selected states
            foreach (var kvp in _toolkitCards)
            {
                if (kvp.Key == newSelection)
                    kvp.Value.AddToClassList("lck-card--selected");
                else
                    kvp.Value.RemoveFromClassList("lck-card--selected");
            }

            // Update Unity XR method container
            _unityXRMethodContainer.Clear();
            _methodCards.Clear();
            if (newSelection == InteractionToolkitType.UnityXR)
            {
                DrawUnityXRMethodOptions(_unityXRMethodContainer);
            }

            // Update setup section
            UpdateToolkitSetupSection();
        }

        private void UpdateMethodCardSelection(UnityXRInteractionMethodType newSelection)
        {
            // Update card selected states
            foreach (var kvp in _methodCards)
            {
                if (kvp.Key == newSelection)
                    kvp.Value.AddToClassList("lck-card--selected");
                else
                    kvp.Value.RemoveFromClassList("lck-card--selected");
            }

            // Update setup section
            UpdateToolkitSetupSection();
        }

        private void AddToolkitOption(VisualElement parent, string title, string description,
            InteractionToolkitType toolkitType, string imagePath)
        {
            VisualElement card = new VisualElement();
            card.AddToClassList("lck-card");
            card.AddToClassList("lck-card--horizontal");
            card.AddToClassList("lck-card--clickable");
            ApplyCardFallbackStyles(card);

            if (_selectedToolkit == toolkitType)
            {
                card.AddToClassList("lck-card--selected");
            }

            card.RegisterCallback<ClickEvent>(evt =>
            {
                _selectedToolkit = toolkitType;
                _selectedUnityXRMethod = UnityXRInteractionMethodType.None;
                UpdateToolkitCardSelection(toolkitType);
            });

            // Store card reference for later selection updates
            _toolkitCards[toolkitType] = card;

            // Image
            if (!string.IsNullOrEmpty(imagePath))
            {
                Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(imagePath);
                if (texture != null)
                {
                    // Outer container with overflow hidden for masked zoom effect
                    VisualElement imageWrapper = new VisualElement();
                    imageWrapper.AddToClassList("lck-card-image-wrapper");

                    // Inner image element that scales on hover
                    VisualElement imageInner = new VisualElement();
                    imageInner.AddToClassList("lck-card-image");
                    imageInner.style.backgroundImage = new StyleBackground(texture);
#if UNITY_2022_1_OR_NEWER
                    imageInner.style.backgroundSize = new BackgroundSize(BackgroundSizeType.Cover);
                    imageInner.style.backgroundPositionX = new BackgroundPosition(BackgroundPositionKeyword.Center);
                    imageInner.style.backgroundPositionY = new BackgroundPosition(BackgroundPositionKeyword.Center);
#endif

                    imageWrapper.Add(imageInner);
                    card.Add(imageWrapper);
                }
            }

            // Text content
            VisualElement textContainer = new VisualElement();
            textContainer.AddToClassList("lck-card-content");

            Label titleLabel = new Label(title);
            titleLabel.AddToClassList("lck-card-title");
            textContainer.Add(titleLabel);

            Label descLabel = new Label(description);
            descLabel.AddToClassList("lck-card-description");
            textContainer.Add(descLabel);

            card.Add(textContainer);
            parent.Add(card);
        }

        private void DrawUnityXRMethodOptions(VisualElement parent)
        {
            AddSpacer(parent, "sm");

            Label subHeader = new Label("Select Interaction Method:");
            subHeader.style.color = Color.white;
            subHeader.style.marginBottom = 8;
            subHeader.style.marginLeft = 16;
            parent.Add(subHeader);

            VisualElement optionsContainer = new VisualElement();
            optionsContainer.style.marginLeft = 16;

            AddMethodOption(optionsContainer, "Ray-Based (Near-Far Interactors)",
                "Use ray casting for tablet interaction", UnityXRInteractionMethodType.RayBased);

            AddMethodOption(optionsContainer, "Direct Touch (Poke / Collider)",
                "Use direct touch or collider-based interaction", UnityXRInteractionMethodType.DirectTouch);

            parent.Add(optionsContainer);
        }

        private void AddMethodOption(VisualElement parent, string title, string description, UnityXRInteractionMethodType methodType)
        {
            VisualElement card = new VisualElement();
            card.AddToClassList("lck-card");
            card.AddToClassList("lck-card--clickable");
            ApplyCardFallbackStyles(card);

            if (_selectedUnityXRMethod == methodType)
            {
                card.AddToClassList("lck-card--selected");
            }

            card.RegisterCallback<ClickEvent>(evt =>
            {
                _selectedUnityXRMethod = methodType;
                UpdateMethodCardSelection(methodType);
            });

            // Store card reference for later selection updates
            _methodCards[methodType] = card;

            Label titleLabel = new Label(title);
            titleLabel.AddToClassList("lck-card-title");
            card.Add(titleLabel);

            Label descLabel = new Label(description);
            descLabel.AddToClassList("lck-card-description");
            card.Add(descLabel);

            parent.Add(card);
        }

        private void DrawUnityXRTabletSetup(VisualElement parent, UnityXRInteractionMethodType methodType)
        {
            AddSpacer(parent, "md");

            var card = CreateCard();
            card.style.marginLeft = 16;

            string prefabName, prefabPath, variantName;
            if (methodType == UnityXRInteractionMethodType.RayBased)
            {
                Label title = new Label("Ray-Based Tablet Setup");
                title.AddToClassList("lck-card-title");
                card.Add(title);

                AddBodyText(card, "Configure your tablet for ray-based interactions.");

                prefabName = "LCK Tablet";
                prefabPath = "Packages/tv.liv.lck/Runtime/Prefabs/Tablets/XR Interaction Toolkit Tablets/LCK Tablet.prefab";
                variantName = "Assets/LCK Tablet Variant.prefab";
            }
            else
            {
                Label title = new Label("Direct Touch Tablet Setup");
                title.AddToClassList("lck-card-title");
                card.Add(title);

                AddBodyText(card, "Configure your tablet for direct touch interactions.");

                prefabName = "LCK Tablet For Direct";
                prefabPath = "Packages/tv.liv.lck/Runtime/Prefabs/Tablets/XR Interaction Toolkit Tablets/LCK Tablet For Direct.prefab";
                variantName = "Assets/LCK Tablet For Direct Variant.prefab";
            }

            DrawPrefabSetupUI(card, prefabName, prefabPath, variantName);
            parent.Add(card);
        }

        private void DrawMetaXRTabletSetup(VisualElement parent)
        {
            AddSpacer(parent, "md");

            var card = CreateCard();
            card.style.marginLeft = 16;

            Label title = new Label("Meta XR Tablet Setup");
            title.AddToClassList("lck-card-title");
            card.Add(title);

            AddBodyText(card, "Configure your tablet for Meta XR Interaction SDK.");

            DrawPrefabSetupUI(card,
                "LCK Meta Tablet",
                "Packages/tv.liv.lck/Runtime/Prefabs/Tablets/Meta XR Interaction SDK Tablets/LCK Meta Tablet.prefab",
                "Assets/LCK Meta Tablet Variant.prefab");

            parent.Add(card);
        }

        private void DrawCustomTabletSetup(VisualElement parent)
        {
            AddSpacer(parent, "md");

            var card = CreateCard();
            card.style.marginLeft = 16;

            Label title = new Label("Custom Tablet Setup");
            title.AddToClassList("lck-card-title");
            card.Add(title);

            AddBodyText(card, "Configure your tablet for a custom interaction system. You'll need to add your own interaction components.");

            DrawPrefabSetupUI(card,
                "Base LCK Tablet",
                "Packages/tv.liv.lck/Runtime/Prefabs/Tablets/Base LCK Tablet.prefab",
                "Assets/Base LCK Tablet Variant.prefab");

            parent.Add(card);
        }

        private void DrawPrefabSetupUI(VisualElement parent, string prefabName, string prefabPath, string variantPath)
        {
            AddSpacer(parent, "sm");

            Label pathLabel = new Label($"The {prefabName} prefab is located at:\n{System.IO.Path.GetDirectoryName(prefabPath).Replace("\\", "/")}");
            pathLabel.AddToClassList("lck-body-text");
            pathLabel.style.whiteSpace = WhiteSpace.Normal;
            parent.Add(pathLabel);

            AddSpacer(parent, "sm");

            VisualElement buttonRow = new VisualElement();
            buttonRow.style.flexDirection = FlexDirection.Row;
            buttonRow.style.flexWrap = Wrap.Wrap;

            Button showButton = CreateSecondaryButton("Show in Project", () => SelectPrefabInProject(prefabPath));
            showButton.style.marginRight = 8;
            showButton.style.marginBottom = 8;
            buttonRow.Add(showButton);

            parent.Add(buttonRow);

            AddSpacer(parent, "sm");

            AddBodyText(parent,
                "We suggest using a Prefab Variant of this tablet as the one you want to use throughout your project. " +
                "Using a variant allows you to make any changes you want to the tablet while still keeping an original default copy of the prefab in the LCK package. " +
                "Also when updating the LCK package you will be able to keep any changes made on your prefab variant.");

            AddSpacer(parent, "sm");

            Button createVariantButton = CreateAccentButton("Create Prefab Variant", () => CreatePrefabVariant(prefabPath, variantPath));
            parent.Add(createVariantButton);
        }

        #endregion

        #region Audio Section

        private void DrawAudioSection(VisualElement parent)
        {
            AddSectionHeader(parent, "3. Audio System");

            AddBodyText(parent,
                "Select the audio system your project uses. LCK needs to know this to capture game audio correctly.");

            // Container for audio cards
            _audioCardsContainer = new VisualElement();
            _audioCards.Clear();
            parent.Add(_audioCardsContainer);

            // Default Unity Audio
            AddAudioOption(_audioCardsContainer, "Default Unity Audio",
                "Standard Unity audio system using AudioSource and AudioListener.",
                AudioSystemType.DefaultUnityAudio);

            // FMOD
            AddAudioOption(_audioCardsContainer, "FMOD",
                "FMOD audio middleware for advanced audio features.",
                AudioSystemType.FMOD);

            // Wwise
            AddAudioOption(_audioCardsContainer, "Wwise",
                "Audiokinetic Wwise audio middleware.",
                AudioSystemType.Wwise);

            // Custom
            AddAudioOption(_audioCardsContainer, "Other / Custom",
                "Custom audio system or other third-party audio solution.",
                AudioSystemType.Other);

            // Audio setup section
            _audioSetupSection = new VisualElement();
            _audioSetupSection.name = "audio-setup-section";
            parent.Add(_audioSetupSection);

            UpdateAudioSetupSection();
        }

        private void UpdateAudioSetupSection()
        {
            _audioSetupSection.Clear();

            // Show detailed setup based on selection
            if (_selectedAudioSystem == AudioSystemType.DefaultUnityAudio)
            {
                DrawDefaultUnityAudioSetup(_audioSetupSection);
            }
            else if (_selectedAudioSystem == AudioSystemType.FMOD)
            {
                DrawFMODSetup(_audioSetupSection);
            }
            else if (_selectedAudioSystem == AudioSystemType.Wwise)
            {
                DrawWwiseSetup(_audioSetupSection);
            }
            else if (_selectedAudioSystem == AudioSystemType.Other)
            {
                DrawOtherAudioSetup(_audioSetupSection);
            }
        }

        private void UpdateAudioCardSelection(AudioSystemType newSelection)
        {
            // Update card selected states
            foreach (var kvp in _audioCards)
            {
                if (kvp.Key == newSelection)
                    kvp.Value.AddToClassList("lck-card--selected");
                else
                    kvp.Value.RemoveFromClassList("lck-card--selected");
            }

            // Update setup section
            UpdateAudioSetupSection();
        }

        private void AddAudioOption(VisualElement parent, string title, string description, AudioSystemType audioType)
        {
            VisualElement card = new VisualElement();
            card.AddToClassList("lck-card");
            card.AddToClassList("lck-card--clickable");
            ApplyCardFallbackStyles(card);

            if (_selectedAudioSystem == audioType)
            {
                card.AddToClassList("lck-card--selected");
            }

            card.RegisterCallback<ClickEvent>(evt =>
            {
                _selectedAudioSystem = audioType;
                UpdateAudioCardSelection(audioType);
            });

            // Store card reference for later selection updates
            _audioCards[audioType] = card;

            Label titleLabel = new Label(title);
            titleLabel.AddToClassList("lck-card-title");
            card.Add(titleLabel);

            Label descLabel = new Label(description);
            descLabel.AddToClassList("lck-card-description");
            card.Add(descLabel);

            parent.Add(card);
        }

        private void DrawDefaultUnityAudioSetup(VisualElement parent)
        {
            // Check if any non-default audio defines are present
            bool hasFmodDefines = ScriptingDefineUtils.HasDefine("LCK_FMOD") ||
                                  ScriptingDefineUtils.HasDefine("LCK_FMOD_2_03") ||
                                  ScriptingDefineUtils.HasDefine("LCK_FMOD_WITH_UNITY_AUDIO");
            bool hasWwiseDefines = ScriptingDefineUtils.HasDefine("LCK_WWISE");
            bool hasOtherDefines = ScriptingDefineUtils.HasDefine("LCK_NOT_UNITY_AUDIO");

            bool hasAnyAudioDefines = hasFmodDefines || hasWwiseDefines || hasOtherDefines;

            if (hasAnyAudioDefines)
            {
                AddSpacer(parent, "md");

                var card = CreateCard();
                card.style.marginLeft = 16;

                Label title = new Label("Reset to Default Unity Audio");
                title.AddToClassList("lck-card-title");
                card.Add(title);

                AddBodyText(card, "Your project has scripting defines for a different audio system. " +
                    "Click the button below to remove these defines and use default Unity audio.");

                Button resetButton = CreateAccentButton("Remove Audio Defines", () =>
                {
                    // Remove all audio-related defines
                    ScriptingDefineUtils.RemoveDefine("LCK_FMOD");
                    ScriptingDefineUtils.RemoveDefine("LCK_FMOD_2_03");
                    ScriptingDefineUtils.RemoveDefine("LCK_FMOD_WITH_UNITY_AUDIO");
                    ScriptingDefineUtils.RemoveDefine("LCK_WWISE");
                    ScriptingDefineUtils.RemoveDefine("LCK_NOT_UNITY_AUDIO");

                    // Refresh the section to update the UI
                    UpdateAudioSetupSection();
                });
                card.Add(resetButton);

                parent.Add(card);
            }
        }

        private void DrawFMODSetup(VisualElement parent)
        {
            AddSpacer(parent, "md");

            var card = CreateCard();
            card.style.marginLeft = 16;

            Label title = new Label("FMOD Audio Setup");
            title.AddToClassList("lck-card-title");
            card.Add(title);

            string fmodVersionString = GetFMODVersionString();
            bool fmodVersionDetected = fmodVersionString != "Version Unknown";

            if (fmodVersionDetected)
            {
                bool isFMOD203OrAbove = IsFMODVersion203OrAbove(fmodVersionString);

                Label versionLabel = new Label($"FMOD Detected Version: {fmodVersionString}");
                versionLabel.style.fontSize = 14;
                versionLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
                versionLabel.style.color = new Color(0.831f, 1f, 0.165f);
                versionLabel.style.marginTop = 10;
                versionLabel.style.marginBottom = 10;
                card.Add(versionLabel);

                AddBodyText(card, "LCK uses a scripting define for FMOD support. Press the button below to automatically add the necessary scripting define.");

                Button configureButton = CreateAccentButton(
                    isFMOD203OrAbove ? "Configure for FMOD 2.03+" : "Configure for FMOD (Pre-2.03)",
                    () =>
                    {
                        if (isFMOD203OrAbove)
                            LckFMODAudioMenu.ConfigureForFMOD2_03AndAbove();
                        else
                            LckFMODAudioMenu.ConfigureForFMODPre2_03();
                    });
                configureButton.style.marginBottom = 15;
                card.Add(configureButton);
            }
            else
            {
                Label notDetectedLabel = new Label("FMOD Version Not Detected");
                notDetectedLabel.style.fontSize = 14;
                notDetectedLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
                notDetectedLabel.style.color = new Color(0.9f, 0.6f, 0.2f);
                notDetectedLabel.style.marginTop = 10;
                notDetectedLabel.style.marginBottom = 10;
                card.Add(notDetectedLabel);

                AddBodyText(card, "Unable to detect FMOD version automatically. Please select your FMOD version below.");

                AddSpacer(card, "sm");

                Label fmod203Label = new Label("For FMOD 2.03 and Above:");
                fmod203Label.AddToClassList("lck-body-text");
                fmod203Label.style.unityFontStyleAndWeight = FontStyle.Bold;
                card.Add(fmod203Label);

                Button configure203Button = CreateAccentButton("Configure for FMOD 2.03+", () =>
                    LckFMODAudioMenu.ConfigureForFMOD2_03AndAbove());
                configure203Button.style.marginBottom = 15;
                card.Add(configure203Button);

                Label fmodPre203Label = new Label("For FMOD Pre-2.03:");
                fmodPre203Label.AddToClassList("lck-body-text");
                fmodPre203Label.style.unityFontStyleAndWeight = FontStyle.Bold;
                card.Add(fmodPre203Label);

                Button configurePre203Button = CreateAccentButton("Configure for FMOD (Pre-2.03)", () =>
                    LckFMODAudioMenu.ConfigureForFMODPre2_03());
                configurePre203Button.style.marginBottom = 15;
                card.Add(configurePre203Button);
            }

            AddSpacer(card, "sm");

            AddBodyText(card, "LCK also requires an LckAudioMarker component to be on an active Game Object in the scene. " +
                "We suggest adding the component to the same Game Object that has the FMOD Studio Listener component.");

            DrawAudioMarkerStatus(card);

            AddSpacer(card, "md");

            AddBodyText(card, "LCK can also combine Unity audio capture with FMOD audio, if your project uses both audio solutions:");

            var currentOptions = LckAudioConfigurationUtils.FMODConfigurer.GetCurrentOptions();
            Toggle combineAudioToggle = new Toggle("Combine FMOD With Unity Audio");
            combineAudioToggle.value = currentOptions.CombineWithUnityAudio;
            combineAudioToggle.style.marginTop = 8;
            combineAudioToggle.RegisterValueChangedCallback(evt =>
            {
                var options = LckAudioConfigurationUtils.FMODConfigurer.GetCurrentOptions();
                options.CombineWithUnityAudio = evt.newValue;
                options.ShouldCapture = true;
                LckAudioConfigurationUtils.FMODConfigurer.Configure(options, shouldValidate: false);
            });
            card.Add(combineAudioToggle);

            parent.Add(card);
        }

        private void DrawWwiseSetup(VisualElement parent)
        {
            AddSpacer(parent, "md");

            var card = CreateCard();
            card.style.marginLeft = 16;

            Label title = new Label("Wwise Audio Setup");
            title.AddToClassList("lck-card-title");
            card.Add(title);

            AddBodyText(card, "LCK uses a scripting define for Wwise support. Press the button below to automatically add the necessary scripting define.");

            Button configureButton = CreateAccentButton("Configure for Wwise", () =>
                LckWwiseAudioMenu.ConfigureForWwiseAudioCapture());
            configureButton.style.marginBottom = 15;
            card.Add(configureButton);

            AddSpacer(card, "sm");

            AddBodyText(card, "LCK also requires an LckAudioMarker component to be on an active Game Object in the scene. " +
                "We suggest adding the component to the same Game Object that has the Wwise AkAudioListener component.");

            DrawAudioMarkerStatus(card);

            parent.Add(card);
        }

        private void DrawOtherAudioSetup(VisualElement parent)
        {
            AddSpacer(parent, "md");

            var card = CreateCard();
            card.style.marginLeft = 16;

            Label title = new Label("Custom Audio Setup");
            title.AddToClassList("lck-card-title");
            card.Add(title);

            AddBodyText(card, "For custom audio systems, you can disable Unity's default audio capture. " +
                "You'll need to implement ILckAudioSource to supply audio data to LCK.");

            Button configureButton = CreateAccentButton("Remove Unity Audio Capture From LCK", () =>
            {
                ScriptingDefineUtils.AddDefine("LCK_NOT_UNITY_AUDIO");
            });
            configureButton.style.marginBottom = 15;
            card.Add(configureButton);

            AddSpacer(card, "sm");

            AddBodyText(card, "If you are not using a Unity AudioListener, LCK also requires an LckAudioMarker component to be on an active Game Object in the scene.");

            DrawAudioMarkerStatus(card);

            AddSpacer(card, "md");

            Button docsButton = CreateSecondaryButton("View Documentation", () =>
                Application.OpenURL("https://liv.mintlify.app/"));
            card.Add(docsButton);

            parent.Add(card);
        }

        private void DrawAudioMarkerStatus(VisualElement parent)
        {
            AddSpacer(parent, "sm");

            VisualElement audioMarkerContainer = new VisualElement();
            audioMarkerContainer.style.flexDirection = FlexDirection.Row;
            audioMarkerContainer.style.alignItems = Align.Center;

            Label audioMarkerStatusLabel = new Label();
            audioMarkerStatusLabel.style.marginRight = 10;
            audioMarkerContainer.Add(audioMarkerStatusLabel);

            Button searchAgainButton = new Button();
            searchAgainButton.text = "Search Again";
            searchAgainButton.AddToClassList("lck-button");
            searchAgainButton.AddToClassList("lck-button--secondary");
            searchAgainButton.AddToClassList("lck-button--small");
            audioMarkerContainer.Add(searchAgainButton);

            Action updateStatus = () =>
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

            searchAgainButton.clicked += updateStatus;
            updateStatus();

            parent.Add(audioMarkerContainer);
        }

        #endregion

        #region Helpers

        private void InitializeAudioSystemFromDefines()
        {
            string defines = PlayerSettings.GetScriptingDefineSymbolsForGroup(
                BuildPipeline.GetBuildTargetGroup(EditorUserBuildSettings.activeBuildTarget));

            if (defines.Contains("LCK_FMOD"))
            {
                _selectedAudioSystem = AudioSystemType.FMOD;
            }
            else if (defines.Contains("LCK_WWISE"))
            {
                _selectedAudioSystem = AudioSystemType.Wwise;
            }
            else
            {
                _selectedAudioSystem = AudioSystemType.DefaultUnityAudio;
            }
        }

        private bool IsFMODInProject()
        {
            try
            {
                var fmodAssembly = Assembly.Load("FMODUnity");
                return fmodAssembly != null;
            }
            catch
            {
                return false;
            }
        }

        private bool IsWwiseInProject()
        {
            try
            {
                var wwiseAssembly = Assembly.Load("AK.Wwise.Unity.API");
                return wwiseAssembly != null;
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
                int major = (versionNumber >> 16) & 0xFF;
                int minor = (versionNumber >> 8) & 0xFF;
                int patch = versionNumber & 0xFF;

                return $"{major}.{minor:D2}.{patch:D2}";
            }
            catch
            {
                return "Version Unknown";
            }
        }

        private bool IsFMODVersion203OrAbove(string versionString)
        {
            if (versionString == "Version Unknown") return false;

            try
            {
                var parts = versionString.Split('.');
                if (parts.Length != 3) return false;

                if (!int.TryParse(parts[0], out int major)) return false;
                if (!int.TryParse(parts[1], out int minor)) return false;

                if (major > 2) return true;
                if (major == 2 && minor >= 3) return true;

                return false;
            }
            catch
            {
                return false;
            }
        }

        private void SelectPrefabInProject(string assetPath)
        {
            UnityEngine.Object prefab = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(assetPath);
            if (prefab != null)
            {
                Selection.activeObject = prefab;
                EditorGUIUtility.PingObject(prefab);
            }
        }

        private void CreatePrefabVariant(string sourcePrefabPath, string variantPath)
        {
            // Load the source prefab
            GameObject sourcePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(sourcePrefabPath);
            if (sourcePrefab == null)
            {
                EditorUtility.DisplayDialog("Error", $"Could not find source prefab at path: {sourcePrefabPath}", "OK");
                return;
            }

            // Check if variant already exists
            if (AssetDatabase.LoadAssetAtPath<GameObject>(variantPath) != null)
            {
                bool overwrite = EditorUtility.DisplayDialog(
                    "Prefab Variant Exists",
                    $"A prefab variant already exists at {variantPath}. Do you want to overwrite it?",
                    "Overwrite",
                    "Cancel"
                );

                if (!overwrite) return;

                AssetDatabase.DeleteAsset(variantPath);
            }

            // Create the prefab variant
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(sourcePrefab);
            GameObject variant = PrefabUtility.SaveAsPrefabAsset(instance, variantPath);
            UnityEngine.Object.DestroyImmediate(instance);

            if (variant != null)
            {
                Selection.activeObject = variant;
                EditorGUIUtility.PingObject(variant);
            }
            else
            {
                EditorUtility.DisplayDialog("Error", $"Failed to create prefab variant at: {variantPath}", "OK");
            }
        }

        #endregion
    }
}
