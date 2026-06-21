#!/bin/bash
# iOS 아카이브 → 익스포트(App Store) → altool 업로드(TestFlight)
# 선행: ./build.sh ios (web 동기화 + xcodegen/pod 완료)
# 사용: scripts/ios_archive_upload.sh [archive|export|upload|all]
set -e
set -o pipefail   # 아카이브 실패가 0으로 새어나가 stale IPA 업로드되는 것 방지

IOS="/Users/yong/Desktop/unity-webgl-merge-engine/ios-wrapper"
KEY_ID="85N66SDDNX"
ISSUER="b195fbab-7bb6-4253-839e-553b15d6b140"
P8="/Users/yong/Downloads/AuthKey_85N66SDDNX.p8"
ARCHIVE="$IOS/build/AnimalPop.xcarchive"
EXPORTDIR="$IOS/build/export"
WS="$IOS/AnimalPop.xcworkspace"

AUTH="-allowProvisioningUpdates -authenticationKeyID $KEY_ID -authenticationKeyIssuerID $ISSUER -authenticationKeyPath $P8"

do_archive() {
  echo "[archive] $ARCHIVE"
  rm -rf "$ARCHIVE"
  xcodebuild -workspace "$WS" -scheme AnimalPop -configuration Release \
    -destination 'generic/platform=iOS' -archivePath "$ARCHIVE" \
    $AUTH clean archive | tail -40
  [ -d "$ARCHIVE" ] || { echo "FAIL: 아카이브 생성 안 됨"; exit 1; }
}

do_export() {
  echo "[export] $EXPORTDIR"
  rm -rf "$EXPORTDIR"
  xcodebuild -exportArchive -archivePath "$ARCHIVE" \
    -exportOptionsPlist "$IOS/ExportOptions.plist" -exportPath "$EXPORTDIR" \
    $AUTH | tail -30
  ls -la "$EXPORTDIR"/*.ipa
}

do_upload() {
  local IPA=$(ls "$EXPORTDIR"/*.ipa 2>/dev/null | head -1)
  [ -n "$IPA" ] || { echo "FAIL: IPA 없음 — export 먼저"; exit 1; }
  echo "[upload] $IPA → TestFlight"
  xcrun altool --upload-app -f "$IPA" -t ios --apiKey "$KEY_ID" --apiIssuer "$ISSUER"
}

case "${1:-all}" in
  archive) do_archive ;;
  export)  do_export ;;
  upload)  do_upload ;;
  all)     do_archive; do_export; do_upload ;;
  *) echo "usage: $0 [archive|export|upload|all]"; exit 1 ;;
esac
