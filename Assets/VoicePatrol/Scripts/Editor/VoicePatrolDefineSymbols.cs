#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace VoicePatrol.Scripts.Editor.VoicePatrol.Scripts.Editor
{
    /// <summary>
    /// Keeps VoicePatrol's voice-provider capability defines in sync with what is actually
    /// installed in the project, so the integration files compile if and only if the voice
    /// package they need is present.
    ///
    /// <b>Why this exists:</b> each integration must be gated on a *voice* symbol, never on a
    /// *networking* symbol. Photon's own FUSION_WEAVER / PUN_2_OR_NEWER describe the networking
    /// layer, and a project can pair that networking layer with a different voice provider
    /// (e.g. Fusion + Vivox). Gating Photon Voice code on FUSION_WEAVER then compiles it with no
    /// Photon Voice present and breaks the build on `using Photon.Voice.Unity;`.
    ///
    /// Detection keys on the provider's runtime <b>type</b> (stable across versions) rather than
    /// on assembly-definition names (which differ between provider releases), so it does not
    /// break when a provider renames its assemblies. Defines are added when a provider is found
    /// and removed when it disappears, which prevents stale-define hard errors after a package
    /// is deleted.
    /// </summary>
    [InitializeOnLoad]
    internal static class VoicePatrolDefineSymbols
    {
        private const string LogPrefix = "<color=#00D5FF>[VoicePatrol]</color>";

        // Marker type  ->  capability define gating the matching integration file.
        private static readonly (string Type, string Define)[] Map =
        {
            ("Photon.Voice.Unity.VoiceConnection",  "VOICEPATROL_PHOTON_VOICE"),
            ("Photon.Voice.PUN.PhotonVoiceNetwork", "VOICEPATROL_PHOTON_VOICE_PUN"),
            ("Unity.Services.Vivox.VivoxService",   "VOICEPATROL_VIVOX"),
            ("Dissonance.DissonanceComms",          "VOICEPATROL_DISSONANCE"),
        };

        static VoicePatrolDefineSymbols()
        {
            // Defer so detection runs after the editor's assemblies have finished loading.
            EditorApplication.delayCall += Sync;
        }

        private static void Sync()
        {
            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();

            HashSet<string> present = new();
            foreach ((string type, string define) in Map)
            {
                if (TypeExists(assemblies, type))
                    present.Add(define);
            }

            foreach (BuildTargetGroup group in TargetGroups())
                Apply(group, present);
        }

        private static bool TypeExists(Assembly[] assemblies, string fullName)
        {
            for (int i = 0; i < assemblies.Length; i++)
            {
                try
                {
                    if (assemblies[i].GetType(fullName, throwOnError: false) != null)
                        return true;
                }
                catch
                {
                    // Dynamic / reflection-only assemblies can throw — ignore and continue.
                }
            }
            return false;
        }

        private static IEnumerable<BuildTargetGroup> TargetGroups()
        {
            // The active group plus VoicePatrol's shipping targets, so a build for any of them
            // sees the define regardless of which platform is currently selected in the editor.
            HashSet<BuildTargetGroup> groups = new()
            {
                BuildPipeline.GetBuildTargetGroup(EditorUserBuildSettings.activeBuildTarget),
                BuildTargetGroup.Standalone,
                BuildTargetGroup.Android,
                BuildTargetGroup.iOS,
            };
            groups.RemoveWhere(g => g == BuildTargetGroup.Unknown);
            return groups;
        }

        private static void Apply(BuildTargetGroup group, HashSet<string> present)
        {
            string current;
            try
            {
                // Group-based API kept for broad version compatibility (2019-2023). On Unity 6
                // it is deprecated in favour of the NamedBuildTarget overloads but still works.
#pragma warning disable CS0618
                current = PlayerSettings.GetScriptingDefineSymbolsForGroup(group);
#pragma warning restore CS0618
            }
            catch
            {
                return; // Unsupported group on this editor — skip it.
            }

            List<string> symbols = new(
                (current ?? string.Empty).Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries));

            bool changed = false;

            foreach (string define in present)
            {
                if (!symbols.Contains(define))
                {
                    symbols.Add(define);
                    changed = true;
                }
            }

            foreach ((string _, string define) in Map)
            {
                if (!present.Contains(define) && symbols.Remove(define))
                    changed = true;
            }

            if (!changed) return;

            try
            {
#pragma warning disable CS0618
                PlayerSettings.SetScriptingDefineSymbolsForGroup(group, string.Join(";", symbols));
#pragma warning restore CS0618
                Debug.Log($"{LogPrefix} Updated voice-provider defines for {group}: " +
                          $"{(present.Count == 0 ? "(none detected)" : string.Join(", ", present))}");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"{LogPrefix} Could not update define symbols for {group}: {ex.Message}");
            }
        }
    }
}
#endif
