import UIKit
import GoogleMobileAds

@main
final class AppDelegate: UIResponder, UIApplicationDelegate {

    func application(_ application: UIApplication,
                     didFinishLaunchingWithOptions launchOptions: [UIApplication.LaunchOptionsKey: Any]?) -> Bool {
        // AdMob 초기화는 AdManager.start()에서 (rootVC 준비 후). 여기선 no-op.
        return true
    }

    // ATT(App Tracking Transparency) 요청은 GameViewController의 UMP 동의 흐름 뒤에서 수행한다.
    // UMP 동의 폼과 ATT 프롬프트가 동시에 표시되면 iOS가 ATT를 조용히 무시하고,
    // 1회 가드 때문에 재요청도 안 돼 프롬프트가 영영 안 뜬다(App Store 2.1 반려 원인).

    // MARK: UISceneSession
    func application(_ application: UIApplication,
                     configurationForConnecting connectingSceneSession: UISceneSession,
                     options: UIScene.ConnectionOptions) -> UISceneConfiguration {
        UISceneConfiguration(name: "Default Configuration", sessionRole: connectingSceneSession.role)
    }
}
