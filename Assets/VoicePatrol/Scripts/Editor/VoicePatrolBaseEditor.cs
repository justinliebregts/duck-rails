#if UNITY_EDITOR && !UNITY_SERVER && !UNITY_DEDICATED_SERVER
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using VoicePatrol.Scripts.VoicePatrol.Scripts.Runtime;

namespace VoicePatrol.Scripts.Editor.VoicePatrol.Scripts.Editor
{
    /// <summary>
    /// Custom editor for <see cref="VoicePatrolBase"/> and all subclasses.
    /// Uses editorForChildClasses to automatically apply to VoicePatrolPhoton, VoicePatrolVivox, etc.
    /// </summary>
    [CustomEditor(typeof(VoicePatrolBase), editorForChildClasses: true)]
    public class VoicePatrolBaseEditor : UnityEditor.Editor
    {
        private SerializedProperty _apiKey;
        private SerializedProperty _ageVerification;
        private SerializedProperty _debugLogs;
        private SerializedProperty _headless; // subclass field — null for integrations without headless support
        private SerializedProperty _enablePrivateRoomFiltering;
        private SerializedProperty _privateRoomIdentifier;
        private SerializedProperty _enableAutomatedMuting;
        private SerializedProperty _muteRules;
        private SerializedProperty _maxMuteDurationMinutes;

        private bool _showMuteRules = true;
        private Rect[] _rowRects;
        private float _meterLevel;
        private readonly VoicePatrolMicSelector _micSelector = new();

        private readonly Dictionary<string, string> _helpContent = new()
        {
            { "apiKey",          "Your VoicePatrol project API key. Get it from dashboard.voicepatrol.dev" },
            { "ageVerification", "Analyzes the first 60 seconds of pure speech to estimate player age. Can only be changed before the SDK starts." },
            { "debugLogs",       "Enables debug logging from VoicePatrol to the Unity console. When unchecked, only warnings and errors are logged." },
            { "headless",        "Server-to-server mode. No audio is sent from this client — your voice provider's servers stream audio directly to VoicePatrol. " +
                                 "The provider identity (Photon Client.UserId / Vivox PlayerId) must equal the unique ID passed to Initialize: set it at login, " +
                                 "before joining a room. A warning is logged at runtime if they don't match. The input meter stays empty in this mode. " +
                                 "Can only be changed before the SDK starts." },
            { "privateFilter",   "When enabled, rooms whose name contains the identifier string below will not be moderated by VoicePatrol." },
            { "privateId",       "Room names containing this string (case-insensitive) are treated as private. Example: '_private' matches 'lobby_private_123'." },
            { "autoMute",        "When enabled, violations trigger progressive punishments defined by the mute rules below." },
            { "maxMute",         "Maximum mute duration in minutes. Once the rules list is exhausted, all subsequent offenses use this cap." },
        };

        private readonly Dictionary<string, bool> _helpToggles = new();

        private void OnEnable()
        {
            _apiKey = serializedObject.FindProperty("_apiKey");
            _ageVerification = serializedObject.FindProperty("_ageVerification");
            _debugLogs = serializedObject.FindProperty("_debugLogs");
            _headless = serializedObject.FindProperty("_headless");
            _enablePrivateRoomFiltering = serializedObject.FindProperty("_enablePrivateRoomFiltering");
            _privateRoomIdentifier = serializedObject.FindProperty("_privateRoomIdentifier");
            _enableAutomatedMuting = serializedObject.FindProperty("_enableAutomatedMuting");
            _muteRules = serializedObject.FindProperty("_muteRules");
            _maxMuteDurationMinutes = serializedObject.FindProperty("_maxMuteDurationMinutes");

            EditorApplication.update += RepaintIfPlaying;
        }

        private void OnDisable()
        {
            EditorApplication.update -= RepaintIfPlaying;
        }

