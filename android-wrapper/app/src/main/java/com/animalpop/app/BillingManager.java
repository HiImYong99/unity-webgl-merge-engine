package com.animalpop.app;

import android.app.Activity;
import android.os.Handler;
import android.os.Looper;
import android.util.Log;

import androidx.annotation.NonNull;

import com.android.billingclient.api.AcknowledgePurchaseParams;
import com.android.billingclient.api.BillingClient;
import com.android.billingclient.api.BillingClientStateListener;
import com.android.billingclient.api.BillingFlowParams;
import com.android.billingclient.api.BillingResult;
import com.android.billingclient.api.PendingPurchasesParams;
import com.android.billingclient.api.ProductDetails;
import com.android.billingclient.api.Purchase;
import com.android.billingclient.api.PurchasesUpdatedListener;
import com.android.billingclient.api.QueryProductDetailsParams;
import com.android.billingclient.api.QueryPurchasesParams;

import java.util.Collections;
import java.util.List;

/**
 * Google Play 인앱 결제 관리자
 *
 * 상품 구성:
 *   PRODUCT_ID_PREMIUM_PACK (990원)
 *   보상: 광고 완벽 제거 + 힌트 아이템 10개 지급
 *
 * 사용 흐름:
 *   1. new BillingManager(activity, callback) → 자동으로 GP 연결
 *   2. launchPurchaseFlow(PRODUCT_ID_PREMIUM_PACK)
 *   3. 결과는 BillingCallback 으로 수신
 *   4. onResume() 마다 restorePurchases() 호출 권장
 */
public class BillingManager implements PurchasesUpdatedListener {

    private static final String TAG = "BillingManager";

    // SERVICE_DISCONNECTED 시 단순 재시도 (공식 권장: 유저 발동 액션은 짧은 재시도)
    private static final int  MAX_RETRY      = 1;
    private static final long RETRY_DELAY_MS = 2000L;

    // ── 상품 ID (구글 플레이 콘솔 등록 ID와 일치해야 함) ──────────────
    public static final String PRODUCT_ID_PREMIUM_PACK = "remove_ads_hint_pack";
    // ────────────────────────────────────────────────────────────────

    private final Activity        activity;
    private final BillingCallback callback;
    private final Handler         mainHandler = new Handler(Looper.getMainLooper());
    private       BillingClient   billingClient;

    // 현재 구매 시도 중인 ProductDetails (launchBillingFlow에 필요)
    private ProductDetails pendingProductDetails;

    // ── 콜백 인터페이스 ──────────────────────────────────────────────
    public interface BillingCallback {
        /** 결제 성공 + 이미 보상 미지급 구매 복원 시 호출 */
        void onPurchaseGranted(String productId, String purchaseToken);
        /** 결제 실패 (responseCode: BillingClient.BillingResponseCode) */
        void onPurchaseFailed(String productId, int responseCode, String debugMsg);
        /** 유저가 결제 창을 닫음(취소) */
        void onPurchaseCancelled(String productId);
        /** 이미 구매한 상품 복원 완료 */
        void onPurchaseRestored(String productId, String purchaseToken);
    }

    // ── 생성자 ──────────────────────────────────────────────────────
    public BillingManager(@NonNull Activity activity, @NonNull BillingCallback callback) {
        this.activity = activity;
        this.callback = callback;
        buildAndConnect();
    }

    // ── BillingClient 초기화 및 GP 연결 ────────────────────────────
    private void buildAndConnect() {
        billingClient = BillingClient.newBuilder(activity)
            .setListener(this)
            // PBL 8+ : 파라미터 없는 enablePendingPurchases() 제거됨
            .enablePendingPurchases(
                PendingPurchasesParams.newBuilder()
                    .enableOneTimeProducts()
                    .build())
            // 연결 끊김 시 SDK가 지수 백오프로 자동 재연결 (PBL 7.1+)
            .enableAutoServiceReconnection()
            .build();

        billingClient.startConnection(new BillingClientStateListener() {
            @Override
            public void onBillingSetupFinished(@NonNull BillingResult result) {
                if (result.getResponseCode() == BillingClient.BillingResponseCode.OK) {
                    Log.d(TAG, "BillingClient 연결 성공");
                    // 연결 직후 미처리 구매 확인 (앱 재시작 시 보상 누락 방지)
                    restorePurchases();
                } else {
                    Log.w(TAG, "BillingClient 연결 실패: " + result.getDebugMessage());
                }
            }

            @Override
            public void onBillingServiceDisconnected() {
                // enableAutoServiceReconnection() 으로 SDK가 자동 재연결하므로 로그만 남김
                Log.w(TAG, "BillingClient 연결 끊김 – SDK 자동 재연결 대기");
            }
        });
    }

