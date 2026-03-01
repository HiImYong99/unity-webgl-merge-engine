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
    public bool IsGameOver { get; private set; }
    public bool HasRevived { get; private set; }

    public bool IsSfxEnabled { get; private set; } = true;
    public bool IsVibrationEnabled { get; private set; } = true;

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
        if (IsGameOver) return;

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
        if (IsGameOver) return;

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

    public void TriggerGameOver()
    {
        if (IsGameOver) return;
        IsGameOver = true;

        if (Score > HighScore)
        {
            HighScore = Score;
            SaveData();
        }

        if (UIManager.Instance != null)
        {
            UIManager.Instance.ShowGameOver();
        }
    }

    public void Revive()
    {
        if (HasRevived) return; // 1-time Undo limit

        IsGameOver = false;
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
            SaveDataModel data = JsonUtility.FromJson<SaveDataModel>(json);
            if (data != null)
            {
                HighScore = data.highScore;
                LastDiscoveryLevel = data.lastDiscoveryLevel;

                if (data.settings != null)
                {
                    IsSfxEnabled = data.settings.sfx;
                    IsVibrationEnabled = data.settings.vibration;
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
        IsGameOver = false;
        HasRevived = false;

        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.SetMute(!IsSfxEnabled);
        }

        if (UIManager.Instance != null)
        {
            UIManager.Instance.UpdateScore(Score);
        }
    }

    public void UpdateSettings(bool sfx, bool vibration)
    {
        IsSfxEnabled = sfx;
        IsVibrationEnabled = vibration;
        SaveData();
    }

    private void SaveData()
    {
        SaveDataModel data = new SaveDataModel
        {
            highScore = HighScore,
            lastDiscoveryLevel = LastDiscoveryLevel,
            settings = new SettingsModel { sfx = IsSfxEnabled, vibration = IsVibrationEnabled }
        };

        string json = JsonUtility.ToJson(data);
        PlayerPrefs.SetString("GameData", json);
        PlayerPrefs.Save();
    }
}
