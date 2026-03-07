/**
 * TossBridge.jslib – AppsInToss 공식 API + HTML 오버레이 연동
 */
mergeInto(LibraryManager.library, {

  // ── HTML 랜딩 오버레이 표시 (Unity 준비 완료 시 호출)
  _ShowHtmlLanding: function(bestScore) {
    var score = bestScore || 0;
    console.log('[TossBridge] ShowHtmlLanding, best:', score);
    // index.html의 window._ShowHtmlLanding 호출
    if (typeof window._ShowHtmlLanding === 'function') {
      window._ShowHtmlLanding(score);
    }
    // localStorage에 최고점수 동기화
    try { localStorage.setItem('dessertpop_best', score.toString()); } catch(e) {}
  },

  // ─────────────────────────────────────────────────
  // 1. 로그인 / 사용자 인증
  // ─────────────────────────────────────────────────
  TossAppLogin: function() {
    console.log('[TossBridge] AppLogin called.');

    var hasTossSDK = window.AppsInToss && typeof window.AppsInToss.appLogin === 'function';
    // Legacy support: apps-in-toss/web-framework
    var hasLegacySDK = window.tossFramework && typeof window.tossFramework.appLogin === 'function';

    if (hasTossSDK) {
      window.AppsInToss.appLogin()
        .then(function(result) {
          var code = (result && result.authorizationCode) ? result.authorizationCode : 'ait_user_' + Date.now();
          console.log('[TossBridge] AIT Login Success:', code);
          SendMessage('TossBridgeManager', 'OnLoginSuccess', code);
        })
        .catch(function(err) {
          console.error('[TossBridge] AIT Login Failed:', err);
          SendMessage('TossBridgeManager', 'OnLoginFailed', err ? err.toString() : 'unknown_error');
        });
    } else if (hasLegacySDK) {
      window.tossFramework.appLogin()
        .then(function(result) {
          var code = (result && result.authorizationCode) ? result.authorizationCode : 'legacy_user_' + Date.now();
          SendMessage('TossBridgeManager', 'OnLoginSuccess', code);
        })
        .catch(function(err) {
          SendMessage('TossBridgeManager', 'OnLoginFailed', err ? err.toString() : 'unknown_error');
        });
    } else {
      // 로컬/일반 브라우저 Fallback
      console.warn('[TossBridge] AppsInToss SDK not found. Simulating login success for local dev.');
      setTimeout(function() {
        SendMessage('TossBridgeManager', 'OnLoginSuccess', 'local_browser_user_' + Date.now());
      }, 300);
    }
  },

  // ─────────────────────────────────────────────────
  // 2. Safe Area 조회 (토스 상단바 고려)
  // ─────────────────────────────────────────────────
  TossGetSafeArea: function() {
    console.log('[TossBridge] GetSafeArea called.');

    var hasTossSDK = window.AppsInToss && typeof window.AppsInToss.getSafeAreaInsets === 'function';

    if (hasTossSDK) {
      window.AppsInToss.getSafeAreaInsets()
        .then(function(insets) {
          // insets: { top, bottom, left, right } (pixels)
          var top    = (insets && insets.top)    ? insets.top    : 0;
          var bottom = (insets && insets.bottom) ? insets.bottom : 0;
          var left   = (insets && insets.left)   ? insets.left   : 0;
          var right  = (insets && insets.right)  ? insets.right  : 0;
          var payload = top + ',' + bottom + ',' + left + ',' + right;
          console.log('[TossBridge] SafeArea:', payload);
          SendMessage('TossBridgeManager', 'OnSafeAreaReceived', payload);
        })
        .catch(function(err) {
          console.error('[TossBridge] GetSafeArea failed:', err);
          // 실패 시 기본값(0,0,0,0) 전송
          SendMessage('TossBridgeManager', 'OnSafeAreaReceived', '0,0,0,0');
        });
    } else {
      // CSS env() 기반 Fallback (일반 브라우저/iOS Safari)
      try {
        var el = document.createElement('div');
        el.style.cssText = 'position:fixed;top:env(safe-area-inset-top,0px);left:env(safe-area-inset-left,0px);width:1px;height:1px;pointer-events:none;opacity:0;';
        document.body.appendChild(el);
        var top    = parseFloat(getComputedStyle(el).top)  || 0;
        var left   = parseFloat(getComputedStyle(el).left) || 0;
        document.body.removeChild(el);
        // bottom은 CSS로 간단히 얻기 어려워 0으로, 실제 기기는 SDK 통해 받아야 함
        var payload = top + ',0,' + left + ',0';
        console.log('[TossBridge] SafeArea (CSS fallback):', payload);
        SendMessage('TossBridgeManager', 'OnSafeAreaReceived', payload);
      } catch(e) {
        SendMessage('TossBridgeManager', 'OnSafeAreaReceived', '44,0,0,0'); // 기본 iPhone X 상단바 높이
      }
    }
  },

  // ─────────────────────────────────────────────────
  // 3. 공유하기 (네이티브 공유 시트)
  // ─────────────────────────────────────────────────
  TossShare: function(messagePtr) {
    var message = UTF8ToString(messagePtr);
    console.log('[TossBridge] Share called:', message);

    var hasTossSDK = window.AppsInToss && typeof window.AppsInToss.share === 'function';

    if (hasTossSDK) {
      // 앱인토스 전용 네이티브 공유
      window.AppsInToss.share({ message: message })
        .then(function() {
          console.log('[TossBridge] Share success');
          SendMessage('TossBridgeManager', 'OnShareSuccess', '');
        })
        .catch(function(err) {
          console.error('[TossBridge] Share failed:', err);
          SendMessage('TossBridgeManager', 'OnShareFailed', err ? err.toString() : 'share_failed');
        });
    } else if (navigator.share) {
      // Web Share API Fallback
      navigator.share({ title: '디저트 팝', text: message })
        .then(function() { console.log('[TossBridge] Web Share success'); })
        .catch(function(err) { console.error('[TossBridge] Web Share failed:', err); });
    } else {
      // Clipboard Fallback
      console.warn('[TossBridge] No share API available. Copying to clipboard.');
      if (navigator.clipboard) {
        navigator.clipboard.writeText(message).then(function() {
          console.log('[TossBridge] Copied to clipboard:', message);
        });
      }
    }
  },

  // ─────────────────────────────────────────────────
  // 4. 햅틱 피드백 (진동)
  // ─────────────────────────────────────────────────
  TossVibrate: function(stylePtr) {
    var style = UTF8ToString(stylePtr); // "light", "medium", "heavy", "success", "error"
    console.log('[TossBridge] Vibrate called, style:', style);

    var hasTossSDK = window.AppsInToss && typeof window.AppsInToss.generateHapticFeedback === 'function';

    if (hasTossSDK) {
      window.AppsInToss.generateHapticFeedback({ style: style })
        .then(function() { console.log('[TossBridge] Haptic success'); })
        .catch(function(err) { console.error('[TossBridge] Haptic failed:', err); });
    } else if (navigator.vibrate) {
      // Web Vibration API Fallback
      var duration = (style === 'heavy') ? 200 : (style === 'medium') ? 100 : 50;
      navigator.vibrate(duration);
    } else {
      console.warn('[TossBridge] No vibration API available.');
    }
  },

  // ─────────────────────────────────────────────────
  // 5. 광고 (인앱 광고) - 부활 아이템
  // ─────────────────────────────────────────────────
  TossShowAd: function() {
    console.log('[TossBridge] ShowAd called.');

    var hasTossSDK = window.AppsInToss && typeof window.AppsInToss.showInterstitialAd === 'function';

    if (hasTossSDK) {
      window.AppsInToss.showInterstitialAd()
        .then(function(result) {
          if (result && result.watched) {
            console.log('[TossBridge] Ad watched successfully.');
            SendMessage('TossBridgeManager', 'OnAdComplete', '');
          } else {
            console.log('[TossBridge] Ad skipped or failed.');
            SendMessage('TossBridgeManager', 'OnAdFailed', 'ad_skipped');
          }
        })
        .catch(function(err) {
          console.error('[TossBridge] Ad error:', err);
          SendMessage('TossBridgeManager', 'OnAdFailed', err ? err.toString() : 'ad_error');
        });
    } else {
      // 로컬 테스트 Fallback: 1초 후 광고 시청 완료 시뮬레이션
      console.warn('[TossBridge] No Ad SDK. Simulating ad completion in 1s.');
      setTimeout(function() {
        console.log('[TossBridge] Simulated Ad Complete!');
        SendMessage('TossBridgeManager', 'OnAdComplete', '');
      }, 1000);
    }
  },

  // ─────────────────────────────────────────────────
  // 6. 앱 종료 (미니앱 닫기)
  // ─────────────────────────────────────────────────
  TossExitApp: function() {
    console.log('[TossBridge] ExitApp called.');

    var hasTossSDK = window.AppsInToss && typeof window.AppsInToss.close === 'function';

    if (hasTossSDK) {
      window.AppsInToss.close();
    } else if (window.Toss && window.Toss.close) {
      window.Toss.close();
    } else {
      window.close();
    }
  },
});
