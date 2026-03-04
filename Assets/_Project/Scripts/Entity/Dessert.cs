using UnityEngine;
using System.Collections;

public class Dessert : MonoBehaviour
{
    public int Level { get; private set; }
    public bool IsMerged { get; private set; } = false;
    public bool IsDropped { get { return isDropped; } }
    private bool isDropped = false;

    // ===== 11단계 오브젝트 크기 정의 (스펙 준수) =====
    // 기준: 용기 너비 3.2 유닛
    //   - Lv1: 용기 너비의 5% = 0.16 유닛
    //   - 레벨 증가 시 약 1.22배씩 성장
    //   - Lv11 (최종): 1.17 ≤ 용기 너비의 50% (1.6) ✓
    //   - 플레이어 스폰 가능 레벨: Lv1~5 (1-indexed)
    private static readonly float[] LevelDiameters =
    {
        0.16f, // Lv1  젤리빈
        0.20f, // Lv2  마카롱
        0.24f, // Lv3  도넛
        0.29f, // Lv4  컵케이크
        0.36f, // Lv5  소금빵
        0.43f, // Lv6  조각케이크
        0.53f, // Lv7  타르트
        0.65f, // Lv8  빙수
        0.79f, // Lv9  홀케이크
        0.96f, // Lv10 3단트레이
        1.17f, // Lv11 프리미엄 디저트 (용기 너비의 37%)
    };

    // 총 레벨 수
    public const int MAX_LEVEL = 11;

    // 플레이어가 스폰할 수 있는 최대 레벨 (0-indexed: 0~4 → 1-indexed: 1~5)
    public const int MAX_SPAWN_LEVEL = 5;

    // 캐싱 (매 프레임 GetComponent 방지)
    private Rigidbody2D cachedRb;
    private Collider2D cachedCol;

    private void Awake()
    {
        try { gameObject.tag = "Dessert"; }
        catch { /* Auto Setup Scene 먼저 실행 필요 */ }

        // Awake에서 즉시 콜라이더 교체 — 물리 엔진이 시작되기 전에 완료해야 함
        EnsureCircleCollider();

        cachedRb = GetComponent<Rigidbody2D>();
    }

    private void Start()
    {
        if (PhysicsManager.Instance != null)
        {
            if (cachedCol != null && cachedCol.sharedMaterial == null)
            {
                cachedCol.sharedMaterial = PhysicsManager.Instance.GetDefaultMaterial();
            }
        }
    }

    /// <summary>
    /// 프리팹 전체(자식 포함)에서 모든 콜라이더를 제거하고, 
    /// 스프라이트의 실제 중앙에 맞춘 단 하나의 CircleCollider2D만 사용하도록 강제합니다.
    /// Pivot이 중앙이 아닌 스프라이트도 이 로직으로 정확한 물리 판정을 갖게 됩니다.
    /// </summary>
    private void EnsureCircleCollider()
    {
        // 1. 모든 자식 포함 기존 콜라이더 제거
        Collider2D[] allCols = GetComponentsInChildren<Collider2D>(true);
        foreach (var col in allCols)
        {
            // 런타임 콜백 중 즉시 삭제는 에러를 유발하므로 Destroy 사용
            Destroy(col);
        }

        // 2. 새로운 클린 콜라이더 추가
        CircleCollider2D cc = gameObject.AddComponent<CircleCollider2D>();
        
        // 3. 스프라이트 데이터 기반 오차 보정 (핵심: Pivot 보정)
        SpriteRenderer sr = GetComponent<SpriteRenderer>();
        if (sr != null && sr.sprite != null)
        {
            // sprite.bounds.center는 피벗으로부터의 오프셋을 의미함
            cc.offset = sr.sprite.bounds.center;
            
            // 반지름 계산: 스프라이트 크기에 맞게 피팅
            // (보통 1x1 규격이지만, 비정형 스프라이트 대응을 위해 bounds 기준 최대값 사용)
            // 반지름 계산: 세로 높이(bounds.size.y)를 기준으로 타이트하게 보정(0.46배)합니다.
            // 가로가 넓은 디저트(마카롱 등)가 공중에 뜨지 않도록 하고 실제 이미지 경계에 닿게 유도합니다.
            cc.radius = (sr.sprite.bounds.size.y * 0.5f) * 0.92f; 
        }
        else
        {
            cc.offset = Vector2.zero;
            cc.radius = 0.46f;
        }

        cc.isTrigger = false;
        cc.enabled = true;
        cachedCol = cc;

        // 4. 물리 재질 적용
        if (PhysicsManager.Instance != null)
            cc.sharedMaterial = PhysicsManager.Instance.GetDefaultMaterial();
    }

