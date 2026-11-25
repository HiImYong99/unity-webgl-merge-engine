"""Playgama 빌드 E2E — 헤드리스 Chrome(SwiftShader)으로 build/animal-pop-playgama를 실제로 돌린다.

    python3 playgama/test/e2e.py [--encoding] [--shots DIR]

localhost라 브리지는 'mock' 플랫폼으로 뜬다(광고는 mock 상태 이벤트, 저장소는 localStorage).
--encoding: 서버가 .gz에 Content-Encoding: gzip을 붙이는 포털 흉내 (fetch 심의 통과 경로).
창을 띄우지 않는다(포커스 안 뺏음). 게임오버는 SendMessage('GameManager','TriggerGameOver')로 당긴다.
"""
import argparse, functools, http.server, json, os, sys, threading, time
from urllib.parse import urlparse
from playwright.sync_api import sync_playwright

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), '../../build/animal-pop-playgama'))
ap = argparse.ArgumentParser()
ap.add_argument('--encoding', action='store_true')
ap.add_argument('--shots', default=None)
args = ap.parse_args()


class Handler(http.server.SimpleHTTPRequestHandler):
    def end_headers(self):
        if args.encoding and self.path.split('?')[0].endswith('.gz'):
            self.send_header('Content-Encoding', 'gzip')
        self.send_header('Cache-Control', 'no-store')
        super().end_headers()

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


# mock 플랫폼은 광고 미지원 → 광고 모듈을 가짜로 바꾸고, 포털 메시지를 기록한다.
# 브리지 초기화가 끝난 직후(호스트 코드가 모듈에 처음 접근하기 전)에 설치해야 한다.
# window.__adMode: 'reward'(기본) | 'close'(중간에 닫음) | 'fail'
STUB = """
window.__pgMessages = [];
window.__adMode = 'reward';
(function () {
  function pin(obj, name, value) {
    Object.defineProperty(obj, name, { configurable: true, enumerable: true, get: function () { return value; }, set: function () {} });
  }
  function install(b) {
    // 모듈은 Proxy라 첫 접근 때 메서드 래퍼를 캐시한다 → 첫 접근 전에 프로토타입을 감싼다
    var proto = Object.getPrototypeOf(b.platform), send = proto.sendMessage;
    proto.sendMessage = function (m) { window.__pgMessages.push(m); return send.apply(this, arguments); };
    var ad = b.advertisement, subs = {}, state = { interstitial: 'closed', rewarded: 'closed' };
    pin(ad, 'isInterstitialSupported', true);
    pin(ad, 'isRewardedSupported', true);
    Object.defineProperty(ad, 'interstitialState', { configurable: true, get: function () { return state.interstitial; } });
    Object.defineProperty(ad, 'rewardedState', { configurable: true, get: function () { return state.rewarded; } });
    pin(ad, 'on', function (e, f) { (subs[e] = subs[e] || []).push(f); });
    pin(ad, 'off', function (e, f) { subs[e] = (subs[e] || []).filter(function (g) { return g !== f; }); });
    function play(kind, event, seq) {
      seq.forEach(function (st, i) {
        setTimeout(function () { state[kind] = st; (subs[event] || []).slice().forEach(function (f) { f(st); }); }, 300 * (i + 1));
      });
    }
    pin(ad, 'showInterstitial', function () { play('interstitial', 'interstitial_state_changed', ['loading', 'opened', 'closed']); });
    pin(ad, 'showRewarded', function () {
      var m = window.__adMode;
      play('rewarded', 'rewarded_state_changed',
        m === 'fail' ? ['loading', 'failed'] : m === 'close' ? ['loading', 'opened', 'closed'] : ['loading', 'opened', 'rewarded', 'closed']);
    });
  }
  // SDK가 window.bridge를 대입하는 순간 initialize를 감싸, 초기화 직후(호스트 코드가 이어지기 전) 설치
  Object.defineProperty(window, 'bridge', {
    configurable: true,
    set: function (b) {
      Object.defineProperty(window, 'bridge', { value: b, writable: true, configurable: true, enumerable: true });
      var init = b.initialize;
      b.initialize = function () {
        return init.apply(b, arguments).then(function (r) { install(b); return r; });
      };
    },
  });
})();
"""

