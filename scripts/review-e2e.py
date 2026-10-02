"""별점 요청 게이트(APReview)와 설정 '앱 평가하기' E2E — 토스 번들(.ait)을 헤드리스 Chrome으로 그대로 돌린다.

    python3 scripts/review-e2e.py [ait-build/animal-pop.ait] [--template]

--template: 번들의 index.html 대신 지금의 Assets/WebGLTemplates/AnimalPop/index.html을 번들 빌드 파일 이름으로
치환해 올린다 — 템플릿만 바꿨을 때 Unity 재빌드 없이 확인용. 가짜 SDK는 scripts/toss-e2e.py의 STUB을 그대로 쓰고
requestReview만 기록하는 가짜로 끼운다. 창을 띄우지 않는다(포커스 안 뺏음).

확인: 하한(2판) 전 미요청 · 저점 미요청 · 하한 후 신기록에 1회 · 결과 카드가 뜬 뒤에만 · 60일 간격 · 더 큰 동물 고점 ·
최대 3회 · 미지원(isSupported false)이면 행 숨김·미요청·미기록 · requestReview가 던져도 조용히 · 설정 행은 세지 않는다.
"""
import argparse, functools, http.server, os, re, shutil, sys, tempfile, threading, time, zipfile
from playwright.sync_api import sync_playwright

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.dirname(HERE)
ap = argparse.ArgumentParser()
ap.add_argument('ait', nargs='?', default=os.path.join(REPO, 'ait-build/animal-pop.ait'))
ap.add_argument('--template', action='store_true')
args = ap.parse_args()

ROOT = tempfile.mkdtemp(prefix='ait-review-')
with zipfile.ZipFile(args.ait) as z:
    for n in z.namelist():
        if n.startswith('sources/') and not n.endswith('/'):
            dst = os.path.join(ROOT, n[len('sources/'):])
            os.makedirs(os.path.dirname(dst), exist_ok=True)
            with open(dst, 'wb') as f:
                f.write(z.read(n))

if args.template:
    # Unity·ait가 번들에 하는 일을 그대로: 빌드 파일 이름 치환, Vite 진입점 교체
    bundled = open(os.path.join(ROOT, 'index.html'), encoding='utf-8-sig').read()
    s = open(os.path.join(REPO, 'Assets/WebGLTemplates/AnimalPop/index.html'), encoding='utf-8').read()

    def found(pattern):
        m = re.search(pattern, bundled)
        if not m:
            sys.exit(f'FAIL: 번들 index.html에서 {pattern} 를 못 찾음')
        return m.group(1)

    values = {
        'LOADER_FILENAME': found(r'<script src="Build/([^"]+\.loader\.js)"'),
        'DATA_FILENAME': found(r'dataUrl:\s*"Build/([^"]+)"'),
        'FRAMEWORK_FILENAME': found(r'frameworkUrl:\s*"Build/([^"]+)"'),
        'CODE_FILENAME': found(r'codeUrl:\s*"Build/([^"]+)"'),
        'COMPANY_NAME': found(r'companyName:\s*"([^"]*)"'),
        'PRODUCT_NAME': found(r'productName:\s*"([^"]*)"'),
        'PRODUCT_VERSION': found(r'productVersion:\s*"([^"]*)"'),
    }
    for k, v in values.items():
        if s.count('{{{ %s }}}' % k) != 1:
            sys.exit(f'FAIL: 템플릿의 {{{{{{ {k} }}}}}} 가 1회가 아님')
        s = s.replace('{{{ %s }}}' % k, v)
    s = re.sub(r'[^\n]*<script type="module" src="\./unity-bridge\.ts"></script>\n', '', s, count=1)
    s = s.replace('</head>', '  <script type="module" crossorigin src="./assets/index.js"></script>\n</head>', 1)
    with open(os.path.join(ROOT, 'index.html'), 'w', encoding='utf-8') as f:
        f.write(s)

src = open(os.path.join(HERE, 'toss-e2e.py'), encoding='utf-8').read()
STUB = re.search(r'STUB = r"""(.*?)"""', src, re.S).group(1)
STUB = STUB.replace('var known = {', 'var known = { requestReview: window.__rr,', 1)
PRE = r"""
window.__reviews = 0;
window.__rr = function () { window.__reviews++; if (window.__rrThrows) throw new Error('quota'); return Promise.resolve(); };
window.__rr.isSupported = function () { return window.__rrSupported !== false; };
"""


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

    def handle(self):
        try:
            super().handle()
        except (BrokenPipeError, ConnectionResetError):
            pass  # 브라우저가 닫히며 끊은 연결


srv = http.server.ThreadingHTTPServer(('127.0.0.1', 0), functools.partial(Handler, directory=ROOT))
threading.Thread(target=srv.serve_forever, daemon=True).start()
URL = f'http://127.0.0.1:{srv.server_port}/index.html'
fails = []
DAY = 'String(Date.now() - 61 * 864e5)'


def check(name, ok, detail=''):
    print(('PASS ' if ok else 'FAIL ') + name + (f' — {detail}' if detail else ''), flush=True)
    if not ok:
        fails.append(name)


