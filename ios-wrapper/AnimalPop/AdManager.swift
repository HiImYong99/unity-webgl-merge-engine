import Foundation
import UIKit
import GoogleMobileAds

/// AdMob 광고 관리 (전면 / 보상형 / 배너).
/// Android `MainActivity`의 AdMob 로직과 동일 역할.
///
/// ★ 배포 전 교체 필수: 아래 *_AD_UNIT_ID 를 AdMob 콘솔의 **iOS** 광고 단위 ID로 교체.
///   현재 값은 Google 공식 테스트 광고 단위 ID.
///   (Android ID는 iOS에서 동작하지 않음)
final class AdManager: NSObject {

    // ── iOS 실 광고 단위 ID (AdMob 콘솔, 2026-06-21) ──────────────────
    private let interstitialAdUnitID = "ca-app-pub-4036435726138230/8071816302"
    private let rewardedAdUnitID     = "ca-app-pub-4036435726138230/5668748311"
    private let bannerAdUnitID       = "ca-app-pub-4036435726138230/7857183564"
    // ───────────────────────────────────────────────────────────────

    weak var rootViewController: UIViewController?
    /// 보상형 광고 로드 완료 시 호출 (→ JS onRewardedAdReadyFromIOS)
    var onRewardedReady: (() -> Void)?

    private var interstitial: GADInterstitialAd?
    private var rewarded: GADRewardedAd?
    private(set) var bannerView: GADBannerView?

    private var onInterstitialClose: (() -> Void)?
    private var onReward: (() -> Void)?
    private var onRewardFail: (() -> Void)?
    private var rewardEarned = false
    private var rewardedRetry = 0
    private var bannerRetry = 0

    /// UMP 동의 완료 후 호출 — SDK 시작 → 전면/보상형/배너 로드.
    func start() {
        GADMobileAds.sharedInstance().start(completionHandler: { [weak self] _ in
            self?.loadInterstitial()
            self?.loadRewarded()
            self?.loadBanner()   // SDK init 완료 후 배너 로드(이전엔 init 전 로드돼 첫 요청 실패)
        })
    }

    // ── 배너 ─────────────────────────────────────────────────────
    /// 뷰만 생성·부착 (로드는 SDK start 완료 후 loadBanner()에서).
    func makeBanner(in vc: UIViewController) -> GADBannerView {
        let banner = GADBannerView(adSize: GADAdSizeBanner)
        banner.adUnitID = bannerAdUnitID
        banner.rootViewController = vc
        banner.delegate = self
        bannerView = banner
        return banner
    }

    func loadBanner() {
        bannerRetry = 0   // 명시적 재트리거는 항상 새 재시도 허용
        bannerView?.load(GADRequest())
    }

    // ── 전면 광고 ─────────────────────────────────────────────────
    func loadInterstitial() {
        GADInterstitialAd.load(withAdUnitID: interstitialAdUnitID, request: GADRequest()) { [weak self] ad, error in
            if let error = error {
                NSLog("[AdManager] 전면 로드 실패: \(error.localizedDescription)")
                self?.interstitial = nil
                return
            }
            self?.interstitial = ad
            ad?.fullScreenContentDelegate = self
        }
    }

    func showInterstitial(onClose: @escaping () -> Void) {
        guard let interstitial = interstitial, let vc = rootViewController else {
            onClose()
            return
        }
        onInterstitialClose = onClose
        interstitial.present(fromRootViewController: vc)
    }

    // ── 보상형 광고 ───────────────────────────────────────────────
    func loadRewarded() {
        GADRewardedAd.load(withAdUnitID: rewardedAdUnitID, request: GADRequest()) { [weak self] ad, error in
            guard let self = self else { return }
            if let error = error {
                NSLog("[AdManager] 보상형 로드 실패(\(self.rewardedRetry)): \(error.localizedDescription)")
                self.rewarded = nil
                if self.rewardedRetry < 3 {
                    self.rewardedRetry += 1
                    let delay = Double(self.rewardedRetry) * 5.0
                    DispatchQueue.main.asyncAfter(deadline: .now() + delay) { self.loadRewarded() }
                }
                return
            }
            self.rewarded = ad
            self.rewardedRetry = 0
            ad?.fullScreenContentDelegate = self
            self.onRewardedReady?()
        }
    }

    var isRewardedReady: Bool { rewarded != nil }

    func showRewarded(onReward: @escaping () -> Void, onFail: @escaping () -> Void) {
        guard let rewarded = rewarded, let vc = rootViewController else {
            onFail()
            return
        }
        self.onReward = onReward
        self.onRewardFail = onFail
        rewardEarned = false
        rewarded.present(fromRootViewController: vc) { [weak self] in
            self?.rewardEarned = true
            self?.onReward?()
            self?.onReward = nil
        }
    }
}

// ── 전면/보상형 공통 콜백 ─────────────────────────────────────────
extension AdManager: GADFullScreenContentDelegate {

    func adDidDismissFullScreenContent(_ ad: GADFullScreenPresentingAd) {
        if ad is GADInterstitialAd {
            interstitial = nil
            loadInterstitial()
            onInterstitialClose?()
            onInterstitialClose = nil
        } else if ad is GADRewardedAd {
            rewarded = nil
            loadRewarded()
            if !rewardEarned {
                onRewardFail?()
            }
            // 보상 여부와 무관하게 보류 콜백 정리 (stale 강참조 방지)
            onReward = nil
            onRewardFail = nil
        }
    }

    func ad(_ ad: GADFullScreenPresentingAd, didFailToPresentFullScreenContentWithError error: Error) {
        NSLog("[AdManager] present 실패: \(error.localizedDescription)")
        if ad is GADInterstitialAd {
            interstitial = nil
            loadInterstitial()
            onInterstitialClose?()
            onInterstitialClose = nil
        } else if ad is GADRewardedAd {
            rewarded = nil
            loadRewarded()
            onRewardFail?()
            onReward = nil
            onRewardFail = nil
        }
    }
}

// ── 배너 콜백 (실패 시 백오프 재시도 — 빈 배너 영역 방지) ──────────
extension AdManager: GADBannerViewDelegate {
    func bannerViewDidReceiveAd(_ bannerView: GADBannerView) { bannerRetry = 0 }
    func bannerView(_ bannerView: GADBannerView, didFailToReceiveAdWithError error: Error) {
        NSLog("[AdManager] 배너 로드 실패(\(bannerRetry)): \(error.localizedDescription)")
        guard bannerRetry < 3 else { return }
        bannerRetry += 1
        let delay = Double(bannerRetry) * 5.0
        DispatchQueue.main.asyncAfter(deadline: .now() + delay) { [weak self] in self?.bannerView?.load(GADRequest()) }
    }
}
