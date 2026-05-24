# 🐾 애니멀 팝 (Animal Pop)
> **토스 앱에서 즐기는 귀여운 동물 머지 퍼즐 게임!**  
> 같은 동물을 합쳐서 사자(Lv.11)를 만들어보세요!

![Logo](https://img.shields.io/badge/Platform-Apps_in_Toss-blue?style=for-the-badge&logo=toss)
![Version](https://img.shields.io/badge/Version-1.0.0-success?style=for-the-badge)
![Engine](https://img.shields.io/badge/Unity-2022.3_LTS-white?style=for-the-badge&logo=unity)

---

## 🎮 게임 소개 (About the Game)
**애니멀 팝**은 떨어지는 귀여운 동물들을 합쳐 더 큰 동물을 만드는 **중력 기반 머지 퍼즐**입니다. 
토스(Toss) 앱 내 미니앱 환경인 **Apps in Toss**에 최적화되어, 별도 설치 없이 3천만 토스 유저들과 함께 즐길 수 있습니다.

### ✨ 주요 특징 (Key Features)
*   **물리 기반 머지:** 쫀득한 물리 효과와 함께 동물이 합쳐지는 짜릿한 손맛!
*   **토스 랭킹 연동:** 친구들, 그리고 전국의 유저들과 점수 경쟁을 즐기세요.
*   **프리미엄 혜택:** 토스페이로 **2배속 모드**를 구매하고 더 빠르고 박진감 넘치게 플레이하세요.
*   **도감 시스템:** 총 11단계의 귀여운 동물을 모두 발견하고 도감을 완성해보세요.

---

## 📸 스크린샷 (Screenshots)

| 메인 화면 | 플레이 화면 | 게임 오버 / 공유 |
| :---: | :---: | :---: |
| ![Landing](https://placehold.co/200x400/3182f6/white?text=Landing+Page) | ![Playing](https://placehold.co/200x400/e8f3e8/3182f6?text=Animal+Merge) | ![Result](https://placehold.co/200x400/3182f6/white?text=Toss+Ranking) |

---

## 🛠 기술 스택 (Tech Stack)
*   **Engine:** Unity 2022.3 LTS (WebGL Build)
*   **Frontend:** React Native (Granite) & Vanilla JS Bridge
*   **Backend Interface:** Toss SDK 2.x (IAP, TossPay, Analytics)
*   **Styling:** Toss Design System (TDS) 기반 프리미엄 UI

---

## 🚀 플레이하기 (How to Play)
애니멀 팝은 **토스 앱** 내에서 만나보실 수 있습니다.

1.  **토스 앱** 실행
2.  하단 **'전체'** 탭 클릭
3.  **'애니멀 팝'** 검색 또는 미니앱 리스트에서 선택
4.  지금 바로 사자를 만들러 가보세요! 🦁

---

## 🧩 멀티플랫폼 운영 (단일 브랜치)

**하나의 코드베이스로 3개 플랫폼을 운영합니다.** Unity 프로젝트와 WebGL 빌드는 1개이며,
`Assets/WebGLTemplates/AnimalPop/index.html`의 `GameBridge`가 **런타임에 플랫폼을 자동 감지**해
광고·결제·리더보드·공유를 분기합니다. 브랜치를 플랫폼별로 나누지 않습니다.

```
window.AP_PLATFORM = AndroidBridge 있으면 'android'
                   : iosBridge 있으면 'ios'
                   : 그 외 'toss'
```

| 플랫폼 | 래퍼 | 광고/결제 |
| :--- | :--- | :--- |
| Toss (앱인토스) | `ait-build/` | Toss SDK (TossAds, TossPay) |
| Google Play (Android) | `android-wrapper/` | AdMob + Play Billing |
| App Store (iOS) | `ios-wrapper/` | AdMob + StoreKit 2 + Game Center |

### 빌드 (`./build.sh <타깃>`)
```bash
./build.sh toss          # 앱인토스 (Unity Brotli + ait)
./build.sh android       # Android APK (디버그)
./build.sh android-aab   # Android AAB (릴리즈)
./build.sh ios           # iOS WKWebView 래퍼 (xcodegen + pod)
./build.sh all           # 전체 (toss + ios + android)
```

### 버전 단일 소스
`VERSION` 파일 하나가 Android(`versionName`/`versionCode`)와 iOS(`MARKETING_VERSION`/
`CURRENT_PROJECT_VERSION`)에 자동 주입됩니다. 빌드 시마다 동기화되며, 올릴 때:
```bash
./build.sh bump 1.2.2 16   # APP_VERSION=1.2.2, BUILD_NUMBER=16 → 양 플랫폼 반영
```

### 배포 전 체크 (ID 교체 등)
플랫폼별 광고/결제 ID는 테스트 값이 들어 있습니다. 배포 전 `PRE_REVIEW_CHECKLIST.md`와
각 래퍼 README(`ios-wrapper/README.md`)를 확인해 실제 값으로 교체하세요.

---

## 💼 파트너십 및 프로젝트 관리
이 프로젝트는 **1인 개발** 프로젝트로, 코드의 간결함과 확장성을 동시에 고려하여 설계되었습니다.

*   **Lead Engineer:** yong ([GitHub](https://github.com/HiImYong99))

---
© 2026 Animal Pop Team. All rights reserved.
