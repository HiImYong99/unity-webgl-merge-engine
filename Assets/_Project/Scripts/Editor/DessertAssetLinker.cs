using UnityEngine;
using UnityEditor;
using System.IO;

public class DessertAssetLinker : EditorWindow
{
    private const string SPRITE_PATH = "Assets/_Project/Sprites/Desserts/";
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

            // 1. Prepare Importer for Processing
            TextureImporter importer = AssetImporter.GetAtPath(fullSpritePath) as TextureImporter;
            if (importer != null)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.alphaIsTransparency = true;
                importer.isReadable = true;
                importer.spritePixelsPerUnit = 200f; // 사이즈 전체적으로 축소 (100 -> 200)
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.SaveAndReimport();
            }

            // 2. Load and Remove White Background
            byte[] bytes = File.ReadAllBytes(fullSpritePath);
            Texture2D tex = new Texture2D(2, 2);
            tex.LoadImage(bytes);
            
            Color[] colors = tex.GetPixels();
            bool modified = false;
            for (int j = 0; j < colors.Length; j++)
            {
                // If it's pure white or very close to it, make it transparent
                if (colors[j].r > 0.97f && colors[j].g > 0.97f && colors[j].b > 0.97f)
                {
                    colors[j] = new Color(0, 0, 0, 0);
                    modified = true;
                }
            }

            if (modified)
            {
                tex.SetPixels(colors);
                tex.Apply();
                File.WriteAllBytes(fullSpritePath, tex.EncodeToPNG());
                AssetDatabase.ImportAsset(fullSpritePath, ImportAssetOptions.ForceUpdate);
            }

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
