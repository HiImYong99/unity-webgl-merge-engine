using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class DessertPopSetup : EditorWindow
{
    [MenuItem("DessertPop/Auto Setup Scene", false, 1)]
    public static void AutoSetupScene()
    {
        if (EditorUtility.DisplayDialog("Auto Setup", "This will create and configure all necessary GameObjects and UI for the Dessert Pop MVP in the current active scene. Proceed?", "Yes", "Cancel"))
        {
            if (!AssetDatabase.IsValidFolder("Assets/_Project/Data"))
            {
                AssetDatabase.CreateFolder("Assets/_Project", "Data");
            }
            CreateCoreManagers();
            CreateUI();
            CreateDefaultData();
            EditorUtility.DisplayDialog("Success", "Scene setup complete! Please create your Dessert prefabs and assign them to the EvolutionData in the Project folder.", "OK");
        }
    }

    private static void CreateCoreManagers()
    {
        // GameManager
        GameObject goGame = new GameObject("GameManager");
        GameManager gm = goGame.AddComponent<GameManager>();

        // SpawnManager
        GameObject goSpawn = new GameObject("SpawnManager");
        SpawnManager sm = goSpawn.AddComponent<SpawnManager>();
        GameObject spawnPoint = new GameObject("SpawnPoint");
        spawnPoint.transform.position = new Vector3(0, 4f, 0);
        spawnPoint.transform.SetParent(goSpawn.transform);
        sm.SpawnPoint = spawnPoint.transform;

        // PhysicsManager
        GameObject goPhysics = new GameObject("PhysicsManager");
        goPhysics.AddComponent<PhysicsManager>();

        // AudioManager
        GameObject goAudio = new GameObject("AudioManager");
        AudioManager am = goAudio.AddComponent<AudioManager>();
        GameObject bgmSource = new GameObject("BGMSource");
        bgmSource.transform.SetParent(goAudio.transform);
        am.BGMSource = bgmSource.AddComponent<AudioSource>();
        GameObject sfxSource = new GameObject("SFXSource");
        sfxSource.transform.SetParent(goAudio.transform);
        am.SFXSource = sfxSource.AddComponent<AudioSource>();

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
        cam.enabled = false; // Only render when needed
    }

    private static void CreateUI()
    {
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
        canvasGo.AddComponent<GraphicRaycaster>();
        SafeArea sa = canvasGo.AddComponent<SafeArea>();

        // UIManager
        GameObject uiManagerGo = new GameObject("UIManager");
        UIManager uiManager = uiManagerGo.AddComponent<UIManager>();

        // 1. Landing Panel
        GameObject landingPanel = CreatePanel(canvasGo.transform, "LandingPanel", new Color(0.1f, 0.1f, 0.1f, 0.9f));
        Text landingHighTxt = CreateText(landingPanel.transform, "Text_HighScore", "최고 점수: 0", new Vector2(0, 100));
        Text loginStatusTxt = CreateText(landingPanel.transform, "Text_LoginStatus", "로그인 중...", new Vector2(0, -100));
        Button btnStart = CreateButton(landingPanel.transform, "Btn_Start", "게임 시작", new Vector2(0, -300));
        uiManager.LandingPanel = landingPanel;
        uiManager.LandingHighScoreText = landingHighTxt;
        uiManager.LoginStatusText = loginStatusTxt;
        uiManager.ApplicationStartButton = btnStart;

        // 2. HUD
        GameObject hudPanel = CreatePanel(canvasGo.transform, "HUDPanel", Color.clear);
        Text scoreTxt = CreateText(hudPanel.transform, "Text_Score", "점수: 0", new Vector2(0, 800));
        Text nextGuideTxt = CreateText(hudPanel.transform, "Text_NextGuide", "다음 디저트: ?", new Vector2(300, 800));
        Button btnSound = CreateButton(hudPanel.transform, "Btn_Sound", "사운드 On/Off", new Vector2(-350, 800));
        Text soundTxt = btnSound.GetComponentInChildren<Text>();
        Button btnOpenEncyclopedia = CreateButton(hudPanel.transform, "Btn_OpenEncy", "도감", new Vector2(-350, -800));
        Button btnExitApp = CreateButton(hudPanel.transform, "Btn_Exit", "X 닫기", new Vector2(350, 800));

        uiManager.ScoreText = scoreTxt;
        uiManager.NextGuideText = nextGuideTxt;
        uiManager.SoundToggleButton = btnSound;
        uiManager.SoundToggleText = soundTxt;
        uiManager.OpenEncyclopediaButton = btnOpenEncyclopedia;
        uiManager.OpenExitModalButton = btnExitApp;

        // 3. Deadline Warning
        GameObject warningPanel = CreatePanel(canvasGo.transform, "DeadlineWarningPanel", new Color(1, 0, 0, 0.3f));
        CreateText(warningPanel.transform, "Text_Warning", "위험해요!", new Vector2(0, 500));
        uiManager.DeadlineWarningPanel = warningPanel;
        warningPanel.SetActive(false);

        // 4. Encyclopedia
        GameObject encyPanel = CreatePanel(canvasGo.transform, "EncyclopediaPanel", new Color(0, 0, 0, 0.8f));
        CreateText(encyPanel.transform, "Title", "진화 도감", new Vector2(0, 600));
        Button btnCloseEncy = CreateButton(encyPanel.transform, "Btn_Close", "닫기", new Vector2(350, 600));
        uiManager.EvolutionEncyclopediaPanel = encyPanel;
        uiManager.CloseEncyclopediaButton = btnCloseEncy;
        encyPanel.SetActive(false);

        // 5. Exit Modal
        GameObject exitPanel = CreatePanel(canvasGo.transform, "ExitModalPanel", new Color(0, 0, 0, 0.8f));
        CreateText(exitPanel.transform, "Title", "정말 종료하시겠습니까?", new Vector2(0, 200));
        Button btnConfirmExit = CreateButton(exitPanel.transform, "Btn_Confirm", "종료하기", new Vector2(200, -100));
        Button btnCancelExit = CreateButton(exitPanel.transform, "Btn_Cancel", "취소", new Vector2(-200, -100));
        uiManager.ExitModalPanel = exitPanel;
        uiManager.ConfirmExitButton = btnConfirmExit;
        uiManager.CancelExitButton = btnCancelExit;
        exitPanel.SetActive(false);

        // 6. Game Over Panel
        GameObject gameOverPanel = CreatePanel(canvasGo.transform, "GameOverPanel", new Color(0, 0, 0, 0.9f));
        CreateText(gameOverPanel.transform, "Title", "Game Over", new Vector2(0, 400));
        Text goHighScore = CreateText(gameOverPanel.transform, "Text_HighScore", "최고 점수: 0", new Vector2(0, 200));
        Button btnRevive = CreateButton(gameOverPanel.transform, "Btn_Revive", "광고 보고 부활", new Vector2(0, -100));
        Button btnShare = CreateButton(gameOverPanel.transform, "Btn_Share", "결과 공유", new Vector2(-200, -300));
        Button btnRestart = CreateButton(gameOverPanel.transform, "Btn_Restart", "다시 하기", new Vector2(200, -300));
        uiManager.GameOverPanel = gameOverPanel;
        uiManager.HighScoreText = goHighScore;
        uiManager.AdReviveButton = btnRevive;
        uiManager.ShareButton = btnShare;
        uiManager.RestartButton = btnRestart;
        gameOverPanel.SetActive(false);
    }

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

    private static Text CreateText(Transform parent, string name, string content, Vector2 pos)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        RectTransform rt = go.AddComponent<RectTransform>();
        rt.anchoredPosition = pos;
        rt.sizeDelta = new Vector2(600, 150);
        Text txt = go.AddComponent<Text>();
        txt.text = content;
        txt.alignment = TextAnchor.MiddleCenter;
        txt.fontSize = 60;
        txt.color = Color.white;
        return txt;
    }

    private static Button CreateButton(Transform parent, string name, string content, Vector2 pos)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        RectTransform rt = go.AddComponent<RectTransform>();
        rt.anchoredPosition = pos;
        rt.sizeDelta = new Vector2(350, 120);
        Image img = go.AddComponent<Image>();
        img.color = Color.white;
        Button btn = go.AddComponent<Button>();

        Text txt = CreateText(go.transform, "Text", content, Vector2.zero);
        txt.color = Color.black;
        txt.fontSize = 40;
        txt.GetComponent<RectTransform>().sizeDelta = new Vector2(350, 120);

        return btn;
    }

    private static void CreateDefaultData()
    {
        DessertEvolutionData data = AssetDatabase.LoadAssetAtPath<DessertEvolutionData>("Assets/_Project/Data/DessertEvolutionData.asset");
        if (data == null)
        {
            data = ScriptableObject.CreateInstance<DessertEvolutionData>();
            data.Levels = new DessertEvolutionData.DessertLevelData[11];
            for (int i = 0; i < 11; i++)
            {
                data.Levels[i].Level = i + 1;
                data.Levels[i].ScaleMultiplier = 1.0f + (i * 0.3f);
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
}
