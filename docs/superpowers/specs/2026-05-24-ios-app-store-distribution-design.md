# iOS App Store 배포 설계 (Approach A: WKWebView 래퍼)

작성일: 2026-05-24
대상 브랜치: `merge` (main 토스 + google-play 안드로이드 통합 브랜치)
상태: 설계 확정 대기

## 1. 목표

Animal Pop(애니멀 팝)을 **Apple App Store(iOS)** 에 배포한다. 기존
Toss(WebGL) / Google Play(Android WebView 래퍼)와 동일한 게임·UI·WebGL 빌드를
재사용하고, 플랫폼 분기만 추가한다.

## 2. 접근 방식 — WKWebView 래퍼 (Approach A)

`android-wrapper/`(네이티브 Android Activity가 WebView로 Unity WebGL 로드)를
그대로 본떠 `ios-wrapper/`(네이티브 iOS 앱이 `WKWebView`로 동일 WebGL 빌드 로드)를
신규 작성한다.

**선정 이유**
- 게임 로직 + HTML 오버레이 UI(현재 모든 UI가 HTML) 100% 재사용
- 검증된 듀얼 플랫폼 구조와 일관 — 신규 표면 최소
- 3개 플랫폼이 단일 WebGL 빌드 공유

**대안 기각 사유**
- 네이티브 Unity iOS 빌드: HTML 오버레이 UI 전체를 Unity Canvas로 재구축 +
  광고/결제 네이티브 재구현 필요 → 과도한 작업, 웹 우선 구조와 괴리
- Capacitor 래퍼: 무거운 프레임워크 의존성 추가, 수작업 튜닝된 Brotli/WASM
  로딩 재작업, 플랫폼 1개 추가에 과함

## 3. 모듈 구조 — `ios-wrapper/`

```
ios-wrapper/
  Podfile                         # Google-Mobile-Ads-SDK, GCDWebServer
  AnimalPop.xcodeproj
  AnimalPop/
    AppDelegate.swift             # 앱 진입, GameCenter 인증, AdMob init
    GameViewController.swift      # WKWebView 호스트, 풀스크린, 오디오 생명주기
    IosBridge.swift               # WKScriptMessageHandler (JS → 네이티브)
    AdManager.swift               # AdMob interstitial/rewarded/banner
    StoreManager.swift            # StoreKit 2 IAP (remove_ads_hint_pack)
    GameCenterManager.swift       # GameKit 인증/점수 제출/리더보드 UI
    LocalWebServer.swift          # GCDWebServer (Brotli + range + MIME)
    Info.plist
    Assets.xcassets               # 앱 아이콘 (애니멀팝)
    web/                          # build.sh가 Unity WebGL Build/ 복사 (gitignore)
```

- **최소 배포 타깃**: iOS 15 (StoreKit 2 async/await)
- **Bundle ID**: `com.animalpop.app` (Android applicationId와 동일 reverse-DNS)
- **버전**: 1.2.1 (Android versionName과 정렬)

### 3.1 GameViewController + WKWebView
- 전체 화면 `WKWebView`, 상태바 숨김, `isIdleTimerDisabled = true`(화면 항상 켜짐)
- 백그라운드 진입 시 Web Audio `audioContext.suspend()`, 복귀 시 `resume()`
  (Android `onPause`/`onResume` 동일 — 기존 알려진 오디오 정지 이슈 방지)
- 하단 배너 영역 확보(safe area 고려), 배너는 `AdManager`가 부착

### 3.2 LocalWebServer (WebGL 서빙)
- 임베디드 **GCDWebServer**를 `127.0.0.1` 루프백에 기동, `web/` 디렉토리 서빙
- `.br` 파일에 `Content-Encoding: br` + 올바른 MIME(`application/wasm`,
  `application/javascript`, `application/octet-stream`) 부여, range 요청 지원
- WebKit은 Brotli 네이티브 지원 → **Toss용 Brotli 빌드 그대로 재사용**
  (Android는 WebView 한계로 수동 헤더 주입; iOS는 WebKit이 처리)
- `file://` 사용 시 WASM streaming/CORS 제약 → 루프백 HTTP로 회피
  (Android `WebViewAssetLoader`의 iOS 대응물)

