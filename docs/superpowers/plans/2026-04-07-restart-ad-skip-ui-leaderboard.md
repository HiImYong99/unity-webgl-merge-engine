# Restart Ad Skip + UI Reposition + Leaderboard Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 게임 재시작 시 전면광고 제거, 하단 컨트롤 바 상단 이동, 앱인토스 리더보드 연동

**Architecture:** index.html의 `restartGame()` 흐름에서 `_showInterstitialThen()` 호출을 직접 콜백으로 대체하여 재시작 광고를 제거한다. 하단 UI 요소(도감/설정/타이머)의 CSS `bottom` 값을 `top` 기준으로 변경한다. 리더보드는 TossBridge.jslib에 `openGameCenterLeaderboard` 브릿지를 추가하고, index.html의 랜딩/게임오버 화면에 리더보드 버튼을 삽입한다. Android에서는 리더보드 버튼을 숨긴다.

**Tech Stack:** HTML/CSS/JS (index.html), C# Unity (TossBridge.jslib, BridgeMgr.cs), Shell (patch-index.sh)

---

## File Structure

| Action | File | Responsibility |
|--------|------|---------------|
| Modify | `Assets/WebGLTemplates/AnimalPop/index.html` | 재시작 광고 제거, UI 위치 조정, 리더보드 UI/JS 추가 |
| Modify | `Assets/Plugins/TossBridge.jslib` | `TossOpenLeaderboard` 브릿지 함수 추가 |
| Modify | `Assets/_Project/Scripts/Managers/BridgeMgr.cs` | `OpenLeaderboard()` C# 메서드 추가 |
| Modify | `android-wrapper/patch-index.sh` | Android 리더보드 버튼 숨기기 패치 추가 |

---

### Task 1: 게임 재시작 시 광고 제거

**Files:**
- Modify: `Assets/WebGLTemplates/AnimalPop/index.html:1936-1939` (onRestartFromSettings)
- Modify: `Assets/WebGLTemplates/AnimalPop/index.html:2004-2035` (restartGame)

현재 `restartGame()`은 `_showInterstitialThen(cb)`을 호출하여 전면광고를 보여준 뒤 콜백으로 게임을 재시작한다. 이 광고 호출을 제거하고 콜백 내용을 직접 실행하도록 변경한다.

> **중요:** `_showInterstitialThen()` 함수 자체는 삭제하지 않는다. 다른 곳(예: 향후 추가될 트리거)에서 사용될 수 있고, 보상형 광고/부활 광고와는 별개이다. 재시작 경로에서만 호출을 제거한다.

- [ ] **Step 1: `restartGame()` 함수에서 `_showInterstitialThen` 호출 제거**

`Assets/WebGLTemplates/AnimalPop/index.html` 라인 2004-2035를 아래로 교체:

```javascript
    function restartGame() {
        if (!unityInstance) { location.reload(); return; }
        // 재시작 시 광고 생략 — 바로 게임 초기화
        goOverlay.classList.remove('visible');
        hideDangerBanner();
        showHUD(false);
        if (newBestBadge) newBestBadge.classList.remove('show');
        _currentDisplayScore = 0;
        if (scoreEl) scoreEl.textContent = '0';
        _speedBoostActive = false;
        _refreshSpeedBtn();
        if (_premiumSpeedOwned && _premiumSpeedOn && unityInstance) {
            setTimeout(function() {
                unityInstance.SendMessage('BridgeManager', 'SetSpeedMultiplier', '2');
            }, 300);
        }
        resetSidePanel();
        startPlayTimer();
        unityInstance.SendMessage('GameManager', 'StartGame');
        _startSpawnWatchdog();
        resumeWebGLAudio();
        setTimeout(function() {
            try { unityInstance.SendMessage('SoundMgr', 'ForceRestartBGM'); } catch(e) {}
        }, 200);
        setTimeout(function() { showHUD(true); }, 200);
    }
```

변경 핵심: `_showInterstitialThen(function() { ... })` 래핑을 제거하고 콜백 내용을 평탄화(flatten). `pauseAudioForAd`/`resumeAudioAfterAd` 쌍도 불필요해져 제거.

