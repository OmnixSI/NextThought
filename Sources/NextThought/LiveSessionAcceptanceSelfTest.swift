import AppKit
import Foundation
import SwiftUI
import NextThoughtCore

/// How often the heavy parts of the live page are rebuilt. The page is told about every
/// recognised syllable and every line that is saved; what holds menus and the whole
/// transcript must not be rebuilt for those (an open menu flickered and missed clicks,
/// and a long meeting got slower by the minute). Counted for the `live-churn` self-test.
@MainActor
enum LiveRenderCount {
    static var topBar = 0, moreMenu = 0, transcript = 0, rightColumn = 0, partial = 0, line = 0, page = 0
    static func reset() { topBar = 0; moreMenu = 0; transcript = 0; rightColumn = 0; partial = 0; line = 0; page = 0 }
}

/// Native, deterministic acceptance evidence for the live meeting slice.
/// All meeting content is synthetic and DemoMode prevents audio/model/network work.
@MainActor
enum LiveSessionAcceptanceSelfTest {
    /// A long meeting in full flow on the page that is on screen: syllables arrive, lines
    /// are saved, a sentence is added now and then. Says how often each heavy part of the
    /// page was rebuilt. SwiftUI only updates a window that is really visible, so this
    /// runs in the walkthrough copy of the app (`NEXTTHOUGHT_DEMO_CHURN=<file>` with
    /// `preview.sh walk`), not in a headless self-test.
    static func churn(lines: Int = 500) async -> String {
        let state = AppState.shared
        configure(state, for: .streaming)
        state.entries = (0..<lines).map { index in
            var entry = TranscriptEntry(source: (index % 3 == 0 ? CaptureSource.microphone : CaptureSource.system).rawValue,
                                        text: "第 \(index) 句：缓存命中率目标是百分之九十二，灰度期间比较 P95 与错误率。", isFinal: true)
            if index % 3 != 0 { entry.voice = index % 4 }
            return entry
        }
        try? await Task.sleep(for: .milliseconds(1_500))
        let before = "before: page \(LiveRenderCount.page) top \(LiveRenderCount.topBar) transcript \(LiveRenderCount.transcript) line \(LiveRenderCount.line) partial \(LiveRenderCount.partial); running \(state.isRunning) section \(state.section)"
        LiveRenderCount.reset()
        let sessionID = state.activeArchiveID!
        let began = Date()
        var added = 0
        for step in 0..<300 {
            state.partialTranscript = String(repeating: "正在说的话 ", count: step % 12 + 1)
            if step % 3 == 0 {
                let saving = step % 6 == 0
                SessionArchive.shared.installDemoPersistence(SessionPersistenceSnapshot(
                    submittedRevision: step + 10, persistedRevision: saving ? step + 9 : step + 10,
                    pendingEvents: saving ? 1 : 0, pendingBytes: saving ? 120 : 0, lastPersistedAt: Date(), lastFailure: nil), for: sessionID)
            }
            if step % 30 == 29 {
                state.entries.append(TranscriptEntry(source: CaptureSource.system.rawValue, text: "新的一句 \(step)", isFinal: true)); added += 1
                state.partialTranscript = ""
            }
            try? await Task.sleep(for: .milliseconds(16))
        }
        let seconds = Date().timeIntervalSince(began)
        let visible = NSApp.windows.first(where: MainWindowLayout.isMainWindow)?.occlusionState.contains(.visible) == true
        return before + "\n" + String(format: "%d lines, 300 updates (%d new sentences) in %.2f s, page visible %@: page %d, top bar rebuilt %d, menu %d, transcript %d, lines %d, partial %d, right column %d",
                      lines, added, seconds, visible ? "yes" : "no", LiveRenderCount.page, LiveRenderCount.topBar, LiveRenderCount.moreMenu, LiveRenderCount.transcript, LiveRenderCount.line, LiveRenderCount.partial, LiveRenderCount.rightColumn)
    }


    private struct Capture: Encodable {
        let scenario: String
        let description: String
        let appearance: String
        let widthPoints: Int
        let heightPoints: Int
        let pixelWidth: Int
        let pixelHeight: Int
        let dynamicTypeSize: String
        let locale: String
        let path: String
        let overlayPath: String?
        let synthetic: Bool
        let usesRealAudio: Bool
        let usesASR: Bool
        let usesNetwork: Bool
        let notes: [String]
    }

