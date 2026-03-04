using UnityEngine;

/// <summary>
/// 런타임에 부드러운 그라데이션 배경을 생성하여 카메라 뒤에 배치합니다.
/// (현재는 HTML 프리미엄 배경 사용을 위해 카메라만 투명하게 설정)
/// </summary>
public class ProceduralBackground : MonoBehaviour
{
    private void Start()
    {
        CreateBackground();
    }

    private void CreateBackground()
    {
        // 1. 카메라 Clear Color를 투명하게 설정 (HTML 배경 투과용)
        Camera cam = GetComponent<Camera>();
        if (cam != null)
        {
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0, 0, 0, 0);
        }
    }
}
