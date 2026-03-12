# AppsInToss 버전 백포트 체크리스트

Android(Google Play) 마이그레이션 과정에서 수정된 버그/기능 중
AppsInToss(Toss 미니앱) 버전에도 적용이 필요한 항목 목록입니다.

---

## 🔴 즉시 수정 필요

### 1. 도감(Dex) 가로 스크롤 불가
**파일**: `Assets/WebGLTemplates/AnimalPop/index.html`

기기에서 도감 영역이 좁아 11번째 동물이 잘림. 현재 `.dex-slots`에 스크롤 CSS 없음.

**수정 내용**: `.dex-slots` CSS 교체
```css
/* 현재 */
.dex-slots {
    display: flex; align-items: center; gap: 4px; flex: 1; justify-content: space-between;
}

/* 수정 후 */
.dex-slots {
    display: flex; align-items: center; gap: 4px;
    overflow-x: auto; overflow-y: hidden;
    -ms-overflow-style: none; scrollbar-width: none;
    scroll-snap-type: x proximity;
    -webkit-overflow-scrolling: touch;
    padding-bottom: 2px;
}
.dex-slots::-webkit-scrollbar { display: none; }
```

`.dex-slot`에 `flex-shrink: 0` 이미 있으므로 CSS만 추가하면 됨.

---

### 2. 도감 툴팁 위치 오류 (좌상단에 표시)
**파일**: `Assets/WebGLTemplates/AnimalPop/index.html`

툴팁 너비를 `tipW = 120`으로 하드코딩해서 실제 위치 계산이 어긋남.

**수정 내용**: `showDexTooltip()` 함수 교체
```js
function showDexTooltip(slot, level) {
    if (!dexTooltip) return;
    var found = _dexFound[level];
    dexTooltip.textContent = found
        ? 'Lv.' + level + ' ' + DEX_NAMES[level]
        : 'Lv.' + level + ' ???';
    dexTooltip.classList.add('show');

    // 렌더링 후 실제 크기 측정
    var tipW = dexTooltip.offsetWidth  || 120;
    var tipH = dexTooltip.offsetHeight || 32;
    var GAP  = 6;
    var r    = slot.getBoundingClientRect();

    var left = r.left + r.width / 2 - tipW / 2;
    left = Math.max(8, Math.min(left, window.innerWidth - tipW - 8));

    // 슬롯 위로 표시, 화면 상단 잘리면 아래로
    var top = r.top - tipH - GAP;
    if (top < 8) top = r.bottom + GAP;

    dexTooltip.style.left = left + 'px';
    dexTooltip.style.top  = top  + 'px';

    if (_dexTipTimer) clearTimeout(_dexTipTimer);
    _dexTipTimer = setTimeout(function() { dexTooltip.classList.remove('show'); }, 1600);
}
```

---

### 3. 게임물관리위원회 / 전체이용가 제거
**파일**: `Assets/WebGLTemplates/AnimalPop/index.html` (744~745번째 줄)

법적 문제 소지로 제거 결정됨.

**삭제할 줄**:
```html
<div style="background:#00a854; color:#fff; font-size:10px; font-weight:800; padding:2px 8px; border-radius:4px;">전체이용가</div>
<div style="font-size:10px; color:var(--text-tertiary); font-weight:600;">게임물관리위원회 심의 준수</div>
```

---

### 4. 공유 버튼 미동작
**파일**: `Assets/WebGLTemplates/AnimalPop/index.html`

현재 `onShareClicked()`가 `unityInstance.SendMessage('ResultCard', 'CaptureAndShare')` 호출 → Toss 환경에서 동작 안 함.

**수정 내용**: `onShareClicked()` 함수 교체
```js
function onShareClicked() {
    var score = _currentDisplayScore || 0;
    var best  = _highScore || 0;
    var text  = '애니멀 팝에서 ' + score.toLocaleString('ko-KR') + '점을 달성했어요!\n'
              + '(최고기록: ' + best.toLocaleString('ko-KR') + '점)\n'
              + '나도 도전해봐요 🐾';

    if (navigator.share) {
        navigator.share({ title: '애니멀 팝 🐾', text: text })
            .catch(function() {});
        return;
    }
    // 클립보드 폴백
    if (navigator.clipboard && navigator.clipboard.writeText) {
        navigator.clipboard.writeText(text).then(function() {
            showToast('클립보드에 복사됐어요!');
        }).catch(function() { showToast('공유를 지원하지 않는 환경입니다.'); });
    } else {
        showToast('공유를 지원하지 않는 환경입니다.');
    }
}
```

---

## 🟡 Unity C# 공통 수정 (이미 반영됐는지 확인 필요)

### 5. 효과음 노이즈 (AudioSource 풀)
**파일**: `Assets/_Project/Scripts/Managers/SoundMgr.cs`

동시에 여러 효과음 재생 시 노이즈 발생.

**수정 내용**:
- `AudioSource` 4개 풀(`SFX_POOL_SIZE = 4`) 라운드로빈 재생
- 병합음 쿨다운 0.03s → 0.08s
- 착지음 쿨다운 0.05s → 0.08s
- 병합 볼륨 0.7 → 0.55 / 착지 max볼륨 0.35 → 0.25

> ✅ Google Play 빌드에서 이미 적용됨. SoundMgr.cs가 공유 파일이면 자동 반영.

---

### 6. 광고 시청 후 부활 로직
**파일**: `Assets/_Project/Scripts/Managers/GameMgr.cs`

`ContinueGameAfterAd(string _)` 메서드가 추가됨.
- 상단 위험 구역 동물 제거 후 Playing 상태 복귀
- `HasRevived` 플래그로 1회 제한

> Toss 버전에서 "광고 보고 계속하기" 기능을 사용한다면 적용 필요.
> 현재 Toss 버전에서 이 기능을 활성화할지 여부 결정 필요.

---

## 🟢 Toss 전용 — 불필요 (적용 제외)

| 항목 | 이유 |
|------|------|
| Google Play Billing (BillingManager.java) | Android 네이티브 전용 |
| AdMob 광고 (MainActivity.java) | Android 네이티브 전용, Toss는 자체 광고 정책 |
| AndroidBridge JavascriptInterface | Android 전용, Toss는 TossBridge 사용 |
| pauseTimers() 백그라운드 처리 | Android WebView 전용, Toss 플랫폼이 자체 관리 |
| signingConfig / AAB 빌드 | Android 배포 전용 |

---

## 작업 순서 권장

1. [ ] **#3** 게임물관리위원회 제거 (2줄 삭제, 5분)
2. [ ] **#1** 도감 스크롤 CSS 수정 (10분)
3. [ ] **#2** 툴팁 위치 수정 (10분)
4. [ ] **#4** 공유 버튼 수정 (10분)
5. [ ] **#5** SoundMgr.cs 공유 파일 여부 확인 → 미반영 시 적용
6. [ ] **#6** 부활 기능 Toss 버전 활성화 여부 결정

---

*작성일: 2026-03-12*
*기준 브랜치: google-play (Android 빌드)*
*적용 대상 브랜치: main (AppsInToss 빌드)*
