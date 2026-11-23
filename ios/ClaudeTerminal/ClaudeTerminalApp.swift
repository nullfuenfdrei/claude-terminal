import SwiftUI
import WebKit

@main
struct ClaudeTerminalApp: App {
    var body: some Scene {
        WindowGroup {
            TerminalWebView()
                .ignoresSafeArea()
                .background(Color.black)
                .preferredColorScheme(.dark)
                .statusBarHidden(false)
        }
    }
}

/// Zeigt die gebündelte index.html in einer WKWebView.
/// Die Seite läuft unter der Origin https://claude-terminal.app, damit
/// localStorage (API-Key, Theme) und Anfragen an api.anthropic.com funktionieren.
struct TerminalWebView: UIViewRepresentable {
    final class Coordinator: NSObject, WKUIDelegate {
        // Links mit target=_blank (z. B. console.anthropic.com) in Safari öffnen
        func webView(_ webView: WKWebView, createWebViewWith configuration: WKWebViewConfiguration,
                     for navigationAction: WKNavigationAction, windowFeatures: WKWindowFeatures) -> WKWebView? {
            if let url = navigationAction.request.url { UIApplication.shared.open(url) }
            return nil
        }
        // Mikrofon für den Audio-Visualizer: iOS fragt den Nutzer
        @available(iOS 15.0, *)
        func webView(_ webView: WKWebView, requestMediaCapturePermissionFor origin: WKSecurityOrigin,
                     initiatedByFrame frame: WKFrameInfo, type: WKMediaCaptureType,
                     decisionHandler: @escaping (WKPermissionDecision) -> Void) {
            decisionHandler(.prompt)
        }
    }

    func makeCoordinator() -> Coordinator { Coordinator() }

    func makeUIView(context: Context) -> WKWebView {
        let config = WKWebViewConfiguration()
        config.websiteDataStore = .default()
        config.allowsInlineMediaPlayback = true

        let web = WKWebView(frame: .zero, configuration: config)
        web.isOpaque = false
        web.backgroundColor = .black
        web.scrollView.backgroundColor = .black
        web.scrollView.isScrollEnabled = false
        web.scrollView.bounces = false
        web.scrollView.contentInsetAdjustmentBehavior = .never
        web.allowsLinkPreview = false
        web.uiDelegate = context.coordinator
        #if DEBUG
        if #available(iOS 16.4, *) { web.isInspectable = true }
        #endif

        if let url = Bundle.main.url(forResource: "index", withExtension: "html"),
           let html = try? String(contentsOf: url, encoding: .utf8) {
            web.loadHTMLString(html, baseURL: URL(string: "https://claude-terminal.app/"))
        }
        return web
    }

    func updateUIView(_ uiView: WKWebView, context: Context) {}
}
