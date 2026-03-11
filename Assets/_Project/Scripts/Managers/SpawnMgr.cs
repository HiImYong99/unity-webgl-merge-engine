using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 동물 스폰과 입력(드롭) 처리를 담당하는 매니저
/// </summary>
public class SpawnMgr : MonoBehaviour
{
    public static SpawnMgr Instance { get; private set; }

    public Transform SpawnPoint;
    public float SpawnCooldown = 0.85f;
    public bool CanSpawn = true;

    private GameObject _currentAnimal;
    private AnimalEvolutionData _evolutionData;

    // Next Queue 시스템
    private int _nextAnimalLevel = -1;
    private int _queuedAnimalLevel = -1;

    private float _minX = -1.0f;  // 내벽 -1.6에서 가장자리 여유 0.6 (Lv3~4 벽 끼임 방지)
    private float _maxX = 1.0f;   // 내벽 +1.6에서 가장자리 여유 0.6

    // 난이도 조절
    private int _totalDropCount = 0;
    private const int DIFFICULTY_THRESHOLD = 30;

    // 가중치 랜덤 스폰 확률
    private static readonly float[] SPAWN_WEIGHTS_EARLY = { 25f, 25f, 20f, 20f, 10f };
    private static readonly float[] SPAWN_WEIGHTS_LATE = { 15f, 15f, 28f, 28f, 14f };

    private const int SPAWN_MIN_LEVEL = 1;
    private const int SPAWN_MAX_LEVEL = Animal.MAX_SPAWN_LEVEL;

    [Header("Dynamic Spawn Point")]
    public LayerMask AnimalLayer;
    private float _spawnBaseY = 8.0f;
    private const float SPAWN_MAX_Y = 10.0f;
    private const float DANGER_ZONE_H = 1.6f;
    private const float DANGER_ZONE_W = 3.4f;
    private const float SPAWN_RISE_STEP = 0.8f;
    private const float SPAWN_DROP_SPEED = 2.0f;
    private float _dynamicSpawnY;

    // Guide Line (점선 방식: 여러 개의 작은 원 스프라이트)
    private const int GUIDE_DOT_COUNT = 12;
    private GameObject[] _guideDots;
    private SpriteRenderer[] _guideDotSrs;

    // 위험 구역 감지
    private bool _isDangerActive = false;
    private float _dangerTimer = 0f;
    private const float DANGER_NOTIFY_DELAY = 2.0f;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    private void Start()
    {
        if (GameMgr.Instance != null)
            _evolutionData = GameMgr.Instance.EvolutionData;

        if (SpawnPoint != null)
            _spawnBaseY = SpawnPoint.position.y;
        _dynamicSpawnY = _spawnBaseY;

        Co_SetupGuideLine();
    }

    private void Co_SetupGuideLine()
    {
        // 점선: 작은 원 오브젝트 배열로 구성
        Sprite dotSprite = CreateCircleSprite(32);
        _guideDots = new GameObject[GUIDE_DOT_COUNT];
        _guideDotSrs = new SpriteRenderer[GUIDE_DOT_COUNT];

        for (int i = 0; i < GUIDE_DOT_COUNT; i++)
        {
            GameObject dot = new GameObject($"GuideDot_{i}");
            dot.transform.SetParent(transform);
            SpriteRenderer sr = dot.AddComponent<SpriteRenderer>();
            sr.sprite = dotSprite;
            sr.sortingOrder = 50;
            dot.SetActive(false);
            _guideDots[i] = dot;
            _guideDotSrs[i] = sr;
        }
    }

