using UnityEngine;
using UnityEngine.UI;

public class UIManager : MonoBehaviour
{
    public static UIManager Instance { get; private set; }

    [Header("UI Panels")]
    public GameObject GameOverPanel;
    public GameObject DeadlineWarningPanel;
    public GameObject EvolutionEncyclopediaPanel; // 진화 도감 UI
    public GameObject ExitModalPanel; // 미니앱 종료 모달
    public Text ScoreText;
    public Text HighScoreText;
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

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    private void Start()
    {
        HideGameOver();
        ShowDeadlineWarning(false);
        ShowEncyclopedia(false);
        ShowExitModal(false);

        if (AdReviveButton != null) AdReviveButton.onClick.AddListener(OnAdReviveClicked);
        if (ShareButton != null) ShareButton.onClick.AddListener(OnShareClicked);
        if (RestartButton != null) RestartButton.onClick.AddListener(OnRestartClicked);
        if (OpenEncyclopediaButton != null) OpenEncyclopediaButton.onClick.AddListener(OnOpenEncyclopediaClicked);
        if (CloseEncyclopediaButton != null) CloseEncyclopediaButton.onClick.AddListener(OnCloseEncyclopediaClicked);

        if (OpenExitModalButton != null) OpenExitModalButton.onClick.AddListener(() => ShowExitModal(true));
        if (CancelExitButton != null) CancelExitButton.onClick.AddListener(() => ShowExitModal(false));
        if (ConfirmExitButton != null) ConfirmExitButton.onClick.AddListener(OnConfirmExitClicked);

        if (SoundToggleButton != null) SoundToggleButton.onClick.AddListener(OnSoundToggleClicked);

        UpdateSoundToggleUI();
    }

    public void UpdateScore(int score)
    {
        if (ScoreText != null)
        {
            ScoreText.text = "점수: " + score;
        }
    }

    public void UpdateNextGuide(int nextLevel)
    {
        if (NextGuideText != null)
        {
            NextGuideText.text = "다음 디저트: 레벨 " + nextLevel;
        }
    }

    public void ShowGameOver()
    {
        if (GameOverPanel != null) GameOverPanel.SetActive(true);
        if (HighScoreText != null && GameManager.Instance != null)
        {
            HighScoreText.text = "최고 점수: " + GameManager.Instance.HighScore;
        }
    }

    public void HideGameOver()
    {
        if (GameOverPanel != null) GameOverPanel.SetActive(false);
    }

    public void ShowDeadlineWarning(bool show)
    {
        if (DeadlineWarningPanel != null && DeadlineWarningPanel.activeSelf != show)
        {
            DeadlineWarningPanel.SetActive(show);
        }
    }

    public void ShowEncyclopedia(bool show)
    {
        if (EvolutionEncyclopediaPanel != null)
        {
            EvolutionEncyclopediaPanel.SetActive(show);

            // Optionally update content of the encyclopedia based on GameManager.Instance.LastDiscoveryLevel
            // Assuming this logic will be bound to UI scripts inside EvolutionEncyclopediaPanel or simple active state logic here.
        }
    }

    private void OnOpenEncyclopediaClicked()
    {
        ShowEncyclopedia(true);
    }

    private void OnCloseEncyclopediaClicked()
    {
        ShowEncyclopedia(false);
    }

    public void ShowExitModal(bool show)
    {
        if (ExitModalPanel != null) ExitModalPanel.SetActive(show);
    }

    private void OnConfirmExitClicked()
    {
        if (BridgeManager.Instance != null)
        {
            BridgeManager.Instance.RequestExitApp();
        }
    }

    private void OnSoundToggleClicked()
    {
        if (GameManager.Instance != null && AudioManager.Instance != null)
        {
            bool wasEnabled = GameManager.Instance.IsSfxEnabled;
            bool isNowEnabled = !wasEnabled;

            // Save the new state
            GameManager.Instance.UpdateSettings(isNowEnabled, GameManager.Instance.IsVibrationEnabled);

            // Apply mute logically (Mute is opposite of Enabled)
            AudioManager.Instance.SetMute(!isNowEnabled);

            UpdateSoundToggleUI();
        }
    }

    private void UpdateSoundToggleUI()
    {
        if (SoundToggleText != null && GameManager.Instance != null)
        {
            SoundToggleText.text = GameManager.Instance.IsSfxEnabled ? "사운드 켜짐" : "사운드 꺼짐";
        }
    }

    private void OnAdReviveClicked()
    {
        // Call BridgeManager to show ad
        if (BridgeManager.Instance != null)
        {
            BridgeManager.Instance.RequestAd();
        }
    }

    private void OnShareClicked()
    {
        // Take screenshot and share
        if (ResultCard.Instance != null)
        {
            ResultCard.Instance.CaptureAndShare();
        }
    }

    private void OnRestartClicked()
    {
        UnityEngine.SceneManagement.SceneManager.LoadScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
    }
}
