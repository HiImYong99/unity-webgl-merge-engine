package com.animalpop.app;

import android.app.Activity;
import android.util.Log;

import androidx.annotation.NonNull;

import com.android.billingclient.api.AcknowledgePurchaseParams;
import com.android.billingclient.api.BillingClient;
import com.android.billingclient.api.BillingClientStateListener;
import com.android.billingclient.api.BillingFlowParams;
import com.android.billingclient.api.BillingResult;
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

    // ── 상품 ID (구글 플레이 콘솔 등록 ID와 일치해야 함) ──────────────
    public static final String PRODUCT_ID_PREMIUM_PACK = "remove_ads_hint_pack";
    // ────────────────────────────────────────────────────────────────

    private final Activity        activity;
    private final BillingCallback callback;
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
            .enablePendingPurchases()
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
                Log.w(TAG, "BillingClient 연결 끊김 – 재연결 시도");
                // 간단한 재연결 (실제 배포 시 지수 백오프 권장)
                buildAndConnect();
            }
        });
    }

    // ── 결제 흐름 시작 ──────────────────────────────────────────────
    /**
     * 구매 바텀시트를 표시한다.
     * 내부적으로 상품 조회 후 자동으로 BillingFlow를 실행한다.
     */
    public void launchPurchaseFlow(@NonNull String productId) {
        if (!billingClient.isReady()) {
            Log.w(TAG, "BillingClient 미준비 – 재연결 후 재시도");
            buildAndConnect();
            callback.onPurchaseFailed(productId,
                BillingClient.BillingResponseCode.SERVICE_DISCONNECTED,
                "BillingClient not ready");
            return;
        }

        QueryProductDetailsParams params = QueryProductDetailsParams.newBuilder()
            .setProductList(Collections.singletonList(
                QueryProductDetailsParams.Product.newBuilder()
                    .setProductId(productId)
                    .setProductType(BillingClient.ProductType.INAPP)
                    .build()
            ))
            .build();

        billingClient.queryProductDetailsAsync(params, (billingResult, productDetailsList) -> {
            if (billingResult.getResponseCode() != BillingClient.BillingResponseCode.OK
                    || productDetailsList == null || productDetailsList.isEmpty()) {
                Log.e(TAG, "상품 조회 실패: " + billingResult.getDebugMessage());
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
        if (!billingClient.isReady()) return;

        billingClient.queryPurchasesAsync(
            QueryPurchasesParams.newBuilder()
                .setProductType(BillingClient.ProductType.INAPP)
                .build(),
            (billingResult, purchases) -> {
                if (billingResult.getResponseCode() != BillingClient.BillingResponseCode.OK) return;
                for (Purchase purchase : purchases) {
                    handlePurchase(purchase, true);
                }
            }
        );
    }

    // ── 생명주기 ────────────────────────────────────────────────────
    public void destroy() {
        if (billingClient != null && billingClient.isReady()) {
            billingClient.endConnection();
        }
    }
}
