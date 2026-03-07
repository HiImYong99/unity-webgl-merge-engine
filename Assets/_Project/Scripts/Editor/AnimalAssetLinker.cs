using UnityEngine;
using UnityEditor;
using System.IO;

public class AnimalAssetLinker : EditorWindow
{
    private const string SPRITE_PATH = "Assets/_Project/Resources/Animals/";
    private const string PREFAB_PATH = "Assets/_Project/Prefabs/Animals/";
    private const string DATA_PATH = "Assets/_Project/Data/AnimalEvolutionData.asset";

    // [MenuItem("AnimalPop/Link Individual Sprites", false, 11)] // Moved to IntegratedMenu
    public static void LinkIndividualSprites()
    {
        AnimalEvolutionData data = AssetDatabase.LoadAssetAtPath<AnimalEvolutionData>(DATA_PATH);
        if (data == null)
        {
            Debug.LogError("AnimalEvolutionData not found at " + DATA_PATH);
            return;
        }

        for (int i = 0; i < data.Levels.Length; i++)
        {
            int levelNum = i + 1;
            string spriteName = "Animal_" + levelNum;
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
                importer.isReadable = true; // 런타임 GetPixels 허용
                importer.maxTextureSize = 4096; // 원본 해상도 보존 (다운스케일 방지)
                importer.mipmapEnabled = false; // 2D 스프라이트에 불필요

                TextureImporterSettings settings = new TextureImporterSettings();
                importer.ReadTextureSettings(settings);
                settings.spriteAlignment = (int)SpriteAlignment.Custom;
                settings.spritePivot = new Vector2(0.5f, 0.5f);
                settings.spriteMeshType = SpriteMeshType.FullRect;
                importer.SetTextureSettings(settings);

                importer.SaveAndReimport();
            }

            // 2. 스프라이트 로드
            Sprite newSprite = AssetDatabase.LoadAssetAtPath<Sprite>(fullSpritePath);
            if (newSprite == null) continue;

            // 3. Update Prefab
            string prefabName = "Animal_Lvl_" + levelNum;
            string fullPrefabPath = PREFAB_PATH + prefabName + ".prefab";
            GameObject prefabGo = AssetDatabase.LoadAssetAtPath<GameObject>(fullPrefabPath);

            if (prefabGo != null)
            {
                GameObject instance = PrefabUtility.LoadPrefabContents(fullPrefabPath);
                SpriteRenderer sr = instance.GetComponent<SpriteRenderer>();
                if (sr != null)
                {
                    sr.sprite = newSprite;

                    foreach (var oldCol in instance.GetComponents<PolygonCollider2D>())
                        Object.DestroyImmediate(oldCol);
                    foreach (var oldCol in instance.GetComponents<BoxCollider2D>())
                        Object.DestroyImmediate(oldCol);

                    CircleCollider2D cc = instance.GetComponent<CircleCollider2D>();
                    if (cc == null) cc = instance.AddComponent<CircleCollider2D>();
                    cc.radius = 0.5f;
                }
                PrefabUtility.SaveAsPrefabAsset(instance, fullPrefabPath);
                PrefabUtility.UnloadPrefabContents(instance);

                data.Levels[i].Prefab = prefabGo;
            }
        }

        EditorUtility.SetDirty(data);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        EditorUtility.DisplayDialog("Success", "Sprites have been processed and linked!\nColliders upgraded to CircleCollider2D.", "OK");
    }

    /// <summary>
    /// 기존 프리팹의 PolygonCollider2D를 CircleCollider2D로 일괄 교체합니다.
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

            foreach (var poly in instance.GetComponents<PolygonCollider2D>())
            {
                Object.DestroyImmediate(poly);
                changed = true;
            }
            foreach (var box in instance.GetComponents<BoxCollider2D>())
            {
                Object.DestroyImmediate(box);
                changed = true;
            }

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
        Debug.Log($"[AnimalAssetLinker] CircleCollider2D 업그레이드 완료: {upgraded}개 프리팹 수정됨");
    }
}
