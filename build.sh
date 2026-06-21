#!/bin/bash
# ═══════════════════════════════════════════════════════════════
#  Animal Pop – 통합 빌드 스크립트
#  Usage:
#    ./build.sh toss        # 앱인토스 빌드 + 배포
#    ./build.sh android     # Android APK 빌드
#    ./build.sh android-aab # Android AAB 빌드 (릴리즈)
#    ./build.sh ios         # iOS WKWebView 래퍼 빌드
#    ./build.sh all         # 전체 빌드 (toss + ios + android)
#    ./build.sh bump 1.2.2 16  # 버전 올리기 (Android+iOS 동시)
#
#  Unity WebGL 빌드는 별도로 먼저 해야 합니다.
#  이 스크립트는 빌드 결과물을 각 플랫폼으로 복사/매핑합니다.
# ═══════════════════════════════════════════════════════════════

set -e

PROJECT_DIR="$(cd "$(dirname "$0")" && pwd)"
AIT_DIR="$PROJECT_DIR/ait-build"
ANDROID_DIR="$PROJECT_DIR/android-wrapper"
ANDROID_ASSETS="$ANDROID_DIR/app/src/main/assets"
IOS_DIR="$PROJECT_DIR/ios-wrapper"
IOS_WEB="$IOS_DIR/AnimalPop/web"
# 주의: 파일명이 'VERSION'이면 macOS(대소문자 무시 APFS)에서 C++ 표준헤더 <version>과 충돌 →
# WebGL il2cpp 빌드가 emcc '-I.'로 ./VERSION을 <version>으로 잘못 include해 빌드 실패. 'VERSION.txt'로 회피.
VERSION_FILE="$PROJECT_DIR/VERSION.txt"

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
#  버전 단일 소스 (VERSION 파일 → Android/iOS 설정 주입)
# ═══════════════════════════════════════════════════════════════

# VERSION 파일을 읽어 APP_VERSION / BUILD_NUMBER 설정
load_version() {
    [ -f "$VERSION_FILE" ] || fail "VERSION 파일 없음: $VERSION_FILE"
    APP_VERSION=$(grep -E "^APP_VERSION=" "$VERSION_FILE" | cut -d= -f2 | tr -d ' ')
    BUILD_NUMBER=$(grep -E "^BUILD_NUMBER=" "$VERSION_FILE" | cut -d= -f2 | tr -d ' ')
    [ -n "$APP_VERSION" ] && [ -n "$BUILD_NUMBER" ] || fail "VERSION 파싱 실패 (APP_VERSION/BUILD_NUMBER)"
}

# VERSION → Android build.gradle + iOS project.yml 에 주입 (멱등 — 동일하면 변화 없음)
sync_version() {
    load_version
    local GRADLE="$ANDROID_DIR/app/build.gradle"
    local IOS_YML="$IOS_DIR/project.yml"
    if [ -f "$GRADLE" ]; then
        sed -i '' -E "s/versionName \"[^\"]*\"/versionName \"$APP_VERSION\"/" "$GRADLE"
        sed -i '' -E "s/versionCode [0-9]+/versionCode $BUILD_NUMBER/" "$GRADLE"
    fi
    if [ -f "$IOS_YML" ]; then
        sed -i '' -E "s/MARKETING_VERSION: \"[^\"]*\"/MARKETING_VERSION: \"$APP_VERSION\"/" "$IOS_YML"
        sed -i '' -E "s/CURRENT_PROJECT_VERSION: \"[^\"]*\"/CURRENT_PROJECT_VERSION: \"$BUILD_NUMBER\"/" "$IOS_YML"
    fi
    ok "버전 동기화: v$APP_VERSION (build $BUILD_NUMBER) → Android + iOS"
}

