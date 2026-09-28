#!/bin/bash
# Animal Pop — 앱인토스(.ait) 빌드
#
#   ./build-ait.sh              Unity 배치 빌드 + SDK 3.x 패키징 → ait-build/animal-pop.ait (운영)
#   ./build-ait.sh --test       위와 같고, 같은 dist/web으로 테스트 번들 ait-build/animal-pop-test.ait도 만든다
#   ./build-ait.sh --no-unity   Unity 빌드 없이 지금 dist/web을 다시 포장 (--test와 같이 쓸 수 있음)
#
# 빌드 경로: AITPackageOnlyBuild.BuildWithProjectTemplate (72e39fa)
#   1) WebGL 빌드 — AnimalPop 템플릿 유지, webgl/로 출력
#   2) 패키징만 SDK 3.x에 위임 — webgl/ → ait-build/public → vite build(dist/web) → ait build(.ait)
# 예전 경로(AITBuildScript.BuildWebGL + pnpm build)는 3.x ait build가 dist/web을 새로 만들지 않아
# 옛 dist/web을 그대로 다시 포장했다(템플릿 수정이 번들에 빠짐). 쓰지 않는다.
#
# 테스트 번들: 운영 광고 그룹 ID로 QR 테스트를 하면 제재 대상이다. 테스트 번들은 운영 번들과 같은 dist/web에
# scripts/ait-test-ads.js(운영 ID → ait-ad-test-* 치환)를 주입하고 미션을 TEST_ 프로모션 코드로 바꾼다.
# 운영 번들에는 테스트 ID·TEST_ 호출이 들어가지 않는다(아래 검증).
set -e

PROJECT_DIR="$(cd "$(dirname "$0")" && pwd)"
UNITY="/Applications/Unity/Hub/Editor/2022.3.62f3/Unity.app/Contents/MacOS/Unity"
AIT_DIR="$PROJECT_DIR/ait-build"
TEMPLATE="$PROJECT_DIR/Assets/WebGLTemplates/AnimalPop/index.html"
LOG_FILE="${UNITY_LOG:-$PROJECT_DIR/unity-build.log}"
WEB_INDEX="$AIT_DIR/dist/web/index.html"

MAKE_TEST=0
SKIP_UNITY=0
for arg in "$@"; do
    case "$arg" in
        --test) MAKE_TEST=1 ;;
        --no-unity) SKIP_UNITY=1 ;;
        *) echo "알 수 없는 옵션: $arg"; exit 1 ;;
    esac
done

fail() { echo "[ERROR] $1"; exit 1; }
# ait CLI를 직접 부른다 — pnpm exec는 의존성 상태 검사로 SDK가 설치한 node_modules를 지우려다 TTY 없이 멈춘다
ait_build() { ( cd "$AIT_DIR" && ./node_modules/.bin/ait build ); }

echo "========================================"
echo "  Animal Pop - AIT 빌드"
echo "========================================"

# ── 1. Unity WebGL 빌드 + SDK 패키징 ──────────
if [ "$SKIP_UNITY" != "1" ]; then
    pgrep -fi "Unity\.app/Contents/MacOS/Unity .*-projectpath $PROJECT_DIR( |$)" >/dev/null \
        && fail "이 프로젝트를 연 Unity가 실행 중이에요. 닫고 다시 실행하세요."
    [ -e "$PROJECT_DIR/Temp/UnityLockfile" ] && fail "Temp/UnityLockfile이 있어요. 다른 Unity 세션을 확인하세요."

    echo ""
    echo "[1/3] Unity 배치 빌드 + SDK 패키징 (AITPackageOnlyBuild)..."
    echo "      로그: $LOG_FILE"
    START_TS=$(date +%s)
    "$UNITY" \
      -batchmode \
      -quit \
      -projectPath "$PROJECT_DIR" \
      -executeMethod AITPackageOnlyBuild.BuildWithProjectTemplate \
      -logFile "$LOG_FILE" || fail "Unity 빌드 실패. 로그: $LOG_FILE"

    # 패키징이 dist/web을 새로 만들었는지 (옛 dist/web 재포장 방지)
    [ -f "$WEB_INDEX" ] || fail "dist/web/index.html이 없어요"
    [ "$(stat -f %m "$WEB_INDEX")" -ge "$START_TS" ] || fail "dist/web/index.html이 이번 빌드에서 갱신되지 않았어요"
fi
# 다시 포장할 때(테스트 번들, --no-unity) 마지막 Unity 빌드와 같은 메타데이터를 쓴다
if [ -f "$LOG_FILE" ]; then
    UNITY_METADATA="$(sed -n 's/^\[AIT\] UNITY_METADATA: //p' "$LOG_FILE" | tail -1)"
    [ -n "$UNITY_METADATA" ] && export UNITY_METADATA
