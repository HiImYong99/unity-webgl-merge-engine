#!/bin/bash
# 애니멀 팝 Android APK 빌드 스크립트
# Unity Android SDK + Gradle로 Android Studio 없이 빌드

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
JAVA_HOME="/Applications/Unity/Hub/Editor/2022.3.62f3/PlaybackEngines/AndroidPlayer/OpenJDK"

echo "========================================"
echo "  애니멀 팝 APK 빌드"
echo "========================================"

# JAVA_HOME 설정
export JAVA_HOME="$JAVA_HOME"
export PATH="$JAVA_HOME/bin:$PATH"

echo "[1/3] Java 버전 확인..."
java -version

echo ""
echo "[2/3] WebGL 빌드 파일 확인..."
if [ ! -f "$SCRIPT_DIR/app/src/main/assets/index.html" ]; then
    echo "❌ assets/index.html 없음!"
    echo "   Unity에서 WebGL 빌드 후 파일을 복사하세요:"
    echo "   android-wrapper/app/src/main/assets/ 에 index.html, Build/, TemplateData/ 복사"
    exit 1
fi
echo "✅ WebGL 파일 확인됨"

echo ""
echo "[3/3] APK 빌드 중..."
cd "$SCRIPT_DIR"
JAVA_HOME="$JAVA_HOME" ./gradlew assembleDebug

if [ $? -eq 0 ]; then
    APK_PATH="$SCRIPT_DIR/app/build/outputs/apk/debug/app-debug.apk"
    cp "$APK_PATH" "$SCRIPT_DIR/../AnimalPop.apk"
    echo ""
    echo "========================================"
    echo "✅ 빌드 완료!"
    echo "   출력: $(dirname "$SCRIPT_DIR")/AnimalPop.apk"
    echo "========================================"
    echo ""
    echo "설치 방법:"
    echo "  adb install ../AnimalPop.apk"
    echo "  또는 카카오톡/구글드라이브로 폰에 전송 후 설치"
else
    echo "❌ 빌드 실패"
    exit 1
fi
