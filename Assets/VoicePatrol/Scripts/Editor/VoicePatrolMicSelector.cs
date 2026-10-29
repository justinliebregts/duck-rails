#if UNITY_EDITOR && !UNITY_SERVER && !UNITY_DEDICATED_SERVER
using System;
using UnityEditor;
using UnityEngine;
using VoicePatrol.Scripts.VoicePatrol.Scripts.Runtime;

namespace VoicePatrol.Scripts.Editor.VoicePatrol.Scripts.Editor
{
    /// <summary>
    /// Runtime microphone picker for the custom editors. It only appears when the SDK is
    /// running and capturing from its own microphone; for provider integrations that disable
    /// the SDK mic (Photon, Vivox via push_audio) it stays hidden, so the same call is safe
    /// from every editor. Switching is applied immediately via the native SDK, which re-opens
    /// the capture device without interrupting the stream or moderation pipeline.
    /// </summary>
    internal sealed class VoicePatrolMicSelector
    {
        private static readonly GUIContent MicLabel =
            new("Microphone", "Switch the SDK's capture device at runtime.");
        private static readonly GUIContent RefreshLabel =
            new("\u21bb", "Re-scan for microphones");
        private const string LogPrefix = "<color=#00D5FF>[VoicePatrol]</color>";

        private string[] _names = Array.Empty<string>();
        private GUIContent[] _labels = Array.Empty<GUIContent>();
        private int _index = -1;
        private bool _wasAvailable;

        public void Draw(bool sdkRunning)
        {
            bool available = sdkRunning && !VoicePatrolWrapper.IsMicrophoneDisabled();

            // Scan once when the SDK mic becomes active; forget the list when it goes away.
            if (available && !_wasAvailable) Refresh();
            else if (!available && _wasAvailable) Clear();
            _wasAvailable = available;

            if (!available) return; // SDK stopped, or mic disabled (provider integration)

            EditorGUILayout.BeginHorizontal();

            if (_names.Length == 0)
            {
                EditorGUILayout.LabelField(MicLabel, new GUIContent("No microphones found"));
            }
            else
            {
                EditorGUI.BeginChangeCheck();
                int sel = EditorGUILayout.Popup(MicLabel, _index, _labels);
                if (EditorGUI.EndChangeCheck() && sel >= 0 && sel < _names.Length)
                {
                    _index = sel;
                    string result = VoicePatrolWrapper.SetInputDevice(_names[sel]);
                    Debug.Log($"{LogPrefix} Microphone \u2192 {_names[sel]}: {result}");
                }
            }

            if (GUILayout.Button(RefreshLabel, EditorStyles.miniButton, GUILayout.Width(26)))
                Refresh();

            EditorGUILayout.EndHorizontal();
        }

        private void Refresh()
        {
            VoicePatrolWrapper.InputDeviceInfo[] devices = VoicePatrolWrapper.GetInputDevices();

            _names = new string[devices.Length];
            _labels = new GUIContent[devices.Length];
            for (int i = 0; i < devices.Length; i++)
            {
                _names[i] = devices[i].Name;
                _labels[i] = new GUIContent(devices[i].IsDefault ? devices[i].Name + "  (default)" : devices[i].Name);
            }

            // Select whatever the SDK reports as current; fall back to the default entry.
            string current = VoicePatrolWrapper.GetCurrentInputDevice();
            _index = Array.FindIndex(_names, n => n == current);
            if (_index < 0)
            {
                _index = Array.FindIndex(devices, d => d.IsDefault);
                if (_index < 0 && _names.Length > 0) _index = 0;
            }
        }

        private void Clear()
        {
            _names = Array.Empty<string>();
            _labels = Array.Empty<GUIContent>();
            _index = -1;
        }
    }
}
#endif