- [ ] **Step 2: Android patch-index.sh에서 재시작 광고 오버라이드 불필요 확인**

`android-wrapper/patch-index.sh`의 `_showInterstitialThen` 오버라이드는 재시작 경로에서 더 이상 호출되지 않으므로 영향 없음. 단, Android에서도 동일하게 광고 없이 재시작되는지 확인.

- patch-index.sh의 기존 `_showInterstitialThen` 오버라이드는 유지 (다른 경로에서 사용 가능).

- [ ] **Step 3: 커밋**

```bash
git add Assets/WebGLTemplates/AnimalPop/index.html
git commit -m "feat: 게임 재시작 시 전면광고 제거"
```

---

### Task 2: 하단 UI 요소 상단 이동

**Files:**
- Modify: `Assets/WebGLTemplates/AnimalPop/index.html` (CSS 섹션)

현재 하단에 배치된 요소들:
- `#dex-bar` (동물 도감): `bottom: calc(var(--safe-bottom) + 80px)`
- `#settings-fab` (설정 버튼): `bottom: calc(var(--safe-bottom) + 20px)`
- `#hud-timer` (타이머): `bottom: calc(var(--safe-bottom) + 20px)`

이들을 화면 상단으로 이동한다. 배너 광고(향후 추가 시)는 하단에 유지하기 위해 하단 공간을 비운다.

**레이아웃 계획:**
```
┌─────────────────────────────────┐
│  Safe Top                       │
│  +56px: #game-hud (점수/다음)    │
│  +아래: speed-boost-btn          │
│  +아래: #danger-banner (조건부)   │
│                                 │
│       GAME CANVAS               │
│                                 │
│  ⚙️ (설정) ─── 도감 바 ───  🕐   │  ← 상단이 아닌 컨테이너 위쪽으로
│  (배너 광고 공간 확보)            │
└─────────────────────────────────┘
```

게임 HUD 바로 아래에 도감/설정/타이머를 배치하면 게임 캔버스와 겹칠 수 있으므로, **컨테이너 상단 영역**(game-hud 바로 아래)으로 이동한다.

- [ ] **Step 1: `#dex-bar` CSS를 bottom → top 기준으로 변경**

`Assets/WebGLTemplates/AnimalPop/index.html`의 `#dex-bar` CSS (약 라인 497-514):

기존:
```css
#dex-bar {
    position: fixed;
    bottom: calc(var(--safe-bottom) + 80px);
    left: 50%;
    transform: translateX(-50%) translateY(8px);
    ...
}
```

변경:
```css
#dex-bar {
    position: fixed;
    top: calc(var(--safe-top) + 140px);
    left: 50%;
    transform: translateX(-50%) translateY(8px);
    ...
}
```

`#dex-bar.visible`:
```css
#dex-bar.visible {
    opacity: 1;
    transform: translateX(-50%) translateY(0);
    pointer-events: auto;
}
```
(변경 없음)

- [ ] **Step 2: `#settings-fab` CSS를 bottom → top 기준으로 변경**

기존 (약 라인 479-492):
```css
#settings-fab {
    position: fixed;
    bottom: calc(var(--safe-bottom) + 20px);
    right: 20px;
    ...
}
```

변경:
```css
#settings-fab {
    position: fixed;
    top: calc(var(--safe-top) + 142px);
    right: 20px;
    ...
}
```

- [ ] **Step 3: `#hud-timer` (`.hud-timer-box`) CSS를 bottom → top 기준으로 변경**

기존 (약 라인 316-340):
```css
.hud-timer-box {
    position: fixed;
    bottom: calc(var(--safe-bottom) + 20px);
    left: 50%;
    transform: translate(-50%, 12px);
    ...
}
```

변경:
```css
.hud-timer-box {
    position: fixed;
    top: calc(var(--safe-top) + 142px);
    left: 50%;
    transform: translate(-50%, 12px);
    ...
}
```

