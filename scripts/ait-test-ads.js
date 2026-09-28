// 테스트 번들 전용 — build-ait.sh --test가 dist/web/index.html의 브리지 module script 바로 뒤에 주입한다.
// 운영 번들에는 들어가지 않는다.
// 운영 광고 그룹 ID로 QR 테스트를 하면 제재 대상이라, SDK를 부르기 직전에 운영 ID를 테스트 ID로 바꾼다.
// 템플릿과 jslib(ShowTossAd)가 모두 window.AppsInToss를 호출 시점에 읽으므로 여기서 한 번 감싸면 된다.
(function () {
    var TEST_IDS = {
        'ait.v2.live.79b8c799130343ec': 'ait-ad-test-rewarded-id',     // 리워드 (부활·2배속·미션)
        'ait.v2.live.f8b6b46c862f48f4': 'ait-ad-test-interstitial-id', // 전면 (처음부터 다시하기)
        'ait.v2.live.629331886f8c49bd': 'ait-ad-test-banner-id'        // 배너
    };
    var sdk = window.AppsInToss;
    if (!sdk) return;

    function testId(id) { return TEST_IDS[id] || id; }
    // 원본을 프로토타입으로 둔 객체 — 바꾼 함수만 자기 속성이고 나머지는 원본을 그대로 읽는다
    function derive(src) { return Object.create(src); }
    // 원본 속성이 읽기 전용(고정된 네임스페이스)이면 대입이 조용히 무시되므로 defineProperty로 덮는다
    function set(obj, key, value) {
        Object.defineProperty(obj, key, { value: value, writable: true, configurable: true, enumerable: true });
    }
    function wrapFullScreen(owner, fn) {
        if (typeof fn !== 'function') return fn;
        var wrapped = function (params) {
            if (params && params.options) {
                params = Object.assign({}, params, {
                    options: Object.assign({}, params.options, { adGroupId: testId(params.options.adGroupId) })
                });
            }
            return fn.call(owner, params);
        };
        wrapped.isSupported = fn.isSupported;
        return wrapped;
    }

    // 모듈 네임스페이스 객체는 고정돼 있어 속성을 바꿀 수 없다 → 파생 객체를 만들어 바꿔 끼운다
    var t = derive(sdk);
    set(t, 'loadFullScreenAd', wrapFullScreen(sdk, sdk.loadFullScreenAd));
    set(t, 'showFullScreenAd', wrapFullScreen(sdk, sdk.showFullScreenAd));
    if (sdk.GoogleAdMob) {
        var g = derive(sdk.GoogleAdMob);
        set(g, 'loadAppsInTossAdMob', wrapFullScreen(sdk.GoogleAdMob, sdk.GoogleAdMob.loadAppsInTossAdMob));
        set(g, 'showAppsInTossAdMob', wrapFullScreen(sdk.GoogleAdMob, sdk.GoogleAdMob.showAppsInTossAdMob));
        set(t, 'GoogleAdMob', g);
    }
    if (sdk.TossAds && typeof sdk.TossAds.attachBanner === 'function') {
        var attach = sdk.TossAds.attachBanner;
        var ads = derive(sdk.TossAds);
        var attachTest = function (adGroupId) {
            var args = Array.prototype.slice.call(arguments);
            args[0] = testId(adGroupId);
            return attach.apply(sdk.TossAds, args);
        };
        attachTest.isSupported = attach.isSupported;
        set(ads, 'attachBanner', attachTest);
        set(t, 'TossAds', ads);
    }
    window.AppsInToss = t;
    console.log('[TEST ADS] 테스트 광고 ID로 바꿔 부르고 있어요');
})();
