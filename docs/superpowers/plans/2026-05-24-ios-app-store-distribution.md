# iOS App Store Distribution Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Ship Animal Pop to the Apple App Store by adding a native Swift `WKWebView` wrapper (`ios-wrapper/`) that loads the existing Unity WebGL build, mirroring `android-wrapper/`.

**Architecture:** A native iOS app hosts one `WKWebView`. An embedded GCDWebServer serves the Brotli WebGL build over loopback HTTP (correct MIME + `Content-Encoding: br`). JS↔native bridge via `window.webkit.messageHandlers.iosBridge`. Native modules: AdMob (ads), StoreKit 2 (IAP), GameKit (Game Center). The shared `GameBridge` in the WebGL template gains an `ios` branch; Toss/Android paths stay unchanged.

**Tech Stack:** Swift 5, WKWebView, GCDWebServer (CocoaPods), Google-Mobile-Ads-SDK (CocoaPods), StoreKit 2, GameKit, XcodeGen (project generation), Xcode (build/sign — user-installed).

**Verification model:** No unit-test harness exists (matches `android-wrapper`). Verification = `xcodegen generate` succeeds, `pod install` succeeds, `node --check` on the patched index.html JS, and a runtime checklist on simulator/device. Xcode-dependent steps (build/archive/submit) are marked **[USER ACTION]**.

---

## File Structure

```
ios-wrapper/
  project.yml                          # XcodeGen spec → generates AnimalPop.xcodeproj
  Podfile                              # GoogleMobileAds, GCDWebServer
  README.md                            # build + release steps, IDs to replace
  AnimalPop/
    Info.plist                         # GADApplicationIdentifier, ATT, orientation
    AppDelegate.swift                  # entry, AdMob init, GameCenter auth
    SceneDelegate.swift                # window → GameViewController
    GameViewController.swift           # WKWebView host, fullscreen, audio lifecycle
    IosBridge.swift                    # WKScriptMessageHandler (JS → native dispatch)
    LocalWebServer.swift               # GCDWebServer: Brotli + MIME + range
    AdManager.swift                    # interstitial / rewarded / banner
    StoreManager.swift                 # StoreKit 2 (remove_ads_hint_pack)
    GameCenterManager.swift            # GameKit auth / submit / leaderboard UI
    Assets.xcassets/                   # app icon (from android mipmap)
    web/                               # build.sh copies Unity Build/ here (gitignored)
Assets/WebGLTemplates/AnimalPop/index.html   # GameBridge: add ios branch
build.sh                              # add `ios` target
.gitignore                            # ignore ios-wrapper/AnimalPop/web, Pods, *.xcodeproj
```

---

## Task 1: Scaffold ios-wrapper (XcodeGen + Podfile + Info.plist)

**Files:**
- Create: `ios-wrapper/project.yml`
- Create: `ios-wrapper/Podfile`
- Create: `ios-wrapper/AnimalPop/Info.plist`
- Modify: `.gitignore`

- [ ] **Step 1: Install XcodeGen**

Run: `brew install xcodegen`
Expected: `xcodegen` on PATH (`which xcodegen`).

- [ ] **Step 2: Write `ios-wrapper/project.yml`**

```yaml
name: AnimalPop
options:
  bundleIdPrefix: com.animalpop
  deploymentTarget:
    iOS: "15.0"
settings:
  base:
    MARKETING_VERSION: "1.2.1"
    CURRENT_PROJECT_VERSION: "15"
    DEVELOPMENT_TEAM: ""        # [USER ACTION] set Apple Team ID
    PRODUCT_BUNDLE_IDENTIFIER: com.animalpop.app
targets:
  AnimalPop:
    type: application
    platform: iOS
    sources: [AnimalPop]
    settings:
      base:
        INFOPLIST_FILE: AnimalPop/Info.plist
        ENABLE_BITCODE: NO
        TARGETED_DEVICE_FAMILY: "1,2"
        ASSETCATALOG_COMPILER_APPICON_NAME: AppIcon
    entitlements:
      path: AnimalPop/AnimalPop.entitlements
      properties:
        com.apple.developer.game-center: true
```

