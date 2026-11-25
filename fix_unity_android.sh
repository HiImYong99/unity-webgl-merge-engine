#!/bin/bash
# Unity 2022.3.x Android Build Support 자동 설치 스크립트

set -e

EDITOR_PATH="/Applications/Unity/Hub/Editor/2022.3.62f3"
MODULES_JSON="$EDITOR_PATH/modules.json"

echo "======================================"
echo " Unity Android Build Support 자동 설치"
echo "======================================"

# 1. modules.json 수정
echo ""
echo "[1/3] modules.json Android 모듈 활성화 중..."

if [ ! -f "$MODULES_JSON" ]; then
  echo "❌ modules.json 파일을 찾을 수 없어요: $MODULES_JSON"
  exit 1
fi

# 백업
cp "$MODULES_JSON" "$MODULES_JSON.backup"
echo "  → 백업 완료: $MODULES_JSON.backup"

# android 관련 모듈 selected: true 로 변경
python3 -c "
import json
path = '$MODULES_JSON'
with open(path) as f:
    data = json.load(f)
count = 0
for m in data:
    if 'android' in m.get('id','').lower():
        m['selected'] = True
        count += 1
        print('  → 활성화:', m.get('id'))
with open(path, 'w') as f:
    json.dump(data, f, indent=2)
print(f'  → 총 {count}개 모듈 활성화 완료')
"

# 2. .pkg 파일 탐색
echo ""
echo "[2/3] Downloads 폴더에서 Android .pkg 파일 탐색 중..."

PKG_FILE=$(ls ~/Downloads/UnitySetup-Android-Support*.pkg ~/Downloads/*Android*Support*.pkg ~/Downloads/*android*.pkg 2>/dev/null | head -1)

if [ -z "$PKG_FILE" ]; then
  echo "❌ Downloads 폴더에서 .pkg 파일을 찾지 못했어요."
  echo "   파일명을 확인해주세요: ls ~/Downloads/*.pkg"
  exit 1
fi

echo "  → 파일 발견: $PKG_FILE"

# 3. .pkg 설치
echo ""
echo "[3/3] .pkg 설치 중... (sudo 비밀번호 필요)"
sudo installer -pkg "$PKG_FILE" -target /

echo ""
echo "======================================"
echo "✅ 완료! Unity Hub에서 확인해보세요."
echo "======================================"
