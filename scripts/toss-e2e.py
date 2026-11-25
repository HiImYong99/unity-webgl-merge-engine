"""토스 번들(.ait) E2E — 헤드리스 Chrome(SwiftShader)으로 .ait 안의 sources/를 그대로 돌린다.

    python3 scripts/toss-e2e.py [ait-build/animal-pop.ait] [--test-bundle]

토스앱 밖이라 window.AppsInToss를 가짜 SDK로 바꾼다(광고·배너·IAP·프로모션 호출을 기록).
광고 사전 로드 큐·배너 시점·전면 고지·미션 리워드·공유 부활 삭제·도감 바 간격을 확인한다.
--test-bundle: build-ait.sh --test로 만든 번들 — 광고 호출이 ait-ad-test-* ID, 미션이 TEST_ 코드인지 확인.
창을 띄우지 않는다(포커스 안 뺏음). 게임오버는 SendMessage('GameManager','TriggerGameOver')로 당긴다.
"""
import argparse, functools, http.server, os, shutil, sys, tempfile, threading, time, zipfile
from playwright.sync_api import sync_playwright

ap = argparse.ArgumentParser()
ap.add_argument('ait', nargs='?', default=os.path.join(os.path.dirname(__file__), '../ait-build/animal-pop.ait'))
ap.add_argument('--test-bundle', action='store_true')
args = ap.parse_args()

ROOT = tempfile.mkdtemp(prefix='ait-e2e-')
with zipfile.ZipFile(args.ait) as z:
    for n in z.namelist():
        if n.startswith('sources/') and not n.endswith('/'):
            dst = os.path.join(ROOT, n[len('sources/'):])
            os.makedirs(os.path.dirname(dst), exist_ok=True)
            with open(dst, 'wb') as f:
                f.write(z.read(n))


class Handler(http.server.SimpleHTTPRequestHandler):
    def end_headers(self):
        path = self.path.split('?')[0]
        if path.endswith('.br'):
            self.send_header('Content-Encoding', 'br')
        if '.wasm' in path:
            self.send_header('Content-Type', 'application/wasm')
        self.send_header('Cache-Control', 'no-store')
        super().end_headers()

    def guess_type(self, path):
        if '.wasm' in path:
            return 'application/wasm'
        if path.endswith('.js.br') or path.endswith('.js'):
            return 'application/javascript'
        return super().guess_type(path)

    def log_message(self, *a):
        pass


srv = http.server.ThreadingHTTPServer(('127.0.0.1', 0), functools.partial(Handler, directory=ROOT))
threading.Thread(target=srv.serve_forever, daemon=True).start()
URL = f'http://127.0.0.1:{srv.server_port}/index.html'

fails = []


def check(name, ok, detail=''):
    print(('PASS ' if ok else 'FAIL ') + name + (f' — {detail}' if detail else ''), flush=True)
    if not ok:
        fails.append(name)


