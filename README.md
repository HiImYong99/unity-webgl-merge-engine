# 🍧 디저트 팝 (Dessert Pop)

> **"토스(Toss) 인앱 환경에서 펼쳐지는 캐주얼 물리 머지 게임"**

![Unity](https://img.shields.io/badge/Unity-2022.3%20LTS-000000?style=flat-square&logo=unity&logoColor=white)
![WebGL](https://img.shields.io/badge/Build-WebGL-563D7C?style=flat-square&logo=html5&logoColor=white)
![C#](https://img.shields.io/badge/Language-C%23-239120?style=flat-square&logo=c-sharp&logoColor=white)
![Toss](https://img.shields.io/badge/Platform-Apps%20in%20Toss-0050FF?style=flat-square)

**디저트 팝**은 유니티 WebGL로 개발되어 토스(Toss) 미니앱 생태계인 'Apps in Toss'에서 구동되도록 최적화된 하이퍼 캐주얼 퍼즐 게임입니다. 동일한 디저트를 떨어뜨려 결합하고 더 높은 단계로 진화시키는 '수박 게임(Suika Game)' 스타일의 로직을 채택했습니다.

단순한 게임성을 넘어, **Toss SDK 연동, 렌더링 최적화, 기기 대응(Safe Area), 인앱 브릿지** 등 실제 상용 서비스(MVP) 레벨에 요구되는 기술적 도전 과제들을 성공적으로 해결한 프로젝트입니다.

---

## ✨ 핵심 기능 (Key Features)

### 🎮 진화형 물리 머지 엔진 (Physics Merge Engine)
* **정교한 물리 연산:** `Unity Physics 2D`를 활용해 디저트들이 쌓이고 튕기는 사실적인 텐션(Bounciness 0.2, Friction 0.4)을 구현했습니다.
* **충돌 최적화:** `GetInstanceID()`를 통한 식별로, 동일 프레임 내에서 발생하는 중복 머지(Double Merge) 버그를 원천 차단했습니다.
* **유연한 데이터 구조:** `ScriptableObject`를 통해 레벨업 별 배율, 점수, 프리팹 등의 밸런싱 데이터를 중앙 집중화하여 기획자의 유지보수를 용이하게 했습니다.

### 🌉 인앱 브릿지 및 SDK 연동 (JSlib Interop)
* **Toss 로그인 (`AppLogin`) 연동:** `@apps-in-toss/web-framework`와 통신하는 `WebBridge.jslib`를 구축하여, 게임 진입 시 유저 식별자(userKey)를 안전하게 획득합니다.
* **Web Share API 완벽 지원:** 유니티 `RenderTexture`로 게임 결과 화면을 실시간 캡쳐하고, 이를 Base64 기반의 PNG 파일 객체로 변환하여 네이티브 공유 시트(`navigator.share`)를 띄웁니다.
* **미니앱 종료 제어:** Toss QA 가이드라인을 준수하여, 사용자가 '닫기' 버튼 클릭 시 종료 확인 모달을 띄우고 `window.Toss.close()`를 안전하게 호출합니다.

### 📱 완벽한 UX 및 모바일 대응 (Mobile-First UX)
* **Safe Area (노치 및 다이내믹 아일랜드) 대응:** `SafeArea.cs` 스크립트를 통해 스크린 비율과 노치 영역을 실시간으로 계산하여 UI가 가려지지 않게 앵커링을 동적으로 조절합니다.
* **백그라운드 사운드 제어:** 앱이 백그라운드로 전환될 때(`OnApplicationPause`, `OnApplicationFocus`) 모든 BGM 및 SFX를 일시 정지하고, 포그라운드 복귀 시 유저의 기존 설정에 맞게 매끄럽게 재생을 재개합니다.
* **로컬 스토리지 데이터 직렬화:** 단일 Key-Value 저장을 넘어, `JsonUtility`를 이용해 최고 점수, 설정 상태, 플레이 진척도를 JSON 구조로 안전하게 로컬 `PlayerPrefs`에 영속화(Persist)합니다.

### ⚡ WebGL 렌더링 최적화 (Optimization)
* Editor 스크립트(`WebGLOptimizer.cs`)를 통해 빌드 파이프라인을 자동화하여, **Strip Engine Code** 및 **Decompression Fallback**을 강제 활성화합니다.
* 무거운 `Resources` 폴더 사용을 배제하고 WebGL 환경에 맞춰 초기 로딩 속도 및 메모리 점유율을 대폭 낮췄습니다.

---

## 🏗 아키텍처 및 폴더 구조 (Architecture)

프로젝트는 유지보수성과 확장성을 극대화하기 위해 도메인별로 철저히 분리되었습니다.

```text
Assets/_Project/
├── Plugins/
│   └── WebBridge.jslib         # JS <-> C# 양방향 통신 브릿지 (Toss SDK)
├── Scripts/
│   ├── Core/                   # GameManager, AudioManager, SpawnManager 등 싱글톤 매니저
│   ├── Entity/                 # Dessert (개별 오브젝트 물리 및 병합 로직)
│   ├── Editor/                 # WebGL 빌드 파이프라인 자동화 스크립트
│   ├── UI/                     # UIManager, SafeArea, ResultCard (렌더 텍스처 캡처)
│   └── Web/                    # BridgeManager (jslib 함수 DllImport 래퍼)
├── Data/                       # ScriptableObject 기반의 디저트 진화 트리 데이터
└── Prefabs/                    # UI 패널 및 레벨별 디저트 프리팹
```

---

## 🕹 게임 플레이 흐름 (Game Flow)

1. **랜딩 페이지 (Landing State):** 게임 로딩 직후 Toss SDK를 통해 유저 인증을 진행합니다. 인증이 완료되면 현재까지의 최고 점수와 시작 버튼이 활성화됩니다.
2. **플레이 영역 (Playing State):** 화면을 터치/드래그하여 디저트의 낙하 위치를 결정합니다. 쿨타임(Spawn Cooldown)과 UI 터치 차단(EventSystem) 로직이 적용되어 안정적인 조작감을 제공합니다.
3. **위험 경고 및 게임 오버 (GameOver State):** 디저트가 상단 데드라인(DeadLine)에 3초 이상 닿을 경우 게임 오버 판정이 내려집니다.
4. **결과 공유 및 부활:** 게임 오버 시 결과 스크린샷을 찍어 친구에게 공유(`ShareResult`)할 수 있으며, 광고 시청(`ShowAd`)을 통해 데드라인 근처의 장애물을 제거하고 1회 한정으로 부활(`Revive`)할 수 있습니다.

---

## 🛠 로컬 빌드 및 실행 방법

1. **Unity Hub**를 통해 `Unity 2022.3 LTS` (WebGL Build Support 포함) 버전으로 프로젝트를 엽니다.
2. `Assets/_Project/Scenes/MainGame.unity` 씬을 엽니다.
3. 상단 메뉴 `File > Build Settings`에서 플랫폼을 **WebGL**로 변경(`Switch Platform`)합니다.
4. `Build And Run`을 클릭하여 로컬 브라우저에서 실행합니다.
   * *주의: 로컬 브라우저나 에디터 환경에서는 Toss SDK가 존재하지 않으므로, 코딩된 Editor Fallback(더미 로그 및 시뮬레이션 로직)이 자동으로 작동하여 게임 테스트에 지장을 주지 않습니다.*

---

## 📄 라이선스 (License)

본 프로젝트는 개인 포트폴리오 및 기술 증명 목적으로 제작된 MVP 버전입니다.
Toss SDK 연동 예제 및 아키텍처 참조용으로 활용할 수 있습니다.

*Developed with ❤️ focusing on Mobile WebGL performance and UX.*
