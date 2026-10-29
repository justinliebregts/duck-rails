using Liv.Lck.Audio;
using Liv.Lck.Settings;
using Liv.Lck.Util;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Liv.Lck
{
    /// <summary>
    /// Helper methods for the LCK Setup Wizard.
    /// Contains utility functions for creating UI elements, validation, and asset management.
    /// </summary>
    public partial class LckSetupWizard
    {
        #region Option Row Creators

        private VisualElement CreateToolkitOptionRow(
            string title,
            string description,
            InteractionToolkitType toolkitType,
            WizardPage targetPage,
            string imagePath = null)
        {
            VisualElement row = new VisualElement();
            row.AddToClassList("lck-card");
            row.AddToClassList("lck-card--horizontal");
            row.AddToClassList("lck-card--clickable");
            LckSettingsPageBase.ApplyCardFallbackStylesStatic(row);

            if (GetSelectedToolkit() == toolkitType)
            {
                row.AddToClassList("lck-card--selected");
            }

            row.RegisterCallback<ClickEvent>(evt =>
            {
                SetSelectedToolkit(toolkitType);
                _currentPage = targetPage;
                UpdateContentArea();
            });

            if (!string.IsNullOrEmpty(imagePath))
            {
                Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(imagePath);
                if (texture != null)
                {
                    VisualElement imageContainer = new VisualElement();
                    imageContainer.AddToClassList("lck-card-image");
                    imageContainer.pickingMode = PickingMode.Ignore;
                    imageContainer.style.backgroundImage = new StyleBackground(texture);
#if UNITY_2022_1_OR_NEWER
                    imageContainer.style.backgroundSize = new BackgroundSize(BackgroundSizeType.Cover);
                    imageContainer.style.backgroundPositionX = new BackgroundPosition(BackgroundPositionKeyword.Center);
                    imageContainer.style.backgroundPositionY = new BackgroundPosition(BackgroundPositionKeyword.Center);
                    imageContainer.style.backgroundRepeat = new BackgroundRepeat(Repeat.NoRepeat, Repeat.NoRepeat);
#endif
                    row.Add(imageContainer);
                }
            }

            VisualElement textContainer = new VisualElement();
            textContainer.AddToClassList("lck-card-content");
            textContainer.pickingMode = PickingMode.Ignore;

            Label titleLabel = new Label(title);
            titleLabel.AddToClassList("lck-card-title");
            titleLabel.pickingMode = PickingMode.Ignore;
            textContainer.Add(titleLabel);

            Label descLabel = new Label(description);
            descLabel.AddToClassList("lck-card-description");
            descLabel.pickingMode = PickingMode.Ignore;
            textContainer.Add(descLabel);

            row.Add(textContainer);
            return row;
        }

        private VisualElement CreateUnityXRMethodOptionRow(
            string title,
            string description,
            UnityXRInteractionMethodType methodType,
            string imagePath)
        {
            VisualElement row = new VisualElement();
            row.AddToClassList("lck-card");
            row.AddToClassList("lck-card--horizontal");
            row.AddToClassList("lck-card--clickable");
            LckSettingsPageBase.ApplyCardFallbackStylesStatic(row);

            if (GetSelectedUnityXRMethod() == methodType)
            {
                row.AddToClassList("lck-card--selected");
            }

            row.RegisterCallback<ClickEvent>(evt =>
            {
                SetSelectedUnityXRMethod(methodType);
                _currentPage = WizardPage.UnityXRSetup;
                UpdateContentArea();
            });

            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(imagePath);
            if (texture != null)
            {
                VisualElement imageContainer = new VisualElement();
                imageContainer.AddToClassList("lck-card-image");
                imageContainer.pickingMode = PickingMode.Ignore;
                imageContainer.style.backgroundImage = new StyleBackground(texture);
#if UNITY_2022_1_OR_NEWER
                imageContainer.style.backgroundSize = new BackgroundSize(BackgroundSizeType.Cover);
                imageContainer.style.backgroundPositionX = new BackgroundPosition(BackgroundPositionKeyword.Center);
                imageContainer.style.backgroundPositionY = new BackgroundPosition(BackgroundPositionKeyword.Center);
                imageContainer.style.backgroundRepeat = new BackgroundRepeat(Repeat.NoRepeat, Repeat.NoRepeat);
#endif
                row.Add(imageContainer);
            }

            VisualElement textContainer = new VisualElement();
            textContainer.AddToClassList("lck-card-content");
            textContainer.pickingMode = PickingMode.Ignore;

            Label titleLabel = new Label(title);
            titleLabel.AddToClassList("lck-card-title");
            titleLabel.pickingMode = PickingMode.Ignore;
            textContainer.Add(titleLabel);

            Label descLabel = new Label(description);
            descLabel.AddToClassList("lck-card-description");
            descLabel.pickingMode = PickingMode.Ignore;
            textContainer.Add(descLabel);

            row.Add(textContainer);
            return row;
        }

        private VisualElement CreateDefaultUnityAudioOptionRow(string title, string description)
        {
            VisualElement row = new VisualElement();
            row.AddToClassList("lck-card");
            row.AddToClassList("lck-card--clickable");
            LckSettingsPageBase.ApplyCardFallbackStylesStatic(row);

            if (GetSelectedAudioSystem() == AudioSystemType.DefaultUnityAudio)
            {
                row.AddToClassList("lck-card--selected");
            }

            row.RegisterCallback<ClickEvent>(evt =>
            {
                SetSelectedAudioSystem(AudioSystemType.DefaultUnityAudio);
                ScriptingDefineUtils.RemoveDefines(LckAudioScriptingDefines.GetAllLckAudioScriptingDefines());
                _currentPage = WizardPage.ProjectValidation;
                UpdateContentArea();
            });

            Label titleLabel = new Label(title);
            titleLabel.AddToClassList("lck-card-title");
            titleLabel.pickingMode = PickingMode.Ignore;
            row.Add(titleLabel);

            Label descLabel = new Label(description);
            descLabel.AddToClassList("lck-card-description");
            descLabel.pickingMode = PickingMode.Ignore;
            row.Add(descLabel);

            return row;
        }

        private VisualElement CreateAudioOptionRow(
            string title,
            string description,
            AudioSystemType audioType,
            WizardPage targetPage)
        {
            VisualElement row = new VisualElement();
            row.AddToClassList("lck-card");
            row.AddToClassList("lck-card--clickable");
            LckSettingsPageBase.ApplyCardFallbackStylesStatic(row);

            if (GetSelectedAudioSystem() == audioType)
            {
                row.AddToClassList("lck-card--selected");
            }

            row.RegisterCallback<ClickEvent>(evt =>
            {
                SetSelectedAudioSystem(audioType);
                _currentPage = targetPage;
                UpdateContentArea();
            });

            Label titleLabel = new Label(title);
            titleLabel.AddToClassList("lck-card-title");
            titleLabel.pickingMode = PickingMode.Ignore;
            row.Add(titleLabel);

            Label descLabel = new Label(description);
            descLabel.AddToClassList("lck-card-description");
            descLabel.pickingMode = PickingMode.Ignore;
            row.Add(descLabel);

            return row;
        }

        #endregion

        #region Validation UI

        private VisualElement CreateValidationElement(
            string text,
            System.Action buttonAction = null,
            bool hasBeenFixed = false,
            string buttonText = "Apply Fix",
            ValidationSeverity severity = ValidationSeverity.Suggested)
        {
            VisualElement row = new VisualElement();
            row.AddToClassList("lck-validation-row");
            LckSettingsPageBase.ApplyValidationRowFallbackStylesStatic(row);

            VisualElement icon = new VisualElement();
            icon.AddToClassList("lck-validation-icon");
            Texture2D iconTexture = EditorGUIUtility.IconContent(hasBeenFixed ? "d_Progress" : "d_console.erroricon.sml").image as Texture2D;
            icon.style.backgroundImage = new StyleBackground(iconTexture);

            if (hasBeenFixed)
            {
                icon.AddToClassList("lck-validation-icon--valid");
                ColorUtility.TryParseHtmlString("#D4FF2A", out Color validGreen);
                icon.style.unityBackgroundImageTintColor = validGreen;
            }
            else if (severity == ValidationSeverity.Required)
            {
                icon.AddToClassList("lck-validation-icon--required");
                ColorUtility.TryParseHtmlString("#FF4D4D", out Color requiredRed);
                icon.style.unityBackgroundImageTintColor = requiredRed;
            }
            else
            {
                icon.AddToClassList("lck-validation-icon--suggested");
                ColorUtility.TryParseHtmlString("#FFB84D", out Color suggestedOrange);
                icon.style.unityBackgroundImageTintColor = suggestedOrange;
            }

            icon.pickingMode = PickingMode.Ignore;
            row.Add(icon);

            Label label = new Label(text);
            label.AddToClassList("lck-validation-text");
            row.Add(label);

            if (buttonAction != null)
            {
                Button fixButton = new Button(buttonAction);
                fixButton.text = buttonText;
                fixButton.AddToClassList("lck-button");
                fixButton.AddToClassList("lck-button--small");
                fixButton.AddToClassList("lck-button--accent");
                fixButton.style.display = hasBeenFixed ? DisplayStyle.None : DisplayStyle.Flex;
                row.Add(fixButton);
            }

            return row;
        }

        /// <summary>
        /// Gets the overall validation status by checking all validations.
        /// Returns null if all validations pass, otherwise returns the worst severity.
        /// </summary>
        private ValidationSeverity? GetOverallValidationStatus()
        {
            int currentLevel = (int)PlayerSettings.Android.minSdkVersion;
            int requiredLevel = LckSettings.RequiredAndroidApiLevel;
            bool requiredPassed = currentLevel >= requiredLevel;

            if (!requiredPassed)
            {
                return ValidationSeverity.Required;
            }

            bool isArm64Enabled = PlayerSettings.Android.targetArchitectures.HasFlag(AndroidArchitecture.ARM64);
            bool hasLckTabletLayer = HasLayer("LCK Tablet");
#if UNITY_2022_1_OR_NEWER
            bool hasAudioListenerOrMarker = Object.FindAnyObjectByType<AudioListener>(FindObjectsInactive.Include) != null
                || Object.FindAnyObjectByType<LckAudioMarker>(FindObjectsInactive.Include) != null;
#else
            bool hasAudioListenerOrMarker = Object.FindObjectOfType<AudioListener>(true) != null
                || Object.FindObjectOfType<LckAudioMarker>(true) != null;
#endif

            if (!isArm64Enabled || !hasLckTabletLayer || !hasAudioListenerOrMarker)
            {
                return ValidationSeverity.Suggested;
            }

            return null;
        }

        #endregion

        #region Asset Management

        private void SelectPrefabInProject(string assetPath)
        {
            Object prefab = AssetDatabase.LoadAssetAtPath<Object>(assetPath);
            if (prefab != null)
            {
                Selection.activeObject = prefab;
                EditorGUIUtility.PingObject(prefab);
            }
        }

        private void CreatePrefabVariant(string sourcePrefabPath, string variantPath)
        {
            GameObject sourcePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(sourcePrefabPath);
            if (sourcePrefab == null)
            {
                EditorUtility.DisplayDialog("Error", $"Could not find source prefab at path: {sourcePrefabPath}", "OK");
                return;
            }

            if (AssetDatabase.LoadAssetAtPath<GameObject>(variantPath) != null)
            {
                bool overwrite = EditorUtility.DisplayDialog(
                    "Prefab Variant Already Exists",
                    $"A prefab variant already exists at {variantPath}. Do you want to overwrite it?",
                    "Overwrite",
                    "Cancel"
                );

                if (!overwrite) return;
                AssetDatabase.DeleteAsset(variantPath);
            }

            GameObject variantPrefab = (GameObject)PrefabUtility.InstantiatePrefab(sourcePrefab);
            GameObject variant = PrefabUtility.SaveAsPrefabAsset(variantPrefab, variantPath);
            Object.DestroyImmediate(variantPrefab);

            if (variant != null)
            {
                Selection.activeObject = variant;
                EditorGUIUtility.PingObject(variant);
                AssetDatabase.Refresh();
            }
            else
            {
                EditorUtility.DisplayDialog("Error", $"Failed to create prefab variant at: {variantPath}", "OK");
            }
        }

        #endregion

        #region Layer Management

        /// <summary>
        /// Checks if a layer with the specified name exists in the Layer/TagManager.
        /// </summary>
        private bool HasLayer(string layerName)
        {
            SerializedObject tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            SerializedProperty layers = tagManager.FindProperty("layers");

            for (int i = 0; i < layers.arraySize; i++)
            {
                SerializedProperty layer = layers.GetArrayElementAtIndex(i);
                if (layer.stringValue == layerName)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Adds a layer with the specified name to the first available slot.
        /// </summary>
        private void AddLayer(string layerName)
        {
            if (HasLayer(layerName)) return;

            SerializedObject tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
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
    }
}
