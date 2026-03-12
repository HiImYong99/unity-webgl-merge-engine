#!/bin/bash
# ═══════════════════════════════════════════════════════════════
#  Animal Pop – 구글 플레이 릴리즈 AAB 빌드 스크립트
#  사용 전 signing.properties 파일을 먼저 설정하세요.
# ═══════════════════════════════════════════════════════════════

set -e

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
PROJECT_ROOT="$(dirname "$SCRIPT_DIR")"

echo "=================================================="
echo "  Animal Pop – Release AAB Build"
echo "=================================================="

# ── 1. WebGL 에셋 확인 ──────────────────────────────────────────
ASSETS_DIR="$SCRIPT_DIR/app/src/main/assets"
if [ ! -f "$ASSETS_DIR/index.html" ]; then
    echo "[ERROR] index.html 없음. WebGL 빌드를 먼저 복사하세요:"
    echo "  Unity → File → Build Settings → WebGL → Build"
    echo "  빌드 출력물을 $ASSETS_DIR/ 로 복사"
    exit 1
fi

if [ ! -d "$ASSETS_DIR/Build" ]; then
    echo "[ERROR] Build/ 디렉토리 없음. WebGL 빌드 파일을 확인하세요."
    exit 1
fi
echo "[OK] WebGL 에셋 확인 완료"

# ── 2. 서명 설정 확인 ───────────────────────────────────────────
if [ ! -f "$SCRIPT_DIR/signing.properties" ]; then
    echo "[ERROR] signing.properties 없음!"
    echo "  cp signing.properties.template signing.properties"
    echo "  후 비밀번호를 입력하세요."
    exit 1
fi
echo "[OK] signing.properties 확인 완료"

# ── 3. Java 환경 설정 ───────────────────────────────────────────
# Unity 번들 JDK 사용 (Java 11, AGP 7.4.x 호환)
UNITY_JDK="/Applications/Unity/Hub/Editor/2022.3.62f3/PlaybackEngines/AndroidPlayer/OpenJDK"
if [ -d "$UNITY_JDK" ]; then
    export JAVA_HOME="$UNITY_JDK"
    export PATH="$JAVA_HOME/bin:$PATH"
    echo "[OK] JAVA_HOME: $JAVA_HOME"
else
    echo "[WARN] Unity JDK 없음. 시스템 Java 사용"
fi

# ── 4. AAB 빌드 ─────────────────────────────────────────────────
cd "$SCRIPT_DIR"
echo ""
echo ">>> Gradle bundleRelease 실행 중..."
./gradlew bundleRelease --stacktrace

# ── 5. 산출물 복사 ──────────────────────────────────────────────
AAB_SRC="$SCRIPT_DIR/app/build/outputs/bundle/release/app-release.aab"
AAB_DST="$PROJECT_ROOT/AnimalPop-release.aab"

if [ -f "$AAB_SRC" ]; then
    cp "$AAB_SRC" "$AAB_DST"
    echo ""
    echo "=================================================="
    echo "  ✅ 빌드 성공!"
    echo "  출력: $AAB_DST"
    echo "  크기: $(du -sh "$AAB_DST" | cut -f1)"
    echo "=================================================="
    echo ""
    echo "다음 단계:"
    echo "  1. 구글 플레이 콘솔 → 앱 → 프로덕션 → 새 릴리즈 만들기"
    echo "  2. AnimalPop-release.aab 업로드"
    echo "  3. 콘텐츠 등급 / 개인정보 처리방침 URL 입력"
    echo "  4. 검토 후 게시"
else
    echo "[ERROR] AAB 파일 생성 실패. 빌드 로그를 확인하세요."
    exit 1
fi
