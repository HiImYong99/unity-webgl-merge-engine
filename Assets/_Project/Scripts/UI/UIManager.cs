using UnityEngine;
using UnityEngine.UI;

public class UIManager : MonoBehaviour
{
    public static UIManager Instance { get; private set; }

    [Header("UI Panels")]
    public GameObject GameOverPanel;
    public GameObject DeadlineWarningPanel;
    public GameObject EvolutionEncyclopediaPanel; // 진화 도감 UI
    public Text ScoreText;
    public Text HighScoreText;
    public Text NextGuideText;
    public Button AdReviveButton;
    public Button ShareButton;
    public Button RestartButton;
    public Button OpenEncyclopediaButton;
    public Button CloseEncyclopediaButton;

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

        if (AdReviveButton != null) AdReviveButton.onClick.AddListener(OnAdReviveClicked);
        if (ShareButton != null) ShareButton.onClick.AddListener(OnShareClicked);
        if (RestartButton != null) RestartButton.onClick.AddListener(OnRestartClicked);
        if (OpenEncyclopediaButton != null) OpenEncyclopediaButton.onClick.AddListener(OnOpenEncyclopediaClicked);
        if (CloseEncyclopediaButton != null) CloseEncyclopediaButton.onClick.AddListener(OnCloseEncyclopediaClicked);
    }

    public void UpdateScore(int score)
    {
        if (ScoreText != null)
        {
            ScoreText.text = "Score: " + score;
        }
    }

    public void UpdateNextGuide(int nextLevel)
    {
        if (NextGuideText != null)
        {
            NextGuideText.text = "Next: Level " + nextLevel;
        }
    }

    public void ShowGameOver()
    {
        if (GameOverPanel != null) GameOverPanel.SetActive(true);
        if (HighScoreText != null && GameManager.Instance != null)
        {
            HighScoreText.text = "High Score: " + GameManager.Instance.HighScore;
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
