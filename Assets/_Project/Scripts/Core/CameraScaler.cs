using System.Collections;
using UnityEngine;

/// <summary>
/// 기기 화면 비율에 따라 카메라 orthographicSize를 동적으로 조정합니다.
/// 화면 크기 변화(WebGL 캔버스 리사이즈)를 매 프레임 감지해 재적용합니다.
/// </summary>
public class CameraScaler : MonoBehaviour
{
    [Tooltip("게임 컨테이너의 월드 너비 (GameContainer 내부 폭)")]
    public float ContainerWorldWidth = 3.6f;
    public float MinOrthoSize = 5.0f;
    public float MaxOrthoSize = 8.5f;

    [Tooltip("카메라 Y 오프셋: 음수값이면 카메라가 아래를 봄 → 컨테이너가 화면 중간 아래로 내려감")]
    public float CameraYOffset = 1.5f;

    private Camera cam;
    private int lastWidth;
    private int lastHeight;

    private void Awake()
    {
        cam = GetComponent<Camera>();
        if (cam == null) cam = Camera.main;
    }

    private void Start()
    {
        StartCoroutine(ApplyScaleWhenReady());
    }

    private IEnumerator ApplyScaleWhenReady()
    {
        // WebGL에서 Screen.width/height가 첫 프레임에 0으로 잡히는 경우 대기
        while (Screen.width == 0 || Screen.height == 0)
            yield return null;

        // 추가로 2프레임 대기 (캔버스 레이아웃 안정화)
        yield return null;
        yield return null;

        ApplyScale();
    }

    private void Update()
    {
        // 화면 크기가 바뀌면 즉시 재적용 (WebGL 캔버스 리사이즈 대응)
        if (Screen.width != lastWidth || Screen.height != lastHeight)
            ApplyScale();
    }

    private void ApplyScale()
    {
        if (cam == null) return;
        if (Screen.width == 0 || Screen.height == 0) return;

        lastWidth  = Screen.width;
        lastHeight = Screen.height;

        float screenAspect = (float)Screen.width / Screen.height;

        float fillRatio = 0.576f;
        float targetOrthoWidth = ContainerWorldWidth / fillRatio;
        float targetOrthoSize = targetOrthoWidth / (2.0f * screenAspect);

        cam.orthographicSize = Mathf.Clamp(targetOrthoSize, MinOrthoSize, MaxOrthoSize);

        var pos = cam.transform.position;
        cam.transform.position = new Vector3(pos.x, CameraYOffset, pos.z);

        // CameraMgr의 shake 기준점을 새 위치로 동기화
        if (CameraMgr.Instance != null)
            CameraMgr.Instance.SyncOrigin();

        Debug.Log($"[CameraScaler] aspect={screenAspect:F3}, orthoSize={cam.orthographicSize:F2}, camY={CameraYOffset}, screen={Screen.width}x{Screen.height}");
    }
}
