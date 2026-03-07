using UnityEngine;
using System.Collections;

/// <summary>
/// 개별 디저트 오브젝트의 물리, 애니메이션, 병합 로직을 담당하는 엔티티
/// </summary>
public class Dessert : MonoBehaviour
{
    public int Level { get; private set; }
    public bool IsMerged { get; private set; } = false;
    public bool IsDropped { get { return _isDropped; } }
    
    private bool _isDropped = false;

    // ===== 11단계 오브젝트 크기 정의 (컨테이너 폭 ≈3.2u 기준) =====
    private static readonly float[] LEVEL_DIAMETERS =
    {
        0.35f, // Lv1  젤리빈
        0.44f, // Lv2  마카롱
        0.55f, // Lv3  도넛
        0.68f, // Lv4  컵케이크
        0.85f, // Lv5  소금빵
        1.05f, // Lv6  조각케이크
        1.28f, // Lv7  타르트
        1.54f, // Lv8  빙수
        1.86f, // Lv9  홀케이크
        2.18f, // Lv10 3단트레이
        2.50f, // Lv11 프리미엄
    };

    public const int MAX_LEVEL = 11;
    public const int MAX_SPAWN_LEVEL = 5;

    // 캐싱
    private Rigidbody2D _cachedRb;
    private Collider2D _cachedCol;
    private SpriteRenderer _cachedSr;

    private bool _hasLanded = false;
    private bool _isSpawning = false; // 애니메이션 중 충돌 억제용
    private Coroutine _spawnAnimCoroutine;

    private void Awake()
    {
        gameObject.tag = "Dessert";
        _cachedSr = GetComponent<SpriteRenderer>();
        _cachedRb = GetComponent<Rigidbody2D>();
        EnsureCircleCollider();
    }

    private void Start()
    {
        if (PhysicsMgr.Instance != null && _cachedCol != null && _cachedCol.sharedMaterial == null)
            _cachedCol.sharedMaterial = PhysicsMgr.Instance.GetDefaultMaterial();
    }

    /// <summary>원형 콜라이더 설정 및 물리 재질 적용</summary>
    private void EnsureCircleCollider()
    {
        foreach (var col in GetComponents<PolygonCollider2D>()) Destroy(col);
        foreach (var col in GetComponents<BoxCollider2D>()) Destroy(col);

        CircleCollider2D cc = GetComponent<CircleCollider2D>();
        if (cc == null) cc = gameObject.AddComponent<CircleCollider2D>();

        cc.offset = Vector2.zero;
        cc.radius = 0.50f;
        cc.isTrigger = false;
        cc.enabled = true;
        _cachedCol = cc;

        if (PhysicsMgr.Instance != null)
            cc.sharedMaterial = PhysicsMgr.Instance.GetDefaultMaterial();
    }

    /// <summary>레벨과 상태 초기화</summary>
    public void Initialize(int level, bool dropped)
    {
        Level = level;
        _isDropped = dropped;

        int idx = Mathf.Clamp(level - 1, 0, LEVEL_DIAMETERS.Length - 1);
        float diameter = LEVEL_DIAMETERS[idx];

        // 초기 스케일 설정
        transform.localScale = dropped ? Vector3.zero : new Vector3(diameter * 0.85f, diameter * 1.15f, 1f);
        transform.rotation = Quaternion.identity;

        if (_cachedRb != null)
        {
            _cachedRb.isKinematic = !_isDropped;
            _cachedRb.simulated = true;
            _cachedRb.gravityScale = _isDropped ? 1.0f : 0f;
            _cachedRb.mass = 0.5f + (idx * 0.18f);
            
            _cachedRb.collisionDetectionMode = _isDropped ?
                CollisionDetectionMode2D.Continuous : CollisionDetectionMode2D.Discrete;
            _cachedRb.interpolation = _isDropped ? RigidbodyInterpolation2D.Interpolate : RigidbodyInterpolation2D.None;
        }

        // 스프라이트 로드 (RemoteAssetMgr 통해 비동기 처리)
        if (_cachedSr != null)
        {
            _cachedSr.material = new Material(Shader.Find("Sprites/Default"));

            if (RemoteAssetMgr.Instance != null)
            {
                RemoteAssetMgr.Instance.LoadDessertSprite(level, (sprite) => {
                    if (sprite != null && _cachedSr != null) _cachedSr.sprite = sprite;
                });
            }
        }

        if (_isDropped)
        {
            if (_cachedRb != null) _cachedRb.WakeUp();
            if (_spawnAnimCoroutine != null) StopCoroutine(_spawnAnimCoroutine);
            _spawnAnimCoroutine = StartCoroutine(Co_MergePopIn(diameter));
        }
        else
        {
            if (_spawnAnimCoroutine != null) StopCoroutine(_spawnAnimCoroutine);
            _spawnAnimCoroutine = StartCoroutine(Co_SpawnSquashStretch(diameter));
        }
    }

