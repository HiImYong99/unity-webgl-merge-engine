using UnityEngine;
using System;

/// <summary>
/// 인앱 결제 상태 및 보상 관리
/// 상품: remove_ads (990원) — 보상: 광고 완벽 제거 (단일 혜택)
/// </summary>
public class IAPMgr : MonoBehaviour
{
    public static IAPMgr Instance { get; private set; }

    public const string PRODUCT_REMOVE_ADS = "remove_ads_hint_pack"; // 기존 ID 유지 (스토어 등록 ID 변경 불가)

    private const string KEY_ADS_REMOVED    = "iap_ads_removed";
    private const string KEY_PURCHASE_TOKEN = "iap_token_premium";

    public bool IsAdsRemoved { get; private set; }

    public event Action OnAdsRemoved;
    public event Action<string> OnPurchaseStarted;
    public event Action<string> OnPurchaseError;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            gameObject.name = "IAPManager";
            DontDestroyOnLoad(gameObject);
        }
        else { Destroy(gameObject); return; }

        IsAdsRemoved = PlayerPrefs.GetInt(KEY_ADS_REMOVED, 0) == 1;
    }

    private void Start() => SyncStateToJS();

    // ── Public API ────────────────────────────────────────────────────

    public void PurchaseFromJS(string _) => Purchase();

    public void Purchase()
    {
        if (IsAdsRemoved) { OnPurchaseError?.Invoke("already_owned"); return; }
        OnPurchaseStarted?.Invoke(PRODUCT_REMOVE_ADS);
        if (BridgeMgr.Instance != null)
            BridgeMgr.Instance.RequestIAPPurchase(PRODUCT_REMOVE_ADS);
    }

    // ── 브릿지 콜백 ───────────────────────────────────────────────────

    public void HandlePurchaseSuccess(string productId, string token)
    {
        if (productId != PRODUCT_REMOVE_ADS) return;
        PlayerPrefs.SetString(KEY_PURCHASE_TOKEN, token);
        GrantRemoveAds();
    }

    public void HandlePurchaseRestored(string productId, string token)
    {
        if (productId != PRODUCT_REMOVE_ADS || IsAdsRemoved) return;
        PlayerPrefs.SetString(KEY_PURCHASE_TOKEN, token);
        GrantRemoveAds();
    }

    public void HandlePurchaseFailed(string productId)
    {
        OnPurchaseError?.Invoke("failed");
        SyncPurchaseResultToJS("failed", productId);
    }

    public void HandlePurchaseCancelled(string productId)
    {
        OnPurchaseError?.Invoke("cancelled");
        SyncPurchaseResultToJS("cancelled", productId);
    }

    // ── 보상 지급 ─────────────────────────────────────────────────────

    private void GrantRemoveAds()
    {
        IsAdsRemoved = true;
        PlayerPrefs.SetInt(KEY_ADS_REMOVED, 1);
        PlayerPrefs.Save();
        if (BridgeMgr.Instance != null)
            BridgeMgr.Instance.RequestSaveLocal(KEY_ADS_REMOVED, "1");

        OnAdsRemoved?.Invoke();
        SyncStateToJS();
        SyncPurchaseResultToJS("success", PRODUCT_REMOVE_ADS);
        Debug.Log("[IAPMgr] 광고 제거 완료");
    }

    // ── JS 동기화 ─────────────────────────────────────────────────────

    private void SyncStateToJS()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        Application.ExternalCall("updateIAPStateFromUnity", IsAdsRemoved ? 1 : 0, 0);
#endif
    }

    private void SyncPurchaseResultToJS(string result, string productId)
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        Application.ExternalCall("onIAPResultFromUnity", result, productId);
#endif
    }
}