    private void UpdateGuideLine(float xPos)
    {
        if (_guideDots == null) return;

        bool show = (_currentAnimal != null && CanSpawn);
        if (!show)
        {
            SetGuideDotsActive(false);
            return;
        }

        float topY = _dynamicSpawnY - 0.35f; // 동물 바로 아래부터 시작
        float botY = -4.0f;

        // 현재 동물의 충돌 반경 구하기
        float animalRadius = 0.5f;
        var col2d = _currentAnimal.GetComponent<CircleCollider2D>();
        if (col2d != null) animalRadius = col2d.radius * _currentAnimal.transform.localScale.x;

        // CircleCast로 정확한 낙하 위치 계산
        RaycastHit2D[] hits = Physics2D.CircleCastAll(new Vector2(xPos, topY), animalRadius * 0.95f, Vector2.down, 15f);
        foreach (var h in hits)
        {
            if (h.collider != null && h.collider.gameObject != _currentAnimal && !h.collider.isTrigger)
            {
                botY = topY - h.distance;
                break;
            }
        }

        // 점선 렌더링: 위에서 아래로 갈수록 크기와 알파가 줄어듦
        float totalDist = topY - botY;
        for (int i = 0; i < GUIDE_DOT_COUNT; i++)
        {
            float t = (float)i / (GUIDE_DOT_COUNT - 1);
            float y = Mathf.Lerp(topY, botY, t);

            // 위→아래 거리에 비례해 실제 간격 내 도착 범위만 표시
            if (y < botY - 0.1f)
            {
                _guideDots[i].SetActive(false);
                continue;
            }

            // 크기: 상단 0.055 → 하단 0.025 (자연스럽게 수렴)
            float dotSize = Mathf.Lerp(0.055f, 0.025f, t);
            // 알파: 상단 0.75 → 하단 0.15
            float alpha = Mathf.Lerp(0.75f, 0.15f, t);
            // 색상: 위쪽은 연보라, 아래쪽은 하늘색
            Color col = Color.Lerp(new Color(0.75f, 0.55f, 0.95f, alpha), new Color(0.55f, 0.75f, 1f, alpha), t);

            _guideDots[i].SetActive(true);
            _guideDots[i].transform.position = new Vector3(xPos, y, -0.1f);
            _guideDots[i].transform.localScale = Vector3.one * dotSize;
            _guideDotSrs[i].color = col;
        }
    }

    private void SetGuideDotsActive(bool active)
    {
        if (_guideDots == null) return;
        for (int i = 0; i < GUIDE_DOT_COUNT; i++)
            if (_guideDots[i] != null) _guideDots[i].SetActive(active);
    }

    private void LateUpdate()
    {
        if (SpawnPoint == null) return;

        Vector2 dangerCenter = new Vector2(
            SpawnPoint.position.x,
            _dynamicSpawnY - DANGER_ZONE_H * 0.5f
        );

        bool danger = Physics2D.OverlapBox(
            dangerCenter,
            new Vector2(DANGER_ZONE_W, DANGER_ZONE_H),
            0f,
            AnimalLayer
        ) != null;

        if (danger)
        {
            _dynamicSpawnY = Mathf.Min(_dynamicSpawnY + SPAWN_RISE_STEP, SPAWN_MAX_Y);
            _dangerTimer += Time.deltaTime;
            if (!_isDangerActive && _dangerTimer >= DANGER_NOTIFY_DELAY)
            {
                _isDangerActive = true;
                NotifyDangerZone(true);
            }
        }
        else
        {
            _dynamicSpawnY = Mathf.MoveTowards(_dynamicSpawnY, _spawnBaseY, SPAWN_DROP_SPEED * Time.deltaTime);
            _dangerTimer = 0f;
            if (_isDangerActive)
            {
                _isDangerActive = false;
                NotifyDangerZone(false);
            }
        }

        Vector3 sp = SpawnPoint.position;
        SpawnPoint.position = new Vector3(sp.x, _dynamicSpawnY, sp.z);

        if (_currentAnimal != null)
        {
            Vector3 dp = _currentAnimal.transform.position;
            _currentAnimal.transform.position = new Vector3(dp.x, _dynamicSpawnY, dp.z);
        }
    }

    private static void NotifyDangerZone(bool active)
    {
        if (BridgeMgr.Instance != null) BridgeMgr.Instance.NotifyDanger(active);
    }

    private void Update()
    {
        if (GameMgr.Instance != null && GameMgr.Instance.CurrentState != GameMgr.GameState.Playing)
        {
            SetGuideDotsActive(false);
            return;
        }

        if (!CanSpawn || _currentAnimal == null)
        {
            SetGuideDotsActive(false);
            return;
        }

        // [Debug] 현재 동물을 거대 동물(Lv 10)로 즉시 교체 (T 키)
        if (Input.GetKeyDown(KeyCode.T))
        {
            Vector3 lastPos = _currentAnimal.transform.position;
            Destroy(_currentAnimal);
            SpawnAnimalAtCursor(10);
            _currentAnimal.transform.position = lastPos;
            Debug.Log("[Debug] Animal Swapped to Level 10");
        }

        ProcessInput();
    }