# 가짜 SDK. window.__cfg로 광고 지연·실패·보상 여부를 바꾸고, window.__log에 호출을 남긴다.
# 브리지(module script)가 진짜 SDK 네임스페이스를 대입하면 무시한다(모듈 네임스페이스가 아닌 객체만 받는다).
STUB = r"""
window.__log = [];
window.__cfg = { loadDelay: {}, delayQueue: {}, loadFail: {}, showError: {}, showMs: 500, rewardMode: 'reward', orders: [] };
(function () {
  function kind(id) {
    return /79b8c799130343ec|ait-ad-test-rewarded-id/.test(id) ? 'rewarded'
      : /f8b6b46c862f48f4|ait-ad-test-interstitial-id/.test(id) ? 'interstitial' : 'other';
  }
  // cache: 그룹별로 받아 둔 광고(로드 번호) 목록. showing: 지금 화면에 떠 있는 광고의 로드 번호
  var cache = {}, inflight = 0, loadSeq = 0, showing = {};
  window.__maxInflight = 0; window.__bannerLoading = false; window.__loadsDuringBanner = 0;
  function log(ev, id, extra) {
    var e = { t: Date.now(), ev: ev, id: id, kind: kind(id) };
    for (var k in (extra || {})) e[k] = extra[k];
    window.__log.push(e);
  }
  function supported(f) { f.isSupported = function () { return true; }; return f; }
  var loadFullScreenAd = supported(function (p) {
    var id = p.options.adGroupId, dead = false, seq = ++loadSeq;
    inflight++; window.__maxInflight = Math.max(window.__maxInflight, inflight);
    if (window.__bannerLoading) window.__loadsDuringBanner++;
    log('load', id, { inflight: inflight });
    var q = window.__cfg.delayQueue[kind(id)];
    var d = q && q.length ? q.shift() : window.__cfg.loadDelay[kind(id)];
    setTimeout(function () {
      inflight--;
      if (window.__cfg.loadFail[kind(id)]) { log('loadError', id); if (!dead) p.onError(new Error('no fill')); return; }
      (cache[id] = cache[id] || []).push(seq);
      log('loaded', id);
      if (!dead) p.onEvent({ type: 'loaded' });
    }, d == null ? 300 : d);
    // 떠 있는 광고의 로드 리스너를 표시 도중에 해제하면 기록 (광고가 닫힌 뒤에 해제해야 함)
    return function () { dead = true; if (showing[seq]) log('unregDuringShow', id); };
  });
  var showFullScreenAd = supported(function (p) {
    var id = p.options.adGroupId, dead = false;
    function emit(e) { if (!dead) p.onEvent(e); }
    log('show', id);
    if (!(cache[id] && cache[id].length)) { setTimeout(function () { log('failedToShow', id); emit({ type: 'failedToShow' }); }, 50); return function () { dead = true; }; }
    var seq = cache[id].pop(); // 가장 최근에 받은 광고 (템플릿 슬롯이 들고 있는 것)
    if (window.__cfg.showError[kind(id)]) {
      setTimeout(function () { log('showError', id); if (!dead) p.onError(new Error('show failed')); }, 50);
      return function () { dead = true; };
    }
    setTimeout(function () { showing[seq] = true; emit({ type: 'requested' }); log('shown', id); emit({ type: 'show' }); }, 100);
    if (kind(id) === 'rewarded' && window.__cfg.rewardMode === 'reward')
      setTimeout(function () { log('reward', id); emit({ type: 'userEarnedReward', data: { unitType: 'x', unitAmount: 1 } }); }, 300);
    setTimeout(function () { delete showing[seq]; log('dismissed', id); emit({ type: 'dismissed' }); }, window.__cfg.showMs);
    return function () { dead = true; };
  });
  var TossAds = {
    initialize: supported(function (o) { log('bannerInit', ''); setTimeout(function () { o.callbacks.onInitialized(); }, 50); }),
    attachBanner: supported(function (id, el, opts) {
      log('banner', id, { inflight: inflight });
      window.__bannerLoading = true;
      setTimeout(function () {
        window.__bannerLoading = false;
        var d = document.createElement('div'); d.style.height = '96px'; el.appendChild(d);
        log('bannerRendered', id);
        opts.callbacks.onAdRendered({ slotId: 's1', adGroupId: id });
      }, 400);
      return { destroy: function () { log('bannerDestroy', id); } };
    }),
    destroyAll: supported(function () {})
  };
  var IAP = {
    getPendingOrders: function () {
      // 미지원 토스앱처럼 동기 throw (SDK withUnsupportedThrow)
      if (window.__cfg.pendingThrows) { log('iapPendingThrow', ''); throw new Error('UNSUPPORTED_APP_VERSION'); }
      return Promise.resolve({ orders: [] });
    },
    getCompletedOrRefundedOrders: supported(function () { log('iapCompleted', ''); return Promise.resolve({ hasNext: false, orders: window.__cfg.orders }); }),
    completeProductGrant: function () { return Promise.resolve(true); },
    createOneTimePurchaseOrder: function () { return function () {}; }
  };
  function anyFn() {
    var f = function () { return Promise.resolve(undefined); };
    return new Proxy(f, { get: function (t, k) {
      if (k === 'then') return undefined;
      if (k === 'isSupported') return function () { return false; };
      if (typeof k === 'symbol') return t[k];
      return anyFn();
    } });
  }
  var known = {
    loadFullScreenAd: loadFullScreenAd, showFullScreenAd: showFullScreenAd, TossAds: TossAds, IAP: IAP,
    grantPromotionRewardForGame: function (o) { log('grant', o.params.promotionCode, { amount: o.params.amount }); return Promise.resolve({ key: 'k1' }); },
    getGameCenterGameProfile: function () { return Promise.resolve(undefined); },
    getUserKeyForGame: function () { return Promise.resolve({ hash: 'e2e' }); },
    SafeAreaInsets: { get: function () { return { top: 0, bottom: 0, left: 0, right: 0 }; } }
  };
  var mock = new Proxy(known, {
    get: function (t, k) { if (k in t) return t[k]; if (typeof k === 'symbol') return undefined; return anyFn(); },
    defineProperty: function () { return false; },
    set: function () { return true; }
  });
  var current = mock;
  Object.defineProperty(window, 'AppsInToss', {
    configurable: true,
    get: function () { return current; },
    set: function (v) { if (v && v[Symbol.toStringTag] !== 'Module') current = v; }
  });
})();
"""

