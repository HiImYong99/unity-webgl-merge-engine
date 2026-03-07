using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using System.Collections.Generic;

/// <summary>
/// 애니멀 팝 프로젝트 초기 세팅 및 씬 자동 구성을 위한 에디터 유틸리티
/// </summary>
public class AnimalPopSetup : EditorWindow
{
    private static readonly string[] SCENE_ROOT_OBJECT_NAMES = new[]
    {
        "GameMgr", "SpawnMgr", "PhysicsMgr", "SoundMgr", "VFXMgr", "RemoteAssetMgr",
        "BridgeMgr", "TossBridgeMgr", "ResultCardMgr",
        "Canvas", "EventSystem", "UIMgr",
        "MainCamera", "Directional Light",
        "GameContainer", "Background"
    };

    [MenuItem("AnimalPop/Auto Setup Scene", false, 1)]
    public static void AutoSetupScene()
    {
        if (EditorUtility.DisplayDialog(
            "Auto Setup Scene",
            "기존 씬의 모든 오브젝트를 삭제하고\n애니멀 팝 프로젝트에 맞게 재구성합니다.\n(AGENTS.md 가이드라인 반영)",
            "예 - 초기화 후 재설정",
            "취소"))
        {
            AutoSetupSceneNoDialog();
            EditorUtility.DisplayDialog("✅ 완료!", "AGENTS.md 규칙이 적용된 씬 재구성이 완료되었습니다!", "확인");
        }
    }

