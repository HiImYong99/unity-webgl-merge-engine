import UIKit
import WebKit
import GoogleMobileAds

/// WKWebView로 Unity WebGL을 호스팅하는 메인 화면.
/// - 풀스크린 WebView + 하단 AdMob 배너(오버레이)
/// - 백그라운드/포그라운드 시 Web Audio suspend/resume (Android onPause/onResume 대응)
/// - 광고/IAP/Game Center 매니저 소유 및 브릿지 배선
final class GameViewController: UIViewController {

    private var webView: WKWebView!
    private let localServer = LocalWebServer()
    private let adManager = AdManager()
    private let gameCenter = GameCenterManager()
    private var storeManager: Any?   // StoreManager (iOS 15+)
    private var bridge: IosBridge!

    override func viewDidLoad() {
        super.viewDidLoad()
        view.backgroundColor = .black
        UIApplication.shared.isIdleTimerDisabled = true

        if #available(iOS 15.0, *) {
            let store = StoreManager()
            store.startObserving()
            storeManager = store
        }

        adManager.rootViewController = self
        gameCenter.presentAuthVC = { [weak self] vc in self?.present(vc, animated: true) }

        setupWebView()
        startServerAndLoad()
        setupBanner()
        observeLifecycle()

        adManager.start()
    }

    // MARK: - WebView
    private func setupWebView() {
        let config = WKWebViewConfiguration()
        config.allowsInlineMediaPlayback = true
        config.mediaTypesRequiringUserActionForPlayback = []

        bridge = IosBridge(gameVC: self,
                           adManager: adManager,
                           storeManager: storeManager ?? NSNull(),
                           gameCenter: gameCenter)
        // 약한 프록시로 등록 — WKUserContentController가 핸들러를 강하게 잡아
        // GameViewController가 해제되지 않는 retain cycle 방지
        config.userContentController.add(WeakScriptMessageHandler(bridge), name: IosBridge.name)

        webView = WKWebView(frame: view.bounds, configuration: config)
        webView.autoresizingMask = [.flexibleWidth, .flexibleHeight]
        webView.scrollView.bounces = false
        webView.scrollView.isScrollEnabled = false
        webView.isOpaque = false
        webView.backgroundColor = .black
        webView.navigationDelegate = self
        if #available(iOS 16.4, *) { webView.isInspectable = true }
        view.addSubview(webView)
    }

    private func startServerAndLoad() {
        localServer.start()
        guard let base = localServer.baseURL else {
            NSLog("[GameViewController] 로컬 서버 시작 실패")
            return
        }
        let indexURL = base.appendingPathComponent("index.html")
        webView.load(URLRequest(url: indexURL))
    }

    // MARK: - Banner (하단 오버레이, 50pt)
    private func setupBanner() {
        let banner = adManager.makeBanner(in: self)
        banner.translatesAutoresizingMaskIntoConstraints = false
        view.addSubview(banner)
        NSLayoutConstraint.activate([
            banner.centerXAnchor.constraint(equalTo: view.centerXAnchor),
            banner.bottomAnchor.constraint(equalTo: view.safeAreaLayoutGuide.bottomAnchor)
        ])
    }

    // MARK: - 오디오 생명주기 (Android onPause/onResume 대응)
    private func observeLifecycle() {
        NotificationCenter.default.addObserver(self, selector: #selector(onBackground),
                                               name: UIApplication.didEnterBackgroundNotification, object: nil)
        NotificationCenter.default.addObserver(self, selector: #selector(onForeground),
                                               name: UIApplication.willEnterForegroundNotification, object: nil)
    }

    @objc private func onBackground() {
        callJS("try{if(typeof Module!=='undefined'&&Module.WEBAudio&&Module.WEBAudio.audioContext)Module.WEBAudio.audioContext.suspend();}catch(e){}")
    }

    @objc private func onForeground() {
        callJS("try{if(typeof Module!=='undefined'&&Module.WEBAudio&&Module.WEBAudio.audioContext)Module.WEBAudio.audioContext.resume();}catch(e){}")
    }

    // MARK: - 네이티브 → JS
    func callJS(_ js: String) {
        webView.evaluateJavaScript(js, completionHandler: nil)
    }

    // MARK: - 풀스크린
    override var prefersStatusBarHidden: Bool { true }
    override var prefersHomeIndicatorAutoHidden: Bool { true }
    override var supportedInterfaceOrientations: UIInterfaceOrientationMask { .portrait }

    deinit {
        webView?.configuration.userContentController.removeScriptMessageHandler(forName: IosBridge.name)
        localServer.stop()
        if #available(iOS 15.0, *), let store = storeManager as? StoreManager { store.stopObserving() }
    }
}

// MARK: - 인증/리더보드는 viewDidAppear 이후 안전
extension GameViewController: WKNavigationDelegate {
    func webView(_ webView: WKWebView, didFinish navigation: WKNavigation!) {
        gameCenter.authenticate()
    }
}

/// WKUserContentController가 실제 핸들러를 강하게 retain하지 않도록 감싸는 약한 프록시.
final class WeakScriptMessageHandler: NSObject, WKScriptMessageHandler {
    private weak var delegate: WKScriptMessageHandler?
    init(_ delegate: WKScriptMessageHandler) { self.delegate = delegate }
    func userContentController(_ ucc: WKUserContentController, didReceive message: WKScriptMessage) {
        delegate?.userContentController(ucc, didReceive: message)
    }
}
