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

    public static void BuildWebGL()
    {
        Debug.Log("[AITBuildScript] ▶ WebGL 빌드 시작");
        Debug.Log($"[AITBuildScript] 출력 경로: {OUTPUT_PATH}");

        // 최적화 설정 먼저 적용
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
}
