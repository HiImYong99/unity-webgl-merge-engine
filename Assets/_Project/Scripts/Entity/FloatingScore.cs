using UnityEngine;
using System.Collections;

public class FloatingScore : MonoBehaviour
{
    private static Font cachedFont;
    private TextMesh textMesh;
    private float duration = 0.8f;
    private float floatSpeed = 1.2f;

    public void Initialize(int score)
    {
        if (cachedFont == null)
            cachedFont = Resources.Load<Font>("Fonts/LilitaOne-Regular");

        textMesh = gameObject.AddComponent<TextMesh>();
        textMesh.text = "+" + score.ToString();
        textMesh.characterSize = 0.08f;
        textMesh.fontSize = 32;
        textMesh.anchor = TextAnchor.MiddleCenter;
        textMesh.alignment = TextAlignment.Center;
        textMesh.color = new Color(1.0f, 0.45f, 0.55f);
        textMesh.fontStyle = FontStyle.Bold;
        if (cachedFont != null)
        {
            textMesh.font = cachedFont;
            GetComponent<MeshRenderer>().material = cachedFont.material;
        }

        MeshRenderer mr = GetComponent<MeshRenderer>();
        mr.sortingOrder = 300;

        StartCoroutine(Animate());
    }

    private IEnumerator Animate()
    {
        float elapsed = 0f;
        Vector3 startPos = transform.position;
        // 약간의 랜덤 X 오프셋으로 더 자연스러운 효과
        Vector3 targetPos = startPos + new Vector3(Random.Range(-0.3f, 0.3f), floatSpeed, 0);
        
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;

            // 1. 위치: 부드러운 가속 후 감속 (Out-Quart)
            float moveT = 1.0f - Mathf.Pow(1.0f - t, 4);
            transform.position = Vector3.Lerp(startPos, targetPos, moveT);

            // 2. 스케일: 쫀득한 팝 효과 (1.2배까지만)
            float scale = 0f;
            if (t < 0.2f) {
                scale = Mathf.Lerp(0.6f, 1.0f, t / 0.2f);
            } else {
                scale = Mathf.Lerp(1.0f, 0.7f, (t - 0.2f) / 0.8f);
            }
            transform.localScale = Vector3.one * scale;

            // 3. 알파/페이드: 자연스럽게 사라짐
            if (t > 0.5f)
            {
                Color c = textMesh.color;
                c.a = Mathf.SmoothStep(1f, 0f, (t - 0.5f) / 0.5f);
                textMesh.color = c;
            }

            yield return null;
        }

        Destroy(gameObject);
    }
}
