import AppKit
import Foundation
import InterviewHackerCore

/// Screenshot / demo mode. Launch with `INTERVIEWHACKER_DEMO=1` to show a realistic
/// sample session without capturing audio or calling any model. Used by the
/// screenshots workflow to render README images on a real macOS runner.
///
/// Environment:
/// - `INTERVIEWHACKER_DEMO_SECTION`: assistant | materials | settings | history
/// - `INTERVIEWHACKER_DEMO_APPEARANCE`: light | dark
/// - `INTERVIEWHACKER_DEMO_OUT`: file that receives the window numbers as JSON
enum DemoMode {
    static var isEnabled: Bool { ProcessInfo.processInfo.environment["INTERVIEWHACKER_DEMO"] == "1" }

    @MainActor
    static func apply() {
        guard isEnabled else { return }
        let environment = ProcessInfo.processInfo.environment
        let state = AppState.shared
        // Screenshots must be able to see the windows.
        state.capturePrivacy = false
        TeleprompterController.shared.applyPrivacy(false)
        state.appearance = environment["INTERVIEWHACKER_DEMO_APPEARANCE"] ?? "dark"
        state.applyAppearance()
        state.section = AppSection.allCases.first { "\($0)" == environment["INTERVIEWHACKER_DEMO_SECTION"] } ?? .assistant
        state.populateDemo()

        DispatchQueue.main.asyncAfter(deadline: .now() + 0.5) {
            NSApp.setActivationPolicy(.regular)
            NSApp.activate(ignoringOtherApps: true)
            if let main = NSApp.windows.first(where: { !($0 is NSPanel) }) {
                main.setFrame(NSRect(x: 80, y: 80, width: 760, height: 900), display: true)
                main.makeKeyAndOrderFront(nil)
            }
            TeleprompterController.shared.show()
            if let panel = NSApp.windows.first(where: { $0 is NSPanel && $0.isVisible }) {
                panel.setFrame(NSRect(x: 900, y: 600, width: 680, height: 300), display: true)
            }
        }
        DispatchQueue.main.asyncAfter(deadline: .now() + 3) {
            guard let path = environment["INTERVIEWHACKER_DEMO_OUT"] else { return }
            let main = NSApp.windows.first(where: { !($0 is NSPanel) && $0.isVisible })?.windowNumber ?? 0
            let panel = NSApp.windows.first(where: { $0 is NSPanel && $0.isVisible })?.windowNumber ?? 0
            try? JSONSerialization.data(withJSONObject: ["main": main, "teleprompter": panel])
                .write(to: URL(fileURLWithPath: path))
        }
    }
}

@MainActor
extension AppState {
    func populateDemo() {
        let now = Date()
        func entry(_ secondsAgo: Double, _ source: CaptureSource, _ text: String) -> TranscriptEntry {
            let date = now.addingTimeInterval(-secondsAgo)
            return TranscriptEntry(date: date, source: source.rawValue, text: text, isFinal: true, startedAt: date, endedAt: date.addingTimeInterval(3))
        }
        capturePermissionGranted = true
        captureTargets = [CaptureTarget(id: "window:1", title: "Zoom · 后端工程师二面"), CaptureTarget(id: "display:1", title: "整个屏幕 · 3024×1964")]
        selectedTargetID = "window:1"
        sessionMode = .interview
        isRunning = true
        statusText = "正在聆听 · 启动 0.4 s"
        transcriptionStatus = "本地识别中"
        lastRTF = 0.31
        lastFirstTokenSeconds = 0.9
        lastAnswerSeconds = 4.2
        lastAnswerModel = "gpt-5.5-mini"
        entries = [
            entry(95, .system, "好，我们先聊聊你最近做的项目吧，简单介绍一下你负责的部分。"),
            entry(80, .microphone, "我主要负责订单服务的缓存层，把核心查询从数据库迁移到 Redis。"),
            entry(52, .system, "那数据库和缓存同时更新的时候，你们是怎么保证一致性的？"),
            entry(20, .microphone, "我们用的是先更新数据库、再删除缓存的方式。"),
        ]
        partialTranscript = "如果删除缓存失败了怎么办"
        documents = [
            ContextDocument(name: "简历-2026.pdf", pages: ["订单服务缓存层，Redis，延迟双删，Canal 订阅 binlog"]),
            ContextDocument(name: "岗位JD-后端开发.md", pages: ["高并发、分布式缓存、消息队列"]),
        ]
        answerPreferences = SessionPreset.templatePreferences(for: .interview)
        var demoTracker = QuestionCandidateTracker(mode: .interview)
        if case let .updated(candidate) = demoTracker.ingest("如果删除缓存失败了怎么办", isFinal: false, utteranceKey: "demo") {
            liveCandidate = candidate
        }
        let question = "那数据库和缓存同时更新的时候，你们是怎么保证一致性的？"
        let reply = """
        我们采用**先更新数据库、再删除缓存**，并用 binlog 订阅兜底，保证最终一致。

        - **Cache Aside**：写库成功后删除缓存，读请求未命中时回源并回填，避免并发写导致脏数据。
        - **删除失败兜底**：删除失败的 key 进入重试队列；同时用 **Canal 订阅 binlog** 异步再删一次。
        - **并发读写窗口**：对热点 key 做**延迟双删**（约 500 ms），把旧值被回填的概率降到很低。
        - **取舍**：这是最终一致方案，对强一致场景（如余额）直接读库或加分布式锁。
        """
        currentQuestion = question
        answer = reply
        answerHistory = [
            AnswerRecord(question: "简单介绍一下你负责的部分", answer: "我负责订单服务的缓存层……", model: "gpt-5.5-mini", seconds: 3.1, firstTokenSeconds: 0.8, automatic: true),
            AnswerRecord(question: question, answer: reply, model: "gpt-5.5-mini", seconds: 4.2, firstTokenSeconds: 0.9, automatic: true),
        ]
        savedSessions = [SavedSession(startedAt: now.addingTimeInterval(-86_400), endedAt: now.addingTimeInterval(-84_000), entries: entries, answers: [reply],
                                      records: answerHistory, mode: "interview")]
    }
}
