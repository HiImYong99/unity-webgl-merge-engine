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

  ShowAd: function() {
    console.log("[WebBridge] Showing Ad...");
    setTimeout(function() {
        console.log("[WebBridge] Ad Complete!");
        SendMessage('BridgeManager', 'OnReviveSuccess');
    }, 1000);
  },

  ShareResult: function(score, level, imageBase64) {
    var scoreStr = typeof score === "number" ? score.toString() : UTF8ToString(score);
    var levelStr = typeof level === "number" ? level.toString() : UTF8ToString(level);
    var imgStr = typeof imageBase64 === "string" ? imageBase64 : UTF8ToString(imageBase64);

    console.log("[WebBridge] ShareResult: Score=" + scoreStr + ", Level=" + levelStr);

    if (navigator.share) {
        var shareData = {
            title: '디저트 팝!',
            text: '디저트 팝에서 ' + scoreStr + '점을 달성했어요! 🍩',
        };

        if (imgStr && imgStr.startsWith('data:image/')) {
            try {
                var arr = imgStr.split(',');
                var mime = arr[0].match(/:(.*?);/)[1];
                var bstr = atob(arr[1]);
                var n = bstr.length;
                var u8arr = new Uint8Array(n);
                while (n--) { u8arr[n] = bstr.charCodeAt(n); }
                var file = new File([u8arr], 'result.png', { type: mime });
                if (navigator.canShare && navigator.canShare({ files: [file] })) {
                    shareData.files = [file];
                }
            } catch (e) {
                console.error('[WebBridge] base64 변환 실패', e);
            }
        }

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

  // ── Unity → JS: 도감 발견 알림 ──
  onDessertDiscoveredFromUnity: function(level) {
    if (typeof window.onDessertDiscoveredFromUnity === 'function') {
      window.onDessertDiscoveredFromUnity(level);
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