    // ── 결제 흐름 시작 ──────────────────────────────────────────────
    /**
     * 구매 바텀시트를 표시한다.
     * 내부적으로 상품 조회 후 자동으로 BillingFlow를 실행한다.
     */
    public void launchPurchaseFlow(@NonNull String productId) {
        queryAndLaunch(productId, 0);
    }

    /**
     * isReady() 로 먼저 걸러내지 않는다 — enableAutoServiceReconnection() 이
     * '호출 시점'에 재연결을 시도하므로, 미준비 상태여도 그대로 호출해야 복구된다.
     * 재연결 후에도 SERVICE_DISCONNECTED면 공식 권장대로 단순 재시도.
     */
    private void queryAndLaunch(@NonNull String productId, int attempt) {
        if (!billingClient.isReady()) {
            Log.w(TAG, "BillingClient 미준비 – SDK 자동 재연결에 맡기고 호출 진행");
        }

        QueryProductDetailsParams params = QueryProductDetailsParams.newBuilder()
            .setProductList(Collections.singletonList(
                QueryProductDetailsParams.Product.newBuilder()
                    .setProductId(productId)
                    .setProductType(BillingClient.ProductType.INAPP)
                    .build()
            ))
            .build();

        // PBL 8+ : 콜백 2번째 인자가 List<ProductDetails> → QueryProductDetailsResult 로 변경
        billingClient.queryProductDetailsAsync(params, (billingResult, productDetailsResult) -> {
            List<ProductDetails> productDetailsList = productDetailsResult == null
                ? null : productDetailsResult.getProductDetailsList();

            if (billingResult.getResponseCode() != BillingClient.BillingResponseCode.OK
                    || productDetailsList == null || productDetailsList.isEmpty()) {
                if (billingResult.getResponseCode()
                        == BillingClient.BillingResponseCode.SERVICE_DISCONNECTED
                        && attempt < MAX_RETRY) {
                    Log.w(TAG, "연결 끊김 – 상품 조회 재시도 " + (attempt + 1) + "/" + MAX_RETRY);
                    mainHandler.postDelayed(
                        () -> queryAndLaunch(productId, attempt + 1), RETRY_DELAY_MS);
                    return;
                }
                Log.e(TAG, "상품 조회 실패: " + billingResult.getDebugMessage()
                    + (productDetailsResult == null ? ""
                       : " / unfetched=" + productDetailsResult.getUnfetchedProductList()));
                callback.onPurchaseFailed(productId,
                    billingResult.getResponseCode(),
                    billingResult.getDebugMessage());
                return;
            }

            pendingProductDetails = productDetailsList.get(0);
            activity.runOnUiThread(() -> {
                BillingFlowParams flowParams = BillingFlowParams.newBuilder()
                    .setProductDetailsParamsList(Collections.singletonList(
                        BillingFlowParams.ProductDetailsParams.newBuilder()
                            .setProductDetails(pendingProductDetails)
                            .build()
                    ))
                    .build();

                BillingResult launchResult = billingClient.launchBillingFlow(activity, flowParams);
                if (launchResult.getResponseCode() != BillingClient.BillingResponseCode.OK) {
                    Log.e(TAG, "BillingFlow 실행 실패: " + launchResult.getDebugMessage());
                    callback.onPurchaseFailed(productId,
                        launchResult.getResponseCode(),
                        launchResult.getDebugMessage());
                }
            });
        });
    }

