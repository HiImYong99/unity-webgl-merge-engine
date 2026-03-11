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
        public bool bgm;
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

    public bool IsBgmEnabled { get; private set; } = true;
    public bool IsSfxEnabled { get; private set; } = true;
    public bool IsVibrationEnabled { get; private set; } = true;

    // Fields
    private SaveDataModel _loadedSaveData;
    public AnimalEvolutionData EvolutionData;

    // ===== Fallout 시스템 (Kill Zone) =====
    private const float CONTAINER_MIN_X = -2.0f;  // 용기 좌측 경계 (외벽 포함)
    private const float CONTAINER_MAX_X = 2.0f;   // 용기 우측 경계 (외벽 포함)
    private float GetKillZoneY()
    {
        if (Camera.main != null)
        {
            // 사용자의 디바이스 화면 비율/안전 영역에 따라 변경되는 카메라의 상단을 게임오버 라인 기준으로 잡음
            return Camera.main.ViewportToWorldPoint(new Vector3(0.5f, 1f, 0f)).y - 0.5f;
        }
        return 3.5f;
    }
    private float _gameOverDetectionCooldown = 0f; // 복구 후 즉시 게임오버 방지용
    private List<Animal> _activeAnimals = new List<Animal>();

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
        for (int i = _activeAnimals.Count - 1; i >= 0; i--)
        {
            Animal animal = _activeAnimals[i];
            if (animal == null)
            {
                _activeAnimals.RemoveAt(i);
                continue;
            }

            if (!animal.IsDropped || animal.IsMerged) continue;

            float x = animal.transform.position.x;
            float y = animal.transform.position.y;

            // 용기 X 범위 밖으로 벗어난 동물은 무조건 게임오버 (떨어져내리는 중이어도 판정되도록 강제)
            bool outsideX = x < CONTAINER_MIN_X || x > CONTAINER_MAX_X;
            if (outsideX)
            {
                TriggerGameOver();
                return;
            }
        }
    }

    public void RegisterAnimal(Animal animal)
    {
        if (!_activeAnimals.Contains(animal))
            _activeAnimals.Add(animal);
    }

    public void UnregisterAnimal(Animal animal)
    {
        if (_activeAnimals.Contains(animal))
            _activeAnimals.Remove(animal);
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

        // 최적화: 매번 저장하지 않고 일정 간격(예: 10회 병합마다)으로 저장
        // (머지 레벨이 0보다 크면 병합에 의한 호출임을 의미)
        if (mergedLevel > 0)
        {
            _mergeCountForSave++;
            if (_mergeCountForSave >= 10)
            {
                _mergeCountForSave = 0;
                SaveCurrentBoardState();
            }
        }
    }

    private int _mergeCountForSave = 0;

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
        // [수정] 게임 시작 시 SpeedBoostActive 상태에 따라 TimeScale 유지
        // 단, 세션(광고) 활성화라면 여기서 초기화할지 선택. 
        // 영구 구매 상태(_premiumSpeedOn)가 JS에서 관리되므로, 여기서는 현재의 SpeedBoostActive 상태를 유지합니다.
        // 만약 광고 보상형이 '이번 판만'이라면 아래 SpeedBoostActive = false를 유지하되, 
        // permanent 체크 로직이 필요함. 일단은 사용자가 명시적으로 'OFF'하기 전까지는 유지하도록 변경.
        
        SetSpeedMultiplier(SpeedBoostActive ? 2.0f : 1.0f);
        
        HasRevived = false;
        AdWatched = false;
        SpareLives = 0;
        // SpeedBoostActive = false; // [제거] 게임 재시작 시 속도 초기화 방지
        _gameOverDetectionCooldown = 1.0f;

        // 기존 동물 정리
        for (int i = _activeAnimals.Count - 1; i >= 0; i--)
        {
            if (_activeAnimals[i] != null) Destroy(_activeAnimals[i].gameObject);
        }
        _activeAnimals.Clear();

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

        if (BridgeMgr.Instance != null)
        {
            BridgeMgr.Instance.SubmitLeaderboardScore(Score);
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

        SetSpeedMultiplier(2.0f);

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
        SpeedBoostActive = (multiplier > 1.1f);
        Time.timeScale = multiplier;
        // 2배속 시 물리가 튀는 것을 방지하기 위해 fixedDeltaTime을 0.02f로 고정하거나 0.01f로 낮춰 정밀도 향상
        Time.fixedDeltaTime = 0.015f; // 약간 더 정밀하게 설정
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
        // [수정] 부활 시에도 배속 상태 유지
        Time.timeScale = SpeedBoostActive ? 2.0f : 1.0f;
        Time.fixedDeltaTime = 0.02f;
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
        for (int i = _activeAnimals.Count - 1; i >= 0; i--)
        {
            Animal animal = _activeAnimals[i];
            if (animal == null) continue;

            float x = animal.transform.position.x;
            float y = animal.transform.position.y;
            bool outside = x < CONTAINER_MIN_X || x > CONTAINER_MAX_X;
            float killZone = GetKillZoneY();

            if (outside || y > killZone + 1.0f || y < -3.5f)
            {
                Destroy(animal.gameObject);
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
                    IsBgmEnabled = _loadedSaveData.settings.bgm;
                    IsSfxEnabled = _loadedSaveData.settings.sfx;
                    IsVibrationEnabled = _loadedSaveData.settings.vibration;
                }
                return;
            }
        }

        HighScore = 0;
        IsBgmEnabled = true;
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
            SoundMgr.Instance.SetMute(!IsBgmEnabled);
            SoundMgr.Instance.SetSfxMute(!IsSfxEnabled);
        }

        if (UIMgr.Instance != null)
        {
            UIMgr.Instance.UpdateScore(Score);
            if (!IsLoggedIn)
            {
                if (BridgeMgr.Instance != null)
                BridgeMgr.Instance.RequestLogin();
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

    public void UpdateSettings(bool bgm, bool sfx, bool vibration)
    {
        IsBgmEnabled = bgm;
        IsSfxEnabled = sfx;
        IsVibrationEnabled = vibration;
        
        if (SoundMgr.Instance != null)
        {
            SoundMgr.Instance.SetMute(!bgm);
            SoundMgr.Instance.SetSfxMute(!sfx);
        }
        SaveData();
    }

    // JS SendMessage 전용 (SoundMgr에서 이관하거나 중복 호출 방지)
    public void SetBgmMuteFromJS(string mute)
    {
        bool enabled = (mute == "0");
        if (IsBgmEnabled == enabled) return;
        UpdateSettings(enabled, IsSfxEnabled, IsVibrationEnabled);
    }

    public void SetSfxMuteFromJS(string mute)
    {
        bool enabled = (mute == "0");
        if (IsSfxEnabled == enabled) return;
        UpdateSettings(IsBgmEnabled, enabled, IsVibrationEnabled);
    }

    public void RequestSaveFromJS()
    {
        // JS 브릿지에서 visibilitychange/pagehide 시 호출됨
        if (CurrentState == GameState.Playing)
        {
            Debug.Log("[GameMgr] RequestSaveFromJS: Saving current board state...");
            SaveCurrentBoardState();
        }
        else
        {
            SaveData();
        }
    }

    public void SaveCurrentBoardState()
    {
        if (CurrentState != GameState.Playing) return;

        SaveDataModel data = new SaveDataModel
        {
            highScore = HighScore,
            settings = new SettingsModel { bgm = IsBgmEnabled, sfx = IsSfxEnabled, vibration = IsVibrationEnabled },
            hasSavedGame = true,
            currentScore = Score,
            activeAnimals = new List<AnimalSaveData>()
        };

        foreach (Animal d in _activeAnimals)
        {
            if (d != null && d.IsDropped && !d.IsMerged)
            {
                Rigidbody2D rb = d.GetComponent<Rigidbody2D>();
                Vector2 vel = rb != null ? rb.velocity : Vector2.zero;

                data.activeAnimals.Add(new AnimalSaveData
                {
                    level = d.Level,
                    position = d.transform.position,
                    velocity = vel,
                    rotation = d.transform.eulerAngles.z
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
        data.settings = new SettingsModel { bgm = IsBgmEnabled, sfx = IsSfxEnabled, vibration = IsVibrationEnabled };

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
