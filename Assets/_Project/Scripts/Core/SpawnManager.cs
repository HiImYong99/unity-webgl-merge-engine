using UnityEngine;
using UnityEngine.EventSystems;

public class SpawnManager : MonoBehaviour
{
    public static SpawnManager Instance { get; private set; }

    public Transform SpawnPoint;
    public float SpawnCooldown = 1.0f;
    public bool CanSpawn = true;

    private GameObject currentDessert;
    private DessertEvolutionData evolutionData;

    // Next Queue 시스템: 다음에 나올 레벨을 미리 결정
    private int nextDessertLevel = -1;
    private int queuedDessertLevel = -1; // 그 다음 예정 레벨 (미리보기용)

    private float minX = -1.1f;
    private float maxX = 1.1f;

    // 난이도 조절: 드롭 횟수 기반
    private int totalDropCount = 0;
    private const int DIFFICULTY_THRESHOLD = 30;

    // ===== 가중치 랜덤 스폰 확률 =====
    private static readonly float[] SPAWN_WEIGHTS_EARLY = { 20f, 20f, 20f, 20f, 20f };
    private static readonly float[] SPAWN_WEIGHTS_LATE  = { 15f, 15f, 25f, 25f, 20f };

    private const int SPAWN_MIN_LEVEL = 1;
    private const int SPAWN_MAX_LEVEL = Dessert.MAX_SPAWN_LEVEL; // = 5

    // ═══════════════════════════════════════════════════════
    //  동적 스폰 포인트 이동 시스템
    //  디저트가 쌓여 위험 구역에 들어오면 스폰 포인트를 올리고,
    //  비면 부드럽게 기본 위치로 복귀합니다.
    // ═══════════════════════════════════════════════════════
    [Tooltip("디저트 감지에 사용할 Physics Layer (Dessert)")]
    public LayerMask DessertLayer;

    private float spawnBaseY    = 5.5f;   // 기본 Y 위치
    private const float SPAWN_MAX_Y      = 8.5f;  // 최대 상승 한계
    private const float DANGER_ZONE_H    = 1.6f;  // 위험 감지 구역 높이
    private const float DANGER_ZONE_W    = 3.0f;  // 위험 감지 구역 폭 (용기 너비 3.2 기준)
    private const float SPAWN_RISE_STEP  = 0.55f; // 1 LateUpdate당 상승폭
    private const float SPAWN_DROP_SPEED = 2.0f;  // 복귀 속도 (units/sec)

    /// <summary>현재 동적 Y — Start() 초기화 후 매 LateUpdate 갱신</summary>
    private float dynamicSpawnY;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    private void Start()
    {
        if (GameManager.Instance != null)
            evolutionData = GameManager.Instance.EvolutionData;

        // SpawnPoint 초기 Y 기록
        if (SpawnPoint != null)
            spawnBaseY = SpawnPoint.position.y;
        dynamicSpawnY = spawnBaseY;
    }

    /// <summary>
    /// 고정 프레임마다 위험 구역 검사 → 스폰 포인트 Y 조정
    /// </summary>
    private void LateUpdate()
    {
        if (SpawnPoint == null) return;

        // 현재 스폰 Y 바로 아래 영역에 디저트가 있는지 OverlapBox 검사
        Vector2 dangerCenter = new Vector2(
            SpawnPoint.position.x,
            dynamicSpawnY - DANGER_ZONE_H * 0.5f
        );

        bool danger = Physics2D.OverlapBox(
            dangerCenter,
            new Vector2(DANGER_ZONE_W, DANGER_ZONE_H),
            0f,
            DessertLayer
        ) != null;

        if (danger)
        {
            // 위험: 올리기 (단번에 RISE_STEP만큼)
            dynamicSpawnY = Mathf.Min(dynamicSpawnY + SPAWN_RISE_STEP, SPAWN_MAX_Y);
        }
        else
        {
            // 안전: 부드럽게 기본 위치로 복귀
            dynamicSpawnY = Mathf.MoveTowards(dynamicSpawnY, spawnBaseY, SPAWN_DROP_SPEED * Time.deltaTime);
        }

        // SpawnPoint 위치 적용
        Vector3 sp = SpawnPoint.position;
        SpawnPoint.position = new Vector3(sp.x, dynamicSpawnY, sp.z);

        // 보유 중인 디저트도 함께 이동 (X는 건드리지 않음)
        if (currentDessert != null)
        {
            Vector3 dp = currentDessert.transform.position;
            currentDessert.transform.position = new Vector3(dp.x, dynamicSpawnY, dp.z);
        }
    }

