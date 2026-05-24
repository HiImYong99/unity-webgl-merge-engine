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

    // ── [USER ACTION] iOS 광고 단위 ID로 교체 ──────────────────────
    private let interstitialAdUnitID = "ca-app-pub-3940256099942544/4411468910" // TEST
    private let rewardedAdUnitID     = "ca-app-pub-3940256099942544/1712485313" // TEST
    private let bannerAdUnitID       = "ca-app-pub-3940256099942544/2934735716" // TEST
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

    func start() {
        GADMobileAds.sharedInstance().start(completionHandler: { [weak self] _ in
            self?.loadInterstitial()
            self?.loadRewarded()
        })
    }

    // ── 배너 ─────────────────────────────────────────────────────
    func makeBanner(in vc: UIViewController) -> GADBannerView {
        let banner = GADBannerView(adSize: GADAdSizeBanner)
        banner.adUnitID = bannerAdUnitID
        banner.rootViewController = vc
        banner.load(GADRequest())
        bannerView = banner
        return banner
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
                onRewardFail = nil
            }
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
            onRewardFail = nil
        }
    }
}
