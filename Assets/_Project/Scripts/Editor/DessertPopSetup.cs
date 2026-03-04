using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using System.Collections.Generic;

public class DessertPopSetup : EditorWindow
{
    // ─── 클리어 대상 오브젝트 이름 목록 ────────────────────────────
    private static readonly string[] SceneRootObjectNames = new[]
    {
        "GameManager", "SpawnManager", "PhysicsManager", "AudioManager",
        "BridgeManager", "TossBridgeManager", "ResultCard",
        "Canvas", "EventSystem", "UIManager",
        "MainCamera", "Directional Light",
        "GameContainer", "Background"
    };

    public static void AutoSetupScene()
    {
        if (EditorUtility.DisplayDialog(
            "Auto Setup Scene",
            "기존 씬의 모든 오브젝트를 삭제하고\n디저트 팝 MVP 씬을 새로 구성합니다.\n\n계속하시겠습니까?",
            "예 - 초기화 후 재설정",
            "취소"))
        {
            AutoSetupSceneNoDialog();
            
            EditorUtility.DisplayDialog(
                "✅ 완료!",
                "씬 재구성이 완료되었습니다!",
                "확인");
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
        LinkAudioAssets();

        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene());
    }

    // [MenuItem("DessertPop/Clear Scene Only", false, 2)] // Moved to IntegratedMenu
    public static void ClearSceneOnly()
    {
        if (EditorUtility.DisplayDialog("씬 클리어", "씬의 모든 오브젝트를 삭제합니다.", "삭제", "취소"))
            ClearScene();
    }

    // ================================================================
    //  태그 등록
    // ================================================================
    private static void EnsureTags()
    {
        EnsureTagExists("Dessert");
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
            if (tagsProp.GetArrayElementAtIndex(i).stringValue == tagName)
                return;
        }

