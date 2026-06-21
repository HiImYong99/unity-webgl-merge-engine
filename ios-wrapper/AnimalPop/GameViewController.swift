import UIKit
import WebKit
import GoogleMobileAds
import UserMessagingPlatform

/// [진단] print는 시뮬레이터 통합로그/Console에 잡히지 않으므로 NSLog로 출력한다.
/// (실기기 `devicectl --console`·시뮬 `log show` 양쪽에서 캡처됨)
/// 릴리스 빌드에서는 no-op으로 컴파일되어 로그·오버헤드가 남지 않는다.
#if DEBUG
fileprivate func diag(_ msg: String) { NSLog("%@", msg) }
#else
@inline(__always) fileprivate func diag(_ msg: String) {}
#endif

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
    private var didLoadContent = false
    #if DEBUG
    private let logHandler = ConsoleLogHandler()   // [진단] JS 콘솔 포워딩 (DEBUG 전용)
    #endif

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
        localServer.start()          // 서버만 먼저 기동. 실제 로드는 레이아웃 완료 후(viewDidAppear)
        setupBanner()                // 배너 뷰만 부착 — 로드는 동의 완료 후 SDK start에서
        observeLifecycle()

        requestConsentThenStartAds() // UMP(GDPR/EEA) 동의 → canRequestAds 시에만 광고 시작
    }

    // MARK: - 광고 동의 (Google UMP — EEA/GDPR 필수)
    private func requestConsentThenStartAds() {
        let params = RequestParameters()
        // EEA 사용자에게 동의 정보 갱신 후 필요 시 동의 폼 표시.
        ConsentInformation.shared.requestConsentInfoUpdate(with: params) { [weak self] error in
            // UMP 콜백은 메인 스레드 보장 안 됨 → UI 표시/ SDK 호출은 메인에서.
            DispatchQueue.main.async {
                if let error = error {
                    NSLog("[Consent] 정보 갱신 오류: \(error.localizedDescription)")
                }
                guard let self = self else { return }
                ConsentForm.loadAndPresentIfRequired(from: self) { [weak self] formError in
                    DispatchQueue.main.async {
                        if let formError = formError {
                            NSLog("[Consent] 폼 오류: \(formError.localizedDescription)")
                        }
                        self?.startAdsIfAllowed()
                    }
                }
            }
        }
    }

    private func startAdsIfAllowed() {
        // 동의 결과상 광고 요청 가능할 때만 SDK 시작/로드 (비동의 EEA 사용자에겐 요청 안 함).
        if ConsentInformation.shared.canRequestAds {
            adManager.start()
        } else {
            NSLog("[Consent] canRequestAds=false — 광고 요청 생략")
        }
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

        // [진단] JS 콘솔/에러를 네이티브 로그로 포워딩 (DEBUG 전용 — 릴리스엔 미주입)
        #if DEBUG
        config.userContentController.add(WeakScriptMessageHandler(logHandler), name: "logger")
        let logScript = """
        (function(){
          function send(t,a){ try{ window.webkit.messageHandlers.logger.postMessage(t+': '+Array.prototype.slice.call(a).join(' ')); }catch(e){} }
          var c=window.console; ['log','warn','error','info'].forEach(function(k){ var o=c[k]; c[k]=function(){ send(k.toUpperCase(), arguments); o&&o.apply(c,arguments); }; });
          window.addEventListener('error', function(e){ send('JSERR', [e.message+' @ '+e.filename+':'+e.lineno]); });
          window.addEventListener('unhandledrejection', function(e){ send('PROMISE', [String(e.reason)]); });
        })();
        """
        config.userContentController.addUserScript(
            WKUserScript(source: logScript, injectionTime: .atDocumentStart, forMainFrameOnly: true))
        #endif

        webView = WKWebView(frame: view.bounds, configuration: config)
        webView.autoresizingMask = [.flexibleWidth, .flexibleHeight]
        webView.scrollView.bounces = false
        webView.scrollView.isScrollEnabled = false
        webView.isOpaque = false
        webView.backgroundColor = .black
        webView.navigationDelegate = self
        #if DEBUG
        if #available(iOS 16.4, *) { webView.isInspectable = true }   // Safari 원격 디버깅 (DEBUG 전용)
        #endif
        view.addSubview(webView)
    }

    override func viewDidAppear(_ animated: Bool) {
        super.viewDidAppear(animated)
        // 뷰가 화면 크기로 레이아웃된 뒤 로드해야 WKWebView가 device-width를
        // 올바르게(예: 430) 인식한다. viewDidLoad 시점엔 bounds가 미확정이라 320×480로 오인됨.
        loadContentIfNeeded()
    }

    private func loadContentIfNeeded() {
        guard !didLoadContent else { return }
        guard let base = localServer.baseURL else {
            diag("[DIAG] 로컬 서버 시작 실패 — baseURL nil")
            return
        }
        didLoadContent = true
        diag("[DIAG] viewDidAppear 로드: view.bounds=\(view.bounds.size)")
        let indexURL = base.appendingPathComponent("index.html")
        diag("[DIAG] 서버 시작 OK, 로드: \(indexURL.absoluteString)")
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
        #if DEBUG
        webView?.configuration.userContentController.removeScriptMessageHandler(forName: "logger")
        #endif
        localServer.stop()
        if #available(iOS 15.0, *), let store = storeManager as? StoreManager { store.stopObserving() }
    }
}

// MARK: - 인증/리더보드는 viewDidAppear 이후 안전
extension GameViewController: WKNavigationDelegate {
    func webView(_ webView: WKWebView, didFinish navigation: WKNavigation!) {
        diag("[DIAG] didFinish 페이지 로드 완료")
        gameCenter.authenticate()
    }
    func webView(_ webView: WKWebView, didFailProvisionalNavigation navigation: WKNavigation!, withError error: Error) {
        diag("[DIAG] didFailProvisionalNavigation: \(error.localizedDescription)")
    }
    func webView(_ webView: WKWebView, didFail navigation: WKNavigation!, withError error: Error) {
        diag("[DIAG] didFail: \(error.localizedDescription)")
    }
    func webViewWebContentProcessDidTerminate(_ webView: WKWebView) {
        diag("[DIAG] WebContent 프로세스 종료(크래시) — 메모리 부족 의심")
    }
    func webView(_ webView: WKWebView, decidePolicyFor navigationResponse: WKNavigationResponse, decisionHandler: @escaping (WKNavigationResponsePolicy) -> Void) {
        if let http = navigationResponse.response as? HTTPURLResponse {
            diag("[DIAG] 응답 \(http.statusCode): \(navigationResponse.response.url?.lastPathComponent ?? "")")
        }
        decisionHandler(.allow)
    }
}

/// [진단] JS console.* + window.onerror를 네이티브 stdout으로 출력 (devicectl --console로 캡처)
final class ConsoleLogHandler: NSObject, WKScriptMessageHandler {
    func userContentController(_ ucc: WKUserContentController, didReceive message: WKScriptMessage) {
        diag("[JS] \(message.body)")
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
