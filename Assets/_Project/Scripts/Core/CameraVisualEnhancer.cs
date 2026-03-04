using UnityEngine;
using System.Collections;

/// <summary>
/// 카메라의 시각적 효과(흔들림, 배경 그라데이션 연출 등)를 담당합니다.
/// </summary>
public class CameraVisualEnhancer : MonoBehaviour
{
    public static CameraVisualEnhancer Instance { get; private set; }

    private Vector3 originalPos;
    private Camera cam;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        cam = GetComponent<Camera>();
        originalPos = transform.position;
    }

    /// <summary>
    /// 화면을 흔듭니다. (병합 시 호출)
    /// </summary>
    public void Shake(float duration = 0.15f, float magnitude = 0.05f)
    {
        StopAllCoroutines();
        StartCoroutine(DoShake(duration, magnitude));
    }

    private IEnumerator DoShake(float duration, float magnitude)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            float x = Random.Range(-1f, 1f) * magnitude;
            float y = Random.Range(-1f, 1f) * magnitude;

            transform.position = new Vector3(originalPos.x + x, originalPos.y + y, originalPos.z);
            elapsed += Time.deltaTime;
            yield return null;
        }
        transform.position = originalPos;
    }

    // 쉐이더 없이 코드로 간단한 비네팅/그라데이션 효과를 주는 팁:
    // UI Canvas에 아주 연한 검은색 외곽 이미지를 배치하는 것이 가장 효율적입니다.
}