`.hud-timer-box.visible`:
```css
.hud-timer-box.visible {
    opacity: 1;
    transform: translate(-50%, 0);
    pointer-events: auto;
}
```
(변경 없음)

- [ ] **Step 4: 모바일 미디어쿼리 업데이트**

`@media (max-width: 480px)` 블록 (약 라인 563) 내의 `#dex-bar` 규칙도 bottom → top으로 변경:

기존:
```css
@media (max-width: 480px) {
    #dex-bar { bottom: calc(var(--safe-bottom) + 80px); padding: 6px 8px; gap: 4px; }
}
```

변경:
```css
@media (max-width: 480px) {
    #dex-bar { top: calc(var(--safe-top) + 140px); padding: 6px 8px; gap: 4px; }
}
```

- [ ] **Step 5: 커밋**

```bash
git add Assets/WebGLTemplates/AnimalPop/index.html
git commit -m "feat: 도감/설정/타이머를 하단에서 상단으로 이동"
```

---

### Task 3: TossBridge.jslib에 리더보드 열기 함수 추가

**Files:**
- Modify: `Assets/Plugins/TossBridge.jslib:370-376`

현재 `TossSubmitLeaderboardScore`는 있지만 `openGameCenterLeaderboard` 브릿지는 없다.

- [ ] **Step 1: `TossOpenLeaderboard` 함수 추가**

`Assets/Plugins/TossBridge.jslib`의 `TossSubmitLeaderboardScore` 함수 뒤 (라인 376 이후)에 추가:

```javascript
  TossOpenLeaderboard: function() {
    if (window.AppsInToss && typeof window.AppsInToss.openGameCenterLeaderboard === 'function') {
      window.AppsInToss.openGameCenterLeaderboard().catch(function(e) {
        console.error('[TossBridge] OpenLeaderboard Failed:', e);
      });
    } else {
      console.warn('[TossBridge] openGameCenterLeaderboard not supported');
    }
  },
```

- [ ] **Step 2: `TossSubmitLeaderboardScore` 호출에 score를 문자열로 변환**

현재 코드 (라인 370-376):
```javascript
  TossSubmitLeaderboardScore: function(score) {
    if (window.AppsInToss && typeof window.AppsInToss.submitGameCenterLeaderBoardScore === 'function') {
      window.AppsInToss.submitGameCenterLeaderBoardScore({ score: score }).catch(function(e) {
        console.error('[TossBridge] SubmitScore Failed:', e);
      });
    }
  },
```

SDK 문서에 따르면 score는 문자열이어야 함. 변경:
```javascript
  TossSubmitLeaderboardScore: function(score) {
    if (window.AppsInToss && typeof window.AppsInToss.submitGameCenterLeaderBoardScore === 'function') {
      window.AppsInToss.submitGameCenterLeaderBoardScore({ score: String(score) }).catch(function(e) {
        console.error('[TossBridge] SubmitScore Failed:', e);
      });
    }
  },
```

- [ ] **Step 3: 커밋**

```bash
git add Assets/Plugins/TossBridge.jslib
git commit -m "feat: TossBridge에 리더보드 열기 + 점수 문자열 변환 추가"
```

---

### Task 4: BridgeMgr.cs에 리더보드 C# 메서드 추가

**Files:**
- Modify: `Assets/_Project/Scripts/Managers/BridgeMgr.cs`

- [ ] **Step 1: DllImport 선언 추가**

BridgeMgr.cs의 기존 DllImport 블록에 추가:

```csharp
[DllImport("__Internal")] private static extern void TossOpenLeaderboard();
```

- [ ] **Step 2: `OpenLeaderboard()` 공개 메서드 추가**

```csharp
public void OpenLeaderboard()
{
#if UNITY_WEBGL && !UNITY_EDITOR
    TossOpenLeaderboard();
#endif
}
```

- [ ] **Step 3: `SubmitLeaderboardScore()` 호출을 게임오버에서 자동 실행되도록 확인**

