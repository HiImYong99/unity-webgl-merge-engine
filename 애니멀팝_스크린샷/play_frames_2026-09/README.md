# Play 스크린샷 (2026-09-19 교체)

교체 이유: 2달간 스토어 방문 284 → 설치 7 (CVR 2%). 기존 스크린샷이 빈 통·타이틀·게임오버 위주였다.
전부 실플레이 캡처 — `webgl/` 빌드를 로컬 서빙해 헤드리스 Chrome에서 자동 드롭으로 게임오버까지 플레이.

- `out/<Play 로케일>/1..5.png` — 라이브에 올라간 1080×1920 프레임 (10개 로케일, UI 현지화)
- `prev_live/` — 교체 전 라이브 이미지 (롤백용)
- `captions.json` — 프레임 헤드라인·보조문구 × 로케일

## 재생성
```
cp ~/.claude/skills/store-screenshots/scripts/cdp.py .   # CDP 클라이언트(스킬 소유)
python3 serve.py ../../webgl 8765 &                       # .br에 Content-Encoding 붙여 서빙
python3 autoplay.py en-US 400 11                          # <Chrome --lang> <최대 드롭> <seed/포트 오프셋>  → raw/<lang>/
python3 render.py en-US ko-KR ...                         # raw → out/
python3 upload_play.py            # validate만 / --commit 으로 반영 (play.py = SA 로더 필요)
```
raw 캡처(로케일당 ~12MB)는 커밋하지 않는다. 인도네시아어는 `--lang=id-ID`로 찍고 `raw/id`로 이름을 바꾼다(Play 로케일 코드가 `id`).
동시 실행은 4개까지 — 6개를 띄우면 로딩이 늦어 Start 탭이 빗나간다(재시도 루프는 넣어둠).

## iOS (App Store) 버전
`IOS=1 python3 render.py <locales>` → `out_ios/<locale>/1..5.png` (1290×2796, 6.9"). **아직 미업로드** — iOS 스크린샷은 버전 바운드라 새 버전+빌드+심사가 필요한데,
sdk-3x-v2 브랜치 VERSION.txt(1.4.0/25)가 iOS 라이브 1.4.1(build 26)·Play 1.4.2(27, origin/merge wip)보다 뒤라 어느 트리로 빌드할지 먼저 정해야 한다.
ASC 로케일 매핑: ko-KR→ko, ja-JP→ja, zh-CN→zh-Hans, zh-TW→zh-Hant, id→id, 나머지 동일.