## 4. JS ↔ 네이티브 브릿지 계약

JS는 `window.webkit.messageHandlers.iosBridge.postMessage({ action, ... })`로 호출.
네이티브는 `webView.evaluateJavaScript(...)`로 콜백.

### 4.1 JS → 네이티브 (action)
| action | payload | 동작 |
|--------|---------|------|
| `showInterstitialAd` | — | 게임오버 전면 광고 |
| `showRewardedAd` | — | 보상형 광고(이어하기) |
| `launchPurchase` | `{ productId }` | StoreKit 결제 시트 |
| `restorePurchases` | — | 구매 복원 |
| `submitScore` | `{ score }` | Game Center 점수 제출 |
| `showLeaderboard` | — | Game Center 리더보드 UI |
| `shareText` | `{ text }` | `UIActivityViewController` 공유 |
| `log` | `{ message }` | 디버그 로그 |

### 4.2 네이티브 → JS (콜백, Android 명명 규칙 대응)
| 콜백 | 시점 |
|------|------|
| `onInterstitialClosedFromIOS()` | 전면 광고 닫힘/실패 |
| `onAdRewardedFromIOS()` | 보상 획득 |
| `onAdFailedFromIOS()` | 보상형 실패/미시청 |
| `onRewardedAdReadyFromIOS()` | 보상형 로드 완료 |
| `onPurchaseSuccessFromIOS(productId, token)` | 결제 성공 |
| `onPurchaseFailedFromIOS(productId, code)` | 결제 실패 |
| `onPurchaseCancelledFromIOS(productId)` | 결제 취소 |
| `onPurchaseRestoredFromIOS(productId, token)` | 구매 복원 |

### 4.3 비동기 readiness 처리
Android `isRewardedAdReady()`는 동기 반환이지만 WKWebView 메시지 핸들러는 비동기.
→ JS가 `onRewardedAdReadyFromIOS()` 콜백으로 readiness 플래그를 **캐시**하고
GameBridge ios 분기는 캐시값을 읽는다. 동기 네이티브 호출 불필요
(`WKScriptMessageHandlerWithReply` 도입 회피로 단순화).

## 5. AdManager (AdMob iOS SDK)
- Google Mobile Ads iOS SDK(CocoaPods, 로컬 1.16.2 설치 확인됨)
- interstitial / rewarded / 하단 banner — Android와 동일 역할
- 로드 실패 시 재시도(백오프), 로드 완료 시 `onRewardedAdReadyFromIOS()` 발행
- **iOS 전용 AdMob 앱 + iOS 광고 단위 ID 필요** (Android ID는 iOS에서 미동작)
- 테스트 단계: Google 테스트 광고 단위 ID 사용, 릴리즈 시 실제 ID 교체
- ATT(App Tracking Transparency): 개인화 광고 시 `ATTrackingManager` 권한 요청
  + `Info.plist` `NSUserTrackingUsageDescription`

## 6. StoreManager (StoreKit 2)
- 비소모성(non-consumable) `remove_ads_hint_pack` — 광고 제거 + 힌트 10개
  (Android `BillingManager`와 동일 의미)
- 구매: `Product.purchase()` → `Transaction.finish()`
- 복원: `Transaction.currentEntitlements` 순회 → `onPurchaseRestoredFromIOS`
- 앱 시작/포그라운드 복귀 시 entitlement 재확인(보상 누락 방지)
- App Store Connect에 동일 product ID 등록 필요

## 7. GameCenterManager (GameKit)
- 앱 시작 시 `GKLocalPlayer.local.authenticateHandler`로 인증
- `submitScore` → `GKLeaderboard.submitScore(...)` (리더보드 ID 1개)
- `showLeaderboard` → `GKGameCenterViewController(leaderboardID:)` 표시
- 게임오버/신기록 시 GameBridge가 `submitScore` 호출
- App Store Connect에 리더보드 생성 + ID 설정 필요
- **4.2 가이드라인 대응**: 네이티브 가치 추가 요소

## 8. GameBridge 변경 (`Assets/WebGLTemplates/AnimalPop/index.html`)
정본 템플릿의 `GameBridge`에 3번째 플랫폼 분기 추가. (android-wrapper의 index.html은
빌드 시 생성/패치되므로 정본만 수정)

