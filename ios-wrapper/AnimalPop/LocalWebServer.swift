import Foundation
import GCDWebServer

/// 번들된 Unity WebGL 빌드(`web/`)를 127.0.0.1 루프백 HTTP로 서빙.
/// - WKWebView가 `file://`로 WASM streaming 시 겪는 CORS/MIME 제약을 회피
/// - `.br`(Brotli) 파일에 `Content-Encoding: br` + 올바른 MIME 부여
///   (WebKit은 Brotli 디코딩을 네이티브 지원 → 토스용 Brotli 빌드 그대로 재사용)
/// - HTTP range 요청 지원 (대용량 .data 스트리밍)
/// Android의 `WebViewAssetLoader` 대응물.
final class LocalWebServer {

    private let server = GCDWebServer()
    private let webRoot: URL
    private(set) var baseURL: URL?

    init() {
        // 앱 번들 내 web/ 디렉토리 (build.sh ios가 Unity Build/ 복사)
        webRoot = Bundle.main.bundleURL.appendingPathComponent("web", isDirectory: true)
    }

    func start() {
        // .br 파일 전용 핸들러 (Content-Encoding 헤더 주입). 일반 파일보다 먼저 등록.
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

                let byteRange = request.hasByteRange()
                    ? request.byteRange
                    : NSRange(location: NSNotFound, length: 0)

                guard let resp = GCDWebServerFileResponse(file: filePath, byteRange: byteRange) else {
                    return GCDWebServerResponse(statusCode: 404)
                }
                resp.setValue("br", forAdditionalHeader: "Content-Encoding")

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
                GCDWebServerOption_AutomaticallySuspendInBackground: false
            ])
            baseURL = server.serverURL
        } catch {
            NSLog("[LocalWebServer] start 실패: \(error)")
        }
    }

    func stop() {
        if server.isRunning { server.stop() }
    }
}
