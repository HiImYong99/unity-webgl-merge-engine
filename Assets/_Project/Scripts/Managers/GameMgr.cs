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
        public SettingsModel settings;

        public int currentScore;
        public bool hasSavedGame;
        public List<AnimalSaveData> activeAnimals = new List<AnimalSaveData>();
    }

    [System.Serializable]
    private class AnimalSaveData
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
    public GameState CurrentState { get; private set; }
    public bool HasRevived { get; private set; } // Legacy - keeping for compat
    public bool AdWatched { get; private set; } = false;
    public int SpareLives { get; private set; } = 0;
    public bool SpeedBoostActive { get; private set; } = false;

    public string UserIdentifier { get; private set; }
    public bool IsLoggedIn { get; private set; }

    public bool IsSfxEnabled { get; private set; } = true;
    public bool IsVibrationEnabled { get; private set; } = true;

    // Fields
    private SaveDataModel _loadedSaveData;
    public AnimalEvolutionData EvolutionData;

    // ===== Fallout 시스템 (Kill Zone) =====
    private const float CONTAINER_MIN_X = -2.0f;  // 용기 좌측 경계 (외벽 포함)
    private const float CONTAINER_MAX_X = 2.0f;   // 용기 우측 경계 (외벽 포함)
    private const float KILL_ZONE_Y = 3.5f;       // 킬존 Y: 컨테이너 상단 높이. 이 위에서 X 범위 밖으로 나가면 게임오버
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

        // [Debug] 개발 중 게임오버 테스트를 위한 단축키 (End 키)
        if (Input.GetKeyDown(KeyCode.End))
        {
            TriggerGameOver();
            return;
        }

        // [Debug] 게임 속도 가속 (G 키)
        if (Input.GetKeyDown(KeyCode.G))
        {
            Time.timeScale = (Time.timeScale > 1.0f) ? 1.0f : 3.0f;
            Debug.Log($"[Debug] TimeScale: {Time.timeScale}");
        }

        // ===== Fallout 게임 오버 판정 =====
        // 용기 X 범위 밖으로 벗어난 동물이 킬존 아래로 떨어지면 게임오버
        GameObject[] animals = GameObject.FindGameObjectsWithTag("Animal");

        foreach (GameObject go in animals)
        {
            if (go == null) continue;

            Animal animal = go.GetComponent<Animal>();
            if (animal == null) continue;
            if (!animal.IsDropped) continue;
            if (animal.IsMerged) continue;

            float x = go.transform.position.x;
            float y = go.transform.position.y;

            // 용기 X 범위 밖으로 벗어난 동물이 킬존 위에 있으면 게임오버
            bool outsideX = x < CONTAINER_MIN_X || x > CONTAINER_MAX_X;
            if (outsideX && y > KILL_ZONE_Y)
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


        if (UIMgr.Instance != null)
            UIMgr.Instance.UpdateScore(Score);


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
        Time.timeScale = 1.0f; // 추가: 게임 시작 시 속도 초기화
        HasRevived = false;
        AdWatched = false;
        SpareLives = 0;
        SpeedBoostActive = false;
        _gameOverDetectionCooldown = 1.0f; // 시작 직후 이전 프레임 잔여 동물로 인한 오판 방지

        // 기존 동물 정리
        GameObject[] existingAnimals = GameObject.FindGameObjectsWithTag("Animal");
        foreach (GameObject go in existingAnimals) Destroy(go);

        if (SpawnMgr.Instance != null) SpawnMgr.Instance.FullReset();

        Score = 0;
        ClearSavedGame();

        if (UIMgr.Instance != null)
        {
            UIMgr.Instance.UpdateScore(Score);
            UIMgr.Instance.ShowLandingPage(false);
            UIMgr.Instance.ShowHUD(true);
        }

        if (SpawnMgr.Instance != null) SpawnMgr.Instance.PrepareNextAnimal();
    }

    public void TriggerGameOver()
    {
        if (CurrentState == GameState.GameOver) return;
        CurrentState = GameState.GameOver;

        // 낙하 쿨다운 중 대기중인 스폰 예약 취소
        if (SpawnMgr.Instance != null)
            SpawnMgr.Instance.CancelPendingSpawn();

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
            _loadedSaveData.activeAnimals.Clear();
            _loadedSaveData.currentScore = 0;
        }
    }

    /// <summary>
    /// 광고 시청 후 부활 처리
    /// </summary>
    private const int MAX_REVIVES = 2;

    /// <summary>
    /// 광고 시청 후 게임속도 2배 부스트 활성화 (이번 판 내내 유지)
    /// </summary>
    public void ActivateSpeedBoost()
    {
        if (CurrentState != GameState.Playing) return;
        if (SpeedBoostActive) return;

        SpeedBoostActive = true;
        Time.timeScale = 2.0f;

#if UNITY_WEBGL && !UNITY_EDITOR
        try { NotifySpeedBoostActivatedJS(); } catch { }
#endif
    }

    /// <summary>
    /// 영구 구매자 2배속 ON/OFF (JS에서 직접 호출)
    /// multiplier: 1f = 일반, 2f = 2배속
    /// </summary>
    public void SetSpeedMultiplier(float multiplier)
    {
        SpeedBoostActive = (multiplier > 1f);
        Time.timeScale = multiplier;
    }

#if UNITY_WEBGL && !UNITY_EDITOR
    [System.Runtime.InteropServices.DllImport("__Internal")]
    private static extern void notifySpeedBoostActivatedFromUnity();
    private static void NotifySpeedBoostActivatedJS() { notifySpeedBoostActivatedFromUnity(); }
#else
    private static void NotifySpeedBoostActivatedJS() { }
#endif

    public void Revive()
    {
        if (CurrentState != GameState.GameOver) return;
        if (SpareLives >= MAX_REVIVES) return;

        CurrentState = GameState.Playing;
        AdWatched = true;
        SpareLives++;

        _gameOverDetectionCooldown = 1.5f;
        ClearFallingAnimals();

        if (UIMgr.Instance != null) UIMgr.Instance.HideGameOver();
        if (SpawnMgr.Instance != null)
        {
            SpawnMgr.Instance.CanSpawn = true;
            SpawnMgr.Instance.PrepareNextAnimal();
        }
    }

    private void ClearFallingAnimals()
    {
        GameObject[] animals = GameObject.FindGameObjectsWithTag("Animal");
        foreach (GameObject go in animals)
        {
            if (go == null) continue;
            float x = go.transform.position.x;
            float y = go.transform.position.y;
            bool outside = x < CONTAINER_MIN_X || x > CONTAINER_MAX_X;

            if (outside || y > KILL_ZONE_Y + 1.0f || y < -3.5f)
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

                if (_loadedSaveData.settings != null)
                {
                    IsSfxEnabled = _loadedSaveData.settings.sfx;
                    IsVibrationEnabled = _loadedSaveData.settings.vibration;
                }
                return;
            }
        }

        HighScore = 0;
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
            settings = new SettingsModel { sfx = IsSfxEnabled, vibration = IsVibrationEnabled },
            hasSavedGame = true,
            currentScore = Score,
            activeAnimals = new List<AnimalSaveData>()
        };

        GameObject[] animals = GameObject.FindGameObjectsWithTag("Animal");
        foreach (GameObject go in animals)
        {
            Animal d = go.GetComponent<Animal>();
            if (d != null && d.IsDropped && !d.IsMerged)
            {
                Rigidbody2D rb = go.GetComponent<Rigidbody2D>();
                Vector2 vel = rb != null ? rb.velocity : Vector2.zero;

                data.activeAnimals.Add(new AnimalSaveData
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
