using UnityEngine;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

/// <summary>
/// AppsInToss 및 일반 웹 환경 통합 브릿지 매니저
/// </summary>
public class BridgeMgr : MonoBehaviour
{
    public static BridgeMgr Instance { get; private set; }

    // 이벤트 정의 (TossBridgeMgr에서 이관)
    public event Action<string> OnLoginSuccessEvent;
    public event Action<string> OnLoginFailedEvent;
    public event Action<float, float, float, float> OnSafeAreaReceivedEvent;
    public event Action OnAdCompleteEvent;
    public event Action OnShareSuccessEvent;

#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")] private static extern void SyncSaveToLocalStorage(string key, string value);
    [DllImport("__Internal")] private static extern void _ShowHtmlLanding(int bestScore);
    [DllImport("__Internal")] private static extern void updateScoreFromUnity(int score);
    [DllImport("__Internal")] private static extern void updateNextFromUnity(int level);
    [DllImport("__Internal")] private static extern void showGameOverFromUnity(int score, int best, bool adWatched, int spareLives);
    [DllImport("__Internal")] private static extern void ShowTossAd(int adType);
    [DllImport("__Internal")] private static extern void TossAppLogin();
    [DllImport("__Internal")] private static extern void TossGetSafeArea();
    [DllImport("__Internal")] private static extern void TossShare(string message);
    [DllImport("__Internal")] private static extern void TossVibrate(string style);
    [DllImport("__Internal")] private static extern void TossExitApp();
    [DllImport("__Internal")] private static extern void TossSubmitLeaderboardScore(int score);
    [DllImport("__Internal")] private static extern void notifySpeedBoostActivatedFromUnity();
    [DllImport("__Internal")] private static extern void notifyDangerZoneFromUnity(bool active);
    [DllImport("__Internal")] private static extern void notifyNewHighScoreFromUnity(int score);
    [DllImport("__Internal")] private static extern void onMergeFromUnity(int level);
#else
    private static void SyncSaveToLocalStorage(string k, string v) { }
    private static void _ShowHtmlLanding(int s) { }
    private static void updateScoreFromUnity(int s) { }
    private static void updateNextFromUnity(int l) { }
    private static void showGameOverFromUnity(int s, int b, bool a, int sp) { }
    private static void ShowTossAd(int t) { 
        Debug.Log($"[BridgeMgr MOCK] ShowAd type: {t}");
        if (t == 0) Instance.OnReviveSuccess();
        else Instance.OnSpeedBoostAdSuccess();
    }
    private static void TossAppLogin() => Instance.OnLoginSuccess("mock_user_" + UnityEngine.Random.Range(100, 999));
    private static void TossGetSafeArea() => Instance.OnSafeAreaReceived("0,0,0,0");
    private static void TossShare(string m) => Instance.OnShareSuccess("");
    private static void TossVibrate(string s) { }
    private static void TossExitApp() { 
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }
    private static void TossSubmitLeaderboardScore(int s) { Debug.Log($"[BridgeMgr MOCK] Submit Score: {s}"); }
    private static void notifySpeedBoostActivatedFromUnity() { }
    private static void notifyDangerZoneFromUnity(bool a) { }
    private static void notifyNewHighScoreFromUnity(int s) { }
    private static void onMergeFromUnity(int l) { }
#endif

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            gameObject.name = "BridgeManager"; // JSLib의 SendMessage 타겟 이름 강제 설정
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    // ── 외부 호출용 Public API ──
    public void RequestSaveLocal(string key, string value) => SyncSaveToLocalStorage(key, value);
    public void ShowLandingUI(int best) => _ShowHtmlLanding(best);
    public void UpdateScore(int s) => updateScoreFromUnity(s);
    public void UpdateNext(int l) => updateNextFromUnity(l);
    public void ShowGameOver(int s, int b, bool a, int sp) => showGameOverFromUnity(s, b, a, sp);
    public void RequestAd() => ShowTossAd(0); // 부활
    public void RequestSpeedBoostAd() => ShowTossAd(1); // 2배속
    public void RequestLogin() => TossAppLogin();
    public void RequestSafeArea() => TossGetSafeArea();
    public void RequestShare(string msg) => TossShare(msg);
    public void RequestShare(int score, int level, string base64) => TossShare("제 점수는 " + score + "점이에요! 함께 애니멀 팝 즐겨봐요!");
    public void RequestVibrate(string style = "medium") => TossVibrate(style);
    public void RequestExit() => TossExitApp();
    public void SubmitLeaderboardScore(int s) => TossSubmitLeaderboardScore(s);
    public void NotifySpeedBoost() => notifySpeedBoostActivatedFromUnity();
    public void NotifyDanger(bool active) => notifyDangerZoneFromUnity(active);
    public void NotifyNewRecord(int score) => notifyNewHighScoreFromUnity(score);
    public void NotifyMerge(int level) => onMergeFromUnity(level);

    // ── JS -> Unity 콜백 (JSLib에서 SendMessage로 호출) ──
    public void OnLoginSuccess(string userKey)
    {
        OnLoginSuccessEvent?.Invoke(userKey);
        if (GameMgr.Instance != null) GameMgr.Instance.OnUserLogin(userKey);
    }

    public void OnLoginFailed(string error)
    {
        OnLoginFailedEvent?.Invoke(error);
        if (GameMgr.Instance != null) GameMgr.Instance.OnUserLogin("guest_" + Guid.NewGuid().ToString().Substring(0, 8));
    }

    public void OnSafeAreaReceived(string payload)
    {
        var parts = payload.Split(',');
        if (parts.Length == 4 &&
            float.TryParse(parts[0], out float top) &&
            float.TryParse(parts[1], out float bottom) &&
            float.TryParse(parts[2], out float left) &&
            float.TryParse(parts[3], out float right))
        {
            OnSafeAreaReceivedEvent?.Invoke(top, bottom, left, right);
        }
    }

    public void OnReviveSuccess()
    {
        OnAdCompleteEvent?.Invoke();
        if (GameMgr.Instance != null) GameMgr.Instance.Revive();
    }

    public void OnSpeedBoostAdSuccess()
    {
        if (GameMgr.Instance != null) GameMgr.Instance.ActivateSpeedBoost();
    }

    // [추가] 영구 구매자용 속도 조절 (JS -> BridgeManager -> GameMgr)
    public void SetSpeedMultiplier(string multiplierStr)
    {
        if (float.TryParse(multiplierStr, out float m))
        {
            if (GameMgr.Instance != null) GameMgr.Instance.SetSpeedMultiplier(m);
        }
    }

    public void OnShareSuccess(string _) => OnShareSuccessEvent?.Invoke();
}
