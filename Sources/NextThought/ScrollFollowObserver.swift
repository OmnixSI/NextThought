import AppKit
import SwiftUI
import NextThoughtCore

/// Observes only user-driven movement in a SwiftUI scroll view. Content layout
/// changes and ScrollViewReader jumps do not pause following.
struct ScrollFollowObserver: NSViewRepresentable {
    var onScrollAway: () -> Void
    var onReachedLatest: () -> Void
    var onTextSelection: () -> Void

    func makeCoordinator() -> Coordinator {
        Coordinator(onScrollAway: onScrollAway,
                    onReachedLatest: onReachedLatest,
                    onTextSelection: onTextSelection)
    }

    func makeNSView(context: Context) -> NSView {
        let view = AnchorView()
        context.coordinator.update(onScrollAway: onScrollAway,
                                  onReachedLatest: onReachedLatest,
                                  onTextSelection: onTextSelection)
        DispatchQueue.main.async { context.coordinator.attach(to: view) }
        return view
    }

    func updateNSView(_ view: NSView, context: Context) {
        context.coordinator.update(onScrollAway: onScrollAway,
                                  onReachedLatest: onReachedLatest,
                                  onTextSelection: onTextSelection)
        DispatchQueue.main.async { context.coordinator.attach(to: view) }
    }

    static func dismantleNSView(_ view: NSView, coordinator: Coordinator) {
        coordinator.detach()
    }

    private final class AnchorView: NSView {
        override func hitTest(_ point: NSPoint) -> NSView? { nil }
    }

    final class Coordinator {
        private var onScrollAway: () -> Void
        private var onReachedLatest: () -> Void
        private var onTextSelection: () -> Void
        private weak var scrollView: NSScrollView?
        private var previousBoundsNotifications = false
        private var boundsObserver: NSObjectProtocol?
        private var selectionObserver: NSObjectProtocol?
        private var scrollWheelMonitor: Any?
        private var dragMonitor: Any?
        private var scrollerClickMonitor: Any?
        private var keyboardScrollMonitor: Any?
        private var lastVisibleRect: NSRect?
        private var pendingUserScroll = false

        init(onScrollAway: @escaping () -> Void,
             onReachedLatest: @escaping () -> Void,
             onTextSelection: @escaping () -> Void) {
            self.onScrollAway = onScrollAway
            self.onReachedLatest = onReachedLatest
            self.onTextSelection = onTextSelection
        }

        func update(onScrollAway: @escaping () -> Void,
                    onReachedLatest: @escaping () -> Void,
                    onTextSelection: @escaping () -> Void) {
            self.onScrollAway = onScrollAway
            self.onReachedLatest = onReachedLatest
            self.onTextSelection = onTextSelection
        }

        func attach(to anchor: NSView) {
            var candidate: NSView? = anchor
            while let view = candidate, !(view is NSScrollView) { candidate = view.superview }
            guard let scrollView = candidate as? NSScrollView, scrollView !== self.scrollView else { return }
            detach()

            self.scrollView = scrollView
            previousBoundsNotifications = scrollView.contentView.postsBoundsChangedNotifications
            scrollView.contentView.postsBoundsChangedNotifications = true
            lastVisibleRect = scrollView.documentView?.visibleRect

            boundsObserver = NotificationCenter.default.addObserver(
                forName: NSView.boundsDidChangeNotification,
                object: scrollView.contentView,
                queue: .main
            ) { [weak self] _ in
                self?.handleBoundsChange()
            }

            selectionObserver = NotificationCenter.default.addObserver(
                forName: NSTextView.didChangeSelectionNotification,
                object: nil,
                queue: .main
            ) { [weak self] notification in
                guard let textView = notification.object as? NSTextView,
                      self?.contains(textView) == true,
                      textView.selectedRanges.contains(where: { $0.rangeValue.length > 0 }) else { return }
                self?.onTextSelection()
            }

            scrollWheelMonitor = NSEvent.addLocalMonitorForEvents(matching: .scrollWheel) { [weak self] event in
                self?.markUserScroll(for: event)
                return event
            }
            dragMonitor = NSEvent.addLocalMonitorForEvents(matching: .leftMouseDragged) { [weak self] event in
                self?.markUserScroll(for: event)
                return event
            }
            scrollerClickMonitor = NSEvent.addLocalMonitorForEvents(matching: .leftMouseDown) { [weak self] event in
                self?.markVerticalScrollerAction(for: event)
                return event
            }
            keyboardScrollMonitor = NSEvent.addLocalMonitorForEvents(matching: .keyDown) { [weak self] event in
                self?.markKeyboardScroll(for: event)
                return event
            }
        }