        private void RepaintIfPlaying()
        {
            if (Application.isPlaying)
                Repaint();
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            bool sdkRunning = VoicePatrolWrapper.IsSdkRunning();
            bool sdkMuted = sdkRunning && VoicePatrolWrapper.IsMuted();

            VoicePatrolBase instance = (VoicePatrolBase)target;

            DrawStatus(sdkRunning, sdkMuted, instance);
            DrawInputMeter(sdkRunning);
            _micSelector.Draw(sdkRunning);
            DrawRuntimeInfo(sdkRunning, instance);
            DrawConfiguration(sdkRunning);
            DrawPrivateRoomFiltering();
            DrawAutomatedMuting();
            DrawInfoBox(instance);
            DrawDebugActions(sdkRunning, instance);

            serializedObject.ApplyModifiedProperties();
        }

        #region Sections

        private void DrawStatus(bool sdkRunning, bool sdkMuted, VoicePatrolBase instance)
        {
            EditorGUILayout.Space(8);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PrefixLabel("Status", EditorStyles.boldLabel, EditorStyles.boldLabel);

            string statusText;
            Color statusColor;
            if (!sdkRunning)
            {
                statusText = "\u25cf SDK Not Running";
                statusColor = new Color(0.6f, 0.6f, 0.6f);
            }
            else if (sdkMuted)
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

            if (Application.isPlaying && sdkRunning && sdkMuted)
            {
                string reason = instance.CurrentMuteReason switch
                {
                    VoicePatrolBase.MuteReason.NotInitialized     => "SDK not yet initialized",
                    VoicePatrolBase.MuteReason.PunishmentMute     => "Player is under punishment mute",
                    VoicePatrolBase.MuteReason.PrivateRoom         => "In a private room \u2014 moderation disabled",
                    VoicePatrolBase.MuteReason.NotConnected        => "Not connected to a voice room",
                    VoicePatrolBase.MuteReason.NotTransmitting     => "Microphone not transmitting (self-muted or recorder disabled)",
                    VoicePatrolBase.MuteReason.InsufficientPlayers => "Waiting for 2+ players in room",
                    VoicePatrolBase.MuteReason.ForceMuted          => "Force mute active (SetForceMuted)",
                    _ => "Unknown"
                };

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.PrefixLabel("Reason");
                GUIStyle reasonStyle = new(EditorStyles.miniLabel) { normal = { textColor = new Color(1f, 0.75f, 0.2f) } };
                EditorGUILayout.LabelField(reason, reasonStyle);
                EditorGUILayout.EndHorizontal();
            }
        }

        private void DrawInputMeter(bool sdkRunning)
        {
            float raw = sdkRunning ? VoicePatrolWrapper.GetInputLevel() : 0f;
            _meterLevel = VoicePatrolInputMeter.Smooth(_meterLevel, raw);
            VoicePatrolInputMeter.Draw(_meterLevel, sdkRunning);
        }

        private void DrawRuntimeInfo(bool sdkRunning, VoicePatrolBase instance)
        {
            if (!Application.isPlaying || !sdkRunning) return;

            if (_enableAutomatedMuting.boolValue && instance.IsPunishmentMuted())
            {
                TimeSpan remaining = instance.GetRemainingPunishmentDuration();

                string timeStr;
                if (remaining.TotalMinutes < 1)
                    timeStr = $"{remaining.Seconds}s remaining";
                else if (remaining.TotalHours < 1)
                    timeStr = $"{remaining.Minutes}m {remaining.Seconds}s remaining";
                else
                    timeStr = $"{(int)remaining.TotalHours}h {remaining.Minutes}m remaining";

                EditorGUILayout.Space(2);
                EditorGUILayout.HelpBox(
                    $"\u26a0 PUNISHMENT MUTE \u2014 {timeStr}\n{instance.GetPunishmentMuteReason()}",
                    MessageType.Warning);
            }

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PrefixLabel("Violations");
            EditorGUILayout.LabelField(instance.GetViolationCount().ToString(), EditorStyles.boldLabel);
            EditorGUILayout.EndHorizontal();

            if (instance.IsCurrentRoomPrivate())
            {
                EditorGUILayout.HelpBox("Currently in a private room \u2014 moderation disabled.", MessageType.Info);
            }

            EditorGUILayout.Space(4);
            DrawSeparator();
        }