    private struct Interaction: Encodable {
        let id: String
        let result: String
        let evidence: String
    }

    private struct ComponentSample: Encodable {
        let path: String
        let widthPoints: Int
        let heightPoints: Int
        let pixelWidth: Int
        let pixelHeight: Int
        let appearance: String
        let locale: String
        let synthetic: Bool
    }

    private struct Manifest: Encodable {
        let schemaVersion: Int
        let generatedAt: Date
        let sourceRevision: String
        let syntheticState: Bool
        let scenarioCount: Int
        let matrixCaptureCount: Int
        let captures: [Capture]
        let componentSample: ComponentSample
        let interactions: [Interaction]
        let limitations: [String]
    }

    private static let appearances = ["light", "dark"]
    private static let widths = [1_200, 900, Int(MainWindowLayout.minimum.width)]
    private static let height = 780
    private static let longTitle = "Q4 A/B Test 增长实验复盘 · Redis cache · 欧洲团队跨时区评审 (Europe Q4 Review)"

    static func run(_ options: [String: String]) async throws -> String {
        guard DemoMode.isEnabled else {
            throw SelfTest.Failure("live-session-ui requires NEXTTHOUGHT_DEMO=1")
        }
        guard let output = options["output"], !output.isEmpty else {
            throw SelfTest.Failure("live-session-ui requires --output <directory>")
        }

        let outputDirectory = URL(fileURLWithPath: output, isDirectory: true).standardizedFileURL
        try FileManager.default.createDirectory(at: outputDirectory, withIntermediateDirectories: true)
        for old in (try FileManager.default.contentsOfDirectory(at: outputDirectory,
                                                                includingPropertiesForKeys: nil))
            where old.pathExtension == "png" || old.lastPathComponent == "manifest.json" || old.lastPathComponent == "report.md" {
            try FileManager.default.removeItem(at: old)
        }

        let state = AppState.shared
        var captures: [Capture] = []
        for appearance in appearances {
            for width in widths {
                for scenario in LiveSessionAcceptanceScenario.allCases {
                    state.populateDemo()
                    configure(state, for: scenario)
                    state.appearance = appearance
                    state.applyAppearance()

                    let fileName = "synthetic-\(scenario.rawValue)-\(appearance)-\(width).png"
                    let url = outputDirectory.appendingPathComponent(fileName)
                    let root = LiveSessionView()
                        .overlay(alignment: .top) { ErrorBanner() }
                        .environment(state)
                        .environment(\.liveSessionAcceptanceScenario, scenario)
                        .environment(\.dynamicTypeSize, scenario.accessibilitySize)
                        .environment(\.locale, Locale(identifier: "zh-Hans"))
                        .preferredColorScheme(appearance == "dark" ? .dark : .light)
                        .frame(width: CGFloat(width), height: CGFloat(height))

                    let window = NSWindow(contentRect: NSRect(x: 0, y: 0, width: width, height: height),
                                          styleMask: [.borderless], backing: .buffered, defer: false)
                    window.isReleasedWhenClosed = false
                    window.ignoresMouseEvents = true
                    window.contentView = NSHostingView(rootView: root)
                    window.minSize = .zero
                    if [.focus, .overlay, .busyFocus, .selectionFocus].contains(scenario) {
                        window.makeKeyAndOrderFront(nil)
                    } else {
                        window.orderFrontRegardless()
                    }

                    try await Task.sleep(for: .milliseconds(scenario == .overlay ? 450 : 120))
                    guard let host = window.contentView,
                          let bitmap = host.bitmapImageRepForCachingDisplay(in: host.bounds) else {
                        throw SelfTest.Failure("no rendered bitmap for \(fileName)")
                    }
                    host.layoutSubtreeIfNeeded()
                    host.cacheDisplay(in: host.bounds, to: bitmap)
                    guard let png = bitmap.representation(using: .png, properties: [:]), !png.isEmpty else {
                        throw SelfTest.Failure("empty PNG for \(fileName)")
                    }
                    try png.write(to: url, options: .atomic)
                    var overlayPath: String?
                    if scenario == .overlay,
                       let overlayWindow = NSApp.windows.first(where: {
                           $0 !== window && $0.isVisible && $0.contentView != nil && $0.frame.width < CGFloat(width)
                       }), let overlayView = overlayWindow.contentView,
                       let overlayBitmap = overlayView.bitmapImageRepForCachingDisplay(in: overlayView.bounds) {
                        overlayView.layoutSubtreeIfNeeded()
                        overlayView.cacheDisplay(in: overlayView.bounds, to: overlayBitmap)
                        if let overlayPNG = overlayBitmap.representation(using: .png, properties: [:]), !overlayPNG.isEmpty {
                            let overlayName = fileName.replacingOccurrences(of: ".png", with: "-popover.png")
                            try overlayPNG.write(to: outputDirectory.appendingPathComponent(overlayName), options: .atomic)
                            overlayPath = overlayName
                        }
                    }
                    let notes = captureNotes(for: scenario, width: width)
                    captures.append(Capture(
                        scenario: scenario.rawValue,
                        description: scenario.description,
                        appearance: appearance,
                        widthPoints: width,
                        heightPoints: height,
                        pixelWidth: bitmap.pixelsWide,
                        pixelHeight: bitmap.pixelsHigh,
                        dynamicTypeSize: String(describing: scenario.accessibilitySize),
                        locale: "zh-Hans",
                        path: fileName,
                        overlayPath: overlayPath,
                        synthetic: true,
                        usesRealAudio: false,
                        usesASR: false,
                        usesNetwork: false,
                        notes: notes))
                    window.orderOut(nil)
                    window.close()
                }
            }
        }

        let expectedScenarios = Set(LiveSessionAcceptanceScenario.allCases.map(\.rawValue))
        let capturedScenarios = Set(captures.map(\.scenario))
        let expectedCaptureCount = expectedScenarios.count * appearances.count * widths.count
        guard capturedScenarios == expectedScenarios, captures.count == expectedCaptureCount else {
            throw SelfTest.Failure("acceptance matrix captured \(captures.count) rows for \(capturedScenarios.count) scenarios; expected \(expectedCaptureCount) rows for \(expectedScenarios.count) scenarios")
        }

        let componentSample = try await captureComponentSamples(to: outputDirectory)

        var interactions = try await verifyInteractions()
        guard let overlayCapture = captures.first(where: {
                  $0.scenario == LiveSessionAcceptanceScenario.overlay.rawValue
                      && $0.appearance == "dark" && $0.widthPoints == 1_200 && $0.overlayPath != nil
              }),
              let overlayIndex = interactions.firstIndex(where: { $0.id == "evidence-overlay" }) else {
            throw SelfTest.Failure("dark 1200 pt evidence popover was not captured as a native overlay window")
        }
        interactions[overlayIndex] = Interaction(id: "evidence-overlay", result: "passed",
                                                 evidence: "Captured the open evidence details popover at \(overlayCapture.overlayPath ?? "") alongside the live page.")
        let manifest = Manifest(schemaVersion: 1,
                                generatedAt: Date(),
                                sourceRevision: ProcessInfo.processInfo.environment["NEXTTHOUGHT_T07_COMMIT"] ?? "working-tree",
                                syntheticState: true,
                                scenarioCount: capturedScenarios.count,
                                matrixCaptureCount: captures.count,
                                captures: captures,
                                componentSample: componentSample,
                                interactions: interactions,
                                limitations: [
                                    "The 900 pt capture is a below-minimum stress render; the current app window minimum is 960 pt.",
                                    "Save warning and recovery are seeded persistence snapshots. They do not simulate a disk fault or execute a retry against storage.",
                                    "Stale notices and manual corrections are synthetic acceptance fixtures; this matrix does not exercise a real archive revision change.",
                                    "Selection coverage verifies selection-pinned scroll state and unread-count behavior; it does not synthesize a native text range highlight.",
                                    "The streaming+history geometry check uses a synthetic partial transcript, not an audio/ASR stream or real meeting.",
                                    "Hover, pressed, and overlap focus rings are deterministic previews, not native pointer or keyboard recordings.",
                                    "The focused suite does not cover live device permission prompts, VoiceOver navigation, Chinese/Japanese IME composition, or Reduce Motion behavior.",
                                    "The accessibility3 environment override is a no-op for text sizing on macOS; font enlargement remains unverified and no app-level text-size control is provided.",
                                    "No native operation video is included in this focused T07 evidence set."
                                ])
        let encoder = JSONEncoder()
        encoder.outputFormatting = [.prettyPrinted, .sortedKeys]
        encoder.dateEncodingStrategy = .iso8601
        try encoder.encode(manifest).write(to: outputDirectory.appendingPathComponent("manifest.json"), options: .atomic)
        try renderReport(manifest, to: outputDirectory.appendingPathComponent("report.md"))
        return "captured \(captures.count) native matrix PNGs across both appearances, 1200/960/900 pt widths, and \(capturedScenarios.count) synthetic states; component sample captured; interaction checks passed"
    }

