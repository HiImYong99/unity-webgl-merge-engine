using UnityEngine;
using System.Runtime.InteropServices;

public class BridgeManager : MonoBehaviour
{
    public static BridgeManager Instance { get; private set; }

#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")]
    private static extern void ShowAd();

    [DllImport("__Internal")]
    private static extern void ShareResult(int score, int level, string imageBase64);

    [DllImport("__Internal")]
    private static extern void ExitApp();

    [DllImport("__Internal")]
    private static extern void AppLogin();
#else
    // Editor Fallbacks
    private static void ShowAd() { Debug.Log("[Bridge] ShowAd called in Editor. Simulating Ad Complete."); Instance.OnReviveSuccess(); }
    private static void ShareResult(int score, int level, string imageBase64) { Debug.Log($"[Bridge] ShareResult: Score={score}, Level={level}, Image length={imageBase64.Length}"); }
    private static void ExitApp() { Debug.Log("[Bridge] ExitApp called in Editor."); }
    private static void AppLogin() 
    { 
        Debug.Log("[Bridge] AppLogin called in Editor. Simulating Success."); 
        // We use a small delay or invoke to simulate the async nature if needed, 
        // but for local testing, immediate is fine.
        Instance.OnLoginSuccess("local_dev_user_" + Random.Range(1000, 9999)); 
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

    public void RequestAd()
    {
        ShowAd();
    }

    public void RequestShare(int score, int level, string imageBase64)
    {
        ShareResult(score, level, imageBase64);
    }

    public void RequestExitApp()
    {
        ExitApp();
    }

    public void RequestAppLogin()
    {
        AppLogin();
    }

    // Called from JSlib
    public void OnLoginSuccess(string userKey)
    {
        Debug.Log("[Bridge] Login Success. UserKey: " + userKey);
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnUserLogin(userKey);
        }
    }

    public void OnLoginFailed(string error)
    {
        Debug.LogError("[Bridge] Login Failed: " + error);
        // Fallback for game flow if login fails
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnUserLogin("guest_" + System.Guid.NewGuid().ToString().Substring(0, 8));
        }
    }

    public void OnReviveSuccess()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.Revive();
        }
        else
        {
            Debug.LogError("[Bridge] GameManager instance not found for Revive!");
        }
    }
}
