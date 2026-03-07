#!/bin/bash
# =====================================================
# macOS 불필요 파일 정리 스크립트
# 4년치 캐시 & 정크 파일 제거용
# =====================================================
# ⚠️  사용 전 반드시 읽어보세요:
#   - 삭제 전 항목별 용량을 미리 보여줍니다
#   - 각 단계마다 확인을 요청합니다
#   - 시스템 안정성에 영향을 주지 않는 항목만 대상입니다
# =====================================================

set -e

RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
CYAN='\033[0;36m'
BOLD='\033[1m'
NC='\033[0m'

TOTAL_FREED=0

print_header() {
  echo ""
  echo -e "${BOLD}${BLUE}======================================${NC}"
  echo -e "${BOLD}${BLUE}   🧹 macOS 정리 도구${NC}"
  echo -e "${BOLD}${BLUE}======================================${NC}"
  echo ""
}

get_size() {
  local path="$1"
  if [ -d "$path" ] || [ -f "$path" ]; then
    du -sh "$path" 2>/dev/null | cut -f1
  else
    echo "0B"
  fi
}

get_size_kb() {
  local path="$1"
  if [ -d "$path" ] || [ -f "$path" ]; then
    du -sk "$path" 2>/dev/null | cut -f1
  else
    echo "0"
  fi
}

ask_confirm() {
  local msg="$1"
  echo -e "${YELLOW}❓ $msg (y/n): ${NC}"
  read -r answer
  [[ "$answer" == "y" || "$answer" == "Y" ]]
}

