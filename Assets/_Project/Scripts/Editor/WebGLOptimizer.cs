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
        // Brotli: Gzip 대비 ~20% 더 작고 브라우저 네이티브 압축 해제로 빠름
        // vite.config.ts의 unityWebContentEncodingPlugin이 Content-Encoding 헤더를 처리
        PlayerSettings.WebGL.decompressionFallback = false; // Brotli는 모던 모바일 브라우저에서 네이티브 지원
        PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Brotli;

        // ─── 텍스처 압축 ───────────────────────────────────────────
        // 앱인토스 게임 권장: ASTC (iOS/Android 모두 지원, 화질↑ 용량↓)
        EditorUserBuildSettings.webGLBuildSubtarget = WebGLTextureSubtarget.ASTC;

        // ─── WebGL 메모리 최적화 ───────────────────────────────────
        // Unity 2022.3+: 고정 메모리(memorySize) 대신 WebGL Memory Growth 활성화 권장
        // OOM(Out of Memory) 방지를 위해 필요에 따라 힙 메모리를 자동 확장함.
        // 초기 메모리는 128~256MB 정도로 시작하되, 성장을 허용하여 크래시 방지.
        // [참고] WebGL2.0 + ASTC 환경에서는 메모리 효율이 높지만 일부 구형 PC 브라우저에서 호환성 주의
        
        // PlayerSettings.WebGL.memorySize = 256; // [LEGACY] 제거
        // 2022.1+ 에서 memorySize는 deprecated 되었을 수 있으며, initialMemorySize를 사용함.
#if UNITY_2021_1_OR_NEWER
        // PlayerSettings.WebGL.initialMemorySize = 256; 
#endif

        // ─── 기타 성능 설정 ────────────────────────────────────────
        // 예외 처리 최소화 (성능 향상)
        // None → 크래시, Full → 빌드 크기 큼. Explicit이 최선의 균형
        PlayerSettings.WebGL.exceptionSupport = WebGLExceptionSupport.ExplicitlyThrownExceptionsOnly;

        // 링크 생성 (불필요한 IL2CPP 코드 제거) - Low로 올려 빌드 크기 감소
        PlayerSettings.SetManagedStrippingLevel(
            BuildTargetGroup.WebGL,
            ManagedStrippingLevel.Medium
        );

        // ─── WebGL 템플릿 자동 설정 ────────────────────────────────
        // Assets/WebGLTemplates/AnimalPop/ 폴더가 있으면 자동 선택
        SetAnimalPopTemplate();

        Debug.Log("[WebGLOptimizer] ✅ 설정 완료: Brotli, ASTC, StripEngineCode ON, Stripping=Low");
    }

    public static void SetAnimalPopTemplate()
    {
        const string templateId = "PROJECT:AnimalPop";
        const string templatePath = "Assets/WebGLTemplates/AnimalPop";

        if (System.IO.Directory.Exists(templatePath))
        {
            PlayerSettings.WebGL.template = templateId;
            Debug.Log("[WebGLOptimizer] ✅ WebGL Template → AnimalPop");
        }
        else
        {
            Debug.LogWarning("[WebGLOptimizer] ⚠ WebGLTemplates/AnimalPop 폴더 없음, 템플릿 설정 건너뜀");
        }
    }

    /// <summary>
    /// Unity 메뉴에서 수동으로 최적화 설정 미리보기/적용 가능
    /// </summary>
    // [MenuItem("AnimalPop/Apply AppsInToss WebGL Settings", false, 50)] // Moved to IntegratedMenu
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
        PlayerSettings.WebGL.decompressionFallback = false;
        PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Brotli;
        EditorUserBuildSettings.webGLBuildSubtarget = WebGLTextureSubtarget.ASTC;
        PlayerSettings.WebGL.exceptionSupport = WebGLExceptionSupport.ExplicitlyThrownExceptionsOnly;
        PlayerSettings.SetManagedStrippingLevel(BuildTargetGroup.WebGL, ManagedStrippingLevel.Low);
        SetAnimalPopTemplate();
    }
}
