using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

// Shared by the Unity build hook and Tools/check_logging.ps1. No Unity dependency.
public static class LoggingPolicyRules
{
    static readonly Regex Calls = new Regex(@"\b(?:UnityEngine\s*\.\s*)?Debug\s*\.\s*(Log\w*|Assert\w*|unityLogger)\b|\bprint\s*\(");
    static readonly Regex Aliases = new Regex(@"\busing\s+(?:static\s+UnityEngine\s*\.\s*Debug|\w+\s*=\s*(?:UnityEngine\s*\.\s*Debug|UnityEngine))\s*;");
    // Preserve line numbers while removing comments and ordinary/verbatim string literals.
    static readonly Regex NonCode = new Regex("@\"(?:\"\"|[^\"])*\"|\"(?:\\\\.|[^\"\\\\])*\"|'(?:\\\\.|[^'\\\\])*'|//[^\\r\\n]*|/\\*[\\s\\S]*?\\*/");

    public static List<string> CheckSource(string path, string source)
    {
        var failures = new List<string>();
        string code = NonCode.Replace(source, m => Regex.Replace(m.Value, @"[^\r\n]", " "));
        var lines = code.Replace("\r", "").Split('\n');
        var editorBranches = new Stack<int>();
        int editorDepth = 0;
        string pendingNotice = null;
        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i].Trim();
            if (line.StartsWith("#if ", StringComparison.Ordinal))
            {
                // Only exclude explicit editor-only sections; inspect both branches of
                // other symbols, including diagnostics and development-build symbols.
                int editor = line == "#if UNITY_EDITOR" ? 1 : line == "#if !UNITY_EDITOR" ? -1 : 0;
                editorBranches.Push(editor);
                if (editor == 1) editorDepth++;
                continue;
            }
            if ((line == "#else" || line.StartsWith("#elif ", StringComparison.Ordinal)) && editorBranches.Count > 0)
            {
                int editor = editorBranches.Pop();
                if (editor == 1) editorDepth--;
                editor = line == "#else" ? -editor : 0;
                editorBranches.Push(editor);
                if (editor == 1) editorDepth++;
                continue;
            }
            if (line == "#endif" && editorBranches.Count > 0)
            {
                if (editorBranches.Pop() == 1) editorDepth--;
                continue;
            }
            if (editorDepth > 0 || line.Length == 0) continue;
            if (pendingNotice != null)
            {
                if (!Regex.IsMatch(line, @"^" + Regex.Escape(pendingNotice) + @"\.Write\s*\("))
                    failures.Add(path + ":" + (i + 1) + ": Write the allowed notice immediately so message construction stays inside the guard.");
                pendingNotice = null;
            }
            if (Aliases.IsMatch(line)) failures.Add(path + ":" + (i + 1) + ": Debug/UnityEngine aliases bypass logging policy.");
            foreach (Match call in Calls.Matches(line))
            {
                failures.Add(path + ":" + (i + 1) + ": Use DuckzLog.Log or an allowed Notice.Write; direct runtime logging bypasses repeat/overflow protection.");
            }
            if (Regex.IsMatch(line, @"\bDuckzLog\.Allow(?:Warning|Error)\b"))
            {
                var guard = Regex.Match(line, @"^(?:else\s+)?if\s*\(.*\bDuckzLog\.Allow(?:Warning|Error)\(out var (\w+)(?:\s*,[^;]*)?\)\s*\)$");
                if (!guard.Success || line.Contains("||") || line.Contains("!DuckzLog"))
                    failures.Add(path + ":" + (i + 1) + ": Use an unbraced if (DuckzLog.AllowWarning/Error(out var notice, ...)) guard.");
                else pendingNotice = guard.Groups[1].Value;
            }
        }
        if (pendingNotice != null) failures.Add(path + ": missing Notice.Write after logging guard.");
        return failures;
    }

    public static List<string> CheckProject(string root)
    {
        var failures = new List<string>();
        string scripts = Path.Combine(root, "Assets", "Scripts");
        foreach (string file in Directory.GetFiles(scripts, "*.cs", SearchOption.AllDirectories))
        {
            string relative = file.Substring(root.TrimEnd('/', '\\').Length + 1).Replace('\\', '/');
            if (relative.Contains("/Editor/") || relative == "Assets/Scripts/Utility/DuckzLog.cs") continue;
            failures.AddRange(CheckSource(relative, File.ReadAllText(file)));
        }
        return failures;
    }
}