- [ ] **Step 3: Write `ios-wrapper/Podfile`**

```ruby
platform :ios, '15.0'
target 'AnimalPop' do
  use_frameworks!
  pod 'Google-Mobile-Ads-SDK'
  pod 'GCDWebServer', '~> 3.0'
end
```

- [ ] **Step 4: Write `ios-wrapper/AnimalPop/Info.plist`**

Include keys: `GADApplicationIdentifier` = iOS AdMob app ID **[USER ACTION placeholder]**,
`NSUserTrackingUsageDescription` (ATT prompt text), `UIRequiresFullScreen` = true,
`UISupportedInterfaceOrientations` = portrait only, `UIStatusBarHidden` = true,
`SKAdNetworkItems` (AdMob network IDs), `NSAppTransportSecurity` allow loopback.
(Full plist authored during execution.)

- [ ] **Step 5: Add `.gitignore` entries**

Append:
```
ios-wrapper/AnimalPop/web/
ios-wrapper/Pods/
ios-wrapper/AnimalPop.xcodeproj/
ios-wrapper/AnimalPop.xcworkspace/
ios-wrapper/Podfile.lock
```

- [ ] **Step 6: Verify generation**

Run: `cd ios-wrapper && xcodegen generate`
Expected: `Created project at .../AnimalPop.xcodeproj` (Swift files added in later tasks; placeholder empty sources dir OK — create `AnimalPop/.gitkeep` if needed so xcodegen finds the folder).

- [ ] **Step 7: Commit**

```bash
git add ios-wrapper/project.yml ios-wrapper/Podfile ios-wrapper/AnimalPop/Info.plist .gitignore
git commit -m "ios: scaffold ios-wrapper (xcodegen + podfile + info.plist)"
```

---

## Task 2: LocalWebServer.swift (Brotli WebGL serving)

**Files:**
- Create: `ios-wrapper/AnimalPop/LocalWebServer.swift`

- [ ] **Step 1: Implement GCDWebServer wrapper**

Responsibilities:
- Serve `web/` (bundled WebGL `Build/` + `index.html` + `sprites/` + `TemplateData/`)
- For `*.br` requests: set `Content-Encoding: br` and the de-suffixed MIME
  (`.js.br`→`application/javascript`, `.wasm.br`→`application/wasm`,
  `.data.br`→`application/octet-stream`, `.json.br`→`application/json`)
- Support HTTP range requests (GCDWebServer file responder handles this)
- Bind `127.0.0.1`, random free port; expose `var baseURL: URL`

```swift
import Foundation
import GCDWebServer

final class LocalWebServer {
    private let server = GCDWebServer()
    private let webRoot: URL
    private(set) var baseURL: URL?

    init() {
        webRoot = Bundle.main.bundleURL.appendingPathComponent("web")
    }

    func start() {
        server.addGETHandler(forBasePath: "/",
                             directoryPath: webRoot.path,
                             indexFilename: "index.html",
                             cacheAge: 0,
                             allowRangeRequests: true)
        // Brotli: wrap responses for .br files with correct headers
        server.addHandler(match: { method, url, headers, path, query in
            guard method == "GET", path.hasSuffix(".br") else { return nil }
            return GCDWebServerRequest(method: method, url: url, headers: headers,
                                       path: path, query: query)
        }, processBlock: { [weak self] request in
            guard let self = self else { return GCDWebServerResponse(statusCode: 500) }
            let filePath = self.webRoot.appendingPathComponent(request.path).path
            guard let resp = GCDWebServerFileResponse(file: filePath, byteRange: request.byteRange) else {
                return GCDWebServerResponse(statusCode: 404)
            }
            resp.setValue("br", forAdditionalHeader: "Content-Encoding")
            let p = request.path
            if p.hasSuffix(".js.br") { resp.contentType = "application/javascript" }
            else if p.hasSuffix(".wasm.br") { resp.contentType = "application/wasm" }
            else if p.hasSuffix(".json.br") { resp.contentType = "application/json" }
            else { resp.contentType = "application/octet-stream" }
            return resp
        })
        try? server.start(options: [
            GCDWebServerOption_BindToLocalhost: true,
            GCDWebServerOption_Port: 0,
            GCDWebServerOption_AutomaticallySuspendInBackground: false
        ])
        baseURL = server.serverURL
    }

    func stop() { server.stop() }
}
```

