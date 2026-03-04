using UnityEngine;
using UnityEditor;

public class DessertPopIntegratedMenu
{
    [MenuItem("DessertPop/🚀 1-Step Full Production Setup", false, 0)]
    public static void FullProductionSetup()
    {
        if (!EditorUtility.DisplayDialog("Full Setup", 
            "이 작업은 다음 과정을 한 번에 수행합니다:\n" +
            "1. 씬 구조 초기화 (카메라, 용기, 매니저)\n" +
            "2. 디저트 스프라이트 배경 제거 및 프리팹 연결\n" +
            "3. 효과음 자동 할당\n" +
            "4. 앱인토스 최적화 빌드 설정 적용\n\n" +
            "계속하시겠습니까?", "예", "취소")) return;

        // 1. Scene Setup
        DessertPopSetup.AutoSetupSceneNoDialog();

        // 2. Asset Link (Sprites + SFX)
        DessertAssetLinker.LinkIndividualSprites(); // Handles Pixels & Transparency
        DessertPopSetup.LinkAudioAssets();         // Handles SFX & Button Events

        // 3. WebGL Optimization
        WebGLOptimizer.ApplySettingsManuallyNoDialog();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        EditorUtility.DisplayDialog("Setup Complete", 
            "🚀 모든 설정이 완료되었습니다!\n빌드 및 배포 준비가 끝났습니다.", "확인");
    }

    [MenuItem("DessertPop/🎨 Update Sprites & SFX Only", false, 10)]
    public static void UpdateAssetsOnly()
    {
        DessertAssetLinker.LinkIndividualSprites();
        DessertPopSetup.LinkAudioAssets();
    }

    [MenuItem("DessertPop/🔧 Apply AppsInToss WebGL Settings", false, 20)]
    public static void OptimizeOnly()
    {
        WebGLOptimizer.ApplySettingsManually();
    }

    [MenuItem("DessertPop/⭕ Upgrade Prefab Colliders (Circle)", false, 25)]
    public static void UpgradeColliders()
    {
        DessertAssetLinker.UpgradePrefabColliders();
        EditorUtility.DisplayDialog("Collider Upgrade",
            "모든 디저트 프리팹의 콜라이더가 CircleCollider2D로 교체되었습니다.", "확인");
    }

    [MenuItem("DessertPop/---", false, 30)]
    private static void Separator() { }

    [MenuItem("DessertPop/🏠 Reset Scene Infrastructure", false, 40)]
    public static void ResetSceneOnly()
    {
        DessertPopSetup.AutoSetupScene();
    }

    [MenuItem("DessertPop/🧹 Clear Scene", false, 50)]
    public static void ClearScene()
    {
        DessertPopSetup.ClearSceneOnly();
    }
}
