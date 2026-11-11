#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using Unity.Multiplayer.PlayMode;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Test-only bridge that lets an agent drive Multiplayer Play Mode virtual players, which Unity MCP
/// cannot reach. Each virtual player polls Temp/rails-remote/&lt;role&gt;-in.cs (in the main project),
/// compiles the CommandScript with the Unity AI Assistant RunCommand builder and writes the logs to
/// &lt;role&gt;-out.txt. The first line of the input must be "// id: &lt;unique&gt;". Inert unless a file is written.
/// Roles are the player's first MPPM tag (Player2, Player3...).
/// </summary>
[InitializeOnLoad]
public static class RailRemoteBridge
{
    static string Root
    {
        get { string p = Application.dataPath.Replace('\\', '/'); int i = p.IndexOf("/Library/VP/", StringComparison.OrdinalIgnoreCase); return i < 0 ? Path.GetDirectoryName(p) : p.Substring(0, i); }
    }
    static string Role => CurrentPlayer.IsMainEditor ? "main" : CurrentPlayer.Tags.FirstOrDefault() ?? "unknown";
    static string Folder => Path.Combine(Root, "Temp/rails-remote");
    static string lastId;
    static double next;

    static RailRemoteBridge()
    {
        if (CurrentPlayer.IsMainEditor) return;
        EditorApplication.update += Tick;
    }

    static void Tick()
    {
        if (EditorApplication.timeSinceStartup < next || EditorApplication.isCompiling) return;
        next = EditorApplication.timeSinceStartup + .25;
        string input = Path.Combine(Folder, Role + "-in.cs");
        if (!File.Exists(input)) return;
        string code;
        try { code = File.ReadAllText(input); } catch (IOException) { return; }
        string first = code.Split('\n')[0].Trim();
        if (!first.StartsWith("// id:", StringComparison.Ordinal)) return;
        string id = first.Substring(6).Trim();
        if (id == lastId) return;
        lastId = id;
        string output;
        try { output = Run(code); }
        catch (Exception e) { output = "BRIDGE EXCEPTION: " + (e.InnerException ?? e); }
        File.WriteAllText(Path.Combine(Folder, Role + "-out.txt"), "id: " + id + "\nrole: " + Role + " playing=" + EditorApplication.isPlaying + "\n" + output);
    }

    /// <summary>Reflection helpers for command scripts (RunCommand forbids System.Reflection imports).</summary>
    const BindingFlags Any = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    public static object Call(object target, string method, params object[] args)
    {
        var type = target as Type ?? target.GetType();
        for (var t = type; t != null; t = t.BaseType)
        {
            var m = t.GetMethods(Any).FirstOrDefault(x => x.Name == method && x.GetParameters().Length == args.Length);
            if (m != null) return m.Invoke(target is Type ? null : target, args);
        }
        throw new MissingMethodException(type.Name, method);
    }
    public static object Get(object target, string member)
    {
        var type = target as Type ?? target.GetType(); object instance = target is Type ? null : target;
        for (var t = type; t != null; t = t.BaseType)
        {
            var f = t.GetField(member, Any); if (f != null) return f.GetValue(instance);
            var p = t.GetProperty(member, Any); if (p != null) return p.GetValue(instance);
        }
        throw new MissingMemberException(type.Name, member);
    }
    public static void Set(object target, string member, object value)
    {
        var type = target as Type ?? target.GetType(); object instance = target is Type ? null : target;
        for (var t = type; t != null; t = t.BaseType)
        {
            var f = t.GetField(member, Any); if (f != null) { f.SetValue(instance, value); return; }
            var p = t.GetProperty(member, Any); if (p != null) { p.SetValue(instance, value); return; }
        }
        throw new MissingMemberException(type.Name, member);
    }

    /// <summary>Commits room properties as key/value pairs (command scripts cannot reference Photon's Hashtable).</summary>
    public static bool Commit(params object[] pairs)
    {
        var table = new ExitGames.Client.Photon.Hashtable();
        for (int i = 0; i + 1 < pairs.Length; i += 2) table[pairs[i]] = pairs[i + 1];
        return PhotonSession.Instance != null && PhotonSession.Instance.Commit(table);
    }

    static string Run(string code)
    {
        var utils = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("Unity.AI.Assistant.Editor.RunCommand.RunCommandUtils")).FirstOrDefault(t => t != null)
            ?? throw new InvalidOperationException("Unity AI Assistant RunCommandUtils not found");
        const BindingFlags F = BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public;
        object command = utils.GetMethod("BuildRunCommand", F).Invoke(null, new object[] { code });
        var type = command.GetType();
        if (!(bool)type.GetField("CompilationSuccess").GetValue(command))
            return "COMPILE FAILED\n" + type.GetProperty("UnauthorizedNamespaceError").GetValue(command) + "\n" + type.GetProperty("CompilationErrors").GetValue(command);
        object result = utils.GetMethod("Execute", F).Invoke(null, new object[] { command, "remote" });
        var logs = result.GetType().GetMethod("GetFormattedLogs")?.Invoke(result, null) as IEnumerable;
        return logs == null ? "(no logs)" : string.Join("\n", logs.Cast<object>());
    }
}
#endif
