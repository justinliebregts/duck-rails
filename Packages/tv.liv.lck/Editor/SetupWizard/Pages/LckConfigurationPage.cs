using Liv.Lck.Core;
using Liv.Lck.Settings;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Liv.Lck
{
    /// <summary>
    /// Configuration page for all LCK settings with styled UI.
    /// </summary>
    public class LckConfigurationPage : LckSettingsPageBase
    {
        private SerializedObject _serializedSettings;

        [SettingsProvider]
        public static SettingsProvider CreateSettingsProvider()
        {
            var provider = new LckConfigurationPage(
                "Project/LCK/3 Configuration",
                SettingsScope.Project,
                new HashSet<string>(new[] { "LCK", "LIV", "Configuration", "Settings", "Recording", "Audio", "Logging" })
            );
            provider.label = "Configuration";
            return provider;
        }

        public LckConfigurationPage(string path, SettingsScope scopes, IEnumerable<string> keywords = null)
            : base(path, scopes, keywords)
        {
        }

        public override void OnActivate(string searchContext, VisualElement rootElement)
        {
            try
            {
                var settings = LckSettings.Instance;
                if (settings != null)
                {
                    _serializedSettings = new SerializedObject(settings);
                }
                else
                {
                    Debug.LogWarning("[LckConfigurationPage] LckSettings.Instance is null, configuration page may not function correctly.");
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[LckConfigurationPage] Failed to load settings: {ex.Message}");
            }
            base.OnActivate(searchContext, rootElement);
        }

        protected override void BuildUI(VisualElement root)
        {
            var scrollView = CreateContentArea();
            var content = scrollView.Q<VisualElement>(className: "lck-content-area");

            AddPageTitle(content, "Configuration");
            AddBodyText(content, "Configure detailed settings for LCK recording, audio, and behavior.");

            AddSpacer(content, "lg");

            // Game Identity Section
            DrawGameIdentitySection(content);

            // Recording Section
            DrawRecordingSection(content);

            // Audio Section
            DrawAudioSection(content);

            // Photo Section
            DrawPhotoSection(content);

            // Android Permissions Section
            DrawAndroidSection(content);

            // Advanced Section
            DrawAdvancedSection(content);

            // Logging Section
            DrawLoggingSection(content);

            root.Add(scrollView);
        }

        private void DrawGameIdentitySection(VisualElement parent)
        {
            AddSectionHeader(parent, "Game Identity", true);

            var card = CreateCompactCard();

            AddPropertyField(card, "GameName", "Game Name", "The name of your game as it will appear in LCK.");
            AddPropertyField(card, "TrackingId", "Tracking ID", "Required. Your unique Tracking ID from the LIV Developer Portal.");

            parent.Add(card);
            AddSpacer(parent, "md");
        }

        private void DrawRecordingSection(VisualElement parent)
        {
            AddSectionHeader(parent, "Recording");

            var card = CreateCompactCard();

            AddPropertyField(card, "RecordingFilenamePrefix", "Filename Prefix", "Prefix added to recording filenames.");
            AddPropertyField(card, "RecordingAlbumName", "Album Name", "Album name for organizing recordings on device.");
            AddPropertyField(card, "RecordingDateSuffixFormat", "Date Format", "Date format suffix for filenames (e.g., yyyy-MM-dd_HH-mm-ss).");

            parent.Add(card);
            AddSpacer(parent, "md");
        }

        private void DrawAudioSection(VisualElement parent)
        {
            AddSectionHeader(parent, "Audio");

            var card = CreateCompactCard();

            AddPropertyField(card, "GameAudioSyncTimeOffsetInMS", "Audio Sync Offset (ms)",
                "Adjust game audio timing relative to video. Positive values move audio forward.");

            AddPropertyField(card, "AudioLimiter", "Audio Limiter",
                "Apply limiter compression to prevent audio clipping in recordings.");

            AddPropertyField(card, "FallbackSampleRate", "Fallback Sample Rate",
                "Sample rate used if LCK can't detect it from other sources.");

            parent.Add(card);
            AddSpacer(parent, "md");
        }

        private void DrawPhotoSection(VisualElement parent)
        {
            AddSectionHeader(parent, "Photo Capture");

            var card = CreateCompactCard();

            AddPropertyField(card, "ImageCaptureFileFormat", "Image Format",
                "File format for captured photos.");

            parent.Add(card);
            AddSpacer(parent, "md");
        }

        private void DrawAndroidSection(VisualElement parent)
        {
            AddSectionHeader(parent, "Android Permissions");

            var card = CreateCompactCard();

            AddPropertyField(card, "MicPermissionType", "Microphone Permission",
                "When to request microphone access on Android.");

            AddPropertyField(card, "AddMicPermissionsToAndroidManifest", "Add Mic Permission to Manifest",
                "Allow LCK to add microphone permissions to AndroidManifest.xml.");

            AddPropertyField(card, "AddInternetPermissionsToAndroidManifest", "Add Internet Permission to Manifest",
                "Allow LCK to add internet permissions to AndroidManifest.xml.");

            AddPropertyField(card, "AddControlCenterPermissionsToAndroidManifest", "Add Control Center Permission",
                "Allow LCK to add permissions for LIV Control Center app integration.");

            parent.Add(card);
            AddSpacer(parent, "md");
        }

        private void DrawAdvancedSection(VisualElement parent)
        {
            AddSectionHeader(parent, "Advanced");

            var card = CreateCompactCard();

            AddPropertyField(card, "EnableStencilSupport", "Enable Stencil Support",
                "Enable stencil buffer support for advanced rendering effects in recordings. Disable to optimize performance.");

            AddPropertyField(card, "TriggerEnterTag", "Tablet Collider Tag",
                "Tag used for trigger detection with 'LCK Tablet Using Collider' prefab.");

            AddPropertyField(card, "ShowSetupWizard", "Show Setup on Startup",
                "Show the setup wizard when Unity starts.");

            parent.Add(card);
            AddSpacer(parent, "md");
        }

        private void DrawLoggingSection(VisualElement parent)
        {
            AddSectionHeader(parent, "Logging");

            var card = CreateCompactCard();

            AddPropertyField(card, "BaseLogLevel", "Base Log Level",
                "Minimum log level for LCK messages.");

            AddPropertyField(card, "MicrophoneLogLevel", "Microphone Log Level",
                "Log level for microphone-related messages.");

            AddPropertyField(card, "NativeLogLevel", "Native Log Level",
                "Log level for native plugin messages.");

            AddPropertyField(card, "CoreLogLevel", "Core Log Level",
                "Log level for LCK core messages.");

            AddPropertyField(card, "ShowOpenGLMessages", "Show OpenGL Messages",
                "Display OpenGL debug messages (useful for graphics debugging).");

            parent.Add(card);
            AddSpacer(parent, "md");
        }

        private VisualElement CreateCompactCard()
        {
            VisualElement card = CreateCard();
            card.AddToClassList("lck-card--compact");
            return card;
        }

        private void AddPropertyField(VisualElement parent, string propertyName, string label, string tooltip = null)
        {
            if (_serializedSettings == null)
            {
                Debug.LogWarning($"[LckConfigurationPage] Cannot add property field '{propertyName}': serialized settings not available.");
                return;
            }

            _serializedSettings.Update();

            var property = _serializedSettings.FindProperty(propertyName);
            if (property == null)
            {
                return;
            }

            VisualElement row = new VisualElement();
            row.AddToClassList("lck-config-row");

            // Label - bold style matching sub-headers
            Label labelElement = new Label(label);
            labelElement.AddToClassList("lck-config-label");
            if (!string.IsNullOrEmpty(tooltip))
            {
                labelElement.tooltip = tooltip;
            }
            row.Add(labelElement);

            // Property field using UIElements binding
            PropertyField field = new PropertyField(property, "");
            field.BindProperty(property);

            // Style the field
            field.RegisterCallback<ChangeEvent<string>>(evt => SaveSettings());
            field.RegisterCallback<ChangeEvent<bool>>(evt => SaveSettings());
            field.RegisterCallback<ChangeEvent<int>>(evt => SaveSettings());
            field.RegisterCallback<ChangeEvent<float>>(evt => SaveSettings());
            field.RegisterCallback<ChangeEvent<System.Enum>>(evt => SaveSettings());

            // Apply input styling after the field is attached to the hierarchy
            field.RegisterCallback<GeometryChangedEvent>(evt =>
            {
                StylePropertyFieldInputs(field);
            });

            row.Add(field);

            // Tooltip as description
            if (!string.IsNullOrEmpty(tooltip))
            {
                Label descLabel = new Label(tooltip);
                descLabel.AddToClassList("lck-config-description");
                row.Add(descLabel);
            }

            parent.Add(row);
        }

        private void SaveSettings()
        {
            try
            {
                if (_serializedSettings != null)
                {
                    _serializedSettings.ApplyModifiedProperties();
                }

                var settings = LckSettings.Instance;
                if (settings != null)
                {
                    EditorUtility.SetDirty(settings);
                    AssetDatabase.SaveAssetIfDirty(settings);
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[LckConfigurationPage] Failed to save settings: {ex.Message}");
            }
        }
    }
}