        tagsProp.arraySize++;
        tagsProp.GetArrayElementAtIndex(tagsProp.arraySize - 1).stringValue = tagName;
        tagManager.ApplyModifiedProperties();
        Debug.Log($"[DessertPopSetup] ✅ 태그 등록 완료: \"{tagName}\"");
    }

    private static void ClearScene()
    {
        var rootObjects = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects();
        foreach (var go in rootObjects)
        {
            Object.DestroyImmediate(go);
        }
        Debug.Log("[DessertPopSetup] 씬 클리어 완료");
    }

    // ================================================================
    //  1. 메인 카메라
    // ================================================================
    private static void CreateCamera()
    {
        GameObject mainCamGo = new GameObject("MainCamera");
        mainCamGo.tag = "MainCamera";
        Camera mainCam = mainCamGo.AddComponent<Camera>();
        mainCam.clearFlags = CameraClearFlags.SolidColor;
        mainCam.backgroundColor = new Color(0.14f, 0.11f, 0.22f); // 딥 퍼플 (배경 밖 색)
        mainCam.orthographic = true;
        mainCam.orthographicSize = 5f; // 런타임에 CameraScaler가 덮어씀
        mainCamGo.transform.position = new Vector3(0, 1.0f, -10f);
        mainCamGo.AddComponent<AudioListener>();

        CameraScaler cs = mainCamGo.GetComponent<CameraScaler>();
        if (cs == null) cs = mainCamGo.AddComponent<CameraScaler>();
        cs.ContainerWorldWidth = 3.2f;
        cs.MinOrthoSize = 5.0f;
        cs.MaxOrthoSize = 8.0f;
        
        Debug.Log("[DessertPopSetup] ✅ 카메라 스케일러 설정 완료");
    }

    // ================================================================
    //  2. 배경 (그라디언트)
    // ================================================================
    private static void CreateBackground()
    {
        GameObject bg = new GameObject("Background");
        bg.transform.position = new Vector3(0, 0, 5f); // 카메라 뒤

        // 배경 스프라이트 (그라디언트 효과를 위한 큰 사각형)
        SpriteRenderer sr = bg.AddComponent<SpriteRenderer>();
        sr.sortingOrder = -100;

        // 128x128 그라디언트 텍스처 생성 (상단: 따뜻한 크림, 하단: 소프트 핑크)
        int res = 128;
        Texture2D tex = new Texture2D(1, res, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;

        Color topColor = new Color(1.0f, 0.98f, 0.94f);    // 아주 연한 크림 (상단)
        Color midColor = new Color(0.99f, 0.91f, 0.85f);   // 연한 복숭아 (중간 #fce8d8 비슷)
        Color bottomColor = new Color(0.98f, 0.85f, 0.82f); // 연한 핑크 (하단)

        for (int y = 0; y < res; y++)
        {
            float t = (float)y / (res - 1);
            Color c;
            if (t < 0.5f)
                c = Color.Lerp(bottomColor, midColor, t * 2f);
            else
                c = Color.Lerp(midColor, topColor, (t - 0.5f) * 2f);
            tex.SetPixel(0, y, c);
        }
        tex.Apply();

        sr.sprite = Sprite.Create(tex, new Rect(0, 0, 1, res), new Vector2(0.5f, 0.5f), 1f);
        bg.transform.localScale = new Vector3(30f, 30f, 1f);

        // (배경 도트 패턴 로직 제거됨)
    }

    // ================================================================
    //  3. 게임 컨테이너 (용기, 벽, 바닥, 데드라인)
    // ================================================================
    private static void CreateGameContainer()
    {
        // ─── 컨테이너 치수 (모바일 최적 콤팩트 규격) ───
        float containerWidth = 3.2f;
        float containerHeight = 5.0f;
        float wallThickness = 0.8f;
        float containerBottomY = -1.5f;

        float halfWidth = containerWidth / 2f;

        GameObject container = new GameObject("GameContainer");
        container.transform.position = Vector3.zero;

        // ─── 바닥 (Floor) ───
        GameObject floor = CreateWall(
            container.transform, "Floor",
            new Vector3(0, containerBottomY - wallThickness / 2f, 0),
            new Vector2(containerWidth + wallThickness * 2, wallThickness),
            new Color(0.9f, 0.91f, 0.93f) // Toss Light Grey
        );

        // ─── 왼쪽 벽 ───
        GameObject leftWall = CreateWall(
            container.transform, "LeftWall",
            new Vector3(-halfWidth - wallThickness / 2f, containerBottomY + containerHeight / 2f, 0),
            new Vector2(wallThickness, containerHeight),
            new Color(0.9f, 0.91f, 0.93f)
        );

        // ─── 오른쪽 벽 ───
        GameObject rightWall = CreateWall(
            container.transform, "RightWall",
            new Vector3(halfWidth + wallThickness / 2f, containerBottomY + containerHeight / 2f, 0),
            new Vector2(wallThickness, containerHeight),
            new Color(0.9f, 0.91f, 0.93f)
        );

        // ─── 테이블 베이스 (사진과 동일한 브라운 보더 반영) ───
        GameObject baseTable = new GameObject("TableBase");
        baseTable.transform.SetParent(container.transform);
        baseTable.transform.position = new Vector3(0, containerBottomY + containerHeight / 2f, 1f);
        
        SpriteRenderer btsr = baseTable.AddComponent<SpriteRenderer>();
        btsr.sortingOrder = -5;
        Texture2D btt = new Texture2D(1, 1);
        btt.SetPixel(0, 0, Color.white);
        btt.Apply();
        btsr.sprite = Sprite.Create(btt, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
        btsr.color = new Color(0.99f, 0.97f, 0.95f, 0.95f); 
        
        // (Lattice 및 UIFooter 생성 로직 제거됨 - 깔끔한 배경을 위함)

        // ─── 용기 내부 배경 (유리 느낌) ───
        GameObject containerBg = new GameObject("ContainerBackground");
        containerBg.transform.SetParent(container.transform);
        containerBg.transform.position = new Vector3(0, containerBottomY + containerHeight / 2f, 1f);
        SpriteRenderer bgSr = containerBg.AddComponent<SpriteRenderer>();
        bgSr.sortingOrder = -10;

        // 반투명 밝은 배경
        Texture2D bgTex = new Texture2D(1, 1);
        bgTex.SetPixel(0, 0, Color.white);
        bgTex.Apply();
        bgSr.sprite = Sprite.Create(bgTex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
        bgSr.color = new Color(1f, 1f, 1f, 0.12f); 
        containerBg.transform.localScale = new Vector3(containerWidth, containerHeight, 1f);
        
        // 유리 효과에 부드러운 하이라이트 추가
        GameObject shine = new GameObject("GlassShine");
        shine.transform.SetParent(containerBg.transform);
        shine.transform.localPosition = new Vector3(0, 0, -0.05f);
        shine.transform.localScale = Vector3.one;
        SpriteRenderer shineSr = shine.AddComponent<SpriteRenderer>();
        shineSr.sprite = bgSr.sprite;
        shineSr.color = new Color(1, 1, 1, 0.05f); // 아주 살짝 반짝임
        
        Debug.Log("[DessertPopSetup] ✅ 게임 컨테이너 및 틀 구현 완료");
    }

    private static GameObject CreateWall(Transform parent, string name, Vector3 position, Vector2 size, Color color)
    {
        GameObject wall = new GameObject(name);
        wall.transform.SetParent(parent);
        wall.transform.position = position;
        try { wall.tag = "Wall"; } catch { }

        // --- Visual (실제 보이는 부분: 얇고 세련되게) ---
        GameObject visual = new GameObject("Visual");
        visual.transform.SetParent(wall.transform);
        visual.transform.localPosition = Vector3.zero;
        
        // 벽이면 가로를 얇게(0.12), 바닥이면 세로를 얇게(0.12)
        float visX = (size.x < size.y) ? 0.12f : size.x;
        float visY = (size.x < size.y) ? size.y : 0.12f;
        visual.transform.localScale = new Vector3(visX, visY, 1f);

        SpriteRenderer sr = visual.AddComponent<SpriteRenderer>();
        sr.sortingOrder = 50; 
        Texture2D tex = new Texture2D(1, 1);
        tex.SetPixel(0, 0, Color.white);
        tex.Apply();
        sr.sprite = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
        sr.color = color;

        // --- Physics (보이지 않지만 두꺼운 충돌체) ---
        BoxCollider2D col = wall.AddComponent<BoxCollider2D>();
        col.size = size; // 전달받은 두꺼운 사이즈 (1.0f 등)

        Rigidbody2D rb = wall.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Static;

        return wall;
    }

    private static void CreateCornerDecor(Transform parent, Vector3 position, string name)
    {
        GameObject corner = new GameObject(name);
        corner.transform.SetParent(parent);
        corner.transform.position = position;

        SpriteRenderer sr = corner.AddComponent<SpriteRenderer>();
        sr.sortingOrder = 2;

        // 원형 장식
        int res = 32;
        Texture2D tex = new Texture2D(res, res, TextureFormat.RGBA32, false);
        float center = res / 2f, r = center - 1f;
        for (int y = 0; y < res; y++)
        for (int x = 0; x < res; x++)
        {
            float dx = x - center, dy = y - center;
            float dist = Mathf.Sqrt(dx * dx + dy * dy);
            float a = Mathf.Clamp01(1f - Mathf.Max(0, dist - r));
            tex.SetPixel(x, y, new Color(1, 1, 1, a));
        }
        tex.Apply();
        sr.sprite = Sprite.Create(tex, new Rect(0, 0, res, res), new Vector2(0.5f, 0.5f), res);
        sr.color = new Color(0.50f, 0.35f, 0.25f);
        corner.transform.localScale = Vector3.one * 0.3f;
    }

    // ================================================================
    //  4. 코어 매니저
    // ================================================================
    private static void CreateCoreManagers()
    {
        // GameManager
        GameObject goGame = new GameObject("GameManager");
        GameManager gm = goGame.AddComponent<GameManager>();

        // SpawnManager
        GameObject goSpawn = new GameObject("SpawnManager");
        SpawnManager sm = goSpawn.AddComponent<SpawnManager>();
        GameObject spawnPoint = new GameObject("SpawnPoint");
        spawnPoint.transform.position = new Vector3(0, 5.5f, 0); // 컨테이너 상단(3.5) 위로 2유닛
        spawnPoint.transform.SetParent(goSpawn.transform);
        sm.SpawnPoint = spawnPoint.transform;

        // PhysicsManager
        GameObject goPhysics = new GameObject("PhysicsManager");
        goPhysics.AddComponent<PhysicsManager>();

        // VFXManager
        GameObject goVFX = new GameObject("VFXManager");
        goVFX.AddComponent<VFXManager>();

        // RemoteAssetManager (CDN 지원)
        GameObject goRemote = new GameObject("RemoteAssetManager");
        goRemote.AddComponent<RemoteAssetManager>();

        // AudioManager
        GameObject goAudio = new GameObject("AudioManager");
        AudioManager am = goAudio.AddComponent<AudioManager>();
        GameObject bgmSource = new GameObject("BGMSource");
        bgmSource.transform.SetParent(goAudio.transform);
        am.BGMSource = bgmSource.AddComponent<AudioSource>();
        GameObject sfxSource = new GameObject("SFXSource");
        sfxSource.transform.SetParent(goAudio.transform);
        am.SFXSource = sfxSource.AddComponent<AudioSource>();

        // TossBridgeManager
        GameObject goTossBridge = new GameObject("TossBridgeManager");
        goTossBridge.AddComponent<TossBridgeManager>();

        // BridgeManager
        GameObject goBridge = new GameObject("BridgeManager");
        goBridge.AddComponent<BridgeManager>();

        // ResultCard
        GameObject goResult = new GameObject("ResultCard");
        ResultCard rc = goResult.AddComponent<ResultCard>();
        GameObject renderCam = new GameObject("RenderCamera");
        renderCam.transform.SetParent(goResult.transform);
        Camera cam = renderCam.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.Color;
        cam.backgroundColor = Color.cyan;
        rc.RenderCamera = cam;

        RenderTexture rt = new RenderTexture(1080, 1920, 24);
        rt.name = "ScreenshotRT";
        AssetDatabase.CreateAsset(rt, "Assets/_Project/Data/ScreenshotRT.renderTexture");
        rc.RenderTex = rt;
        cam.targetTexture = rt;
        cam.enabled = false;
    }

    // ================================================================
    //  5. UI 시스템
    // ================================================================
    private static void CreateUI()
    {
        // 테마 색상 정의 (토스 디자인 가이드라인 준수)
        Color bgWarm = new Color(0.99f, 0.98f, 0.97f, 1f);           // #fdfaf7
        Color tossBlue = new Color(0.19f, 0.51f, 0.96f, 1f);         // #3182f6 토스 블루
        Color tossBlueDark = new Color(0.11f, 0.39f, 0.85f, 1f);     // #1b64da
        Color darkText = new Color(0.10f, 0.12f, 0.16f, 1f);         // #191f28
        Color lightText = new Color(0.31f, 0.35f, 0.41f, 1f);        // #4e5968
        Color successGreen = new Color(0.20f, 0.78f, 0.35f, 1f);
        Color dangerRed = new Color(1f, 0.25f, 0.25f, 1f);
        Color cardBg = new Color(1f, 1f, 1f, 0.96f);
        Color overlayBg = new Color(0f, 0f, 0f, 0.5f);

        // EventSystem
        if (Object.FindObjectOfType<EventSystem>() == null)
        {
            GameObject es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            es.AddComponent<StandaloneInputModule>();
        }

        // Canvas
        GameObject canvasGo = new GameObject("Canvas");
        Canvas canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080, 1920);
        scaler.matchWidthOrHeight = 0.5f;
        canvasGo.AddComponent<GraphicRaycaster>();
        canvasGo.AddComponent<TossSafeArea>();

        GameObject uiManagerGo = new GameObject("UIManager");
        UIManager uiManager = uiManagerGo.AddComponent<UIManager>();

        // ═══════════════════════════════════════════════════════
        //  LANDING PANEL
        // ═══════════════════════════════════════════════════════
        GameObject landingPanel = CreatePanel(canvasGo.transform, "LandingPanel", bgWarm);

        // 타이틀
        Text landingTitle = CreateText(landingPanel.transform, "Text_Title",
            "디저트 팝!", new Vector2(0, 320), 80, darkText);
        landingTitle.fontStyle = FontStyle.Bold;

        // 서브타이틀
        CreateText(landingPanel.transform, "Text_Subtitle",
            "같은 디저트를 합쳐서 진화시켜 보세요", new Vector2(0, 220), 36, lightText);

        // 디저트 미리보기 이모지
        CreateText(landingPanel.transform, "Text_DessertPreview",
            "🍬 🍩 🎂 🍰 👑", new Vector2(0, 100), 52, Color.white);

        // 최고점수 카드
        GameObject scoreCard = CreateRoundedPanel(landingPanel.transform, "ScoreCard",
            new Color(1f, 1f, 1f, 0.95f), new Vector2(0, -40), new Vector2(420, 120));
        CreateText(scoreCard.transform, "Text_ScoreLabel", "최고 점수", new Vector2(0, 18), 26, lightText);
        Text landingHighTxt = CreateText(scoreCard.transform, "Text_HighScore", "0",
            new Vector2(0, -18), 48, tossBlue);
        landingHighTxt.fontStyle = FontStyle.Bold;

        // 로그인 상태
        Text loginStatusTxt = CreateText(landingPanel.transform, "Text_LoginStatus",
            "연결 중이에요...", new Vector2(0, -130), 28, lightText);

        // 시작 버튼 (토스 블루)
        Button btnStart = CreateStyledButton(landingPanel.transform, "Btn_Start",
            "게임 시작하기", new Vector2(0, -260), new Vector2(480, 110),
            tossBlue, Color.white, 42);

        // 랜딩 패널 레퍼런스
        uiManager.LandingPanel = landingPanel;
        uiManager.LandingHighScoreText = landingHighTxt;
        uiManager.LoginStatusText = loginStatusTxt;
        uiManager.ApplicationStartButton = btnStart;

        // ═══════════════════════════════════════════════════════
        //  HUD PANEL (인게임)
        // ═══════════════════════════════════════════════════════
        GameObject hudPanel = new GameObject("HUDPanel");
        hudPanel.transform.SetParent(canvasGo.transform, false);
        RectTransform hudRt = hudPanel.AddComponent<RectTransform>();
        hudRt.anchorMin = Vector2.zero; hudRt.anchorMax = Vector2.one;
        hudRt.offsetMin = Vector2.zero; hudRt.offsetMax = Vector2.zero;
        hudPanel.AddComponent<CanvasGroup>();

        // ── 점수 표시 (상단 중앙) ──
        GameObject scoreBar = CreateRoundedPanel(hudPanel.transform, "ScoreBar",
            new Color(0.12f, 0.08f, 0.05f, 0.85f), Vector2.zero, new Vector2(340, 90));
        RectTransform sbRt = scoreBar.GetComponent<RectTransform>();
        sbRt.anchorMin = new Vector2(0.5f, 1); sbRt.anchorMax = new Vector2(0.5f, 1);
        sbRt.pivot = new Vector2(0.5f, 1);
        sbRt.anchoredPosition = new Vector2(0, -20);

        Text scoreTxt = CreateText(scoreBar.transform, "Text_Score", "0",
            Vector2.zero, 48, Color.white);
        scoreTxt.fontStyle = FontStyle.Bold;
        scoreTxt.alignment = TextAnchor.MiddleCenter;
        SetStretchFill(scoreTxt.GetComponent<RectTransform>());

        // ── Next 미리보기 (우상단) ──
        GameObject nextBox = CreateRoundedPanel(hudPanel.transform, "NextBox",
            new Color(1f, 1f, 1f, 0.9f), Vector2.zero, new Vector2(160, 100));
        RectTransform nbRt = nextBox.GetComponent<RectTransform>();
        nbRt.anchorMin = new Vector2(1, 1); nbRt.anchorMax = new Vector2(1, 1);
        nbRt.pivot = new Vector2(1, 1);
        nbRt.anchoredPosition = new Vector2(-20, -20);

        CreateText(nextBox.transform, "Text_NextLabel", "NEXT", new Vector2(0, 20), 22, lightText);
        Text nextTxt = CreateText(nextBox.transform, "Text_NextGuide", "Lv.1",
            new Vector2(0, -12), 36, tossBlue);
        nextTxt.fontStyle = FontStyle.Bold;

        // ── 사운드 토글 (좌상단) ──
        Button soundBtn = CreateIconButton(hudPanel.transform, "Btn_SoundToggle",
            "🔊", new Vector2(20, -20), new Vector2(80, 80));
        RectTransform soundRt = soundBtn.GetComponent<RectTransform>();
        soundRt.anchorMin = new Vector2(0, 1); soundRt.anchorMax = new Vector2(0, 1);
        soundRt.pivot = new Vector2(0, 1);

        // ── 도감 버튼 (좌상단, 사운드 아래) ──
        Button encycBtn = CreateIconButton(hudPanel.transform, "Btn_OpenEncyclopedia",
            "📖", new Vector2(20, -110), new Vector2(80, 80));
        RectTransform encycRt = encycBtn.GetComponent<RectTransform>();
        encycRt.anchorMin = new Vector2(0, 1); encycRt.anchorMax = new Vector2(0, 1);
        encycRt.pivot = new Vector2(0, 1);

        // ── 나가기 버튼 (우하단 작게) ──
        Button exitBtn = CreateIconButton(hudPanel.transform, "Btn_OpenExitModal",
            "✖", new Vector2(-20, 20), new Vector2(70, 70));
        RectTransform exitRt = exitBtn.GetComponent<RectTransform>();
        exitRt.anchorMin = new Vector2(1, 0); exitRt.anchorMax = new Vector2(1, 0);
        exitRt.pivot = new Vector2(1, 0);

        // HUD 레퍼런스
        uiManager.ScoreText = scoreTxt;
        uiManager.NextGuideText = nextTxt;
        uiManager.HUDPanel = hudPanel;
        uiManager.SoundToggleButton = soundBtn;
        uiManager.SoundToggleText = soundBtn.GetComponentInChildren<Text>();
        uiManager.OpenEncyclopediaButton = encycBtn;
        uiManager.OpenExitModalButton = exitBtn;
        hudPanel.SetActive(false);


        // ═══════════════════════════════════════════════════════
        //  GAME OVER PANEL
        // ═══════════════════════════════════════════════════════
        GameObject gameOverPanel = CreatePanel(canvasGo.transform, "GameOverPanel", overlayBg);

        // ── 결과 카드 ──
        GameObject goCard = CreateRoundedPanel(gameOverPanel.transform, "GoCard",
            cardBg, new Vector2(0, 60), new Vector2(720, 900));

        // Game Over 타이틀
        Text goTitle = CreateText(goCard.transform, "Text_GOTitle",
            "게임이 끝났어요!", new Vector2(0, 330), 52, darkText);
        goTitle.fontStyle = FontStyle.Bold;

        // 이모지 장식
        CreateText(goCard.transform, "Text_GOEmoji",
            "🍩", new Vector2(0, 250), 56, Color.white);

        // 구분선
        GameObject divider = new GameObject("Divider");
        divider.transform.SetParent(goCard.transform, false);
        RectTransform divRt = divider.AddComponent<RectTransform>();
        divRt.anchoredPosition = new Vector2(0, 180);
        divRt.sizeDelta = new Vector2(560, 3);
        Image divImg = divider.AddComponent<Image>();
        divImg.color = new Color(1f, 1f, 1f, 0.15f);

        // 점수 레이블
        CreateText(goCard.transform, "Text_ScoreLabel2", "점수",
            new Vector2(0, 130), 24, lightText);

        Text goScore = CreateText(goCard.transform, "Text_GOScore",
            "0", new Vector2(0, 75), 64, darkText);
        goScore.fontStyle = FontStyle.Bold;

        // 최고 점수
        CreateText(goCard.transform, "Text_BestLabel", "최고 기록",
            new Vector2(0, 10), 24, lightText);

        Text goHigh = CreateText(goCard.transform, "Text_HighScore",
            "0", new Vector2(0, -40), 44, tossBlue);
        goHigh.fontStyle = FontStyle.Bold;

        // 구분선 2
        GameObject divider2 = new GameObject("Divider2");
        divider2.transform.SetParent(goCard.transform, false);
        RectTransform div2Rt = divider2.AddComponent<RectTransform>();
        div2Rt.anchoredPosition = new Vector2(0, -90);
        div2Rt.sizeDelta = new Vector2(560, 3);
        Image div2Img = divider2.AddComponent<Image>();
        div2Img.color = new Color(1f, 1f, 1f, 0.15f);

        // 다시하기 버튼 (토스 블루 — 주요 액션)
        Button btnRestart = CreateStyledButton(goCard.transform, "Btn_Restart",
            "다시 도전할게요", new Vector2(0, -160), new Vector2(540, 90),
            tossBlue, Color.white, 36);

        // 부활 버튼
        Button btnRevive = CreateStyledButton(goCard.transform, "Btn_Revive",
            "광고 보고 이어서 할게요", new Vector2(0, -260), new Vector2(540, 90),
            new Color(0.95f, 0.96f, 0.97f), darkText, 34);

        // 공유 버튼
        Button btnShare = CreateStyledButton(goCard.transform, "Btn_Share",
            "결과 공유하기", new Vector2(0, -360), new Vector2(540, 90),
            new Color(1f, 1f, 1f, 0f), lightText, 34);

        // Game Over 레퍼런스
        uiManager.GameOverPanel = gameOverPanel;
        uiManager.HighScoreText = goHigh;
        uiManager.AdReviveButton = btnRevive;
        uiManager.ShareButton = btnShare;
        uiManager.RestartButton = btnRestart;
        gameOverPanel.SetActive(false);

        // ═══════════════════════════════════════════════════════
        //  ENCYCLOPEDIA PANEL (도감)
        // ═══════════════════════════════════════════════════════
        GameObject encycPanel = CreatePanel(canvasGo.transform, "EncyclopediaPanel", overlayBg);

        GameObject encycCard = CreateRoundedPanel(encycPanel.transform, "EncycCard",
            new Color(1f, 0.98f, 0.94f, 0.98f), new Vector2(0, 0), new Vector2(800, 1200));

        CreateText(encycCard.transform, "Text_EncycTitle",
            "📖 디저트 도감", new Vector2(0, 520), 56, darkText).fontStyle = FontStyle.Bold;

        // 도감 항목 (11단계)
        string[] dessertNames = {
            "Lv.1  🍬 젤리빈", "Lv.2  🍪 마카롱", "Lv.3  🍩 도넛",
            "Lv.4  🧁 컵케이크", "Lv.5  🥐 소금빵", "Lv.6  🍰 조각케이크",
            "Lv.7  🥧 타르트", "Lv.8  🍧 빙수", "Lv.9  🎂 홀케이크",
            "Lv.10 🗼 티 세트", "Lv.11 👑 파라다이스"
        };

        for (int i = 0; i < dessertNames.Length; i++)
        {
            float yPos = 420 - i * 80;
            Text entryTxt = CreateText(encycCard.transform, $"Text_Ency_{i}",
                dessertNames[i], new Vector2(0, yPos), 34,
                i < 5 ? darkText : new Color(0.6f, 0.5f, 0.4f)); // 미발견은 흐리게
            entryTxt.alignment = TextAnchor.MiddleLeft;
            RectTransform entryRt = entryTxt.GetComponent<RectTransform>();
            entryRt.anchoredPosition = new Vector2(-200, yPos);
            entryRt.sizeDelta = new Vector2(600, 70);
        }

        Button closeEncycBtn = CreateStyledButton(encycCard.transform, "Btn_CloseEncyclopedia",
            "닫기", new Vector2(0, -520), new Vector2(300, 80),
            lightText, Color.white, 36);

        uiManager.EvolutionEncyclopediaPanel = encycPanel;
        uiManager.CloseEncyclopediaButton = closeEncycBtn;
        encycPanel.SetActive(false);

        // ═══════════════════════════════════════════════════════
        //  EXIT MODAL (나가기 확인)
        // ═══════════════════════════════════════════════════════
        GameObject exitPanel = CreatePanel(canvasGo.transform, "ExitModalPanel", overlayBg);

        GameObject exitCard = CreateRoundedPanel(exitPanel.transform, "ExitCard",
            new Color(1f, 0.98f, 0.94f, 0.98f), new Vector2(0, 0), new Vector2(600, 380));

        CreateText(exitCard.transform, "Text_ExitTitle",
            "게임을 종료하시겠어요?", new Vector2(0, 100), 40, darkText).fontStyle = FontStyle.Bold;
        CreateText(exitCard.transform, "Text_ExitSub",
            "현재 진행 상황이 저장돼요.", new Vector2(0, 40), 28, lightText);

        Button confirmExitBtn = CreateStyledButton(exitCard.transform, "Btn_ConfirmExit",
            "종료할게요", new Vector2(0, -60), new Vector2(440, 80),
            dangerRed, Color.white, 34);

        Button cancelExitBtn = CreateStyledButton(exitCard.transform, "Btn_CancelExit",
            "계속 할게요", new Vector2(0, -150), new Vector2(440, 80),
            tossBlue, Color.white, 34);

        uiManager.ExitModalPanel = exitPanel;
        uiManager.ConfirmExitButton = confirmExitBtn;
        uiManager.CancelExitButton = cancelExitBtn;
        exitPanel.SetActive(false);

        Debug.Log("[DessertPopSetup] ✅ UI 생성 완료");
    }

    // ================================================================
    //  UI 헬퍼 메서드
    // ================================================================

    private static GameObject CreatePanel(Transform parent, string name, Color color)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        RectTransform rt = go.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        Image img = go.AddComponent<Image>();
        img.color = color;
        return go;
    }

    private static GameObject CreateRoundedPanel(Transform parent, string name,
        Color color, Vector2 position, Vector2 size)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        RectTransform rt = go.AddComponent<RectTransform>();
        rt.anchoredPosition = position;
        rt.sizeDelta = size;
        Image img = go.AddComponent<Image>();
        img.color = color;
        img.type = Image.Type.Sliced;
        return go;
    }

    private static Text CreateText(Transform parent, string name, string content,
        Vector2 pos, int fontSize, Color color)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        RectTransform rt = go.AddComponent<RectTransform>();
        rt.anchoredPosition = pos;
        rt.sizeDelta = new Vector2(800, 200);
        Text txt = go.AddComponent<Text>();
        txt.text = content;
        txt.alignment = TextAnchor.MiddleCenter;
        txt.fontSize = fontSize;
        txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        txt.color = color;
        txt.horizontalOverflow = HorizontalWrapMode.Overflow;
        return txt;
    }

    private static Button CreateStyledButton(Transform parent, string name, string content,
        Vector2 pos, Vector2 size, Color btnColor, Color textColor, int fontSize)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        RectTransform rt = go.AddComponent<RectTransform>();
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        Image img = go.AddComponent<Image>();
        img.color = btnColor;
        img.type = Image.Type.Sliced;
        Button btn = go.AddComponent<Button>();

        // 버튼 색상 전환
        ColorBlock colors = btn.colors;
        colors.highlightedColor = new Color(
            Mathf.Clamp01(btnColor.r + 0.1f),
            Mathf.Clamp01(btnColor.g + 0.1f),
            Mathf.Clamp01(btnColor.b + 0.1f));
        colors.pressedColor = new Color(
            Mathf.Clamp01(btnColor.r - 0.15f),
            Mathf.Clamp01(btnColor.g - 0.15f),
            Mathf.Clamp01(btnColor.b - 0.15f));
        btn.colors = colors;

        Text txt = CreateText(go.transform, "Text", content, Vector2.zero, fontSize, textColor);
        txt.fontStyle = FontStyle.Bold;
        RectTransform txtRt = txt.GetComponent<RectTransform>();
        txtRt.sizeDelta = size;

        return btn;
    }

    private static Button CreateIconButton(Transform parent, string name, string icon,
        Vector2 pos, Vector2 size)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        RectTransform rt = go.AddComponent<RectTransform>();
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;

        Image img = go.AddComponent<Image>();
        img.color = new Color(0f, 0f, 0f, 0.35f);

        Button btn = go.AddComponent<Button>();
        ColorBlock cb = btn.colors;
        cb.highlightedColor = new Color(0f, 0f, 0f, 0.5f);
        cb.pressedColor = new Color(0f, 0f, 0f, 0.65f);
        btn.colors = cb;

        Text txt = CreateText(go.transform, "Text", icon, Vector2.zero, 38, Color.white);
        txt.GetComponent<RectTransform>().sizeDelta = size;

        return btn;
    }

    private static void SetStretchFill(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        rt.anchoredPosition = Vector2.zero;
    }

    private static Button CreateButton(Transform parent, string name, string content,
        Vector2 pos, Color btnColor, Color textColor)
    {
        return CreateStyledButton(parent, name, content, pos, new Vector2(400, 140),
            btnColor, textColor, 45);
    }

    // ================================================================
    //  6. 기본 데이터
    // ================================================================
    private static void CreateDefaultData()
    {
        DessertEvolutionData data = AssetDatabase.LoadAssetAtPath<DessertEvolutionData>(
            "Assets/_Project/Data/DessertEvolutionData.asset");
        if (data == null)
        {
            data = ScriptableObject.CreateInstance<DessertEvolutionData>();
            data.Levels = new DessertEvolutionData.DessertLevelData[11];
            string[] namesEN = {
                "Jelly Bean", "Macaron", "Donut", "Cupcake", "Salt Bread",
                "Cake Slice", "Fruit Tart", "Bingsu", "Whole Cake", "Three-Tier Tray", "Giant Pound Cake"
            };
            string[] namesKR = {
                "젤리빈", "마카롱", "도넛", "컵케이크", "소금빵",
                "조각 케이크", "과일 타르트", "팥빙수", "프리미엄 홀케이크", "애프터눈 티 세트", "로열 디저트 파라다이스 👑"
            };
            string[] descriptions = {
                "알록달록 톡톡 튀는 첫 번째 디저트!",
                "겉은 바삭, 속은 촉촉한 파리의 맛",
                "알록달록 스프링클이 매력 포인트!",
                "부드러운 크림 한 입이면 기분 UP",
                "따끈따끈 갓 구운 버터 소금빵",
                "딸기 생크림의 클래식한 한 조각",
                "보석처럼 빛나는 과일들의 향연",
                "여름엔 역시 얼음 위의 달콤함!",
                "촛불 후~ 불면 소원이 이루어져요 🎂",
                "우아한 3단 트레이의 품격 🗼",
                "디저트 세계의 전설, 황금빛 왕관의 주인 ✨"
            };
            for (int i = 0; i < 11; i++)
            {
                data.Levels[i].Level = i + 1;
                data.Levels[i].Name = namesEN[i];
                data.Levels[i].NameKR = namesKR[i];
                data.Levels[i].FlavorText = descriptions[i];
                data.Levels[i].ScaleMultiplier = 1.0f + (i * 0.15f); // Slightly smaller scale increment
                data.Levels[i].ScorePoint = (i + 1) * 10;
            }
            AssetDatabase.CreateAsset(data, "Assets/_Project/Data/DessertEvolutionData.asset");
            AssetDatabase.SaveAssets();
        }

        GameManager gm = Object.FindObjectOfType<GameManager>();
        if (gm != null)
        {
            gm.EvolutionData = data;
        }
    }

    // [MenuItem("DessertPop/Link Audio & Setup Button SFX", false, 20)]
    public static void LinkAudioAssets()
    {
        AudioManager am = Object.FindObjectOfType<AudioManager>();
        if (am == null) return;

        // 1. 오디오 파일 자동 검색 및 할당
        am.DropSFX = FindAudioClip("Drop");
        am.MergeSFX = FindAudioClip("Merge");
        am.ScoreSFX = FindAudioClip("Score");
        am.UIClickSFX = FindAudioClip("Click");
        am.GameOverSFX = FindAudioClip("GameOver");

        EditorUtility.SetDirty(am);

        // 2. 모든 버튼에 클릭 사운드 이벤트 연결
        SetupButtonSounds(am);

        Debug.Log("[DessertPopSetup] ✅ 오디오 에셋 및 버튼 SFX 연결 완료");
    }

    private static AudioClip FindAudioClip(string keyword)
    {
        string[] searchPaths = new[] { "Assets/_Project/Audio/SFX", "Assets/_Project/Resources/Audio/SFX" };
        string[] guids = AssetDatabase.FindAssets($"{keyword} t:AudioClip", searchPaths);
        if (guids.Length > 0)
        {
            return AssetDatabase.LoadAssetAtPath<AudioClip>(AssetDatabase.GUIDToAssetPath(guids[0]));
        }
        return null;
    }

    private static void SetupButtonSounds(AudioManager am)
    {
        Button[] buttons = Object.FindObjectsOfType<Button>(true);
        foreach (var btn in buttons)
        {
            // 중복 연결 방지를 위해 기존 이벤트 확인은 어렵지만, 보통 에디터 스크립트에서는 아래처럼 추가
            // 프로그래밍 방식으로 OnClick에 추가 (Runtime) 혹은 UnityEvent 에디터 설정
            UnityEditor.Events.UnityEventTools.RemovePersistentListener(btn.onClick, am.PlayUIClick);
            UnityEditor.Events.UnityEventTools.AddPersistentListener(btn.onClick, am.PlayUIClick);
            EditorUtility.SetDirty(btn);
        }
    }
}
