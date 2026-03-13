#!/bin/bash
set -e

PROJECT_DIR="$(cd "$(dirname "$0")" && pwd)"
UNITY="/Applications/Unity/Hub/Editor/2022.3.62f3/Unity.app/Contents/MacOS/Unity"
AIT_DIR="$PROJECT_DIR/ait-build"
LOG_FILE="$PROJECT_DIR/unity-build.log"

echo "========================================"
echo "  Animal Pop - AIT 전체 빌드 시작"
echo "========================================"

# ── 1. Unity WebGL 빌드 ──────────────────────
echo ""
echo "[1/2] Unity WebGL 빌드 중..."
echo "      출력: $AIT_DIR/public"
echo "      로그: $LOG_FILE"
echo ""

"$UNITY" \
  -batchmode \
  -quit \
  -projectPath "$PROJECT_DIR" \
  -executeMethod AITBuildScript.BuildWebGL \
  -logFile "$LOG_FILE"

echo "[1/2] ✅ Unity WebGL 빌드 완료"

# ── 2. AIT 패키징 빌드 ───────────────────────
echo ""
echo "[2/2] ait build 실행 중..."
echo ""

cd "$AIT_DIR"
pnpm build

echo ""
echo "========================================"
echo "  ✅ 빌드 완료! animal-pop.ait 생성됨"
echo "========================================"
echo ""
echo "배포하려면: cd ait-build && pnpm deploy"
