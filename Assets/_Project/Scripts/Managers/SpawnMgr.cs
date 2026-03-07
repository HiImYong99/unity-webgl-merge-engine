using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 디저트 스폰과 입력(드롭) 처리를 담당하는 매니저
/// </summary>
public class SpawnMgr : MonoBehaviour
{
    public static SpawnMgr Instance { get; private set; }

    public Transform SpawnPoint;
    public float SpawnCooldown = 0.85f;
    public bool CanSpawn = true;

    private GameObject _currentDessert;
    private DessertEvolutionData _evolutionData;

    // Next Queue 시스템
    private int _nextDessertLevel = -1;
    private int _queuedDessertLevel = -1;

    private float _minX = -1.4f;
    private float _maxX = 1.4f;

    // 난이도 조절
    private int _totalDropCount = 0;
    private const int DIFFICULTY_THRESHOLD = 30;

    // 가중치 랜덤 스폰 확률
    private static readonly float[] SPAWN_WEIGHTS_EARLY = { 25f, 25f, 20f, 20f, 10f };
    private static readonly float[] SPAWN_WEIGHTS_LATE = { 15f, 15f, 28f, 28f, 14f };

    private const int SPAWN_MIN_LEVEL = 1;
    private const int SPAWN_MAX_LEVEL = Dessert.MAX_SPAWN_LEVEL;

    [Header("Dynamic Spawn Point")]
    public LayerMask DessertLayer;
    private float _spawnBaseY = 5.5f;
    private const float SPAWN_MAX_Y = 8.5f;
    private const float DANGER_ZONE_H = 1.6f;
    private const float DANGER_ZONE_W = 3.4f; // 3.0 -> 3.4
    private const float SPAWN_RISE_STEP = 0.55f;
    private const float SPAWN_DROP_SPEED = 2.0f;
    private float _dynamicSpawnY;

    // Guide Line
    private LineRenderer _guideLine;
    private const int GUIDE_DOTS = 14;

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
        GameObject glGo = new GameObject("GuideLine");
        glGo.transform.SetParent(transform);
        _guideLine = glGo.AddComponent<LineRenderer>();
        _guideLine.positionCount = GUIDE_DOTS;
        _guideLine.useWorldSpace = true;
        _guideLine.startWidth = 0.025f;
        _guideLine.endWidth = 0.01f;
        _guideLine.numCapVertices = 4;

        Gradient grad = new Gradient();
        grad.SetKeys(
            new GradientColorKey[] {
                new GradientColorKey(new Color(0.8f, 0.5f, 0.9f), 0f),
                new GradientColorKey(new Color(0.5f, 0.7f, 1f), 1f)
            },
            new GradientAlphaKey[] {
                new GradientAlphaKey(0.85f, 0f),
                new GradientAlphaKey(0f, 1f)
            }
        );
        _guideLine.colorGradient = grad;
        _guideLine.material = new Material(Shader.Find("Sprites/Default"));
        _guideLine.sortingOrder = 50;
        _guideLine.enabled = false;
    }

    private void UpdateGuideLine(float xPos)
    {
        if (_guideLine == null) return;
        if (_currentDessert == null || !CanSpawn)
        {
            _guideLine.enabled = false;
            return;
        }

        _guideLine.enabled = true;
        float topY = _dynamicSpawnY - 0.1f;
        float botY = -1.5f;

        for (int i = 0; i < GUIDE_DOTS; i++)
        {
            float t = (float)i / (GUIDE_DOTS - 1);
            float y = Mathf.Lerp(topY, botY, t);
            _guideLine.SetPosition(i, new Vector3(xPos, y, -0.1f));
        }
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
            DessertLayer
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

        if (_currentDessert != null)
        {
            Vector3 dp = _currentDessert.transform.position;
            _currentDessert.transform.position = new Vector3(dp.x, _dynamicSpawnY, dp.z);
        }
    }

#if UNITY_WEBGL && !UNITY_EDITOR
    [System.Runtime.InteropServices.DllImport("__Internal")]
    private static extern void notifyDangerZoneFromUnity(bool active);
    private static void NotifyDangerZone(bool active)
    {
        try { notifyDangerZoneFromUnity(active); } catch { }
    }
#else
    private static void NotifyDangerZone(bool active)
    {
        Debug.Log($"[SpawnMgr] Danger Zone: {active}");
    }