    public static void AutoSetupSceneNoDialog()
    {
        EnsureTags();
        if (!AssetDatabase.IsValidFolder("Assets/_Project/Data"))
            AssetDatabase.CreateFolder("Assets/_Project", "Data");

        ClearScene();
        CreateCamera();
        CreateBackground();
        CreateGameContainer();
        CreateCoreManagers();
        CreateUI();
        CreateDefaultData();

        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene());
    }

    private static void EnsureTags()
    {
        EnsureTagExists("Animal");
        EnsureTagExists("Wall");
        EnsureTagExists("DeadLine");
    }

    private static void EnsureTagExists(string tagName)
    {
        var tagManager = new SerializedObject(
            AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
        SerializedProperty tagsProp = tagManager.FindProperty("tags");

        for (int i = 0; i < tagsProp.arraySize; i++)
        {
            if (tagsProp.GetArrayElementAtIndex(i).stringValue == tagName) return;
        }

        tagsProp.arraySize++;
        tagsProp.GetArrayElementAtIndex(tagsProp.arraySize - 1).stringValue = tagName;
        tagManager.ApplyModifiedProperties();
    }

    public static void ClearSceneOnly()
    {
        var rootObjects = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects();
        foreach (var go in rootObjects) Object.DestroyImmediate(go);
    }

    private static void ClearScene()
    {
        ClearSceneOnly();
    }

    private static void CreateCamera()
    {
        GameObject camGo = new GameObject("MainCamera");
        camGo.tag = "MainCamera";
        Camera cam = camGo.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.14f, 0.11f, 0.22f);
        cam.orthographic = true;
        cam.orthographicSize = 5f;
        camGo.transform.position = new Vector3(0, 1.0f, -10f);
        camGo.AddComponent<AudioListener>();

        camGo.AddComponent<CameraScaler>();
        camGo.AddComponent<CameraMgr>();
    }

    private static void CreateBackground()
    {
        GameObject bg = new GameObject("Background");
        bg.transform.position = new Vector3(0, 0, 5f);
        SpriteRenderer sr = bg.AddComponent<SpriteRenderer>();
        sr.sortingOrder = -100;

        int res = 128;
        Texture2D tex = new Texture2D(1, res, TextureFormat.RGBA32, false);
        Color top = new Color(1.0f, 0.98f, 0.94f);
        Color bottom = new Color(0.98f, 0.85f, 0.82f);

        for (int y = 0; y < res; y++)
            tex.SetPixel(0, y, Color.Lerp(bottom, top, (float)y / res));
        tex.Apply();

        sr.sprite = Sprite.Create(tex, new Rect(0, 0, 1, res), new Vector2(0.5f, 0.5f), 1f);
        bg.transform.localScale = new Vector3(30f, 30f, 1f);
    }

    private static void CreateGameContainer()
    {
        GameObject container = new GameObject("GameContainer");
        container.transform.position = Vector3.zero;
        container.AddComponent<VesselVisualEnhancer>();

        float w = 3.2f, h = 5.0f, t = 0.8f, bY = -1.5f;
        CreateWall(container.transform, "Floor", new Vector3(0, bY - t/2, 0), new Vector2(w + t*2, t));
        CreateWall(container.transform, "LeftWall", new Vector3(-w/2 - t/2, bY + h/2, 0), new Vector2(t, h));
        CreateWall(container.transform, "RightWall", new Vector3(w/2 + t/2, bY + h/2, 0), new Vector2(t, h));
    }

    private static void CreateWall(Transform parent, string name, Vector3 pos, Vector2 size)
    {
        GameObject wall = new GameObject(name);
        wall.transform.SetParent(parent);
        wall.transform.position = pos;
        wall.tag = "Wall";
        wall.AddComponent<BoxCollider2D>().size = size;
        wall.AddComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Static;
    }

    private static void CreateCoreManagers()
    {
        AnimalEvolutionData data = AssetDatabase.LoadAssetAtPath<AnimalEvolutionData>("Assets/_Project/Data/AnimalEvolutionData.asset");

        GameObject goGame = new GameObject("GameManager");
        GameMgr gm = goGame.AddComponent<GameMgr>();
        gm.EvolutionData = data;

        GameObject goSpawn = new GameObject("SpawnManager");
        SpawnMgr sm = goSpawn.AddComponent<SpawnMgr>();
        GameObject sp = new GameObject("SpawnPoint");
        sp.transform.position = new Vector3(0, 7.0f, 0);
        sp.transform.SetParent(goSpawn.transform);
        sm.SpawnPoint = sp.transform;

        new GameObject("PhysicsMgr").AddComponent<PhysicsMgr>();
        new GameObject("VFXMgr").AddComponent<VFXMgr>();
        new GameObject("RemoteAssetMgr").AddComponent<RemoteAssetMgr>();

        GameObject goSound = new GameObject("SoundMgr");
        SoundMgr snd = goSound.AddComponent<SoundMgr>();
        snd.BGMSource = new GameObject("BGMSource", typeof(AudioSource)).GetComponent<AudioSource>();
        snd.BGMSource.transform.SetParent(goSound.transform);
        snd.SFXSource = new GameObject("SFXSource", typeof(AudioSource)).GetComponent<AudioSource>();
        snd.SFXSource.transform.SetParent(goSound.transform);

        new GameObject("TossBridgeManager").AddComponent<TossBridgeMgr>();
        new GameObject("BridgeManager").AddComponent<BridgeMgr>();
        new GameObject("ResultCard").AddComponent<ResultCardMgr>();
    }

    private static void CreateUI()
    {
        GameObject canvasGo = new GameObject("Canvas");
        Canvas c = canvasGo.AddComponent<Canvas>();
        c.renderMode = RenderMode.ScreenSpaceOverlay;
        canvasGo.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        canvasGo.AddComponent<GraphicRaycaster>();
        canvasGo.AddComponent<TossSafeArea>();

        new GameObject("UIMgr").AddComponent<UIMgr>();
    }

    public static void LinkAudioAssets()
    {
        SoundMgr snd = FindObjectOfType<SoundMgr>();
        if (snd == null) return;

        if (snd.DropClip == null) snd.DropClip = Resources.Load<AudioClip>("Audio/SFX/Drop");
        if (snd.BgmClip == null) snd.BgmClip = Resources.Load<AudioClip>("Audio/BGM/MainBGM");

        Debug.Log("[AnimalPopSetup] Audio assets linked (if found in Resources).");
    }

    private static void CreateDefaultData()
    {
        AnimalEvolutionData data = AssetDatabase.LoadAssetAtPath<AnimalEvolutionData>("Assets/_Project/Data/AnimalEvolutionData.asset");
        if (data == null)
        {
            data = ScriptableObject.CreateInstance<AnimalEvolutionData>();
            AssetDatabase.CreateAsset(data, "Assets/_Project/Data/AnimalEvolutionData.asset");
        }
    }
}
