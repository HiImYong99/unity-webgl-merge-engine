using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 게임의 전반적인 상태와 스코어, 데이터를 관리하는 핵심 매니저
/// </summary>
public class GameMgr : MonoBehaviour
{
    public static GameMgr Instance { get; private set; }

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

    // Properties
    public int Score { get; private set; }
    public int HighScore { get; private set; }
    public int LastDiscoveryLevel { get; private set; }
    public GameState CurrentState { get; private set; }
    public bool HasRevived { get; private set; } // Legacy - keeping for compat
    public bool AdWatched { get; private set; } = false;
    public int SpareLives { get; private set; } = 0;

    public string UserIdentifier { get; private set; }
    public bool IsLoggedIn { get; private set; }

    public bool IsSfxEnabled { get; private set; } = true;
    public bool IsVibrationEnabled { get; private set; } = true;

    // Fields
    private SaveDataModel _loadedSaveData;
    public DessertEvolutionData EvolutionData;

    // ===== Fallout 시스템 (Kill Zone) =====
    private const float CONTAINER_MIN_X = -2.0f;  // 용기 좌측 경계
    private const float CONTAINER_MAX_X = 2.0f;   // 용기 우측 경계
    private const float KILL_ZONE_Y = -4f;         // 이 Y값 아래로 떨어지면 게임 오버
    private float _gameOverDetectionCooldown = 0f; // 복구 후 즉시 게임오버 방지용

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
        if (_gameOverDetectionCooldown > 0)
        {
            _gameOverDetectionCooldown -= Time.deltaTime;
            return;
        }

        if (CurrentState != GameState.Playing) return;

        // ===== Fallout 게임 오버 판정 =====
        // 용기 밖으로 떨어지는 디저트 감지
        GameObject[] desserts = GameObject.FindGameObjectsWithTag("Dessert");

        foreach (GameObject go in desserts)
        {
            if (go == null) continue;

            Dessert dessert = go.GetComponent<Dessert>();
            if (dessert == null) continue;

            if (!dessert.IsDropped) continue;
            if (dessert.IsMerged) continue;

            float x = go.transform.position.x;
            float y = go.transform.position.y;

            // 용기 외부 임계값 체크
            bool outsideContainer = x < CONTAINER_MIN_X || x > CONTAINER_MAX_X;
            if (outsideContainer && y < KILL_ZONE_Y)
            {
                TriggerGameOver();
                return;
            }
        }
    }

    /// <summary>
    /// 점수 추가 및 새로운 디저트 발견 처리
    /// </summary>
    public void AddScore(int amount, int mergedLevel)
    {
        if (CurrentState != GameState.Playing) return;

        Score += amount;

        bool newDiscovery = false;
        if (mergedLevel > LastDiscoveryLevel)
        {
            LastDiscoveryLevel = mergedLevel;
            newDiscovery = true;
            SaveData();
        }

        if (UIMgr.Instance != null)
            UIMgr.Instance.UpdateScore(Score);

        if (newDiscovery && UIMgr.Instance != null)
            UIMgr.Instance.NotifyDessertDiscovered(mergedLevel);

        if (Score > HighScore)
        {
            HighScore = Score;
            NotifyNewHighScore(Score);
        }
    }

#if UNITY_WEBGL && !UNITY_EDITOR
    [System.Runtime.InteropServices.DllImport("__Internal")]
    private static extern void notifyNewHighScoreFromUnity(int score);
    private static void NotifyNewHighScore(int score)
    {
        try { notifyNewHighScoreFromUnity(score); } catch { }
    }
#else
    private static void NotifyNewHighScore(int score) { }
