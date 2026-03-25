using System.Diagnostics;
using UnityEngine;
using UnityDebug = UnityEngine.Debug;

/// <summary>
/// 배포 빌드에서 로그 노이즈를 제거하기 위한 공용 로그 유틸.
/// UNITY_EDITOR / DEVELOPMENT_BUILD 에서만 출력됩니다.
/// </summary>
public static class DebugUtil
{
    [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
    public static void Log(object message)
    {
        UnityDebug.Log(message);
    }

    [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
    public static void LogWarning(object message)
    {
        UnityDebug.LogWarning(message);
    }

    [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
    public static void LogError(object message)
    {
        UnityDebug.LogError(message);
    }
}
