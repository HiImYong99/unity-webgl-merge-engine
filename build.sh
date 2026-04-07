#!/bin/bash
# ═══════════════════════════════════════════════════════════════
#  Animal Pop – 통합 빌드 스크립트
#  Usage:
#    ./build.sh toss        # 앱인토스 빌드 + 배포
#    ./build.sh android     # Android APK 빌드
#    ./build.sh android-aab # Android AAB 빌드 (릴리즈)
#    ./build.sh all         # 전체 빌드
#
#  Unity WebGL 빌드는 별도로 먼저 해야 합니다.
#  이 스크립트는 빌드 결과물을 각 플랫폼으로 복사/매핑합니다.
# ═══════════════════════════════════════════════════════════════

set -e

PROJECT_DIR="$(cd "$(dirname "$0")" && pwd)"
AIT_DIR="$PROJECT_DIR/ait-build"
ANDROID_DIR="$PROJECT_DIR/android-wrapper"
ANDROID_ASSETS="$ANDROID_DIR/app/src/main/assets"

# ── 색상 ──
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
CYAN='\033[0;36m'
NC='\033[0m'

info()  { echo -e "${CYAN}[INFO]${NC} $1"; }
ok()    { echo -e "${GREEN}[OK]${NC} $1"; }
warn()  { echo -e "${YELLOW}[WARN]${NC} $1"; }
fail()  { echo -e "${RED}[ERROR]${NC} $1"; exit 1; }

# ═══════════════════════════════════════════════════════════════
#  WebGL 빌드 결과물 탐지
# ═══════════════════════════════════════════════════════════════

# Unity WebGL 빌드 결과물 위치 자동 탐지
# 우선순위: ait-build/public/Build > WebGL Build 폴더
detect_webgl_build() {
    # 1) ait-build/public/Build (가장 최신)
    if [ -d "$AIT_DIR/public/Build" ]; then
        local count=$(ls "$AIT_DIR/public/Build/"*.loader.js 2>/dev/null | wc -l)
        if [ "$count" -gt 0 ]; then
            WEBGL_BUILD_DIR="$AIT_DIR/public"
            ok "WebGL 빌드 발견: $WEBGL_BUILD_DIR/Build/"
            return 0
        fi
    fi

    # 2) android-wrapper에 이미 있는 경우
    if [ -d "$ANDROID_ASSETS/Build" ]; then
        local count=$(ls "$ANDROID_ASSETS/Build/"*.loader.js 2>/dev/null | wc -l)
        if [ "$count" -gt 0 ]; then
            WEBGL_BUILD_DIR="$ANDROID_ASSETS"
            ok "WebGL 빌드 발견 (android-wrapper): $WEBGL_BUILD_DIR/Build/"
            return 0
        fi
    fi

    fail "WebGL 빌드를 찾을 수 없습니다. Unity에서 WebGL 빌드를 먼저 실행하세요."
}

# ═══════════════════════════════════════════════════════════════
#  Android: Build 파일 복사 + index.html 해시 파일명 자동 매핑
# ═══════════════════════════════════════════════════════════════

