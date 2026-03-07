using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 게임의 전체 UI 시스템과 캔버스 패널을 관리하는 매니저
/// </summary>
public class UIMgr : MonoBehaviour
{
    public static UIMgr Instance { get; private set; }

    [Header("UI Panels")]
    public GameObject GameOverPanel;
    public GameObject ExitModalPanel;
    public GameObject LandingPanel;
    
    [Header("Score Controls")]
    public Text ScoreText;
    public Text HighScoreText;
    public Text LandingHighScoreText;
    public Text NextGuideText;

    [Header("Buttons")]
    public Button ApplicationStartButton;
    public Button AdReviveButton;
    public Button ShareButton;
    public Button RestartButton;
    public Button SoundToggleButton;
    public Text SoundToggleText;

    [Header("HUD")]
    public GameObject HUDPanel;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }

        Co_FindMissingReferences();
        HideAllPanels();
    }

    /// <summary>모든 패널을 레이아웃에 맞게 숨김</summary>
    private void HideAllPanels()
    {
        if (LandingPanel != null) LandingPanel.SetActive(false);
        if (HUDPanel != null) HUDPanel.SetActive(false);
        if (GameOverPanel != null) GameOverPanel.SetActive(false);
        if (ExitModalPanel != null) ExitModalPanel.SetActive(false);
    }

    private void Start()
    {
        InitializePanels();
        Co_ApplyPremiumUIAssets();
        
        // 버튼 리스너 등록
        if (ApplicationStartButton != null) ApplicationStartButton.onClick.AddListener(OnApplicationStartClicked);
        if (SoundToggleButton != null) SoundToggleButton.onClick.AddListener(OnSoundToggleClicked);
        if (AdReviveButton != null) AdReviveButton.onClick.AddListener(OnAdReviveClicked);
        if (ShareButton != null) ShareButton.onClick.AddListener(OnShareClicked);
        if (RestartButton != null) RestartButton.onClick.AddListener(OnRestartClicked);

        UpdateSoundToggleUI();
    }

    /// <summary>프리미엄 리소스(세팅 아이콘 등) 동적 적용</summary>
    private void Co_ApplyPremiumUIAssets()
    {
        Sprite settingsSprite = Resources.Load<Sprite>("UI/SettingsIcon");
        if (settingsSprite != null && SoundToggleButton != null)
        {
            Image btnImg = SoundToggleButton.GetComponent<Image>();
            if (btnImg != null)
            {
                btnImg.sprite = settingsSprite;
                btnImg.color = Color.white;
            }

            if (SoundToggleText != null)
                SoundToggleText.gameObject.SetActive(false);
        }
    }

    private void InitializePanels()
    {
        if (LandingPanel != null) LandingPanel.SetActive(true);
        if (HUDPanel != null) HUDPanel.SetActive(false);
    }

    private void OnApplicationStartClicked()
    {
        if (GameMgr.Instance != null)
            GameMgr.Instance.StartGame();
    }

    // ================================================================
    //  자동 레퍼런스 탐색 (캐싱 루틴)
    // ================================================================
    private void Co_FindMissingReferences()
    {
        Canvas canvas = FindObjectOfType<Canvas>();
        if (canvas == null) return;

        // 명시적으로 지정되지 않은 레퍼런스는 이름 기반으로 자동 연결
        if (LandingPanel == null) LandingPanel = FindInactiveChild(canvas.transform, "LandingPanel");
        if (GameOverPanel == null) GameOverPanel = FindInactiveChild(canvas.transform, "GameOverPanel");
        if (ScoreText == null) ScoreText = FindTextInChildren(canvas.transform, "Text_Score");

        Debug.Log($"[UIMgr] Reference setup complete.");
    }

    private static GameObject FindInactiveChild(Transform parent, string name)
    {
        foreach (Transform t in parent.GetComponentsInChildren<Transform>(includeInactive: true))
        {
            if (t.gameObject.name == name) return t.gameObject;
        }
        return null;
    }

    private static Text FindTextInChildren(Transform parent, string name)
    {
        var go = FindInactiveChild(parent, name);
        return go != null ? go.GetComponent<Text>() : null;
    }

    // ================================================================
    //  UI 상태 전환 루틴
    // ================================================================
    public void ShowLandingPage(bool show)
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        if (LandingPanel != null) LandingPanel.SetActive(false);
        if (HUDPanel != null) HUDPanel.SetActive(false);

        if (show)
        {
            int best = GameMgr.Instance != null ? GameMgr.Instance.HighScore : 0;
            Co_ShowHtmlLanding(best);
        }
