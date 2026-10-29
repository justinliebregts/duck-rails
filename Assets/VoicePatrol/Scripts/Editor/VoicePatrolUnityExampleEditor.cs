#if UNITY_EDITOR && !UNITY_SERVER && !UNITY_DEDICATED_SERVER
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using VoicePatrol.Scripts.VoicePatrol.Scripts.Runtime;

namespace VoicePatrol.Scripts.Editor.VoicePatrol.Scripts.Editor
{
    [CustomEditor(typeof(VoicePatrolUnityExample))]
    public class VoicePatrolUnityExampleEditor : UnityEditor.Editor
    {
        private bool _isSdkRunning;
        private bool _isMuted;
        private bool _isProcessing;
        private bool _lastSdkRunningState;
        private float _processingStartTime;
        private float _processingDuration;
        private AudioClip _currentClip;
        private float _meterLevel;
        private readonly VoicePatrolMicSelector _micSelector = new();
        private readonly Color _processingColor = new(1f, 0.7f, 0f);
        private readonly Color _readyColor = new(0.4f, 0.8f, 0.4f);
        private readonly string _logPrefix = $"<color=#00D5FF>[{nameof(VoicePatrolUnityExample)}]</color>";

        // Help content dictionary
        private readonly Dictionary<string, string> _helpContent = new()
        {
            { "apiKey", "Your VoicePatrol project API key. This can be found in your dashboard at voicepatrol.dev" },
            { "ageVerification", "Capture and analyze the first 60 seconds of pure speech each time the SDK starts to determine player age. This feature can only be set when the SDK is not running" },
            { "useCdn", "Route connections through a CDN for improved reliability in regions with poor network connectivity. Recommended for players experiencing connection issues. This feature can only be set when the SDK is not running" },
            { "debugLogs", "Enable detailed SDK debug logs in the console. This feature can only be set when the SDK is not running" },
            { "testAudio", "Upload an audio clip to test voice moderation or age verification. The clip must be uncompressed or marked as 'Decompress On Load'" },
            { "offenderId", "The unique identifier of the player you want to report. Used when submitting player reports with the most recent 120 seconds of everything the local player said and heard" },
            { "reason", "The reason for submitting the report. Used when submitting player reports with the most recent 120 seconds of everything the local player said and heard" }
        };

        // Help toggle states
        private readonly Dictionary<string, bool> _helpToggles = new();

        private void OnEnable()
        {
            EditorApplication.update += UpdateEditor;
        }

        private void OnDisable()
        {
            EditorApplication.update -= UpdateEditor;
            ResetProcessingState();
        }

        // Repaint every frame while the SDK is running so the input meter animates live.
        public override bool RequiresConstantRepaint() => VoicePatrolUnityExample.IsSdkRunning;

        private void ResetProcessingState()
        {
            _isProcessing = false;
            _currentClip = null;
            _processingStartTime = 0;
            _processingDuration = 0;
        }