    public void Initialize(int level, bool dropped)
    {
        Level = level;
        isDropped = dropped;

        // 레벨별 고정 크기 적용
        int idx = Mathf.Clamp(level - 1, 0, LevelDiameters.Length - 1);
        float diameter = LevelDiameters[idx];
        transform.localScale = new Vector3(diameter, diameter, 1f);

        // 일직선 낙하를 위해 회전 및 물리 물리량 초기화
        transform.rotation = Quaternion.identity;

        if (cachedRb == null) cachedRb = GetComponent<Rigidbody2D>();
        if (cachedRb != null)
        {
            cachedRb.isKinematic = !isDropped;
            cachedRb.simulated = true;
            cachedRb.gravityScale = isDropped ? 1.0f : 0f;
            cachedRb.mass = 0.5f + (idx * 0.15f); 
            cachedRb.drag = 0.1f;
            cachedRb.angularDrag = 1.0f; // 회전 저항 증가 (정갈한 낙하용)

            // 모든 드롭된 오브젝트는 겹침과 탈출을 방지하기 위해 연속 충돌 감지(Continuous) 사용
            cachedRb.collisionDetectionMode = isDropped ? 
                CollisionDetectionMode2D.Continuous : CollisionDetectionMode2D.Discrete;

            cachedRb.interpolation = isDropped ? RigidbodyInterpolation2D.Interpolate : RigidbodyInterpolation2D.None;
            cachedRb.sleepMode = RigidbodySleepMode2D.StartAwake;

            if (isDropped && !dropped) // 방금 드롭됨
            {
                cachedRb.velocity = Vector2.zero;
                cachedRb.angularVelocity = 0f;
            }
        }

        // CDN 또는 리소스 로더를 통한 스프라이트 자동 할당
        SpriteRenderer sr = GetComponent<SpriteRenderer>();
        if (sr != null)
        {
            // 배경 제거 쉐이더 적용 (코드 기반 해결책)
            Shader chromaShader = Shader.Find("Sprites/WhiteChromaKey");
            if (chromaShader != null)
            {
                sr.material = new Material(chromaShader);
            }

            if (RemoteAssetManager.Instance != null)
            {
                RemoteAssetManager.Instance.LoadDessertSprite(level, (sprite) => {
                    if (sprite != null && sr != null) sr.sprite = sprite;
                });
            }
        }
        // Z축 고정으로 2D 물리 안정성 확보 및 겹침 방지
        transform.position = new Vector3(transform.position.x, transform.position.y, 0f);

        if (cachedRb != null && isDropped)
        {
            cachedRb.WakeUp();
            // 약간의 미세한 흔들림을 주어 겹침 강제 해소 유도
            cachedRb.AddForce(new Vector2(Random.Range(-0.01f, 0.01f), 0.01f), ForceMode2D.Impulse);
        }
    }

    public void SetDropped(bool state)
    {
        isDropped = state;
        if (cachedRb != null && isDropped)
        {
            cachedRb.isKinematic = false;
            cachedRb.simulated = true;
            cachedRb.gravityScale = 1.0f;
            cachedRb.WakeUp();
        }
    }

    private bool hasLanded = false;

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (IsMerged) return;

        // Landing Effect Logic
        if (isDropped && !hasLanded)
        {
            if (collision.gameObject.CompareTag("Wall") || collision.gameObject.CompareTag("Dessert"))
            {
                hasLanded = true;
                if (VFXManager.Instance != null)
                {
                    VFXManager.Instance.SpawnLandingEffect(transform.position);
                }
            }
        }