        func detach() {
            if let boundsObserver { NotificationCenter.default.removeObserver(boundsObserver) }
            if let selectionObserver { NotificationCenter.default.removeObserver(selectionObserver) }
            if let scrollWheelMonitor { NSEvent.removeMonitor(scrollWheelMonitor) }
            if let dragMonitor { NSEvent.removeMonitor(dragMonitor) }
            if let scrollerClickMonitor { NSEvent.removeMonitor(scrollerClickMonitor) }
            if let keyboardScrollMonitor { NSEvent.removeMonitor(keyboardScrollMonitor) }
            if let scrollView {
                scrollView.contentView.postsBoundsChangedNotifications = previousBoundsNotifications
            }
            boundsObserver = nil
            selectionObserver = nil
            scrollWheelMonitor = nil
            dragMonitor = nil
            scrollerClickMonitor = nil
            keyboardScrollMonitor = nil
            scrollView = nil
            lastVisibleRect = nil
            pendingUserScroll = false
        }

        private func markUserScroll(for event: NSEvent) {
            guard let scrollView,
                  event.window === scrollView.window,
                  scrollView.bounds.contains(scrollView.convert(event.locationInWindow, from: nil)) else { return }
            markPendingUserScroll()
        }

        private func markVerticalScrollerAction(for event: NSEvent) {
            guard let scrollView,
                  event.window === scrollView.window,
                  let scroller = scrollView.verticalScroller else { return }
            let point = scrollView.convert(event.locationInWindow, from: nil)
            let scrollerFrame = scroller.convert(scroller.bounds, to: scrollView)
            guard ScrollFollowInputClassifier.isPoint(point, inside: scrollerFrame) else { return }
            markPendingUserScroll()
        }

        private func markKeyboardScroll(for event: NSEvent) {
            guard let scrollView,
                  event.window === scrollView.window,
                  ScrollFollowInputClassifier.isScrollNavigationKeyCode(event.keyCode),
                  let firstResponder = scrollView.window?.firstResponder as? NSView,
                  firstResponder === scrollView || firstResponder.isDescendant(of: scrollView) else { return }
            markPendingUserScroll()
        }

        private func markPendingUserScroll() {
            pendingUserScroll = true
            // Bounds notifications are normally sent synchronously while AppKit
            // handles the event. Clear the marker if the gesture was at an edge.
            DispatchQueue.main.async { [weak self] in self?.pendingUserScroll = false }
        }

        private func handleBoundsChange() {
            guard let scrollView, let document = scrollView.documentView else { return }
            let current = document.visibleRect
            let previous = lastVisibleRect
            lastVisibleRect = current
            guard pendingUserScroll else { return }
            pendingUserScroll = false

            if isAtLatest(current, in: document) {
                onReachedLatest()
            } else if let previous, current.origin != previous.origin {
                onScrollAway()
            }
        }

        private func isAtLatest(_ visible: NSRect, in document: NSView) -> Bool {
            let inset: CGFloat = 24
            if document.isFlipped {
                return visible.maxY >= document.bounds.maxY - inset
            }
            return visible.minY <= document.bounds.minY + inset
        }

        private func contains(_ view: NSView) -> Bool {
            guard let document = scrollView?.documentView else { return false }
            var current: NSView? = view
            while let ancestor = current {
                if ancestor === document { return true }
                current = ancestor.superview
            }
            return false
        }
    }
}
