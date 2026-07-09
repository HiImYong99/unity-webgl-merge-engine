import Foundation
import UIKit
import WebKit
import StoreKit

/// JS → 네이티브 브릿지. JS는 다음과 같이 호출:
///   window.webkit.messageHandlers.iosBridge.postMessage({ action: "...", ... })
///
/// WKWebView 메시지 핸들러는 비동기/단방향이므로, 네이티브 → JS 응답은
/// `gameVC.callJS(...)`(evaluateJavaScript)로 콜백한다.
/// 콜백 함수명은 Android(`*FromAndroid`) 대응 `*FromIOS` 규약을 따른다.
final class IosBridge: NSObject, WKScriptMessageHandler {

    static let name = "iosBridge"

    weak var gameVC: GameViewController?
    private let adManager: AdManager
    private let storeManager: Any   // StoreManager (iOS 15+); Any로 보관 후 캐스팅
    private let gameCenter: GameCenterManager

    // 햅틱 제너레이터 (매 호출 생성 비용 회피 — 병합은 고빈도)
    private let hapticLight  = UIImpactFeedbackGenerator(style: .light)
    private let hapticMedium = UIImpactFeedbackGenerator(style: .medium)
    private let hapticNotify = UINotificationFeedbackGenerator()

    init(gameVC: GameViewController, adManager: AdManager, storeManager: Any, gameCenter: GameCenterManager) {
        self.gameVC = gameVC
        self.adManager = adManager
        self.storeManager = storeManager
        self.gameCenter = gameCenter
        super.init()
        wireCallbacks()
    }

    // 매니저 → JS 콜백 배선 (Android 콜백명 대응)
    private func wireCallbacks() {
        adManager.onRewardedReady = { [weak self] in
            self?.callJS("window.onRewardedAdReadyFromIOS && onRewardedAdReadyFromIOS()")
        }
        if #available(iOS 15.0, *), let store = storeManager as? StoreManager {
            store.onSuccess  = { [weak self] id, t in self?.callJS("window.onPurchaseSuccessFromIOS && onPurchaseSuccessFromIOS(\(Self.js(id)),\(Self.js(t)))") }
            store.onFailed   = { [weak self] id, c in self?.callJS("window.onPurchaseFailedFromIOS && onPurchaseFailedFromIOS(\(Self.js(id)),\(c))") }
            store.onCancelled = { [weak self] id in self?.callJS("window.onPurchaseCancelledFromIOS && onPurchaseCancelledFromIOS(\(Self.js(id)))") }
            store.onRestored = { [weak self] id, t in self?.callJS("window.onPurchaseRestoredFromIOS && onPurchaseRestoredFromIOS(\(Self.js(id)),\(Self.js(t)))") }
            store.onNothingRestored = { [weak self] in self?.callJS("window.onNothingRestoredFromIOS && onNothingRestoredFromIOS()") }
        }
        gameCenter.onUnavailable = { [weak self] in
            self?.callJS("window.onLeaderboardUnavailableFromIOS && onLeaderboardUnavailableFromIOS()")
        }
    }

    /// 문자열을 JS 리터럴로 안전 인코딩 (따옴표/백슬래시 이스케이프) → JS 인젝션 방지
    static func js(_ s: String) -> String {
        if let data = try? JSONEncoder().encode(s), let str = String(data: data, encoding: .utf8) {
            return str   // JSON 문자열은 그대로 유효한 JS 문자열 리터럴
        }
        return "''"
    }

    private func callJS(_ js: String) {
        DispatchQueue.main.async { [weak self] in self?.gameVC?.callJS(js) }
    }

    // MARK: - WKScriptMessageHandler
    func userContentController(_ userContentController: WKUserContentController,
                               didReceive message: WKScriptMessage) {
        guard message.name == IosBridge.name else { return }
        guard let body = message.body as? [String: Any],
              let action = body["action"] as? String else { return }

        DispatchQueue.main.async { [weak self] in
            self?.handle(action: action, body: body)
        }
    }

    private func handle(action: String, body: [String: Any]) {
        switch action {
        case "showInterstitialAd":
            adManager.showInterstitial { [weak self] in
                self?.callJS("window.onInterstitialClosedFromIOS && onInterstitialClosedFromIOS()")
            }

        case "showRewardedAd":
            adManager.showRewarded(
                onReward: { [weak self] in self?.callJS("window.onAdRewardedFromIOS && onAdRewardedFromIOS()") },
                onFail:   { [weak self] in self?.callJS("window.onAdFailedFromIOS && onAdFailedFromIOS()") }
            )

        case "launchPurchase":
            let productId = (body["productId"] as? String) ?? StoreManagerIDs.defaultProduct
            if #available(iOS 15.0, *), let store = storeManager as? StoreManager {
                store.purchase(productId)
            }

        case "restorePurchases":
            if #available(iOS 15.0, *), let store = storeManager as? StoreManager {
                store.restore()
            }

        case "submitScore":
            let score = (body["score"] as? Int) ?? Int((body["score"] as? Double) ?? 0)
            gameCenter.submit(score: score)

        case "showLeaderboard":
            if let vc = gameVC { gameCenter.presentLeaderboard(from: vc) }

        case "reportAchievement":
            let id = (body["id"] as? String) ?? ""
            let percent = (body["percent"] as? Double) ?? Double((body["percent"] as? Int) ?? 0)
            if !id.isEmpty { gameCenter.reportAchievement(id: id, percent: percent) }

        case "shareText":
            let text = (body["text"] as? String) ?? ""
            let url = body["url"] as? String
            presentShare(text: text, url: url)

        case "haptic":
            playHaptic(kind: (body["kind"] as? String) ?? "light")

        case "requestReview":
            // 신기록 모먼트 리뷰 요청 — 노출 빈도는 OS가 throttle (연 3회 상한)
            if let scene = gameVC?.view.window?.windowScene {
                if #available(iOS 16.0, *) {
                    AppStore.requestReview(in: scene)
                } else {
                    SKStoreReviewController.requestReview(in: scene)
                }
            }

        case "log":
            NSLog("[JS] \((body["message"] as? String) ?? "")")

        default:
            NSLog("[IosBridge] 알 수 없는 action: \(action)")
        }
    }

    private func playHaptic(kind: String) {
        switch kind {
        case "medium":  hapticMedium.impactOccurred()
        case "success": hapticNotify.notificationOccurred(.success)
        case "warning": hapticNotify.notificationOccurred(.warning)
        case "error":   hapticNotify.notificationOccurred(.error)
        default:        hapticLight.impactOccurred()
        }
    }

    private func presentShare(text: String, url: String? = nil) {
        guard let vc = gameVC else { return }
        var items: [Any] = [text]
        if let url = url, let u = URL(string: url) { items.append(u) } // App Store 링크 첨부(바이럴)
        let av = UIActivityViewController(activityItems: items, applicationActivities: nil)
        av.popoverPresentationController?.sourceView = vc.view
        vc.present(av, animated: true)
    }
}

/// product ID 폴백 상수 (StoreManager가 iOS 15+ 가드 뒤에 있어 직접 참조 회피)
enum StoreManagerIDs {
    static let defaultProduct = "remove_ads_hint_pack"
}