    private IEnumerator Co_SpawnSquashStretch(float diameter)
    {
        _isSpawning = true;
        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / 0.12f;
            float ease = 1f - Mathf.Pow(1f - Mathf.Clamp01(t), 3f);
            transform.localScale = new Vector3(
                Mathf.Lerp(diameter * 0.85f, diameter * 1.12f, ease),
                Mathf.Lerp(diameter * 1.15f, diameter * 0.88f, ease),
                1f
            );
            yield return null;
        }

        t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / 0.18f;
            float ease = 1f - Mathf.Pow(1f - Mathf.Clamp01(t), 3f);
            float s = Mathf.Lerp(diameter * 0.88f, diameter, ease);
            transform.localScale = new Vector3(s, s, 1f);
            yield return null;
        }
        transform.localScale = Vector3.one * diameter;
        _isSpawning = false;
    }

    private IEnumerator Co_MergePopIn(float diameter)
    {
        _isSpawning = true;
        float[] keyframes = { 0f, 1.28f, 0.92f, 1.04f, 0.98f, 1.0f };
        float[] times = { 0f, 0.18f, 0.30f, 0.40f, 0.48f, 0.56f };
        float elapsed = 0f;
        float total = times[times.Length - 1];

        while (elapsed < total)
        {
            elapsed += Time.deltaTime;
            float tNorm = Mathf.Clamp01(elapsed / total) * (times.Length - 1);
            int i = Mathf.Min((int)tNorm, times.Length - 2);
            float localT = tNorm - i;
            float s = Mathf.Lerp(keyframes[i], keyframes[i + 1], localT) * diameter;
            transform.localScale = new Vector3(s, s, 1f);
            yield return null;
        }
        transform.localScale = Vector3.one * diameter;
        _isSpawning = false;
    }

    public void SetDropped(bool state)
    {
        _isDropped = state;
        if (_cachedRb != null && _isDropped)
        {
            _cachedRb.isKinematic = false;
            _cachedRb.simulated = true;
            _cachedRb.gravityScale = 1.0f;
            _cachedRb.WakeUp();
        }
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (IsMerged) return;

        if (_isDropped && !_hasLanded)
        {
            if (collision.gameObject.CompareTag("Wall") || collision.gameObject.CompareTag("Dessert"))
            {
                _hasLanded = true;
                float speed = collision.relativeVelocity.magnitude;
                if (speed > 1.0f)
                {
                    int idx = Mathf.Clamp(Level - 1, 0, LEVEL_DIAMETERS.Length - 1);
                    StartCoroutine(Co_LandingSquash(LEVEL_DIAMETERS[idx], Mathf.Clamp01(speed / 8f)));
                }

                if (VFXMgr.Instance != null) VFXMgr.Instance.SpawnLandingEffect(transform.position, Level);
                if (Level >= 4 && TossBridgeMgr.Instance != null) TossBridgeMgr.Instance.RequestVibrate("light");
            }
        }
        CheckMerge(collision.gameObject);
    }

    private void OnCollisionStay2D(Collision2D collision)
    {
        if (IsMerged) return;
        CheckMerge(collision.gameObject);
    }

    private void CheckMerge(GameObject otherObj)
    {
        if (IsMerged || _isSpawning) return;

        Dessert other = otherObj.GetComponent<Dessert>();
        if (other != null && other.Level == Level && !other.IsMerged)
        {
            if (gameObject.GetInstanceID() > other.gameObject.GetInstanceID())
            {
                Merge(other);
            }
        }
    }

    private IEnumerator Co_LandingSquash(float diameter, float intensity)
    {
        float squashX = 1f + intensity * 0.22f;
        float squashY = 1f - intensity * 0.18f;
        float t = 0f;

        while (t < 1f)
        {
            t += Time.deltaTime / 0.08f;
            float e = 1f - Mathf.Pow(1f - Mathf.Clamp01(t), 3f);
            transform.localScale = new Vector3(
                Mathf.Lerp(diameter, diameter * squashX, e),
                Mathf.Lerp(diameter, diameter * squashY, e), 1f);
            yield return null;
        }

        t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / 0.14f;
            float e = 1f - Mathf.Pow(1f - Mathf.Clamp01(t), 3f);
            transform.localScale = new Vector3(
                Mathf.Lerp(diameter * squashX, diameter, e),
                Mathf.Lerp(diameter * squashY, diameter, e), 1f);
            yield return null;
        }
        transform.localScale = Vector3.one * diameter;
    }

    private void Merge(Dessert other)
    {
        if (GameMgr.Instance == null) return;

        IsMerged = true;
        other.IsMerged = true;

        if (_cachedCol != null) _cachedCol.enabled = false;
        if (other._cachedCol != null) other._cachedCol.enabled = false;
        if (_cachedRb != null) _cachedRb.simulated = false;
        if (other._cachedRb != null) other._cachedRb.simulated = false;

        Vector3 spawnPos = (transform.position + other.transform.position) / 2f;
        int[] scores = { 2, 4, 8, 16, 35, 70, 150, 350, 800, 2000, 5000 };
        int score = scores[Mathf.Clamp(Level - 1, 0, scores.Length - 1)];

        if (VFXMgr.Instance != null)
        {
            VFXMgr.Instance.SpawnMergeEffect(spawnPos, Level);
            VFXMgr.Instance.SpawnScoreEffect(spawnPos, score, Level);
        }

        if (CameraMgr.Instance != null)
            CameraMgr.Instance.Shake(0.12f + (Level * 0.01f), 0.02f + (Level * 0.012f));

        if (SoundMgr.Instance != null) SoundMgr.Instance.PlayMerge(Level);

        if (TossBridgeMgr.Instance != null)
        {
            string haptic = Level >= 9 ? "heavy" : (Level >= 6 ? "medium" : "light");
            TossBridgeMgr.Instance.RequestVibrate(haptic);
        }

        if (Level >= MAX_LEVEL)
        {
            GameMgr.Instance.AddScore(score, MAX_LEVEL);
            Destroy(gameObject);
            Destroy(other.gameObject);
            return;
        }

        int nextLevel = Level + 1;
        GameMgr.Instance.AddScore(score, nextLevel);

        spawnPos.x = Mathf.Clamp(spawnPos.x, -0.55f, 0.55f);
        spawnPos.y = Mathf.Max(spawnPos.y, -0.3f);

        DessertEvolutionData data = GameMgr.Instance.EvolutionData;
        GameObject nextPrefab = (data != null && nextLevel <= data.Levels.Length) ? data.Levels[nextLevel - 1].Prefab : null;

        GameObject newDessert = (nextPrefab != null)
            ? Instantiate(nextPrefab, spawnPos, Quaternion.identity)
            : SpawnMgr.CreatePlaceholderForMerge(nextLevel, spawnPos);

        if (newDessert != null)
        {
            Dessert nd = newDessert.GetComponent<Dessert>();
            if (nd != null) nd.Initialize(nextLevel, true);
            Rigidbody2D nRb = newDessert.GetComponent<Rigidbody2D>();
            if (nRb != null) nRb.velocity = new Vector2(0, 1.5f);
        }

        Destroy(gameObject);
        Destroy(other.gameObject);
    }
}