현재 `SubmitLeaderboardScore(int s)`가 존재하는지 확인하고, 게임오버 시 호출되는 플로우가 있는지 검증. 만약 C# 측에서 호출하지 않고 JS 측에서 직접 호출한다면 JS에서 처리 (Task 5에서 다룸).

- [ ] **Step 4: 커밋**

```bash
git add Assets/_Project/Scripts/Managers/BridgeMgr.cs
git commit -m "feat: BridgeMgr에 OpenLeaderboard 메서드 추가"
```

---

### Task 5: index.html에 리더보드 UI 및 점수 제출 연동

**Files:**
- Modify: `Assets/WebGLTemplates/AnimalPop/index.html`

#### 5-A: 랜딩 화면에 리더보드 버튼 추가

- [ ] **Step 1: 랜딩 HTML에 리더보드 버튼 삽입**

`Assets/WebGLTemplates/AnimalPop/index.html` 라인 816 (`<button class="lg-start-btn"...>`) 바로 아래에 추가:

기존:
```html
            <button class="lg-start-btn" onclick="onHtmlStartClicked()">게임 시작</button>
            <div class="lg-howto">동물을 떨어뜨려 합쳐보세요 · 최대 레벨까지 도전!</div>
```

변경:
```html
            <button class="lg-start-btn" onclick="onHtmlStartClicked()">게임 시작</button>
            <button class="lg-leaderboard-btn" id="lg-leaderboard-btn" onclick="onOpenLeaderboard()">🏆 리더보드</button>
            <div class="lg-howto">동물을 떨어뜨려 합쳐보세요 · 최대 레벨까지 도전!</div>
```

- [ ] **Step 2: 리더보드 버튼 CSS 추가**

CSS 섹션의 `.lg-start-btn` 스타일 근처에 추가:

```css
.lg-leaderboard-btn {
    width: 100%;
    padding: 14px 0;
    border: 1.5px solid var(--toss-blue);
    border-radius: var(--radius-md);
    background: rgba(49,130,246,0.06);
    color: var(--toss-blue);
    font-size: 16px;
    font-weight: 700;
    cursor: pointer;
    transition: background .15s, transform .1s;
    margin-top: 8px;
}
.lg-leaderboard-btn:active {
    background: rgba(49,130,246,0.15);
    transform: scale(0.97);
}
```

#### 5-B: 게임오버 화면에 리더보드 버튼 추가

- [ ] **Step 3: 게임오버 버튼 스택에 리더보드 버튼 삽입**

`Assets/WebGLTemplates/AnimalPop/index.html` 라인 922 (공유 버튼) 뒤에 추가:

기존:
```html
                <button class="go-btn go-btn-ghost" onclick="onShareClicked()">📤 결과 공유하기</button>
            </div>
```

변경:
```html
                <button class="go-btn go-btn-ghost" onclick="onShareClicked()">📤 결과 공유하기</button>
                <button class="go-btn go-btn-ghost" id="go-leaderboard-btn" onclick="onOpenLeaderboard()">🏆 리더보드</button>
            </div>
```

#### 5-C: JS 함수 추가

- [ ] **Step 4: `onOpenLeaderboard()` 함수 추가**

JS 섹션 (설정 패널 코드 근처)에 추가:

```javascript
    // ════════════════════════════════════════════════════
    //  리더보드
    // ════════════════════════════════════════════════════
    function onOpenLeaderboard() {
        if (window.AppsInToss && typeof window.AppsInToss.openGameCenterLeaderboard === 'function') {
            window.AppsInToss.openGameCenterLeaderboard().catch(function(e) {
                console.error('[Leaderboard] open failed:', e);
            });
        } else {
            console.warn('[Leaderboard] Not supported on this platform');
        }
    }
```

- [ ] **Step 5: 게임오버 시 자동 점수 제출 추가**

`showGameOverFromUnity` 함수 (라인 1824 부근, Analytics 호출 뒤)에 리더보드 점수 제출 추가:

