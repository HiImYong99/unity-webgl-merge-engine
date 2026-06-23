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

import java.io.InputStream;
import java.util.HashMap;
import java.util.Map;

import android.widget.FrameLayout;
import android.view.Gravity;

import com.google.android.gms.ads.AdError;
import com.google.android.gms.ads.AdRequest;
import com.google.android.gms.ads.AdSize;
import com.google.android.gms.ads.AdView;
import com.google.android.gms.ads.FullScreenContentCallback;
import com.google.android.gms.ads.LoadAdError;
import com.google.android.gms.ads.MobileAds;
import com.google.android.gms.ads.interstitial.InterstitialAd;
import com.google.android.gms.ads.interstitial.InterstitialAdLoadCallback;
import com.google.android.gms.ads.rewarded.RewardedAd;
import com.google.android.gms.ads.rewarded.RewardedAdLoadCallback;
import com.google.android.ump.ConsentInformation;
import com.google.android.ump.ConsentRequestParameters;
import com.google.android.ump.UserMessagingPlatform;
import android.util.DisplayMetrics;
import java.util.concurrent.atomic.AtomicBoolean;

/**
 * Animal Pop – 메인 액티비티
 * ─ WebView에 Unity WebGL 로드
 * ─ AdMob 전면(Interstitial) / 보상형(Rewarded) 광고 연동
 * ─ JavaScript Interface로 WebGL ↔ 네이티브 양방향 통신
 *
 * ★ 배포 전 교체 필수:
 *   INTERSTITIAL_AD_UNIT_ID → AdMob 콘솔 전면 광고 단위 ID
 *   REWARDED_AD_UNIT_ID     → AdMob 콘솔 보상형 광고 단위 ID
 *   (실제 AdMob ID 사용 중)
 */
public class MainActivity extends Activity {

    private static final String TAG = "AnimalPop";

    // ── AdMob 광고 단위 ID ──────────────────────────────────────
    // AdMob 실제 광고 단위 ID
    private static final String INTERSTITIAL_AD_UNIT_ID = "ca-app-pub-4036435726138230/6160074039";
    private static final String REWARDED_AD_UNIT_ID     = "ca-app-pub-4036435726138230/3533910694";
    private static final String BANNER_AD_UNIT_ID       = "ca-app-pub-4036435726138230/2224698459"; // 실제 배너
    // ────────────────────────────────────────────────────────────

    private WebView webView;
    private final Handler mainHandler = new Handler(Looper.getMainLooper());
    private WebViewAssetLoader assetLoader;

    private InterstitialAd interstitialAd;
    private RewardedAd     rewardedAd;
    private AdView         bannerAdView;
    private BillingManager billingManager;
    private PlayGamesManager playGames;
    private ConsentInformation consentInformation;
    private final AtomicBoolean adsInitialized = new AtomicBoolean(false);
    private boolean pageLoaded = false;

