using UnityEngine;
using System.Collections;

/// <summary>
/// 병합 효과 오브젝트의 투명도를 점진적으로 줄이고 파괴하는 유틸리티
/// </summary>
public class MergeEffectFader : MonoBehaviour
{
    private SpriteRenderer _sr;
    private ParticleSystem _ps;

    private void Awake()
    {
        _sr = GetComponentInChildren<SpriteRenderer>();
        _ps = GetComponentInChildren<ParticleSystem>();
    }

    /// <summary>페이드 시작 호출</summary>
    public void StartFade()
    {
        StartCoroutine(Co_FadeAndDestroy());
    }

    private IEnumerator Co_FadeAndDestroy()
    {
        float duration = 1.0f;
        float elapsed = 0f;

        // 파티클 시스템이 있으면 해당 기간에 맞춤 (최소 1초 보장)
        if (_ps != null) duration = Mathf.Max(_ps.main.duration, 1.0f);
        else duration = 1.0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;

            if (_sr != null)
            {
                Color c = _sr.color;
                c.a = 1f - t;
                _sr.color = c;
            }
            yield return null;
        }

        Destroy(gameObject);
    }
}