with sync_playwright() as p:
    browser = p.chromium.launch(channel='chrome', headless=True, args=[
        '--use-angle=swiftshader', '--enable-unsafe-swiftshader', '--ignore-gpu-blocklist', '--mute-audio'])

    def open_page(extra=''):
        ctx = browser.new_context(viewport={'width': 390, 'height': 844}, locale='ko-KR')
        page = ctx.new_page()
        page.add_init_script(PRE + extra + STUB)
        errors = []
        page.on('pageerror', lambda e: errors.append(str(e)))
        page.goto(URL)
        page.wait_for_selector('#landing-overlay.visible', timeout=240000)
        return page, errors

    page, errors = open_page()
    ev = page.evaluate
    asks = lambda: ev("localStorage.getItem('animalpop_review_asks')")
    check('platform = toss', ev('window.AP_PLATFORM') == 'toss')
    check('requestReview가 지원되는 가짜 SDK', ev('window.GameBridge.canRequestReview()') is True)

    # 설정 행: 플레이 중 톱니에서 연다(랜딩이 덮고 있을 땐 못 누른다)
    ev('onHtmlStartClicked()')
    time.sleep(1.0)
    ev('openSettings()')
    time.sleep(0.8)
    shown = ev("getComputedStyle(document.getElementById('settings-rate')).display")
    check('설정 행이 토스에서 보인다', shown != 'none', shown)
    label = ev("document.getElementById('settings-rate').textContent.trim()")
    check('설정 행 라벨 (ko)', label == '앱 평가하기', label)
    page.click('#settings-rate')
    check('설정 행 탭 → requestReview', ev('window.__reviews') == 1)
    check('설정 행은 예산을 쓰지 않는다', asks() is None)
    ev('closeSettings()')

    def game_over(score, best, restart_first=True):
        if restart_first:
            ev('restartGame()')
        ev(f'window.showGameOverFromUnity({score}, {best}, false, 0)')

    base = ev('window.__reviews')
    game_over(120, 120, restart_first=False)
    time.sleep(2.0)
    check('1판(신기록, 하한 전) 미요청', ev('window.__reviews') == base,
          'games=' + str(ev("localStorage.getItem('animalpop_total_games')")))
    game_over(60, 120)
    time.sleep(2.0)
    check('2판(신기록·새 동물·챌린지 없음 = 저점) 미요청', ev('window.__reviews') == base)
    game_over(150, 150)
    time.sleep(0.5)
    check('결과 카드가 뜨기 전엔 묻지 않는다', ev('window.__reviews') == base)
    time.sleep(1.6)
    check('3판(신기록, 하한 통과) 1회 요청', ev('window.__reviews') == base + 1)
    check('요청이 기록된다', asks() == '1' and int(ev("localStorage.getItem('animalpop_review_last')") or 0) > 0)
    game_over(200, 200)
    time.sleep(2.0)
    check('60일 안의 신기록은 미요청', ev('window.__reviews') == base + 1)

    ev(f"localStorage.setItem('animalpop_review_last', {DAY})")
    game_over(250, 250)
    ev('restartGame()')
    time.sleep(2.0)
    check('결과 카드에서 바로 다시하기 → 미요청', ev('window.__reviews') == base + 1)
    check('...기록도 없음', asks() == '1')

    ev("localStorage.setItem('animalpop_top_level', '6')")
    ev('window.onMergeFromUnity(7)')
    ev('window.showGameOverFromUnity(10, 250, false, 0)')
    time.sleep(2.0)
    check('이전 최고(6)보다 큰 동물(7) = 신기록 없이도 고점', ev('window.__reviews') == base + 2)
    check('최고 동물 기록 갱신', ev("localStorage.getItem('animalpop_top_level')") == '7')

    ev(f"localStorage.setItem('animalpop_review_last', {DAY})")
    game_over(300, 300)
    time.sleep(2.0)
    check('3번째 요청', ev('window.__reviews') == base + 3)
    ev(f"localStorage.setItem('animalpop_review_last', {DAY})")
    game_over(400, 400)
    time.sleep(2.0)
    check('4번째는 없다 (설치당 3회)', ev('window.__reviews') == base + 3, asks())
    check('페이지 에러 없음', not errors, '; '.join(errors[:3]))

    page2, errors2 = open_page('window.__rrSupported = false;')
    ev2 = page2.evaluate
    ev2('onHtmlStartClicked()')
    ev2('openSettings()')
    check('미지원 토스앱: 설정 행 숨김',
          ev2("getComputedStyle(document.getElementById('settings-rate')).display") == 'none')
    ev2('closeSettings()')
    ev2("localStorage.setItem('animalpop_total_games', '5')")
    ev2('window.showGameOverFromUnity(500, 500, false, 0)')
    time.sleep(2.0)
    check('미지원: 미요청', ev2('window.__reviews') == 0)
    check('미지원: 예산 그대로', ev2("localStorage.getItem('animalpop_review_asks')") is None)
    check('미지원: 페이지 에러 없음', not errors2, '; '.join(errors2[:3]))

    page3, errors3 = open_page('window.__rrThrows = true;')
    ev3 = page3.evaluate
    ev3("localStorage.setItem('animalpop_total_games', '5')")
    ev3('onHtmlStartClicked()')
    ev3('window.showGameOverFromUnity(500, 500, false, 0)')
    time.sleep(2.0)
    check('던지는 시트: 1회 불림', ev3('window.__reviews') == 1)
    check('던지는 시트: 그래도 기록 (고점마다 재시도 방지)', ev3("localStorage.getItem('animalpop_review_asks')") == '1')
    check('던지는 시트: 페이지 에러 없음', not errors3, '; '.join(errors3[:3]))
    browser.close()

srv.shutdown()
shutil.rmtree(ROOT, ignore_errors=True)
print('ALL PASS' if not fails else f'{len(fails)} FAIL: {fails}')
sys.exit(1 if fails else 0)