#endif

    /// <summary>
    /// 게임 시작 및 초기화
    /// </summary>
    public void StartGame()
    {
        CurrentState = GameState.Playing;
        HasRevived = false;
        AdWatched = false;
        SpareLives = 0;

        // 기존 디저트 정리
        GameObject[] existingDesserts = GameObject.FindGameObjectsWithTag("Dessert");
        foreach (GameObject go in existingDesserts) Destroy(go);

        if (SpawnMgr.Instance != null) SpawnMgr.Instance.FullReset();

        Score = 0;
        ClearSavedGame();

        if (UIMgr.Instance != null)
        {
            UIMgr.Instance.UpdateScore(Score);
            UIMgr.Instance.ShowLandingPage(false);
            UIMgr.Instance.ShowHUD(true);
        }

        if (SpawnMgr.Instance != null) SpawnMgr.Instance.PrepareNextDessert();
    }

    public void TriggerGameOver()
    {
        if (CurrentState == GameState.GameOver) return;
        CurrentState = GameState.GameOver;

        if (Score > HighScore)
        {
            HighScore = Score;
        }

        ClearSavedGame();
        SaveData();

        if (UIMgr.Instance != null)
        {
            UIMgr.Instance.ShowGameOver();
        }
    }

    public void ClearSavedGame()
    {
        if (_loadedSaveData != null)
        {
            _loadedSaveData.hasSavedGame = false;
            _loadedSaveData.activeDesserts.Clear();
            _loadedSaveData.currentScore = 0;
        }
    }

    /// <summary>
    /// 광고 시청 후 부활 처리
    /// </summary>
    private const int MAX_REVIVES = 2;

    public void Revive()
    {
        if (CurrentState != GameState.GameOver) return;
        if (SpareLives >= MAX_REVIVES) return;

        CurrentState = GameState.Playing;
        AdWatched = true;
        SpareLives++;

        _gameOverDetectionCooldown = 1.5f;
        ClearFallingDesserts();

        if (UIMgr.Instance != null) UIMgr.Instance.HideGameOver();
        if (SpawnMgr.Instance != null)
        {
            SpawnMgr.Instance.CanSpawn = true;
            SpawnMgr.Instance.PrepareNextDessert();
        }
    }

    private void ClearFallingDesserts()
    {
        GameObject[] desserts = GameObject.FindGameObjectsWithTag("Dessert");
        foreach (GameObject go in desserts)
        {
            if (go == null) continue;
            float x = go.transform.position.x;
            float y = go.transform.position.y;
            bool outside = x < CONTAINER_MIN_X || x > CONTAINER_MAX_X;
            
            if (outside || y > 4.1f || y < KILL_ZONE_Y + 0.5f)
            {
                Destroy(go);
            }
        }
    }

    private void LoadData()
    {
        if (PlayerPrefs.HasKey("GameData"))
        {
            string json = PlayerPrefs.GetString("GameData");
            _loadedSaveData = JsonUtility.FromJson<SaveDataModel>(json);
            if (_loadedSaveData != null)
            {
                HighScore = _loadedSaveData.highScore;
                LastDiscoveryLevel = _loadedSaveData.lastDiscoveryLevel;

                if (_loadedSaveData.settings != null)
                {
                    IsSfxEnabled = _loadedSaveData.settings.sfx;
                    IsVibrationEnabled = _loadedSaveData.settings.vibration;
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
        // 프리미엄 용기 디자인 적용 루틴
        GameObject container = GameObject.Find("GameContainer");
        if (container != null && container.GetComponent<VesselVisualEnhancer>() == null)
            container.AddComponent<VesselVisualEnhancer>();

        Score = 0;
        CurrentState = GameState.Landing;
        HasRevived = false;

        if (SoundMgr.Instance != null)
        {
            SoundMgr.Instance.SetMute(!IsSfxEnabled);
        }

        if (UIMgr.Instance != null)
        {
            UIMgr.Instance.UpdateScore(Score);
            if (!IsLoggedIn)
            {
                if (TossBridgeMgr.Instance != null)
                    TossBridgeMgr.Instance.RequestLogin();
                else
                    UIMgr.Instance.ShowLandingPage(true);
            }
            else
            {
                UIMgr.Instance.ShowLandingPage(true);
            }
        }
    }

    public void OnUserLogin(string userKey)
    {
        IsLoggedIn = true;
        UserIdentifier = userKey;

        if (UIMgr.Instance != null && CurrentState == GameState.Landing)
        {
            UIMgr.Instance.ShowLandingPage(true);
            UIMgr.Instance.UpdateLoginStatus("토스 계정이 연결됐어요");
        }
    }

    public void UpdateSettings(bool sfx, bool vibration)
    {
        IsSfxEnabled = sfx;
        IsVibrationEnabled = vibration;
        
        if (SoundMgr.Instance != null)
        {
            SoundMgr.Instance.SetSfxMute(!sfx);
        }
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

        _loadedSaveData = data;
        string json = JsonUtility.ToJson(data);
        PlayerPrefs.SetString("GameData", json);
        PlayerPrefs.Save();
    }

    public void ForcePlayBGM()
    {
        if (SoundMgr.Instance != null) SoundMgr.Instance.ForcePlayBGM();
    }

    public void SaveData()
    {
        SaveDataModel data = _loadedSaveData ?? new SaveDataModel();

        data.highScore = HighScore;
        data.lastDiscoveryLevel = LastDiscoveryLevel;
        data.settings = new SettingsModel { sfx = IsSfxEnabled, vibration = IsVibrationEnabled };

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
