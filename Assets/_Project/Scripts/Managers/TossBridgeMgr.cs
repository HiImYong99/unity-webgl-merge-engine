using UnityEngine;
using System;
using System.Runtime.InteropServices;

/// <summary>
/// AppsInToss 전용 WebGL 브리지 매니저
/// </summary>
public class TossBridgeMgr : MonoBehaviour
{
    public static TossBridgeMgr Instance { get; private set; }

    public event Action<string> OnLoginSuccessEvent;
    public event Action<string> OnLoginFailedEvent;
    public event Action<float, float, float, float> OnSafeAreaReceivedEvent;
    public event Action OnAdCompleteEvent;
    public event Action OnShareSuccessEvent;

#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")] private static extern void TossAppLogin();
    [DllImport("__Internal")] private static extern void TossGetSafeArea();
    [DllImport("__Internal")] private static extern void TossShare(string message);
    [DllImport("__Internal")] private static extern void TossVibrate(string style);
    [DllImport("__Internal")] private static extern void TossShowAd();
    [DllImport("__Internal")] private static extern void TossExitApp();
#else
    private static void TossAppLogin() => Instance.Invoke(nameof(Co_SimulateLogin), 0.2f);
    private void Co_SimulateLogin() => OnLoginSuccess("mock_toss_user_" + UnityEngine.Random.Range(1000, 9999));
    private static void TossGetSafeArea() => Instance.OnSafeAreaReceived("0,0,0,0");
    private static void TossShare(string msg) => Instance.OnShareSuccess("");
    private static void TossVibrate(string style) => Debug.Log($"[TossBridge MOCK] Vibrate: {style}");
    private static void TossShowAd() => Instance.Invoke(nameof(Co_SimulateAd), 1f);
    private void Co_SimulateAd() => OnAdComplete("");
    private static void TossExitApp() { 
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }
#endif

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

    public void RequestLogin() => TossAppLogin();
    public void RequestSafeArea() => TossGetSafeArea();
    public void RequestShare(string msg) => TossShare(msg);
    public void RequestVibrate(string style = "medium") => TossVibrate(style);
    public void RequestShowAd() => TossShowAd();
    public void RequestExit() => TossExitApp();

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
            var safe = FindObjectOfType<TossSafeArea>();
            if (safe != null) safe.ApplyTossInsets(top, bottom, left, right);
        }
    }

    public void OnAdComplete(string _)
    {
        OnAdCompleteEvent?.Invoke();
        if (GameMgr.Instance != null) GameMgr.Instance.Revive();
    }

    public void OnShareSuccess(string _) => OnShareSuccessEvent?.Invoke();
}
