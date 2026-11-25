#!/bin/bash
# ═══════════════════════════════════════════════════════════════
#  Android index.html 패치 스크립트 (v2 — 최소화)
#
#  GameBridge 통합 브릿지 도입으로 대부분의 sed 패치가 제거됨.
#  index.html이 런타임에 window.AndroidBridge 유무로 플랫폼을 감지하여
#  광고/IAP/배너/리더보드를 자동 분기함.
#
#  남은 패치: localStorage 키 통일 (dessertpop → animalpop 마이그레이션 안전장치)
# ═══════════════════════════════════════════════════════════════

set -e

INDEX="$1"
if [ -z "$INDEX" ] || [ ! -f "$INDEX" ]; then
    echo "[ERROR] Usage: patch-index.sh <path/to/index.html>"
    exit 1
fi

echo "[PATCH] Android index.html 패치 시작: $INDEX"

# ── 1. localStorage 키 통일 (안전장치) ──
sed -i '' "s|dessertpop_best|animalpop_best|g" "$INDEX"
echo "[PATCH] 1/1 localStorage 키 통일"

echo "[PATCH] ✅ Android 패치 완료! (GameBridge가 런타임 분기 처리)"
