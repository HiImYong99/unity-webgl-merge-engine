using UnityEngine;

public class PhysicsManager : MonoBehaviour
{
    public static PhysicsManager Instance { get; private set; }

    [Header("Physics Settings")]
    [Tooltip("탄성 계수: 0에 가까울수록 오브젝트가 튀지 않음 (스펙: near-zero)")]
    [Range(0f, 1f)]
    public float Bounciness = 0.0f;     // near-zero bounciness

    [Tooltip("마찰력: 적당한 마찰로 안정적인 쌓기 지원")]
    [Range(0f, 1f)]
    public float Friction = 0.3f;       // moderate friction for stable stacking

    [Header("Sleeping Threshold")]
    [Tooltip("수면 상태 전환 임계값 - 낮을수록 빠르게 수면")]
    public float SleepingThreshold = 0.005f;

    private PhysicsMaterial2D defaultMaterial;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }

        // 기본 물리 머티리얼 생성
        defaultMaterial = new PhysicsMaterial2D("DessertMaterial")
        {
            bounciness = Bounciness,
            friction = Friction
        };

        // ===== 글로벌 물리 설정 =====
        Physics2D.autoSyncTransforms = true; // 트랜스폼 변경 시 즉시 물리 엔진에 반영
        Physics2D.velocityIterations = 16;
        Physics2D.positionIterations = 16;
        Physics2D.defaultContactOffset = 0.001f;
        // Sleeping Threshold: 움직임이 거의 없는 오브젝트는 연산 최적화를 위해
        // '수면 상태'로 전환하지만, 외부 충격 시 즉시 깨어남
        Physics2D.linearSleepTolerance = SleepingThreshold;
        Physics2D.angularSleepTolerance = 2f; // 회전 수면 임계값

        // 중력 설정
        Physics2D.gravity = new Vector2(0f, -9.81f);

        Physics2D.velocityThreshold = 0.05f; // 낮은 속도에서도 겹침 해소 작동하도록 하향
        
        // 연속 충돌 감지는 성능 저하의 주범이므로 사용하지 않음
    }

    public PhysicsMaterial2D GetDefaultMaterial()
    {
        return defaultMaterial;
    }

    /// <summary>
    /// 런타임에 물리 속성을 업데이트합니다.
    /// </summary>
    public void UpdatePhysicsSettings(float newBounciness, float newFriction)
    {
        Bounciness = Mathf.Clamp01(newBounciness);
        Friction = Mathf.Clamp01(newFriction);

        if (defaultMaterial != null)
        {
            defaultMaterial.bounciness = Bounciness;
            defaultMaterial.friction = Friction;
        }
    }
}
