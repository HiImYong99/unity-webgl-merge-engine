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

  // 프로모션 미션: 한 판 완료(게임오버) 시 1회 호출 (부활 중복 제외, C# GameMgr가 세션당 1회만 보냄)
  notifyGamePlayedFromUnity: function() {
    if (typeof window.onGamePlayedFromUnity === 'function') window.onGamePlayedFromUnity();
  },

  // ─────────────────────────────────────────────────
  // 3. 광고 및 결제 (Interstitial / Rewarded)
  // ─────────────────────────────────────────────────

  // 게임오버 시 전면 광고 (비보상형, 자동 노출)
  ShowTossInterstitialAd: function() {
    var adId = 'ait.v2.live.f8b6b46c862f48f4';
    console.log('[TossBridge] ShowTossInterstitialAd:', adId);

    if (typeof window.pauseAudioForAd === 'function') window.pauseAudioForAd();

    // 광고 API 탐지: 인앱 광고 2.0 ver2 (최상위) → GoogleAdMob (레거시) 폴백
    var ait = window.AppsInToss;
    var api = null;
    if (ait && typeof ait.loadFullScreenAd === 'function') {
      api = { load: ait.loadFullScreenAd, show: ait.showFullScreenAd, name: 'IntegratedAd' };
    } else if (ait && ait.GoogleAdMob && typeof ait.GoogleAdMob.loadAppsInTossAdMob === 'function') {
      api = { load: ait.GoogleAdMob.loadAppsInTossAdMob, show: ait.GoogleAdMob.showAppsInTossAdMob, name: 'GoogleAdMob' };
    }
    if (!api) {
      console.warn('[TossBridge] Ad SDK Not Found - Skip interstitial');
      if (typeof window.resumeAudioAfterAd === 'function') window.resumeAudioAfterAd();
      SendMessage('BridgeManager', 'OnInterstitialAdClosed');
      return;
    }
    console.log('[TossBridge] Using ad API:', api.name);

    var loadUnregister = api.load({
      options: { adGroupId: adId },
      onEvent: function(event) {
        if (event.type !== 'loaded') return;
        loadUnregister && loadUnregister();

        var showUnregister = api.show({
          options: { adGroupId: adId },
          onEvent: function(ev) {
            if (ev.type === 'dismissed' || ev.type === 'failedToShow') {
              showUnregister && showUnregister();
              if (typeof window.resumeAudioAfterAd === 'function') window.resumeAudioAfterAd();
              SendMessage('BridgeManager', 'OnInterstitialAdClosed');
            }
          },
          onError: function(err) {
            showUnregister && showUnregister();
            console.error('[TossBridge] Interstitial show error:', err);
            if (typeof window.resumeAudioAfterAd === 'function') window.resumeAudioAfterAd();
            SendMessage('BridgeManager', 'OnInterstitialAdClosed');
          }
        });
      },
      onError: function(err) {
        loadUnregister && loadUnregister();
        console.error('[TossBridge] Interstitial load error:', err);
        if (typeof window.resumeAudioAfterAd === 'function') window.resumeAudioAfterAd();
        SendMessage('BridgeManager', 'OnInterstitialAdClosed');
      }
    });
  },

  // 다시하기 보상형 광고 / 2배속 보상형 광고
  // adType: 0 (다시하기/부활), 1 (2배속)
  ShowTossAd: function(adType) {
    var adId = 'ait.v2.live.79b8c799130343ec';
    console.log('[TossBridge] ShowTossAd called, type:', adType, 'adId:', adId);

    if (typeof window.pauseAudioForAd === 'function') window.pauseAudioForAd();

    // 광고 API 탐지: 인앱 광고 2.0 ver2 (최상위) → GoogleAdMob (레거시) 폴백
    var ait = window.AppsInToss;
    var api = null;
    if (ait && typeof ait.loadFullScreenAd === 'function') {
      api = { load: ait.loadFullScreenAd, show: ait.showFullScreenAd, name: 'IntegratedAd' };
    } else if (ait && ait.GoogleAdMob && typeof ait.GoogleAdMob.loadAppsInTossAdMob === 'function') {
      api = { load: ait.GoogleAdMob.loadAppsInTossAdMob, show: ait.GoogleAdMob.showAppsInTossAdMob, name: 'GoogleAdMob' };
    }
    if (!api) {
      console.warn('[TossBridge] Ad SDK Not Found - Ad skipped, no reward');
      if (typeof window.resumeAudioAfterAd === 'function') window.resumeAudioAfterAd();
      if (typeof window._showAdUnavailableToast === 'function') window._showAdUnavailableToast();
      SendMessage('BridgeManager', 'OnAdFailed', 'SDK_NOT_AVAILABLE');
      return;
    }
    console.log('[TossBridge] Using ad API:', api.name);

    function _doShow() {
      var showUnregister = api.show({
        options: { adGroupId: adId },
        onEvent: function(ev) {
          if (ev.type === 'userEarnedReward') {
            if (adType === 0) {
              if (typeof window._dismissGameOverForRevive === 'function') window._dismissGameOverForRevive();
              SendMessage('BridgeManager', 'OnReviveSuccess');
            } else {
              SendMessage('BridgeManager', 'OnSpeedBoostAdSuccess');
            }
          }
          if (ev.type === 'dismissed' || ev.type === 'failedToShow') {
            showUnregister && showUnregister();
            if (typeof window.resumeAudioAfterAd === 'function') window.resumeAudioAfterAd();
            if (typeof window._preloadReviveAd === 'function') window._preloadReviveAd();
          }
        },
        onError: function(err) {
          showUnregister && showUnregister();
          console.error('[TossBridge] Rewarded show error:', err);
          if (typeof window.resumeAudioAfterAd === 'function') window.resumeAudioAfterAd();
          if (typeof window._showAdUnavailableToast === 'function') window._showAdUnavailableToast();
          SendMessage('BridgeManager', 'OnAdFailed', 'SHOW_ERROR');
        }
      });
    }

    // 사전 로드된 광고가 있으면 바로 표시, 아니면 로드 후 표시
    if (window._reviveAdLoaded) {
      window._reviveAdLoaded = false;
      _doShow();
    } else {
      var loadUnregister = api.load({
        options: { adGroupId: adId },
        onEvent: function(event) {
          if (event.type === 'loaded') {
            loadUnregister && loadUnregister();
            _doShow();
          }
        },
        onError: function(err) {
          loadUnregister && loadUnregister();
          console.error('[TossBridge] Rewarded load error:', err);
          if (typeof window.resumeAudioAfterAd === 'function') window.resumeAudioAfterAd();
          if (typeof window._showAdUnavailableToast === 'function') window._showAdUnavailableToast();
          SendMessage('BridgeManager', 'OnAdFailed', 'LOAD_ERROR');
        }
      });
    }
  },

  // ─────────────────────────────────────────────────
  // 4. 인앱 결제 (IAP)
  // ─────────────────────────────────────────────────

  // 상품 구매 요청
  // productId: 콘솔에서 발급받은 상품 ID (ait.xxx...)
  TossIAPPurchase: function(productIdPtr) {
    var productId = UTF8ToString(productIdPtr);
    console.log('[TossBridge] TossIAPPurchase Request:', productId);

    if (!window.AppsInToss || !window.AppsInToss.IAP ||
        typeof window.AppsInToss.IAP.createOneTimePurchaseOrder !== 'function') {
      console.warn('[TossBridge] IAP SDK Not Found - Simulating Success');
      setTimeout(function() {
        SendMessage('BridgeManager', 'OnIAPSuccess', productId);
      }, 500);
      return;
    }

    // [MCP Guide] createOneTimePurchaseOrder 시그니처 준수
    var cleanup = window.AppsInToss.IAP.createOneTimePurchaseOrder({
      options: {
        sku: productId,
        // processProductGrant: SDK가 결제 성공 후 상품 지급 여부를 묻는 콜백
        // true 반환 → 지급 성공 → onEvent(success) 발생
        // false 반환 → 지급 실패 → onError(PRODUCT_NOT_GRANTED_BY_PARTNER) 발생
        // ※ completeProductGrant는 이 콜백 안에서 호출하는 게 아님 (getPendingOrders 복원 전용)
        processProductGrant: function(params) {
          var orderId = (params && params.orderId) ? params.orderId : null;
          console.log('[TossBridge] processProductGrant:', orderId, 'params:', JSON.stringify(params));

          if (!orderId) {
            console.warn('[TossBridge] processProductGrant: orderId 없음 → false 반환 (에러 시나리오)');
            return false;
          }

          console.log('[TossBridge] processProductGrant: 지급 성공 → true 반환');
          return true;
        }
      },
      onEvent: function(event) {
        console.log('[TossBridge] IAP onEvent:', event.type, event.data);
        if (event.type === 'success') {
          // 지급까지 완료된 최종 성공 — cleanup 후 Unity에 알림
          if (typeof cleanup === 'function') cleanup();
          SendMessage('BridgeManager', 'OnIAPSuccess', productId);
        }
      },
      onError: function(error) {
        console.error('[TossBridge] IAP onError:', error);
        if (typeof cleanup === 'function') cleanup();
        var code = (error && error.code) ? error.code : 'UNKNOWN';
        SendMessage('BridgeManager', 'OnIAPFailed', code);
      }
    });

    // 페이지를 떠날 때 리소스 정리
    window.addEventListener('pagehide', function() {
      if (typeof cleanup === 'function') cleanup();
    }, { once: true });
  },

  // 앱 시작 시 미결 주문 복원 (결제됐지만 지급 안 된 주문)
  TossIAPRestorePendingOrders: function() {
    if (!window.AppsInToss || !window.AppsInToss.IAP ||
        typeof window.AppsInToss.IAP.getPendingOrders !== 'function') {
      return;
    }

    window.AppsInToss.IAP.getPendingOrders().then(function(res) {
      if (!res || !res.orders || res.orders.length === 0) return;
      res.orders.forEach(function(order) {
        var sku = order.sku || '';
        console.log('[TossBridge] Restoring pending order:', order.orderId, sku);
        SendMessage('BridgeManager', 'OnIAPRestored', sku);
        if (typeof window.AppsInToss.IAP.completeProductGrant === 'function') {
          // [MCP Guide] 파라미터 구조: { params: { orderId: '...' } }
          window.AppsInToss.IAP.completeProductGrant({ params: { orderId: order.orderId } })
            .catch(function(e) { console.error('[TossBridge] restore complete failed:', e); });
        }
      });
    }).catch(function(e) {
      console.error('[TossBridge] getPendingOrders failed:', e);
    });
  },

  // 상품 지급 완료 확인 브릿지 (Unity에서 호출)
  // orderId: 지급 완료된 주문 ID
  TossIAPCompleteProductGrant: function(orderIdPtr) {
    var orderId = UTF8ToString(orderIdPtr);
    console.log('[TossBridge] TossIAPCompleteProductGrant Request:', orderId);

    if (window.AppsInToss && window.AppsInToss.IAP && 
        typeof window.AppsInToss.IAP.completeProductGrant === 'function') {
      window.AppsInToss.IAP.completeProductGrant({ params: { orderId: orderId } })
        .then(function(res) {
          console.log('[TossBridge] completeProductGrant success:', res);
        })
        .catch(function(e) {
          console.error('[TossBridge] completeProductGrant failed:', e);
        });
    } else {
      console.warn('[TossBridge] completeProductGrant not supported or not found');
    }
  },

  // 토스페이 결제창 호출 (Checkout 방식)
  TossPayCheckout: function(payTokenPtr) {
    var payToken = UTF8ToString(payTokenPtr);
    console.log('[TossBridge] TossPayCheckout start, token:', payToken);

    // SDK 2.x에서는 직접 export되거나 TossPay 네임스페이스 아래에 있을 수 있음
    var checkoutFn = null;
    if (typeof window.AppsInToss.checkoutPayment === 'function') {
      checkoutFn = window.AppsInToss.checkoutPayment;
    } else if (window.AppsInToss.TossPay && typeof window.AppsInToss.TossPay.checkoutPayment === 'function') {
      checkoutFn = window.AppsInToss.TossPay.checkoutPayment;
    }

    if (!checkoutFn) {
      console.warn('[TossBridge] TossPay.checkoutPayment not found - Simulating success for testing');
      setTimeout(function() {
        SendMessage('BridgeManager', 'OnIAPSuccess', 'toss_pay_success_mock');
      }, 500);
      return;
    }

    checkoutFn({ payToken: payToken }).then(function(result) {
      if (result.success) {
        console.log('[TossBridge] TossPay Success');
        SendMessage('BridgeManager', 'OnIAPSuccess', 'toss_pay_success');
      } else {
        console.warn('[TossBridge] TossPay Failed:', result.reason);
        SendMessage('BridgeManager', 'OnIAPFailed', result.reason || 'USER_CANCEL');
      }
    }).catch(function(error) {
      console.error('[TossBridge] TossPay Error:', error);
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
      window.AppsInToss.submitGameCenterLeaderBoardScore({ score: String(score) }).catch(function(e) {
        console.error('[TossBridge] SubmitScore Failed:', e);
      });
    }
  },

  TossOpenLeaderboard: function() {
    if (window.AppsInToss && typeof window.AppsInToss.openGameCenterLeaderboard === 'function') {
      window.AppsInToss.openGameCenterLeaderboard().catch(function(e) {
        console.error('[TossBridge] OpenLeaderboard Failed:', e);
      });
    } else {
      console.warn('[TossBridge] openGameCenterLeaderboard not supported');
    }
  },

  TossGetSafeArea: function() {
    if (window.AppsInToss) {
      var fetchFunc = null;
      if (window.AppsInToss.SafeAreaInsets && typeof window.AppsInToss.SafeAreaInsets.get === 'function') {
        fetchFunc = window.AppsInToss.SafeAreaInsets.get.bind(window.AppsInToss.SafeAreaInsets);
      } else if (typeof window.AppsInToss.getSafeAreaInsets === 'function') {
        fetchFunc = window.AppsInToss.getSafeAreaInsets;
      }

      if (fetchFunc) {
        try {
          var res = fetchFunc();
          Promise.resolve(res).then(function(insets) {
            if (insets) {
              var payload = (insets.top || 0) + ',' + (insets.bottom || 0) + ',' + (insets.left || 0) + ',' + (insets.right || 0);
              SendMessage('BridgeManager', 'OnSafeAreaReceived', payload);
            }
          }).catch(function(err) {
            console.error('[TossBridge] OnSafeArea Error:', err);
            SendMessage('BridgeManager', 'OnSafeAreaReceived', '0,0,0,0');
          });
          return;
        } catch (e) {
          console.error('[TossBridge] getSafeAreaInsets Exception:', e);
        }
      }
    }
    SendMessage('BridgeManager', 'OnSafeAreaReceived', '0,0,0,0');
  },

  TossShare: function(msgPtr) {
    var raw = UTF8ToString(msgPtr);
    // C#은 점수(숫자)만 전달 → 로케일별 공유문구 생성. 숫자가 아니면 그대로 사용(하위호환).
    var msg = (window._buildShareText && /^\d+$/.test(raw)) ? window._buildShareText(raw) : raw;
    var shareTitle = (typeof window.AP_T === 'function') ? window.AP_T('app_title') : 'Animal Pop';
    var appScheme = 'intoss://animal-pop';
    var ogImageUrl = 'https://static.toss.im/icons/png/4x/icon-share-dots-mono.png';

    function doShare(linkText) {
      var finalMsg = linkText ? (msg + '\n' + linkText) : msg;
      if (window.AppsInToss && typeof window.AppsInToss.share === 'function') {
        window.AppsInToss.share({ message: finalMsg });
      } else if (navigator.share) {
        navigator.share({ title: shareTitle, text: finalMsg, url: window.location.href });
      }
    }

    if (window.AppsInToss && typeof window.AppsInToss.getTossShareLink === 'function') {
      try {
        var p = window.AppsInToss.getTossShareLink(appScheme, ogImageUrl);
        if (p && typeof p.then === 'function') {
          p.then(function(link) { doShare(link); })
           .catch(function(e) {
              console.warn('[TossBridge] getTossShareLink failed:', e);
              doShare(appScheme);
           });
        } else {
          doShare(p || appScheme);
        }
      } catch (e) {
        console.warn('[TossBridge] getTossShareLink exception:', e);
        doShare(appScheme);
      }
    } else {
      doShare(appScheme);
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

  // ─────────────────────────────────────────────────
  // 4-B. Android Google Play 인앱 결제 (Android WebView 전용)
  // ─────────────────────────────────────────────────

  RequestPurchase: function(productIdPtr) {
    var productId = UTF8ToString(productIdPtr);
    if (window.AndroidBridge && typeof window.AndroidBridge.launchPurchase === 'function') {
      window.AndroidBridge.launchPurchase(productId);
    } else {
      console.log('[TossBridge] IAP mock purchase: ' + productId);
      setTimeout(function() {
        SendMessage('BridgeManager', 'OnPurchaseSuccess', productId + '|mock_token_' + Date.now());
      }, 800);
    }
  },

  RestorePurchases: function() {
    if (window.AndroidBridge && typeof window.AndroidBridge.restorePurchases === 'function') {
      window.AndroidBridge.restorePurchases();
    }
  },

  TossExitApp: function() {
    // 체크리스트: 종료 확인 모달 표시
    if (typeof window._showExitConfirmModal === 'function') {
      window._showExitConfirmModal();
    } else if (window.AppsInToss && typeof window.AppsInToss.close === 'function') {
      window.AppsInToss.close();
    } else if (window.Toss && window.Toss.close) {
      window.Toss.close();
    } else {
      window.close();
    }
  }
});
