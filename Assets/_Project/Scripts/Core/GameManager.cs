using UnityEngine;
using System.Collections.Generic;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [System.Serializable]
    private class SaveDataModel
    {
        public int highScore;
        public int lastDiscoveryLevel;
        public SettingsModel settings;

        public int currentScore;
        public bool hasSavedGame;
        public List<DessertSaveData> activeDesserts = new List<DessertSaveData>();
    }

    [System.Serializable]
    private class DessertSaveData
    {
        public int level;
        public Vector2 position;
        public Vector2 velocity;
        public float rotation;
    }

    public enum GameState
    {
        Landing,
        Playing,
        GameOver
    }

    [System.Serializable]
    private class SettingsModel
    {
        public bool sfx;
        public bool vibration;
    }

    public int Score { get; private set; }
    public int HighScore { get; private set; }
    public int LastDiscoveryLevel { get; private set; }
    public GameState CurrentState { get; private set; }
    public bool HasRevived { get; private set; }

    public string UserIdentifier { get; private set; }
    public bool IsLoggedIn { get; private set; }

    public bool IsSfxEnabled { get; private set; } = true;
    public bool IsVibrationEnabled { get; private set; } = true;

    private SaveDataModel loadedSaveData;

    public DessertEvolutionData EvolutionData;

    private float maxDeadlineTimer = 0f;
    private const float DEADLINE_LIMIT = 3f;
    private const float DEADLINE_Y = 3.5f;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            LoadData();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Update()
    {
        if (CurrentState != GameState.Playing) return;

        bool isAnyOverDeadline = false;
        GameObject[] desserts = GameObject.FindGameObjectsWithTag("Dessert");

        foreach (GameObject go in desserts)
        {
            Dessert dessert = go.GetComponent<Dessert>();
            if (dessert != null && dessert.IsDropped)
            {
                if (go.transform.position.y >= DEADLINE_Y)
                {
                    isAnyOverDeadline = true;
                    break;
                }
            }
        }

        if (isAnyOverDeadline)
        {
            maxDeadlineTimer += Time.deltaTime;
            if (UIManager.Instance != null)
            {
                UIManager.Instance.ShowDeadlineWarning(true);
            }

            if (maxDeadlineTimer >= DEADLINE_LIMIT)
            {
                TriggerGameOver();
            }
        }
        else
        {
            maxDeadlineTimer = 0f;
            if (UIManager.Instance != null)
            {
                UIManager.Instance.ShowDeadlineWarning(false);
            }
        }
    }

    public void AddScore(int amount, int mergedLevel)
    {
        if (CurrentState != GameState.Playing) return;

        Score += amount;
        if (mergedLevel > LastDiscoveryLevel)
        {
            LastDiscoveryLevel = mergedLevel;
            SaveData();
        }

        if (UIManager.Instance != null)
        {
            UIManager.Instance.UpdateScore(Score);
        }
    }

    public void StartGame()
    {
        CurrentState = GameState.Playing;
        HasRevived = false;

        // Clean up any existing desserts first (if restarting)
        GameObject[] existingDesserts = GameObject.FindGameObjectsWithTag("Dessert");
        foreach (GameObject go in existingDesserts) Destroy(go);

        if (loadedSaveData != null && loadedSaveData.hasSavedGame)
        {
            Score = loadedSaveData.currentScore;
            RestoreSavedDesserts(loadedSaveData.activeDesserts);
        }
        else
        {
            Score = 0;
        }

        if (UIManager.Instance != null)
        {
            UIManager.Instance.UpdateScore(Score);
            UIManager.Instance.ShowLandingPage(false);
        }

        if (SpawnManager.Instance != null)
        {
            SpawnManager.Instance.PrepareNextDessert();
        }
    }

    private void RestoreSavedDesserts(List<DessertSaveData> savedDesserts)
    {
        if (savedDesserts == null || EvolutionData == null) return;

        foreach (var data in savedDesserts)
        {
            if (data.level > 0 && data.level <= EvolutionData.Levels.Length)
            {
                GameObject prefab = EvolutionData.Levels[data.level - 1].Prefab;
                if (prefab != null)
                {
                    GameObject newDessert = Instantiate(prefab, data.position, Quaternion.Euler(0, 0, data.rotation));
                    Dessert dessertScript = newDessert.GetComponent<Dessert>();
                    if (dessertScript != null)
                    {
                        dessertScript.Initialize(data.level, true); // True because it's already dropped/active in the playfield
                    }
                    Rigidbody2D rb = newDessert.GetComponent<Rigidbody2D>();
                    if (rb != null)
                    {
                        rb.velocity = data.velocity;
                    }
                }
            }
        }
    }

    public void TriggerGameOver()
    {
        if (CurrentState == GameState.GameOver) return;
        CurrentState = GameState.GameOver;

        if (Score > HighScore)
        {
            HighScore = Score;
        }

        ClearSavedGame(); // Delete saved state since game is over
        SaveData();

        if (UIManager.Instance != null)
        {
            UIManager.Instance.ShowGameOver();
        }
    }

    public void ClearSavedGame()
    {
        if (loadedSaveData != null)
        {
            loadedSaveData.hasSavedGame = false;
            loadedSaveData.activeDesserts.Clear();
            loadedSaveData.currentScore = 0;
        }
    }

    public void Revive()
    {
        if (HasRevived || CurrentState != GameState.GameOver) return; // 1-time Undo limit

        CurrentState = GameState.Playing;
        HasRevived = true;

        // Clean up items near deadline
        GameObject[] desserts = GameObject.FindGameObjectsWithTag("Dessert");
        foreach (GameObject go in desserts)
        {
            if (go.transform.position.y > 3.0f) // Adjust magic number 3.0f based on actual deadline Y
            {
                Destroy(go);
            }
        }

        if (UIManager.Instance != null)
        {
            UIManager.Instance.HideGameOver();
        }

        if (SpawnManager.Instance != null)
        {
            SpawnManager.Instance.CanSpawn = true;
        }
    }

    private void LoadData()
    {
        if (PlayerPrefs.HasKey("GameData"))
        {
            string json = PlayerPrefs.GetString("GameData");
            loadedSaveData = JsonUtility.FromJson<SaveDataModel>(json);
            if (loadedSaveData != null)
            {
                HighScore = loadedSaveData.highScore;
                LastDiscoveryLevel = loadedSaveData.lastDiscoveryLevel;

                if (loadedSaveData.settings != null)
                {
                    IsSfxEnabled = loadedSaveData.settings.sfx;
                    IsVibrationEnabled = loadedSaveData.settings.vibration;
                }
                return;
            }
        }

        HighScore = 0;
        LastDiscoveryLevel = 1;
        IsSfxEnabled = true;
        IsVibrationEnabled = true;
    }

    private void Start()
    {
        Score = 0;
        CurrentState = GameState.Landing;
        HasRevived = false;

        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.SetMute(!IsSfxEnabled);
        }

        if (UIManager.Instance != null)
        {
            UIManager.Instance.UpdateScore(Score);
            // Don't show landing page immediately, wait for login to finish or timeout
            // For MVP purposes, trigger login here.
            if (BridgeManager.Instance != null && !IsLoggedIn)
            {
                BridgeManager.Instance.RequestAppLogin();
            }
            else
            {
                UIManager.Instance.ShowLandingPage(true);
            }
        }
    }

    public void OnUserLogin(string userKey)
    {
        IsLoggedIn = true;
        UserIdentifier = userKey;

        if (UIManager.Instance != null && CurrentState == GameState.Landing)
        {
            UIManager.Instance.ShowLandingPage(true);
            UIManager.Instance.UpdateLoginStatus("토스 계정 연결 완료");
        }
    }

    public void UpdateSettings(bool sfx, bool vibration)
    {
        IsSfxEnabled = sfx;
        IsVibrationEnabled = vibration;
        SaveData();
    }

    public void SaveCurrentBoardState()
    {
        if (CurrentState != GameState.Playing) return;

        SaveDataModel data = new SaveDataModel
        {
            highScore = HighScore,
            lastDiscoveryLevel = LastDiscoveryLevel,
            settings = new SettingsModel { sfx = IsSfxEnabled, vibration = IsVibrationEnabled },
            hasSavedGame = true,
            currentScore = Score,
            activeDesserts = new List<DessertSaveData>()
        };

        GameObject[] desserts = GameObject.FindGameObjectsWithTag("Dessert");
        foreach (GameObject go in desserts)
        {
            Dessert d = go.GetComponent<Dessert>();
            if (d != null && d.IsDropped && !d.IsMerged)
            {
                Rigidbody2D rb = go.GetComponent<Rigidbody2D>();
                Vector2 vel = rb != null ? rb.velocity : Vector2.zero;

                data.activeDesserts.Add(new DessertSaveData
                {
                    level = d.Level,
                    position = go.transform.position,
                    velocity = vel,
                    rotation = go.transform.eulerAngles.z
                });
            }
        }

        loadedSaveData = data; // Keep a reference
        string json = JsonUtility.ToJson(data);
        PlayerPrefs.SetString("GameData", json);
        PlayerPrefs.Save();
    }

    private void SaveData()
    {
        SaveDataModel data = loadedSaveData ?? new SaveDataModel();

        data.highScore = HighScore;
        data.lastDiscoveryLevel = LastDiscoveryLevel;
        data.settings = new SettingsModel { sfx = IsSfxEnabled, vibration = IsVibrationEnabled };
        // currentScore and hasSavedGame and activeDesserts properties remain whatever they were.

        string json = JsonUtility.ToJson(data);
        PlayerPrefs.SetString("GameData", json);
        PlayerPrefs.Save();
    }

    private void OnApplicationPause(bool pauseStatus)
    {
        if (pauseStatus && CurrentState == GameState.Playing)
        {
            SaveCurrentBoardState();
        }
    }

    private void OnApplicationQuit()
    {
        if (CurrentState == GameState.Playing)
        {
            SaveCurrentBoardState();
        }
    }
}
