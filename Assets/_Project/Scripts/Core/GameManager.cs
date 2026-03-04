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
    public bool HasRevived { get; private set; } // Legacy - keeping for compat
    public bool AdWatched { get; private set; } = false;
    public int SpareLives { get; private set; } = 0;

    public string UserIdentifier { get; private set; }
    public bool IsLoggedIn { get; private set; }

    public bool IsSfxEnabled { get; private set; } = true;
    public bool IsVibrationEnabled { get; private set; } = true;

    private SaveDataModel loadedSaveData;

    public DessertEvolutionData EvolutionData;

    // ===== Fallout 시스템 (Kill Zone) =====
    // 타이머 방식을 제거하고, 오브젝트가 용기 수평 범위 밖으로 나가서
    // Kill Zone Y 임계값 아래로 떨어질 때만 게임 오버를 발생시킵니다.
    private const float CONTAINER_MIN_X = -2.0f;  // 용기 좌측 경계 (halfWidth 1.6 + 마진)
    private const float CONTAINER_MAX_X = 2.0f;   // 용기 우측 경계
    private const float KILL_ZONE_Y = -4f;         // 이 Y값 아래로 떨어지면 게임 오버

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

        // ===== Fallout 게임 오버 판정 =====
        // 오브젝트가 용기의 수평 범위(X축)를 벗어난 후
        // Kill Zone Y값 아래로 떨어질 때만 게임 오버를 발생시킵니다.
        // (용기 내부에서 쌓인 오브젝트는 판정 대상 아님)
        GameObject[] desserts = GameObject.FindGameObjectsWithTag("Dessert");

        foreach (GameObject go in desserts)
        {
            if (go == null) continue;

            Dessert dessert = go.GetComponent<Dessert>();
            if (dessert == null) continue;

            // 조작 중이거나 병합 중인 오브젝트 제외
            if (!dessert.IsDropped) continue;
            if (dessert.IsMerged) continue;

            float x = go.transform.position.x;
            float y = go.transform.position.y;

            // 용기 수평 범위 밖이고 Kill Zone 아래로 떨어진 경우
            bool outsideContainer = x < CONTAINER_MIN_X || x > CONTAINER_MAX_X;
            if (outsideContainer && y < KILL_ZONE_Y)
            {
                TriggerGameOver();
                return;
            }
        }
    }

    /// <summary>
    /// 점수 추가. Score += Tier × Multiplier 방식.
    /// </summary>
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
        AdWatched = false;
        SpareLives = 0;

        // 기존 디저트 정리
        GameObject[] existingDesserts = GameObject.FindGameObjectsWithTag("Dessert");
        foreach (GameObject go in existingDesserts) Destroy(go);

        if (SpawnManager.Instance != null) SpawnManager.Instance.FullReset();

        // 항상 빈 보드에서 시작 (스펙: Initial state is empty)
        Score = 0;
        ClearSavedGame();

        if (UIManager.Instance != null)
        {
            UIManager.Instance.UpdateScore(Score);
            UIManager.Instance.ShowLandingPage(false);
            UIManager.Instance.ShowHUD(true);
        }

        if (SpawnManager.Instance != null) SpawnManager.Instance.PrepareNextDessert();
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
                        dessertScript.Initialize(data.level, true);
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

        ClearSavedGame();
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
        // 광고 시청 후 호출됨
        if (AdWatched || CurrentState != GameState.GameOver) return;

        CurrentState = GameState.Playing;
        AdWatched = true;
        SpareLives = 1; // 첫 번째 복구는 지금 즉시 하고, 추가로 1회 더 가능

        ClearFallingDesserts();

        if (UIManager.Instance != null) UIManager.Instance.HideGameOver();
        if (SpawnManager.Instance != null) SpawnManager.Instance.CanSpawn = true;
    }

    public void UseSpareLife()
    {
        // 남은 여분의 생명 사용
        if (SpareLives <= 0 || CurrentState != GameState.GameOver) return;

        CurrentState = GameState.Playing;
        SpareLives--;

        ClearFallingDesserts();

        if (UIManager.Instance != null) UIManager.Instance.HideGameOver();
        if (SpawnManager.Instance != null) SpawnManager.Instance.CanSpawn = true;
    }

    private void ClearFallingDesserts()
    {
        GameObject[] desserts = GameObject.FindGameObjectsWithTag("Dessert");
        foreach (GameObject go in desserts)
        {
            // Kill Zone 아래로 떨어졌거나 너무 높이 쌓인(게임오버 유발) 오브젝트들 정리
            if (go.transform.position.y < KILL_ZONE_Y || go.transform.position.y > 4.5f)
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
        // 프리미엄 용기 디자인 적용 루틴 실행
        GameObject container = GameObject.Find("GameContainer");
        if (container != null && container.GetComponent<VesselVisualEnhancer>() == null)
            container.AddComponent<VesselVisualEnhancer>();

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
            if (!IsLoggedIn)
            {
                if (TossBridgeManager.Instance != null)
                    TossBridgeManager.Instance.RequestLogin();
                else if (BridgeManager.Instance != null)
                    BridgeManager.Instance.RequestAppLogin();
                else
                    UIManager.Instance.ShowLandingPage(true);
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
            UIManager.Instance.UpdateLoginStatus("토스 계정이 연결됐어요");
        }
    }

    public void UpdateSettings(bool sfx, bool vibration)
    {
        IsSfxEnabled = sfx;
        IsVibrationEnabled = vibration;
        
        if (AudioManager.Instance != null)
        {
            // BGM은 SFX 설정과 별개로 관리되거나, 필요에 따라 같이 처리
            AudioManager.Instance.SetSfxMute(!sfx);
        }
        SaveData();
    }

    public void ForcePlayBGM()
    {
        if (AudioManager.Instance != null && AudioManager.Instance.BGMSource != null)
        {
            if (AudioManager.Instance.BGMSource.clip != null && !AudioManager.Instance.BGMSource.isPlaying)
            {
                AudioManager.Instance.BGMSource.Play();
            }
        }
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

        loadedSaveData = data;
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
