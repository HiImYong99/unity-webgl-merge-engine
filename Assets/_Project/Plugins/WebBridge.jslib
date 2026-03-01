mergeInto(LibraryManager.library, {
  ShowAd: function() {
    // In a real scenario, this calls the Toss Ad SDK.
    // Simulating ad completion immediately for this MVP.
    // Call OnAdComplete or directly SendMessage.
    console.log("Showing Ad...");

    // Simulate async ad view
    setTimeout(function() {
        console.log("Ad Complete!");
        SendMessage('BridgeManager', 'OnReviveSuccess');
    }, 1000);
  },

  ShareResult: function(score, level, imageBase64) {
    var scoreStr = typeof score === "number" ? score.toString() : UTF8ToString(score);
    var levelStr = typeof level === "number" ? level.toString() : UTF8ToString(level);
    var imgStr = typeof imageBase64 === "string" ? imageBase64 : UTF8ToString(imageBase64);

    console.log("ShareResult called. Score: " + scoreStr + ", Level: " + levelStr);

    if (navigator.share) {
        var shareData = {
            title: '디저트 팝!',
            text: '제가 디저트 팝에서 점수 ' + scoreStr + '점, 레벨 ' + levelStr + '에 도달했어요!',
        };

        if (imgStr && imgStr.startsWith('data:image/')) {
            try {
                // Convert base64 to File object
                var arr = imgStr.split(',');
                var mime = arr[0].match(/:(.*?);/)[1];
                var bstr = atob(arr[1]);
                var n = bstr.length;
                var u8arr = new Uint8Array(n);
                while (n--) {
                    u8arr[n] = bstr.charCodeAt(n);
                }
                var file = new File([u8arr], 'result.png', { type: mime });

                // Add to share data if files are supported
                if (navigator.canShare && navigator.canShare({ files: [file] })) {
                    shareData.files = [file];
                }
            } catch (e) {
                console.error('Failed to convert base64 image', e);
            }
        }

        navigator.share(shareData).then(() => {
            console.log('Shared successfully');
        }).catch(console.error);
    } else {
        console.log('Web Share API not supported in this browser.');
    }
  },

  OnAdComplete: function() {
    SendMessage('BridgeManager', 'OnReviveSuccess');
  }
});