        private void DrawPropertyWithHelp(string helpKey, SerializedProperty property, GUIContent label, bool disabled = false)
        {
            EditorGUILayout.BeginHorizontal();

            // Save current indent level and temporarily remove it for the horizontal group
            int currentIndent = EditorGUI.indentLevel;
            EditorGUI.indentLevel = 0;

            // Help Button
            if (GUILayout.Button("?", EditorStyles.miniButton, GUILayout.Width(20)))
            {
                if (!_helpToggles.ContainsKey(helpKey))
                {
                    _helpToggles[helpKey] = false;
                }
                _helpToggles[helpKey] = !_helpToggles[helpKey];
            }

            // Restore indent level for the property field
            EditorGUI.indentLevel = currentIndent;

            // Property Field
            using (new EditorGUI.DisabledScope(disabled))
            {
                EditorGUILayout.PropertyField(property, label, GUILayout.ExpandWidth(true));
            }

            EditorGUILayout.EndHorizontal();

            // Help Box
            if (_helpToggles.TryGetValue(helpKey, out bool showHelp) && showHelp)
            {
                EditorGUILayout.HelpBox(_helpContent[helpKey], MessageType.Info);
                EditorGUILayout.Space(2);
            }
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            bool isSdkRunning = VoicePatrolUnityExample.IsSdkRunning;
            bool isSdkMuted = VoicePatrolUnityExample.IsMuted;

            EditorGUILayout.Space(10);

            // Status section
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PrefixLabel("Status", EditorStyles.boldLabel, EditorStyles.boldLabel);

            string statusText;
            Color statusColor;
            if (!isSdkRunning)
            {
                statusText = "\u25cf SDK Not Running";
                statusColor = new Color(0.6f, 0.6f, 0.6f);
            }
            else if (_isProcessing && _currentClip != null)
            {
                float elapsed = (float)(EditorApplication.timeSinceStartup - _processingStartTime);
                float progress = Mathf.Clamp01(elapsed / _processingDuration);
                statusText = $"\u25cf SDK Processing {_currentClip.name}... ({progress * 100:F0}%)";
                statusColor = _processingColor;
            }
            else if (isSdkMuted)
            {
                statusText = "\u25cf SDK Muted";
                statusColor = new Color(1f, 0.75f, 0.2f);
            }
            else
            {
                statusText = "\u25cf SDK Running";
                statusColor = new Color(0.3f, 0.85f, 0.3f);
            }

            GUIStyle statusStyle = new(EditorStyles.boldLabel) { normal = { textColor = statusColor } };
            EditorGUILayout.LabelField(statusText, statusStyle);
            EditorGUILayout.EndHorizontal();

            float meterRaw = isSdkRunning ? VoicePatrolWrapper.GetInputLevel() : 0f;
            _meterLevel = VoicePatrolInputMeter.Smooth(_meterLevel, meterRaw);
            VoicePatrolInputMeter.Draw(_meterLevel, isSdkRunning);
            _micSelector.Draw(isSdkRunning);

            EditorGUILayout.Space(10);

            // Configuration section
            EditorGUILayout.LabelField("Configuration", EditorStyles.boldLabel);

            DrawPropertyWithHelp("apiKey",
                serializedObject.FindProperty("_voicePatrolAPIKey"),
                new GUIContent("API Key"));

            DrawPropertyWithHelp("ageVerification",
                serializedObject.FindProperty("_ageVerification"),
                new GUIContent("Age Verification"),
                isSdkRunning);

            DrawPropertyWithHelp("useCdn",
                serializedObject.FindProperty("_useCdn"),
                new GUIContent("Use CDN"),
                isSdkRunning);

            DrawPropertyWithHelp("debugLogs",
                serializedObject.FindProperty("_debugLogs"),
                new GUIContent("Debug Logs"),
                isSdkRunning);

            EditorGUILayout.Space(10);

            // Audio Test section
            EditorGUILayout.LabelField("Audio Test", EditorStyles.boldLabel);

            DrawPropertyWithHelp("testAudio",
                serializedObject.FindProperty("_testAudioClip"),
                new GUIContent("Test Audio Clip"));

            {
                // Grab a layout rect for one line
                Rect progressRect = EditorGUILayout.GetControlRect();

                // Shift it right to skip over the label area (and the 20px help button)
                progressRect.x += EditorGUIUtility.labelWidth + 24;
                progressRect.width -= EditorGUIUtility.labelWidth + 24;

                // Draw a background
                EditorGUI.DrawRect(progressRect, new Color(0.2f, 0.2f, 0.2f));

                // If SDK is running, draw the fill portion
                if (isSdkRunning)
                {
                    if (_isProcessing && _currentClip != null)
                    {
                        float elapsed = (float)(EditorApplication.timeSinceStartup - _processingStartTime);
                        float progress = Mathf.Clamp01(elapsed / _processingDuration);

                        // Make a copy of progressRect, then shrink it by progress
                        Rect fillRect = progressRect;
                        fillRect.width *= progress;

                        // Use your "_processingColor" or "_readyColor"
                        EditorGUI.DrawRect(fillRect, _processingColor);
                    }
                    else
                    {
                        // For example, show it as "ready" if not processing
                        EditorGUI.DrawRect(progressRect, _readyColor);
                    }
                }
            }

            {
                // Grab a layout rect for one line
                Rect buttonRect = EditorGUILayout.GetControlRect();

                // Shift/resize so it matches the property field's area
                buttonRect.x += EditorGUIUtility.labelWidth + 24;
                buttonRect.width -= EditorGUIUtility.labelWidth + 24;

                // Disable if processing, or if the SDK is not running/muted
                EditorGUI.BeginDisabledGroup(_isProcessing || !isSdkRunning || isSdkMuted);
                if (GUI.Button(buttonRect, "Process Audio Clip"))
                {
                    SerializedProperty testAudioProp = serializedObject.FindProperty("_testAudioClip");
                    AudioClip testClip = testAudioProp.objectReferenceValue as AudioClip;
                    if (testClip != null)
                    {
                        _currentClip = testClip;
                        _processingStartTime = (float)EditorApplication.timeSinceStartup;
                        _processingDuration = testClip.length;
                        _isProcessing = true;

                        Debug.Log($"{_logPrefix} {VoicePatrolWrapper.ProcessAudioFile(testClip)}: {testClip.name}");
                    }
                    else
                    {
                        Debug.LogError($"{_logPrefix} Test audio clip is not assigned");
                    }
                }
                EditorGUI.EndDisabledGroup();
            }

            EditorGUILayout.Space(10);

            // Report Player section
            EditorGUILayout.LabelField("Report Player", EditorStyles.boldLabel);

            // Draw the Unique ID property + help
            DrawPropertyWithHelp("offenderId",
                serializedObject.FindProperty("_offenderID"),
                new GUIContent("Offender ID"));

            DrawPropertyWithHelp("reason",
                serializedObject.FindProperty("_reportReason"),
                new GUIContent("Reason"));

            {
                Rect reportButtonRect = EditorGUILayout.GetControlRect();
                reportButtonRect.x += EditorGUIUtility.labelWidth + 24;
                reportButtonRect.width -= EditorGUIUtility.labelWidth + 24;

                EditorGUI.BeginDisabledGroup(!isSdkRunning || isSdkMuted ||
                    string.IsNullOrWhiteSpace(serializedObject.FindProperty("_offenderID").stringValue) ||
                    string.IsNullOrWhiteSpace(serializedObject.FindProperty("_reportReason").stringValue));

                if (GUI.Button(reportButtonRect, "Submit Player Report"))
                {
                    string result = VoicePatrolWrapper.SubmitPlayerReport(
                        serializedObject.FindProperty("_offenderID").stringValue, serializedObject.FindProperty("_reportReason").stringValue);
                    Debug.Log($"{_logPrefix} {result}");
                }
                EditorGUI.EndDisabledGroup();
            }

            serializedObject.ApplyModifiedProperties();
        }