- 플랫폼 감지:
  `window.AP_PLATFORM = window.AndroidBridge ? 'android' : (window.webkit?.messageHandlers?.iosBridge ? 'ios' : 'toss')`
- ios 분기: 광고/IAP/공유/리더보드를 `iosBridge.postMessage`로 라우팅
- 4.2절 네이티브→JS 콜백 8종 + readiness 캐시 추가
- 리더보드 버튼: ios에서 `showLeaderboard` 라우팅 (숨기지 않음 — Game Center 연동)
- 점수 제출: 신기록/게임오버 훅에서 ios면 `submitScore` 호출

## 9. build.sh `ios` 타깃 추가
```
./build.sh ios   # Unity WebGL(Brotli) 빌드 → ios-wrapper/web/ 복사 → pod install
```
- `unity_build_ios()`: 기존 `unity_build_toss`와 동일 Brotli 빌드 재사용 가능
  (별도 압축 분기 불필요 — WebKit Brotli 지원)
- `Build/` → `ios-wrapper/AnimalPop/web/` 복사
- `pod install` 실행 후 `.xcworkspace` 안내
- **아카이브/서명/제출은 Xcode GUI 수동 단계** (서명 인증서 필요)
- `build.sh all`에 ios 포함 여부는 선택 (기본 미포함, 명시 호출)

## 10. 필요한 ID / 설정 (사용자 등록)
| 항목 | 값/출처 |
|------|---------|
| Bundle ID | `com.animalpop.app` |
| IAP product ID | `remove_ads_hint_pack` (App Store Connect 등록) |
| AdMob iOS 앱 ID | 신규 생성 (Info.plist `GADApplicationIdentifier`) |
| AdMob 광고 단위 ID | iOS 전면/보상형/배너 신규 생성 |
| Game Center 리더보드 ID | App Store Connect 생성 |

## 11. App Store Guideline 4.2 (최소 기능) 대응
순수 웹 래퍼는 리젝 리스크 존재. 네이티브 가치로 완화:
- StoreKit 2 네이티브 결제 (웹 결제 아님)
- Game Center 리더보드/점수 (네이티브 통합)
- 오프라인 플레이(번들된 WebGL, 네트워크 불필요)
- 브라우저 크롬 없음(풀스크린 WKWebView)
- 햅틱 피드백(선택) — 병합/게임오버 시 `UIImpactFeedbackGenerator`
- 그래도 리젝 가능성 존재 → 제출 시 리뷰 노트로 네이티브 기능 강조

## 12. 사전 준비 (자동화 불가, 사용자 액션)
1. **Apple Developer Program** 가입 ($99/년)
2. **Xcode 설치** — 현재 Mac에 미설치(`xcodebuild` 없음). 빌드/서명/제출 필수
3. **AdMob**: iOS 앱 + iOS 광고 단위 ID 생성
4. **App Store Connect**: 앱 레코드, IAP `remove_ads_hint_pack`, 리더보드,
   개인정보 라벨, 스크린샷(`애니멀팝_스크린샷/` 활용)

## 13. 범위 외 (Non-goals)
- 네이티브 Unity iOS 빌드 전환
- iPad 전용 레이아웃 최적화 (iPhone 우선, iPad는 호환 모드)
- 푸시 알림
- 토스/안드로이드 기존 동작 변경 (회귀 없어야 함)

## 14. 검증
- Toss / Android 빌드 회귀 없음 확인 (`AP_PLATFORM` 분기 → 기존 경로 불변)
- iOS 시뮬레이터/실기기에서: WebGL 로드, 60fps 근접, 광고 표시,
  결제 시트(Sandbox), 복원, 리더보드 표시, 백그라운드 오디오 정지/복귀
- 코드 리뷰(`feature-dev:code-reviewer`) 후 빌드 (전역 규칙)

## 15. 리스크
- WKWebView WebGL 성능이 네이티브보다 낮음 → 저사양 기기 검증 필요
- App Store 4.2 리젝 가능 → 네이티브 기능 강조로 완화
- Brotli + GCDWebServer range 요청 상호작용 → 실기기 검증 필요
- ATT 권한 거부 시 비개인화 광고로 폴백
