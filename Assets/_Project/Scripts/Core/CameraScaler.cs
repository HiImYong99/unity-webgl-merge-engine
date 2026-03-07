using UnityEngine;

/// <summary>
/// 기기 화면 비율에 따라 카메라 orthographicSize를 동적으로 조정합니다.
/// 게임 컨테이너(GameContainer)가 항상 화면에 맞게 표시되도록 보장합니다.
///
/// 동작 원리:
///   - 목표: 컨테이너 너비(CONTAINER_WORLD_WIDTH)가 화면 가로의 일정 비율을 차지하도록 함
///   - 화면 종횡비(aspect)가 좁을수록(낮을수록) orthographicSize를 키워 전체가 보이게 함
///   - SpawnManager의 X 이동 범위(minX/maxX)도 화면 비율에 맞게 보정
/// </summary>
public class CameraScaler : MonoBehaviour
{
    [Tooltip("게임 컨테이너의 월드 너비 (GameContainer 내부 폭)")]
    public float ContainerWorldWidth = 3.6f; 
    public float MinOrthoSize = 5.0f;
    public float MaxOrthoSize = 8.5f; // 높이가 늘어났으므로 최대 사이즈도 살까 늘립니다.

    private Camera cam;

    private void Awake()
    {
        cam = GetComponent<Camera>();
        if (cam == null) cam = Camera.main;
    }

    private void Start()
    {
        ApplyScale();
    }

    private void ApplyScale()
    {
        if (cam == null) return;

        float screenAspect = (float)Screen.width / Screen.height;

        // 컨테이너가 화면에 들어오게 하는 orthographicSize 계산
        // 좁은 화면(세로)에서는 너비에 맞추고, 넓은 화면에서는 중앙에 고정
        float fillRatio = 0.576f;
        float targetOrthoWidth = ContainerWorldWidth / fillRatio;
        float targetOrthoSize = targetOrthoWidth / (2.0f * screenAspect);

        cam.orthographicSize = Mathf.Clamp(targetOrthoSize, MinOrthoSize, MaxOrthoSize);

        Debug.Log($"[CameraScaler] aspect={screenAspect:F3}, orthoSize={cam.orthographicSize:F2}");
    }
}
