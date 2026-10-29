#if UNITY_EDITOR && !UNITY_SERVER && !UNITY_DEDICATED_SERVER
using UnityEditor;
using UnityEngine;

namespace VoicePatrol.Scripts.Editor.VoicePatrol.Scripts.Editor
{
    /// <summary>
    /// Shared "audio reaching the SDK" meter for the custom editors. Reflects the live
    /// input level the native SDK reports for whatever it is ingesting — the internal
    /// microphone or audio supplied via push_audio (e.g. a Photon tap) — so a developer
    /// can confirm at a glance that audio is actually getting into the SDK.
    /// </summary>
    internal static class VoicePatrolInputMeter
    {
        private static readonly Color Background = new(0.15f, 0.15f, 0.15f);
        private static readonly Color Quiet = new(0.30f, 0.80f, 0.40f);
        private static readonly Color Loud = new(1f, 0.70f, 0f);

        /// <summary>
        /// Eases a stored display level toward the latest raw level: fast attack so speech
        /// registers immediately, slower release so the bar visibly falls (and reaches zero)
        /// when ingestion stops — which is the signal developers are looking for.
        /// </summary>
        public static float Smooth(float displayed, float raw)
        {
            float t = raw > displayed ? 0.6f : 0.15f;
            return Mathf.Lerp(displayed, raw, t);
        }

        /// <summary>Draws a labelled level bar on its own inspector line.</summary>
        public static void Draw(float level, bool sdkRunning)
        {
            Rect line = EditorGUILayout.GetControlRect(false, 16f);

            float labelWidth = EditorGUIUtility.labelWidth;
            Rect labelRect = new(line.x, line.y, labelWidth, line.height);
            Rect barRect = new(line.x + labelWidth, line.y, line.width - labelWidth, line.height);

            EditorGUI.LabelField(labelRect, "Audio In");
            EditorGUI.DrawRect(barRect, Background);

            if (sdkRunning)
            {
                float w = Mathf.Clamp01(level);
                if (w > 0f)
                {
                    Rect fill = new(barRect.x, barRect.y, barRect.width * w, barRect.height);
                    EditorGUI.DrawRect(fill, Color.Lerp(Quiet, Loud, w));
                }
            }

            if (sdkRunning && level < 0.001f)
            {
                EditorGUILayout.HelpBox(
                    "No audio is reaching the SDK. If you expect input, check the device microphone " +
                    "or the integration's push path (e.g. that the voice tap is attached).",
                    MessageType.None);
            }
        }
    }
}
#endif
