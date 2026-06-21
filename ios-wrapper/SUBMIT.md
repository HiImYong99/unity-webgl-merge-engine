# iOS 제출 준비 완료 (2026-05-26)

검정화면 + 게임플레이 레이아웃 깨짐 버그 수정·시뮬 검증 완료. App Store 업로드용 아카이브와 서명된 `.ipa` 준비됨.
**아래 "제출 방법" 중 하나만 누르면 됩니다.**

## 게임플레이 레이아웃 깨짐 (4번째 근본원인, 수정됨)
초기엔 메인 메뉴만 검증하고 "준비 완료"로 판단했으나, 게임 진입 시 컨테이너가 좁고
화면을 못 채우며 HUD가 겹치는 문제가 있었음. 실측 결과 **WKWebView 뷰포트가 320×480**으로
잡혀 있었음(iPhone 16 Plus 실제 430×932).
- 원인: **Info.plist에 런치스크린 키 누락** → iOS가 앱을 레거시 호환 모드(320×480)로 띄우고
  화면 전체로 스케일업 → `device-width`가 320으로 오인되어 게임 반응형 레이아웃이 어긋남.
- 수정: `Info.plist`에 `<key>UILaunchScreen</key><dict/>` 추가(네이티브 전체화면 렌더 활성화).
  + `GameViewController`가 `viewDidAppear`(레이아웃 완료 후) 시점에 로드하도록 변경.
- 검증: view.bounds 430×932, vw=430, canvas 1290×2796, **컨테이너가 화면을 꽉 채움** 확인.
- 잔여 미세 폴리시: 상단 다이나믹 아일랜드 뒤로 스폰 동물이 살짝 걸침(기능 무관).

---

## 검정화면 원인 (3중 버그, 모두 수정됨)

1. **로컬 웹서버 미기동** — `GCDWebServer`를 기본값(`AutomaticallySuspendInBackground=true`)으로
   시작하면, 콜드런치 초기에 앱이 잠깐 `background` 상태라 `startWithOptions:`가 단락평가로
   **실제 소켓 바인딩을 건너뛰고도 YES를 반환**함 → `port=0`, `serverURL=nil`, `baseURL=nil` →
   WebView 로드 자체가 스킵 → 영구 검정. (b105 런치만 우연히 active라 떴던 게 "간헐성"의 정체)
   - 수정: `GCDWebServerOption_AutomaticallySuspendInBackground: false`로 즉시 바인딩 강제.
   - 추가 방어: `serverURL`이 nil이면 `server.port`로 baseURL 직접 구성.

2. **WKWebView는 plain HTTP에서 Brotli(`Content-Encoding: br`)를 디코딩하지 않음** (HTTPS 전용).
   토스용 `.br` 빌드를 그대로 루프백 HTTP로 주면 Unity 로더가 raw brotli를 받아 파싱 실패.
   - 수정: 빌드 동기화(`build.sh`) 시 `.br` 파일을 미리 해제(identity, 파일명은 유지),
     서버는 `Content-Encoding` 없이 올바른 MIME으로 서빙.

3. (부수) `.br` + HTTP range(206)는 동시 성립 불가 — 핸들러가 전체 파일(200)만 서빙하도록 정리.

## 변경 파일
- `ios-wrapper/AnimalPop/Info.plist` — **`UILaunchScreen` 추가(레이아웃 깨짐 핵심 수정)**, DEVELOPMENT_TEAM 등
- `ios-wrapper/AnimalPop/LocalWebServer.swift` — suspend=false, baseURL 폴백, CE 제거, MIME 유지
- `ios-wrapper/AnimalPop/GameViewController.swift` — viewDidAppear 로드, 진단 로깅(전부 `#if DEBUG`)
- `build.sh` — iOS 동기화 시 `.br` 자동 해제 단계 추가
- `ProjectSettings/ProjectSettings.asset` — `webGLCompressionFormat` 2 복원(토스 Brotli 보존)
- `ios-wrapper/project.yml` — `DEVELOPMENT_TEAM: A3NZTDWF42`

## e2e 검증 (시뮬레이터, iPhone 16 Plus / iOS 26.5)
- 서버 기동 OK → index.html 200 → `[GameBridge] Platform: ios` → WebGL 2.0 컨텍스트
- **메인 메뉴 정상 렌더** + **게임 진입(자동시작 주입)해 컨테이너가 화면 꽉 채움 확인**
- 뷰포트 430×932(네이티브) 확정, AdMob 테스트 배너 표시

---

## 산출물 (레이아웃 수정 반영 최신본)
- 아카이브(서명됨): `ios-wrapper/build/AnimalPop.xcarchive`
  - Xcode Organizer에도 복사됨: `~/Library/Developer/Xcode/Archives/2026-05-26/`
- **App Store 서명된 IPA**: `ios-wrapper/build/export/AnimalPop.ipa` (Cloud Managed Apple Distribution)
- 버전: **1.2.1 (build 15)**, 번들 ID: `com.animalpop.app`, 팀: `A3NZTDWF42`

---

## 제출 방법 (둘 중 하나)

### A) Transporter 앱 (가장 빠름)
1. Mac App Store에서 **Transporter** 설치(무료) 후 Apple ID 로그인.
2. `ios-wrapper/build/export/AnimalPop.ipa`를 창에 드래그.
3. **Deliver** 클릭 → App Store Connect에 업로드 완료.

### B) Xcode Organizer
1. Xcode → Window → Organizer → Archives.
2. **"AnimalPop 2026-05-26"** 선택 → **Distribute App** → **App Store Connect** → **Upload**.
3. 자동 서명으로 distribution 재서명·업로드 진행.

> 업로드 후: App Store Connect → 해당 빌드 선택 → 심사 제출(Submit for Review).

---

## 제출 전 확인사항
- **App Store Connect에 `com.animalpop.app` 앱 레코드**가 있어야 함(없으면 먼저 생성, 버전 1.2.1).
- **build 15가 이미 업로드된 적 있으면** 번호 충돌 → 빌드 번호 올릴 것:
  `./build.sh bump`(또는 `project.yml`의 `CURRENT_PROJECT_VERSION` 증가) 후 재아카이브.
- altool/API 자동 업로드는 App Store Connect API 키 또는 앱 전용 암호 필요(여기선 미설정).

## ⚠️ 공개 출시 전 (심사엔 무방, 수익화 전 필수)
- **AdMob이 전부 테스트 ID** (`ca-app-pub-3940256099942544~…`, 배너에 "Test mode" 표시).
  - `AnimalPop/Info.plist`의 `GADApplicationIdentifier`
  - `AnimalPop/AdManager.swift`의 `interstitial/rewarded/banner AdUnitID`
  실제 AdMob ID로 교체해야 광고 수익 발생.

## 알려진 비차단 경고
- `wasm streaming compile failed: Unexpected MIME ... falling back to ArrayBuffer instantiation`
  → instantiateStreaming 대신 ArrayBuffer 폴백으로 정상 동작(게임 구동 OK). 시작이 살짝 느려질 뿐,
  기능·심사 무관. 추후 perf 최적화로 wasm Content-Type 정합성만 손보면 됨.
