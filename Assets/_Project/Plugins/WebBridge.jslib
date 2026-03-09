mergeInto(LibraryManager.library, {

  // ── 인게임 HUD 업데이트 (Unity → JS) ──
  updateScoreFromUnity: function(score) {
    if (typeof window.updateScoreFromUnity === 'function') {
      window.updateScoreFromUnity(score);
    }
  },

  updateNextFromUnity: function(level) {
    if (typeof window.updateNextFromUnity === 'function') {
      window.updateNextFromUnity(level);
    }
  },

  showGameOverFromUnity: function(score, best, adWatched, spareLives) {
    if (typeof window.showGameOverFromUnity === 'function') {
      window.showGameOverFromUnity(score, best, adWatched, spareLives);
    }
  },

  // ── 부활 광고 (보상형 Rewarded) ──
  // 흐름: ShowAd 호출 → loadFullScreenAd → loaded → showFullScreenAd
  //        → userEarnedReward → OnReviveSuccess
  ShowAd: function() {
    console.log("[WebBridge] ShowAd: 부활 보상형 광고 시작");

    var REVIVE_AD_ID = 'ait-ad-test-rewarded-id'; // TODO: 실제 adGroupId로 교체

    // AppsInToss SDK 미지원 환경 (브라우저/에디터) 폴백
    if (typeof window.loadFullScreenAd !== 'function' ||
        !window.loadFullScreenAd.isSupported || !window.loadFullScreenAd.isSupported()) {
      console.warn('[WebBridge] loadFullScreenAd 미지원 환경 — 즉시 성공 시뮬레이션');
      setTimeout(function() {
        SendMessage('BridgeManager', 'OnReviveSuccess');
      }, 800);
      return;
    }

    var loadUnregister = window.loadFullScreenAd({
      options: { adGroupId: REVIVE_AD_ID },
      onEvent: function(event) {
        if (event.type !== 'loaded') return;
        console.log('[WebBridge] 부활 광고 loaded → show');
        loadUnregister && loadUnregister();

        var showUnregister = window.showFullScreenAd({
          options: { adGroupId: REVIVE_AD_ID },
          onEvent: function(ev) {
            if (ev.type === 'userEarnedReward') {
              // 보상형: 시청 완료 시 지급
              console.log('[WebBridge] 부활 광고 userEarnedReward → OnReviveSuccess');
              SendMessage('BridgeManager', 'OnReviveSuccess');
            }
            if (ev.type === 'dismissed' || ev.type === 'failedToShow') {
              showUnregister && showUnregister();
              // 다음 광고 미리 로드
              window._preloadReviveAd && window._preloadReviveAd();
            }
          },
          onError: function(err) {
            console.error('[WebBridge] 부활 광고 show 실패:', err);
            showUnregister && showUnregister();
          }
        });
      },
      onError: function(err) {
        console.error('[WebBridge] 부활 광고 load 실패:', err);
        loadUnregister && loadUnregister();
      }
    });
  },

  // ── 2배속 광고 (보상형 Rewarded) ──
  // userEarnedReward 이벤트에서 OnSpeedBoostAdSuccess 콜백
  ShowSpeedBoostAd: function() {
    console.log("[WebBridge] ShowSpeedBoostAd: 2배속 보상형 광고 시작");

    var SPEED_AD_ID = 'ait-ad-test-rewarded-id'; // TODO: 실제 adGroupId로 교체

    // AppsInToss SDK 미지원 환경 (브라우저/에디터) 폴백
    if (typeof window.loadFullScreenAd !== 'function' ||
        !window.loadFullScreenAd.isSupported || !window.loadFullScreenAd.isSupported()) {
      console.warn('[WebBridge] loadFullScreenAd 미지원 환경 — 즉시 성공 시뮬레이션');
      setTimeout(function() {
        SendMessage('BridgeManager', 'OnSpeedBoostAdSuccess');
      }, 800);
      return;
    }

    var loadUnregister = window.loadFullScreenAd({
      options: { adGroupId: SPEED_AD_ID },
      onEvent: function(event) {
        if (event.type !== 'loaded') return;
        console.log('[WebBridge] 2배속 광고 loaded → show');
        loadUnregister && loadUnregister();

        var showUnregister = window.showFullScreenAd({
          options: { adGroupId: SPEED_AD_ID },
          onEvent: function(ev) {
            if (ev.type === 'userEarnedReward') {
              // 보상형: 시청 완료 시 지급
              console.log('[WebBridge] 2배속 광고 userEarnedReward → OnSpeedBoostAdSuccess');
              SendMessage('BridgeManager', 'OnSpeedBoostAdSuccess');
            }
            if (ev.type === 'dismissed' || ev.type === 'failedToShow') {
              showUnregister && showUnregister();
            }
          },
          onError: function(err) {
            console.error('[WebBridge] 2배속 광고 show 실패:', err);
            showUnregister && showUnregister();
          }
        });
      },
      onError: function(err) {
        console.error('[WebBridge] 2배속 광고 load 실패:', err);
        loadUnregister && loadUnregister();
      }
    });
  },

  notifySpeedBoostActivatedFromUnity: function() {
    if (typeof window.notifySpeedBoostActivatedFromUnity === 'function') {
      window.notifySpeedBoostActivatedFromUnity();
    }
  },

  ShareResult: function(score, level, imageBase64) {
    var scoreStr = typeof score === "number" ? score.toString() : UTF8ToString(score);
    var levelStr = typeof level === "number" ? level.toString() : UTF8ToString(level);

    console.log("[WebBridge] ShareResult: Score=" + scoreStr + ", Level=" + levelStr);

    if (navigator.share) {
        var shareData = {
            title: '애니멀 팝!',
            text: '애니멀 팝에서 ' + scoreStr + '점을 달성했어요! 🐾',
        };

        navigator.share(shareData).catch(function(err) { console.error(err); });
    }
  },

  OnAdComplete: function() {
    SendMessage('BridgeManager', 'OnReviveSuccess');
  },

  ExitApp: function() {
    console.log("[WebBridge] ExitApp called.");
    if (window.AppsInToss && typeof window.AppsInToss.close === 'function') {
        window.AppsInToss.close();
    } else if (window.Toss && window.Toss.close) {
        window.Toss.close();
    } else {
        window.close();
    }
  },


  // ── Unity → JS: 위험 구역 경고 (데드라인 근처) ──
  notifyDangerZoneFromUnity: function(active) {
    if (typeof window.notifyDangerZoneFromUnity === 'function') {
      window.notifyDangerZoneFromUnity(active ? 1 : 0);
    }
  },

  // ── Unity → JS: 신기록 갱신 알림 ──
  notifyNewHighScoreFromUnity: function(score) {
    if (typeof window.notifyNewHighScoreFromUnity === 'function') {
      window.notifyNewHighScoreFromUnity(score);
    }
  },

  // ── Unity → JS: 병합 이벤트 (사이드 패널 콤보/병합 카운터) ──
  onMergeFromUnity: function(level) {
    if (typeof window.onMergeFromUnity === 'function') {
      window.onMergeFromUnity(level);
    }
  },

  AppLogin: function() {
    console.log("[WebBridge] AppLogin called.");
    if (window.AppsInToss && typeof window.AppsInToss.appLogin === 'function') {
        window.AppsInToss.appLogin().then(function(result) {
            var code = (result && result.authorizationCode) ? result.authorizationCode : 'ait_user_' + Date.now();
            SendMessage('BridgeManager', 'OnLoginSuccess', code);
        }).catch(function(err) {
            console.error(err);
            SendMessage('BridgeManager', 'OnLoginFailed', err.toString());
        });
    } else if (window.tossFramework && window.tossFramework.appLogin) {
        window.tossFramework.appLogin().then(function(result) {
            var code = result.authorizationCode || 'legacy_user_' + Date.now();
            SendMessage('BridgeManager', 'OnLoginSuccess', code);
        }).catch(function(err) {
            SendMessage('BridgeManager', 'OnLoginFailed', err.toString());
        });
    } else {
        console.warn("[WebBridge] Toss SDK not found. Simulating login.");
        setTimeout(function() {
            SendMessage('BridgeManager', 'OnLoginSuccess', 'local_browser_' + Date.now());
        }, 300);
    }
  }
});