        Dessert other = collision.gameObject.GetComponent<Dessert>();
        if (other != null && other.Level == Level && !other.IsMerged)
        {
            // 동일 레벨 충돌 → 병합 판정
            // ID 비교로 한 쪽에서만 Merge 실행
            if (gameObject.GetInstanceID() > other.gameObject.GetInstanceID())
            {
                Merge(other);
            }
        }
    }

    /// <summary>
    /// 병합 로직: Cat_n + Cat_n → Cat_{n+1}
    /// 최고 레벨(MAX_LEVEL)끼리 만나면 소멸 처리 (보너스 점수 부여)
    /// 병합 위치: P_C = (P_A + P_B) / 2
    /// 점수: Score += Tier × Multiplier
    /// </summary>
    private void Merge(Dessert other)
    {
        if (GameManager.Instance == null) return;

        IsMerged = true;
        other.IsMerged = true;

        // 병합 시작 시 충돌체와 물리를 즉시 제거하여 겹침/튕김/탈출 방지
        if (cachedCol != null) cachedCol.enabled = false;
        if (other.cachedCol != null) other.cachedCol.enabled = false;

        if (cachedRb != null) { cachedRb.velocity = Vector2.zero; cachedRb.isKinematic = true; }
        if (other.cachedRb != null) { other.cachedRb.velocity = Vector2.zero; other.cachedRb.isKinematic = true; }

        // 병합 위치: 두 오브젝트의 중점
        Vector3 spawnPos = (transform.position + other.transform.position) / 2f;
        
        if (VFXManager.Instance != null)
        {
            VFXManager.Instance.SpawnPopEffect(spawnPos);
        }

        if (CameraVisualEnhancer.Instance != null)
        {
            // 레벨이 높을수록 더 강하게 흔들림
            float intensity = 0.03f + (Level * 0.01f);
            CameraVisualEnhancer.Instance.Shake(0.15f, intensity);
        }

        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayMerge();
            AudioManager.Instance.PlayScore();
        }

        int nextLevel = Level + 1;

        // ===== 새로운 점수 산정 방식 (사용자 정의 테이블) =====
        int[] mergeScores = {
            2,     // Lv1 + Lv1 -> Lv2
            4,     // Lv2 + Lv2 -> Lv3
            8,     // Lv3 + Lv3 -> Lv4
            16,    // Lv4 + Lv4 -> Lv5
            35,    // Lv5 + Lv5 -> Lv6
            70,    // Lv6 + Lv6 -> Lv7
            150,   // Lv7 + Lv7 -> Lv8
            350,   // Lv8 + Lv8 -> Lv9
            800,   // Lv9 + Lv9 -> Lv10
            2000,  // Lv10 + Lv10 -> Lv11
            5000   // Lv11 + Lv11 -> (제거)
        };

        int scoreIndex = Mathf.Clamp(Level - 1, 0, mergeScores.Length - 1);
        int score = mergeScores[scoreIndex];

        if (VFXManager.Instance != null)
        {
            VFXManager.Instance.SpawnScoreEffect(spawnPos, score);
        }

        // ===== 최고 레벨 예외 처리 (Lv.11 + Lv.11) =====
        if (Level >= MAX_LEVEL)
        {
            GameManager.Instance.AddScore(score, MAX_LEVEL);
            SpawnMergeEffect(spawnPos);
            Destroy(gameObject);
            Destroy(other.gameObject);
            return;
        }

        // 일반 병합
        GameManager.Instance.AddScore(score, nextLevel);

        // 다음 레벨 오브젝트 생성
        DessertEvolutionData data = GameManager.Instance.EvolutionData;
        GameObject nextPrefab = null;
        if (data != null && data.Levels != null && nextLevel <= data.Levels.Length)
            nextPrefab = data.Levels[nextLevel - 1].Prefab;

        // ===== Box Escape 방지: 3.2 너비 용기에 맞춘 타이트한 클램핑 =====
        float limitX = 1.5f;
        float floorY = -0.8f;
        spawnPos.x = Mathf.Clamp(spawnPos.x, -limitX, limitX);
        spawnPos.y = Mathf.Max(spawnPos.y, floorY);

        GameObject newDessert;
        if (nextPrefab != null)
        {
            // 위쪽 힘을 주어 바닥 탈출 방지 및 겹침 해소 도움
            newDessert = Instantiate(nextPrefab, spawnPos, Quaternion.identity);
            Rigidbody2D nRb = newDessert.GetComponent<Rigidbody2D>();
            if (nRb != null) nRb.velocity = new Vector2(0, 2.0f); 
        }
        else
        {
            newDessert = SpawnManager.CreatePlaceholderForMerge(nextLevel, spawnPos);
        }

        if (newDessert != null)
        {
            Dessert newDessertScript = newDessert.GetComponent<Dessert>();
            if (newDessertScript != null)
            {
                newDessertScript.Initialize(nextLevel, true);
                newDessertScript.StartCoroutine(PopEffect(newDessert.transform));
            }

            // 연쇄 반응(Chain Reaction) 지원:
            // 새로 생성된 오브젝트가 다른 같은 레벨 오브젝트와
            // 접촉하면 OnCollisionEnter2D에서 자동으로 Merge 트리거됨
            // → 물리 엔진이 자연스럽게 연쇄를 처리합니다
        }

        // 소멸 이펙트
        SpawnMergeEffect(spawnPos);

        Destroy(gameObject);
        Destroy(other.gameObject);
    }

    private void SpawnMergeEffect(Vector3 position)
    {
        if (VFXManager.Instance != null)
        {
            VFXManager.Instance.SpawnMergeEffect(position, Level);
        }
        else
        {
            // Fallback: 간단한 구형 이펙트
            GameObject effectGo = new GameObject("MergeEffect_Fallback");
            effectGo.transform.position = position;
            var sr = effectGo.AddComponent<SpriteRenderer>();
            sr.color = new Color(1f, 1f, 0.5f, 0.8f);
            sr.sortingOrder = 100;
            effectGo.transform.localScale = Vector3.one * 0.3f;
            var fader = effectGo.AddComponent<MergeEffectFader>();
            fader.StartFade();
        }
    }

    private IEnumerator PopEffect(Transform target)
    {
        Vector3 originalScale = target.localScale;
        float elapsed = 0f;
        float duration = 0.5f; // 조금 더 길게 두어 여운을 줌

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            
            // Elastic Out Bounce Curve (0 -> 1.2 -> 0.9 -> 1.0)
            float s = 0;
            if (t < 0.3f) {
                s = Mathf.Lerp(0f, 1.25f, t / 0.3f); // 확 커짐
            } else if (t < 0.6f) {
                s = Mathf.Lerp(1.25f, 0.9f, (t - 0.3f) / 0.3f); // 살짝 줄어듦
            } else {
                s = Mathf.Lerp(0.9f, 1.0f, (t - 0.6f) / 0.4f); // 제자리
            }
            
            target.localScale = originalScale * s;
            yield return null;
        }
        target.localScale = originalScale;
    }
}

/// <summary>
/// 병합 이펙트 페이드아웃용 헬퍼 MonoBehaviour
/// </summary>
public class MergeEffectFader : MonoBehaviour
{
    public void StartFade()
    {
        StartCoroutine(FadeAndDestroy());
    }

    private IEnumerator FadeAndDestroy()
    {
        SpriteRenderer sr = GetComponent<SpriteRenderer>();
        float duration = 0.4f;
        float elapsed = 0f;
        Vector3 startScale = transform.localScale;
        Vector3 endScale = startScale * 3f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            transform.localScale = Vector3.Lerp(startScale, endScale, t);
            if (sr != null)
            {
                Color c = sr.color;
                c.a = Mathf.Lerp(0.8f, 0f, t);
                sr.color = c;
            }
            yield return null;
        }
        Destroy(gameObject);
    }
}
