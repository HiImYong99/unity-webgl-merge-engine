using UnityEngine;
using System;
using System.Runtime.InteropServices;

/// <summary>
/// AppsInToss (앱인토스) 공식 Unity WebGL Bridge
/// 문서: https://developers-apps-in-toss.toss.im/unity/porting-tutorials/unity-sdk.md
///
/// 모든 API는 다음 패턴을 따릅니다:
///   - WebGL 빌드: DllImport("__Internal") → TossBridge.jslib 함수 호출
///   - Unity 에디터: 로컬 테스트용 Mock 구현
///
/// 사용 방법:
///   TossBridgeManager.Instance.RequestLogin();
///   TossBridgeManager.Instance.RequestVibrate("medium");
///   TossBridgeManager.Instance.RequestShare("디저트 팝에서 1234점 달성!");
/// </summary>
public class TossBridgeManager : MonoBehaviour
{
    public static TossBridgeManager Instance { get; private set; }

    // ── 이벤트 ──────────────────────────────────────────────────
    public event Action<string> OnLoginSuccessEvent;
    public event Action<string> OnLoginFailedEvent;
    public event Action<float, float, float, float> OnSafeAreaReceivedEvent;
    public event Action OnAdCompleteEvent;
    public event Action<string> OnAdFailedEvent;
    public event Action OnShareSuccessEvent;

    // ─────────────────────────────────────────────────────────────
    // DllImport (WebGL 빌드 전용)
    // ─────────────────────────────────────────────────────────────
#if UNITY_WEBGL && !UNITY_EDITOR

    [DllImport("__Internal")] private static extern void TossAppLogin();
    [DllImport("__Internal")] private static extern void TossGetSafeArea();
    [DllImport("__Internal")] private static extern void TossShare(string message);
    [DllImport("__Internal")] private static extern void TossVibrate(string style);
    [DllImport("__Internal")] private static extern void TossShowAd();
    [DllImport("__Internal")] private static extern void TossExitApp();

#else
    // ─────────────────────────────────────────────────────────────
    // Unity Editor Mock 구현 (로컬 테스트용)
    // ─────────────────────────────────────────────────────────────
    private static void TossAppLogin()
    {
        Debug.Log("[TossBridge MOCK] AppLogin → 로컬 로그인 시뮬레이션");
        // 약간의 딜레이를 줘서 비동기 로그인처럼 동작
        Instance.Invoke(nameof(SimulateLoginSuccess), 0.2f);
    }

    private void SimulateLoginSuccess()
    {
        OnLoginSuccess("mock_local_user_" + UnityEngine.Random.Range(1000, 9999));
    }

    private static void TossGetSafeArea()
    {
        Debug.Log("[TossBridge MOCK] GetSafeArea → 에디터 기본값(0,0,0,0) 반환");
        Instance.OnSafeAreaReceived("0,0,0,0");
    }

    private static void TossShare(string message)
    {
        Debug.Log($"[TossBridge MOCK] Share → '{message}'");
        Instance.OnShareSuccess("");
    }

    private static void TossVibrate(string style)
    {
        Debug.Log($"[TossBridge MOCK] Vibrate → style: {style}");
    }

    private static void TossShowAd()
    {
        Debug.Log("[TossBridge MOCK] ShowAd → 1초 후 광고 완료 시뮬레이션");
        Instance.Invoke(nameof(SimulateAdComplete), 1f);
    }

    private void SimulateAdComplete()
    {
        OnAdComplete("");
    }

    private static void TossExitApp()
    {
        Debug.Log("[TossBridge MOCK] ExitApp called in Editor.");
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }
#endif

    // ─────────────────────────────────────────────────────────────
    // MonoBehaviour 생명주기
    // ─────────────────────────────────────────────────────────────
    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    // ─────────────────────────────────────────────────────────────
    // Public API (C# 에서 호출)
    // ─────────────────────────────────────────────────────────────

    /// <summary>토스 로그인 요청</summary>
    public void RequestLogin()
    {
        TossAppLogin();
    }

    /// <summary>Safe Area 인셋 요청 (토스 상단바 등 고려)</summary>
    public void RequestSafeArea()
    {
        TossGetSafeArea();
    }

    /// <summary>네이티브 공유 시트 호출</summary>
    public void RequestShare(string message)
    {
        TossShare(message);
    }

    /// <summary>햅틱 피드백 (진동). style: "light", "medium", "heavy", "success", "error"</summary>
    public void RequestVibrate(string style = "medium")
    {
        TossVibrate(style);
    }

    /// <summary>광고 재생 (부활 아이템)</summary>
    public void RequestShowAd()
    {
        TossShowAd();
    }

    /// <summary>미니앱 종료</summary>
    public void RequestExit()
    {
        TossExitApp();
    }

    // ─────────────────────────────────────────────────────────────
    // Callbacks (jslib → Unity, SendMessage 대상)
    // 이 함수들은 TossBridge.jslib 에서 SendMessage('TossBridgeManager', ...) 로 호출됩니다.
    // ─────────────────────────────────────────────────────────────

    public void OnLoginSuccess(string userKey)
    {
        Debug.Log($"[TossBridge] ✅ 로그인 성공. UserKey: {userKey}");
        OnLoginSuccessEvent?.Invoke(userKey);

        // GameManager에 통보
        if (GameManager.Instance != null)
            GameManager.Instance.OnUserLogin(userKey);
    }

    public void OnLoginFailed(string error)
    {
        Debug.LogError($"[TossBridge] ❌ 로그인 실패: {error}");
        OnLoginFailedEvent?.Invoke(error);

        // 게스트 계정으로 폴백
        if (GameManager.Instance != null)
            GameManager.Instance.OnUserLogin("guest_" + Guid.NewGuid().ToString().Substring(0, 8));
    }

    /// <summary>
    /// Safe Area 인셋 수신. 형식: "top,bottom,left,right" (픽셀 단위)
    /// </summary>
    public void OnSafeAreaReceived(string payload)
    {
        Debug.Log($"[TossBridge] Safe Area received: {payload}");

        var parts = payload.Split(',');
        if (parts.Length == 4 &&
            float.TryParse(parts[0], out float top) &&
            float.TryParse(parts[1], out float bottom) &&
            float.TryParse(parts[2], out float left) &&
            float.TryParse(parts[3], out float right))
        {
            OnSafeAreaReceivedEvent?.Invoke(top, bottom, left, right);

            // TossSafeArea 컴포넌트가 있으면 직접 업데이트
            var safeAreaComp = FindObjectOfType<TossSafeArea>();
            if (safeAreaComp != null)
                safeAreaComp.ApplyTossInsets(top, bottom, left, right);
        }
        else
        {
            Debug.LogWarning($"[TossBridge] SafeArea 파싱 실패: {payload}");
        }
    }

    public void OnAdComplete(string _)
    {
        Debug.Log("[TossBridge] ✅ 광고 시청 완료 → 부활 처리");
        OnAdCompleteEvent?.Invoke();

        if (GameManager.Instance != null)
            GameManager.Instance.Revive();
    }

    public void OnAdFailed(string error)
    {
        Debug.LogWarning($"[TossBridge] 광고 실패: {error}");
        OnAdFailedEvent?.Invoke(error);
    }

    public void OnShareSuccess(string _)
    {
        Debug.Log("[TossBridge] ✅ 공유 성공");
        OnShareSuccessEvent?.Invoke();
    }
}
