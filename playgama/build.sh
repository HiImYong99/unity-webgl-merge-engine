#!/usr/bin/env bash
# Playgama(HTML5 포털) 빌드 — Unity 재빌드 없이 마지막 WebGL 빌드 + 현재 템플릿 + Playgama Bridge.
# 같은 zip이 CrazyGames에도 간다(브리지가 호스트를 감지해 CrazyGames SDK로 매핑).
#
#   ./build.sh playgama            (루트에서)  또는  cd playgama && npm run build
#   UNITY_OUT=<dir>                Unity WebGL 출력 폴더 (기본 ait-build/public = 토스 빌드 산출물)
#
# 출력: build/animal-pop-playgama/ + build/animal-pop-playgama.zip (업로드용)
#
# Unity 빌드는 Brotli(.br)인데 포털 서버가 Content-Encoding: br을 붙여준다는 보장이 없다 →
#   framework.js는 원본으로, data·wasm은 gzip(.gz)으로 다시 싸고 playgama_bridge.js의 fetch 심이
#   DecompressionStream으로 푼다(호스트가 이미 풀어서 주면 그대로 통과).
# index.html = Assets/WebGLTemplates/AnimalPop/index.html에 템플릿 변수 치환 +
#   브리지 주입 + 토스 번들(unity-bridge.ts) 제거 + Google Fonts → 로컬 폰트.
#   하나라도 안 먹으면 FAIL — 템플릿의 해당 줄을 바꾸면 아래 치환도 같이 고친다.
set -euo pipefail
export PATH="/opt/homebrew/bin:$PATH"

root="$(cd "$(dirname "$0")/.." && pwd)"
here="$root/playgama"
unity_out="${UNITY_OUT:-$root/ait-build/public}"
template="$root/Assets/WebGLTemplates/AnimalPop"
out="$root/build/animal-pop-playgama"
zip_file="$root/build/animal-pop-playgama.zip"

command -v brotli >/dev/null || { echo 'FAIL: brotli CLI 필요 (brew install brotli)'; exit 1; }
[ -f "$unity_out/index.html" ] || { echo "FAIL: Unity 빌드 없음: $unity_out/index.html"; exit 1; }
[ -d "$here/node_modules/@playgama/bridge" ] || (cd "$here" && npm install --no-audit --no-fund)

# Unity가 생성한 index.html의 config에서 현재 파일명을 읽는다 (Build/에 옛 해시 파일이 남아 있을 수 있다)
pick() { sed -nE "s|.*$1[^\"]*\"Build/([^\"]+)\".*|\\1|p" "$unity_out/index.html" | head -1; }
loader=$(sed -nE 's|.*<script src="Build/([^"]+\.loader\.js)".*|\1|p' "$unity_out/index.html" | head -1)
data=$(pick dataUrl); framework=$(pick frameworkUrl); code=$(pick codeUrl)
for f in "$loader" "$data" "$framework" "$code"; do
  [ -n "$f" ] && [ -f "$unity_out/Build/$f" ] || { echo "FAIL: Unity 빌드 파일 없음: '$f'"; exit 1; }
done
case "$data$framework$code" in *.br*.br*.br) ;; *) echo "FAIL: Brotli(.br) 빌드가 아님: $data $framework $code"; exit 1 ;; esac

rm -rf "$out" "$zip_file"
mkdir -p "$out/Build" "$out/TemplateData/sprites" "$out/fonts"

cp "$unity_out/Build/$loader" "$out/Build/"
brotli -dc "$unity_out/Build/$framework" > "$out/Build/${framework%.br}"
gz_data="${data%.br}.gz"; gz_code="${code%.br}.gz"
brotli -dc "$unity_out/Build/$data" | gzip -9n > "$out/Build/$gz_data"
brotli -dc "$unity_out/Build/$code" | gzip -9n > "$out/Build/$gz_code"
raw_data=$(brotli -dc "$unity_out/Build/$data" | wc -c | tr -d ' ')
raw_code=$(brotli -dc "$unity_out/Build/$code" | wc -c | tr -d ' ')

cp "$template"/TemplateData/sprites/*.webp "$out/TemplateData/sprites/"
cp "$here"/fonts/*.woff2 "$here/fonts/nunito.css" "$here/fonts/OFL.txt" "$out/fonts/"
cp "$here/playgama-bridge-config.json" "$out/"
(cd "$here" && npx esbuild src/host.js --bundle --format=iife --minify --target=es2020 \
  --log-level=warning --outfile="$out/playgama_bridge.js")

python3 - "$template/index.html" "$out/index.html" "$loader" "$gz_data" "${framework%.br}" "$gz_code" \
  "$raw_data" "$raw_code" <<'PY'
import json, re, sys
src, dst, loader, data, framework, code, raw_data, raw_code = sys.argv[1:]
s = open(src, encoding='utf-8').read()

def sub(old, new, count=1):
    global s
    n = s.count(old)
    if n != count:
        sys.exit(f'FAIL: "{old[:70]}" {n}회 (기대 {count}회) — 템플릿이 바뀌었으면 build.sh 치환을 고친다')
    s = s.replace(old, new)

for var, val in {
    'LOADER_FILENAME': loader, 'DATA_FILENAME': data, 'FRAMEWORK_FILENAME': framework,
    'CODE_FILENAME': code, 'COMPANY_NAME': 'DefaultCompany', 'PRODUCT_NAME': 'animal-pop',
    'PRODUCT_VERSION': '1.0',
}.items():
    sub('{{{ %s }}}' % var, val)

# 토스 번들 진입점 (포털에선 404)
sub('    <!-- Apps in Toss Bridge (Vite bundling entry) — Android에서는 404되지만 module script는 무시됨 -->\n'
    '    <script type="module" src="./unity-bridge.ts"></script>\n', '')

# Google Fonts → 로컬 (외부 요청 금지 포털: YouTube 등)
s, n = re.subn(r'[ \t]*(<!--[^\n]*Nunito[^\n]*-->\n[ \t]*)?'
               r'(<noscript>)?<link[^>]*fonts\.(googleapis|gstatic)\.com[^>]*>(</noscript>)?\n', '', s)
if n != 4:
    sys.exit(f'FAIL: Google Fonts 링크 {n}개 제거 (기대 4개)')
sub('    <title>Animal Pop</title>\n',
    '    <title>Animal Pop</title>\n    <link rel="stylesheet" href="fonts/nunito.css">\n')

# 브리지는 플랫폼 감지 스크립트보다 먼저 — window.PlaygamaHost로 'playgama'가 잡힌다
gz = json.dumps({f'Build/{data}': int(raw_data), f'Build/{code}': int(raw_code)})
sub('    <!-- 플랫폼 감지 (AndroidBridge',
    f'    <script>window.AP_PG_GZ = {gz};</script>\n'
    '    <script src="playgama_bridge.js"></script>\n'
    '    <!-- 플랫폼 감지 (AndroidBridge')

for bad in ('{{{', 'unity-bridge.ts', 'fonts.googleapis', 'fonts.gstatic'):
    if bad in s:
        sys.exit(f'FAIL: index.html에 "{bad}" 남음')
open(dst, 'w', encoding='utf-8').write(s)
PY

(cd "$out" && zip -qr -X "$zip_file" . -x '.*')

echo "animal-pop-playgama: $(du -sh "$out" | cut -f1)  zip: $(du -h "$zip_file" | cut -f1)  ($zip_file)"