sync_android_assets() {
    info "Android assets 동기화 중..."

    # Build 디렉토리 복사 (ait-build → android-wrapper)
    if [ "$WEBGL_BUILD_DIR" != "$ANDROID_ASSETS" ]; then
        info "Build 파일 복사: $WEBGL_BUILD_DIR/Build/ → $ANDROID_ASSETS/Build/"
        rm -rf "$ANDROID_ASSETS/Build"
        cp -r "$WEBGL_BUILD_DIR/Build" "$ANDROID_ASSETS/Build/"

        # 스프라이트도 동기화
        if [ -d "$WEBGL_BUILD_DIR/sprites" ]; then
            rm -rf "$ANDROID_ASSETS/sprites"
            cp -r "$WEBGL_BUILD_DIR/sprites" "$ANDROID_ASSETS/sprites/"
        fi
        if [ -d "$WEBGL_BUILD_DIR/TemplateData" ]; then
            rm -rf "$ANDROID_ASSETS/TemplateData"
            cp -r "$WEBGL_BUILD_DIR/TemplateData" "$ANDROID_ASSETS/TemplateData/"
        fi
    fi

    # 해시 파일명 탐지
    local LOADER=$(ls "$ANDROID_ASSETS/Build/"*.loader.js 2>/dev/null | head -1 | xargs basename)
    local DATA=$(ls "$ANDROID_ASSETS/Build/"*.data.* 2>/dev/null | grep -v symbols | head -1 | xargs basename)
    local FRAMEWORK=$(ls "$ANDROID_ASSETS/Build/"*.framework.js* 2>/dev/null | head -1 | xargs basename)
    local WASM=$(ls "$ANDROID_ASSETS/Build/"*.wasm.* 2>/dev/null | head -1 | xargs basename)

    if [ -z "$LOADER" ] || [ -z "$DATA" ] || [ -z "$FRAMEWORK" ] || [ -z "$WASM" ]; then
        fail "Build 파일을 찾을 수 없습니다: loader=$LOADER data=$DATA framework=$FRAMEWORK wasm=$WASM"
    fi

    info "파일명 매핑:"
    echo "  loader:    $LOADER"
    echo "  data:      $DATA"
    echo "  framework: $FRAMEWORK"
    echo "  wasm:      $WASM"

    # index.html에서 파일명 자동 치환
    local INDEX="$ANDROID_ASSETS/index.html"
    if [ ! -f "$INDEX" ]; then
        fail "android index.html이 없습니다: $INDEX"
    fi

    # loader script src 치환
    sed -i '' -E "s|src=\"Build/[^\"]*\.loader\.js\"|src=\"Build/$LOADER\"|g" "$INDEX"

    # config URL 치환 (data, framework, wasm)
    sed -i '' -E "s|\"Build/[^\"]*\.data\.[^\"]*\"|\"Build/$DATA\"|g" "$INDEX"
    sed -i '' -E "s|\"Build/[^\"]*\.framework\.js[^\"]*\"|\"Build/$FRAMEWORK\"|g" "$INDEX"
    sed -i '' -E "s|\"Build/[^\"]*\.wasm\.[^\"]*\"|\"Build/$WASM\"|g" "$INDEX"

    ok "index.html 파일명 매핑 완료"
}

# ═══════════════════════════════════════════════════════════════
#  Java 환경 설정
# ═══════════════════════════════════════════════════════════════

# ═══════════════════════════════════════════════════════════════
#  Unity WebGL 빌드 (플랫폼별)
# ═══════════════════════════════════════════════════════════════

UNITY="/Applications/Unity/Hub/Editor/2022.3.62f3/Unity.app/Contents/MacOS/Unity"

unity_build_toss() {
    info "Unity WebGL 빌드 (토스/Brotli)..."
    local LOG="$PROJECT_DIR/unity-build.log"
    "$UNITY" -batchmode -quit -projectPath "$PROJECT_DIR" \
        -executeMethod AITBuildScript.BuildWebGL -logFile "$LOG"
    if [ $? -ne 0 ]; then
        fail "Unity 빌드 실패. 로그: $LOG"
    fi
    ok "Unity WebGL 빌드 완료 (Brotli)"
}

unity_build_android() {
    info "Unity WebGL 빌드 (Android/비압축)..."
    local LOG="$PROJECT_DIR/unity-build-android.log"
    "$UNITY" -batchmode -quit -projectPath "$PROJECT_DIR" \
        -executeMethod AITBuildScript.BuildWebGLForAndroid -logFile "$LOG"
    if [ $? -ne 0 ]; then
        fail "Unity 빌드 실패. 로그: $LOG"
    fi
    ok "Unity WebGL 빌드 완료 (비압축)"

    # Unity가 토스용 index.html을 생성하므로 Android 패치 적용
    info "Android index.html 패치 적용..."
    bash "$ANDROID_DIR/patch-index.sh" "$ANDROID_ASSETS/index.html"
}

setup_java() {
    local UNITY_JDK="/Applications/Unity/Hub/Editor/2022.3.62f3/PlaybackEngines/AndroidPlayer/OpenJDK"
    local HOMEBREW_JDK="/opt/homebrew/Cellar/openjdk@17/17.0.18"
    if [ -d "$UNITY_JDK" ]; then
        export JAVA_HOME="$UNITY_JDK"
    elif [ -d "$HOMEBREW_JDK" ]; then
        export JAVA_HOME="$HOMEBREW_JDK"
    fi
    export PATH="$JAVA_HOME/bin:$PATH"
}

# ═══════════════════════════════════════════════════════════════
#  빌드 명령어
# ═══════════════════════════════════════════════════════════════

build_toss() {
    echo ""
    echo -e "${CYAN}══════════════════════════════════════${NC}"
    echo -e "${CYAN}  앱인토스 (Toss) 빌드${NC}"
    echo -e "${CYAN}══════════════════════════════════════${NC}"

    if [ "${SKIP_UNITY:-}" != "1" ]; then
        unity_build_toss
    fi

    cd "$AIT_DIR"
    if ! command -v npx &>/dev/null; then
        fail "npx를 찾을 수 없습니다. Node.js를 설치하세요."
    fi

    info "ait build 실행 중..."
    npx ait build

    ok "앱인토스 빌드 완료!"
    echo ""
    echo -e "  배포: ${YELLOW}cd ait-build && npx ait deploy${NC}"
}