    /// <summary>터치 및 마우스 입력 처리</summary>
    private void ProcessInput()
    {
        Vector3 inputPos = Vector3.zero;
        bool isInput = false;
        bool isOverUI = false;
        Camera cam = Camera.main;

        if (Input.touchCount > 0)
        {
            Touch touch = Input.GetTouch(0);
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject(touch.fingerId))
                isOverUI = true;

            if (touch.phase == TouchPhase.Ended)
            {
                DropAnimal();
                isInput = false;
            }
            else if (!isOverUI && cam != null)
            {
                inputPos = cam.ScreenToWorldPoint(touch.position);
                isInput = true;
            }
        }
        else if (Input.GetMouseButtonUp(0))
        {
            DropAnimal();
            isInput = false;
        }
        else if (Input.GetMouseButton(0) && !isOverUI && cam != null)
        {
            inputPos = cam.ScreenToWorldPoint(Input.mousePosition);
            isInput = true;
        }

        if (isInput && _currentAnimal != null)
        {
            float targetX = Mathf.Clamp(inputPos.x, _minX, _maxX);
            _currentAnimal.transform.position = new Vector3(targetX, _dynamicSpawnY, 0f);
            UpdateGuideLine(targetX);
        }
        else if (_currentAnimal != null)
        {
            UpdateGuideLine(_currentAnimal.transform.position.x);
        }
    }

    private int GetWeightedRandomLevel()
    {
        float[] weights = (_totalDropCount >= DIFFICULTY_THRESHOLD)
            ? SPAWN_WEIGHTS_LATE
            : SPAWN_WEIGHTS_EARLY;

        int count = weights.Length;
        float totalWeight = 0f;
        for (int i = 0; i < count; i++) totalWeight += weights[i];

        float randomValue = Random.Range(0f, totalWeight);
        float cumulative = 0f;

        for (int i = 0; i < count; i++)
        {
            cumulative += weights[i];
            if (randomValue <= cumulative)
                return SPAWN_MIN_LEVEL + i;
        }

        return SPAWN_MIN_LEVEL;
    }

    public void PrepareNextAnimal()
    {
        _nextAnimalLevel = (_queuedAnimalLevel > 0) ? _queuedAnimalLevel : GetWeightedRandomLevel();
        _queuedAnimalLevel = GetWeightedRandomLevel();

        if (UIMgr.Instance != null)
            UIMgr.Instance.UpdateNextGuide(_queuedAnimalLevel);

        SpawnAnimalAtCursor(_nextAnimalLevel);
    }

    /// <summary>게임오버 시 대기 중인 스폰 예약을 취소하고 상태를 잠금</summary>
    public void CancelPendingSpawn()
    {
        CancelInvoke(nameof(ResetSpawn));
        CanSpawn = false;

        if (_currentAnimal != null)
        {
            PoolMgr.Instance.ReturnAnimal(_currentAnimal.GetComponent<Animal>().Level, _currentAnimal);
            _currentAnimal = null;
        }
    }

    public void FullReset()
    {
        CancelInvoke(nameof(ResetSpawn));

        if (_currentAnimal != null)
        {
            PoolMgr.Instance.ReturnAnimal(_currentAnimal.GetComponent<Animal>().Level, _currentAnimal);
            _currentAnimal = null;
        }

        SetGuideDotsActive(false);
        _isDangerActive = false;
        _dangerTimer = 0f;

        _nextAnimalLevel = -1;
        _queuedAnimalLevel = -1;
        _totalDropCount = 0;
        CanSpawn = true;

        _dynamicSpawnY = _spawnBaseY;
        if (SpawnPoint != null)
        {
            Vector3 sp = SpawnPoint.position;
            SpawnPoint.position = new Vector3(sp.x, _spawnBaseY, sp.z);
        }
    }

    private void SpawnAnimalAtCursor(int level)
    {
        Vector3 spawnPos = (SpawnPoint != null)
            ? new Vector3(SpawnPoint.position.x, _dynamicSpawnY, 0f)
            : new Vector3(0f, _dynamicSpawnY, 0f);

        GameObject prefab = null;
        if (_evolutionData != null && _evolutionData.Levels != null && level <= _evolutionData.Levels.Length)
            prefab = _evolutionData.Levels[level - 1].Prefab;

        GameObject animalGo = (prefab != null)
            ? PoolMgr.Instance.GetAnimal(prefab, level, spawnPos, Quaternion.identity)
            : CreatePlaceholderAnimal(level, spawnPos);

        _currentAnimal = animalGo;

        Rigidbody2D rb = _currentAnimal.GetComponent<Rigidbody2D>();
        if (rb != null) { rb.isKinematic = true; rb.velocity = Vector2.zero; }

        // 대기 중인 동물은 콜라이더를 비활성화하여 박스 안 동물과 물리 충돌 방지
        Collider2D col = _currentAnimal.GetComponent<Collider2D>();
        if (col != null) col.enabled = false;

        Animal animalScript = _currentAnimal.GetComponent<Animal>();
        if (animalScript != null) animalScript.Initialize(level, false);

        UpdateGuideLine(spawnPos.x);
    }

    public static GameObject CreatePlaceholderForMerge(int level, Vector3 position)
    {
        return CreatePlaceholderAnimal(level, position);
    }

    private static GameObject CreatePlaceholderAnimal(int level, Vector3 position)
    {
        float[] diameters = {
            0.35f, 0.44f, 0.55f, 0.68f, 0.85f,
            1.05f, 1.28f, 1.54f, 1.86f, 2.18f, 2.50f
        };
        int idx = Mathf.Clamp(level - 1, 0, diameters.Length - 1);
        float diameter = diameters[idx];

        Color[] palette = {
            new Color(1.00f, 0.78f, 0.80f), new Color(0.98f, 0.90f, 0.70f),
            new Color(0.80f, 0.93f, 0.80f), new Color(0.78f, 0.85f, 1.00f),
            new Color(1.00f, 0.92f, 0.72f), new Color(0.95f, 0.76f, 0.85f),
            new Color(0.80f, 0.90f, 0.98f), new Color(0.70f, 0.95f, 0.90f),
            new Color(1.00f, 0.85f, 0.65f), new Color(0.90f, 0.78f, 1.00f),
            new Color(1.00f, 0.98f, 0.90f),
        };
        Color col = palette[Mathf.Clamp(level - 1, 0, palette.Length - 1)];

        GameObject go = new GameObject($"Animal_Lv{level}_Placeholder");
        try { go.tag = "Animal"; } catch { }
        go.transform.position = position;

        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = CreateCircleSprite(64);
        sr.color = col;
        go.transform.localScale = Vector3.one * diameter;

        Rigidbody2D rb = go.AddComponent<Rigidbody2D>();
        rb.mass = 0.3f + idx * 0.1f;
        rb.gravityScale = 0;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        CircleCollider2D col2d = go.AddComponent<CircleCollider2D>();
        col2d.radius = 0.5f;

        if (PhysicsMgr.Instance != null)
            col2d.sharedMaterial = PhysicsMgr.Instance.GetDefaultMaterial();

        go.AddComponent<Animal>();
        return go;
    }

    private static Sprite CreateCircleSprite(int resolution)
    {
        Texture2D tex = new Texture2D(resolution, resolution, TextureFormat.RGBA32, false);
        float center = resolution / 2f;
        float r = center - 1f;
        Color[] pixels = new Color[resolution * resolution];
        for (int y = 0; y < resolution; y++)
        for (int x = 0; x < resolution; x++)
        {
            float dx = x - center, dy = y - center;
            float dist = Mathf.Sqrt(dx * dx + dy * dy);
            float alpha = Mathf.Clamp01(1f - Mathf.Max(0f, dist - r + 1f));
            pixels[y * resolution + x] = new Color(1f, 1f, 1f, alpha);
        }
        tex.SetPixels(pixels);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, resolution, resolution), new Vector2(0.5f, 0.5f), resolution);
    }

    private void DropAnimal()
    {
        if (_currentAnimal == null) return;
        SetGuideDotsActive(false);

        // 드롭 시 콜라이더 복원
        Collider2D col = _currentAnimal.GetComponent<Collider2D>();
        if (col != null) col.enabled = true;

        Rigidbody2D rb = _currentAnimal.GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.isKinematic = false;
            rb.gravityScale = 1.25f;
            rb.velocity = new Vector2(Random.Range(-0.015f, 0.015f), 0f);
            rb.angularVelocity = Random.Range(-3f, 3f);

            if (SoundMgr.Instance != null)
                SoundMgr.Instance.PlayDrop();
        }

        Animal animalScript = _currentAnimal.GetComponent<Animal>();
        if (animalScript != null) animalScript.SetDropped(true);

        _currentAnimal = null;
        CanSpawn = false;
        _totalDropCount++;

        // [수정] Invoke는 Time.timeScale의 영향을 받지 않으므로 직접 계산하여 호출 (2배속 시 쿨다운 절반)
        float actualDelay = SpawnCooldown / Time.timeScale;
        Invoke(nameof(ResetSpawn), actualDelay);
    }

    private void ResetSpawn()
    {
        CanSpawn = true;
        PrepareNextAnimal();
    }
}
