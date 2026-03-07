using UnityEngine;
using UnityEditor;
using System.IO;

public class DessertAssetLinker : EditorWindow
{
    private const string SPRITE_PATH = "Assets/_Project/Resources/Desserts/";
    private const string PREFAB_PATH = "Assets/_Project/Prefabs/Desserts/";
    private const string DATA_PATH = "Assets/_Project/Data/DessertEvolutionData.asset";

    // [MenuItem("DessertPop/Link Individual Sprites", false, 11)] // Moved to IntegratedMenu
    public static void LinkIndividualSprites()
    {
        DessertEvolutionData data = AssetDatabase.LoadAssetAtPath<DessertEvolutionData>(DATA_PATH);
        if (data == null)
        {
            Debug.LogError("DessertEvolutionData not found at " + DATA_PATH);
            return;
        }

        for (int i = 0; i < data.Levels.Length; i++)
        {
            int levelNum = i + 1;
            string spriteName = "Dessert_" + levelNum;
            string fullSpritePath = SPRITE_PATH + spriteName + ".png";
            
            if (!File.Exists(fullSpritePath)) continue;

            // 1. Importer 설정 - 완전한 원형 PNG (투명 배경 포함)
            TextureImporter importer = AssetImporter.GetAtPath(fullSpritePath) as TextureImporter;
            if (importer != null)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.alphaIsTransparency = true;
                importer.spritePixelsPerUnit = 512f;
                importer.filterMode = FilterMode.Bilinear;
                importer.textureCompression = TextureImporterCompression.CompressedHQ;

                // 피벗을 이미지 정중앙(Custom 0.5,0.5)으로 명시 — alignment:Center(0)는
                // 불투명 픽셀의 bounds.center를 pivot으로 써서 투명 여백이 있으면 어긋남
                TextureImporterSettings settings = new TextureImporterSettings();
                importer.ReadTextureSettings(settings);
                settings.spriteAlignment = (int)SpriteAlignment.Custom;
                settings.spritePivot = new Vector2(0.5f, 0.5f);
                settings.spriteMeshType = SpriteMeshType.FullRect;
                importer.SetTextureSettings(settings);

                importer.SaveAndReimport();
            }

            // 2. 스프라이트 로드 (흰색 배경 제거 불필요 - 원형 PNG는 이미 투명 배경)
            Sprite newSprite = AssetDatabase.LoadAssetAtPath<Sprite>(fullSpritePath);
            if (newSprite == null) continue;

            // 3. Update Prefab
            string prefabName = "Dessert_Lvl_" + levelNum;
            string fullPrefabPath = PREFAB_PATH + prefabName + ".prefab";
            GameObject prefabGo = AssetDatabase.LoadAssetAtPath<GameObject>(fullPrefabPath);
            
            if (prefabGo != null)
            {
                GameObject instance = PrefabUtility.LoadPrefabContents(fullPrefabPath);
                SpriteRenderer sr = instance.GetComponent<SpriteRenderer>();
                if (sr != null)
                {
                    sr.sprite = newSprite;

                    // PolygonCollider2D 제거 — 복잡한 폴리곤 충돌체는 디저트끼리 맞물려서
                    // 겹침/달라붙기 버그의 근본 원인임. CircleCollider2D로 교체.
                    foreach (var oldCol in instance.GetComponents<PolygonCollider2D>())
                        Object.DestroyImmediate(oldCol);
                    foreach (var oldCol in instance.GetComponents<BoxCollider2D>())
                        Object.DestroyImmediate(oldCol);

                    CircleCollider2D cc = instance.GetComponent<CircleCollider2D>();
                    if (cc == null) cc = instance.AddComponent<CircleCollider2D>();
                    cc.radius = 0.5f; // 스케일은 Dessert.Initialize()에서 설정
                }
                PrefabUtility.SaveAsPrefabAsset(instance, fullPrefabPath);
                PrefabUtility.UnloadPrefabContents(instance);
                
                data.Levels[i].Prefab = prefabGo;
            }
        }

        EditorUtility.SetDirty(data);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        
        EditorUtility.DisplayDialog("Success", "Sprites have been processed (White BG removed) and linked!\nColliders upgraded to CircleCollider2D.", "OK");
    }

    /// <summary>
    /// 기존 프리팹의 PolygonCollider2D를 CircleCollider2D로 일괄 교체합니다.
    /// 프리팹을 다시 임포트하지 않고 콜라이더만 교체할 때 사용하세요.
    /// </summary>
    public static void UpgradePrefabColliders()
    {
        string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { PREFAB_PATH });
        int upgraded = 0;

        foreach (string guid in prefabGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject instance = PrefabUtility.LoadPrefabContents(path);

            bool changed = false;

            // PolygonCollider2D 모두 제거
            foreach (var poly in instance.GetComponents<PolygonCollider2D>())
            {
                Object.DestroyImmediate(poly);
                changed = true;
            }
            // BoxCollider2D 모두 제거
            foreach (var box in instance.GetComponents<BoxCollider2D>())
            {
                Object.DestroyImmediate(box);
                changed = true;
            }

            // CircleCollider2D 보장
            CircleCollider2D cc = instance.GetComponent<CircleCollider2D>();
            if (cc == null)
            {
                cc = instance.AddComponent<CircleCollider2D>();
                cc.radius = 0.5f;
                changed = true;
            }

            if (changed)
            {
                PrefabUtility.SaveAsPrefabAsset(instance, path);
                upgraded++;
            }
            PrefabUtility.UnloadPrefabContents(instance);
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"[DessertAssetLinker] CircleCollider2D 업그레이드 완료: {upgraded}개 프리팹 수정됨");
    }
}
