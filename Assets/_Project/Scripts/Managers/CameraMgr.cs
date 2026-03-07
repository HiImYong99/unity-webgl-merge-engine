using UnityEngine;
using System.Collections;

/// <summary>
/// 카메라 연출(흔들림 등)을 전담하는 매니저
/// </summary>
public class CameraMgr : MonoBehaviour
{
    public static CameraMgr Instance { get; private set; }

    private Vector3 _originalPos;
    private float _shakeMagnitude = 0f;
    private bool _isShaking = false;
    private Coroutine _shakeCoroutine;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        _originalPos = transform.localPosition;
    }

    /// <summary>CameraScaler가 위치를 변경한 후 기준점을 갱신합니다.</summary>
    public void SyncOrigin()
    {
        if (!_isShaking)
            _originalPos = transform.localPosition;
    }

    /// <summary>감쇠 진동 효과 (강도가 서서히 줄어듦)</summary>
    public void Shake(float duration = 0.15f, float magnitude = 0.05f)
    {
        if (_isShaking && magnitude < _shakeMagnitude * 0.5f) return;

        if (_shakeCoroutine != null) StopCoroutine(_shakeCoroutine);
        _shakeCoroutine = StartCoroutine(Co_DecayShake(duration, magnitude));
    }

    private IEnumerator Co_DecayShake(float duration, float magnitude)
    {
        _isShaking = true;
        _shakeMagnitude = magnitude;

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            float decay = 1f - Mathf.Pow(t, 2.5f);
            float currentMag = magnitude * decay;

            float noiseX = (Mathf.PerlinNoise(elapsed * 45f, 0f) - 0.5f) * 2f;
            float noiseY = (Mathf.PerlinNoise(0f, elapsed * 45f) - 0.5f) * 2f;

            transform.localPosition = new Vector3(
                _originalPos.x + noiseX * currentMag,
                _originalPos.y + noiseY * currentMag,
                _originalPos.z
            );
            yield return null;
        }

        transform.localPosition = _originalPos;
        _isShaking = false;
    }

    private void OnDisable()
    {
        transform.localPosition = _originalPos;
        _isShaking = false;
    }
}
