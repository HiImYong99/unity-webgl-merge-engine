using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;
using System.IO;
using AppsInToss;

/// <summary>
/// SDK 3.x 전환용 빌드 진입점.
///
/// SDK의 AppsInTossMenu.BuildAndPackage는 WebGL 템플릿을 PROJECT:AITTemplate으로 강제하는데
/// (AITBuildInitializer.Init), 이 게임의 UI·데일리챌린지·출석스트릭·업적·GameBridge는 전부
/// Assets/WebGLTemplates/AnimalPop/index.html 에 들어 있어 그대로 쓰면 게임 셸이 통째로 바뀐다.
///
/// 그래서 2단계 파이프라인을 쪼갠다.
///   1) WebGL 빌드는 여기서 직접 — PlayerSettings의 프로젝트 템플릿(AnimalPop)을 유지한 채 webgl/ 로 출력
///   2) 패키징만 SDK 3.x에 위임 — DoExport(buildWebGL: false, doPackaging: true)
///
/// 실행: Unity -quit -batchmode -executeMethod AITPackageOnlyBuild.BuildWithProjectTemplate
/// </summary>
public static class AITPackageOnlyBuild
{
    private static readonly string WEBGL_OUTPUT =
        Path.GetFullPath(Path.Combine(Application.dataPath, "../webgl"));

    public static void BuildWithProjectTemplate()
    {
        Debug.Log($"[PackageOnly] WebGL 템플릿: {PlayerSettings.WebGL.template}");
        Debug.Log($"[PackageOnly] 출력 경로: {WEBGL_OUTPUT}");

        WebGLOptimizer.ApplySettingsManuallyNoDialog();

        var options = new BuildPlayerOptions
        {
            scenes = new[] { "Assets/_Project/Scenes/MainGame.unity" },
            locationPathName = WEBGL_OUTPUT,
            target = BuildTarget.WebGL,
            options = BuildOptions.None,
        };

        BuildReport report = BuildPipeline.BuildPlayer(options);
        if (report.summary.result != BuildResult.Succeeded)
        {
            Debug.LogError($"[PackageOnly] WebGL 빌드 실패: {report.summary.result}");
            EditorApplication.Exit(1);
            return;
        }

        Debug.Log($"[PackageOnly] WebGL 빌드 성공 ({report.summary.totalSize / 1024 / 1024} MB) — 패키징 위임");

        var err = AITConvertCore.DoExport(buildWebGL: false, doPackaging: true, cleanBuild: false);
        if (err != AITConvertCore.AITExportError.SUCCEED)
        {
            Debug.LogError($"[PackageOnly] 패키징 실패: {err}");
            EditorApplication.Exit(1);
            return;
        }

        Debug.Log("[PackageOnly] 패키징 완료");
        EditorApplication.Exit(0);
    }
}
