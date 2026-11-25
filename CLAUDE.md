# Project Overview
- Unity 2022.3.62f3 dual-platform project: WebGL (Toss) + Android (Google Play)
- Single codebase with platform-specific branches managed via `merge` branch

# Game Core Logic Rules (DO NOT MODIFY)
- Level-based fixed assets
- Game over detection: Overflow-based only (animal must fall completely outside container), NOT deadline crossing
- Scoreboard data: do not use 'dessert name', 'toss score', or 'notes' columns

# Coding Style & Environment
- WebView communication: Always consider async handling when writing Unity-JS bridge code
- Platform conditionals: Use `#if UNITY_WEBGL` / `#if UNITY_ANDROID` for platform-specific code paths
- Android wrapper: `android-wrapper/` contains the Android WebView shell (Gradle project)
- WebGL template: `Assets/WebGLTemplates/AnimalPop/` for Toss deployment

# Build Targets
- **Toss (WebGL):** `build-ait.sh` for .ait package generation
- **Android:** `android-wrapper/build_apk.sh` or `android-wrapper/build_aab.sh`
- **Playgama/CrazyGames (HTML5 포털):** `./build.sh playgama` — 아래 섹션

# Playgama 웹 빌드 (HTML5 포털: Playgama 카탈로그 → CrazyGames 등)
```bash
./build.sh playgama            # playgama/build.sh → build/animal-pop-playgama/ + build/animal-pop-playgama.zip (업로드용)
cd playgama && npm test        # host.js 단위 테스트 (가짜 브리지·가짜 시계)
python3 playgama/test/e2e.py   # 헤드리스 Chrome 실주행 (--encoding: .gz에 Content-Encoding 붙이는 포털 흉내)
```
- **Unity 재빌드 없음**: `ait-build/public`(마지막 토스 WebGL 빌드)의 Build 파일 + 현재 템플릿. C#/jslib 변경은 토스 빌드를 새로 뽑아야 반영된다.
- 압축: .br을 풀어 framework.js는 원본, data·wasm은 `.gz` → `playgama_bridge.js`의 fetch 심이 DecompressionStream(없으면 fflate)으로 푼다. 포털 서버의 Content-Encoding 설정과 무관.
- `playgama/src/host.js` = npm `@playgama/bridge@2.2.0`을 esbuild로 번들(CDN 없음) → `window.PlaygamaHost`. 템플릿은 이게 있으면 `AP_PLATFORM='playgama'`(`_PG`) — Toss/Android/iOS 빌드엔 번들이 없어 동작 그대로.
- index.html = 템플릿 + 변수 치환 + 브리지 주입 + `unity-bridge.ts` 제거 + Google Fonts→`playgama/fonts`(Nunito, OFL). 치환이 하나라도 안 먹으면 FAIL.

| 템플릿 (`_PG`) | PlaygamaHost |
|---|---|
| 부활·2배속 버튼 | `showRewarded()` — `'rewarded'`일 때만 보상. 실패=토스트, 중간 닫음=무반응. 2배속은 결제 시트 없이 바로 광고 |
| 게임오버 → 다시하기 | `showInterstitial()` (템플릿 75s 간격 + bridge config 60s) |
| 랜딩 표시 | `ready`(초기화 8s + 세이브 선로드 5s) 뒤 → 설정·포털 언어 재적용 → `game_ready` 1회 |
| 시작·다시하기 / 게임오버 / 부활 | `level_started` / `level_failed` / `gameplay_started` |
| 저장 | `KEYS`(best·bgm·sfx·dc·streak)를 선로드해 localStorage에 채움. 게임오버·설정 토글·탭 숨김 때 바뀐 키만 set(1건씩, 8s 멈추면 늦게 끝날 때 최신 재전송). 선로드 실패 세션은 안 씀 |
| 포털 일시정지·광고 중 | `onPause` → Unity `Module.pauseMainLoop()`, 소리는 host.js 마스터 게인 0 |

- 꺼진 것: 결제(무료 프리미엄 개발용 confirm 폴백도 삭제)·리더보드·공유·구매복원·배너·뒤로가기 가로채기·토스 미션/프로필. 음소거 UI = 설정(톱니) BGM/효과음 토글, YouTube면 숨김.
- 화면: 폰은 세로 전용(터치 기기 가로 = 회전 안내) → 포털 등록 orientation=portrait, YouTube 불가. 데스크톱 가로는 캔버스를 1:2로 필러박스(넓으면 CameraScaler가 시야를 줄여 스폰 y=7이 위로 잘림), 낮은 iframe은 카드 zoom 축소.
- 외부 요청: 게임 자체는 0(e2e 검증). 브리지가 호스트별 SDK를 로드하고 Playgama 분석 이벤트를 보낸다(외부 호출 금지 호스트에선 브리지가 끔).

함정:
- 최고 점수 원본은 Unity PlayerPrefs(IndexedDB, 브리지 밖)이고 빌드된 jslib `_ShowHtmlLanding`이 localStorage 최고점을 그 값으로 덮는다 → 표시는 항상 `_PG.best()`(Unity·로컬·브리지 최댓값), 브리지 쓰기는 감소 금지(`MAX_KEYS`). 완전히 브리지로 옮기려면 C# 재빌드 필요.
- 빌드된 C#에 디버그 단축키가 살아 있다(End=즉시 게임오버, G=3배속, 토스 빌드도 동일) → host.js가 캡처 단계에서 막는다.
- Unity는 터치·사운드 재생마다 AudioContext를 resume한다 → suspend로는 음소거가 새어 나온다(그래서 마스터 게인).
- 템플릿에 localStorage 진행 키를 추가하면 host.js `KEYS`에도 추가.
- e2e: 브리지 모듈은 Proxy라 첫 접근 때 메서드 래퍼를 캐시한다 → 스텁은 initialize 직후 설치. localhost=mock 플랫폼은 광고 미지원이라 광고 스텁 필수.