#else
        if (LandingPanel != null) LandingPanel.SetActive(show);
        if (HUDPanel != null) HUDPanel.SetActive(!show);
        if (show && LandingHighScoreText != null && GameMgr.Instance != null)
            LandingHighScoreText.text = GameMgr.Instance.HighScore.ToString();
#endif
    }

    public void ShowHUD(bool show)
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        if (HUDPanel != null) HUDPanel.SetActive(false);
#else
        if (HUDPanel != null) HUDPanel.SetActive(show);
#endif
    }

#if UNITY_WEBGL && !UNITY_EDITOR
    [System.Runtime.InteropServices.DllImport("__Internal")]
    private static extern void _ShowHtmlLanding(int bestScore);

    private static void Co_ShowHtmlLanding(int bestScore)
    {
        try { _ShowHtmlLanding(bestScore); } catch { }
    }
#else
    private static void Co_ShowHtmlLanding(int bestScore) { }
#endif

    public void UpdateScore(int score)
    {
        if (ScoreText != null)
        {
            ScoreText.text = score.ToString("N0");
            ScoreText.color = new Color(0.44f, 0.25f, 0.25f);
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        try { UpdateScoreJS(score); } catch { }
#endif
    }

    public void UpdateNextGuide(int nextLevel)
    {
        if (NextGuideText != null)
        {
            string[] animalEmojis = { "🐥", "🐭", "🦔", "🐸", "🐰", "🐱", "🐕", "🐷", "🐼", "🐻", "🐯" };
            int idx = Mathf.Clamp(nextLevel - 1, 0, animalEmojis.Length - 1);
            NextGuideText.text = animalEmojis[idx] + " Lv." + nextLevel;
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        try { UpdateNextJS(nextLevel); } catch { }
#endif
    }

    public void ShowGameOver()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        if (GameOverPanel != null) GameOverPanel.SetActive(false);
#else
        if (GameOverPanel != null) GameOverPanel.SetActive(true);
#endif

        if (HighScoreText != null && GameMgr.Instance != null)
            HighScoreText.text = GameMgr.Instance.HighScore.ToString("N0");

#if UNITY_WEBGL && !UNITY_EDITOR
        if (GameMgr.Instance != null)
        {
            try { ShowGameOverJS(GameMgr.Instance.Score, GameMgr.Instance.HighScore, GameMgr.Instance.AdWatched, GameMgr.Instance.SpareLives); } catch { }
        }
#endif
    }

    public void HideGameOver()
    {
        if (GameOverPanel != null) GameOverPanel.SetActive(false);
    }

    public void UpdateLoginStatus(string status)
    {
        if (LoginStatusText != null)
            LoginStatusText.text = status;
        // ... (필요 시 LoginStatusText 레퍼런스 할당 로직 추가)
    }
    public Text LoginStatusText; // 자동 검색 연동용

    public void OnSoundToggleClicked()
    {
        if (GameMgr.Instance != null && SoundMgr.Instance != null)
        {
            bool isNowEnabled = !GameMgr.Instance.IsSfxEnabled;
            GameMgr.Instance.UpdateSettings(isNowEnabled, GameMgr.Instance.IsVibrationEnabled);
            SoundMgr.Instance.SetMute(!isNowEnabled);
            UpdateSoundToggleUI();
        }
    }

    private void UpdateSoundToggleUI()
    {
        if (SoundToggleText != null && GameMgr.Instance != null)
        {
            SoundToggleText.text = GameMgr.Instance.IsSfxEnabled ? "❤️" : "🤍";
            SoundToggleText.color = new Color(1f, 0.41f, 0.71f);
        }
    }

    private void OnAdReviveClicked()
    {
        if (BridgeMgr.Instance != null)
            BridgeMgr.Instance.RequestAd();
    }

    private void OnShareClicked()
    {
        if (ResultCardMgr.Instance != null)
            ResultCardMgr.Instance.CaptureAndShare();
    }

    private void OnRestartClicked()
    {
        UnityEngine.SceneManagement.SceneManager.LoadScene(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
    }


    // WebGL Bridge
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

#else
    private static void UpdateScoreJS(int score) { }
    private static void UpdateNextJS(int level) { }
    private static void ShowGameOverJS(int score, int best, bool adWatched, int spareLives) { }
#endif
}
