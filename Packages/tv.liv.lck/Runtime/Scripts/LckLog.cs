using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using Liv.Lck.Core;
using Liv.Lck.Settings;

namespace Liv.Lck
{
  public enum LogLevel : int
  {
    None,
    Error,
    Warning,
    Info,
  }

  internal static class LckLog
  {
    private static readonly Queue<(LogType type, string message, string memberName, string filePath, int lineNumber)> _earlyLogs = new Queue<(LogType, string, string, string, int)>();
    private static bool _isInitialized = false;
    private static readonly object _lockObject = new object();

    /// <summary>
    /// Called by LckCoreHandler after lck-core is initialized.
    /// Flushes all buffered early logs to the native library.
    /// </summary>
    internal static void OnLckCoreInitialized()
    {
      lock (_lockObject)
      {
        _isInitialized = true;

        while (_earlyLogs.Count > 0)
        {
          var (type, message, memberName, filePath, lineNumber) = _earlyLogs.Dequeue();
          LckCore.Log(type, message, memberName, filePath, lineNumber);
        }
      }
    }

    public static void Log(string message, [CallerMemberName] string memberName = "", [CallerFilePath] string filePath = "", [CallerLineNumber] int lineNumber = 0)
    {
      if(ShouldPrint(LogLevel.Info))
      {
        UnityEngine.Debug.Log(message);
      }

      SendToLckCore(LogType.Info, message, memberName, GetFileName(filePath), lineNumber);
    }

    public static void LogWarning(string message, [CallerMemberName] string memberName = "", [CallerFilePath] string filePath = "", [CallerLineNumber] int lineNumber = 0)
    {
      if(ShouldPrint(LogLevel.Warning))
      {
        UnityEngine.Debug.LogWarning(message);
      }

      SendToLckCore(LogType.Warning, message, memberName, GetFileName(filePath), lineNumber);
    }

    public static void LogError(string message, [CallerMemberName] string memberName = "", [CallerFilePath] string filePath = "", [CallerLineNumber] int lineNumber = 0)
    {
      if(ShouldPrint(LogLevel.Error))
      {
        UnityEngine.Debug.LogError(message);
      }

      SendToLckCore(LogType.Error, message, memberName, GetFileName(filePath), lineNumber);
    }

    [System.Diagnostics.Conditional("LCK_TRACE")]
    public static void LogTrace(string message, [CallerMemberName] string memberName = "", [CallerFilePath] string filePath = "", [CallerLineNumber] int lineNumber = 0)
    {
      UnityEngine.Debug.Log(message);
      SendToLckCore(LogType.Trace, message, memberName, GetFileName(filePath), lineNumber);
    }

    private static void SendToLckCore(LogType type, string message, string memberName, string filePath, int lineNumber)
    {
      lock (_lockObject)
      {
        if (_isInitialized)
        {
          LckCore.Log(type, message, memberName, filePath, lineNumber);
        }
        else
        {
          _earlyLogs.Enqueue((type, message, memberName, filePath, lineNumber));
        }
      }
    }

    private static bool ShouldPrint(LogLevel level)
    {
      return (int)LckSettings.Instance.BaseLogLevel >= (int)level;
    }

    private static string GetFileName(string filePath)
    {
#if UNITY_EDITOR
      return Path.GetFileName(filePath);
#else
      int lastSlashIndex = filePath.LastIndexOfAny(new char[] { '/', '\\' });
      if (lastSlashIndex >= 0 && lastSlashIndex < filePath.Length - 1)
      {
        return filePath.Substring(lastSlashIndex + 1);
      }
      return filePath;
#endif
    }
  }
}