- [ ] **Step 2: Verify (deferred to Task 8 build)**

No standalone test (needs Xcode). Compile verified in Task 8 build step.

- [ ] **Step 3: Commit**

```bash
git add ios-wrapper/AnimalPop/LocalWebServer.swift
git commit -m "ios: local web server (brotli + range serving for WebGL)"
```

---

## Task 3: GameViewController.swift (WKWebView host)

**Files:**
- Create: `ios-wrapper/AnimalPop/GameViewController.swift`

- [ ] **Step 1: Implement WKWebView host**

Responsibilities:
- Create `WKWebViewConfiguration`; register `IosBridge` as `WKScriptMessageHandler`
  named `iosBridge` on `userContentController`
- Full-screen webView pinned to view bounds (banner inset at bottom safe area)
- `UIApplication.shared.isIdleTimerDisabled = true`
- Load `localServer.baseURL/index.html`
- Background/foreground: suspend/resume Web Audio via `evaluateJavaScript`
  (mirror Android onPause/onResume):
  `Module.WEBAudio.audioContext.suspend()/.resume()`
- Expose `func callJS(_ js: String)` → `webView.evaluateJavaScript(js)`
- Own instances: `AdManager`, `StoreManager`, `GameCenterManager`, `LocalWebServer`
- Wire bridge → managers (Task 4 dispatch)

(Full implementation authored during execution; key interface:
`func callJS(_ js: String)`, `var webView: WKWebView`.)

- [ ] **Step 2: Commit**

```bash
git add ios-wrapper/AnimalPop/GameViewController.swift
git commit -m "ios: WKWebView host with fullscreen + audio lifecycle"
```

---

## Task 4: IosBridge.swift (JS → native dispatch)

**Files:**
- Create: `ios-wrapper/AnimalPop/IosBridge.swift`

- [ ] **Step 1: Implement WKScriptMessageHandler**

Receives `{ action, ... }` dicts and dispatches to managers on main thread:

| action | → |
|--------|---|
| `showInterstitialAd` | `adManager.showInterstitial { callJS("onInterstitialClosedFromIOS()") }` |
| `showRewardedAd` | `adManager.showRewarded(onReward:onFail:)` → `onAdRewardedFromIOS()` / `onAdFailedFromIOS()` |
| `launchPurchase` | `storeManager.purchase(productId)` |
| `restorePurchases` | `storeManager.restore()` |
| `submitScore` | `gameCenter.submit(score)` |
| `showLeaderboard` | `gameCenter.presentLeaderboard(from: vc)` |
| `shareText` | present `UIActivityViewController` |
| `log` | `print("[JS] \(message)")` |

Holds weak refs to `GameViewController` (for `callJS` + presenting) and the three managers.
Callbacks all routed through `gameVC?.callJS(...)`.

- [ ] **Step 2: Verify callback name parity with Android**

Confirm names match the spec §4.2 table exactly:
`onInterstitialClosedFromIOS`, `onAdRewardedFromIOS`, `onAdFailedFromIOS`,
`onRewardedAdReadyFromIOS`, `onPurchaseSuccessFromIOS`, `onPurchaseFailedFromIOS`,
`onPurchaseCancelledFromIOS`, `onPurchaseRestoredFromIOS`.

- [ ] **Step 3: Commit**

```bash
git add ios-wrapper/AnimalPop/IosBridge.swift
git commit -m "ios: JS<->native bridge (WKScriptMessageHandler dispatch)"
```

---

## Task 5: AdManager.swift (AdMob iOS)

**Files:**
- Create: `ios-wrapper/AnimalPop/AdManager.swift`

- [ ] **Step 1: Implement**

