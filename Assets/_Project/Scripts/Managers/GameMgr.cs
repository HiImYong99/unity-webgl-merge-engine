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
    public bool HasRevived { get; private set; }

    public string UserIdentifier { get; private set; }
    public bool IsLoggedIn { get; private set; }

    public bool IsBgmEnabled { get; private set; } = true;
    public bool IsSfxEnabled { get; private set; } = true;
    public bool IsVibrationEnabled { get; private set; } = true;

    // Fields
    private SaveDataModel _loadedSaveData;
    public AnimalEvolutionData EvolutionData;

    // ================================================================

    // ================================================================

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
        Time.timeScale = 1.0f;
        Time.fixedDeltaTime = 0.02f;
        HasRevived = false;
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
            BridgeMgr.Instance.SubmitLeaderboardScore(Score);

        ClearSavedGame();
        SaveData();

        if (UIMgr.Instance != null)
            UIMgr.Instance.ShowGameOver();
    }

    /// <summary>
    /// 광고 시청 후 게임 계속하기 (JS → SendMessage로 호출)
    /// 위험 구역 동물 제거 후 Playing 상태로 복귀
    /// </summary>
    public void ContinueGameAfterAd(string _)
    {
        if (CurrentState != GameState.GameOver) return;
        if (HasRevived) return; // 한 판에 한 번만 허용

        HasRevived = true;
        CurrentState = GameState.Playing;
        _gameOverDetectionCooldown = 2.0f; // 복귀 직후 즉시 게임오버 방지

        // 킬존 위의 동물 중 위험 구역(상단 30%) 동물만 제거
        float killY = GetKillZoneY();
        float dangerThreshold = killY * 0.7f; // 위험 구역 상단 기준
        for (int i = _activeAnimals.Count - 1; i >= 0; i--)
        {
            if (_activeAnimals[i] == null) { _activeAnimals.RemoveAt(i); continue; }
            if (_activeAnimals[i].transform.position.y > dangerThreshold)
            {
                Destroy(_activeAnimals[i].gameObject);
                _activeAnimals.RemoveAt(i);
            }
        }

        if (UIMgr.Instance != null)
        {
            UIMgr.Instance.ShowHUD(true);
        }

        if (SpawnMgr.Instance != null)
            SpawnMgr.Instance.PrepareNextAnimal();
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