with sync_playwright() as p:
    browser = p.chromium.launch(channel='chrome', headless=True, args=[
        '--use-angle=swiftshader', '--enable-unsafe-swiftshader', '--ignore-gpu-blocklist', '--mute-audio'])
    ctx = browser.new_context(viewport={'width': 390, 'height': 844}, locale='en-US')
    page = ctx.new_page()
    page.add_init_script(STUB)
    hosts, errors = set(), []
    page.on('request', lambda r: hosts.add(urlparse(r.url).hostname))
    page.on('pageerror', lambda e: errors.append(str(e)))
    page.on('console', lambda m: m.type == 'error' and 'Failed to load resource' not in m.text and errors.append(m.text))
    page.on('response', lambda r: r.status >= 400 and not r.url.endswith('favicon.ico') and errors.append(f'HTTP {r.status} {r.url}'))

    t0 = time.time()
    page.goto(URL)
    page.wait_for_selector('#landing-overlay.visible', timeout=180000)
    check('landing opens', True, f'{time.time() - t0:.1f}s')
    ev = lambda js: page.evaluate(js)
    check('platform = playgama', ev('window.AP_PLATFORM') == 'playgama')
    check('bridge platform', ev('PlaygamaHost.platformId()') == 'mock', ev('PlaygamaHost.platformId()'))
    time.sleep(0.5)
    check('game_ready sent once', ev('__pgMessages.filter(m => m === "game_ready").length') == 1, ev('JSON.stringify(__pgMessages)'))
    hidden = ev("""['lg-leaderboard-btn','go-leaderboard-btn','go-btn-share','lg-restore','go-restore','settings-restore']
        .filter(id => { var e = document.getElementById(id); return e && getComputedStyle(e).display !== 'none'; })""")
    check('leaderboard/share/restore hidden', hidden == [], hidden)
    check('no back-button trap', ev('history.length') <= 2, ev('history.length'))  # about:blank + 페이지
    check('Nunito local font', ev("document.fonts.check('900 20px Nunito')"))
    gain = lambda: ev("_unityAudioContext && _unityAudioContext.destination instanceof GainNode ? _unityAudioContext.destination.gain.value : null")
    check('master gain in front of Unity audio', gain() == 1, gain())
    if args.shots:
        os.makedirs(args.shots, exist_ok=True)
        page.screenshot(path=f'{args.shots}/1_landing_portrait.png')

    # 시작 → 실제 드롭 몇 번
    page.click('.lg-start-btn')
    page.wait_for_selector('#game-hud.visible', timeout=10000)
    check('level_started', 'level_started' in ev('__pgMessages'))
    for i in range(8):
        page.mouse.click(150 + (i % 4) * 30, 300)
        time.sleep(1.2)
    drops = int(ev("document.getElementById('msb-drop').textContent") or 0)
    check('drops register', drops >= 6, f'drops={drops}')
    if args.shots:
        page.screenshot(path=f'{args.shots}/2_play_portrait.png')

    # End 키(빌드된 C#의 디버그 게임오버)는 막혀야 한다
    page.keyboard.press('End')
    time.sleep(1.5)
    check('End key blocked', not ev("document.getElementById('gameover-overlay').classList.contains('visible')"))

    # 2배속 보상형 — 광고 동안 Unity 정지
    page.click('#speed-boost-btn')
    time.sleep(0.8)
    check('frozen + muted during ad', ev('_apFrozen') is True and gain() == 0, f'gain={gain()}')
    time.sleep(2)
    check('speed boost via rewarded', ev('_speedBoostActive') is True, ev("document.getElementById('speed-boost-btn').textContent.trim()"))
    check('unpaused + unmuted after ad', ev('_apFrozen') is False and gain() == 1)

    # 게임오버 → 저장
    ev("unityInstance.SendMessage('GameManager', 'TriggerGameOver')")
    page.wait_for_selector('#gameover-overlay.visible', timeout=10000)
    score = int(ev("document.getElementById('go-score').textContent.replace(/,/g,'')"))
    time.sleep(1)
    check('level_failed', 'level_failed' in ev('__pgMessages'))
    stored = ev("localStorage.getItem('animalpop_best')")
    check('best saved', stored is not None and int(stored) >= score, f'score={score} stored={stored}')
    if args.shots:
        page.screenshot(path=f'{args.shots}/3_gameover_portrait.png')

    # 부활: 실패 → 토스트·보상 없음 / 중간에 닫음 → 보상 없음 / 끝까지 → 부활
    click = lambda sel: ev(f"document.querySelector('{sel}').click()")
    go_visible = lambda: ev("document.getElementById('gameover-overlay').classList.contains('visible')")
    ev("window.__adMode = 'fail'"); click('#go-btn-revive'); time.sleep(1.2)
    check('revive ad failed → toast, still game over', go_visible() and ev("!!document.getElementById('ad-unavail-toast')"))
    time.sleep(2.5)
    ev("window.__adMode = 'close'"); click('#go-btn-revive'); time.sleep(1.8)
    check('revive ad closed early → no reward', go_visible())
    ev("window.__adMode = 'reward'"); click('#go-btn-revive'); time.sleep(2)
    revived = not ev("document.getElementById('gameover-overlay').classList.contains('visible')")
    check('revive via rewarded', revived)
    check('gameplay_started after revive', 'gameplay_started' in ev('__pgMessages'))

    # 다시 게임오버 → 다시하기(전면 광고) → 새 판
    ev("unityInstance.SendMessage('GameManager', 'TriggerGameOver')")
    page.wait_for_selector('#gameover-overlay.visible', timeout=10000)
    n_started = ev('__pgMessages.filter(m => m === "level_started").length')
    click('#go-btn-restart')
    page.wait_for_selector('#game-hud.visible', timeout=40000)
    time.sleep(1)
    check('restart → level_started', ev('__pgMessages.filter(m => m === "level_started").length') == n_started + 1)

    # 설정 토글 → 저장
    page.click('#settings-fab')
    page.click('label.tog:has(#toggle-bgm)')
    time.sleep(1)
    check('bgm pref saved', ev("localStorage.getItem('animalpop_bgm')") == '0')
    page.click('label.tog:has(#toggle-bgm)')
    page.click('#settings-overlay .sht-ghost')

    # 호스트 일시정지 → Unity 메인 루프 정지
    ev("PlaygamaHost.onPause(_apFreeze)")  # 같은 리스너 재등록 (no-op)
    ev("_apFreeze(true)")
    check('freeze', ev('_apFrozen') is True)
    ev("_apFreeze(false)")

    # 레이아웃: 데스크톱 가로(좁은 iframe 포함) = 회전 안내 없음 + 캔버스 1:2 필러박스
    for w, h, name in ((1280, 720, 'desktop_landscape'), (896, 504, 'small_iframe')):
        page.set_viewport_size({'width': w, 'height': h})
        time.sleep(1.5)
        rot = ev("getComputedStyle(document.getElementById('rotate-overlay')).display")
        cw = ev("document.getElementById('unity-canvas').getBoundingClientRect().width")
        check(f'{name} {w}x{h}: no rotate prompt, canvas 1:2', rot == 'none' and abs(cw - h / 2) < 2, f'rotate={rot} canvas_w={cw}')
        if args.shots:
            page.screenshot(path=f'{args.shots}/4_{name}.png')
    page.set_viewport_size({'width': 390, 'height': 844})
    time.sleep(1)
    check('portrait canvas full width', ev("document.getElementById('unity-canvas').getBoundingClientRect().width") == 390)

    # 터치 폰 가로 → 세로 회전 안내 (포털에 portrait로 등록)
    phone = browser.new_context(viewport={'width': 844, 'height': 390}, is_mobile=True, has_touch=True, locale='en-US').new_page()
    phone.goto(URL)
    phone.wait_for_selector('#rotate-overlay', state='attached')
    check('touch landscape → rotate prompt', phone.evaluate("getComputedStyle(document.getElementById('rotate-overlay')).display") == 'flex')
    phone.close()

    external = sorted(h for h in hosts if h not in ('127.0.0.1', 'localhost', None))
    check('no external requests', external == [], external)
    real_errors = [e for e in errors if 'favicon' not in e]
    check('no page errors', real_errors == [], real_errors[:5])
    print('messages:', ev('JSON.stringify(__pgMessages)'))
    browser.close()

srv.shutdown()
print('\n' + ('ALL PASS' if not fails else f'{len(fails)} FAIL: {fails}'))
sys.exit(1 if fails else 0)
