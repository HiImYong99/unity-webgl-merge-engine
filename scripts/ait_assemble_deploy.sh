#!/bin/bash
# ait-build 조립 + 배포 v2 — SDK 2.0.0 스캐폴드 사용 (`ait build`, 서버 요구 번들 포맷)
# 선행: Unity AITBuildScript.BuildWebGL 로 ait-build/public + ait-build/index.html (AnimalPop) 생성됨
# 1.14.1(granite build)은 서버가 "필수 번들 파일 누락"으로 거부 → 2.0.0 `ait build` 필요
set -e
PROJ="/Users/yong/Desktop/unity-webgl-merge-engine"
AIT="$PROJ/ait-build"
# 2.0.0 스캐폴드: SDK 패키지의 BuildConfig~ (프로젝트 BuildConfig는 1.14.1로 outdated)
BC="$PROJ/Library/PackageCache/im.toss.apps-in-toss-unity-sdk@1d218b7779/WebGLTemplates/AITTemplate/BuildConfig~"
NODEBIN="$HOME/.ait-unity-sdk/nodejs/v24.13.0/darwin-arm64/bin"
export PATH="$NODEBIN:$PATH"

[ -f "$AIT/index.html" ] || { echo "FAIL: $AIT/index.html 없음 — Unity 빌드 먼저"; exit 1; }
[ -d "$AIT/public/Build" ] || { echo "FAIL: $AIT/public/Build 없음 — Unity 빌드 먼저"; exit 1; }

echo "[0] 이전 1.14.1 잔재 정리 (node_modules/lock/dist/.ait)"
rm -rf "$AIT/node_modules" "$AIT/pnpm-lock.yaml" "$AIT/dist" "$AIT"/*.ait

echo "[1] 2.0.0 BuildConfig 스캐폴드 복사 (public/·index.html 은 건드리지 않음)"
cp "$BC/package.json"    "$AIT/package.json"
cp "$BC/vite.config.ts"  "$AIT/vite.config.ts"
cp "$BC/tsconfig.json"   "$AIT/tsconfig.json"
cp "$BC/unity-bridge.ts" "$AIT/unity-bridge.ts"
cp "$BC/pnpm-lock.yaml"  "$AIT/pnpm-lock.yaml"

echo "[2] granite.config.ts 생성 (AITConfig 값 치환, 2.0.0 스키마)"
cat > "$AIT/granite.config.ts" <<'EOF'
import { defineConfig } from '@apps-in-toss/web-framework/config';

//// SDK_GENERATED_START - DO NOT EDIT THIS SECTION ////
const sdkConfig = {
  appName: 'animal-pop',
  brand: {
    displayName: '애니멀 팝',
    primaryColor: '#3182F6',
    icon: 'https://static.toss.im/appsintoss/25373/13ee6c7f-08f9-42ef-8294-dafbb535be7f.png',
    bridgeColorMode: 'inverted',
  },
  webViewProps: {
    type: 'game',
    allowsInlineMediaPlayback: false,
    mediaPlaybackRequiresUserAction: false,
  },
  web: {
    host: process.env.AIT_VITE_HOST || 'localhost',
    port: parseInt(process.env.AIT_VITE_PORT || '5173', 10),
    strictPort: false,
    commands: {
      dev: 'vite --host',
      build: 'vite build',
    },
  },
  permissions: [],
  outdir: 'dist',
};
//// SDK_GENERATED_END ////

//// USER_CONFIG_START ////
const userConfig = {};
//// USER_CONFIG_END ////

export default defineConfig({ ...sdkConfig, ...userConfig });
EOF

cd "$AIT"
echo "[3] pnpm install (web-framework 2.0.0, 번들 node $(node -v))"
pnpm install

echo "[4] 설치된 web-framework 버전 확인"
cat node_modules/@apps-in-toss/web-framework/package.json | grep '"version"' | head -1

echo "[5] ait build (서버 요구 번들 포맷 → .ait)"
pnpm exec ait build

echo "[6] .ait 확인"
ls -la *.ait

echo "[7] ait deploy (테스트 스킴)"
pnpm exec ait deploy
echo "=== 위 intoss-private:// 가 테스트 배포 URL ==="
