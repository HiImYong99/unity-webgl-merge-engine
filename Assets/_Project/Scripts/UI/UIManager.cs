using UnityEngine;
using UnityEngine.UI;

public class UIManager : MonoBehaviour
{
    public static UIManager Instance { get; private set; }

    [Header("UI Panels")]
    public GameObject GameOverPanel;
    public GameObject EvolutionEncyclopediaPanel;
    public GameObject ExitModalPanel;
    public GameObject LandingPanel;
    public Text ScoreText;
    public Text HighScoreText;
    public Text LandingHighScoreText;
    public Text LoginStatusText;
    public Button ApplicationStartButton;
    public Text NextGuideText;
    public Button AdReviveButton;
    public Button ShareButton;
    public Button RestartButton;
    public Button OpenEncyclopediaButton;
    public Button CloseEncyclopediaButton;
    public Button OpenExitModalButton;
    public Button ConfirmExitButton;
    public Button CancelExitButton;
    public Button SoundToggleButton;
    public Text SoundToggleText;

    [Header("Extra Panels")]
    public GameObject HUDPanel;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }

        FindMissingReferences();

        // 모든 패널을 초기에 숨김 — 랜딩/HUD는 GameManager.Start()가 올바른 순서로 표시
        HideAllPanels();
    }

    private void HideAllPanels()
    {
        if (LandingPanel != null) LandingPanel.SetActive(false);
        if (HUDPanel != null) HUDPanel.SetActive(false);
        if (GameOverPanel != null) GameOverPanel.SetActive(false);
        if (EvolutionEncyclopediaPanel != null) EvolutionEncyclopediaPanel.SetActive(false);
        if (ExitModalPanel != null) ExitModalPanel.SetActive(false);
    }

    private void Start()
    {
        InitializePanels();
        ApplyPremiumUIAssets();
        
        // 버튼 리스너 연결
        if (ApplicationStartButton != null) ApplicationStartButton.onClick.AddListener(OnApplicationStartClicked);
        if (SoundToggleButton != null) SoundToggleButton.onClick.AddListener(OnSoundToggleClicked);
        if (AdReviveButton != null) AdReviveButton.onClick.AddListener(OnAdReviveClicked);
        if (ShareButton != null) ShareButton.onClick.AddListener(OnShareClicked);
        if (RestartButton != null) RestartButton.onClick.AddListener(OnRestartClicked);
        if (OpenEncyclopediaButton != null) OpenEncyclopediaButton.onClick.AddListener(OnOpenEncyclopediaClicked);
        if (CloseEncyclopediaButton != null) CloseEncyclopediaButton.onClick.AddListener(OnCloseEncyclopediaClicked);
        if (ConfirmExitButton != null) ConfirmExitButton.onClick.AddListener(OnConfirmExitClicked);
        if (CancelExitButton != null) CancelExitButton.onClick.AddListener(OnCancelExitClicked);
        if (OpenExitModalButton != null) OpenExitModalButton.onClick.AddListener(OnOpenExitModalClicked);

        UpdateSoundToggleUI();
    }

    private void ApplyPremiumUIAssets()
    {
        // 프리미엄 설정 아이콘 로드 및 적용 (이모지 대체)
        Sprite settingsSprite = Resources.Load<Sprite>("UI/SettingsIcon");
        if (settingsSprite != null && SoundToggleButton != null)
        {
            Image btnImg = SoundToggleButton.GetComponent<Image>();
            if (btnImg != null)
            {
                btnImg.sprite = settingsSprite;
                btnImg.color = Color.white;
            }

            // 기존 이모지 텍스트 숨기기
            if (SoundToggleText != null)
                SoundToggleText.gameObject.SetActive(false);
        }
    }

    private void InitializePanels()
    {
        // 초기 패널 상태 설정 (GameManager에서 제어하지만 명시적 초기화)
        if (LandingPanel != null) LandingPanel.SetActive(true);
        if (HUDPanel != null) HUDPanel.SetActive(false);
    }

    private void OnApplicationStartClicked()
    {
        if (GameManager.Instance != null)
            GameManager.Instance.StartGame();
    }

    private void OnOpenEncyclopediaClicked() => ShowEncyclopedia(true);
    private void OnCloseEncyclopediaClicked() => ShowEncyclopedia(false);
    private void OnOpenExitModalClicked() => ShowExitModal(true);
    private void OnCancelExitClicked() => ShowExitModal(false);


    // ================================================================
    //  자동 레퍼런스 탐색
    // ================================================================
    private void FindMissingReferences()
    {
        Canvas canvas = FindObjectOfType<Canvas>();
        if (canvas == null) return;

        if (LandingPanel == null) LandingPanel = FindInactiveChild(canvas.transform, "LandingPanel");
        if (GameOverPanel == null) GameOverPanel = FindInactiveChild(canvas.transform, "GameOverPanel");
        if (EvolutionEncyclopediaPanel == null) EvolutionEncyclopediaPanel = FindInactiveChild(canvas.transform, "EncyclopediaPanel");
        if (ExitModalPanel == null) ExitModalPanel = FindInactiveChild(canvas.transform, "ExitModalPanel");
        if (ApplicationStartButton == null) ApplicationStartButton = FindButtonInChildren(canvas.transform, "Btn_Start");
        if (RestartButton == null) RestartButton = FindButtonInChildren(canvas.transform, "Btn_Restart");
        if (AdReviveButton == null) AdReviveButton = FindButtonInChildren(canvas.transform, "Btn_Revive");
        if (ScoreText == null) ScoreText = FindTextInChildren(canvas.transform, "Text_Score");

        Debug.Log($"[UIManager] References resolved: LandingPanel={LandingPanel != null}, StartBtn={ApplicationStartButton != null}, ScoreText={ScoreText != null}");
    }

    private static GameObject FindInactiveChild(Transform parent, string name)
    {
        foreach (Transform t in parent.GetComponentsInChildren<Transform>(includeInactive: true))
        {
            if (t.gameObject.name == name) return t.gameObject;
        }
        return null;
    }

    private static Button FindButtonInChildren(Transform parent, string name)
    {
        var go = FindInactiveChild(parent, name);
        return go != null ? go.GetComponent<Button>() : null;
    }

    private static Text FindTextInChildren(Transform parent, string name)
    {
        var go = FindInactiveChild(parent, name);
        return go != null ? go.GetComponent<Text>() : null;
    }

    // ================================================================
    //  랜딩 페이지
    // ================================================================
    public void ShowLandingPage(bool show)
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        // WebGL: 랜딩 UI는 HTML이 담당. HUD는 랜딩 중에 숨김.
        if (LandingPanel != null) LandingPanel.SetActive(false);
        if (HUDPanel != null) HUDPanel.SetActive(false);

        if (show)
        {
            int best = GameManager.Instance != null ? GameManager.Instance.HighScore : 0;
            ShowHtmlLanding(best);
        }