- Test ad unit IDs as constants with a `// [USER ACTION] replace with real iOS IDs`
  marker. Use Google test IDs by default:
  - interstitial `ca-app-pub-3940256099942544/4411468910`
  - rewarded `ca-app-pub-3940256099942544/1712485313`
  - banner `ca-app-pub-3940256099942544/2934735716`
- `preload()`: load interstitial + rewarded; on rewarded loaded → `callJS("onRewardedAdReadyFromIOS()")`
- `showInterstitial(onClose:)`, `showRewarded(onReward:onFail:)`
- banner: `GADBannerView` added to bottom by GameViewController
- retry with backoff on load failure (mirror Android `loadRewardedAd` retry)

- [ ] **Step 2: Commit**

```bash
git add ios-wrapper/AnimalPop/AdManager.swift
git commit -m "ios: AdMob manager (interstitial/rewarded/banner)"
```

---

## Task 6: StoreManager.swift (StoreKit 2)

**Files:**
- Create: `ios-wrapper/AnimalPop/StoreManager.swift`

- [ ] **Step 1: Implement**

- `static let productId = "remove_ads_hint_pack"`
- `purchase(_ id:)` → `Product.products(for:)` → `product.purchase()`; on `.success`
  verify + `transaction.finish()` → `callJS("onPurchaseSuccessFromIOS('id','token')")`;
  `.userCancelled` → `onPurchaseCancelledFromIOS`; failure → `onPurchaseFailedFromIOS`
- `restore()` → iterate `Transaction.currentEntitlements` → `onPurchaseRestoredFromIOS`
- `observeTransactions()` task on launch (handle interrupted/Ask-to-Buy)

- [ ] **Step 2: Commit**

```bash
git add ios-wrapper/AnimalPop/StoreManager.swift
git commit -m "ios: StoreKit 2 IAP (remove_ads_hint_pack + restore)"
```

---

## Task 7: GameCenterManager.swift (GameKit)

**Files:**
- Create: `ios-wrapper/AnimalPop/GameCenterManager.swift`

- [ ] **Step 1: Implement**

- `authenticate()`: `GKLocalPlayer.local.authenticateHandler = { vc, err in ... }`
  (present auth VC if provided)
- `static let leaderboardId = "animalpop_highscore"  // [USER ACTION] match ASC`
- `submit(_ score: Int)` → `GKLeaderboard.submitScore(score, context:0, player:.local, leaderboardIDs:[leaderboardId])`
- `presentLeaderboard(from vc:)` → `GKGameCenterViewController(leaderboardID:..., playerScope:.global, timeScope:.allTime)`

- [ ] **Step 2: Commit**

```bash
git add ios-wrapper/AnimalPop/GameCenterManager.swift
git commit -m "ios: Game Center auth + score submit + leaderboard UI"
```

---

## Task 8: AppDelegate / SceneDelegate wiring + first build

**Files:**
- Create: `ios-wrapper/AnimalPop/AppDelegate.swift`
- Create: `ios-wrapper/AnimalPop/SceneDelegate.swift`
- Create: `ios-wrapper/AnimalPop/AnimalPop.entitlements`
- Create: `ios-wrapper/AnimalPop/Assets.xcassets/AppIcon.appiconset/` (icon from android mipmap)

- [ ] **Step 1: AppDelegate** — `GADMobileAds.sharedInstance().start()`, request ATT
  after launch, `GameCenterManager.shared.authenticate()`.

- [ ] **Step 2: SceneDelegate** — set `window.rootViewController = GameViewController()`.

- [ ] **Step 3: App icon** — copy `android-wrapper/.../mipmap-xxxhdpi/ic_launcher.png`,
  resize to required iOS icon sizes (1024 marketing + app sizes) into the appiconset.

- [ ] **Step 4: Generate + install pods**

Run:
```bash
cd ios-wrapper && xcodegen generate && pod install
```
Expected: `AnimalPop.xcworkspace` created, pods integrated.

- [ ] **Step 5: [USER ACTION] Build in Xcode**

Open `ios-wrapper/AnimalPop.xcworkspace`, set Development Team, build to simulator.
Expected: compiles, app launches (web/ empty until Task 9–10 build — expect blank until
`build.sh ios` copies the WebGL build).