clean_section() {
  local title="$1"
  local path="$2"
  local desc="$3"

  if [ ! -d "$path" ]; then
    return
  fi

  local size
  size=$(get_size "$path")
  local size_kb
  size_kb=$(get_size_kb "$path")

  echo -e "${CYAN}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
  echo -e "${BOLD}📁 $title${NC}"
  echo -e "   경로: ${path/#$HOME/~}"
  echo -e "   용량: ${RED}${BOLD}$size${NC}"
  echo -e "   설명: $desc"

  if ask_confirm "삭제하시겠습니까?"; then
    rm -rf "$path"/* 2>/dev/null || true
    TOTAL_FREED=$((TOTAL_FREED + size_kb))
    echo -e "   ${GREEN}✅ 삭제 완료 ($size 확보)${NC}"
  else
    echo -e "   ⏭️  건너뜀"
  fi
  echo ""
}

clean_file_pattern() {
  local title="$1"
  local search_path="$2"
  local pattern="$3"
  local desc="$4"
  local max_age="${5:-0}"  # days, 0 = all

  local files
  if [ "$max_age" -gt 0 ]; then
    files=$(find "$search_path" -name "$pattern" -mtime +$max_age 2>/dev/null | head -100)
  else
    files=$(find "$search_path" -name "$pattern" 2>/dev/null | head -100)
  fi

  if [ -z "$files" ]; then
    return
  fi

  local total_kb=0
  while IFS= read -r f; do
    local fkb
    fkb=$(du -sk "$f" 2>/dev/null | cut -f1)
    total_kb=$((total_kb + fkb))
  done <<< "$files"

  local total_mb=$((total_kb / 1024))

  echo -e "${CYAN}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
  echo -e "${BOLD}📄 $title${NC}"
  echo -e "   용량: ${RED}${BOLD}~${total_mb}MB${NC}"
  echo -e "   설명: $desc"

  if ask_confirm "삭제하시겠습니까?"; then
    while IFS= read -r f; do
      rm -rf "$f" 2>/dev/null || true
    done <<< "$files"
    TOTAL_FREED=$((TOTAL_FREED + total_kb))
    echo -e "   ${GREEN}✅ 삭제 완료${NC}"
  else
    echo -e "   ⏭️  건너뜀"
  fi
  echo ""
}

# ── 메인 실행 ──────────────────────────────────────

print_header

# 현재 디스크 사용량 확인
echo -e "${BOLD}💾 현재 디스크 사용 현황:${NC}"
df -h / | tail -1 | awk '{print "   전체: "$2"  사용중: "$3"  남은 공간: "$4"  ("$5" 사용)"}'
echo ""

echo -e "${BOLD}🔍 정리 항목별 용량 분석 중...${NC}"
echo ""

# ─────────────────────────────────────────────────────
echo -e "${BOLD}${YELLOW}[ 1단계: 시스템 캐시 ]${NC}"
echo ""

clean_section \
  "사용자 캐시 (~/Library/Caches)" \
  "$HOME/Library/Caches" \
  "앱들이 생성하는 임시 캐시. 4년치 누적되면 수GB. 삭제해도 앱이 재생성함"

clean_section \
  "시스템 로그 (~/Library/Logs)" \
  "$HOME/Library/Logs" \
  "앱 로그 파일. 디버깅 목적 외 불필요. 안전하게 삭제 가능"

# ─────────────────────────────────────────────────────
echo -e "${BOLD}${YELLOW}[ 2단계: 개발 도구 캐시 ]${NC}"
echo ""

# npm 캐시
if [ -d "$HOME/.npm" ]; then
  clean_section \
    "npm 캐시 (~/.npm)" \
    "$HOME/.npm" \
    "Node.js 패키지 캐시. 삭제해도 npm이 자동 재생성"
fi

# yarn 캐시
if [ -d "$HOME/Library/Caches/Yarn" ]; then
  clean_section \
    "Yarn 캐시" \
    "$HOME/Library/Caches/Yarn" \
    "Yarn 패키지 캐시"
fi

# pnpm 캐시
if [ -d "$HOME/Library/Caches/pnpm" ]; then
  clean_section \
    "pnpm 캐시" \
    "$HOME/Library/Caches/pnpm" \
    "pnpm 패키지 캐시"
fi

# Homebrew 캐시
if [ -d "$HOME/Library/Caches/Homebrew" ]; then
  clean_section \
    "Homebrew 캐시" \
    "$HOME/Library/Caches/Homebrew" \
    "설치된 패키지의 이전 버전 캐시"
fi

# CocoaPods 캐시
if [ -d "$HOME/Library/Caches/CocoaPods" ]; then
  clean_section \
    "CocoaPods 캐시" \
    "$HOME/Library/Caches/CocoaPods" \
    "iOS/macOS 의존성 캐시"
fi

# Gradle 캐시
if [ -d "$HOME/.gradle/caches" ]; then
  clean_section \
    "Gradle 캐시 (~/.gradle/caches)" \
    "$HOME/.gradle/caches" \
    "Android/Java 빌드 캐시"
fi

# ─────────────────────────────────────────────────────
echo -e "${BOLD}${YELLOW}[ 3단계: 앱별 대용량 캐시 ]${NC}"
echo ""

# Xcode
if [ -d "$HOME/Library/Developer/Xcode/DerivedData" ]; then
  clean_section \
    "Xcode DerivedData" \
    "$HOME/Library/Developer/Xcode/DerivedData" \
    "Xcode 빌드 중간 산출물. 수GB 차지하는 경우 많음. 다음 빌드 시 재생성"
fi

if [ -d "$HOME/Library/Developer/CoreSimulator/Caches" ]; then
  clean_section \
    "iOS 시뮬레이터 캐시" \
    "$HOME/Library/Developer/CoreSimulator/Caches" \
    "iOS 시뮬레이터 캐시 파일"
fi

# Unity 에디터 캐시
if [ -d "$HOME/Library/Unity" ]; then
  clean_section \
    "Unity 에디터 캐시 (~/Library/Unity)" \
    "$HOME/Library/Unity" \
    "Unity 에디터 전역 캐시. 삭제해도 재생성됨"
fi

# VS Code 캐시
if [ -d "$HOME/Library/Application Support/Code/CachedData" ]; then
  clean_section \
    "VS Code 캐시" \
    "$HOME/Library/Application Support/Code/CachedData" \
    "Visual Studio Code 캐시"
fi

# Chrome 캐시
if [ -d "$HOME/Library/Caches/Google/Chrome" ]; then
  clean_section \
    "Chrome 캐시" \
    "$HOME/Library/Caches/Google/Chrome" \
    "Google Chrome 브라우저 캐시. 삭제 후 브라우저 재시작 필요"
fi

# ─────────────────────────────────────────────────────
echo -e "${BOLD}${YELLOW}[ 4단계: 임시 파일 ]${NC}"
echo ""

clean_section \
  "시스템 임시 폴더 (/tmp)" \
  "/tmp" \
  "시스템 임시 파일. 재부팅 시 자동 삭제되는 파일들"

# .DS_Store 파일
echo -e "${CYAN}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
echo -e "${BOLD}📄 .DS_Store 파일 (홈 디렉토리 전체)${NC}"
echo -e "   설명: Finder가 만드는 숨김 파일. 완전 불필요"
DS_COUNT=$(find "$HOME" -name ".DS_Store" 2>/dev/null | wc -l | tr -d ' ')
echo -e "   발견된 파일 수: ${RED}${DS_COUNT}개${NC}"

if ask_confirm "삭제하시겠습니까?"; then
  find "$HOME" -name ".DS_Store" -delete 2>/dev/null || true
  echo -e "   ${GREEN}✅ ${DS_COUNT}개 삭제 완료${NC}"
fi
echo ""

# ─────────────────────────────────────────────────────
# 최종 결과
echo -e "${BOLD}${BLUE}======================================"
echo -e "   ✨ 정리 완료!"
echo -e "======================================${NC}"
echo ""

FREED_MB=$((TOTAL_FREED / 1024))
FREED_GB=$(echo "scale=1; $FREED_MB / 1024" | bc 2>/dev/null || echo "${FREED_MB}MB")

echo -e "   💾 확보된 공간: ${GREEN}${BOLD}약 ${FREED_MB}MB${NC}"
echo ""
echo -e "${BOLD}💾 정리 후 디스크 현황:${NC}"
df -h / | tail -1 | awk '{print "   전체: "$2"  사용중: "$3"  남은 공간: "$4"  ("$5" 사용)"}'
echo ""
echo -e "${YELLOW}💡 추가 팁:${NC}"
echo "   • Homebrew 이전 버전 정리: brew cleanup"
echo "   • npm 캐시 검증 후 정리:   npm cache clean --force"
echo "   • pnpm 캐시 정리:          pnpm store prune"
echo "   • Xcode 시뮬레이터 정리:   xcrun simctl delete unavailable"
echo ""
