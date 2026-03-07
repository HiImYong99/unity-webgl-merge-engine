using UnityEngine;
using UnityEditor;

public class AnimalPopIntegratedMenu
{
    [MenuItem("AnimalPop/🚀 1-Step Full Production Setup", false, 0)]
    public static void FullProductionSetup()
    {
        if (!EditorUtility.DisplayDialog("Full Setup",
            "이 작업은 다음 과정을 한 번에 수행합니다:\n" +
            "1. 씬 구조 초기화 (카메라, 용기, 매니저)\n" +
            "2. 동물 스프라이트 배경 제거 및 프리팹 연결\n" +
            "3. 효과음 자동 할당\n" +
            "4. 앱인토스 최적화 빌드 설정 적용\n\n" +
            "계속하시겠습니까?", "예", "취소")) return;

        // 1. Scene Setup
        AnimalPopSetup.AutoSetupSceneNoDialog();

        // 2. Sprite Processing (Background Removal & Centering)
        AnimalBackgroundRemover.RemoveAllAnimalBackgrounds();

        // 3. Asset Link (Sprites + SFX)
        AnimalAssetLinker.LinkIndividualSprites();
        AnimalPopSetup.LinkAudioAssets();

        // 3. WebGL Optimization
        WebGLOptimizer.ApplySettingsManuallyNoDialog();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        EditorUtility.DisplayDialog("Setup Complete",
            "🚀 모든 설정이 완료되었습니다!\n빌드 및 배포 준비가 끝났습니다.", "확인");
    }

    [MenuItem("AnimalPop/🎨 Update Sprites & SFX Only", false, 10)]
    public static void UpdateAssetsOnly()
    {
        AnimalAssetLinker.LinkIndividualSprites();
        AnimalPopSetup.LinkAudioAssets();
    }

    [MenuItem("AnimalPop/🔧 Apply AppsInToss WebGL Settings", false, 20)]
    public static void OptimizeOnly()
    {
        WebGLOptimizer.ApplySettingsManually();
    }

    [MenuItem("AnimalPop/⭕ Upgrade Prefab Colliders (Circle)", false, 25)]
    public static void UpgradeColliders()
    {
        AnimalAssetLinker.UpgradePrefabColliders();
        EditorUtility.DisplayDialog("Collider Upgrade",
            "모든 동물 프리팹의 콜라이더가 CircleCollider2D로 교체되었습니다.", "확인");
    }

    [MenuItem("AnimalPop/---", false, 30)]
    private static void Separator() { }

    [MenuItem("AnimalPop/🏠 Reset Scene Infrastructure", false, 40)]
    public static void ResetSceneOnly()
    {
        AnimalPopSetup.AutoSetupScene();
    }

    [MenuItem("AnimalPop/🧹 Clear Scene", false, 50)]
    public static void ClearScene()
    {
        AnimalPopSetup.ClearSceneOnly();
    }
}
