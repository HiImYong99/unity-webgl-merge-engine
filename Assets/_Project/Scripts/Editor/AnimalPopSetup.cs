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
        "BridgeMgr", "ResultCardMgr",
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

    // ================================================================
    //  Android 출시용 씬 구성
    // ================================================================

    [MenuItem("AnimalPop/📱 Setup Android Scene", false, 2)]
    public static void SetupAndroidScene()
    {
        if (EditorUtility.DisplayDialog(
            "📱 Android Scene Setup",
            "기존 씬을 Android 출시용으로 재구성합니다.\n" +
            "• LandingPanel / HUDPanel / GameOverPanel 생성\n" +
            "• 모든 버튼 자동 연결\n" +
            "• Safe Area (노치) 지원\n\n계속하시겠습니까?",
            "예 - Android로 구성",
            "취소"))
        {
            SetupAndroidSceneNoDialog();
            EditorUtility.DisplayDialog("✅ 완료!", "Android 씬 구성이 완료되었습니다!\nUnity에서 Android 빌드하세요.", "확인");
        }
    }

    public static void SetupAndroidSceneNoDialog()
    {
        EnsureTags();
        if (!AssetDatabase.IsValidFolder("Assets/_Project/Data"))
            AssetDatabase.CreateFolder("Assets/_Project", "Data");

        ClearScene();
        CreateCamera();
        CreateBackground();
        CreateGameContainer();
        CreateCoreManagers();
        CreateAndroidUI();
        CreateDefaultData();

        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene());
    }

    private static void CreateAndroidUI()
    {
        // EventSystem
        GameObject esGo = new GameObject("EventSystem");
        esGo.AddComponent<UnityEngine.EventSystems.EventSystem>();
        esGo.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();

        // Canvas (1080×1920 기준)
        GameObject canvasGo = new GameObject("Canvas");
        Canvas canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10;
        CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080, 1920);
        scaler.matchWidthOrHeight = 0.5f;
        canvasGo.AddComponent<GraphicRaycaster>();

        // SafeArea 컨테이너 (노치 대응)
        GameObject safeAreaGo = new GameObject("SafeArea");
        safeAreaGo.transform.SetParent(canvasGo.transform, false);
        RectTransform safeAreaRT = safeAreaGo.AddComponent<RectTransform>();
        safeAreaRT.anchorMin = Vector2.zero;
        safeAreaRT.anchorMax = Vector2.one;
        safeAreaRT.offsetMin = safeAreaRT.offsetMax = Vector2.zero;
        safeAreaGo.AddComponent<SafeArea>();
        Transform uiRoot = safeAreaGo.transform;

        // UIMgr
        GameObject uiMgrGo = new GameObject("UIMgr");
        UIMgr uiMgr = uiMgrGo.AddComponent<UIMgr>();

        // ── Landing Panel ──────────────────────────────────────────────
        GameObject landingPanel = MakeFullscreenPanel(uiRoot, "LandingPanel", new Color(0.06f, 0.04f, 0.14f, 0.97f));

        Text titleText = MakeText(landingPanel.transform, "TitleText", "🐾 애니멀 팝",
            80, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter);
        SetAnchored(titleText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0, 350), new Vector2(960, 110));

        Text subText = MakeText(landingPanel.transform, "SubtitleText", "같은 동물을 합쳐 더 크게 키워요!",
            34, FontStyle.Normal, new Color(0.78f, 0.78f, 1f), TextAnchor.MiddleCenter);
        SetAnchored(subText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0, 255), new Vector2(880, 52));

        GameObject divider = MakeImage(landingPanel.transform, "Divider", new Color(1f, 1f, 1f, 0.15f));
        SetAnchored(divider.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0, 190), new Vector2(700, 2));

        Text bestLabel = MakeText(landingPanel.transform, "BestLabel", "최고 기록",
            30, FontStyle.Normal, new Color(0.65f, 0.65f, 0.9f), TextAnchor.MiddleCenter);
        SetAnchored(bestLabel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0, 120), new Vector2(500, 44));

        Text landingHSText = MakeText(landingPanel.transform, "LandingHighScoreText", "0",
            68, FontStyle.Bold, new Color(1f, 0.87f, 0.3f), TextAnchor.MiddleCenter);
        SetAnchored(landingHSText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0, 25), new Vector2(700, 90));

        Button startBtn = MakeButton(landingPanel.transform, "StartButton",
            "시작하기  ▶", new Color(0.19f, 0.51f, 0.96f), Color.white, 48);
        SetAnchored(startBtn.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0, -130), new Vector2(680, 110));

        // ── HUD Panel ──────────────────────────────────────────────────
        GameObject hudPanel = new GameObject("HUDPanel");
        hudPanel.transform.SetParent(uiRoot, false);
        RectTransform hudRT = hudPanel.AddComponent<RectTransform>();
        hudRT.anchorMin = Vector2.zero;
        hudRT.anchorMax = Vector2.one;
        hudRT.offsetMin = hudRT.offsetMax = Vector2.zero;
        hudPanel.SetActive(false);

        // 상단 배경바
        GameObject hudBar = MakeImage(hudPanel.transform, "HUDBar", new Color(1f, 0.97f, 0.93f, 0.88f));
        SetAnchored(hudBar.GetComponent<RectTransform>(), new Vector2(0f, 1f), new Vector2(1f, 1f),
            new Vector2(0, -70), new Vector2(0, 140));

        // 현재 점수
        Text scoreText = MakeText(hudPanel.transform, "Text_Score", "0",
            64, FontStyle.Bold, new Color(0.18f, 0.1f, 0.08f), TextAnchor.MiddleCenter);
        SetAnchored(scoreText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0, -52), new Vector2(500, 80));

        // 최고 기록 (소형)
        Text hudBestText = MakeText(hudPanel.transform, "HUDBestLabel", "최고: 0",
            26, FontStyle.Normal, new Color(0.45f, 0.3f, 0.25f), TextAnchor.MiddleCenter);
        SetAnchored(hudBestText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0, -110), new Vector2(380, 36));

        // 다음 동물 (우측)
        Text nextGuideText = MakeText(hudPanel.transform, "NextGuideText", "다음: 🐥",
            30, FontStyle.Normal, new Color(0.3f, 0.18f, 0.45f), TextAnchor.MiddleRight);
        SetAnchored(nextGuideText.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f),
            new Vector2(-24, -60), new Vector2(260, 50));

        // 사운드 토글 버튼 (좌측)
        Button soundBtn = MakeButton(hudPanel.transform, "SoundButton",
            "🔊", new Color(0.9f, 0.88f, 1f, 0.75f), new Color(0.25f, 0.15f, 0.4f), 36);
        SetAnchored(soundBtn.GetComponent<RectTransform>(), new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(60, -60), new Vector2(80, 80));
        Text soundBtnText = soundBtn.GetComponentInChildren<Text>();

        // ── GameOver Panel ─────────────────────────────────────────────
        GameObject gameOverPanel = MakeFullscreenPanel(uiRoot, "GameOverPanel", new Color(0f, 0f, 0f, 0.72f));
        gameOverPanel.SetActive(false);

        // 카드 배경
        GameObject card = MakeImage(gameOverPanel.transform, "Card", new Color(0.09f, 0.06f, 0.18f, 1f));
        SetAnchored(card.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0, 40), new Vector2(860, 960));
        Transform cardT = card.transform;

        Text goTitle = MakeText(cardT, "GameOverTitle", "게임 오버 🎮",
            52, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter);
        SetAnchored(goTitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0, -80), new Vector2(800, 70));

        Text sessionLabel = MakeText(cardT, "SessionScoreLabel", "이번 점수",
            28, FontStyle.Normal, new Color(0.68f, 0.68f, 0.95f), TextAnchor.MiddleCenter);
        SetAnchored(sessionLabel.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0, -170), new Vector2(700, 40));

        // 세션 점수 (GameOverScoreText)
        Text sessionScoreText = MakeText(cardT, "SessionScoreText", "0",
            80, FontStyle.Bold, new Color(1f, 0.87f, 0.3f), TextAnchor.MiddleCenter);
        SetAnchored(sessionScoreText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0, -280), new Vector2(700, 100));

        Text bestRecordLabel = MakeText(cardT, "BestRecordLabel", "최고 기록",
            26, FontStyle.Normal, new Color(0.68f, 0.68f, 0.95f), TextAnchor.MiddleCenter);
        SetAnchored(bestRecordLabel.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0, -390), new Vector2(700, 40));

        // 최고 점수 (HighScoreText)
        Text highScoreText = MakeText(cardT, "HighScoreText", "0",
            48, FontStyle.Bold, new Color(0.95f, 0.7f, 0.2f), TextAnchor.MiddleCenter);
        SetAnchored(highScoreText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0, -460), new Vector2(700, 65));

        // 다시하기
        Button restartBtn = MakeButton(cardT, "RestartButton",
            "🔄  다시하기", new Color(0.19f, 0.51f, 0.96f), Color.white, 38);
        SetAnchored(restartBtn.GetComponent<RectTransform>(), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0, -700), new Vector2(760, 95));

        // 공유
        Button shareBtn = MakeButton(cardT, "ShareButton",
            "📤  결과 공유", new Color(0.22f, 0.62f, 0.36f), Color.white, 32);
        SetAnchored(shareBtn.GetComponent<RectTransform>(), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0, -815), new Vector2(760, 78));

        // ── UIMgr 레퍼런스 연결 ────────────────────────────────────────
        uiMgr.LandingPanel = landingPanel;
        uiMgr.HUDPanel = hudPanel;
        uiMgr.GameOverPanel = gameOverPanel;
        uiMgr.LandingHighScoreText = landingHSText;
        uiMgr.ScoreText = scoreText;
        uiMgr.HighScoreText = highScoreText;
        uiMgr.GameOverScoreText = sessionScoreText;
        uiMgr.NextGuideText = nextGuideText;
        uiMgr.ApplicationStartButton = startBtn;
        uiMgr.RestartButton = restartBtn;
        uiMgr.ShareButton = shareBtn;
        uiMgr.SoundToggleButton = soundBtn;
        uiMgr.SoundToggleText = soundBtnText;
    }

    // ── UI 헬퍼 ─────────────────────────────────────────────────────

    private static GameObject MakeFullscreenPanel(Transform parent, string name, Color bgColor)
    {
        GameObject go = MakeImage(parent, name, bgColor);
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        return go;
    }

    private static GameObject MakeImage(Transform parent, string name, Color color)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.AddComponent<Image>().color = color;
        return go;
    }

    private static Text MakeText(Transform parent, string name, string content,
        int fontSize, FontStyle style, Color color, TextAnchor anchor)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        Text t = go.AddComponent<Text>();
        t.text = content;
        t.fontSize = fontSize;
        t.fontStyle = style;
        t.color = color;
        t.alignment = anchor;
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        Font builtinFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (builtinFont != null) t.font = builtinFont;
        return t;
    }

    private static Button MakeButton(Transform parent, string name,
        string labelText, Color bgColor, Color textColor, int fontSize)
    {
        GameObject go = MakeImage(parent, name, bgColor);
        Button btn = go.AddComponent<Button>();
        Text label = MakeText(go.transform, "Label", labelText,
            fontSize, FontStyle.Bold, textColor, TextAnchor.MiddleCenter);
        SetAnchored(label.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        return btn;
    }

    private static void SetAnchored(RectTransform rt,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPos, Vector2 sizeDelta)
    {
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = sizeDelta;
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