    private static func configure(_ state: AppState, for scenario: LiveSessionAcceptanceScenario) {
        state.errorMessage = nil
        state.isRunning = true
        state.isPaused = false
        state.isAnswering = false
        state.partialTranscript = ""
        state.microphonePartialTranscript = ""
        state.automaticAnswers = true
        state.screenshotPreview = nil
        state.questionInput = ""
        state.activeArchiveID = UUID()
        state.plannedMeeting = MeetingPlan(title: longTitle)

        let saved = SessionPersistenceSnapshot(submittedRevision: 8, persistedRevision: 8,
                                               pendingEvents: 0, pendingBytes: 0,
                                               lastPersistedAt: Date(), lastFailure: nil)
        SessionArchive.shared.installDemoPersistence(saved, for: state.activeArchiveID!)

        if scenario == .overlay, let answerID = state.currentAnswerID {
            state.answerCoordinator.update(id: answerID) { answer in
                answer.evidence.retrieval = "演示：固定资料检索，未调用模型"
                answer.evidence.materials = [ContextExcerpt(
                    source: "Q4-growth-review.md", page: 2,
                    text: "缓存命中率目标 ≥ 92%；灰度期间比较 P95 与错误率，达到阈值后进入欧洲区发布。")]
                answer.evidence.notes = ["合成验收依据，不来自真实会议"]
            }
        }

        switch scenario {
        case .streaming:
            state.automaticAnswers = false
            state.partialTranscript = "如果发布窗口需要延长，我们会先核对回滚指标，再决定是否调整。"
        case .streamingHistory:
            state.partialTranscript = "如果发布窗口需要延长，我们会先核对回滚指标，再决定是否调整。"
        case .busy, .busyFocus:
            state.currentAnswerID = nil
            state.answerHistory = []
            state.answer = ""
            state.currentQuestion = "如何验证这次缓存改动没有引入回归？"
            state.pendingQuestion = state.currentQuestion
            state.answerDraft = "先看命中率和 P95 延迟，再比较灰度前后的错误率……"
            state.isAnswering = true
        case .saveWarning:
            SessionArchive.shared.installDemoPersistence(
                SessionPersistenceSnapshot(submittedRevision: 12, persistedRevision: 9,
                                           pendingEvents: 3, pendingBytes: 640,
                                           lastPersistedAt: Date().addingTimeInterval(-8),
                                           lastFailure: "磁盘暂不可写，已保留待保存内容。",
                                           unqueuedWriteCount: 0),
                for: state.activeArchiveID!)
        case .unavailable:
            state.automaticAnswers = false
            state.errorMessage = "回答暂时不可用。记录仍在继续。重试 / 检查连接"
        case .stale:
            state.errorMessage = "原话已修订；这份译文基于之前版本。"
        case .staleManualEdit:
            if let index = state.entries.firstIndex(where: { $0.text.contains("我们用的是先更新数据库") }) {
                state.entries[index].text = "我们采用先更新数据库、再删除缓存，并用 binlog 订阅兜底。"
            }
            state.errorMessage = "已保留手动校正；这份译文基于修订前的原话。"
        case .empty:
            state.automaticAnswers = false
            state.entries = []
            state.partialTranscript = ""
            state.microphonePartialTranscript = ""
            state.currentAnswerID = nil
            state.answerHistory = []
            state.answer = ""
            state.answerDraft = ""
            state.currentQuestion = ""
        case .cancelLateCallback:
            let id = UUID()
            state.currentAnswerID = id
            state.currentQuestion = "如何验证这次缓存改动没有引入回归？"
            state.answerCoordinator.begin(id: id, question: state.currentQuestion)
            state.answerCoordinator.draft(id: id, text: "已生成的部分：先看命中率和 P95 延迟。")
            state.answerCoordinator.cancel()
            state.answerCoordinator.finish(id: id, text: "过晚的结果不应覆盖已保留的部分回答。")
        case .normal, .success, .saveRecovery, .readingHistory, .selection, .selectionFocus,
             .focus, .overlay, .largeText:
            break
        }
    }