    // ── 구매 결과 수신 (PurchasesUpdatedListener) ────────────────────
    @Override
    public void onPurchasesUpdated(@NonNull BillingResult billingResult,
                                    List<Purchase> purchases) {
        int code = billingResult.getResponseCode();

        if (code == BillingClient.BillingResponseCode.OK && purchases != null) {
            for (Purchase purchase : purchases) {
                handlePurchase(purchase, false);
            }
        } else if (code == BillingClient.BillingResponseCode.USER_CANCELED) {
            String productId = pendingProductDetails != null
                ? pendingProductDetails.getProductId() : "unknown";
            Log.d(TAG, "유저 결제 취소: " + productId);
            callback.onPurchaseCancelled(productId);
        } else {
            String productId = pendingProductDetails != null
                ? pendingProductDetails.getProductId() : "unknown";
            Log.w(TAG, "결제 실패 [" + code + "]: " + billingResult.getDebugMessage());
            callback.onPurchaseFailed(productId, code, billingResult.getDebugMessage());
        }
    }

    // ── 구매 처리 (승인 + 콜백) ─────────────────────────────────────
    private void handlePurchase(@NonNull Purchase purchase, boolean isRestore) {
        // PURCHASED 상태만 처리 (PENDING은 결제 완료 대기 중)
        if (purchase.getPurchaseState() != Purchase.PurchaseState.PURCHASED) {
            Log.d(TAG, "구매 대기 중 (PENDING): " + purchase.getOrderId());
            return;
        }

        String productId = purchase.getProducts().isEmpty()
            ? "unknown" : purchase.getProducts().get(0);

        // 미승인 구매 → 승인 처리 (3일 내 미승인 시 자동 환불)
        if (!purchase.isAcknowledged()) {
            AcknowledgePurchaseParams ackParams = AcknowledgePurchaseParams.newBuilder()
                .setPurchaseToken(purchase.getPurchaseToken())
                .build();

            billingClient.acknowledgePurchase(ackParams, ackResult -> {
                if (ackResult.getResponseCode() == BillingClient.BillingResponseCode.OK) {
                    Log.d(TAG, "구매 승인 완료: " + productId);
                    if (isRestore) {
                        callback.onPurchaseRestored(productId, purchase.getPurchaseToken());
                    } else {
                        callback.onPurchaseGranted(productId, purchase.getPurchaseToken());
                    }
                } else {
                    Log.e(TAG, "구매 승인 실패: " + ackResult.getDebugMessage());
                    callback.onPurchaseFailed(productId,
                        ackResult.getResponseCode(), ackResult.getDebugMessage());
                }
            });
        } else {
            // 이미 승인된 구매 (앱 재시작 시 복원)
            if (isRestore) {
                callback.onPurchaseRestored(productId, purchase.getPurchaseToken());
            }
        }
    }

    // ── 구매 복원 (앱 재시작 시 미지급 보상 처리) ─────────────────────
    /**
     * onResume() 또는 BillingClient 연결 완료 시 호출하여
     * 이전에 구매했으나 보상이 누락된 케이스를 복구한다.
     */
    public void restorePurchases() {
        // isReady() 게이트 없음 — 미준비 상태면 SDK가 자동 재연결 후 호출을 처리한다
        billingClient.queryPurchasesAsync(
            QueryPurchasesParams.newBuilder()
                .setProductType(BillingClient.ProductType.INAPP)
                .build(),
            (billingResult, purchases) -> {
                if (billingResult.getResponseCode() != BillingClient.BillingResponseCode.OK) {
                    Log.w(TAG, "구매 복원 조회 실패 [" + billingResult.getResponseCode() + "]: "
                        + billingResult.getDebugMessage());
                    return;
                }
                for (Purchase purchase : purchases) {
                    handlePurchase(purchase, true);
                }
            }
        );
    }

    // ── 생명주기 ────────────────────────────────────────────────────
    public void destroy() {
        // 예약된 재시도가 endConnection() 이후 실행되면 IllegalStateException / Activity 누수
        mainHandler.removeCallbacksAndMessages(null);
        if (billingClient != null && billingClient.isReady()) {
            billingClient.endConnection();
        }
    }
}
