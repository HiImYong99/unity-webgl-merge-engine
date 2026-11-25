import Foundation
import GCDWebServer

/// 번들된 Unity WebGL 빌드(`web/`)를 127.0.0.1 루프백 HTTP로 서빙.
/// - WKWebView가 `file://`로 WASM streaming 시 겪는 CORS/MIME 제약을 회피
/// - `.br` 확장자 파일에 올바른 MIME(application/wasm 등) 부여
///   ※ WKWebView는 plain HTTP(http://localhost)에서 `Content-Encoding: br`를
///     디코딩하지 않는다(HTTPS 전용). 따라서 빌드 동기화 시 `.br` 파일을 미리
///     해제(identity)해 두고, 여기서는 Content-Encoding 없이 그대로 서빙한다.
/// - HTTP range 요청 지원 (대용량 .data 스트리밍)
/// Android의 `WebViewAssetLoader` 대응물.
final class LocalWebServer {

    private let server = GCDWebServer()
    private let webRoot: URL
    private(set) var baseURL: URL?

    init() {
        // 앱 번들 내 web/ 디렉토리 (build.sh ios가 Unity Build/ 복사)
        let root = Bundle.main.resourceURL ?? Bundle.main.bundleURL
        webRoot = root.appendingPathComponent("web", isDirectory: true)
    }

    func start() {
        // .br 확장자 파일 전용 핸들러 (올바른 MIME 부여). 일반 파일보다 먼저 등록.
        // 파일은 빌드 동기화 단계에서 이미 해제(identity)되어 있으므로 Content-Encoding은 붙이지 않는다.
        server.addHandler(
            match: { method, url, headers, path, query -> GCDWebServerRequest? in
                guard method == "GET", path.hasSuffix(".br") else { return nil }
                return GCDWebServerRequest(method: method, url: url, headers: headers,
                                           path: path, query: query)
            },
            processBlock: { [weak self] request -> GCDWebServerResponse? in
                guard let self = self else { return GCDWebServerResponse(statusCode: 500) }
                // 선행 슬래시 제거 후 안전하게 경로 결합
                let relative = request.path.hasPrefix("/") ? String(request.path.dropFirst()) : request.path
                let filePath = self.webRoot.appendingPathComponent(relative).path

                guard let resp = GCDWebServerFileResponse(file: filePath) else {
                    return GCDWebServerResponse(statusCode: 404)
                }

                let p = request.path
                if p.hasSuffix(".js.br") {
                    resp.contentType = "application/javascript"
                } else if p.hasSuffix(".wasm.br") {
                    resp.contentType = "application/wasm"
                } else if p.hasSuffix(".json.br") {
                    resp.contentType = "application/json"
                } else {
                    resp.contentType = "application/octet-stream"
                }
                return resp
            }
        )

        // 나머지 정적 파일(.html/.js/.png/.data 등) 기본 서빙 + range 지원
        server.addGETHandler(forBasePath: "/",
                             directoryPath: webRoot.path,
                             indexFilename: "index.html",
                             cacheAge: 0,
                             allowRangeRequests: true)

        do {
            try server.start(options: [
                GCDWebServerOption_BindToLocalhost: true,
                GCDWebServerOption_Port: 0,                       // 빈 포트 자동 할당
                // 기본값(true)이면 start 시점에 앱이 background 상태일 경우 GCDWebServer가
                // 실제 소켓 바인딩을 미루고 throw 없이 YES만 반환한다(_port=0, serverURL=nil).
                // 콜드런치 초기엔 applicationState가 background인 경우가 많아 baseURL이 nil이 되고
                // WebView 로드가 영구히 스킵되어 검정화면이 된다. false로 즉시 바인딩을 강제한다.
                GCDWebServerOption_AutomaticallySuspendInBackground: false
            ])
            // server.serverURL은 기기/시뮬에서 start 성공 후에도 간헐적으로 nil을 반환한다
            // (주 IP 주소 결정 실패). BindToLocalhost로 띄웠으므로 바인딩된 포트로 직접 구성한다.
            if let url = server.serverURL {
                baseURL = url
            } else if server.port > 0 {
                baseURL = URL(string: "http://127.0.0.1:\(server.port)/")
            }
        } catch {
            NSLog("[LocalWebServer] start 실패: \(error)")
        }
    }

    func stop() {
        if server.isRunning { server.stop() }
    }
}