        private void UpdateEditor()
        {
            bool currentSdkRunning = VoicePatrolUnityExample.IsSdkRunning;

            // Detect SDK state changes (specifically when it goes from running to not running)
            if (_lastSdkRunningState && !currentSdkRunning)
            {
                // The SDK has been stopped - reset the processing state
                ResetProcessingState();
            }
            _lastSdkRunningState = currentSdkRunning;

            // Update the running state only if it changed
            if (_isSdkRunning != currentSdkRunning)
            {
                _isSdkRunning = currentSdkRunning;
                Repaint();
            }

            // If the SDK is running, check the muted state
            if (_isSdkRunning)
            {
                bool currentMuted = VoicePatrolUnityExample.IsMuted;
                if (_isMuted != currentMuted)
                {
                    _isMuted = currentMuted;
                    Repaint();
                }
            }

            // If we're processing a clip, check if it's done
            if (_isProcessing)
            {
                // If the SDK is not running, stop processing
                if (!currentSdkRunning)
                {
                    _isProcessing = false;
                    _currentClip = null;
                }
                // Otherwise check if processing is complete based on time
                else if (EditorApplication.timeSinceStartup - _processingStartTime >= _processingDuration)
                {
                    _isProcessing = false;
                    _currentClip = null;
                }
                Repaint();
            }
        }
    }
}
#endif