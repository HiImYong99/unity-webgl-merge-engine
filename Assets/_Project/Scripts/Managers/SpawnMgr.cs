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

    private float _minX = -1.4f;
    private float _maxX = 1.4f;

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
    private float _spawnBaseY = 7.0f;
    private const float SPAWN_MAX_Y = 8.5f;
    private const float DANGER_ZONE_H = 1.6f;
    private const float DANGER_ZONE_W = 3.4f; // 3.0 -> 3.4
    private const float SPAWN_RISE_STEP = 0.55f;
    private const float SPAWN_DROP_SPEED = 2.0f;
    private float _dynamicSpawnY;

    // Guide Line
    private LineRenderer _guideLine;
    private const int GUIDE_DOTS = 14;

    private GameObject _dropIndicator;
    private SpriteRenderer _dropIndicatorSr;

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

        // --- Drop Indicator Setup ---
        _dropIndicator = new GameObject("DropIndicator");
        _dropIndicator.transform.SetParent(transform);
        _dropIndicatorSr = _dropIndicator.AddComponent<SpriteRenderer>();
        _dropIndicatorSr.sprite = CreateCircleSprite(128);
        _dropIndicatorSr.color = new Color(0.9f, 0.5f, 0.7f, 0.7f); // 귀여운 반투명 핑크 타겟
        _dropIndicator.transform.localScale = new Vector3(0.5f, 0.2f, 1f); 
        _dropIndicatorSr.sortingOrder = 60;
        _dropIndicator.SetActive(false);
    }

    private void UpdateGuideLine(float xPos)
    {
        if (_guideLine == null) return;
        if (_currentAnimal == null || !CanSpawn)
        {
            _guideLine.enabled = false;
            if (_dropIndicator != null) _dropIndicator.SetActive(false);
            return;
        }

        _guideLine.enabled = true;
        if (_dropIndicator != null) _dropIndicator.SetActive(true);

        float topY = _dynamicSpawnY - 0.1f;
        float botY = -4.0f; // 기본으로 아주 아래쪽 낙하 위치로 잡음

        // 현재 동물의 충돌 반경 구하기
        float animalRadius = 0.5f;
        var col2d = _currentAnimal.GetComponent<CircleCollider2D>();
        if (col2d != null) animalRadius = col2d.radius * _currentAnimal.transform.localScale.x;

        // CircleCast로 정확한 낙하 위치(Centroid) 계산
        RaycastHit2D[] hits = Physics2D.CircleCastAll(new Vector2(xPos, topY), animalRadius * 0.95f, Vector2.down, 15f);
        foreach (var h in hits)
        {
            if (h.collider != null && h.collider.gameObject != _currentAnimal && !h.collider.isTrigger)
            {
                botY = topY - h.distance; // distance는 진행 거리이므로, 이를 빼면 도착할 원의 중심 Y좌표가 됨!
                break;
            }
        }

        // 가이드 라인 렌더링
        for (int i = 0; i < GUIDE_DOTS; i++)
        {
            float t = (float)i / (GUIDE_DOTS - 1);
            float y = Mathf.Lerp(topY, botY, t);
            _guideLine.SetPosition(i, new Vector3(xPos, y, -0.1f));
        }

        // 도착 마커 업데이트
        if (_dropIndicator != null)
        {
            // 동물 크기에 비례하여 마커 폭 조절
            float targetWidth = animalRadius * 2.2f;
            _dropIndicator.transform.localScale = new Vector3(targetWidth, targetWidth * 0.35f, 1f);
            
            // 바닥에 살짝 눌린 위치에 배치 (- animalRadius * 0.8f 하여 동물 중심보다 아래 바닥쯤에)
            _dropIndicator.transform.position = new Vector3(xPos, botY - animalRadius * 0.9f, -0.2f);
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
            if (_dropIndicator != null) _dropIndicator.SetActive(false);
            return;
        }

        if (!CanSpawn || _currentAnimal == null)
        {
            if (_guideLine != null) _guideLine.enabled = false;
            if (_dropIndicator != null) _dropIndicator.SetActive(false);
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

    public void FullReset()
    {
        CancelInvoke(nameof(ResetSpawn));

        if (_currentAnimal != null)
        {
            Destroy(_currentAnimal);
            _currentAnimal = null;
        }

        if (_guideLine != null) _guideLine.enabled = false;
        if (_dropIndicator != null) _dropIndicator.SetActive(false);
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
            ? Instantiate(prefab, spawnPos, Quaternion.identity)
            : CreatePlaceholderAnimal(level, spawnPos);

        _currentAnimal = animalGo;

        Rigidbody2D rb = _currentAnimal.GetComponent<Rigidbody2D>();
        if (rb != null) { rb.isKinematic = true; rb.velocity = Vector2.zero; }

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
        if (_guideLine != null) _guideLine.enabled = false;
        if (_dropIndicator != null) _dropIndicator.SetActive(false);

        Rigidbody2D rb = _currentAnimal.GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.isKinematic = false;
            rb.gravityScale = 1f;
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

        Invoke(nameof(ResetSpawn), SpawnCooldown);
    }

    private void ResetSpawn()
    {
        CanSpawn = true;
        PrepareNextAnimal();
    }
}