    private static func captureNotes(for scenario: LiveSessionAcceptanceScenario, width: Int) -> [String] {
        var notes = ["Synthetic transcript and answer content; no microphone, system audio, ASR, model, or network request."]
        if width == 900 { notes.append("Below the 960 pt minimum window width; stress render only.") }
        if scenario == .largeText { notes.append("Records DynamicTypeSize.accessibility3 in the renderer; macOS does not change text size for this override, so this is not large-text acceptance evidence.") }
        if scenario == .stale { notes.append("Stale message is rendered through the real ErrorBanner view but the state itself is synthetic.") }
        if scenario == .staleManualEdit { notes.append("Corrected transcript text and stale notice are synthetic; no persisted correction/version transition is exercised.") }
        if scenario == .selection { notes.append("ScrollFollowState is selection-pinned; the screenshot does not paint a fake text highlight.") }
        if scenario == .selectionFocus { notes.append("The return-to-live control is focus-highlighted; native keyboard navigation is not exercised.") }
        if scenario == .streamingHistory { notes.append("Synthetic partial transcript is shown while the reading-history state is pinned; this is not a live ASR stream.") }
        if scenario == .cancelLateCallback { notes.append("AnswerCoordinator rejects a synthetic late completion after cancellation; no provider/network request runs.") }
        if scenario == .saveWarning || scenario == .saveRecovery { notes.append("Top-bar persistence status is seeded; no archive file was written.") }
        if scenario == .busyFocus { notes.append("Stop-generation control receives a keyboard-focus highlight in the synthetic busy state.") }
        return notes
    }

