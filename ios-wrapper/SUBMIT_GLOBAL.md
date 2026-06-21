# Animal Pop — iOS 글로벌 출시 노트 (1.3.0 / build 17)

토스 라이브 버전 기반 + **10개 언어 다국어** + **CTR/ASO 최적화**. WKWebView 래퍼. ASC ID `6782373454`, 번들 `com.animalpop.app`, 팀 A3NZTDWF42.

## 상태 (2026-06-20)
- ✅ App Store Connect 앱 레코드 생성됨 — App Store 이름 **`Animal Pop: Merge Puzzle`**("Animal Pop"은 타계정 선점 → 콜론 변형으로 고유 확보). 홈화면 표시명은 `Animal Pop`/로케일별(.lproj).
- ✅ **TestFlight 업로드 완료**: build **17** (1.3.0), `proc=VALID`, `usesNonExemptEncryption=False`.
- ✅ **수출규정(compliance) 자동 통과**: Info.plist `ITSAppUsesNonExemptEncryption=false`(표준 HTTPS=면제) → 빌드마다 수동 응답 불필요. (빌드16은 이 키 없어 미사용 → 무시.)
- ✅ **App Store 메타데이터 10개 언어 ASC 주입 완료**: 이름·부제·설명·키워드·프로모션 텍스트(en-US,ko,ja,zh-Hans,zh-Hant,es-ES,pt-BR,de-DE,fr-FR,id). 버전 문자열 1.3.0.

## 이번 버전 변경점 (심사 노트)
- **글로벌 다국어(신규)**: 기기 언어 자동감지 + 영어 폴백 10개 언어. 인앱 UI 전체·동물 이름·공유 문구·홈화면 표시명·ATT 문구 현지화. 게임 코어 로직/에셋 불변(현 토스 라이브와 동일).
- **광고/결제**: AdMob 배너+전면+보상형(현재 Google 테스트 ID — 수익화 전 교체, 심사 무관). IAP: 광고 제거/평생 2배속(비소모성). 로그인 불필요. Game Center 리더보드(선택).
- **테스트 방법**: 실행 → 게임 시작 → 같은 동물 떨어뜨려 합치기. 오버플로 시 게임오버. 세로 전용. 첫 실행 ATT 안내.

## 공개 출시 전 남은 것 (텍스트는 완료, 비차단)
- TestFlight: 내부 테스터 즉시 / 외부 테스터는 베타 심사 1회.
- App Store 심사 제출 시 필요(에셋·정책): **로케일별 스크린샷·앱 아이콘·연령등급·개인정보 라벨·지원 URL**. 메타 텍스트는 주입 완료.
- ASO/CTR 권고(아이콘 A/B·스크린샷 캡션·PPO): `ios-wrapper/ASO_GLOBAL.md`.
- AdMob 실 ID 교체(수익화 시).

## 산출물 / 스크립트
- IPA(App Store 서명): `ios-wrapper/build/export/AnimalPop.ipa` (build 17)
- 빌드/업로드: `scripts/ios_archive_upload.sh [archive|export|upload|all]`
- 메타데이터 재주입: `python3 /tmp/asc_push.py <meta.json>` (멱등; 패턴은 메모리 `ios_global_release_pipeline.md`)