    @Override
    protected void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);

        // 풀스크린 + 화면 항상 켜짐
        getWindow().addFlags(WindowManager.LayoutParams.FLAG_KEEP_SCREEN_ON);
        getWindow().addFlags(WindowManager.LayoutParams.FLAG_FULLSCREEN);

        // FrameLayout: WebView(전체) + Banner(하단)
        FrameLayout rootLayout = new FrameLayout(this);

        webView = new WebView(this);
        rootLayout.addView(webView, new FrameLayout.LayoutParams(
            FrameLayout.LayoutParams.MATCH_PARENT,
            FrameLayout.LayoutParams.MATCH_PARENT));

        // 배너 광고 (하단 고정)
        bannerAdView = new AdView(this);
        bannerAdView.setAdUnitId(BANNER_AD_UNIT_ID);
        bannerAdView.setAdSize(getAdaptiveBannerAdSize());
        FrameLayout.LayoutParams bannerParams = new FrameLayout.LayoutParams(
            FrameLayout.LayoutParams.MATCH_PARENT,
            FrameLayout.LayoutParams.WRAP_CONTENT);
        bannerParams.gravity = Gravity.BOTTOM | Gravity.CENTER_HORIZONTAL;
        rootLayout.addView(bannerAdView, bannerParams);

        setContentView(rootLayout);

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

        // UMP(EEA/GDPR) 동의 → canRequestAds일 때만 AdMob 시작 (iOS GameViewController와 동일 흐름)
        consentInformation = UserMessagingPlatform.getConsentInformation(this);
        ConsentRequestParameters consentParams = new ConsentRequestParameters.Builder().build();
        consentInformation.requestConsentInfoUpdate(this, consentParams,
            () -> UserMessagingPlatform.loadAndShowConsentFormIfRequired(this, formError -> {
                if (formError != null) Log.w(TAG, "[Consent] form: " + formError.getMessage());
                if (consentInformation.canRequestAds()) initializeAdsOnce();
            }),
            requestError -> {
                Log.w(TAG, "[Consent] info update failed: " + requestError.getMessage());
                if (consentInformation.canRequestAds()) initializeAdsOnce();
            });
        // 재방문(이미 동의 처리됨)이면 폼 없이 즉시 시작 (initializeAdsOnce가 중복 방지)
        if (consentInformation.canRequestAds()) initializeAdsOnce();

        // Play Games Services(리더보드) 초기화 — 사인인 성공 시 JS에 통지하여 리더보드 버튼 노출
        playGames = new PlayGamesManager(this, authenticated -> {
            if (authenticated) callJs("window.onPlayGamesReadyFromAndroid && window.onPlayGamesReadyFromAndroid()");
        });
        playGames.init();

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
                String path = request.getUrl().getPath();

                // .br (Brotli) 파일: Content-Encoding: br 헤더 추가
                // Unity WebGL 로더는 서버가 이 헤더를 보내야 Brotli 압축 해제를 수행
                if (path != null && path.endsWith(".br")) {
                    WebResourceResponse response = assetLoader.shouldInterceptRequest(request.getUrl());
                    if (response != null) {
                        // 원본 MIME에서 .br 제거한 실제 콘텐츠 타입 결정
                        String mimeType = "application/octet-stream";
                        if (path.endsWith(".js.br")) mimeType = "application/javascript";
                        else if (path.endsWith(".wasm.br")) mimeType = "application/wasm";
                        else if (path.endsWith(".data.br")) mimeType = "application/octet-stream";
                        else if (path.endsWith(".json.br")) mimeType = "application/json";

                        Map<String, String> headers = new HashMap<>(response.getResponseHeaders() != null
                                ? response.getResponseHeaders() : new HashMap<>());
                        headers.put("Content-Encoding", "br");

                        return new WebResourceResponse(
                                mimeType,
                                "UTF-8",
                                200,
                                "OK",
                                headers,
                                response.getData()
                        );
                    }
                }

                return assetLoader.shouldInterceptRequest(request.getUrl());
            }
            @Override
            public void onPageFinished(WebView view, String url) {
                applyImmersiveMode();
                pageLoaded = true;
                // 페이지 로드 완료 후 기구매 복원 (JS 함수가 정의된 뒤에 호출)
                if (billingManager != null) {
                    mainHandler.postDelayed(() -> billingManager.restorePurchases(), 500);
                }
                // PGS 사인인이 페이지 로드 전에 완료된 경우(캐시된 인증) JS 핸들러가 없어 콜백이 유실됨 → 여기서 재통지
                if (playGames != null && playGames.isAuthenticated()) {
                    callJs("window.onPlayGamesReadyFromAndroid && window.onPlayGamesReadyFromAndroid()");
                }
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
            Log.d(TAG, "[Bridge] showInterstitialAd requested, ready=" + (interstitialAd != null));
            mainHandler.post(() -> {
                if (interstitialAd != null) {
                    interstitialAd.setFullScreenContentCallback(new FullScreenContentCallback() {
                        @Override
                        public void onAdShowedFullScreenContent() {
                            Log.d(TAG, "[AD] Interstitial SHOWED");
                        }
                        @Override
                        public void onAdImpression() {
                            Log.d(TAG, "[AD] Interstitial IMPRESSION");
                        }
                        @Override
                        public void onAdClicked() {
                            Log.d(TAG, "[AD] Interstitial CLICKED");
                        }
                        @Override
                        public void onAdDismissedFullScreenContent() {
                            Log.d(TAG, "[AD] Interstitial DISMISSED");
                            interstitialAd = null;
                            loadInterstitialAd();
                            callJs("onInterstitialClosedFromAndroid()");
                        }
                        @Override
                        public void onAdFailedToShowFullScreenContent(AdError e) {
                            Log.w(TAG, "[AD] Interstitial FAILED TO SHOW: " + e.getMessage());
                            interstitialAd = null;
                            loadInterstitialAd();
                            callJs("onInterstitialClosedFromAndroid()");
                        }
                    });
                    interstitialAd.setImmersiveMode(true);
                    interstitialAd.show(MainActivity.this);
                    Log.d(TAG, "[AD] .show() called");
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
                    final boolean[] rewardEarned = {false};
                    rewardedAd.setFullScreenContentCallback(new FullScreenContentCallback() {
                        @Override
                        public void onAdShowedFullScreenContent() {
                            Log.d(TAG, "[AD] Rewarded SHOWED");
                        }
                        @Override
                        public void onAdDismissedFullScreenContent() {
                            Log.d(TAG, "[AD] Rewarded DISMISSED (rewardEarned=" + rewardEarned[0] + ")");
                            rewardedAd = null;
                            loadRewardedAd();
                            if (!rewardEarned[0]) {
                                callJs("onAdFailedFromAndroid()");
                            }
                        }
                        @Override
                        public void onAdFailedToShowFullScreenContent(AdError e) {
                            Log.w(TAG, "[AD] Rewarded FAILED TO SHOW: " + e.getMessage());
                            rewardedAd = null;
                            loadRewardedAd();
                            callJs("onAdFailedFromAndroid()");
                        }
                    });
                    rewardedAd.setImmersiveMode(true);
                    rewardedAd.show(MainActivity.this, rewardItem -> {
                        Log.d(TAG, "User earned reward: " + rewardItem.getAmount() + " " + rewardItem.getType());
                        rewardEarned[0] = true;
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

        // ── Play Games Services 리더보드 (iOS Game Center 대응) ───────────

        /** 게임오버 점수를 PGS 리더보드에 제출 (window.GameBridge.submitScore) */
        @JavascriptInterface
        public void submitScore(long score) {
            mainHandler.post(() -> { if (playGames != null) playGames.submitScore(score); });
        }

        /** 리더보드 버튼 → PGS 리더보드 UI 표시 */
        @JavascriptInterface
        public void showLeaderboard() {
            mainHandler.post(() -> { if (playGames != null) playGames.showLeaderboard(); });
        }

        /** 리더보드 사용 가능(사인인 완료) 여부 */
        @JavascriptInterface
        public boolean isLeaderboardReady() {
            return playGames != null && playGames.isAuthenticated();
        }
    }

    // ══════════════════════════════════════════════════════
    //  AdMob 로드 메서드
    // ══════════════════════════════════════════════════════

    /** AdMob SDK 1회 초기화 + 광고 프리로드 (UMP 동의 통과 후 호출; AtomicBoolean으로 중복 방지). */
    private void initializeAdsOnce() {
        if (!adsInitialized.compareAndSet(false, true)) return;
        MobileAds.initialize(this, initStatus -> {
            Log.d(TAG, "AdMob initialized");
            loadInterstitialAd();
            loadRewardedAd();
            bannerAdView.loadAd(new AdRequest.Builder().build());
        });
    }

    /** 화면 너비 기반 anchored adaptive 배너 크기 (고정 320×50 대비 fill/eCPM↑). */
    private AdSize getAdaptiveBannerAdSize() {
        DisplayMetrics dm = getResources().getDisplayMetrics();
        int adWidthDp = Math.round(dm.widthPixels / dm.density);
        return AdSize.getCurrentOrientationAnchoredAdaptiveBannerAdSize(this, adWidthDp);
    }

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

    private static final long REWARDED_RETRY_DELAY_MS = 5000L;
    private int rewardedRetryCount = 0;

    private void loadRewardedAd() {
        AdRequest req = new AdRequest.Builder().build();
        RewardedAd.load(this, REWARDED_AD_UNIT_ID, req, new RewardedAdLoadCallback() {
            @Override
            public void onAdLoaded(RewardedAd ad) {
                Log.d(TAG, "Rewarded ad loaded");
                rewardedAd = ad;
                rewardedRetryCount = 0;
                callJs("onRewardedAdReadyFromAndroid()");
            }
            @Override
            public void onAdFailedToLoad(LoadAdError e) {
                Log.w(TAG, "Rewarded ad load failed (attempt " + rewardedRetryCount + "): " + e.getMessage());
                rewardedAd = null;
                if (rewardedRetryCount < 3) {
                    rewardedRetryCount++;
                    long delay = REWARDED_RETRY_DELAY_MS * rewardedRetryCount;
                    Log.d(TAG, "Retrying rewarded ad in " + delay + "ms");
                    mainHandler.postDelayed(() -> loadRewardedAd(), delay);
                }
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

    // ── 리더보드 등 외부 액티비티 복귀 시 몰입 모드 복원 ──────────────────
    @Override
    protected void onActivityResult(int requestCode, int resultCode, android.content.Intent data) {
        super.onActivityResult(requestCode, resultCode, data);
        applyImmersiveMode();
    }

    // ── 백그라운드 진입 시 오디오 정지 ──────────────────────────────────
    @Override
    protected void onPause() {
        super.onPause();
        if (webView != null) {
            // Web Audio API suspend → Unity BGM 즉시 정지
            webView.evaluateJavascript(
                "try { if(typeof Module!=='undefined' && Module.WEBAudio && Module.WEBAudio.audioContext)" +
                "  Module.WEBAudio.audioContext.suspend(); } catch(e){}", null);
            webView.onPause();
        }
        if (bannerAdView != null) bannerAdView.pause();
    }

    @Override
    protected void onResume() {
        super.onResume();
        applyImmersiveMode();
        if (bannerAdView != null) bannerAdView.resume();
        if (webView != null) {
            webView.onResume();
            // Web Audio API resume → BGM 재개
            webView.evaluateJavascript(
                "try { if(typeof Module!=='undefined' && Module.WEBAudio && Module.WEBAudio.audioContext)" +
                "  Module.WEBAudio.audioContext.resume(); } catch(e){}", null);
        }
        if (billingManager != null && pageLoaded) billingManager.restorePurchases();
    }

    @Override
    protected void onDestroy() {
        if (bannerAdView != null) bannerAdView.destroy();
        if (webView != null) webView.destroy();
        if (billingManager != null) billingManager.destroy();
        super.onDestroy();
    }
}