fi
if [ "$SKIP_UNITY" = "1" ]; then
    echo ""
    echo "[1/3] Unity 빌드 건너뜀 (--no-unity) — 지금 dist/web을 포장해요"
    [ -f "$WEB_INDEX" ] || fail "dist/web/index.html이 없어요. Unity 빌드부터 하세요."
    ait_build || fail "ait build 실패"
fi

# ── 2. 운영 번들 검증 ─────────────────────────
echo ""
echo "[2/3] 운영 번들 검증..."
[ -f "$AIT_DIR/animal-pop.ait" ] || fail "ait-build/animal-pop.ait가 없어요"
python3 - "$AIT_DIR/animal-pop.ait" "$TEMPLATE" <<'PY' || fail "운영 번들 검증 실패"
import re, sys, zipfile
ait, template = sys.argv[1], sys.argv[2]
z = zipfile.ZipFile(ait)
html = z.read('sources/index.html').decode('utf-8-sig')
js = [n for n in z.namelist() if n.startswith('sources/assets/') and n.endswith('.js')]
bridge = ''.join(z.read(n).decode('utf-8', 'ignore') for n in js)
tpl = open(template, encoding='utf-8-sig').read()
checks = {
    '테스트 광고 ID 없음': 'ait-ad-test' not in html,
    '운영 광고 ID 있음': 'ait.v2.live.' in html,
    '미션 LIVE 코드(test: false)': re.search(r"\btest: false,", html) is not None,
    'contactsViral 없음': 'contactsViral' not in html,
    '템플릿 수정 반영(restart_ad)': 'restart_ad' in html and 'restart_ad' in tpl,
    'SDK 3.x JS(getStorageItems)': 'getStorageItems' in bridge,
}
data = [n for n in z.namelist() if n.endswith('.data.br') or n.endswith('.data')]
print('  data:', ', '.join(n.split('/')[-1] for n in data))
ok = True
for name, passed in checks.items():
    print(('  OK   ' if passed else '  FAIL ') + name)
    ok = ok and passed
sys.exit(0 if ok else 1)
PY

# ── 3. 테스트 번들 (--test) ───────────────────
if [ "$MAKE_TEST" = "1" ]; then
    echo ""
    echo "[3/3] 테스트 번들 (테스트 광고 ID + TEST_ 프로모션 코드)..."
    BACKUP_DIR="$(mktemp -d)"
    cp "$WEB_INDEX" "$BACKUP_DIR/index.html"
    cp "$AIT_DIR/animal-pop.ait" "$BACKUP_DIR/animal-pop.ait"
    restore() {
        cp "$BACKUP_DIR/index.html" "$WEB_INDEX"
        cp "$BACKUP_DIR/animal-pop.ait" "$AIT_DIR/animal-pop.ait"
        rm -rf "$BACKUP_DIR"
    }
    trap restore EXIT

    python3 - "$WEB_INDEX" "$PROJECT_DIR/scripts/ait-test-ads.js" <<'PY' || fail "테스트 주입 실패"
import re, sys
index, shim_path = sys.argv[1], sys.argv[2]
html = open(index, encoding='utf-8-sig').read()
shim = open(shim_path, encoding='utf-8').read()
# 브리지 module script(window.AppsInToss 설정) 바로 뒤 — module script는 문서 순서대로 실행된다
html, n = re.subn(r'(<script type="module"[^>]*src="[^"]*assets/[^"]*\.js"></script>)',
                  lambda m: m.group(1) + '\n<script type="module">\n' + shim + '\n</script>', html, count=1)
assert n == 1, 'bridge module script not found'
html, n = re.subn(r"\btest: false,", "test: true, ", html, count=1)
assert n == 1, 'MISSION.test not found'
open(index, 'w', encoding='utf-8').write(html)
PY
    ait_build || fail "테스트 ait build 실패"
    mv "$AIT_DIR/animal-pop.ait" "$AIT_DIR/animal-pop-test.ait"
    python3 - "$AIT_DIR/animal-pop-test.ait" <<'PY' || fail "테스트 번들 검증 실패"
import sys, zipfile
html = zipfile.ZipFile(sys.argv[1]).read('sources/index.html').decode('utf-8-sig')
for name, passed in {
    '테스트 광고 ID 치환기 있음': 'ait-ad-test-rewarded-id' in html and 'ait-ad-test-interstitial-id' in html and 'ait-ad-test-banner-id' in html,
    '미션 TEST_ 코드(test: true)': 'test: true, ' in html,
}.items():
    print(('  OK   ' if passed else '  FAIL ') + name)
    if not passed: sys.exit(1)
PY
    echo "      → $AIT_DIR/animal-pop-test.ait (QR 테스트 전용, 심사 제출 금지)"
else
    echo ""
    echo "[3/3] 테스트 번들 건너뜀 (필요하면 --test)"
fi

echo ""
echo "========================================"
echo "  빌드 완료: ait-build/animal-pop.ait"
echo "========================================"
echo "배포하려면: cd ait-build && ./node_modules/.bin/ait deploy (또는 콘솔 MCP bundle_upload) — 테스트 번들은 올리지 않아요"