with sync_playwright() as p:
    browser = p.chromium.launch(channel='chrome', headless=True, args=[
        '--use-angle=swiftshader', '--enable-unsafe-swiftshader', '--ignore-gpu-blocklist', '--mute-audio'])

    def open_page(init_js=''):
        ctx = browser.new_context(viewport={'width': 390, 'height': 844}, locale='ko-KR')
        page = ctx.new_page()
        page.add_init_script(STUB + init_js)
        errors = []
        page.on('pageerror', lambda e: errors.append(str(e)))
        page.on('response', lambda r: r.status >= 400 and not r.url.endswith('favicon.ico') and errors.append(f'HTTP {r.status} {r.url}'))
        t0 = time.time()
        page.goto(URL)
        page.wait_for_selector('#landing-overlay.visible', timeout=240000)
        return page, errors, time.time() - t0

    page, errors, dt = open_page()
    ev = lambda js: page.evaluate(js)
    log = lambda: ev('window.__log')
    events = lambda name, kind=None: [e for e in log() if e['ev'] == name and (kind is None or e['kind'] == kind)]

    def wait_for(js, timeout=10.0):
        end = time.time() + timeout
        while time.time() < end:
            if ev(js):
                return True
            time.sleep(0.1)
        return False

    check('landing opens', True, f'{dt:.1f}s')
    check('platform = toss', ev('window.AP_PLATFORM') == 'toss')
    wait_for("window.__log.some(e => e.ev === 'loaded' && e.kind === 'rewarded')")
    time.sleep(1.5)
    check('revive preloaded at Unity ready', len(events('loaded', 'rewarded')) == 1)
    check('no banner on loading/landing', not events('bannerInit') and not events('banner'))
    check('share revive removed', ev("!document.getElementById('go-btn-share-revive') && typeof onShareReviveClicked === 'undefined'"))
    check('iap restore checked', len(events('iapCompleted')) == 1)

    # 첫 게임 시작 → 배너는 받는 중인 전면·리워드가 없을 때 한 번 붙는다
    ev("window.__cfg.loadDelay.rewarded = 300")
    page.click('.lg-start-btn')
    check('banner attached after start', wait_for("window.__log.some(e => e.ev === 'bannerRendered')", 5))
    b = events('banner')
    check('banner attached once, with no fullscreen load in flight', len(b) == 1 and b[0]['inflight'] == 0, str(b))
    time.sleep(1)  # HUD·도감 바 전환 애니메이션이 끝난 뒤
    gap = ev("""(() => {
        const banner = document.getElementById('banner-ad-container').getBoundingClientRect();
        const dex = document.getElementById('dex-bar').getBoundingClientRect();
        return { gap: banner.top - dex.bottom, bannerTop: banner.top, dexBottom: dex.bottom,
                 visible: document.getElementById('banner-ad-container').classList.contains('visible') };
    })()""")
    check('dex bar >= 32px above banner', gap['visible'] and gap['gap'] >= 32, str(gap))

    def game_over():
        ev("unityInstance.SendMessage('GameManager', 'TriggerGameOver')")
        page.wait_for_selector('#gameover-overlay.visible', timeout=15000)
        time.sleep(0.3)

    # 게임오버 버튼은 애니메이션 중이라 Playwright 클릭이 'not stable'로 막힌다 → DOM click
    click = lambda sel: ev(f"document.querySelector('{sel}').click()")
    restart_html = lambda: ev("document.getElementById('go-btn-restart').textContent.trim()")
    hud_visible = lambda: ev("document.getElementById('game-hud').classList.contains('visible')")

    banner_visibility = lambda: ev("getComputedStyle(document.getElementById('banner-ad-container')).visibility")

    # 1) 게임오버 → 전면 고지 + 부활 리워드 뒤에 전면 사전 로드
    game_over()
    check('banner hidden behind game-over modal', banner_visibility() == 'hidden', banner_visibility())
    check('restart shows ad notice', restart_html() == '광고 보고 처음부터 다시하기', restart_html())
    check('mission notice visible', ev("""(() => { const n = document.querySelector('#go-mission .go-mission-notice');
        return !!n && getComputedStyle(document.getElementById('go-mission')).display !== 'none' && n.textContent.includes('사전 고지 없이 중단될 수 있어요'); })()"""))
    check('interstitial preloaded at game over', wait_for("window.__log.some(e => e.ev === 'loaded' && e.kind === 'interstitial')", 5))
    n_inter_loads = len(events('load', 'interstitial'))
    click('#go-btn-restart')
    wait_for("window.__log.some(e => e.ev === 'dismissed' && e.kind === 'interstitial')", 3)
    shows = events('show', 'interstitial')
    check('restart shows preloaded interstitial immediately', len(shows) == 1 and len(events('load', 'interstitial')) == n_inter_loads)
    check('new game after interstitial', wait_for("document.getElementById('game-hud').classList.contains('visible')", 5))
    check('banner visible again in game', banner_visibility() == 'visible', banner_visibility())
    # 모달 뒤 배너 숨김은 :has()가 아니라 클래스(behind-modal) — 구형 WebView에서도 동작
    ev("window._showExitConfirmModal()")
    time.sleep(0.1)
    check('banner hidden behind exit modal via class', banner_visibility() == 'hidden'
          and ev("document.getElementById('banner-ad-container').classList.contains('behind-modal')"), banner_visibility())
    ev("document.getElementById('exit-confirm-modal').remove()")
    time.sleep(0.1)
    check('banner visible after exit modal closed', banner_visibility() == 'visible'
          and not ev("document.getElementById('banner-ad-container').classList.contains('behind-modal')"), banner_visibility())

    # 2) 75초 안에 다시 게임오버 → 고지 없음, 광고 없음
    game_over()
    check('no notice inside 75s interval', restart_html() in ('처음부터 다시하기', '다시 도전하기'), restart_html())
    time.sleep(0.8)
    n_inter_loads = len(events('load', 'interstitial'))
    click('#go-btn-restart')
    time.sleep(1.2)
    check('no interstitial when not announced', len(events('show', 'interstitial')) == 1 and hud_visible())

    # 3) 전면이 늦게 오면 1초 뒤 광고 없이 재시작, 늦게 온 광고는 다음 게임오버에서 바로 씀
    ev("window._lastInterstitialAt = 0; window.__cfg.loadDelay.interstitial = 3000")
    game_over()
    check('notice again after interval', restart_html() == '광고 보고 처음부터 다시하기', restart_html())
    t0 = time.time()
    click('#go-btn-restart')
    wait_for("document.getElementById('game-hud').classList.contains('visible')", 4)
    waited = time.time() - t0
    check('late interstitial: restart without ad within ~1s', hud_visible() and len(events('show', 'interstitial')) == 1 and waited < 2.5, f'{waited:.1f}s')
    check('late interstitial kept for next time', wait_for("window.__log.filter(e => e.ev === 'loaded' && e.kind === 'interstitial').length === 2", 5))
    ev("window._lastInterstitialAt = 0")
    n_inter_loads = len(events('load', 'interstitial'))
    game_over()
    time.sleep(0.5)
    click('#go-btn-restart')
    wait_for("window.__log.filter(e => e.ev === 'show' && e.kind === 'interstitial').length === 2", 3)
    check('late ad shown next time without a new load', len(events('show', 'interstitial')) == 2 and len(events('load', 'interstitial')) == n_inter_loads)
    wait_for("document.getElementById('game-hud').classList.contains('visible')", 5)

    # 4) 미션 보상: 부활 슬롯을 같이 써서 받아 둔 광고를 바로 보여줌, 두 번째 로드 없음
    ev("""localStorage.setItem('animalpop_mission_play10_count', '10');
          localStorage.removeItem('animalpop_mission_play10_claimday');""")
    game_over()
    check('mission claim button visible', ev("getComputedStyle(document.getElementById('go-mission-btn')).display !== 'none'"))
    n_rew_loads = len(events('load', 'rewarded'))
    ev("window.__cfg.loadDelay.rewarded = 1500")
    click('#go-mission-btn')
    check('mission grant after reward', wait_for("window.__log.some(e => e.ev === 'grant')", 4))
    g = events('grant')
    want_code = 'TEST_01KVDFRBSY7XJBJRNH1HC4D8TV' if args.test_bundle else '01KVDFRBSY7XJBJRNH1HC4D8TV'
    check('mission promotion code', g and g[0]['id'] == want_code and g[0]['amount'] == 15, str(g))
    rew_shows = events('show', 'rewarded')
    check('mission used preloaded revive ad (no load before show)', len(rew_shows) == 1 and
          len([e for e in log() if e['ev'] == 'load' and e['kind'] == 'rewarded' and e['t'] <= rew_shows[0]['t']]) == n_rew_loads)
    check('revive slot refilled after mission ad', wait_for(f"window.__log.filter(e => e.ev === 'load' && e.kind === 'rewarded').length === {n_rew_loads + 1}", 3))

    # 5) 부활: 받는 중인 부활 광고를 기다렸다가 jslib에 넘김 (jslib가 같은 그룹을 또 로드하지 않음)
    time.sleep(0.2)
    click('#go-btn-revive')
    check('revived via rewarded ad', wait_for("!document.getElementById('gameover-overlay').classList.contains('visible')", 6))
    wait_for("window.__log.filter(e => e.ev === 'dismissed' && e.kind === 'rewarded').length === 2", 5)
    time.sleep(0.5)
    rew = [e for e in log() if e['kind'] == 'rewarded']
    show2 = [e for e in rew if e['ev'] == 'show'][1]
    loads_before_show2 = len([e for e in rew if e['ev'] == 'load' and e['t'] <= show2['t']])
    # 미션 뒤 채우기 1회만 — jslib가 받는 중인 광고를 기다려 바로 보여줌(자체 로드 없음). 닫은 뒤 jslib가 다시 채움 +1
    check('no duplicate revive load while one in flight', loads_before_show2 == n_rew_loads + 1 and len(events('load', 'rewarded')) == n_rew_loads + 2,
          str([(e['ev'], e['kind']) for e in rew]))

    toast_text = lambda: ev("(document.getElementById('ad-unavail-toast') || {}).textContent || ''")
    go_visible = lambda: ev("document.getElementById('gameover-overlay').classList.contains('visible')")
    rew_loads = lambda: len(events('load', 'rewarded'))

    def settle_rewarded():
        wait_for("!_adInFlight(REVIVE_AD_ID)", 8)

    def fresh_game():
        # 부활은 한 판에 광고 1번 → 새 판에서 다시 게임오버를 만든다
        game_over()
        click('#go-btn-restart')
        wait_for("document.getElementById('game-hud').classList.contains('visible')", 5)
        time.sleep(0.3)

    # 5b) 부활 광고가 2초 넘게 걸리면 jslib에 넘기지 않고(큐 밖 로드 없음) '준비 중' 안내, 늦게 온 광고는 다음 탭에 바로 씀
    settle_rewarded()
    fresh_game()
    settle_rewarded()
    ev("_adReset(_adSlot(REVIVE_AD_ID)); window.__cfg.loadDelay.rewarded = 3000; window._lastInterstitialAt = Date.now()")
    game_over()
    n0 = rew_loads()
    n_loaded = len(events('loaded', 'rewarded'))
    click('#go-btn-revive')
    time.sleep(2.4)
    check('slow revive ad: preparing toast, no jslib load', toast_text() == '광고를 준비하고 있어요. 잠시 후 다시 눌러 주세요'
          and go_visible() and rew_loads() == n0, f'toast={toast_text()!r} loads={rew_loads() - n0}')
    wait_for(f"window.__log.filter(e => e.ev === 'loaded' && e.kind === 'rewarded').length === {n_loaded + 1}", 3)
    n_show = len(events('show', 'rewarded'))
    click('#go-btn-revive')
    check('late revive ad used on next tap', wait_for("!document.getElementById('gameover-overlay').classList.contains('visible')", 3)
          and len(events('show', 'rewarded')) == n_show + 1 and len([e for e in events('load', 'rewarded') if e['t'] <= events('show', 'rewarded')[-1]['t']]) == n0)

    # 5c) 부활 사전 로드 실패 → 탭하면 큐 맨 앞으로 다시 받음(받는 중인 전면과 겹치지 않음), jslib 자체 로드 없음
    settle_rewarded()
    fresh_game()
    settle_rewarded()
    ev("""_adReset(_adSlot(REVIVE_AD_ID)); _adReset(_adSlot(INTERSTITIAL_AD_ID));
          window.__cfg.loadFail.rewarded = true; window.__cfg.loadDelay.rewarded = 300;
          window.__cfg.loadDelay.interstitial = 900; window._lastInterstitialAt = 0;""")
    n_err = len(events('loadError', 'rewarded'))
    game_over()
    wait_for(f"window.__log.filter(e => e.ev === 'loadError' && e.kind === 'rewarded').length === {n_err + 1}", 3)
    ev("window.__cfg.loadFail.rewarded = false")
    n0 = rew_loads()
    t_tap = ev('Date.now()')
    click('#go-btn-revive')
    revived = wait_for("!document.getElementById('gameover-overlay').classList.contains('visible')", 4)
    shows_after = [e for e in events('show', 'rewarded') if e['t'] >= t_tap]
    loads_between = [e for e in events('load', 'rewarded') if e['t'] >= t_tap and shows_after and e['t'] <= shows_after[0]['t']]
    check('failed preload: revive re-queued, one load, no overlap', revived and len(loads_between) == 1 and ev('window.__maxInflight') == 1,
          f'revived={revived} loads={len(loads_between)} maxInflight={ev("window.__maxInflight")}')

    # 5d) 부활 광고가 계속 실패하면 안내만 하고 게임오버 화면 유지 (jslib로 넘기지 않음)
    settle_rewarded()
    fresh_game()
    settle_rewarded()
    ev("_adReset(_adSlot(REVIVE_AD_ID)); window.__cfg.loadFail.rewarded = true; window._lastInterstitialAt = Date.now()")
    game_over()
    settle_rewarded()
    n0 = rew_loads()
    click('#go-btn-revive')
    time.sleep(0.8)
    check('revive ad unavailable: toast, stays on game over, one queued load', toast_text() == '광고를 불러올 수 없어요'
          and go_visible() and rew_loads() == n0 + 1, f'toast={toast_text()!r} loads={rew_loads() - n0}')

    # 5e) 부활과 미션을 같이 누름 — 받은 광고 1개를 한 곳만 보여줌 (같은 광고 두 번 show 없음)
    ev("window.__cfg.loadFail.rewarded = false")
    click('#go-btn-restart')
    wait_for("document.getElementById('game-hud').classList.contains('visible')", 5)
    settle_rewarded()
    ev("""localStorage.setItem('animalpop_mission_play10_count', '10');
          localStorage.removeItem('animalpop_mission_play10_claimday');
          _adReset(_adSlot(REVIVE_AD_ID)); window.__cfg.loadDelay.rewarded = 800; window._lastInterstitialAt = Date.now();""")
    n_fail = len(events('failedToShow'))
    game_over()
    both = ev("getComputedStyle(document.getElementById('go-mission-btn')).display !== 'none' && getComputedStyle(document.getElementById('go-btn-revive')).display !== 'none'")
    click('#go-btn-revive')
    click('#go-mission-btn')
    wait_for("!document.getElementById('gameover-overlay').classList.contains('visible')", 4)
    time.sleep(3)
    check('revive + mission together: no double show of one ad', both and len(events('failedToShow')) == n_fail,
          f'both={both} failedToShow={len(events("failedToShow")) - n_fail}')

    # 5f) 부활 광고를 jslib에 넘겨 보여주는 중에 미션이 끝나도(대기 초과) 그 광고의 로드 리스너를 해제하지 않음
    settle_rewarded()
    fresh_game()
    settle_rewarded()
    ev("""localStorage.setItem('animalpop_mission_play10_count', '10');
          localStorage.removeItem('animalpop_mission_play10_claimday');
          _adReset(_adSlot(REVIVE_AD_ID)); window.__cfg.delayQueue.rewarded = [800, 5000];
          window.__cfg.showMs = 3500; window._lastInterstitialAt = Date.now();""")
    n_unreg = len(events('unregDuringShow'))
    n_dis = len(events('dismissed', 'rewarded'))
    game_over()
    click('#go-btn-revive')
    click('#go-mission-btn')
    revived = wait_for("!document.getElementById('gameover-overlay').classList.contains('visible')", 4)
    wait_for(f"window.__log.filter(e => e.ev === 'dismissed' && e.kind === 'rewarded').length > {n_dis}", 6)
    time.sleep(0.2)
    check('mission ends while jslib revive ad showing: listener kept until close', revived
          and len(events('unregDuringShow')) == n_unreg and ev('window._reviveHandoffActive === false'),
          f'revived={revived} unregDuringShow={len(events("unregDuringShow")) - n_unreg}')
    ev("window.__cfg.showMs = 500; window.__cfg.delayQueue.rewarded = []")

    # 5g) jslib 표시 오류(onError) 경로 — 핸드오프를 정리하고 슬롯을 다시 채워 다음 탭은 바로 보여줌
    settle_rewarded()
    fresh_game()
    settle_rewarded()
    ev("window.__cfg.showError.rewarded = true; window._lastInterstitialAt = Date.now()")
    game_over()
    settle_rewarded()
    n0 = rew_loads()
    click('#go-btn-revive')
    refilled = wait_for(f"window.__log.filter(e => e.ev === 'load' && e.kind === 'rewarded').length === {n0 + 1}", 2)
    check('jslib show error: toast, handoff released, revive slot refilled', refilled and go_visible()
          and toast_text() == '광고를 불러올 수 없어요' and ev('window._reviveHandoffActive === false'),
          f'refilled={refilled} toast={toast_text()!r} active={ev("window._reviveHandoffActive")}')
    ev("window.__cfg.showError.rewarded = false")
    settle_rewarded()
    n_show = len(events('show', 'rewarded'))
    n0 = rew_loads()
    click('#go-btn-revive')
    check('after show error: next tap shows refilled ad at once', wait_for("!document.getElementById('gameover-overlay').classList.contains('visible')", 2)
          and len(events('show', 'rewarded')) == n_show + 1
          and len([e for e in events('load', 'rewarded') if e['t'] <= events('show', 'rewarded')[-1]['t']]) == n0)

    # 5h) 부활 연타 — 광고가 뜨기 전 두 번째 탭은 무시 (광고 하나만, 뜨는 중인 광고의 로드 리스너 유지)
    settle_rewarded()
    fresh_game()
    settle_rewarded()
    ev("window.__cfg.showMs = 1500; window.__cfg.loadDelay.rewarded = 300; window._lastInterstitialAt = Date.now()")
    game_over()
    settle_rewarded()
    n_show = len(events('show', 'rewarded'))
    n_unreg = len(events('unregDuringShow'))
    n_dis = len(events('dismissed', 'rewarded'))
    click('#go-btn-revive')
    click('#go-btn-revive')
    revived = wait_for("!document.getElementById('gameover-overlay').classList.contains('visible')", 3)
    wait_for(f"window.__log.filter(e => e.ev === 'dismissed' && e.kind === 'rewarded').length > {n_dis}", 4)
    time.sleep(0.8)
    check('double tap on revive: one ad, listener kept while showing', revived
          and len(events('show', 'rewarded')) == n_show + 1 and len(events('unregDuringShow')) == n_unreg,
          f'shows={len(events("show", "rewarded")) - n_show} unregDuringShow={len(events("unregDuringShow")) - n_unreg}')
    ev("window.__cfg.showMs = 500")

    # 6) 동시 로드 없음 (전체 기록)
    check('fullscreen loads never overlap', ev('window.__maxInflight') == 1, str(ev('window.__maxInflight')))
    check('no fullscreen load started while banner loading', ev('window.__loadsDuringBanner') == 0)
    ids = {e['id'] for e in log() if e['ev'] in ('load', 'show', 'banner')}
    if args.test_bundle:
        check('test bundle uses only test ad IDs', ids and all(i.startswith('ait-ad-test-') for i in ids), str(ids))
    else:
        check('live bundle uses live ad IDs', ids and all(i.startswith('ait.v2.live.') for i in ids), str(ids))
    check('no page errors', not errors, '; '.join(errors[:5]))
    page.context.close()

    # 7) 평생 소장 구매 복원 (완료 주문 → 광고 제거, 환불된 주문은 제외)
    SKU = 'ait.0000022018.560c8f2d.99adbacd5a.3325211470'
    # getPendingOrders가 미지원 토스앱처럼 동기 throw해도 템플릿 쪽은 로딩 실패로 번지지 않고 완료 주문 복원은 된다
    # (jslib TossIAPRestorePendingOrders도 같은 호출을 가드 없이 해서 page error가 하나 남는다 — jslib는 이번 범위 밖)
    page, errors, _ = open_page(f"window.__cfg.pendingThrows = true; window.__cfg.orders = [{{orderId: 'o1', sku: '{SKU}', status: 'COMPLETED', date: '2026-04-08T00:00:00'}}];")
    ev = lambda js: page.evaluate(js)
    time.sleep(2)
    check('premium restored from completed order', ev('_premiumSpeedOwned === true'))
    check('unsupported getPendingOrders does not fail loading', ev("window.__log.some(e => e.ev === 'iapPendingThrow')")
          and ev("document.getElementById('loading-text').textContent") != '로딩에 실패했어요'
          and all('UNSUPPORTED_APP_VERSION' in e for e in errors), '; '.join(errors[:3]))
    page.click('.lg-start-btn')
    time.sleep(1.5)
    check('no banner for restored premium', not any(e['ev'] == 'banner' for e in ev('window.__log')))
    page.context.close()
    page, errors, _ = open_page(f"window.__cfg.orders = [{{orderId: 'o1', sku: '{SKU}', status: 'REFUNDED', date: '2026-04-08T00:00:00'}}];")
    ev = lambda js: page.evaluate(js)
    time.sleep(2)
    check('refunded order not restored', ev('_premiumSpeedOwned === false'))
    page.context.close()
    browser.close()

shutil.rmtree(ROOT, ignore_errors=True)
print('\n' + ('ALL PASS' if not fails else f'{len(fails)} FAIL: ' + ', '.join(fails)))
sys.exit(1 if fails else 0)
