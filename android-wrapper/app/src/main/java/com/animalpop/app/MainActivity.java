package com.animalpop.app;

import android.app.Activity;
import android.os.Bundle;
import android.os.Handler;
import android.os.Looper;
import android.util.Log;
import android.view.View;
import android.view.WindowManager;
import android.webkit.ConsoleMessage;
import android.webkit.JavascriptInterface;
import android.webkit.WebChromeClient;
import android.webkit.WebResourceRequest;
import android.webkit.WebResourceResponse;
import android.webkit.WebSettings;
import android.webkit.WebView;
import android.webkit.WebViewClient;
import androidx.webkit.WebViewAssetLoader;

import com.google.android.gms.ads.AdError;
import com.google.android.gms.ads.AdRequest;
import com.google.android.gms.ads.FullScreenContentCallback;
import com.google.android.gms.ads.LoadAdError;
import com.google.android.gms.ads.MobileAds;
import com.google.android.gms.ads.interstitial.InterstitialAd;
import com.google.android.gms.ads.interstitial.InterstitialAdLoadCallback;
import com.google.android.gms.ads.rewarded.RewardedAd;
import com.google.android.gms.ads.rewarded.RewardedAdLoadCallback;

/**
 * Animal Pop – 메인 액티비티
 * ─ WebView에 Unity WebGL 로드
 * ─ AdMob 전면(Interstitial) / 보상형(Rewarded) 광고 연동
 * ─ JavaScript Interface로 WebGL ↔ 네이티브 양방향 통신
 *
 * ★ 배포 전 교체 필수:
 *   INTERSTITIAL_AD_UNIT_ID → AdMob 콘솔 전면 광고 단위 ID
 *   REWARDED_AD_UNIT_ID     → AdMob 콘솔 보상형 광고 단위 ID
 *   (현재는 Google 공식 테스트 ID 사용)
 */
public class MainActivity extends Activity {

    private static final String TAG = "AnimalPop";

    // ── AdMob 광고 단위 ID (테스트 ID) ──────────────────────────
    // 실제 배포 시 AdMob 콘솔의 실제 ID로 교체하세요.
    private static final String INTERSTITIAL_AD_UNIT_ID = "ca-app-pub-2371797890397990/8608686822";
    private static final String REWARDED_AD_UNIT_ID     = "ca-app-pub-2371797890397990/7714912138";
    // ────────────────────────────────────────────────────────────

    private WebView webView;
    private final Handler mainHandler = new Handler(Looper.getMainLooper());
    private WebViewAssetLoader assetLoader;

    private InterstitialAd interstitialAd;
    private RewardedAd     rewardedAd;
    private BillingManager billingManager;

