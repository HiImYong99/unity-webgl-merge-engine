# Unified Platform Branch Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** main(토스)과 google-play(안드로이드) 두 브랜치를 단일 코드베이스로 통합하여, 빌드 타겟(WebGL/Android)에 따라 광고/결제 백엔드만 자동 전환되도록 한다.

**Architecture:** C#에서는 `UNITY_WEBGL`(토스) / `UNITY_ANDROID`(구글플레이) 전처리기로 브릿지 호출만 분기. 게임 로직(리바이브 2회, 스피드부스트, 점수 등)은 100% 공유. HTML 템플릿은 토스용(index.html)과 안드로이드용(android-wrapper/index.html) 두 벌 유지하되 기능은 동일. TossBridge.jslib은 양쪽 코드 모두 포함.

**Tech Stack:** Unity 2D (C#), WebGL, Android WebView, TossBridge.jslib, AdMob (Android), AppsInToss TossAds (WebGL)

---

## File Structure

### Modified Files (main 기준 수정)
- `Assets/Plugins/TossBridge.jslib` — google-play의 Android IAP 브릿지 코드 추가
- `Assets/_Project/Scripts/Managers/BridgeMgr.cs` — Android IAP 콜백 + RequestPurchase 분기 추가
- `Assets/_Project/Scripts/Managers/GameMgr.cs` — IAPMgr 연동, 플랫폼 무관 로직 유지
- `Assets/_Project/Scripts/Managers/SoundMgr.cs` — ForceRestartBGM/PauseForAd 유지 + SFX 풀링 옵션 추가
- `Assets/_Project/Scripts/Entity/Animal.cs` — 진동 호출 가드 (Android에서도 동작하도록)
- `android-wrapper/app/src/main/assets/index.html` — main의 전체 기능(리바이브, 스피드부스트, 애니메이션) 이식

### New Files (google-play에서 가져오기)
- `Assets/_Project/Scripts/Managers/IAPMgr.cs` — 통합 IAP 매니저 (토스+구글 양쪽 지원)
- `Assets/_Project/Scripts/Utils/DebugUtil.cs` — 조건부 로깅 유틸

### Unchanged Files (건드리지 않음)
- `Assets/_Project/Scripts/Managers/SpawnMgr.cs` — main 버전 그대로 유지
- `Assets/_Project/Scripts/Managers/UIMgr.cs` — main 버전 그대로 유지
- `Assets/_Project/Scripts/UI/TossSafeArea.cs` — main 버전 그대로 유지
- `android-wrapper/app/src/main/java/` — Java 코드는 그대로 유지 (이미 AdMob 완비)

---

### Task 1: DebugUtil.cs 추가

**Files:**
- Create: `Assets/_Project/Scripts/Utils/DebugUtil.cs`

- [ ] **Step 1: DebugUtil.cs 생성**

```csharp
using UnityEngine;
using System.Diagnostics;

public static class DebugUtil
{
    [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
    public static void Log(string msg) => UnityEngine.Debug.Log(msg);

    [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
    public static void LogWarning(string msg) => UnityEngine.Debug.LogWarning(msg);

    [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
    public static void LogError(string msg) => UnityEngine.Debug.LogError(msg);
}
```

- [ ] **Step 2: 컴파일 확인**

Run: Unity Editor에서 컴파일 에러 없는지 확인
Expected: 에러 없음

- [ ] **Step 3: Commit**

```bash
git add Assets/_Project/Scripts/Utils/DebugUtil.cs
git commit -m "feat: add DebugUtil conditional logging utility"
```

---

### Task 2: IAPMgr.cs 통합 버전 생성

google-play의 IAPMgr.cs를 기반으로 토스 IAP도 지원하는 통합 버전 작성.

**Files:**
- Create: `Assets/_Project/Scripts/Managers/IAPMgr.cs`

- [ ] **Step 1: IAPMgr.cs 생성**

```csharp
using UnityEngine;
using System;

/// <summary>
/// 통합 인앱 결제 매니저
/// - WebGL(토스): BridgeMgr.PRODUCT_ID 사용, AppsInToss IAP SDK
/// - Android(구글플레이): PRODUCT_REMOVE_ADS 사용, Google Play Billing
/// </summary>
public class IAPMgr : MonoBehaviour
{
    public static IAPMgr Instance { get; private set; }

    // 토스 상품 ID
    public const string TOSS_PRODUCT_ID = "ait.0000022018.560c8f2d.99adbacd5a.3325211470";
    // 구글플레이 상품 ID
    public const string GOOGLE_PRODUCT_ID = "remove_ads_hint_pack";

    private const string KEY_ADS_REMOVED = "iap_ads_removed";
    private const string KEY_PURCHASE_TOKEN = "iap_token_premium";

    public bool IsAdsRemoved { get; private set; }

    public event Action OnAdsRemoved;
    public event Action<string> OnPurchaseStarted;
    public event Action<string> OnPurchaseError;

    /// <summary>현재 플랫폼의 상품 ID 반환</summary>
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

    // ── Public API ──

    /// <summary>JS에서 SendMessage로 호출 (문자열 인자 무시)</summary>
    public void PurchaseFromJS(string _) => Purchase();

    public void Purchase()
    {
        if (IsAdsRemoved) { OnPurchaseError?.Invoke("already_owned"); return; }
        OnPurchaseStarted?.Invoke(CurrentProductId);
        if (BridgeMgr.Instance != null)
            BridgeMgr.Instance.RequestIAPPurchase();
    }

    // ── 브릿지 콜백 (양쪽 플랫폼 공통) ──

    /// <summary>구매 성공 (토스: OnIAPSuccess → GameMgr → 여기, 안드로이드: HandlePurchaseSuccess)</summary>
    public void HandlePurchaseSuccess(string productId, string token)
    {
        PlayerPrefs.SetString(KEY_PURCHASE_TOKEN, token);
        GrantRemoveAds();
    }

    public void HandlePurchaseRestored(string productId, string token)
    {
        if (IsAdsRemoved) return;
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

    // ── 보상 지급 ──

    private void GrantRemoveAds()
    {
        IsAdsRemoved = true;
        PlayerPrefs.SetInt(KEY_ADS_REMOVED, 1);
        PlayerPrefs.Save();

        if (BridgeMgr.Instance != null)
            BridgeMgr.Instance.RequestSaveLocal(KEY_ADS_REMOVED, "1");

        OnAdsRemoved?.Invoke();

        // GameMgr에도 동기화
        if (GameMgr.Instance != null)
            GameMgr.Instance.OnIAPPurchased(CurrentProductId);

        SyncStateToJS();
        SyncPurchaseResultToJS("success", CurrentProductId);
        DebugUtil.Log("[IAPMgr] 광고 제거 완료");
    }

    // ── JS 동기화 ──

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
```

- [ ] **Step 2: 컴파일 확인**

Run: Unity Editor 컴파일
Expected: 에러 없음 (BridgeMgr.RequestIAPPurchase()는 이미 존재)

- [ ] **Step 3: Commit**

```bash
git add Assets/_Project/Scripts/Managers/IAPMgr.cs
git commit -m "feat: add unified IAPMgr supporting both Toss and Google Play"
```

---

### Task 3: BridgeMgr.cs 통합 — Android 브릿지 추가

main의 BridgeMgr.cs에 Android 전용 DllImport(RequestPurchase, RestorePurchases)와 콜백(OnPurchaseSuccess 등)을 추가한다.

**Files:**
- Modify: `Assets/_Project/Scripts/Managers/BridgeMgr.cs`

- [ ] **Step 1: DllImport 블록에 Android IAP 함수 추가**

기존 `#if UNITY_WEBGL && !UNITY_EDITOR` 블록 내부, `TossIAPCompleteProductGrant` 다음에 추가:

```csharp
    [DllImport("__Internal")] private static extern void RequestPurchase(string productId);
    [DllImport("__Internal")] private static extern void RestorePurchases();
```

`#else` 블록(mock)에도 추가:

```csharp
    private static void RequestPurchase(string productId) {
        Debug.Log($"[BridgeMgr MOCK] RequestPurchase: {productId}");
        Instance.OnPurchaseSuccess(productId + "|mock_token_" + UnityEngine.Random.Range(1000, 9999));
    }
    private static void RestorePurchases() { Debug.Log("[BridgeMgr MOCK] RestorePurchases"); }
```

- [ ] **Step 2: RequestIAPPurchase()를 플랫폼 분기로 변경**

기존:
```csharp
    public const string PRODUCT_ID = "ait.0000022018.560c8f2d.99adbacd5a.3325211470";
    public void RequestIAPPurchase() => TossIAPPurchase(PRODUCT_ID);
    public void RestorePendingOrders() => TossIAPRestorePendingOrders();
```

변경:
```csharp
    public void RequestIAPPurchase()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        RequestPurchase(IAPMgr.GOOGLE_PRODUCT_ID);
#else
        TossIAPPurchase(IAPMgr.TOSS_PRODUCT_ID);
#endif
    }

    public void RestorePendingOrders()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        RestorePurchases();
#else
        TossIAPRestorePendingOrders();
#endif
    }
```

`PRODUCT_ID` 상수는 제거 (IAPMgr로 이관됨).

- [ ] **Step 3: Android IAP 콜백 메서드 추가**

BridgeMgr.cs 하단, `OnProductGrant` 메서드 아래에 추가:

```csharp
    // ── Android (Google Play Billing) 콜백 ──
    // JS: onPurchaseSuccessFromAndroid(productId, token)
    // → AndroidBridge → index.html JS → SendMessage('BridgeManager', 'OnPurchaseSuccess', 'productId|token')
    public void OnPurchaseSuccess(string payload)
    {
        var parts = payload.Split('|');
        string productId = parts.Length > 0 ? parts[0] : "";
        string token = parts.Length > 1 ? parts[1] : "";
        Debug.Log($"[BridgeMgr] OnPurchaseSuccess: {productId}");
        if (IAPMgr.Instance != null)
            IAPMgr.Instance.HandlePurchaseSuccess(productId, token);
    }

    public void OnPurchaseFailed(string payload)
    {
        var parts = payload.Split('|');
        string productId = parts.Length > 0 ? parts[0] : "";
        Debug.LogWarning($"[BridgeMgr] OnPurchaseFailed: {payload}");
        if (IAPMgr.Instance != null)
            IAPMgr.Instance.HandlePurchaseFailed(productId);
    }

    public void OnPurchaseCancelled(string productId)
    {
        Debug.Log($"[BridgeMgr] OnPurchaseCancelled: {productId}");
        if (IAPMgr.Instance != null)
            IAPMgr.Instance.HandlePurchaseCancelled(productId);
    }

    public void OnPurchaseRestored(string payload)
    {
        var parts = payload.Split('|');
        string productId = parts.Length > 0 ? parts[0] : "";
        string token = parts.Length > 1 ? parts[1] : "";
        Debug.Log($"[BridgeMgr] OnPurchaseRestored: {productId}");
        if (IAPMgr.Instance != null)
            IAPMgr.Instance.HandlePurchaseRestored(productId, token);
    }
```

- [ ] **Step 4: 기존 OnIAPSuccess에서 IAPMgr 연동 추가**

기존 `OnIAPSuccess` 메서드에 IAPMgr 호출 추가:

```csharp
    public void OnIAPSuccess(string productId)
    {
        Debug.Log($"[BridgeMgr] IAP Success: {productId}");
        OnIAPSuccessEvent?.Invoke(productId);
        if (GameMgr.Instance != null) GameMgr.Instance.OnIAPPurchased(productId);
        // IAPMgr에도 동기화 (토스 IAP 경로)
        if (IAPMgr.Instance != null) IAPMgr.Instance.HandlePurchaseSuccess(productId, "toss_" + productId);
    }
```

- [ ] **Step 5: Android 전면/보상형 광고 콜백 추가**

Android에서 AdMob 광고 닫힘/보상 콜백을 받을 메서드 추가:

```csharp
    // ── Android AdMob 콜백 ──
    // JS: onInterstitialClosedFromAndroid() → SendMessage
    // (기존 OnInterstitialAdClosed 그대로 사용)

    // JS: onAdRewardedFromAndroid() → SendMessage
    public void OnAdRewarded(string _)
    {
        OnAdCompleteEvent?.Invoke();
        if (GameMgr.Instance != null) GameMgr.Instance.Revive();
    }

    // JS: onAdFailedFromAndroid() → SendMessage
    // (기존 OnAdFailed 그대로 사용)
```

- [ ] **Step 6: 컴파일 확인**

Run: Unity Editor 컴파일
Expected: 에러 없음

- [ ] **Step 7: Commit**

```bash
git add Assets/_Project/Scripts/Managers/BridgeMgr.cs
git commit -m "feat: BridgeMgr unified with Android IAP/Ad callbacks"
```

---

### Task 4: GameMgr.cs — PRODUCT_ID 참조 업데이트

GameMgr.cs에서 `BridgeMgr.PRODUCT_ID` 참조를 `IAPMgr.CurrentProductId`로 변경.

**Files:**
- Modify: `Assets/_Project/Scripts/Managers/GameMgr.cs:563-571`

- [ ] **Step 1: OnIAPPurchased 수정**

기존:
```csharp
    public void OnIAPPurchased(string productId)
    {
        if (productId == BridgeMgr.PRODUCT_ID)
        {
```

변경:
```csharp
    public void OnIAPPurchased(string productId)
    {
        // 토스/구글 어느 상품이든 광고 제거 상품이면 처리
        if (productId == IAPMgr.TOSS_PRODUCT_ID || productId == IAPMgr.GOOGLE_PRODUCT_ID)
        {
```

- [ ] **Step 2: 컴파일 확인 후 Commit**

```bash
git add Assets/_Project/Scripts/Managers/GameMgr.cs
git commit -m "fix: GameMgr uses IAPMgr product IDs for both platforms"
```

---

### Task 5: TossBridge.jslib — Android 브릿지 코드 추가

main의 TossBridge.jslib에 google-play 브랜치의 RequestPurchase, RestorePurchases 함수를 추가.

**Files:**
- Modify: `Assets/Plugins/TossBridge.jslib`

- [ ] **Step 1: Section 4 (IAP) 뒤, Section 5 (네이티브) 앞에 Android IAP 섹션 추가**

`TossExitApp` 함수 바로 위에 삽입:

```javascript
  // ─────────────────────────────────────────────────
  // 4-B. Android Google Play 인앱 결제 (Android 래퍼 전용)
  // ─────────────────────────────────────────────────

  // 결제 바텀시트 요청 (Android WebView → AndroidBridge → Google Play Billing)
  RequestPurchase: function(productIdPtr) {
    var productId = UTF8ToString(productIdPtr);
    if (window.AndroidBridge && typeof window.AndroidBridge.launchPurchase === 'function') {
      window.AndroidBridge.launchPurchase(productId);
    } else {
      // 에디터/WebGL 데스크탑 – 모의 성공 (테스트용)
      console.log('[TossBridge] IAP mock purchase: ' + productId);
      setTimeout(function() {
        SendMessage('BridgeManager', 'OnPurchaseSuccess', productId + '|mock_token_' + Date.now());
      }, 800);
    }
  },

  // 기구매 복원 요청
  RestorePurchases: function() {
    if (window.AndroidBridge && typeof window.AndroidBridge.restorePurchases === 'function') {
      window.AndroidBridge.restorePurchases();
    }
  },
```

- [ ] **Step 2: notifySpeedBoostActivatedFromUnity 함수가 있는지 확인**

google-play에서 빠져있었음. main에 이미 있으므로 그대로 유지.

- [ ] **Step 3: localStorage 키 확인**

main의 `_ShowHtmlLanding`에서 `animalpop_best`를 사용하는지 확인. 
google-play에서 `dessertpop_best`를 쓰고 있었으므로, android-wrapper의 index.html에서도 `animalpop_best`로 통일할 것 (Task 7에서 처리).

- [ ] **Step 4: Commit**

```bash
git add Assets/Plugins/TossBridge.jslib
git commit -m "feat: TossBridge.jslib add Android Google Play IAP bridge"
```

---

### Task 6: SoundMgr.cs — 광고 오디오 핸들링 유지 확인

main의 SoundMgr.cs에 이미 `ForceRestartBGM`, `PauseForAd`, `ResumeAfterAd`가 있으므로 Android에서도 동일하게 사용 가능. 변경 불필요. 확인만.

**Files:**
- Review: `Assets/_Project/Scripts/Managers/SoundMgr.cs`

- [ ] **Step 1: 확인사항**

- `ForceRestartBGM()` — 광고 후 BGM 강제 재시작. Android에서도 필요.
- `PauseForAd()` / `ResumeAfterAd()` — AudioListener 제어. Android에서도 동작.
- SFX 풀링은 google-play에서만 있었지만, main의 PlayOneShot 방식도 안정적이므로 현재는 변경하지 않음.

Expected: 변경 불필요. 스킵.

---

### Task 7: android-wrapper/index.html — main 기능 이식

android-wrapper의 index.html에 main의 전체 기능(리바이브 2회, 스피드부스트, 복잡한 애니메이션, 위험 구역 배너 등)을 이식.

**Files:**
- Modify: `android-wrapper/app/src/main/assets/index.html`

- [ ] **Step 1: main의 index.html을 android-wrapper용으로 복사 후 수정**

main의 `Assets/WebGLTemplates/AnimalPop/index.html`을 기반으로 android-wrapper용 index.html을 생성. 변경사항:

1. **스크립트 로딩**: Vite 모듈(`./unity-bridge.ts`) 참조 제거 → 인라인 JS로 변환 (android WebView에서 Vite 안 씀)
2. **경로 수정**: 스프라이트 경로 `sprites/Animal_N.png` → `https://appassets.androidplatform.net/assets/sprites/Animal_N.png`
3. **광고 호출**: `window.AppsInToss.TossAds` 대신 `window.AndroidBridge.showInterstitialAd()` / `showRewardedAd()` 사용
4. **결제 호출**: `window.AppsInToss.IAP` 대신 `window.AndroidBridge.launchPurchase(productId)` 사용
5. **공유**: `window.AppsInToss.share()` 대신 `window.AndroidBridge.shareText(msg)` 폴백 추가
6. **종료**: `window.AppsInToss.close()` 대신 뒤로가기(Android 하드웨어 버튼)로 처리
7. **Unity 로더**: `{{{ LOADER_FILENAME }}}` 템플릿 변수 → 실제 파일명으로 변경 (WebGL 빌드 후 복사 시)
8. **AudioContext resume**: main의 복잡한 패치 로직 유지

핵심 JS 함수 매핑:
```javascript
// 광고 호출 분기
function requestInterstitialAd() {
  if (window.AndroidBridge && window.AndroidBridge.showInterstitialAd) {
    window.AndroidBridge.showInterstitialAd();
  }
  // Toss 경로는 Unity C# → TossBridge.jslib에서 처리하므로 여기서는 Android만
}

function requestRewardedAd(adType) {
  if (window.AndroidBridge && window.AndroidBridge.showRewardedAd) {
    window.AndroidBridge.showRewardedAd();
    window._pendingAdType = adType; // 0: revive, 1: speed boost
  }
}

// Android 콜백 (MainActivity.java에서 callJs)
function onInterstitialClosedFromAndroid() {
  resumeAudioAfterAd();
  SendMessage('BridgeManager', 'OnInterstitialAdClosed');
}

function onAdRewardedFromAndroid() {
  var adType = window._pendingAdType || 0;
  if (adType === 0) SendMessage('BridgeManager', 'OnReviveSuccess');
  else SendMessage('BridgeManager', 'OnSpeedBoostAdSuccess');
}

function onAdFailedFromAndroid() {
  resumeAudioAfterAd();
  SendMessage('BridgeManager', 'OnAdFailed', 'ANDROID_AD_FAILED');
}

// Android IAP 콜백 (BillingManager.java → callJs)
function onPurchaseSuccessFromAndroid(productId, token) {
  SendMessage('BridgeManager', 'OnPurchaseSuccess', productId + '|' + token);
}
function onPurchaseFailedFromAndroid(productId, responseCode) {
  SendMessage('BridgeManager', 'OnPurchaseFailed', productId + '|' + responseCode);
}
function onPurchaseCancelledFromAndroid(productId) {
  SendMessage('BridgeManager', 'OnPurchaseCancelled', productId);
}
function onPurchaseRestoredFromAndroid(productId, token) {
  SendMessage('BridgeManager', 'OnPurchaseRestored', productId + '|' + token);
}
```

- [ ] **Step 2: 게임오버 UI에 리바이브/스피드부스트 버튼 추가**

main의 게임오버 패널 HTML 구조를 그대로 복사:
- "광고 보고 이어서 하기" 버튼 (spareLives < 2일 때 표시)
- "2배속 플레이" 버튼 (speedBoostActive가 false일 때 표시)
- `showGameOverFromUnity(score, best, adWatched, spareLives)` 파라미터 활용

- [ ] **Step 3: HUD에 스피드부스트 버튼 추가**

main의 `.hud-speed-btn` CSS + HTML + JS 복사.

- [ ] **Step 4: 로딩 애니메이션 이식**

main의 `#load-animals` 동물 이모지 애니메이션 + 90%→99% creep 로직 복사.

- [ ] **Step 5: 위험구역 배너, 신기록 뱃지 이식**

main의 `#danger-banner`, `#new-record-badge` HTML/CSS/JS 복사.

- [ ] **Step 6: localStorage 키 통일**

`dessertpop_best` → `animalpop_best`로 통일.

- [ ] **Step 7: Commit**

```bash
git add android-wrapper/app/src/main/assets/index.html
git commit -m "feat: android index.html synced with main features (revive, speed boost, animations)"
```

---

### Task 8: Android 스프라이트 WebP 변환 및 동기화

main은 WebP 스프라이트를 사용하지만 android-wrapper는 PNG를 사용 중. WebP로 통일.

**Files:**
- Modify: `android-wrapper/app/src/main/assets/sprites/`
- Modify: `android-wrapper/app/src/main/assets/TemplateData/sprites/`

- [ ] **Step 1: main의 WebP 스프라이트를 android-wrapper에 복사**

```bash
cp Assets/WebGLTemplates/AnimalPop/sprites/Animal_*.webp android-wrapper/app/src/main/assets/sprites/
cp Assets/WebGLTemplates/AnimalPop/TemplateData/sprites/Animal_*.webp android-wrapper/app/src/main/assets/TemplateData/sprites/
```

- [ ] **Step 2: index.html에서 PNG → WebP 참조 변경**

android-wrapper의 index.html에서 `.png` 스프라이트 참조를 `.webp`로 변경.

- [ ] **Step 3: 기존 대용량 PNG 삭제**

```bash
rm android-wrapper/app/src/main/assets/sprites/Animal_*.png
rm android-wrapper/app/src/main/assets/TemplateData/sprites/Animal_*.png
```

- [ ] **Step 4: Commit**

```bash
git add android-wrapper/app/src/main/assets/sprites/
git add android-wrapper/app/src/main/assets/TemplateData/sprites/
git commit -m "chore: android sprites migrated to WebP format"
```

---

### Task 9: Animal.cs — 진동 가드 확인

**Files:**
- Review: `Assets/_Project/Scripts/Entity/Animal.cs`

- [ ] **Step 1: 진동 호출 확인**

Animal.cs에서 `BridgeMgr.Instance.RequestVibrate()` 호출이 있는지 확인.
있다면 Android에서도 `navigator.vibrate()`로 폴백되므로 문제없음 (TossBridge.jslib의 TossVibrate에서 처리).

Expected: 변경 불필요. TossBridge.jslib의 TossVibrate가 `navigator.vibrate` 폴백을 이미 포함하고 있음.

---

### Task 10: 빌드 검증 및 정리

**Files:**
- Review: `ProjectSettings/ProjectSettings.asset`

- [ ] **Step 1: WebGL 빌드 테스트**

Unity Editor에서 Build Target = WebGL로 설정 후 빌드.
- TossBridge.jslib 컴파일 에러 없는지 확인
- BridgeMgr의 `#if UNITY_WEBGL` 경로가 활성화되는지 확인

- [ ] **Step 2: Android 빌드 테스트**

Unity Editor에서 Build Target = Android로 전환 후 빌드.
- BridgeMgr의 `#if UNITY_ANDROID` 경로가 활성화되는지 확인
- IAPMgr.CurrentProductId가 `remove_ads_hint_pack`을 반환하는지 확인

- [ ] **Step 3: google-play 브랜치 전용 파일 정리**

main에 있지만 google-play에만 있던 파일들이 필요한지 확인:
- `CLAUDE.md` → google-play에 있던 것은 무시 (main 것 유지)
- `TOSS_BACKPORT_CHECKLIST.md` → 더 이상 불필요 (통합 완료), 삭제
- `PRE_REVIEW_CHECKLIST.md` → 유용하면 유지
- `fix_unity_android.sh` → android-wrapper 빌드 시 필요하면 유지

- [ ] **Step 4: 최종 통합 Commit**

```bash
git add -A
git commit -m "feat: unified platform branch - Toss WebGL + Google Play Android from single codebase"
```

---

## Risk Areas

| 위험 | 영향 | 완화 |
|------|------|------|
| DllImport 충돌 | Android 빌드 시 WebGL 전용 DllImport가 링크 에러 | `#if UNITY_WEBGL && !UNITY_EDITOR` 가드로 이미 보호됨 |
| SendMessage 타겟 이름 | "BridgeManager" 문자열 불일치 | Awake에서 `gameObject.name = "BridgeManager"` 강제 설정 |
| localStorage 키 불일치 | 기존 google-play 유저 데이터 유실 | 마이그레이션 코드 추가 (dessertpop_best → animalpop_best) |
| IAPMgr + GameMgr 이중 호출 | 광고 제거 2번 처리됨 | HandlePurchaseSuccess에서 IsAdsRemoved 체크 |
| Android WebP 미지원 | 구형 Android WebView에서 WebP 깨짐 | Android 5.0+ 타겟이므로 WebP 지원됨 |