#endif

    private void Update()
    {
        if (GameMgr.Instance != null && GameMgr.Instance.CurrentState != GameMgr.GameState.Playing)
        {
            if (_guideLine != null) _guideLine.enabled = false;
            return;
        }

        if (!CanSpawn || _currentDessert == null)
        {
            if (_guideLine != null) _guideLine.enabled = false;
            return;
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
                DropDessert();
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
            DropDessert();
            isInput = false;
        }
        else if (Input.GetMouseButton(0) && !isOverUI && cam != null)
        {
            inputPos = cam.ScreenToWorldPoint(Input.mousePosition);
            isInput = true;
        }

        if (isInput && _currentDessert != null)
        {
            float targetX = Mathf.Clamp(inputPos.x, _minX, _maxX);
            _currentDessert.transform.position = new Vector3(targetX, _dynamicSpawnY, 0f);
            UpdateGuideLine(targetX);
        }
        else if (_currentDessert != null)
        {
            UpdateGuideLine(_currentDessert.transform.position.x);
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

    public void PrepareNextDessert()
    {
        _nextDessertLevel = (_queuedDessertLevel > 0) ? _queuedDessertLevel : GetWeightedRandomLevel();
        _queuedDessertLevel = GetWeightedRandomLevel();

        if (UIMgr.Instance != null)
            UIMgr.Instance.UpdateNextGuide(_queuedDessertLevel);

        SpawnDessertAtCursor(_nextDessertLevel);
    }

    public void FullReset()
    {
        CancelInvoke(nameof(ResetSpawn));

        if (_currentDessert != null)
        {
            Destroy(_currentDessert);
            _currentDessert = null;
        }

        if (_guideLine != null) _guideLine.enabled = false;
        _isDangerActive = false;
        _dangerTimer = 0f;

        _nextDessertLevel = -1;
        _queuedDessertLevel = -1;
        _totalDropCount = 0;
        CanSpawn = true;

        _dynamicSpawnY = _spawnBaseY;
        if (SpawnPoint != null)
        {
            Vector3 sp = SpawnPoint.position;
            SpawnPoint.position = new Vector3(sp.x, _spawnBaseY, sp.z);
        }
    }

    private void SpawnDessertAtCursor(int level)
    {
        Vector3 spawnPos = (SpawnPoint != null)
            ? new Vector3(SpawnPoint.position.x, _dynamicSpawnY, 0f)
            : new Vector3(0f, _dynamicSpawnY, 0f);

        GameObject prefab = null;
        if (_evolutionData != null && _evolutionData.Levels != null && level <= _evolutionData.Levels.Length)
            prefab = _evolutionData.Levels[level - 1].Prefab;

        GameObject dessertGo = (prefab != null)
            ? Instantiate(prefab, spawnPos, Quaternion.identity)
            : CreatePlaceholderDessert(level, spawnPos);

        _currentDessert = dessertGo;

        Rigidbody2D rb = _currentDessert.GetComponent<Rigidbody2D>();
        if (rb != null) { rb.isKinematic = true; rb.velocity = Vector2.zero; }

        Dessert dessertScript = _currentDessert.GetComponent<Dessert>();
        if (dessertScript != null) dessertScript.Initialize(level, false);

        UpdateGuideLine(spawnPos.x);
    }

    public static GameObject CreatePlaceholderForMerge(int level, Vector3 position)
    {
        return CreatePlaceholderDessert(level, position);
    }

    private static GameObject CreatePlaceholderDessert(int level, Vector3 position)
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

        GameObject go = new GameObject($"Dessert_Lv{level}_Placeholder");
        try { go.tag = "Dessert"; } catch { }
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

        go.AddComponent<Dessert>();
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

    private void DropDessert()
    {
        if (_currentDessert == null) return;
        if (_guideLine != null) _guideLine.enabled = false;

        Rigidbody2D rb = _currentDessert.GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.isKinematic = false;
            rb.gravityScale = 1f;
            rb.velocity = new Vector2(Random.Range(-0.015f, 0.015f), 0f);
            rb.angularVelocity = Random.Range(-3f, 3f);

            if (SoundMgr.Instance != null)
                SoundMgr.Instance.PlayDrop();
        }

        Dessert dessertScript = _currentDessert.GetComponent<Dessert>();
        if (dessertScript != null) dessertScript.SetDropped(true);

        _currentDessert = null;
        CanSpawn = false;
        _totalDropCount++;

        Invoke(nameof(ResetSpawn), SpawnCooldown);
    }

    private void ResetSpawn()
    {
        CanSpawn = true;
        PrepareNextDessert();
    }
}