# ./build.sh bump <버전> <빌드번호> — VERSION 갱신 후 전 플랫폼 주입
bump_version() {
    local NEW_VER="$1" NEW_BUILD="$2"
    [ -n "$NEW_VER" ] && [ -n "$NEW_BUILD" ] || fail "사용법: ./build.sh bump <버전> <빌드번호>  예) ./build.sh bump 1.2.2 16"
    cat > "$VERSION_FILE" <<EOF
# 앱 버전 단일 소스 (Android + iOS 공통)
# build.sh가 빌드 시 각 플랫폼 설정에 자동 주입:
#   Android → android-wrapper/app/build.gradle (versionName / versionCode)
#   iOS     → ios-wrapper/project.yml (MARKETING_VERSION / CURRENT_PROJECT_VERSION)
# 버전 올리기: ./build.sh bump <버전> <빌드번호>   예) ./build.sh bump 1.2.2 16
# (Toss는 ait 툴링이 별도 관리)
APP_VERSION=$NEW_VER
BUILD_NUMBER=$NEW_BUILD
EOF
    sync_version
    ok "버전 올림 완료: v$NEW_VER (build $NEW_BUILD)"
}

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

    # Unity 템플릿 변수 치환 ({{{ LOADER_FILENAME }}} 등)
    sed -i '' "s|{{{ LOADER_FILENAME }}}|$LOADER|g" "$INDEX"
    sed -i '' "s|{{{ DATA_FILENAME }}}|$DATA|g" "$INDEX"
    sed -i '' "s|{{{ FRAMEWORK_FILENAME }}}|$FRAMEWORK|g" "$INDEX"
    sed -i '' "s|{{{ CODE_FILENAME }}}|$WASM|g" "$INDEX"
    sed -i '' "s|{{{ COMPANY_NAME }}}|DefaultCompany|g" "$INDEX"
    sed -i '' "s|{{{ PRODUCT_NAME }}}|AnimalPop|g" "$INDEX"
    sed -i '' "s|{{{ PRODUCT_VERSION }}}|1.0|g" "$INDEX"

    # 이미 치환된 해시 파일명 업데이트 (재빌드 시)
    sed -i '' -E "s|src=\"Build/[^\"]*\.loader\.js\"|src=\"Build/$LOADER\"|g" "$INDEX"
    sed -i '' -E "s|\"Build/[^\"]*\.data[^\"]*\"|\"Build/$DATA\"|g" "$INDEX"
    sed -i '' -E "s|\"Build/[^\"]*\.framework\.js[^\"]*\"|\"Build/$FRAMEWORK\"|g" "$INDEX"
    sed -i '' -E "s|\"Build/[^\"]*\.wasm[^\"]*\"|\"Build/$WASM\"|g" "$INDEX"

    ok "index.html 파일명 매핑 완료"
}

# ═══════════════════════════════════════════════════════════════
#  Android: index.html 템플릿 변수만 치환 (Build 파일은 건드리지 않음)
# ═══════════════════════════════════════════════════════════════