    private static func captureComponentSamples(to outputDirectory: URL) async throws -> ComponentSample {
        let path = "synthetic-component-samples-light-1200.png"
        let window = NSWindow(contentRect: NSRect(x: 0, y: 0, width: 1_200, height: 1_080),
                              styleMask: [.borderless], backing: .buffered, defer: false)
        window.isReleasedWhenClosed = false
        window.ignoresMouseEvents = true
        window.contentView = NSHostingView(rootView: LiveSessionAcceptanceComponentSamplesView()
            .preferredColorScheme(.light))
        window.minSize = .zero
        window.orderFrontRegardless()
        try await Task.sleep(for: .milliseconds(180))
        guard let host = window.contentView,
              let bitmap = host.bitmapImageRepForCachingDisplay(in: host.bounds) else {
            throw SelfTest.Failure("no rendered bitmap for component sample sheet")
        }
        host.layoutSubtreeIfNeeded()
        host.cacheDisplay(in: host.bounds, to: bitmap)
        guard let png = bitmap.representation(using: .png, properties: [:]), !png.isEmpty else {
            throw SelfTest.Failure("empty component sample PNG")
        }
        try png.write(to: outputDirectory.appendingPathComponent(path), options: .atomic)
        window.orderOut(nil)
        window.close()
        return ComponentSample(path: path, widthPoints: 1_200, heightPoints: 1_080,
                               pixelWidth: bitmap.pixelsWide, pixelHeight: bitmap.pixelsHigh,
                               appearance: "light", locale: "zh-Hans", synthetic: true)
    }