        private void DrawConfiguration(bool sdkRunning)
        {
            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("Configuration", EditorStyles.boldLabel);
            EditorGUILayout.Space(2);

            DrawFieldWithHelp("apiKey", _apiKey, "API Key");
            DrawFieldWithHelp("ageVerification", _ageVerification, "Age Verification", sdkRunning);
            DrawFieldWithHelp("debugLogs", _debugLogs, "Debug Logs");

            if (_headless != null)
                DrawFieldWithHelp("headless", _headless, "Headless Mode", sdkRunning);

            EditorGUILayout.Space(2);
            DrawSeparator();
        }

        private void DrawPrivateRoomFiltering()
        {
            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("Private Room Filtering", EditorStyles.boldLabel);
            EditorGUILayout.Space(2);

            DrawFieldWithHelp("privateFilter", _enablePrivateRoomFiltering, "Enable Filtering");

            if (_enablePrivateRoomFiltering.boolValue)
            {
                EditorGUI.indentLevel++;
                DrawFieldWithHelp("privateId", _privateRoomIdentifier, "Room Identifier");
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space(2);
            DrawSeparator();
        }

        private void DrawAutomatedMuting()
        {
            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("Automated Muting", EditorStyles.boldLabel);
            EditorGUILayout.Space(2);

            DrawFieldWithHelp("autoMute", _enableAutomatedMuting, "Enable Automated Muting");

            if (!_enableAutomatedMuting.boolValue)
            {
                EditorGUILayout.Space(2);
                DrawSeparator();
                return;
            }

            EditorGUI.indentLevel++;
            DrawFieldWithHelp("maxMute", _maxMuteDurationMinutes, "Max Mute (minutes)");
            EditorGUI.indentLevel--;

            EditorGUILayout.Space(6);

            _showMuteRules = EditorGUILayout.Foldout(_showMuteRules, $"Mute Rules ({_muteRules.arraySize})", true, EditorStyles.foldoutHeader);

            if (_showMuteRules)
            {
                EditorGUILayout.Space(4);

                EditorGUILayout.BeginHorizontal();
                GUILayout.Space(4);
                EditorGUILayout.LabelField("Offense", EditorStyles.miniLabel, GUILayout.Width(50));
                EditorGUILayout.LabelField("Action", EditorStyles.miniLabel);
                EditorGUILayout.LabelField("Minutes", EditorStyles.miniLabel, GUILayout.Width(60));
                GUILayout.Space(26);
                EditorGUILayout.EndHorizontal();

                Rect headerLine = EditorGUILayout.GetControlRect(false, 1);
                EditorGUI.DrawRect(headerLine, new Color(0.35f, 0.35f, 0.35f));

                if (_rowRects == null || _rowRects.Length != _muteRules.arraySize)
                    _rowRects = new Rect[_muteRules.arraySize];

                for (int i = 0; i < _muteRules.arraySize; i++)
                {
                    if (i % 2 == 1 && Event.current.type == EventType.Repaint && i < _rowRects.Length && _rowRects[i].height > 0)
                    {
                        EditorGUI.DrawRect(_rowRects[i], new Color(0.22f, 0.22f, 0.22f));
                    }

                    SerializedProperty rule = _muteRules.GetArrayElementAtIndex(i);
                    SerializedProperty action = rule.FindPropertyRelative("action");
                    SerializedProperty duration = rule.FindPropertyRelative("muteDurationMinutes");

                    EditorGUILayout.BeginHorizontal();
                    GUILayout.Space(4);

                    GUIStyle numStyle = new(EditorStyles.label)
                    {
                        alignment = TextAnchor.MiddleCenter,
                        normal = { textColor = new Color(0.55f, 0.55f, 0.55f) }
                    };
                    EditorGUILayout.LabelField($"#{i + 1}", numStyle, GUILayout.Width(50));

                    EditorGUILayout.PropertyField(action, GUIContent.none);

                    bool isMuteAction = action.enumValueIndex == (int)VoicePatrolBase.MuteAction.Mute;
                    if (isMuteAction)
                    {
                        duration.intValue = EditorGUILayout.IntField(duration.intValue, GUILayout.Width(60));
                    }
                    else
                    {
                        using (new EditorGUI.DisabledScope(true))
                        {
                            EditorGUILayout.LabelField("\u2014", new GUIStyle(EditorStyles.label)
                            {
                                alignment = TextAnchor.MiddleCenter
                            }, GUILayout.Width(60));
                        }
                    }

                    if (GUILayout.Button("\u2715", EditorStyles.miniButton, GUILayout.Width(22)))
                    {
                        _muteRules.DeleteArrayElementAtIndex(i);
                        break;
                    }

                    EditorGUILayout.EndHorizontal();

                    if (i < _rowRects.Length)
                        _rowRects[i] = GUILayoutUtility.GetLastRect();
                }

                EditorGUILayout.Space(4);

                EditorGUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("+ Add Rule", GUILayout.Width(90)))
                {
                    int newIndex = _muteRules.arraySize;
                    _muteRules.InsertArrayElementAtIndex(newIndex);

                    SerializedProperty newRule = _muteRules.GetArrayElementAtIndex(newIndex);
                    newRule.FindPropertyRelative("action").enumValueIndex = (int)VoicePatrolBase.MuteAction.Mute;
                    newRule.FindPropertyRelative("muteDurationMinutes").intValue =
                        serializedObject.FindProperty("_maxMuteDurationMinutes").intValue;
                }
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.Space(2);
            DrawSeparator();
        }

        private void DrawInfoBox(VoicePatrolBase instance)
        {
            EditorGUILayout.Space(6);

            bool headless = _headless != null && _headless.boolValue;

            string audioPath = headless
                ? $"Headless mode — no audio is sent from this client. {instance.ProviderName}'s servers stream " +
                  "voice directly to VoicePatrol, matched to this player by unique ID: your " +
                  $"{instance.ProviderName} identity (Client.UserId / PlayerId) must equal the unique ID passed " +
                  "to Initialize. Set it at login, before joining a room — a mismatch logs a warning at runtime."
                : $"This integration does not capture the microphone. It taps the outgoing {instance.ProviderName} voice stream — " +
                  "whatever the player transmits to the voice servers is also sent to VoicePatrol for moderation.";

            EditorGUILayout.HelpBox(
                "Call VoicePatrolBase.Instance?.Initialize(uniqueId); once the player has logged in. " +
                $"Voice room join/leave is detected automatically via the {instance.ProviderName} voice client.\n\n" +
                audioPath + "\n\n" +
                "Violation data is saved to encrypted local storage (PlayerPrefs + AES-128) by default. " +
                "Override SaveViolationData() and LoadViolationData() in a subclass to use your own backend.",
                MessageType.Info);
        }

        private void DrawDebugActions(bool sdkRunning, VoicePatrolBase instance)
        {
            if (!Application.isPlaying || !sdkRunning) return;

            EditorGUILayout.Space(6);
            DrawSeparator();
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Debug", EditorStyles.boldLabel);
            EditorGUILayout.Space(2);

            if (GUILayout.Button("Reset Violation Data"))
            {
                instance.ResetViolationData();
            }
        }

        #endregion

        #region Helpers

        private void DrawFieldWithHelp(string helpKey, SerializedProperty property, string label, bool disabled = false)
        {
            EditorGUILayout.BeginHorizontal();

            int savedIndent = EditorGUI.indentLevel;
            EditorGUI.indentLevel = 0;
            GUILayout.Space(savedIndent * 15f);

            if (GUILayout.Button("?", EditorStyles.miniButton, GUILayout.Width(20)))
            {
                _helpToggles.TryAdd(helpKey, false);
                _helpToggles[helpKey] = !_helpToggles[helpKey];
            }

            EditorGUI.indentLevel = savedIndent;

            using (new EditorGUI.DisabledScope(disabled))
            {
                EditorGUILayout.PropertyField(property, new GUIContent(label), GUILayout.ExpandWidth(true));
            }

            EditorGUILayout.EndHorizontal();

            if (_helpToggles.TryGetValue(helpKey, out bool show) && show && _helpContent.TryGetValue(helpKey, out string content))
            {
                EditorGUILayout.HelpBox(content, MessageType.Info);
                EditorGUILayout.Space(2);
            }
        }

        private static void DrawSeparator()
        {
            Rect rect = EditorGUILayout.GetControlRect(false, 1);
            rect.height = 1;
            EditorGUI.DrawRect(rect, new Color(0.3f, 0.3f, 0.3f));
        }

        #endregion
    }
}
#endif