/**
 * TossBridge.jslib – AppsInToss 공식 API + 게임 엔진 연동 통합 브릿지
 * (WebBridge.jslib과 TossBridge.jslib을 통합함)
 */
mergeInto(LibraryManager.library, {

  // ─────────────────────────────────────────────────
  // 1. 시스템 및 데이터 관리 (Reliability)
  // ─────────────────────────────────────────────────

  // [Issue 2 해결] IndexedDB 비동기 유실 방지를 위한 localStorage 동기 백업
  SyncSaveToLocalStorage: function(keyPtr, valuePtr) {
    var key = UTF8ToString(keyPtr);
    var value = UTF8ToString(valuePtr);
    try {
      localStorage.setItem(key, value);
      console.log('[TossBridge] SyncSave Success:', key);
    } catch(e) {
      console.error('[TossBridge] SyncSave Failed:', e);
    }
  },

  // HTML 랜딩 오버레이 표시
  _ShowHtmlLanding: function(bestScore) {
    var score = bestScore || 0;
    if (typeof window._ShowHtmlLanding === 'function') {
      window._ShowHtmlLanding(score);
    }
    try { localStorage.setItem('animalpop_best', score.toString()); } catch(e) {}
  },

  // ─────────────────────────────────────────────────
  // 2. 인게임 HUD 및 이벤트 통신 (Unity -> JS)
  // ─────────────────────────────────────────────────
  updateScoreFromUnity: function(score) {
    if (typeof window.updateScoreFromUnity === 'function') window.updateScoreFromUnity(score);
  },

  updateNextFromUnity: function(level) {
    if (typeof window.updateNextFromUnity === 'function') window.updateNextFromUnity(level);
  },

  showGameOverFromUnity: function(score, best, adWatched, spareLives) {
    if (typeof window.showGameOverFromUnity === 'function') window.showGameOverFromUnity(score, best, adWatched, spareLives);
  },

  notifySpeedBoostActivatedFromUnity: function() {
    if (typeof window.notifySpeedBoostActivatedFromUnity === 'function') window.notifySpeedBoostActivatedFromUnity();
  },

  notifyDangerZoneFromUnity: function(active) {
    if (typeof window.notifyDangerZoneFromUnity === 'function') window.notifyDangerZoneFromUnity(active ? 1 : 0);
  },

  notifyNewHighScoreFromUnity: function(score) {
    if (typeof window.notifyNewHighScoreFromUnity === 'function') window.notifyNewHighScoreFromUnity(score);
  },

  onMergeFromUnity: function(level) {
    if (typeof window.onMergeFromUnity === 'function') window.onMergeFromUnity(level);
  },

  // ─────────────────────────────────────────────────
  // 3. 광고 및 결제 (Interstitial / Rewarded)
  // ─────────────────────────────────────────────────
  
  // 게임오버 시 전면 광고 (비보상형, 자동 노출)
  ShowTossInterstitialAd: function() {
    var adId = 'ait.v2.live.f8b6b46c862f48f4';
    console.log('[TossBridge] ShowTossInterstitialAd:', adId);

    if (!window.AppsInToss || !window.AppsInToss.TossAds || typeof window.AppsInToss.TossAds.loadFullScreenAd !== 'function') {
      console.warn('[TossBridge] TossAds SDK Not Found - Skip interstitial');
      SendMessage('BridgeManager', 'OnInterstitialAdClosed');
      return;
    }

    var loadUnregister = window.AppsInToss.TossAds.loadFullScreenAd({
      options: { adGroupId: adId },
      onEvent: function(event) {
        if (event.type !== 'loaded') return;
        loadUnregister && loadUnregister();

        var showUnregister = window.AppsInToss.TossAds.showFullScreenAd({
          options: { adGroupId: adId },
          onEvent: function(ev) {
            if (ev.type === 'dismissed' || ev.type === 'failedToShow') {
              showUnregister && showUnregister();
              SendMessage('BridgeManager', 'OnInterstitialAdClosed');
            }
          }
        });
      }
    });
  },

  // 다시하기 보상형 광고 / 2배속 보상형 광고
  // adType: 0 (다시하기/부활), 1 (2배속)
  ShowTossAd: function(adType) {
    var adId = 'ait.v2.live.79b8c799130343ec'; // 다시하기 광고 ID
    console.log('[TossBridge] ShowTossAd called, type:', adType, 'adId:', adId);

    if (!window.AppsInToss || !window.AppsInToss.TossAds || typeof window.AppsInToss.TossAds.loadFullScreenAd !== 'function') {
      console.warn('[TossBridge] TossAds SDK Not Found - Simulating Success');
      setTimeout(function() {
        if (adType === 0) SendMessage('BridgeManager', 'OnReviveSuccess');
        else SendMessage('BridgeManager', 'OnSpeedBoostAdSuccess');
      }, 500);
      return;
    }

    var loadUnregister = window.AppsInToss.TossAds.loadFullScreenAd({
      options: { adGroupId: adId },
      onEvent: function(event) {
        if (event.type !== 'loaded') return;
        loadUnregister && loadUnregister();

        var showUnregister = window.AppsInToss.TossAds.showFullScreenAd({
          options: { adGroupId: adId },
          onEvent: function(ev) {
            if (ev.type === 'userEarnedReward') {
              if (adType === 0) SendMessage('BridgeManager', 'OnReviveSuccess');
              else SendMessage('BridgeManager', 'OnSpeedBoostAdSuccess');
            }
            if (ev.type === 'dismissed' || ev.type === 'failedToShow') {
              showUnregister && showUnregister();
            }
          }
        });
      }
    });
  },

  // ─────────────────────────────────────────────────
  // 4. 인앱 결제 (IAP)
  // ─────────────────────────────────────────────────

  // 상품 구매 요청
  // productId: 콘솔에서 발급받은 상품 ID (ait.xxx...)
  TossIAPPurchase: function(productIdPtr) {
    var productId = UTF8ToString(productIdPtr);
    console.log('[TossBridge] TossIAPPurchase:', productId);

    if (!window.AppsInToss || !window.AppsInToss.IAP ||
        typeof window.AppsInToss.IAP.createOneTimePurchaseOrder !== 'function') {
      console.warn('[TossBridge] IAP SDK Not Found - Simulating Success');
      setTimeout(function() {
        SendMessage('BridgeManager', 'OnIAPSuccess', productId);
      }, 500);
      return;
    }

    window.AppsInToss.IAP.createOneTimePurchaseOrder({
      sku: productId,
      onEvent: function(event) {
        console.log('[TossBridge] IAP event:', event.type, event.data);
        if (event.type === 'success') {
          var orderId = (event.data && event.data.orderId) ? event.data.orderId : '';
          // SDK 1.1.3+: 상품 지급 후 completeProductGrant 필수 호출
          if (typeof window.AppsInToss.IAP.completeProductGrant === 'function') {
            window.AppsInToss.IAP.completeProductGrant({ orderId: orderId })
              .then(function() {
                SendMessage('BridgeManager', 'OnIAPSuccess', productId);
              })
              .catch(function(e) {
                console.error('[TossBridge] completeProductGrant failed:', e);
                SendMessage('BridgeManager', 'OnIAPSuccess', productId);
              });
          } else {
            SendMessage('BridgeManager', 'OnIAPSuccess', productId);
          }
        } else if (event.type === 'error') {
          var code = (event.data && event.data.code) ? event.data.code : 'UNKNOWN';
          SendMessage('BridgeManager', 'OnIAPFailed', code);
        }
      }
    });
  },

  // 앱 시작 시 미결 주문 복원 (결제됐지만 지급 안 된 주문)
  TossIAPRestorePendingOrders: function() {
    if (!window.AppsInToss || !window.AppsInToss.IAP ||
        typeof window.AppsInToss.IAP.getPendingOrders !== 'function') {
      return;
    }

    window.AppsInToss.IAP.getPendingOrders().then(function(orders) {
      if (!orders || orders.length === 0) return;
      orders.forEach(function(order) {
        var sku = order.sku || '';
        console.log('[TossBridge] Restoring pending order:', order.orderId, sku);
        SendMessage('BridgeManager', 'OnIAPRestored', sku);
        if (typeof window.AppsInToss.IAP.completeProductGrant === 'function') {
          window.AppsInToss.IAP.completeProductGrant({ orderId: order.orderId })
            .catch(function(e) { console.error('[TossBridge] restore complete failed:', e); });
        }
      });
    }).catch(function(e) {
      console.error('[TossBridge] getPendingOrders failed:', e);
    });
  },

  // 토스페이 결제창 호출 (Checkout 방식)
  TossPayCheckout: function(payTokenPtr) {
    var payToken = UTF8ToString(payTokenPtr);
    console.log('[TossBridge] TossPayCheckout start, token:', payToken);

    if (!window.AppsInToss || !window.AppsInToss.TossPay || 
        typeof window.AppsInToss.TossPay.checkoutPayment !== 'function') {
      console.warn('[TossBridge] TossPay SDK Not Found - Simulating Success');
      setTimeout(function() {
        SendMessage('BridgeManager', 'OnIAPSuccess', 'toss_pay_success');
      }, 500);
      return;
    }

    window.AppsInToss.TossPay.checkoutPayment({
      payToken: payToken
    }).then(function(result) {
      if (result.success) {
        SendMessage('BridgeManager', 'OnIAPSuccess', 'toss_pay_success');
      } else {
        SendMessage('BridgeManager', 'OnIAPFailed', result.reason || 'USER_CANCEL');
      }
    }).catch(function(error) {
      SendMessage('BridgeManager', 'OnIAPFailed', error.toString());
    });
  },

  notifyAdRemovedFromUnity: function() {
    if (typeof window.notifyAdRemovedFromUnity === 'function') window.notifyAdRemovedFromUnity();
  },

  // ─────────────────────────────────────────────────
  // 5. 플랫폼 네이티브 기능 (Login, Share, Vibrate, etc.)
  // ─────────────────────────────────────────────────
  TossAppLogin: function() {
    if (window.AppsInToss && typeof window.AppsInToss.getUserKeyForGame === 'function') {
      // [Guide] 게임 전용 로그인(hash)을 사용하면 약관 동의 절차 없이 즉시 유저를 식별할 수 있어 이탈률이 줄어듭니다.
      window.AppsInToss.getUserKeyForGame().then(function(res) {
        SendMessage('BridgeManager', 'OnLoginSuccess', res.hash || '');
      }).catch(function(e) {
        // 폴백: getUserKeyForGame 미지원 버전이면 appLogin 시도
        if (typeof window.AppsInToss.appLogin === 'function') {
           window.AppsInToss.appLogin().then(function(res) {
             SendMessage('BridgeManager', 'OnLoginSuccess', res.authorizationCode || '');
           }).catch(function(err) { SendMessage('BridgeManager', 'OnLoginFailed', err.toString()); });
        } else {
           SendMessage('BridgeManager', 'OnLoginFailed', e.toString());
        }
      });
    } else if (window.AppsInToss && typeof window.AppsInToss.appLogin === 'function') {
      window.AppsInToss.appLogin().then(function(res) {
        SendMessage('BridgeManager', 'OnLoginSuccess', res.authorizationCode || '');
      }).catch(function(e) {
        SendMessage('BridgeManager', 'OnLoginFailed', e.toString());
      });
    } else {
      setTimeout(function() { SendMessage('BridgeManager', 'OnLoginSuccess', 'local_user'); }, 200);
    }
  },

  // [Guide] 게임 종료 시 네이티브 리더보드에 점수를 제출하여 소셜 기능을 연동합니다.
  TossSubmitLeaderboardScore: function(score) {
    if (window.AppsInToss && typeof window.AppsInToss.submitGameCenterLeaderBoardScore === 'function') {
      window.AppsInToss.submitGameCenterLeaderBoardScore({ score: score }).catch(function(e) {
        console.error('[TossBridge] SubmitScore Failed:', e);
      });
    }
  },

  TossGetSafeArea: function() {
    if (window.AppsInToss && typeof window.AppsInToss.getSafeAreaInsets === 'function') {
      window.AppsInToss.getSafeAreaInsets().then(function(insets) {
        var payload = insets.top + ',' + insets.bottom + ',' + insets.left + ',' + insets.right;
        SendMessage('BridgeManager', 'OnSafeAreaReceived', payload);
      });
    } else {
      SendMessage('BridgeManager', 'OnSafeAreaReceived', '0,0,0,0');
    }
  },

  TossShare: function(msgPtr) {
    var msg = UTF8ToString(msgPtr);
    if (window.AppsInToss && typeof window.AppsInToss.share === 'function') {
      window.AppsInToss.share({ message: msg });
    } else if (navigator.share) {
      navigator.share({ title: '애니멀 팝!', text: msg, url: window.location.href });
    }
  },

  TossVibrate: function(stylePtr) {
    var style = UTF8ToString(stylePtr);
    if (window.AppsInToss && typeof window.AppsInToss.generateHapticFeedback === 'function') {
      window.AppsInToss.generateHapticFeedback({ style: style });
    } else if (navigator.vibrate) {
      navigator.vibrate(50);
    }
  },

  TossExitApp: function() {
    if (window.AppsInToss && typeof window.AppsInToss.close === 'function') window.AppsInToss.close();
    else if (window.Toss && window.Toss.close) window.Toss.close();
    else window.close();
  }
});