- [ ] **Step 6: Commit**

```bash
git add ios-wrapper/AnimalPop/AppDelegate.swift ios-wrapper/AnimalPop/SceneDelegate.swift ios-wrapper/AnimalPop/AnimalPop.entitlements ios-wrapper/AnimalPop/Assets.xcassets
git commit -m "ios: app/scene delegate wiring + icon + entitlements"
```

---

## Task 9: GameBridge ios branch (template index.html)

**Files:**
- Modify: `Assets/WebGLTemplates/AnimalPop/index.html` (GameBridge block ~line 1058–1180)

- [ ] **Step 1: Platform detection**

Change:
```js
window.AP_PLATFORM = window.AndroidBridge ? 'android' : 'toss';
```
to:
```js
window.AP_PLATFORM = window.AndroidBridge ? 'android'
  : (window.webkit && window.webkit.messageHandlers && window.webkit.messageHandlers.iosBridge ? 'ios' : 'toss');
window._iosCall = function(action, payload) {
  try { window.webkit.messageHandlers.iosBridge.postMessage(Object.assign({action:action}, payload||{})); }
  catch(e){ console.warn('[iosBridge] '+e); }
};
```

- [ ] **Step 2: Add iOS callbacks + readiness cache**

Alongside the `*FromAndroid` callbacks, add:
```js
window._iosRewardedReady = false;
window.onRewardedAdReadyFromIOS = function(){ window._iosRewardedReady = true; };
window.onAdRewardedFromIOS = function(){ if(window._onRewardDone){window._onRewardDone();window._onRewardDone=null;} };
window.onAdFailedFromIOS = function(){ if(window._onRewardFail){window._onRewardFail();window._onRewardFail=null;} };
window.onInterstitialClosedFromIOS = function(){ if(window._onInterstitialDone){window._onInterstitialDone();window._onInterstitialDone=null;} };
window.onPurchaseSuccessFromIOS = function(id,t){ window.onPurchaseSuccessFromAndroid && window.onPurchaseSuccessFromAndroid(id,t); };
window.onPurchaseFailedFromIOS = function(id,c){ window.onPurchaseFailedFromAndroid && window.onPurchaseFailedFromAndroid(id,c); };
window.onPurchaseCancelledFromIOS = function(id){ window.onPurchaseCancelledFromAndroid && window.onPurchaseCancelledFromAndroid(id); };
window.onPurchaseRestoredFromIOS = function(id,t){ window.onPurchaseRestoredFromAndroid && window.onPurchaseRestoredFromAndroid(id,t); };
```
(Reuse existing Android handler bodies to avoid duplicating grant logic.)

- [ ] **Step 3: Route GameBridge methods for ios**

In `showRewardedAd`, `showInterstitialAd`, `launchPurchase`, `share`, leaderboard,
and score-submit paths, add `else if (P === 'ios') { window._iosCall(...) }` branches:
- `showRewardedAd`: set `_onRewardDone`/`_onRewardFail`, `_iosCall('showRewardedAd')`
- `showInterstitialAd`: set `_onInterstitialDone`, `_iosCall('showInterstitialAd')`
- `launchPurchase`: `_iosCall('launchPurchase',{productId:productId})`
- `share`: `_iosCall('shareText',{text:text})`
- leaderboard button (`onOpenLeaderboard`): if ios `_iosCall('showLeaderboard')`
- high-score/gameover hook: if ios `_iosCall('submitScore',{score:s})`
- rewarded readiness check: ios reads `window._iosRewardedReady`

- [ ] **Step 4: Verify JS syntax**

Run: `node --check <(sed -n '/<script>/,/<\/script>/p' Assets/WebGLTemplates/AnimalPop/index.html)` is unreliable for mixed HTML;
instead extract the GameBridge `<script>` block to a temp `.js` and run `node --check tmp.js`.
Expected: no syntax errors.

- [ ] **Step 5: Verify no regression to toss/android**

Confirm `P === 'toss'` and `P === 'android'` branches are untouched (only `else if` added).

- [ ] **Step 6: Commit**

