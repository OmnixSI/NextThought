import AppKit
import SwiftUI

/// macOS 14-compatible scroll intent. Content growth never turns off follow; user scrolling does.
struct ReadingScrollMonitor: NSViewRepresentable {
    @Binding var follows: Bool

    func makeNSView(context: Context) -> Probe {
        let view = Probe()
        view.onFollowChange = { follows = $0 }
        return view
    }
    func updateNSView(_ nsView: Probe, context: Context) { nsView.onFollowChange = { follows = $0 } }

    final class Probe: NSView {
        var onFollowChange: ((Bool) -> Void)?
        private weak var scrollView: NSScrollView?
        private var observer: NSObjectProtocol?
        private var monitor: Any?
        private var userScrollUntil = Date.distantPast

        override func viewDidMoveToWindow() {
            super.viewDidMoveToWindow()
            removeObservers()
            guard window != nil else { return }
            DispatchQueue.main.async { [weak self] in self?.install() }
        }
        private func install() {
            guard window != nil, let scroll = enclosingScrollView else { return }
            scrollView = scroll
            scroll.contentView.postsBoundsChangedNotifications = true
            observer = NotificationCenter.default.addObserver(forName: NSView.boundsDidChangeNotification,
                                                               object: scroll.contentView, queue: .main) { [weak self] _ in
                guard let self, Date() < self.userScrollUntil || NSEvent.pressedMouseButtons != 0 else { return }
                self.reportPosition()
            }
            monitor = NSEvent.addLocalMonitorForEvents(matching: [.scrollWheel, .keyDown]) { [weak self] event in
                guard let self, let scroll = self.scrollView, event.window === scroll.window else { return event }
                let focused = (scroll.window?.firstResponder as? NSView).map { $0 === scroll || $0.isDescendant(of: scroll) } ?? false
                let inside = event.type == .keyDown ? focused : scroll.bounds.contains(scroll.convert(event.locationInWindow, from: nil))
                guard inside, event.type == .scrollWheel || [115, 116, 121, 125, 126].contains(event.keyCode) else { return event }
                self.userScrollUntil = Date().addingTimeInterval(0.5)
                self.onFollowChange?(false)
                DispatchQueue.main.async { [weak self] in self?.reportPosition() }
                return event
            }
        }
        private func reportPosition() {
            guard let scroll = scrollView, let document = scroll.documentView else { return }
            let visible = scroll.documentVisibleRect
            let remaining = document.isFlipped ? document.bounds.maxY - visible.maxY : visible.minY - document.bounds.minY
            onFollowChange?(remaining < 28)
        }
        private func removeObservers() {
            if let observer { NotificationCenter.default.removeObserver(observer) }
            if let monitor { NSEvent.removeMonitor(monitor) }
            observer = nil; monitor = nil; scrollView = nil
        }
        deinit { removeObservers() }
    }
}
