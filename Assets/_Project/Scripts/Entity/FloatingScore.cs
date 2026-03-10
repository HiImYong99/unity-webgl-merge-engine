using UnityEngine;
using System.Collections;

/// <summary>
/// 병합 시 나타나는 "+점수" 팝업 텍스트 연출
/// </summary>
public class FloatingScore : MonoBehaviour
{
    private static Font _cachedFont;
    private TextMesh _textMesh;
    private readonly float _duration = 0.9f;
    private readonly float _floatSpeed = 1.5f;

    /// <summary>점수 및 색상 초기화</summary>
    public void Initialize(int score, Color color)
    {
        if (_cachedFont == null)
            _cachedFont = Resources.Load<Font>("Fonts/LilitaOne-Regular");

        _textMesh = GetComponent<TextMesh>();
        if (_textMesh == null)
            _textMesh = gameObject.AddComponent<TextMesh>();

        _textMesh.text = "+" + score.ToString("N0");
        _textMesh.characterSize = 0.07f;
        _textMesh.fontSize = 36;
        _textMesh.anchor = TextAnchor.MiddleCenter;
        _textMesh.alignment = TextAlignment.Center;
        _textMesh.fontStyle = FontStyle.Bold;
        _textMesh.color = color;

        if (_cachedFont != null)
        {
            _textMesh.font = _cachedFont;
            GetComponent<MeshRenderer>().material = _cachedFont.material;
        }

        MeshRenderer mr = GetComponent<MeshRenderer>();
        if (mr != null) mr.sortingOrder = 300;

        StartCoroutine(Co_Animate());
    }

    private IEnumerator Co_Animate()
    {
        float elapsed = 0f;
        Vector3 startPos = transform.position;
        float randomX = Random.Range(-0.25f, 0.25f);
        Vector3 targetPos = startPos + new Vector3(randomX, _floatSpeed, 0f);

        transform.localScale = Vector3.zero;

        while (elapsed < _duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / _duration);

            // 위치 이동
            float moveT = 1f - Mathf.Pow(1f - t, 3f);
            transform.position = Vector3.Lerp(startPos, targetPos, moveT);

            // 쫀득한 스케일 애니메이션
            float scale;
            if (t < 0.15f) scale = Mathf.Lerp(0f, 1.3f, t / 0.15f);
            else if (t < 0.30f) scale = Mathf.Lerp(1.3f, 0.9f, (t - 0.15f) / 0.15f);
            else if (t < 0.42f) scale = Mathf.Lerp(0.9f, 1.05f, (t - 0.30f) / 0.12f);
            else scale = Mathf.Lerp(1.05f, 0.85f, (t - 0.42f) / 0.58f);
            
            transform.localScale = Vector3.one * scale;

            // 페이드 아웃
            if (_textMesh != null && t > 0.45f)
            {
                Color c = _textMesh.color;
                c.a = Mathf.SmoothStep(1f, 0f, (t - 0.45f) / 0.55f);
                _textMesh.color = c;
            }

            yield return null;
        }

        if (PoolMgr.Instance != null)
        {
            PoolMgr.Instance.ReturnScoreText(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }
}