```bash
git add Assets/WebGLTemplates/AnimalPop/index.html
git commit -m "webgl: GameBridge ios branch (ads/iap/share/leaderboard/score)"
```

---

## Task 10: build.sh `ios` target

**Files:**
- Modify: `build.sh` (add `unity_build_ios`, `build_ios`, `ios)` case, usage text)

- [ ] **Step 1: Add functions**

```bash
IOS_DIR="$PROJECT_DIR/ios-wrapper"
IOS_WEB="$IOS_DIR/AnimalPop/web"

unity_build_ios() {
    # Reuse Toss Brotli build (WebKit supports br)
    unity_build_toss   # or dedicated method target producing Build/ with Brotli
}

build_ios() {
    info "iOS 빌드 시작"
    [ "${SKIP_UNITY:-0}" = "1" ] || unity_build_ios
    rm -rf "$IOS_WEB"; mkdir -p "$IOS_WEB"
    cp -R "$WEBGL_BUILD_DIR/." "$IOS_WEB/"
    ( cd "$IOS_DIR" && xcodegen generate && pod install )
    ok "iOS web/ 동기화 완료 → Xcode에서 ios-wrapper/AnimalPop.xcworkspace 열어 아카이브"
}
```

- [ ] **Step 2: Add case + usage**

```bash
    ios)
        build_ios
        ;;
```
Add to usage: `echo "  ios           iOS WKWebView 래퍼 (Unity Brotli + xcodegen/pod)"`

- [ ] **Step 3: Verify (dry, no Unity)**

Run: `SKIP_UNITY=1 ./build.sh ios` — verify it syncs `web/` from an existing build
and runs xcodegen/pod (skip if no prior WebGL build; just confirm script parses:
`bash -n build.sh`).
Expected: `bash -n build.sh` exits 0.

- [ ] **Step 4: Commit**

```bash
git add build.sh
git commit -m "build: add ios target (sync WebGL + xcodegen/pod)"
```

---

## Task 11: ios-wrapper README + release checklist

**Files:**
- Create: `ios-wrapper/README.md`
- Modify: `PRE_REVIEW_CHECKLIST.md` (add iOS section)

- [ ] **Step 1: README** — build steps (`./build.sh ios`), IDs to replace
  (AdMob iOS app/ad-unit IDs, `GADApplicationIdentifier`, Game Center leaderboard ID,
  Team ID), Xcode archive/upload steps, prerequisites.

- [ ] **Step 2: PRE_REVIEW_CHECKLIST** — add iOS items: ATT string present, IAP product
  registered, Game Center configured, screenshots, privacy nutrition labels, 4.2 review
  note draft.

- [ ] **Step 3: Commit**

```bash
git add ios-wrapper/README.md PRE_REVIEW_CHECKLIST.md
git commit -m "docs: ios-wrapper README + pre-review checklist"
```

---

## Post-implementation: USER ACTIONS (cannot be automated)

1. Install **Xcode** (Mac App Store) — currently absent.
2. **Apple Developer Program** membership ($99/yr); set Team ID in `project.yml`.
3. **AdMob**: create iOS app + iOS ad unit IDs → replace test IDs in `AdManager.swift`
   + `GADApplicationIdentifier` in `Info.plist`.
4. **App Store Connect**: app record (bundle `com.animalpop.app`), IAP non-consumable
   `remove_ads_hint_pack`, Game Center leaderboard `animalpop_highscore`, privacy labels,
   screenshots (`애니멀팝_스크린샷/`).
5. Run `./build.sh ios`, open `.xcworkspace`, archive, upload, submit for review.

## Runtime verification checklist (simulator/device, after build)

- [ ] WebGL loads, gameplay smooth (near 60fps on recent device)
- [ ] Interstitial on game over; rewarded "이어하기" grants reward
- [ ] Banner shows at bottom (safe-area inset correct)
- [ ] IAP purchase (Sandbox) removes ads + grants hints; restore works
- [ ] Leaderboard button opens Game Center; score submits on high score
- [ ] Background → audio stops; foreground → audio resumes
- [ ] Toss + Android builds unchanged (regression check)