    private static func verifyInteractions() async throws -> [Interaction] {
        var history = ScrollFollowState()
        history.userScrolledAway()
        history.receivedNewContent(count: 6)
        guard history.mode == .readingHistory, history.pendingItemCount == 6 else {
            throw SelfTest.Failure("reading-history scroll state did not retain six incoming items")
        }
        history.reachedLatest()
        guard history.isFollowing, history.pendingItemCount == 0 else {
            throw SelfTest.Failure("return-to-latest did not clear the unread count")
        }

        var selection = ScrollFollowState()
        selection.userSelectedText()
        selection.receivedNewContent(count: 2)
        selection.reachedLatest()
        guard selection.mode == .selectingText, selection.pendingItemCount == 2 else {
            throw SelfTest.Failure("text selection did not keep the viewport pinned")
        }
        selection.returnToLatest()
        guard selection.isFollowing, !selection.hasNewContent else {
            throw SelfTest.Failure("explicit return did not release selection pinning")
        }

        guard ScrollFollowInputClassifier.isScrollNavigationKeyCode(126),
              ScrollFollowInputClassifier.isScrollNavigationKeyCode(121),
              !ScrollFollowInputClassifier.isScrollNavigationKeyCode(0),
              ScrollFollowInputClassifier.isPoint(CGPoint(x: 12, y: 12), inside: CGRect(x: 0, y: 0, width: 20, height: 20)),
              !ScrollFollowInputClassifier.isPoint(CGPoint(x: 24, y: 24), inside: CGRect(x: 0, y: 0, width: 20, height: 20)) else {
            throw SelfTest.Failure("scroll input classifier changed its navigation or hit-test contract")
        }

        let cancellation = try verifyCancelledLateCallback()
        let geometry = try await verifyStreamingHistoryGeometry()

        return [
            Interaction(id: "reading-history", result: "passed", evidence: "ScrollFollowState holds position, counts six new items, and resumes only on explicit return."),
            Interaction(id: "selection-pinning", result: "passed", evidence: "Selecting text pins the viewport through new content; explicit return releases it."),
            Interaction(id: "scroll-navigation-input", result: "passed", evidence: "Arrow/Page navigation keys and pointer-inside hit testing are classified; unrelated keys and outside points are ignored."),
            Interaction(id: "keyboard-focus", result: "rendered", evidence: "The live pause control receives keyboard focus in the native SwiftUI capture."),
            Interaction(id: "evidence-overlay", result: "rendered", evidence: "The live answer evidence popover is opened in the native SwiftUI capture."),
            Interaction(id: "busy-focus", result: "rendered", evidence: "The focus-busy capture shows the Stop control's focus ring while generation is active; focus is a synthetic preview state."),
            Interaction(id: "selection-focus", result: "rendered", evidence: "The selected-focus capture shows the return-to-live action's focus ring; focus is a synthetic preview state."),
            cancellation,
            geometry,
            Interaction(id: "component-sample-sheet", result: "passed", evidence: "Captured the native component sample sheet at synthetic-component-samples-light-1200.png."),
            Interaction(id: "accessibility-text-size", result: "limited", evidence: "The renderer records an accessibility3 environment override, but native macOS text remains the same size; app-level font enlargement is not implemented.")
        ]
    }

    private static func verifyCancelledLateCallback() throws -> Interaction {
        let coordinator = AnswerCoordinator()
        let id = UUID()
        let partial = "已生成的部分：先看命中率和 P95 延迟。"
        coordinator.begin(id: id, question: "如何验证这次缓存改动没有引入回归？")
        coordinator.draft(id: id, text: partial)
        coordinator.cancel()
        coordinator.finish(id: id, text: "过晚的结果不应覆盖已保留的部分回答。")
        guard coordinator.state?.phase == .cancelled, coordinator.state?.text == partial else {
            throw SelfTest.Failure("late answer completion changed the cancelled partial result")
        }
        return Interaction(id: "cancel-late-callback", result: "passed",
                           evidence: "A synthetic finish after cancellation was ignored; the cancelled phase and partial text remained unchanged.")
    }