#else
        if (LandingPanel != null) LandingPanel.SetActive(show);
        if (HUDPanel != null) HUDPanel.SetActive(!show);
        if (show && LandingHighScoreText != null && GameManager.Instance != null)
            LandingHighScoreText.text = GameManager.Instance.HighScore.ToString();
#endif
    }

    /// <summary>
    /// 게임 플레이 시작 시 HUD를 표시합니다.
    /// WebGL 빌드에서는 HTML overlay가 HUD 역할을 하므로 Unity Canvas HUD는 숨깁니다.
    /// </summary>
    public void ShowHUD(bool show)
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        // WebGL: HTML overlay가 모든 HUD를 담당 — Unity Canvas HUD는 항상 숨김
        if (HUDPanel != null) HUDPanel.SetActive(false);
#else
        if (HUDPanel != null) HUDPanel.SetActive(show);
#endif
    }

#if UNITY_WEBGL && !UNITY_EDITOR
    [System.Runtime.InteropServices.DllImport("__Internal")]
    private static extern void _ShowHtmlLanding(int bestScore);

    private static void ShowHtmlLanding(int bestScore)
    {
        try { _ShowHtmlLanding(bestScore); } catch { }
    }
#else
    private static void ShowHtmlLanding(int bestScore) { }
#endif

    // ================================================================
    //  스코어 / Next 업데이트
    // ================================================================
    public void UpdateScore(int score)
    {
        if (ScoreText != null)
        {
            ScoreText.text = score.ToString("N0");
            ScoreText.color = new Color(0.44f, 0.25f, 0.25f); // #704040
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        try { UpdateScoreJS(score); } catch { }
#endif
    }

    public void UpdateNextGuide(int nextLevel)
    {
        if (NextGuideText != null)
        {
            string[] dessertEmojis = { "🍬", "🍪", "🍩", "🧁", "🥐", "🍰", "🥧", "🍧", "🎂", "🗼", "🏆" };
            int idx = Mathf.Clamp(nextLevel - 1, 0, dessertEmojis.Length - 1);
            NextGuideText.text = dessertEmojis[idx] + " Lv." + nextLevel;
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        try { UpdateNextJS(nextLevel); } catch { }
#endif
    }

    // ================================================================
    //  게임 오버
    // ================================================================
    public void ShowGameOver()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        if (GameOverPanel != null) GameOverPanel.SetActive(false); // HTML에서 처리하므로 숨김
#else
        if (GameOverPanel != null) GameOverPanel.SetActive(true);
#endif

        if (HighScoreText != null && GameManager.Instance != null)
            HighScoreText.text = GameManager.Instance.HighScore.ToString("N0");

#if UNITY_WEBGL && !UNITY_EDITOR
        if (GameManager.Instance != null)
        {
            try { ShowGameOverJS(GameManager.Instance.Score, GameManager.Instance.HighScore, GameManager.Instance.AdWatched, GameManager.Instance.SpareLives); } catch { }
        }
#endif
    }

    public void HideGameOver()
    {
        if (GameOverPanel != null) GameOverPanel.SetActive(false);
    }

    // ================================================================
    //  기타 패널
    // ================================================================
    public void UpdateLoginStatus(string status)
    {
        if (LoginStatusText != null)
            LoginStatusText.text = status;
    }

    public void ShowEncyclopedia(bool show)
    {
        if (EvolutionEncyclopediaPanel != null)
            EvolutionEncyclopediaPanel.SetActive(show);
    }

    public void ShowExitModal(bool show)
    {
        if (ExitModalPanel != null)
            ExitModalPanel.SetActive(show);
    }

    // ================================================================
    //  버튼 핸들러
    // ================================================================
    private void OnConfirmExitClicked()
    {
        if (GameManager.Instance != null && GameManager.Instance.CurrentState == GameManager.GameState.Playing)
            GameManager.Instance.SaveCurrentBoardState();

        if (BridgeManager.Instance != null)
            BridgeManager.Instance.RequestExitApp();
    }

    private void OnSoundToggleClicked()
    {
        if (GameManager.Instance != null && AudioManager.Instance != null)
        {
            bool isNowEnabled = !GameManager.Instance.IsSfxEnabled;
            GameManager.Instance.UpdateSettings(isNowEnabled, GameManager.Instance.IsVibrationEnabled);
            AudioManager.Instance.SetMute(!isNowEnabled);
            UpdateSoundToggleUI();
        }
    }

    private void UpdateSoundToggleUI()
    {
        if (SoundToggleText != null && GameManager.Instance != null)
        {
            SoundToggleText.text = GameManager.Instance.IsSfxEnabled ? "❤️" : "🤍";
            SoundToggleText.color = new Color(1f, 0.41f, 0.71f); // #FF69B4
        }
    }

    private void OnAdReviveClicked()
    {
        if (BridgeManager.Instance != null)
            BridgeManager.Instance.RequestAd();
    }

    private void OnShareClicked()
    {
        if (ResultCard.Instance != null)
            ResultCard.Instance.CaptureAndShare();
    }

    private void OnRestartClicked()
    {
        UnityEngine.SceneManagement.SceneManager.LoadScene(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
    }

    // ================================================================
    //  JS → Unity: SendMessage 수신 메서드 (HTML 설정 패널에서 호출)
    // ================================================================

    /// <summary>HTML 설정 토글 → BGM 음소거 (SendMessage 수신)</summary>
    // JS 전용 리스너는 AudioManager에서 처리하므로 삭제하거나 forward 시킴

    /// <summary>디저트 발견 시 JS 도감 업데이트 알림</summary>
    public void NotifyDessertDiscovered(int level)
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        try { OnDessertDiscoveredJS(level); } catch { }
#endif
    }

    // ================================================================
    //  WebGL JavaScript Bridge
    // ================================================================
#if UNITY_WEBGL && !UNITY_EDITOR
    [System.Runtime.InteropServices.DllImport("__Internal")]
    private static extern void updateScoreFromUnity(int score);
    private static void UpdateScoreJS(int score) { updateScoreFromUnity(score); }

    [System.Runtime.InteropServices.DllImport("__Internal")]
    private static extern void updateNextFromUnity(int level);
    private static void UpdateNextJS(int level) { updateNextFromUnity(level); }

    [System.Runtime.InteropServices.DllImport("__Internal")]
    private static extern void showGameOverFromUnity(int score, int best, bool adWatched, int spareLives);
    private static void ShowGameOverJS(int score, int best, bool adWatched, int spareLives) { 
        showGameOverFromUnity(score, best, adWatched, spareLives); 
    }


    [System.Runtime.InteropServices.DllImport("__Internal")]
    private static extern void onDessertDiscoveredFromUnity(int level);
    private static void OnDessertDiscoveredJS(int level) { onDessertDiscoveredFromUnity(level); }
#else
    private static void UpdateScoreJS(int score) { }
    private static void UpdateNextJS(int level) { }
    private static void ShowGameOverJS(int score, int best, bool adWatched, int spareLives) { }
    private static void OnDessertDiscoveredJS(int level) { }
#endif
}