    private void Update()
    {
        if (GameManager.Instance != null && GameManager.Instance.CurrentState != GameManager.GameState.Playing) return;
        if (!CanSpawn || currentDessert == null) return;

        // Touch / Mouse 입력 추적
        Vector3 inputPos = Vector3.zero;
        bool isInput    = false;
        bool isOverUI   = false;

        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            isOverUI = true;

        Camera cam = Camera.main;

        if (Input.touchCount > 0)
        {
            Touch touch = Input.GetTouch(0);
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject(touch.fingerId))
                isOverUI = true;

            if (touch.phase == TouchPhase.Ended)
            {
                DropDessert();
                isInput = false;
            }
            else if (!isOverUI && cam != null)
            {
                inputPos = cam.ScreenToWorldPoint(touch.position);
                isInput  = true;
            }
        }
        else if (Input.GetMouseButtonUp(0))
        {
            DropDessert();
            isInput = false;
        }
        else if (Input.GetMouseButton(0) && !isOverUI && cam != null)
        {
            inputPos = cam.ScreenToWorldPoint(Input.mousePosition);
            isInput  = true;
        }

        if (isInput && currentDessert != null)
        {
            // X만 조정 — Y는 LateUpdate의 동적 시스템이 관리
            float targetX = Mathf.Clamp(inputPos.x, minX, maxX);
            currentDessert.transform.position = new Vector3(targetX, dynamicSpawnY, 0f);
        }
    }

    /// <summary>
    /// 가중치 랜덤 알고리즘으로 스폰 레벨을 결정합니다.
    /// 게임 진행도에 따라 초반/후반 확률을 다르게 적용합니다.
    /// </summary>
    private int GetWeightedRandomLevel()
    {
        float[] weights = (totalDropCount >= DIFFICULTY_THRESHOLD)
            ? SPAWN_WEIGHTS_LATE
            : SPAWN_WEIGHTS_EARLY;

        int count = SPAWN_MAX_LEVEL - SPAWN_MIN_LEVEL + 1; // = 5
        float totalWeight = 0f;
        for (int i = 0; i < count; i++)
            totalWeight += weights[i];

        float randomValue = Random.Range(0f, totalWeight);
        float cumulative = 0f;

        for (int i = 0; i < count; i++)
        {
            cumulative += weights[i];
            if (randomValue <= cumulative)
                return SPAWN_MIN_LEVEL + i; // 1-indexed
        }

        return SPAWN_MIN_LEVEL; // Fallback
    }

    public void PrepareNextDessert()
    {
        // Next Queue 시스템:
        // queuedDessertLevel이 이미 예약되어 있으면 그것을 사용
        if (queuedDessertLevel > 0)
        {
            nextDessertLevel = queuedDessertLevel;
        }
        else
        {
            nextDessertLevel = GetWeightedRandomLevel();
        }

        // 다음 예약 레벨 결정 (미리보기용)
        queuedDessertLevel = GetWeightedRandomLevel();

        // UI에 현재 + 다음 미리보기 표시
        if (UIManager.Instance != null)
        {
            UIManager.Instance.UpdateNextGuide(queuedDessertLevel);
        }

        SpawnDessertAtCursor(nextDessertLevel);
    }

    /// <summary>
    /// 현재 큐를 리셋합니다 (게임 재시작 시 사용).
    /// </summary>
    public void ResetQueue()
    {
        nextDessertLevel = -1;
        queuedDessertLevel = -1;
        totalDropCount = 0;
    }

    /// <summary>
    /// 게임 재시작 시 SpawnManager의 모든 상태를 완전히 초기화합니다.
    /// 대기 중인 Invoke(ResetSpawn)를 취소하고, 현재 보유 디저트를 제거하며,
    /// CanSpawn을 true로 리셋합니다.
    /// </summary>
    public void FullReset()
    {
        // 대기 중인 ResetSpawn Invoke 취소
        CancelInvoke(nameof(ResetSpawn));

        // 현재 보유 중인 (아직 드롭 안 된) 디저트 제거
        if (currentDessert != null)
        {
            Destroy(currentDessert);
            currentDessert = null;
        }

        // 큐 리셋
        ResetQueue();

        // 스폰 가능 상태로 복원
        CanSpawn = true;

        // 동적 스폰 Y 초기화
        dynamicSpawnY = spawnBaseY;
        if (SpawnPoint != null)
        {
            Vector3 sp = SpawnPoint.position;
            SpawnPoint.position = new Vector3(sp.x, spawnBaseY, sp.z);
        }
    }

    private void SpawnDessertAtCursor(int level)
    {
        // 동적 스폰 Y 적용
        Vector3 spawnPos = (SpawnPoint != null)
            ? new Vector3(SpawnPoint.position.x, dynamicSpawnY, 0f)
            : new Vector3(0f, dynamicSpawnY, 0f);

        GameObject prefab = null;
        if (evolutionData != null && evolutionData.Levels != null && level <= evolutionData.Levels.Length)
            prefab = evolutionData.Levels[level - 1].Prefab;

        GameObject dessertGo = (prefab != null)
            ? Instantiate(prefab, spawnPos, Quaternion.identity)
            : CreatePlaceholderDessert(level, spawnPos);

        currentDessert = dessertGo;

        // 떨어뜨리기 전까지 물리 정지
        Rigidbody2D rb = currentDessert.GetComponent<Rigidbody2D>();
        if (rb != null) { rb.isKinematic = true; rb.velocity = Vector2.zero; }

        Dessert dessertScript = currentDessert.GetComponent<Dessert>();
        if (dessertScript != null) dessertScript.Initialize(level, false);

        if (VFXManager.Instance != null)
        {
            VFXManager.Instance.SpawnSpawnEffect(spawnPos);
        }
    }

    /// <summary>
    /// 프리팹이 없을 때 런타임에 색-원 임시 디저트를 생성합니다.
    /// </summary>
    public static GameObject CreatePlaceholderForMerge(int level, Vector3 position)
    {
        return CreatePlaceholderDessert(level, position);
    }

    private static GameObject CreatePlaceholderDessert(int level, Vector3 position)
    {
        // Dessert.LevelDiameters 와 동일한 크기 (스펙: 5% of 3.2=0.16, ×1.22)
        float[] diameters = {
            0.16f, 0.20f, 0.24f, 0.29f, 0.36f,
            0.43f, 0.53f, 0.65f, 0.79f, 0.96f, 1.17f
        };
        int idx = Mathf.Clamp(level - 1, 0, diameters.Length - 1);
        float diameter = diameters[idx];

        Color[] palette = {
            new Color(1f, 0.45f, 0.45f),   // Lv1 빨강
            new Color(1f, 0.70f, 0.30f),   // Lv2 주황
            new Color(1f, 0.95f, 0.35f),   // Lv3 노랑
            new Color(0.50f, 0.90f, 0.40f),// Lv4 초록
            new Color(0.35f, 0.75f, 1.00f),// Lv5 파랑
            new Color(0.70f, 0.45f, 1.00f),// Lv6 보라
            new Color(1f, 0.50f, 0.80f),   // Lv7 핑크
            new Color(0.30f, 0.90f, 0.85f),// Lv8 민트
            new Color(1f, 0.80f, 0.20f),   // Lv9 금색
            new Color(0.90f, 0.60f, 0.30f),// Lv10 갈색
            new Color(0.95f, 0.95f, 0.95f),// Lv11 흰색
        };
        Color col = palette[Mathf.Clamp(level - 1, 0, palette.Length - 1)];

        GameObject go = new GameObject($"Dessert_Lv{level}_Placeholder");
        try { go.tag = "Dessert"; }
        catch { }
        go.transform.position = position;

        // SpriteRenderer – 원형 스프라이트
        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = CreateCircleSprite(64);
        sr.color = col;
        go.transform.localScale = Vector3.one * diameter;

        // 물리 (near-zero 탄성, 고마찰)
        Rigidbody2D rb = go.AddComponent<Rigidbody2D>();
        rb.mass = 0.3f + idx * 0.1f;
        rb.gravityScale = 0;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        // CircleCollider2D: 원형 콜라이더로 맞물림(interlocking) 방지
        CircleCollider2D col2d = go.AddComponent<CircleCollider2D>();
        col2d.radius = 0.5f;

        // 물리 머티리얼
        if (PhysicsManager.Instance != null)
        {
            col2d.sharedMaterial = PhysicsManager.Instance.GetDefaultMaterial();
        }
        else
        {
            PhysicsMaterial2D mat = new PhysicsMaterial2D("DessertMat")
            {
                bounciness = 0.0f,
                friction = 0.3f
            };
            col2d.sharedMaterial = mat;
        }

        // Dessert 스크립트 부착
        go.AddComponent<Dessert>();

        return go;
    }

    /// <summary>원형 Sprite를 런타임에 생성합니다.</summary>
    private static Sprite CreateCircleSprite(int resolution)
    {
        Texture2D tex = new Texture2D(resolution, resolution, TextureFormat.RGBA32, false);
        float center = resolution / 2f;
        float r = center - 1f;
        for (int y = 0; y < resolution; y++)
        for (int x = 0; x < resolution; x++)
        {
            float dx = x - center, dy = y - center;
            float dist = Mathf.Sqrt(dx * dx + dy * dy);
            float alpha = Mathf.Clamp01(1f - Mathf.Max(0, dist - r));
            tex.SetPixel(x, y, new Color(1, 1, 1, alpha));
        }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, resolution, resolution), new Vector2(0.5f, 0.5f), resolution);
    }

    private void DropDessert()
    {
        if (currentDessert == null) return;

        Rigidbody2D rb = currentDessert.GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.isKinematic = false;
            rb.gravityScale = 1f;

            // 일직선 낙하: 단, 물리적으로 불가능한 '완벽한 수직 타워'를 방지하기 위해 
            // 미세한 랜덤 오프셋과 회전력을 주어 자연스러운 움직임을 유도합니다.
            float tinyOffset = Random.Range(-0.02f, 0.02f);
            rb.velocity = new Vector2(tinyOffset, 0f);
            rb.angularVelocity = Random.Range(-5f, 5f);

            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlayDrop();
            }
        }

        Dessert dessertScript = currentDessert.GetComponent<Dessert>();
        if (dessertScript != null)
        {
            dessertScript.SetDropped(true);
        }

        currentDessert = null;
        CanSpawn = false;
        totalDropCount++;

        Invoke(nameof(ResetSpawn), SpawnCooldown);
    }

    private void ResetSpawn()
    {
        CanSpawn = true;
        PrepareNextDessert();
    }
}