기존 (라인 1820-1824):
```javascript
        try {
            if (window.AppsInToss && window.AppsInToss.Analytics) {
                window.AppsInToss.Analytics.click({ log_name: 'game_over', score: scoreNum, best: bestNum, is_new_best: isNewBest ? 1 : 0, merge_count: _mergeCount });
            }
        } catch(e) {}
```

바로 뒤에 추가:
```javascript
        // 리더보드 점수 제출 (앱인토스 전용)
        try {
            if (window.AppsInToss && typeof window.AppsInToss.submitGameCenterLeaderBoardScore === 'function') {
                window.AppsInToss.submitGameCenterLeaderBoardScore({ score: String(scoreNum) }).catch(function(e) {
                    console.error('[Leaderboard] score submit failed:', e);
                });
            }
        } catch(e) {}
```

#### 5-D: Android에서 리더보드 버튼 숨기기

- [ ] **Step 6: Android 리더보드 버튼 숨기기는 patch-index.sh에서 처리 (Task 6)**

이 단계에서는 index.html만 수정. Android 패치는 Task 6에서 별도 처리.

- [ ] **Step 7: 커밋**

```bash
git add Assets/WebGLTemplates/AnimalPop/index.html
git commit -m "feat: 랜딩/게임오버에 리더보드 버튼 + 자동 점수 제출 추가"
```

---

### Task 6: Android patch-index.sh에 리더보드 버튼 숨김 패치 추가

**Files:**
- Modify: `android-wrapper/patch-index.sh`

리더보드는 앱인토스 전용이므로 Android WebView에서는 버튼을 숨겨야 한다.

- [ ] **Step 1: patch-index.sh에 리더보드 숨기기 패치 추가**

`android-wrapper/patch-index.sh` 끝부분 (`echo "[PATCH] 6/6 localStorage 키 통일"` 뒤)에 추가:

```bash
# ── 7. 리더보드 버튼 숨기기 (앱인토스 전용 기능) ──
sed -i '' 's|id="lg-leaderboard-btn"|id="lg-leaderboard-btn" style="display:none"|g' "$INDEX"
sed -i '' 's|id="go-leaderboard-btn"|id="go-leaderboard-btn" style="display:none"|g' "$INDEX"
echo "[PATCH] 7/7 리더보드 버튼 숨기기"
```

- [ ] **Step 2: 패치 카운트 업데이트**

기존 echo 문들의 번호를 `1/7` ~ `6/7`로 업데이트 (또는 그대로 두고 7/7만 추가해도 기능상 문제없음).

- [ ] **Step 3: 최종 완료 메시지 업데이트 불필요 (기존 "✅ Android 패치 완료!" 유지)**

- [ ] **Step 4: 커밋**

```bash
git add android-wrapper/patch-index.sh
git commit -m "feat: Android에서 리더보드 버튼 숨기기 패치 추가"
```

---

## 전체 변경 요약

| 기능 | 파일 | 변경 내용 |
|------|------|----------|
| 재시작 광고 제거 | index.html | `restartGame()`에서 `_showInterstitialThen` 호출 제거 |
| UI 위치 조정 | index.html (CSS) | `#dex-bar`, `#settings-fab`, `.hud-timer-box`를 bottom→top |
| 리더보드 (jslib) | TossBridge.jslib | `TossOpenLeaderboard` 추가, score 문자열 변환 |
| 리더보드 (C#) | BridgeMgr.cs | `OpenLeaderboard()` 메서드 추가 |
| 리더보드 (UI) | index.html | 랜딩/게임오버에 🏆 버튼 + 자동 점수 제출 |
| 리더보드 (Android) | patch-index.sh | 리더보드 버튼 `display:none` 패치 |

## 플랫폼별 동작 확인

| 동작 | 앱인토스 (Toss) | Android (Play Store) |
|------|-----------------|---------------------|
| 재시작 광고 | 제거됨 | 제거됨 (동일) |
| UI 위치 | 상단 이동 | 상단 이동 (동일) |
| 리더보드 버튼 | 표시 | 숨김 (patch-index.sh) |
| 점수 제출 | 자동 (게임오버 시) | 미실행 (SDK 없음, 무시됨) |
