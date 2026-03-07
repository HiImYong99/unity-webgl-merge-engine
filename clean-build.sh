#!/bin/bash
# =====================================================
# Unity WebGL 빌드 캐시 정리 스크립트
# 프로젝트: unity-webgl-merge-engine
# =====================================================

PROJECT_DIR="$(cd "$(dirname "$0")" && pwd)"

echo "======================================"
echo "  Unity WebGL 빌드 캐시 정리 도구"
echo "======================================"
echo ""

# 현재 용량 확인
echo "📊 현재 용량 현황:"
echo "-----------------------------------"

check_size() {
  local path="$PROJECT_DIR/$1"
  if [ -d "$path" ]; then
    local size=$(du -sh "$path" 2>/dev/null | cut -f1)
    echo "  $1: $size"
  else
    echo "  $1: (없음)"
  fi
}

check_size "Library/Bee"
check_size "Library/ShaderCache"
check_size "Library/PlayerDataCache"
check_size "Temp"
check_size "Logs"
check_size "ait-build/node_modules"
echo ""

# 모드 선택
echo "🧹 정리 옵션을 선택하세요:"
echo "  1) 가벼운 정리  - Temp, Logs만 삭제 (빠름, Unity 재시작 불필요)"
echo "  2) 일반 정리    - Temp + Bee 빌드 캐시 삭제 (권장)"
echo "  3) 완전 정리    - Library 전체 + Temp 삭제 (Unity가 모두 재생성, 시간 오래 걸림)"
echo "  4) node_modules - ait-build/node_modules 삭제 (pnpm install로 재설치 필요)"
echo "  5) 전부 정리    - 2번 + 4번 동시 실행"
echo "  q) 취소"
echo ""
read -p "선택 (1/2/3/4/5/q): " choice

FREED=0

delete_dir() {
  local path="$PROJECT_DIR/$1"
  if [ -d "$path" ]; then
    local size_before=$(du -sk "$path" 2>/dev/null | cut -f1)
    echo "  🗑️  삭제 중: $1"
    rm -rf "$path"
    FREED=$((FREED + size_before))
    echo "  ✅ 완료: $1"
  else
    echo "  ⏭️  건너뜀 (없음): $1"
  fi
}

case "$choice" in
  1)
    echo ""
    echo "🔹 가벼운 정리 시작..."
    delete_dir "Temp"
    delete_dir "Logs"
    ;;
  2)
    echo ""
    echo "🔸 일반 정리 시작..."
    delete_dir "Temp"
    delete_dir "Logs"
    delete_dir "Library/Bee"
    delete_dir "Library/ShaderCache"
    ;;
  3)
    echo ""
    echo "🔴 완전 정리 시작... (Unity 재시작 시 Library가 전부 재생성됩니다)"
    read -p "  정말 Library 전체를 삭제하시겠습니까? (yes/no): " confirm
    if [ "$confirm" = "yes" ]; then
      delete_dir "Temp"
      delete_dir "Logs"
      delete_dir "Library"
    else
      echo "  취소되었습니다."
      exit 0
    fi
    ;;
  4)
    echo ""
    echo "📦 node_modules 정리 시작..."
    echo "  ⚠️  이후 'cd ait-build && pnpm install' 실행이 필요합니다"
    delete_dir "ait-build/node_modules"
    ;;
  5)
    echo ""
    echo "🔸 일반 + node_modules 정리 시작..."
    delete_dir "Temp"
    delete_dir "Logs"
    delete_dir "Library/Bee"
    delete_dir "Library/ShaderCache"
    echo "  ⚠️  node_modules 삭제 후 'cd ait-build && pnpm install' 실행이 필요합니다"
    delete_dir "ait-build/node_modules"
    ;;
  q|Q)
    echo "취소되었습니다."
    exit 0
    ;;
  *)
    echo "잘못된 선택입니다."
    exit 1
    ;;
esac

echo ""
echo "======================================"
FREED_MB=$((FREED / 1024))
echo "✨ 정리 완료! 약 ${FREED_MB}MB 확보"
echo "======================================"
echo ""
echo "📊 정리 후 용량:"
check_size "Library/Bee"
check_size "Library/ShaderCache"
check_size "Library/PlayerDataCache"
check_size "Temp"
check_size "Logs"
check_size "ait-build/node_modules"
echo ""
