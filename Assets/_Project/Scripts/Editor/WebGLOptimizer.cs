using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// AppsInToss 앱인토스 배포를 위한 WebGL 빌드 자동 최적화.
/// 빌드 전 자동으로 실행되어 최적 WebGL 설정을 적용합니다.
/// 참고: https://developers-apps-in-toss.toss.im/unity/optimization
/// </summary>
public class WebGLOptimizer : IPreprocessBuildWithReport
{
    public int callbackOrder => 0;

    public void OnPreprocessBuild(BuildReport report)
    {
        if (report.summary.platform != BuildTarget.WebGL) return;

        Debug.Log("[WebGLOptimizer] 앱인토스 WebGL 최적화 설정 적용 중...");

        // ─── 코드 최적화 ───────────────────────────────────────────
        // 미사용 엔진 코드 제거: 빌드 사이즈 감소에 핵심
        PlayerSettings.stripEngineCode = true;

        // ─── 압축 설정 ─────────────────────────────────────────────
        // Brotli or Gzip → 앱인토스 WebView는 Gzip을 지원, Decompression Fallback 필수
        // AppsInToss의 미니앱 서버가 gzip 헤더 없이 서빙하는 경우가 있어 fallback 필수
        PlayerSettings.WebGL.decompressionFallback = true;
        PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;

        // ─── 텍스처 압축 ───────────────────────────────────────────
        // 앱인토스 게임 권장: ASTC (iOS/Android 모두 지원, 화질↑ 용량↓)
        EditorUserBuildSettings.webGLBuildSubtarget = WebGLTextureSubtarget.ASTC;

        // ─── WebGL 메모리 최적화 ───────────────────────────────────
        // 앱인토스는 메모리 제약이 있으므로 힙 크기 제한
        // 디저트 팝 같은 캐주얼 게임은 256MB 이면 충분
        PlayerSettings.WebGL.memorySize = 256;

        // ─── 기타 성능 설정 ────────────────────────────────────────
        // 예외 처리 최소화 (성능 향상)
        // None → 크래시, Full → 빌드 크기 큼. Explicit이 최선의 균형
        PlayerSettings.WebGL.exceptionSupport = WebGLExceptionSupport.ExplicitlyThrownExceptionsOnly;

        // 링크 생성 (불필요한 IL2CPP 코드 제거)
        PlayerSettings.SetManagedStrippingLevel(
            BuildTargetGroup.WebGL,
            ManagedStrippingLevel.Minimal
        );

        // ─── WebGL 템플릿 자동 설정 ────────────────────────────────
        // Assets/WebGLTemplates/DessertPop/ 폴더가 있으면 자동 선택
        SetDessertPopTemplate();

        Debug.Log("[WebGLOptimizer] ✅ 설정 완료: Gzip+Fallback, ASTC, 256MB Heap, StripEngineCode ON");
    }

    public static void SetDessertPopTemplate()
    {
        const string templateId = "PROJECT:DessertPop";
        const string templatePath = "Assets/WebGLTemplates/DessertPop";

        if (System.IO.Directory.Exists(templatePath))
        {
            PlayerSettings.WebGL.template = templateId;
            Debug.Log("[WebGLOptimizer] ✅ WebGL Template → DessertPop");
        }
        else
        {
            Debug.LogWarning("[WebGLOptimizer] ⚠ WebGLTemplates/DessertPop 폴더 없음, 템플릿 설정 건너뜀");
        }
    }

    /// <summary>
    /// Unity 메뉴에서 수동으로 최적화 설정 미리보기/적용 가능
    /// </summary>
    // [MenuItem("DessertPop/Apply AppsInToss WebGL Settings", false, 50)] // Moved to IntegratedMenu
    public static void ApplySettingsManually()
    {
        ApplySettingsManuallyNoDialog();

        EditorUtility.DisplayDialog(
            "AppsInToss WebGL Settings Applied",
            "✅ 앱인토스 최적화 설정이 적용되었습니다.",
            "확인"
        );
    }

    public static void ApplySettingsManuallyNoDialog()
    {
        PlayerSettings.stripEngineCode = true;
        PlayerSettings.WebGL.decompressionFallback = true;
        PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
        EditorUserBuildSettings.webGLBuildSubtarget = WebGLTextureSubtarget.ASTC;
        PlayerSettings.WebGL.memorySize = 256;
        PlayerSettings.WebGL.exceptionSupport = WebGLExceptionSupport.ExplicitlyThrownExceptionsOnly;
        PlayerSettings.SetManagedStrippingLevel(BuildTargetGroup.WebGL, ManagedStrippingLevel.Minimal);
        SetDessertPopTemplate();
    }
}
