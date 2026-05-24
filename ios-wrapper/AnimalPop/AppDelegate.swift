import UIKit
import AppTrackingTransparency
import GoogleMobileAds

@main
final class AppDelegate: UIResponder, UIApplicationDelegate {

    private var didRequestATT = false

    func application(_ application: UIApplication,
                     didFinishLaunchingWithOptions launchOptions: [UIApplication.LaunchOptionsKey: Any]?) -> Bool {
        // AdMob 초기화는 AdManager.start()에서 (rootVC 준비 후). 여기선 no-op.
        return true
    }

    func applicationDidBecomeActive(_ application: UIApplication) {
        requestTrackingPermissionOnce()
    }

    /// ATT(App Tracking Transparency) 권한 1회 요청.
    /// 거부 시 AdMob은 자동으로 비개인화 광고로 폴백.
    private func requestTrackingPermissionOnce() {
        guard !didRequestATT else { return }
        didRequestATT = true
        if #available(iOS 14, *) {
            // 앱이 active 상태가 된 직후 요청 (Apple 요구사항)
            DispatchQueue.main.asyncAfter(deadline: .now() + 0.5) {
                ATTrackingManager.requestTrackingAuthorization { _ in }
            }
        }
    }

    // MARK: UISceneSession
    func application(_ application: UIApplication,
                     configurationForConnecting connectingSceneSession: UISceneSession,
                     options: UIScene.ConnectionOptions) -> UISceneConfiguration {
        UISceneConfiguration(name: "Default Configuration", sessionRole: connectingSceneSession.role)
    }
}