    private static func verifyStreamingHistoryGeometry() async throws -> Interaction {
        let state = AppState.shared
        state.populateDemo()
        configure(state, for: .streamingHistory)
        state.partialTranscript = "当前识别：缓存命中率维持在 92%。"

        let collector = LiveSessionAcceptanceFrameCollector()
        let root = LiveSessionView()
            .overlay(alignment: .top) { ErrorBanner() }
            .environment(state)
            .environment(\.liveSessionAcceptanceScenario, .streamingHistory)
            .environment(\.locale, Locale(identifier: "zh-Hans"))
            .coordinateSpace(name: LiveSessionAcceptanceLayout.coordinateSpace)
            .onPreferenceChange(LiveSessionAcceptanceFramePreferenceKey.self) { collector.record($0) }
            .frame(width: 1_200, height: CGFloat(height))
        let window = NSWindow(contentRect: NSRect(x: 0, y: 0, width: 1_200, height: height),
                              styleMask: [.borderless], backing: .buffered, defer: false)
        window.isReleasedWhenClosed = false
        window.ignoresMouseEvents = true
        window.contentView = NSHostingView(rootView: root)
        window.minSize = .zero
        window.orderFrontRegardless()
        defer { window.orderOut(nil); window.close() }

        try await Task.sleep(for: .milliseconds(180))
        window.contentView?.layoutSubtreeIfNeeded()
        try await Task.sleep(for: .milliseconds(80))
        let before = collector.frames
        guard let composerBefore = before["composer"],
              let primaryBefore = before["primary-action"],
              let transcriptBefore = before["partial-transcript"] else {
            throw SelfTest.Failure("streaming geometry probe did not observe composer, primary action, and partial transcript")
        }

        state.partialTranscript = String(repeating: "如果发布窗口需要延长，我们会先核对回滚指标，再决定是否调整；缓存治理继续比较命中率、P95 延迟和错误率。", count: 8)
        try await Task.sleep(for: .milliseconds(180))
        window.contentView?.layoutSubtreeIfNeeded()
        try await Task.sleep(for: .milliseconds(80))
        let after = collector.frames
        guard let composerAfter = after["composer"],
              let primaryAfter = after["primary-action"],
              let transcriptAfter = after["partial-transcript"] else {
            throw SelfTest.Failure("streaming geometry probe lost a measured view after transcript growth")
        }

        let composerDrift = frameDrift(composerBefore, composerAfter)
        let primaryDrift = frameDrift(primaryBefore, primaryAfter)
        guard composerDrift <= 0.5, primaryDrift <= 0.5 else {
            throw SelfTest.Failure("composer/primary action moved during transcript growth: composer \(composerDrift) pt, action \(primaryDrift) pt")
        }
        guard transcriptAfter.height > transcriptBefore.height + 10 else {
            throw SelfTest.Failure("streaming fixture did not increase the rendered partial transcript height")
        }
        return Interaction(id: "streaming-history-layout", result: "passed",
                           evidence: "Expanded synthetic partial text by \(Int(transcriptAfter.height - transcriptBefore.height)) pt; composer drift \(composerDrift) pt and primary-action drift \(primaryDrift) pt (≤0.5 pt).")
    }

    private static func frameDrift(_ before: CGRect, _ after: CGRect) -> CGFloat {
        [abs(before.minX - after.minX), abs(before.minY - after.minY),
         abs(before.width - after.width), abs(before.height - after.height)].max() ?? 0
    }

    private static func renderReport(_ manifest: Manifest, to url: URL) throws {
        let scenarios = LiveSessionAcceptanceScenario.allCases.map(\.rawValue).joined(separator: ", ")
        let interactionRows = manifest.interactions.map { "| `\($0.id)` | \($0.result) | \($0.evidence) |" }.joined(separator: "\n")
        let limitations = manifest.limitations.map { "- \($0)" }.joined(separator: "\n")
        let report = """
        # T07 Live Session Acceptance Evidence

        Generated at: \(ISO8601DateFormatter().string(from: manifest.generatedAt))  
        Source revision: `\(manifest.sourceRevision)`  
        Evidence: \(manifest.matrixCaptureCount) native SwiftUI/AppKit matrix PNG captures across \(manifest.scenarioCount) scenarios; all meeting content and statuses are synthetic. No live audio, ASR, model request, network access, archive write, signing, installation, or TCC operation is used.

        ## Matrix

        - Appearance: light, dark
        - Widths: 1200 pt, current app minimum 960 pt, 900 pt below-minimum stress render
        - Height: 780 pt
        - UI locale: `zh-Hans`
        - Scenarios (\(manifest.scenarioCount)): \(scenarios)
        - Accessibility override: `large-text` records `DynamicTypeSize.accessibility3`; other cases use `.large`. On macOS this environment value does not resize text, so these screenshots do not validate enlarged type.
        - Long title: `\(Self.longTitle)` is used for every capture.

        Capture file names are `synthetic-<scenario>-<appearance>-<width>.png`; `manifest.json` maps every row to its PNG, point size, pixel size, and synthetic-state constraints.

        ## Component sample sheet

        ![Native component state samples](\(manifest.componentSample.path))

        This additional capture is separate from the \(manifest.matrixCaptureCount)-image scenario matrix.

        ## Interaction checks

        | Check | Result | Evidence |
        |---|---|---|
        \(interactionRows)

        ## Known acceptance gaps and limits

        \(limitations)

        This is a focused T07 visual/interaction slice; it does not claim complete 12.4 permission, IME, VoiceOver, or device coverage. The baseline README screenshot workflow writes only `docs/screenshots`; this matrix writes to its requested output directory and does not touch those files.
        """
        try report.write(to: url, atomically: true, encoding: .utf8)
    }
}
