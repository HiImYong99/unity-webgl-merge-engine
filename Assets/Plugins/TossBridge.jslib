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
    try { localStorage.setItem('dessertpop_best', score.toString()); } catch(e) {}
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
  // 3. 플랫폼 네이티브 기능 (Login, Share, etc.)
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

  TossExitApp: function() {
    if (window.AppsInToss && typeof window.AppsInToss.close === 'function') window.AppsInToss.close();
    else if (window.Toss && window.Toss.close) window.Toss.close();
    else window.close();
  },

  // ─────────────────────────────────────────────────
  // 4. Google Play 인앱 결제 (Android 래퍼 전용)
  // ─────────────────────────────────────────────────

  // 결제 바텀시트 요청
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
  }
});
