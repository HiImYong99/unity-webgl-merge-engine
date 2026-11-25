using UnityEngine;
using System;

/// <summary>
/// 통합 IAP 매니저 — Toss (WebGL) + Google Play (Android) 모두 지원
/// </summary>
public class IAPMgr : MonoBehaviour
{
    public static IAPMgr Instance { get; private set; }

    // ── 상품 ID ──
    public const string TOSS_PRODUCT_ID = "ait.0000022018.560c8f2d.99adbacd5a.3325211470";
    public const string GOOGLE_PRODUCT_ID = "remove_ads_hint_pack";

    public static string CurrentProductId
    {
        get
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            return GOOGLE_PRODUCT_ID;
#else
            return TOSS_PRODUCT_ID;
#endif
        }
    }

    // ── 상태 ──
    private const string PREF_KEY = "iap_ads_removed";
    public bool IsAdsRemoved
    {
        get => PlayerPrefs.GetInt(PREF_KEY, 0) == 1;
        private set => PlayerPrefs.SetInt(PREF_KEY, value ? 1 : 0);
    }

    // ── 이벤트 ──
    public event Action OnAdsRemoved;
    public event Action OnPurchaseStarted;
    public event Action<string> OnPurchaseError;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            gameObject.name = "IAPManager";
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    // ── 구매 요청 ──
    public void Purchase()
    {
        OnPurchaseStarted?.Invoke();
        if (BridgeMgr.Instance != null)
            BridgeMgr.Instance.RequestIAPPurchase();
    }

    // ── 콜백: 구매 성공 ──
    public void HandlePurchaseSuccess(string productId, string token)
    {
        Debug.Log($"[IAPMgr] PurchaseSuccess: {productId}, token={token}");
        if (productId == TOSS_PRODUCT_ID || productId == GOOGLE_PRODUCT_ID)
        {
            GrantRemoveAds();
        }
    }

    // ── 콜백: 구매 실패 ──
    public void HandlePurchaseFailed(string productId)
    {
        Debug.LogWarning($"[IAPMgr] PurchaseFailed: {productId}");
        OnPurchaseError?.Invoke(productId);
    }

    // ── 콜백: 구매 취소 ──
    public void HandlePurchaseCancelled(string productId)
    {
        Debug.Log($"[IAPMgr] PurchaseCancelled: {productId}");
    }

    // ── 콜백: 복원 ──
    public void HandlePurchaseRestored(string productId, string token)
    {
        Debug.Log($"[IAPMgr] PurchaseRestored: {productId}");
        if (productId == TOSS_PRODUCT_ID || productId == GOOGLE_PRODUCT_ID)
        {
            GrantRemoveAds();
        }
    }

    // ── 광고 제거 지급 ──
    private void GrantRemoveAds()
    {
        IsAdsRemoved = true;
        PlayerPrefs.Save();

        // JS 동기화 (WebGL 전용)
#if UNITY_WEBGL && !UNITY_EDITOR
        try { Application.ExternalCall("onAdRemovedFromUnity"); } catch { }
#endif

        // GameMgr에 알림
        if (GameMgr.Instance != null)
            GameMgr.Instance.OnIAPPurchased(CurrentProductId);

        OnAdsRemoved?.Invoke();
        Debug.Log("[IAPMgr] GrantRemoveAds 완료");
    }

    // ── JS -> Unity SendMessage 진입점 ──
    public void PurchaseFromJS(string _)
    {
        Purchase();
    }
}
