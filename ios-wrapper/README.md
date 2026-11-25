# iOS 래퍼 (App Store 배포)

Animal Pop을 Apple App Store에 배포하기 위한 **WKWebView 네이티브 래퍼**.
`android-wrapper/`와 동일 구조 — 동일 Unity WebGL(Brotli) 빌드를 그대로 로드한다.

## 구조
```
ios-wrapper/
  project.yml        # XcodeGen 프로젝트 정의 (.xcodeproj 생성)
  Podfile            # GoogleMobileAds, GCDWebServer
  AnimalPop/
    AppDelegate.swift        # ATT 권한 요청
    SceneDelegate.swift      # 윈도우 → GameViewController
    GameViewController.swift # WKWebView 호스트 + 배너 + 오디오 생명주기
    IosBridge.swift          # JS ↔ 네이티브 (window.webkit.messageHandlers.iosBridge)
    LocalWebServer.swift     # GCDWebServer (Brotli + range + MIME)
    AdManager.swift          # AdMob 전면/보상형/배너
    StoreManager.swift       # StoreKit 2 (remove_ads_hint_pack)
    GameCenterManager.swift  # Game Center 인증/점수/리더보드
    Info.plist / *.entitlements / Assets.xcassets
    web/                     # build.sh ios가 채움 (gitignore)
```

## 빌드
```bash
# 프로젝트 루트에서
./build.sh ios          # Unity WebGL(Brotli) 빌드 → web/ 복사 → xcodegen + pod install
open ios-wrapper/AnimalPop.xcworkspace
# Xcode: Signing & Capabilities에서 Team 설정 → Archive → App Store Connect 업로드
```
`SKIP_UNITY=1 ./build.sh ios` 로 기존 WebGL 빌드 재사용 가능.

## ★ 배포 전 교체 필수 (USER ACTION)
| 항목 | 위치 |
|------|------|
| Apple Team ID | `project.yml` → `DEVELOPMENT_TEAM` |
| AdMob iOS 앱 ID | `AnimalPop/Info.plist` → `GADApplicationIdentifier` |
| AdMob iOS 광고 단위 ID (전면/보상형/배너) | `AnimalPop/AdManager.swift` 상단 상수 |
| Game Center 리더보드 ID | `AnimalPop/GameCenterManager.swift` → `leaderboardId` |
| SKAdNetwork 전체 목록 | `Info.plist` → `SKAdNetworkItems` (Google 공식 목록으로 보강) |

현재 광고 ID는 **Google 공식 테스트 ID**다. 실제 ID로 교체 전 테스트 광고로 확인.

## 사전 준비
1. **Xcode** 설치 (Mac App Store) — 빌드/서명/제출에 필수
2. **Apple Developer Program** 가입 ($99/년)
3. **AdMob**: iOS 앱 + iOS 광고 단위 ID 생성
4. **App Store Connect**:
   - 앱 레코드 (Bundle ID `com.animalpop.app`)
   - 인앱 구매(비소모성) `remove_ads_hint_pack`
   - Game Center 리더보드 (`animalpop_highscore`)
   - 개인정보 처리방침 URL, 개인정보 라벨, 스크린샷(`애니멀팝_스크린샷/`)

## App Store 심사 4.2(최소 기능) 대응
순수 웹 래퍼는 리젝 가능성 존재. 네이티브 가치로 완화:
- StoreKit 2 네이티브 결제 · Game Center 리더보드/점수 · 오프라인 플레이 ·
  풀스크린(브라우저 크롬 없음). 제출 시 리뷰 노트로 네이티브 기능 명시.

## 의존성 버전 주의
`Podfile`은 AdMob을 `~> 11.13`(GAD 프리픽스 API)에 고정. SDK 12.x는 Swift API에서
`GAD` 프리픽스를 제거하므로, 12.x로 올리면 `AdManager.swift`의 타입명 수정 필요.
