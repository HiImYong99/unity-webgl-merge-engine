using UnityEngine;
using System.Diagnostics;

public static class DebugUtil
{
    [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
    public static void Log(string msg) => UnityEngine.Debug.Log(msg);

    [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
    public static void LogWarning(string msg) => UnityEngine.Debug.LogWarning(msg);

    [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
    public static void LogError(string msg) => UnityEngine.Debug.LogError(msg);
}
