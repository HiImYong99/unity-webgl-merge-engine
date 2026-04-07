using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;
using System.IO;

/// <summary>
/// AppsInToss 앱인토스용 WebGL 배치 빌드 스크립트
/// 실행: Unity -batchmode -executeMethod AITBuildScript.BuildWebGL
/// 빌드 결과물: ait-build/public/Build
/// </summary>
public class AITBuildScript
{
    private static readonly string OUTPUT_PATH =
        Path.GetFullPath(Path.Combine(Application.dataPath, "../ait-build/public"));

    private static readonly string ANDROID_OUTPUT_PATH =
        Path.GetFullPath(Path.Combine(Application.dataPath, "../android-wrapper/app/src/main/assets"));

    public static void BuildWebGL()
    {
        Debug.Log("[AITBuildScript] ▶ WebGL 빌드 시작 (토스/Brotli)");
        Debug.Log($"[AITBuildScript] 출력 경로: {OUTPUT_PATH}");

        // 토스용: Brotli 압축
        WebGLOptimizer.ApplySettingsManuallyNoDialog();

        // 빌드 옵션 구성
        BuildPlayerOptions options = new BuildPlayerOptions
        {
            scenes = new[] { "Assets/_Project/Scenes/MainGame.unity" },
            locationPathName = OUTPUT_PATH,
            target = BuildTarget.WebGL,
            options = BuildOptions.None,
        };

        BuildReport report = BuildPipeline.BuildPlayer(options);
        BuildSummary summary = report.summary;

        if (summary.result == BuildResult.Succeeded)
        {
            Debug.Log($"[AITBuildScript] ✅ 빌드 성공! 크기: {summary.totalSize / 1024 / 1024} MB, 경로: {OUTPUT_PATH}");

            // Unity 빌드 시 생성된 index.html(올바른 해시 포함)을 ait-build 루트로 복사
            string generatedHtml = Path.Combine(OUTPUT_PATH, "index.html");
            string targetHtml = Path.GetFullPath(Path.Combine(Application.dataPath, "../ait-build/index.html"));
            if (File.Exists(generatedHtml))
            {
                File.Copy(generatedHtml, targetHtml, overwrite: true);
                Debug.Log($"[AITBuildScript] ✅ index.html 복사 완료 → {targetHtml}");
            }

            EditorApplication.Exit(0);
        }
        else
        {
            Debug.LogError($"[AITBuildScript] ❌ 빌드 실패: {summary.result}");
            EditorApplication.Exit(1);
        }
    }

    /// <summary>
    /// Android용 WebGL 빌드 (비압축, decompressionFallback 활성)
    /// 실행: Unity -batchmode -executeMethod AITBuildScript.BuildWebGLForAndroid
    /// </summary>
    public static void BuildWebGLForAndroid()
    {
        Debug.Log("[AITBuildScript] ▶ Android용 WebGL 빌드 시작 (비압축)");
        Debug.Log($"[AITBuildScript] 출력 경로: {ANDROID_OUTPUT_PATH}");

        // WebGLOptimizer의 IPreprocessBuild가 Brotli로 덮어씌우는 것을 방지
        WebGLOptimizer.SkipAutoOptimize = true;

        // Android WebView용: 비압축 (Brotli/Gzip 디코딩 미지원)
        PlayerSettings.stripEngineCode = true;
        PlayerSettings.WebGL.decompressionFallback = false;
        PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Disabled;
        EditorUserBuildSettings.webGLBuildSubtarget = WebGLTextureSubtarget.ASTC;
        PlayerSettings.WebGL.exceptionSupport = WebGLExceptionSupport.ExplicitlyThrownExceptionsOnly;
        PlayerSettings.SetManagedStrippingLevel(BuildTargetGroup.WebGL, ManagedStrippingLevel.Medium);
        WebGLOptimizer.SetAnimalPopTemplate();

        BuildPlayerOptions options = new BuildPlayerOptions
        {
            scenes = new[] { "Assets/_Project/Scenes/MainGame.unity" },
            locationPathName = ANDROID_OUTPUT_PATH,
            target = BuildTarget.WebGL,
            options = BuildOptions.None,
        };

        BuildReport report = BuildPipeline.BuildPlayer(options);
        BuildSummary summary = report.summary;

        if (summary.result == BuildResult.Succeeded)
        {
            Debug.Log($"[AITBuildScript] ✅ Android용 빌드 성공! 크기: {summary.totalSize / 1024 / 1024} MB");
            EditorApplication.Exit(0);
        }
        else
        {
            Debug.LogError($"[AITBuildScript] ❌ 빌드 실패: {summary.result}");
            EditorApplication.Exit(1);
        }
    }
}
