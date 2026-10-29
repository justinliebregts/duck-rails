using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;

namespace Liv.Lck.Util
{
    internal static class ScriptingDefineUtils
    {
        private const char DefineSeparator = ';';
        
        public static void AddDefine(string define)
        {
            if (HasDefine(define))
                return;
            
#if UNITY_2021_2_OR_NEWER
            var target = NamedBuildTarget.FromBuildTargetGroup(
                EditorUserBuildSettings.selectedBuildTargetGroup
            );

            var defines = PlayerSettings.GetScriptingDefineSymbols(target);
            defines += $"{DefineSeparator}{define}";
            PlayerSettings.SetScriptingDefineSymbols(target, defines);
#else
        var group = EditorUserBuildSettings.selectedBuildTargetGroup;

        var defines = PlayerSettings
            .GetScriptingDefineSymbolsForGroup(group)
            .Split(DefineSeparator)
            .Where(d => !string.IsNullOrEmpty(d))
            .ToList();

        if (!defines.Contains(define))
        {
            defines.Add(define);
            PlayerSettings.SetScriptingDefineSymbolsForGroup(
                group,
                string.Join(DefineSeparator.ToString(), defines)
            );
        }
#endif
        }

        public static void RemoveDefine(string define)
        {
            if (!HasDefine(define))
                return;
            
#if UNITY_2021_2_OR_NEWER
            var target = NamedBuildTarget.FromBuildTargetGroup(
                EditorUserBuildSettings.selectedBuildTargetGroup
            );

            var defines = PlayerSettings.GetScriptingDefineSymbols(target);
            var definesList = defines.Split(DefineSeparator).ToList();
            if (definesList.Remove(define))
            {
                defines = string.Join(DefineSeparator, definesList);
                PlayerSettings.SetScriptingDefineSymbols(target, defines);
            }
#else
        var group = EditorUserBuildSettings.selectedBuildTargetGroup;

        var defines = PlayerSettings
            .GetScriptingDefineSymbolsForGroup(group)
            .Split(DefineSeparator)
            .Where(d => !string.IsNullOrEmpty(d))
            .ToList();

        if (defines.Remove(define))
        {
            PlayerSettings.SetScriptingDefineSymbolsForGroup(
                group,
                string.Join(DefineSeparator.ToString(), defines)
            );
        }
#endif
        }

        public static void RemoveDefines(IEnumerable<string> defines)
        {
            foreach (var define in defines)
            {
                RemoveDefine(define);
            }
        }

        public static bool HasDefine(string define)
        {
#if UNITY_2021_2_OR_NEWER
            var target = NamedBuildTarget.FromBuildTargetGroup(
                EditorUserBuildSettings.selectedBuildTargetGroup
            );

            return PlayerSettings
                .GetScriptingDefineSymbols(target)
                .Contains(define);
#else
        var group = EditorUserBuildSettings.selectedBuildTargetGroup;

        return PlayerSettings
            .GetScriptingDefineSymbolsForGroup(group)
            .Contains(define);
#endif
        }

        public static bool HasAny(IEnumerable<string> defines) => defines.Any(HasDefine);
    }
}
