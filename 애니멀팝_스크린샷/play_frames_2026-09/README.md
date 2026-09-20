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
`IOS=1 python3 render.py <locales>` → `out_ios/<locale>/1..5.png` (1290×2796, 6.9"). 2026-09-20 iOS 1.4.2(28)로 업로드·심사 제출 완료(브랜치 `ios-1.4.2-shots`, 워크트리 `../unity-webgl-merge-engine-merge`). 번들은 본 체크아웃 `ait-build/public`(토스 라이브와 동일)을 그대로 씀.
주의: 워크트리에서 Unity 배치빌드를 새로 돌리면 merge 브랜치 manifest의 태그 없는 Toss SDK URL이 HEAD로 풀려 패키지가 늘어난 다른 번들이 된다 — `SKIP_UNITY=1`로 검증된 번들을 복사해 쓸 것.
ASC 로케일 매핑: ko-KR→ko, ja-JP→ja, zh-CN→zh-Hans, zh-TW→zh-Hant, id→id, 나머지 동일.
