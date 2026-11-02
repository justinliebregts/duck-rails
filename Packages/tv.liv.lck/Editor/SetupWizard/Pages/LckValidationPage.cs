using Liv.Lck.Audio;
using Liv.Lck.Settings;
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Liv.Lck
{
    /// <summary>
    /// Validation page for checking project setup and applying fixes.
    /// </summary>
    public class LckValidationPage : LckSettingsPageBase
    {
        private enum ValidationSeverity
        {
            Required,
            Suggested
        }

        private VisualElement _contentArea;
        private bool _celebrationTriggered = false;

        [SettingsProvider]
        public static SettingsProvider CreateSettingsProvider()
        {
            var provider = new LckValidationPage(
                "Project/LCK/4 Validation",
                SettingsScope.Project,
                new HashSet<string>(new[] { "LCK", "LIV", "Validation", "Check", "Setup" })
            );
            provider.label = "Validation";
            return provider;
        }

        public LckValidationPage(string path, SettingsScope scopes, IEnumerable<string> keywords = null)
            : base(path, scopes, keywords)
        {
        }

        protected override void BuildUI(VisualElement root)
        {
            var scrollView = CreateContentArea();
            _contentArea = scrollView.Q<VisualElement>(className: "lck-content-area");

            AddPageTitle(_contentArea, "Project Validation");
            AddBodyText(_contentArea, "Verify your project is correctly configured for LCK.");

            AddSpacer(_contentArea, "lg");

            // Required section
            AddSectionHeader(_contentArea, "Required", true);

            // Required: Tracking ID configured
            bool hasTrackingId = false;
            try
            {
                var settings = LckSettings.Instance;
                hasTrackingId = settings != null && !string.IsNullOrEmpty(settings.TrackingId);
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[LckValidationPage] Error checking tracking ID: {ex.Message}");
            }
            _contentArea.Add(CreateValidationElement(
                "Tracking ID Configured",
                hasTrackingId,
                ValidationSeverity.Required,
                "Configure",
                () => SettingsService.OpenProjectSettings("Project/LCK/2 Setup")
            ));

            // Required: Minimum API Level
            int currentLevel = (int)PlayerSettings.Android.minSdkVersion;
            int requiredLevel = LckSettings.RequiredAndroidApiLevel;
            bool apiLevelPassed = currentLevel >= requiredLevel;

            _contentArea.Add(CreateValidationElement(
                $"Minimum API Level is {requiredLevel}+ (Android)",
                apiLevelPassed,
                ValidationSeverity.Required,
                "Apply Fix",
                () =>
                {
                    try
                    {
                        PlayerSettings.Android.minSdkVersion = (AndroidSdkVersions)LckSettings.RequiredAndroidApiLevel;
                        RefreshContent();
                    }
                    catch (System.Exception ex)
                    {
                        Debug.LogError($"[LckValidationPage] Failed to apply API level fix: {ex.Message}");
                    }
                }
            ));

            AddSpacer(_contentArea, "lg");

            // Suggested section
            AddSectionHeader(_contentArea, "Suggested");

            // Suggested: ARM64 Architecture
            bool isArm64Enabled = PlayerSettings.Android.targetArchitectures.HasFlag(AndroidArchitecture.ARM64);
            _contentArea.Add(CreateValidationElement(
                "Target Architecture is ARM64 (Android)",
                isArm64Enabled,
                ValidationSeverity.Suggested,
                "Apply Fix",
                () =>
                {
                    try
                    {
                        PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
                        RefreshContent();
                    }
                    catch (System.Exception ex)
                    {
                        Debug.LogError($"[LckValidationPage] Failed to apply ARM64 fix: {ex.Message}");
                    }
                }
            ));

            // Suggested: LCK Tablet Layer
            bool hasLckTabletLayer = HasLayer("LCK Tablet");
            _contentArea.Add(CreateValidationElement(
                "LCK Tablet Layer Added",
                hasLckTabletLayer,
                ValidationSeverity.Suggested,
                "Add Layer",
                () =>
                {
                    try
                    {
                        AddLayer("LCK Tablet");
                        RefreshContent();
                    }
                    catch (System.Exception ex)
                    {
                        Debug.LogError($"[LckValidationPage] Failed to add layer: {ex.Message}");
                    }
                }
            ));

            // Suggested: AudioListener or LckAudioMarker
#if UNITY_2022_1_OR_NEWER
            bool hasAudioListenerOrMarker =
                UnityEngine.Object.FindAnyObjectByType<AudioListener>(FindObjectsInactive.Include) != null
                || UnityEngine.Object.FindAnyObjectByType<LckAudioMarker>(FindObjectsInactive.Include) != null;
#else
            bool hasAudioListenerOrMarker =
                UnityEngine.Object.FindObjectOfType<AudioListener>(true) != null
                || UnityEngine.Object.FindObjectOfType<LckAudioMarker>(true) != null;
#endif

            string audioText = hasAudioListenerOrMarker
                ? "Found AudioListener or LckAudioMarker in scene"
                : "No AudioListener or LckAudioMarker found in scene";

            _contentArea.Add(CreateValidationElement(
                audioText,
                hasAudioListenerOrMarker,
                ValidationSeverity.Suggested,
                "Refresh",
                () => RefreshContent()
            ));

            AddSpacer(_contentArea, "lg");

            // Summary
            bool allPassed = apiLevelPassed && isArm64Enabled && hasLckTabletLayer && hasAudioListenerOrMarker && hasTrackingId;
            bool requiredPassed = hasTrackingId && apiLevelPassed;

            if (allPassed)
            {
                var successBox = CreateSuccessBox("All validations passed! Your project is ready for LCK.");
                _contentArea.Add(successBox);

                // Trigger celebration (only once per "all passed" state)
                if (!_celebrationTriggered)
                {
                    _celebrationTriggered = true;
                    _contentArea.schedule.Execute(() => TriggerConfettiCelebration(_rootElement)).StartingIn(100);
                }
            }
            else
            {
                // Reset celebration flag when not all passed, so fixing issues triggers confetti
                _celebrationTriggered = false;

                if (requiredPassed)
                {
                    var infoBox = CreateInfoBox("Required checks passed. Consider addressing the suggested items for optimal setup.");
                    _contentArea.Add(infoBox);
                }
                else
                {
                    var warningBox = CreateWarningBox("Some required checks failed. Please address the issues above.");
                    _contentArea.Add(warningBox);
                }
            }

            // Refresh button
            AddSpacer(_contentArea, "md");

            Button refreshButton = CreateSecondaryButton("Refresh All Checks", () =>
            {
                _celebrationTriggered = false;
                RefreshContent();
            });
            _contentArea.Add(refreshButton);

            root.Add(scrollView);
        }

        private VisualElement CreateValidationElement(string text, bool passed, ValidationSeverity severity, string buttonText, Action buttonAction)
        {
            VisualElement row = new VisualElement();
            row.AddToClassList("lck-validation-row");
            ApplyValidationRowFallbackStylesStatic(row);

            // Icon
            VisualElement icon = new VisualElement();
            icon.AddToClassList("lck-validation-icon");

            string iconName = passed ? "d_FilterSelectedOnly" : "d_console.erroricon.sml";
            Texture2D iconTexture = EditorGUIUtility.IconContent(iconName).image as Texture2D;
            icon.style.backgroundImage = new StyleBackground(iconTexture);

            Color tintColor;
            if (passed)
            {
                icon.AddToClassList("lck-validation-icon--valid");
                ColorUtility.TryParseHtmlString("#D4FF2A", out tintColor);
            }
            else if (severity == ValidationSeverity.Required)
            {
                icon.AddToClassList("lck-validation-icon--required");
                ColorUtility.TryParseHtmlString("#FF4D4D", out tintColor);
            }
            else
            {
                icon.AddToClassList("lck-validation-icon--suggested");
                ColorUtility.TryParseHtmlString("#FFB84D", out tintColor);
            }

            icon.style.unityBackgroundImageTintColor = tintColor;
            icon.pickingMode = PickingMode.Ignore;
            row.Add(icon);

            // Text
            Label label = new Label(text);
            label.AddToClassList("lck-validation-text");
            row.Add(label);

            // Button (only if not passed)
            if (!passed && buttonAction != null)
            {
                Button button = new Button(buttonAction);
                button.text = buttonText;
                button.AddToClassList("lck-button");
                button.AddToClassList("lck-button--small");
                button.AddToClassList("lck-button--accent");
                row.Add(button);
            }

            return row;
        }

        private void RefreshContent()
        {
            if (_rootElement != null)
            {
                _rootElement.Clear();
                BuildUI(_rootElement);
            }
        }

        #region Layer Utilities

        private bool HasLayer(string layerName)
        {
            for (int i = 0; i < 32; i++)
            {
                string name = LayerMask.LayerToName(i);
                if (name == layerName) return true;
            }
            return false;
        }

        private void AddLayer(string layerName)
        {
            SerializedObject tagManager = new SerializedObject(
                AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            SerializedProperty layers = tagManager.FindProperty("layers");

            for (int i = 8; i < layers.arraySize; i++)
            {
                SerializedProperty layer = layers.GetArrayElementAtIndex(i);
                if (string.IsNullOrEmpty(layer.stringValue))
                {
                    layer.stringValue = layerName;
                    tagManager.ApplyModifiedProperties();
                    return;
                }
            }
        }

        #endregion

        #region Confetti Animation

        private void TriggerConfettiCelebration(VisualElement parent)
        {
            if (parent == null) return;

            VisualElement confettiContainer = new VisualElement();
            confettiContainer.name = "confetti-container";
            confettiContainer.style.position = Position.Absolute;
            confettiContainer.style.top = 0;
            confettiContainer.style.left = 0;
            confettiContainer.style.right = 0;
            confettiContainer.style.bottom = 0;
            confettiContainer.pickingMode = PickingMode.Ignore;
            confettiContainer.style.overflow = Overflow.Hidden;

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

                int size = random.Next(6, 13);
                particle.style.width = size;
                particle.style.height = size;

                float initialRotation = random.Next(0, 360);
#if UNITY_2021_2_OR_NEWER
                particle.style.rotate = new Rotate(initialRotation);
#endif

                Color particleColor = confettiColors[random.Next(confettiColors.Length)];
                particle.style.backgroundColor = particleColor;

                particle.style.position = Position.Absolute;
                particle.style.left = Length.Percent(random.Next(0, 100));
                particle.style.top = Length.Percent(-10);

                if (random.Next(2) == 0)
                {
                    particle.style.borderTopLeftRadius = size / 2;
                    particle.style.borderTopRightRadius = size / 2;
                    particle.style.borderBottomLeftRadius = size / 2;
                    particle.style.borderBottomRightRadius = size / 2;
                }

                confettiContainer.Add(particle);

                float horizontalDrift = (float)(random.NextDouble() * 2 - 1) * 0.5f;
                float fallSpeed = (float)(random.NextDouble() * 0.5 + 0.8);
                int rotationSpeed = random.Next(-360, 360);
                int delay = random.Next(0, 1500);

                AnimateConfettiFalling(particle, delay, horizontalDrift, fallSpeed, initialRotation, rotationSpeed, particleColor);
            }

            parent.Add(confettiContainer);

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
            int totalDuration = 4000;
            int steps = 120;
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

                    currentY += fallSpeed * 1.3f;
                    currentX += horizontalDrift * Mathf.Sin(progress * Mathf.PI * 4);

                    particle.style.left = Length.Percent(currentX);
                    particle.style.top = Length.Percent(currentY);

#if UNITY_2021_2_OR_NEWER
                    particle.style.rotate = new Rotate(initialRotation + (rotationSpeed * progress));
#endif

                    if (currentY > 85f)
                    {
                        float fadeProgress = (currentY - 85f) / 25f;
                        float alpha = Mathf.Clamp01(1f - fadeProgress);
                        particle.style.backgroundColor = new Color(originalColor.r, originalColor.g, originalColor.b, alpha);
                    }

                }).Every(stepDuration).Until(() => currentStep >= steps || currentY > 110f);
            }).StartingIn(delay);
        }

        #endregion
    }
}
