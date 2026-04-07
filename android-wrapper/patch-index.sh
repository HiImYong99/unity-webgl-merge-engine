#!/bin/bash
# ═══════════════════════════════════════════════════════════════
#  Android index.html 패치 스크립트
#  Unity WebGL 빌드가 토스용 index.html을 생성한 후,
#  Android WebView에 필요한 수정사항을 자동 적용합니다.
# ═══════════════════════════════════════════════════════════════

set -e

INDEX="$1"
if [ -z "$INDEX" ] || [ ! -f "$INDEX" ]; then
    echo "[ERROR] Usage: patch-index.sh <path/to/index.html>"
    exit 1
fi

echo "[PATCH] Android index.html 패치 시작: $INDEX"

# ── 1. unity-bridge.ts 제거 (Vite 전용, Android에서 404) ──
sed -i '' 's|<script type="module" src="./unity-bridge.ts"></script>|<!-- unity-bridge.ts removed for Android -->|g' "$INDEX"
echo "[PATCH] 1/6 unity-bridge.ts 제거"

# ── 2. Android 광고/IAP 헬퍼 + 콜백 함수 삽입 ──
# </body> 바로 앞에 Android 전용 JS 블록 삽입
cat >> "$INDEX" << 'ANDROID_PATCH'

<!-- ═══ Android Bridge Patch ═══ -->
<script>
(function() {
    // ── Android 광고 호출 헬퍼 ──
    window._showAndroidRewardedAd = function(adType) {
        window._pendingAdType = adType;
        if (window.AndroidBridge && typeof window.AndroidBridge.showRewardedAd === 'function') {
            if (typeof window.pauseAudioForAd === 'function') window.pauseAudioForAd();
            window.AndroidBridge.showRewardedAd();
        } else {
            console.warn('[Ad] AndroidBridge not available');
            if (typeof window._showAdUnavailableToast === 'function') window._showAdUnavailableToast();
        }
    };

    window._showAndroidInterstitialAd = function(callback) {
        window._onInterstitialDone = callback;
        if (window.AndroidBridge && typeof window.AndroidBridge.showInterstitialAd === 'function') {
            if (typeof window.pauseAudioForAd === 'function') window.pauseAudioForAd();
            window.AndroidBridge.showInterstitialAd();
        } else {
            if (callback) callback();
        }
    };

    // ── Android 네이티브 콜백 (MainActivity.java → callJs) ──
    window.onInterstitialClosedFromAndroid = function() {
        if (typeof window.resumeAudioAfterAd === 'function') window.resumeAudioAfterAd();
        if (window._onInterstitialDone) { window._onInterstitialDone(); window._onInterstitialDone = null; }
    };

    window.onAdRewardedFromAndroid = function() {
        var t = window._pendingAdType || 0;
        if (typeof window.resumeAudioAfterAd === 'function') window.resumeAudioAfterAd();
        var ui = window.unityInstance;
        if (!ui) return;
        if (t === 0) ui.SendMessage('BridgeManager', 'OnReviveSuccess');
        else ui.SendMessage('BridgeManager', 'OnSpeedBoostAdSuccess');
    };

    window.onAdFailedFromAndroid = function() {
        if (typeof window.resumeAudioAfterAd === 'function') window.resumeAudioAfterAd();
        if (typeof window._showAdUnavailableToast === 'function') window._showAdUnavailableToast();
    };

    window.onRewardedAdReadyFromAndroid = function() {
        console.log('[Android] Rewarded ad ready');
    };

    // ── Android IAP 콜백 ──
    window.onPurchaseSuccessFromAndroid = function(productId, token) {
        if (window.unityInstance) window.unityInstance.SendMessage('BridgeManager', 'OnPurchaseSuccess', productId + '|' + token);
    };
    window.onPurchaseFailedFromAndroid = function(productId, responseCode) {
        if (window.unityInstance) window.unityInstance.SendMessage('BridgeManager', 'OnPurchaseFailed', productId + '|' + responseCode);
    };
    window.onPurchaseCancelledFromAndroid = function(productId) {
        if (window.unityInstance) window.unityInstance.SendMessage('BridgeManager', 'OnPurchaseCancelled', productId);
    };
    window.onPurchaseRestoredFromAndroid = function(productId, token) {
        if (window.unityInstance) window.unityInstance.SendMessage('BridgeManager', 'OnPurchaseRestored', productId + '|' + token);
    };

    // ── 기존 토스 함수 오버라이드 ──
    // 리바이브 (광고 보고 이어서 하기) → Android 보상형 광고
    var _origOnReviveClicked = window.onReviveClicked;
    window._androidReviveOverride = function() {
        var ui = window.unityInstance;
        if (!ui) return;
        // 프리미엄 유저 → 바로 부활
        if (window._premiumSpeedOwned) {
            ui.SendMessage('BridgeManager', 'OnReviveSuccess');
            return;
        }
        // Android 보상형 광고
        window._showAndroidRewardedAd(0);
    };

    // 전면 광고 오버라이드
    var _origShowInterstitialThen = window._showInterstitialThen;
    window._showInterstitialThen = function(cb) {
        if (window._premiumSpeedOwned) { cb(); return; }
        window._showAndroidInterstitialAd(cb);
    };

    // 스피드부스트 광고 → Android 보상형
    // sc-btn-ad 버튼의 onclick에서 SendMessage 대신 직접 호출하도록
    // showSpeedBoostAdFromUnity도 오버라이드
    window.showSpeedBoostAdFromUnity = function() {
        window._showAndroidRewardedAd(1);
    };

    // IAP 결제 → Android Google Play Billing
    var _origPurchasePremiumSpeed = window._purchasePremiumSpeed;
    window._purchasePremiumSpeed = function() {
        var productId = 'remove_ads_hint_pack';
        if (window.AndroidBridge && typeof window.AndroidBridge.launchPurchase === 'function') {
            window.AndroidBridge.launchPurchase(productId);
            return;
        }
        // 폴백
        if (_origPurchasePremiumSpeed) _origPurchasePremiumSpeed();
    };

    console.log('[Android] Bridge patch loaded');
})();
</script>
ANDROID_PATCH

echo "[PATCH] 2/6 Android Bridge 콜백 삽입"

# ── 3. 리바이브 버튼 onclick을 Android 오버라이드로 변경 ──
# onReviveClicked() 내부의 SendMessage('BridgeManager', 'RequestAd') → _androidReviveOverride()
sed -i '' "s|SendMessage('BridgeManager', 'RequestAd')|window._androidReviveOverride ? window._androidReviveOverride() : SendMessage('BridgeManager', 'RequestAd'); return|g" "$INDEX"
echo "[PATCH] 3/6 리바이브 광고 호출 패치"

# ── 4. 스피드부스트 광고 버튼 패치 ──
sed -i '' "s|SendMessage('BridgeManager', 'RequestSpeedBoostAd')|window._showAndroidRewardedAd(1)|g" "$INDEX"
echo "[PATCH] 4/6 스피드부스트 광고 패치"

# ── 5. 공유 AndroidBridge 폴백 추가 ──
# navigator.share 없을 때 AndroidBridge.shareText 사용
# (이건 기존 코드에 이미 있으면 스킵)

echo "[PATCH] 5/6 공유 기능 (기존 유지)"

# ── 6. localStorage 키 확인 ──
# animalpop_best 사용 확인 (dessertpop_best가 있으면 교체)
sed -i '' "s|dessertpop_best|animalpop_best|g" "$INDEX"
echo "[PATCH] 6/6 localStorage 키 통일"

echo "[PATCH] ✅ Android 패치 완료!"