build_android_apk() {
    echo ""
    echo -e "${CYAN}══════════════════════════════════════${NC}"
    echo -e "${CYAN}  Android APK 빌드 (Debug)${NC}"
    echo -e "${CYAN}══════════════════════════════════════${NC}"

    # SKIP_UNITY=1 → Unity 빌드 건너뛰고 기존 android-wrapper 빌드 재사용
    if [ "${SKIP_UNITY:-}" != "1" ]; then
        unity_build_android
    fi

    # Android 패치가 적용됐는지 확인, 안 됐으면 적용
    if ! grep -q 'AndroidBridge' "$ANDROID_ASSETS/index.html" 2>/dev/null; then
        info "Android 패치 미적용 — 패치 적용 중..."
        bash "$ANDROID_DIR/patch-index.sh" "$ANDROID_ASSETS/index.html"
    fi

    setup_java

    info "Gradle assembleDebug 실행 중..."
    cd "$ANDROID_DIR"
    ./gradlew assembleDebug --quiet

    local APK_SRC="$ANDROID_DIR/app/build/outputs/apk/debug/app-debug.apk"
    local APK_DST="$PROJECT_DIR/AnimalPop.apk"
    cp "$APK_SRC" "$APK_DST"

    ok "APK 빌드 완료!"
    echo -e "  출력: ${GREEN}$APK_DST${NC} ($(du -sh "$APK_DST" | cut -f1))"
    echo -e "  설치: ${YELLOW}adb install AnimalPop.apk${NC}"
}

build_android_aab() {
    echo ""
    echo -e "${CYAN}══════════════════════════════════════${NC}"
    echo -e "${CYAN}  Android AAB 빌드 (Release)${NC}"
    echo -e "${CYAN}══════════════════════════════════════${NC}"

    if [ "${SKIP_UNITY:-}" != "1" ]; then
        unity_build_android
    fi

    if ! grep -q 'AndroidBridge' "$ANDROID_ASSETS/index.html" 2>/dev/null; then
        info "Android 패치 미적용 — 패치 적용 중..."
        bash "$ANDROID_DIR/patch-index.sh" "$ANDROID_ASSETS/index.html"
    fi

    setup_java

    # 서명 설정 확인
    if [ ! -f "$ANDROID_DIR/signing.properties" ]; then
        fail "signing.properties 없음! cp signing.properties.template signing.properties 후 설정하세요."
    fi

    info "Gradle bundleRelease 실행 중..."
    cd "$ANDROID_DIR"
    ./gradlew bundleRelease --quiet

    local AAB_SRC="$ANDROID_DIR/app/build/outputs/bundle/release/app-release.aab"
    local AAB_DST="$PROJECT_DIR/AnimalPop-release.aab"
    cp "$AAB_SRC" "$AAB_DST"

    ok "AAB 빌드 완료!"
    echo -e "  출력: ${GREEN}$AAB_DST${NC} ($(du -sh "$AAB_DST" | cut -f1))"
    echo -e "  업로드: Google Play Console → 프로덕션 → 새 릴리즈"
}

# ═══════════════════════════════════════════════════════════════
#  메인 엔트리포인트
# ═══════════════════════════════════════════════════════════════

echo ""
echo "═══════════════════════════════════════"
echo "  Animal Pop 통합 빌드 시스템"
echo "═══════════════════════════════════════"

case "${1:-}" in
    toss)
        build_toss
        ;;
    android)
        build_android_apk
        ;;
    android-aab|aab)
        build_android_aab
        ;;
    all)
        build_toss
        build_android_apk
        build_android_aab
        ;;
    *)
        echo ""
        echo "Usage: ./build.sh <target>"
        echo ""
        echo "  toss          앱인토스 빌드 (Unity Brotli + ait build)"
        echo "  android       Android APK (Unity 비압축 + Gradle debug)"
        echo "  android-aab   Android AAB (Unity 비압축 + Gradle release)"
        echo "  all           전체 빌드"
        echo ""
        echo "옵션:"
        echo "  SKIP_UNITY=1 ./build.sh android   Unity 빌드 건너뛰기 (기존 빌드 재사용)"
        echo ""
        exit 1
        ;;
esac

echo ""
echo "═══════════════════════════════════════"
echo -e "  ${GREEN}✅ 완료!${NC}"
echo "═══════════════════════════════════════"
echo ""
