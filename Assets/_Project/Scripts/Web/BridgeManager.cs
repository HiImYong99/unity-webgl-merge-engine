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
#else
    // Editor Fallbacks
    private static void ShowAd() { Debug.Log("[Bridge] ShowAd called in Editor. Simulating Ad Complete."); Instance.OnReviveSuccess(); }
    private static void ShareResult(int score, int level, string imageBase64) { Debug.Log($"[Bridge] ShareResult: Score={score}, Level={level}, Image length={imageBase64.Length}"); }
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

    // Called from JSlib
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