sync_android_index() {
    info "Android index.html 템플릿 변수 치환 중..."

    local INDEX="$ANDROID_ASSETS/index.html"
    if [ ! -f "$INDEX" ]; then
        fail "android index.html이 없습니다: $INDEX"
    fi

    local LOADER=$(ls "$ANDROID_ASSETS/Build/"*.loader.js 2>/dev/null | head -1 | xargs basename)
    local DATA=$(ls "$ANDROID_ASSETS/Build/"*.data 2>/dev/null | head -1 | xargs basename)
    local FRAMEWORK=$(ls "$ANDROID_ASSETS/Build/"*.framework.js 2>/dev/null | head -1 | xargs basename)
    local WASM=$(ls "$ANDROID_ASSETS/Build/"*.wasm 2>/dev/null | head -1 | xargs basename)

    if [ -z "$LOADER" ] || [ -z "$DATA" ] || [ -z "$FRAMEWORK" ] || [ -z "$WASM" ]; then
        fail "Build 파일을 찾을 수 없습니다: loader=$LOADER data=$DATA framework=$FRAMEWORK wasm=$WASM"
    fi

    info "파일명 매핑: loader=$LOADER data=$DATA framework=$FRAMEWORK wasm=$WASM"

    # Unity 템플릿 변수 치환
    sed -i '' "s|{{{ LOADER_FILENAME }}}|$LOADER|g" "$INDEX"
    sed -i '' "s|{{{ DATA_FILENAME }}}|$DATA|g" "$INDEX"
    sed -i '' "s|{{{ FRAMEWORK_FILENAME }}}|$FRAMEWORK|g" "$INDEX"
    sed -i '' "s|{{{ CODE_FILENAME }}}|$WASM|g" "$INDEX"
    sed -i '' "s|{{{ COMPANY_NAME }}}|DefaultCompany|g" "$INDEX"
    sed -i '' "s|{{{ PRODUCT_NAME }}}|AnimalPop|g" "$INDEX"
    sed -i '' "s|{{{ PRODUCT_VERSION }}}|1.0|g" "$INDEX"

    # 이미 치환된 해시 파일명 업데이트 (재빌드 시)
    sed -i '' -E "s|src=\"Build/[^\"]*\.loader\.js\"|src=\"Build/$LOADER\"|g" "$INDEX"
    sed -i '' -E "s|\"Build/[^\"]*\.data[^\"]*\"|\"Build/$DATA\"|g" "$INDEX"
    sed -i '' -E "s|\"Build/[^\"]*\.framework\.js[^\"]*\"|\"Build/$FRAMEWORK\"|g" "$INDEX"
    sed -i '' -E "s|\"Build/[^\"]*\.wasm[^\"]*\"|\"Build/$WASM\"|g" "$INDEX"

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

    # GameBridge 통합 브릿지로 런타임 분기 — 최소 패치만 적용
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

    # index.html 템플릿 변수 치환 (Build 파일은 Unity 빌드가 직접 생성)
    sync_android_index

    # localStorage 키 안전장치 패치 (GameBridge가 런타임 분기 처리)
    bash "$ANDROID_DIR/patch-index.sh" "$ANDROID_ASSETS/index.html"

    sync_version   # VERSION → build.gradle
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

build_ios() {
    echo ""
    echo -e "${CYAN}══════════════════════════════════════${NC}"
    echo -e "${CYAN}  iOS WKWebView 래퍼 빌드${NC}"
    echo -e "${CYAN}══════════════════════════════════════${NC}"

    # iOS는 토스용 Brotli 빌드 그대로 재사용 (WebKit이 Brotli 디코딩 지원)
    if [ "${SKIP_UNITY:-}" != "1" ]; then
        unity_build_toss
    fi

    detect_webgl_build

    info "WebGL 빌드 → ios-wrapper/web/ 동기화: $WEBGL_BUILD_DIR → $IOS_WEB"
    rm -rf "$IOS_WEB"
    mkdir -p "$IOS_WEB"
    cp -R "$WEBGL_BUILD_DIR/Build" "$IOS_WEB/Build"
    cp "$WEBGL_BUILD_DIR/index.html" "$IOS_WEB/index.html"
    [ -d "$WEBGL_BUILD_DIR/sprites" ]      && cp -R "$WEBGL_BUILD_DIR/sprites" "$IOS_WEB/sprites"
    [ -d "$WEBGL_BUILD_DIR/TemplateData" ] && cp -R "$WEBGL_BUILD_DIR/TemplateData" "$IOS_WEB/TemplateData"

    # WKWebView는 plain HTTP(루프백 서버)에서 Content-Encoding: br를 디코딩하지 않으므로
    # (HTTPS 전용) 토스용 Brotli(.br) 빌드를 그대로 쓰면 Unity 로더가 raw brotli를 받아
    # 파싱 실패 → 검정화면. .br 파일을 미리 해제(identity)하고 파일명(.br)은 유지한다.
    # index.html이 .br URL을 참조하므로 이름은 그대로 두고 내용만 해제하면 된다.
    if command -v brotli &>/dev/null; then
        info ".br 파일 해제(identity) — WKWebView Brotli 미지원(HTTP) 대응"
        # process substitution으로 메인 셸에서 실행 → set -e/실패 전파 보장
        while IFS= read -r f; do
            brotli -d -c "$f" > "$f.dec" || { rm -f "$f.dec"; fail "brotli 해제 실패: $f"; }
            mv "$f.dec" "$f"
        done < <(find "$IOS_WEB/Build" -name "*.br")
    else
        fail "brotli 없음. 설치: brew install brotli (iOS .br 해제에 필요)"
    fi

    if ! command -v xcodegen &>/dev/null; then
        fail "xcodegen 없음. 설치: brew install xcodegen"
    fi
    if ! command -v pod &>/dev/null; then
        fail "CocoaPods 없음. 설치: sudo gem install cocoapods"
    fi

    sync_version   # VERSION → project.yml (xcodegen 전에 주입)

    info "Xcode 프로젝트 생성 + Pod 설치..."
    ( cd "$IOS_DIR" && xcodegen generate && pod install )

    ok "iOS 동기화 완료!"
    echo -e "  다음: ${YELLOW}open $IOS_DIR/AnimalPop.xcworkspace${NC} → Team 설정 → Archive → App Store Connect 업로드"
    echo -e "  (Xcode 설치 + Apple Developer 계정 필요)"
}

build_android_aab() {
    echo ""
    echo -e "${CYAN}══════════════════════════════════════${NC}"
    echo -e "${CYAN}  Android AAB 빌드 (Release)${NC}"
    echo -e "${CYAN}══════════════════════════════════════${NC}"

    if [ "${SKIP_UNITY:-}" != "1" ]; then
        unity_build_android
    fi

    # index.html 템플릿 변수 치환 (Build 파일은 Unity 빌드가 직접 생성)
    sync_android_index

    # localStorage 키 안전장치 패치 (GameBridge가 런타임 분기 처리)
    bash "$ANDROID_DIR/patch-index.sh" "$ANDROID_ASSETS/index.html"

    # 서명 설정 확인
    if [ ! -f "$ANDROID_DIR/signing.properties" ]; then
        fail "signing.properties 없음! cp signing.properties.template signing.properties 후 설정하세요."
    fi

    sync_version   # VERSION → build.gradle
    setup_java

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
    ios)
        build_ios
        ;;
    bump)
        bump_version "$2" "$3"
        ;;
    all)
        build_toss                 # Brotli 빌드 + ait
        SKIP_UNITY=1 build_ios     # 토스 Brotli 재사용 (재빌드 없이 동기화)
        build_android_apk          # 비압축 빌드 + APK (디버그)
        build_android_aab          # 기존 비압축 재사용 + AAB (릴리즈)
        ;;
    *)
        echo ""
        echo "Usage: ./build.sh <target>"
        echo ""
        echo "  toss          앱인토스 빌드 (Unity Brotli + ait build)"
        echo "  android       Android APK (Unity 비압축 + Gradle debug)"
        echo "  android-aab   Android AAB (Unity 비압축 + Gradle release)"
        echo "  ios           iOS WKWebView 래퍼 (Unity Brotli + xcodegen/pod)"
        echo "  all           전체 빌드 (toss + ios + android apk/aab)"
        echo "  bump V B      버전 올리기: VERSION 갱신 → Android/iOS 주입  예) bump 1.2.2 16"
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
