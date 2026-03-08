using UnityEngine;
using System.Runtime.InteropServices;

/// <summary>
/// 일반적인 웹 통신 및 브리지 처리를 담당하는 매니저
/// </summary>
public class BridgeMgr : MonoBehaviour
{
    public static BridgeMgr Instance { get; private set; }

#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")] private static extern void ShowAd();
    [DllImport("__Internal")] private static extern void ShowSpeedBoostAd();
    [DllImport("__Internal")] private static extern void ShareResult(int score, int level, string imageBase64);
    [DllImport("__Internal")] private static extern void ExitApp();
    [DllImport("__Internal")] private static extern void AppLogin();
#else
    private static void ShowAd() { Debug.Log("[BridgeMgr] Mock ShowAd."); Instance.OnReviveSuccess(); }
    private static void ShowSpeedBoostAd() { Debug.Log("[BridgeMgr] Mock ShowSpeedBoostAd."); Instance.OnSpeedBoostAdSuccess(); }
    private static void ShareResult(int score, int level, string base64) { Debug.Log($"[BridgeMgr] Mock Share: {score}"); }
    private static void ExitApp() { Debug.Log("[BridgeMgr] Mock Exit."); }
    private static void AppLogin() { Instance.OnLoginSuccess("mock_bridge_user_" + Random.Range(100, 999)); }
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

    public void RequestAd() => ShowAd();
    public void RequestSpeedBoostAd() => ShowSpeedBoostAd();
    public void RequestShare(int score, int level, string base64) => ShareResult(score, level, base64);
    public void RequestExitApp() => ExitApp();
    public void RequestAppLogin() => AppLogin();

    // Called from JSLib
    public void OnLoginSuccess(string userKey)
    {
        if (GameMgr.Instance != null) GameMgr.Instance.OnUserLogin(userKey);
    }

    public void OnLoginFailed(string error)
    {
        if (GameMgr.Instance != null) GameMgr.Instance.OnUserLogin("guest_" + System.Guid.NewGuid().ToString().Substring(0, 8));
    }

    public void OnReviveSuccess()
    {
        if (GameMgr.Instance != null) GameMgr.Instance.Revive();
    }

    // Called from JSLib after speed boost ad watched
    public void OnSpeedBoostAdSuccess()
    {
        if (GameMgr.Instance != null) GameMgr.Instance.ActivateSpeedBoost();
    }
}
