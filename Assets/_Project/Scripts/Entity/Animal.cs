using UnityEngine;
using System.Collections;
using System.Runtime.InteropServices;

/// <summary>
/// 개별 동물 오브젝트의 물리, 애니메이션, 병합 로직을 담당하는 엔티티
/// </summary>
public class Animal : MonoBehaviour
{
#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")] private static extern void onMergeFromUnity(int level);
    private static void WebBridgeCallMerge(int level) { try { onMergeFromUnity(level); } catch {} }
#else
    private static void WebBridgeCallMerge(int level) {}
#endif

    public int Level { get; private set; }
    public bool IsMerged { get; private set; } = false;
    public bool IsDropped { get { return _isDropped; } }

    private bool _isDropped = false;

    // ===== 11단계 오브젝트 크기 정의 (컨테이너 폭 ≈3.2u 기준) =====
    private static readonly float[] LEVEL_DIAMETERS =
    {
        0.35f, // Lv1  병아리
        0.44f, // Lv2  생쥐
        0.55f, // Lv3  고슴도치
        0.68f, // Lv4  개구리
        0.85f, // Lv5  토끼
        1.05f, // Lv6  고양이
        1.28f, // Lv7  시바견
        1.54f, // Lv8  돼지
        1.86f, // Lv9  판다
        2.18f, // Lv10 곰
        2.50f, // Lv11 호랑이
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
        gameObject.tag = "Animal";
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

        // 초기 스케일 설정 (항상 정방형 유지)
        transform.localScale = dropped ? Vector3.zero : Vector3.one * diameter;
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
                RemoteAssetMgr.Instance.LoadAnimalSprite(level, (sprite) => {
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
        // 정방형 팝인: 작게 → 살짝 크게 → 원래 크기
        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / 0.15f;
            float ease = 1f - Mathf.Pow(1f - Mathf.Clamp01(t), 3f);
            float s = Mathf.Lerp(diameter * 0.7f, diameter * 1.08f, ease);
            transform.localScale = Vector3.one * s;
            yield return null;
        }

        t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / 0.12f;
            float ease = 1f - Mathf.Pow(1f - Mathf.Clamp01(t), 3f);
            float s = Mathf.Lerp(diameter * 1.08f, diameter, ease);
            transform.localScale = Vector3.one * s;
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
            if (collision.gameObject.CompareTag("Wall") || collision.gameObject.CompareTag("Animal"))
            {
                _hasLanded = true;
                float speed = collision.relativeVelocity.magnitude;
                if (speed > 1.0f)
                {
                    int idx = Mathf.Clamp(Level - 1, 0, LEVEL_DIAMETERS.Length - 1);
                    StartCoroutine(Co_LandingSquash(LEVEL_DIAMETERS[idx], Mathf.Clamp01(speed / 8f)));
                }

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

        Animal other = otherObj.GetComponent<Animal>();
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

    private void Merge(Animal other)
    {
        if (GameMgr.Instance == null) return;

        IsMerged = true;
        other.IsMerged = true;

        // simulated=false 이전에 velocity 캡처
        Vector2 myVel = _cachedRb != null ? _cachedRb.velocity : Vector2.zero;
        Vector2 otherVel = other._cachedRb != null ? other._cachedRb.velocity : Vector2.zero;

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

        int nextLevelForJS = Level + 1;
#if UNITY_WEBGL && !UNITY_EDITOR
        WebBridgeCallMerge(nextLevelForJS);
#endif

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

        // 컨테이너 내부로만 클램핑 (여유 있게)
        float halfW = 1.45f; // 컨테이너 halfWidth 1.6 - 여유 0.15
        spawnPos.x = Mathf.Clamp(spawnPos.x, -halfW, halfW);

        AnimalEvolutionData data = GameMgr.Instance.EvolutionData;
        GameObject nextPrefab = (data != null && nextLevel <= data.Levels.Length) ? data.Levels[nextLevel - 1].Prefab : null;

        GameObject newAnimal = (nextPrefab != null)
            ? Instantiate(nextPrefab, spawnPos, Quaternion.identity)
            : SpawnMgr.CreatePlaceholderForMerge(nextLevel, spawnPos);

        if (newAnimal != null)
        {
            Animal nd = newAnimal.GetComponent<Animal>();
            if (nd != null) nd.Initialize(nextLevel, true);
            // 머지된 두 동물의 평균 속도를 부여해 자연스러운 물리 연속성 유지
            Vector2 avgVel = (myVel + otherVel) * 0.5f;
            Rigidbody2D nRb = newAnimal.GetComponent<Rigidbody2D>();
            if (nRb != null) nRb.velocity = new Vector2(avgVel.x * 0.3f, Mathf.Max(avgVel.y * 0.3f, 0f));
        }

        Destroy(gameObject);
        Destroy(other.gameObject);
    }
}