    @Override
    protected void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);

        // 풀스크린 + 화면 항상 켜짐
        getWindow().addFlags(WindowManager.LayoutParams.FLAG_KEEP_SCREEN_ON);
        getWindow().addFlags(WindowManager.LayoutParams.FLAG_FULLSCREEN);

        webView = new WebView(this);
        setContentView(webView);

        applyImmersiveMode();

        // WebViewAssetLoader: assets/ 를 https://appassets.androidplatform.net/assets/ 로 서빙
        // → WASM 파일에 올바른 MIME 타입 부여, file:// 제한 우회
        assetLoader = new WebViewAssetLoader.Builder()
            .addPathHandler("/assets/", new WebViewAssetLoader.AssetsPathHandler(this))
            .build();

        setupWebView();

        // Google Play Billing 초기화
        billingManager = new BillingManager(this, new BillingManager.BillingCallback() {
            @Override
            public void onPurchaseGranted(String productId, String purchaseToken) {
                Log.d(TAG, "결제 성공: " + productId);
                callJs("onPurchaseSuccessFromAndroid(" +
                    "'" + productId + "','" + purchaseToken + "')");
            }
            @Override
            public void onPurchaseFailed(String productId, int responseCode, String debugMsg) {
                Log.w(TAG, "결제 실패 [" + responseCode + "]: " + debugMsg);
                callJs("onPurchaseFailedFromAndroid(" +
                    "'" + productId + "'," + responseCode + ")");
            }
            @Override
            public void onPurchaseCancelled(String productId) {
                Log.d(TAG, "결제 취소: " + productId);
                callJs("onPurchaseCancelledFromAndroid('" + productId + "')");
            }
            @Override
            public void onPurchaseRestored(String productId, String purchaseToken) {
                Log.d(TAG, "구매 복원: " + productId);
                callJs("onPurchaseRestoredFromAndroid(" +
                    "'" + productId + "','" + purchaseToken + "')");
            }
        });

        // AdMob SDK 초기화 → 광고 프리로드
        MobileAds.initialize(this, initStatus -> {
            Log.d(TAG, "AdMob initialized");
            loadInterstitialAd();
            loadRewardedAd();
        });

        // file:// 대신 로컬 HTTPS로 로드 (WASM streaming 정상 동작)
        webView.loadUrl("https://appassets.androidplatform.net/assets/index.html");
    }

    // ══════════════════════════════════════════════════════
    //  WebView 설정
    // ══════════════════════════════════════════════════════

    private void setupWebView() {
        WebSettings settings = webView.getSettings();
        settings.setJavaScriptEnabled(true);
        settings.setDomStorageEnabled(true);
        settings.setAllowFileAccess(false);
        settings.setAllowFileAccessFromFileURLs(false);
        settings.setAllowUniversalAccessFromFileURLs(false);
        settings.setMediaPlaybackRequiresUserGesture(false);
        settings.setLoadWithOverviewMode(true);
        settings.setUseWideViewPort(true);
        settings.setCacheMode(WebSettings.LOAD_DEFAULT);

        // ── JavaScript Interface 등록 ──
        // JS에서 window.AndroidBridge.xxx() 로 네이티브 메서드 호출
        webView.addJavascriptInterface(new AndroidBridge(), "AndroidBridge");

        webView.setWebViewClient(new WebViewClient() {
            @Override
            public WebResourceResponse shouldInterceptRequest(WebView view, WebResourceRequest request) {
                // assets 요청을 로컬 HTTPS로 인터셉트 → 올바른 MIME 타입 자동 부여
                return assetLoader.shouldInterceptRequest(request.getUrl());
            }
            @Override
            public void onPageFinished(WebView view, String url) {
                applyImmersiveMode();
            }
        });

        webView.setWebChromeClient(new WebChromeClient() {
            @Override
            public boolean onConsoleMessage(ConsoleMessage msg) {
                Log.d(TAG, msg.message() + " [" + msg.sourceId() + ":" + msg.lineNumber() + "]");
                return true;
            }
        });
    }

    // ══════════════════════════════════════════════════════
    //  JavaScript Interface (WebGL → 네이티브 호출)
    // ══════════════════════════════════════════════════════

    /**
     * JavaScript에서 window.AndroidBridge.xxx() 형태로 호출합니다.
     * 모든 @JavascriptInterface 메서드는 백그라운드 스레드에서 실행되므로
     * UI 작업은 반드시 mainHandler.post()로 메인 스레드로 전환하세요.
     */
    private class AndroidBridge {

        /** 게임 오버 시 전면 광고 요청 */
        @JavascriptInterface
        public void showInterstitialAd() {
            Log.d(TAG, "[Bridge] showInterstitialAd requested");
            mainHandler.post(() -> {
                if (interstitialAd != null) {
                    interstitialAd.setFullScreenContentCallback(new FullScreenContentCallback() {
                        @Override
                        public void onAdDismissedFullScreenContent() {
                            Log.d(TAG, "Interstitial dismissed");
                            interstitialAd = null;
                            loadInterstitialAd(); // 다음 광고 미리 로드
                            callJs("onInterstitialClosedFromAndroid()");
                        }
                        @Override
                        public void onAdFailedToShowFullScreenContent(AdError e) {
                            Log.w(TAG, "Interstitial failed to show: " + e.getMessage());
                            interstitialAd = null;
                            loadInterstitialAd();
                            callJs("onInterstitialClosedFromAndroid()");
                        }
                    });
                    interstitialAd.show(MainActivity.this);
                } else {
                    Log.w(TAG, "Interstitial not ready, skipping");
                    callJs("onInterstitialClosedFromAndroid()");
                }
            });
        }

        /** '광고 보고 계속하기' 버튼 – 보상형 광고 요청 */
        @JavascriptInterface
        public void showRewardedAd() {
            Log.d(TAG, "[Bridge] showRewardedAd requested");
            mainHandler.post(() -> {
                if (rewardedAd != null) {
                    rewardedAd.setFullScreenContentCallback(new FullScreenContentCallback() {
                        @Override
                        public void onAdDismissedFullScreenContent() {
                            Log.d(TAG, "Rewarded dismissed");
                            rewardedAd = null;
                            loadRewardedAd();
                            // 보상은 onUserEarnedReward에서 지급 → 여기선 닫힘만 통지
                        }
                        @Override
                        public void onAdFailedToShowFullScreenContent(AdError e) {
                            Log.w(TAG, "Rewarded failed to show: " + e.getMessage());
                            rewardedAd = null;
                            loadRewardedAd();
                            callJs("onAdFailedFromAndroid()");
                        }
                    });
                    rewardedAd.show(MainActivity.this, rewardItem -> {
                        // 광고 완전 시청 → 보상 지급
                        Log.d(TAG, "User earned reward: " + rewardItem.getAmount() + " " + rewardItem.getType());
                        callJs("onAdRewardedFromAndroid()");
                    });
                } else {
                    Log.w(TAG, "Rewarded ad not ready");
                    callJs("onAdFailedFromAndroid()");
                }
            });
        }

        /** 광고 준비 여부 확인 (JS에서 버튼 활성화/비활성화에 사용) */
        @JavascriptInterface
        public boolean isRewardedAdReady() {
            return rewardedAd != null;
        }

        @JavascriptInterface
        public boolean isInterstitialAdReady() {
            return interstitialAd != null;
        }

        // ── 인앱 결제 ────────────────────────────────────────────────

        /**
         * 결제 바텀시트 표시
         * JS에서: window.AndroidBridge.launchPurchase('remove_ads_hint_pack')
         */
        @JavascriptInterface
        public void launchPurchase(String productId) {
            Log.d(TAG, "[Bridge] launchPurchase: " + productId);
            mainHandler.post(() -> billingManager.launchPurchaseFlow(productId));
        }

        /** 구매 복원 요청 (설정 화면 '구매 복원' 버튼용) */
        @JavascriptInterface
        public void restorePurchases() {
            Log.d(TAG, "[Bridge] restorePurchases requested");
            mainHandler.post(() -> billingManager.restorePurchases());
        }

        /** 상품 ID 상수 노출 (JS에서 하드코딩 제거) */
        @JavascriptInterface
        public String getProductIdPremiumPack() {
            return BillingManager.PRODUCT_ID_PREMIUM_PACK;
        }

        /** 결과 공유 (navigator.share 미지원 기기 폴백용) */
        @JavascriptInterface
        public void shareText(String text) {
            Log.d(TAG, "[Bridge] shareText");
            mainHandler.post(() -> {
                android.content.Intent intent = new android.content.Intent(android.content.Intent.ACTION_SEND);
                intent.setType("text/plain");
                intent.putExtra(android.content.Intent.EXTRA_TEXT, text);
                startActivity(android.content.Intent.createChooser(intent, "결과 공유하기"));
            });
        }

        /** JS → 네이티브 로그 (디버깅용) */
        @JavascriptInterface
        public void logFromJS(String message) {
            Log.d(TAG, "[JS] " + message);
        }
    }

    // ══════════════════════════════════════════════════════
    //  AdMob 로드 메서드
    // ══════════════════════════════════════════════════════

    private void loadInterstitialAd() {
        AdRequest req = new AdRequest.Builder().build();
        InterstitialAd.load(this, INTERSTITIAL_AD_UNIT_ID, req, new InterstitialAdLoadCallback() {
            @Override
            public void onAdLoaded(InterstitialAd ad) {
                Log.d(TAG, "Interstitial loaded");
                interstitialAd = ad;
            }
            @Override
            public void onAdFailedToLoad(LoadAdError e) {
                Log.w(TAG, "Interstitial load failed: " + e.getMessage());
                interstitialAd = null;
            }
        });
    }

    private void loadRewardedAd() {
        AdRequest req = new AdRequest.Builder().build();
        RewardedAd.load(this, REWARDED_AD_UNIT_ID, req, new RewardedAdLoadCallback() {
            @Override
            public void onAdLoaded(RewardedAd ad) {
                Log.d(TAG, "Rewarded ad loaded");
                rewardedAd = ad;
                // 보상형 광고 준비 완료를 JS에 알림 → '계속하기' 버튼 활성화
                callJs("onRewardedAdReadyFromAndroid()");
            }
            @Override
            public void onAdFailedToLoad(LoadAdError e) {
                Log.w(TAG, "Rewarded ad load failed: " + e.getMessage());
                rewardedAd = null;
            }
        });
    }

    // ══════════════════════════════════════════════════════
    //  유틸리티
    // ══════════════════════════════════════════════════════

    /** 네이티브 → JS 콜백 (메인 스레드에서 실행) */
    private void callJs(final String jsCode) {
        mainHandler.post(() -> webView.evaluateJavascript(jsCode, null));
    }

    private void applyImmersiveMode() {
        webView.setSystemUiVisibility(
            View.SYSTEM_UI_FLAG_FULLSCREEN |
            View.SYSTEM_UI_FLAG_HIDE_NAVIGATION |
            View.SYSTEM_UI_FLAG_IMMERSIVE_STICKY |
            View.SYSTEM_UI_FLAG_LAYOUT_STABLE |
            View.SYSTEM_UI_FLAG_LAYOUT_FULLSCREEN |
            View.SYSTEM_UI_FLAG_LAYOUT_HIDE_NAVIGATION
        );
    }

    // ── 뒤로가기: 종료 확인 다이얼로그 ────────────────────────────────
    @Override
    public void onBackPressed() {
        new android.app.AlertDialog.Builder(this)
            .setTitle("게임 종료")
            .setMessage("게임을 종료할까요?")
            .setPositiveButton("종료", (dialog, which) -> finish())
            .setNegativeButton("계속하기", (dialog, which) -> {
                dialog.dismiss();
                applyImmersiveMode(); // 다이얼로그로 벗어난 몰입 모드 복원
            })
            .setCancelable(true)
            .show();
    }

    // ── 백그라운드 진입 시 오디오 정지 ──────────────────────────────────
    @Override
    protected void onPause() {
        super.onPause();
        if (webView != null) {
            // JS 실행 전체 중단 (타이머, 오디오, 애니메이션, 게임루프 모두 정지)
            webView.pauseTimers();
            webView.onPause();
        }
    }

    @Override
    protected void onResume() {
        super.onResume();
        applyImmersiveMode();
        if (webView != null) {
            // JS 실행 재개
            webView.onResume();
            webView.resumeTimers();
        }
        if (billingManager != null) billingManager.restorePurchases();
    }

    @Override
    protected void onDestroy() {
        if (webView != null) webView.destroy();
        if (billingManager != null) billingManager.destroy();
        super.onDestroy();
    }
}
