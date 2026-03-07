using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 앱인토스(AppsInToss) 환경의 Safe Area를 처리하는 컴포넌트.
/// 
/// 동작 방식:
/// 1. 게임 시작 시 TossBridgeMgr를 통해 실제 토스 앱의 Safe Area Insets 를 요청합니다.
/// 2. 인셋 값을 수신하면 Canvas RectTransform의 padding에 반영합니다.
/// 3. 앱인토스 SDK가 없는 환경(로컬/일반 브라우저)에서는 Unity의 Screen.safeArea 를 폴백으로 사용합니다.
/// 
/// 사용: Canvas GameObject 에 이 컴포넌트를 추가하세요.
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class TossSafeArea : MonoBehaviour
{
    private RectTransform rectTransform;
    private Rect lastSafeArea = new Rect(0, 0, 0, 0);

    // AppsInToss에서 수신한 실제 인셋 (픽셀 단위)
    private float tossInsetTop    = 0f;
    private float tossInsetBottom = 0f;
    private float tossInsetLeft   = 0f;
    private float tossInsetRight  = 0f;
    private bool  tossInsetsApplied = false;

    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
    }

    private void Start()
    {
        // 앱인토스 SDK로부터 실제 Safe Area 요청
        if (TossBridgeMgr.Instance != null)
        {
            TossBridgeMgr.Instance.OnSafeAreaReceivedEvent += OnTossInsetsReceived;
            TossBridgeMgr.Instance.RequestSafeArea();
        }
        else
        {
            // TossBridgeMgr가 없으면 Unity Screen.safeArea 폴백
            Debug.LogWarning("[TossSafeArea] TossBridgeMgr not found, using Screen.safeArea fallback.");
            ApplyUnityScreenSafeArea();
        }
    }

    private void OnDestroy()
    {
        if (TossBridgeMgr.Instance != null)
            TossBridgeMgr.Instance.OnSafeAreaReceivedEvent -= OnTossInsetsReceived;
    }

    private void Update()
    {
        // 앱인토스 인셋이 적용되지 않은 경우에만 Unity Screen.safeArea를 계속 모니터링
        if (!tossInsetsApplied && Screen.safeArea != lastSafeArea)
        {
            ApplyUnityScreenSafeArea();
        }
    }

    /// <summary>
    /// 앱인토스로부터 수신한 인셋 적용
    /// </summary>
    public void ApplyTossInsets(float top, float bottom, float left, float right)
    {
        tossInsetTop    = top;
        tossInsetBottom = bottom;
        tossInsetLeft   = left;
        tossInsetRight  = right;
        tossInsetsApplied = true;

        Debug.Log($"[TossSafeArea] Applying Toss insets: top={top}, bottom={bottom}, left={left}, right={right}");

        // 픽셀 → 0~1 Anchor 비율로 변환
        float screenW = Screen.width;
        float screenH = Screen.height;

        Vector2 anchorMin = new Vector2(left / screenW, bottom / screenH);
        Vector2 anchorMax = new Vector2(1f - (right / screenW), 1f - (top / screenH));

        rectTransform.anchorMin = anchorMin;
        rectTransform.anchorMax = anchorMax;
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;
    }

    /// <summary>
    /// Fallback: Unity Screen.safeArea 사용 (일반 브라우저 / 에디터)
    /// </summary>
    private void ApplyUnityScreenSafeArea()
    {
        Rect safeArea = Screen.safeArea;
        lastSafeArea = safeArea;

        Vector2 anchorMin = safeArea.position;
        Vector2 anchorMax = safeArea.position + safeArea.size;

        anchorMin.x /= Screen.width;
        anchorMin.y /= Screen.height;
        anchorMax.x /= Screen.width;
        anchorMax.y /= Screen.height;

        rectTransform.anchorMin = anchorMin;
        rectTransform.anchorMax = anchorMax;
    }

    private void OnTossInsetsReceived(float top, float bottom, float left, float right)
    {
        ApplyTossInsets(top, bottom, left, right);
    }
}
