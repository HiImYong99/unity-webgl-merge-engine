using UnityEngine;

/// <summary>
/// 2D 물리 엔진 설정과 재질을 관리하는 매니저
/// </summary>
public class PhysicsMgr : MonoBehaviour
{
    public static PhysicsMgr Instance { get; private set; }

    [Header("Physics Materials")]
    public PhysicsMaterial2D DefaultMaterial;
    public PhysicsMaterial2D StickyMaterial;

    [Header("Settings")]
    public float VelocityThreshold = 0.05f;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
        
        // 전역 물리 설정 최적화 (2D Physics)
        Physics2D.velocityThreshold = VelocityThreshold;
    }

    /// <summary>기본 디저트 물리 재질 반환</summary>
    public PhysicsMaterial2D GetDefaultMaterial()
    {
        if (DefaultMaterial != null) return DefaultMaterial;
        
        // 폴백 재질 생성 (반발계수 낮음, 마찰력 적절)
        var mat = new PhysicsMaterial2D("AnimalDefault")
        {
            bounciness = 0.01f,
            friction = 0.45f
        };
        return mat;
    }

    /// <summary>부착형 디저트 물리 재질 반환</summary>
    public PhysicsMaterial2D GetStickyMaterial()
    {
        if (StickyMaterial != null) return StickyMaterial;
        
        var mat = new PhysicsMaterial2D("AnimalSticky")
        {
            bounciness = 0.01f,
            friction = 0.85f
        };
        return mat;
    }
}
