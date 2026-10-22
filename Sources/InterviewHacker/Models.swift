import Foundation
import InterviewHackerCore

struct TranscriptEntry: Identifiable, Codable, Equatable {
    var id = UUID()
    var date = Date()
    var source: String
    var text: String
    var isFinal: Bool
    var startedAt: Date?
    var endedAt: Date?
    /// Microphone line that looked like speaker echo: shown dimmed, kept out of answers
    /// and records until the user marks it as theirs.
    var suspectedEcho: Bool?
    var roleLabel: String { source == CaptureSource.microphone.rawValue ? "我 · 麦克风" : "对方 · 系统声音" }
    var contextualText: String { "[\(roleLabel)] \(text)" }
    var isMe: Bool { source == CaptureSource.microphone.rawValue }
    var speaker: SessionSpeaker { isMe ? .me : .remote }
}

/// Format of the single `sessions.json` written by earlier versions (read once for migration).
struct SavedSession: Codable, Identifiable {
    var id = UUID()
    var startedAt: Date
    var endedAt: Date
    var entries: [TranscriptEntry]
    var answers: [String]
    /// Question/answer pairs (absent in sessions saved by older versions).
    var records: [AnswerRecord]?
    var mode: String?
}

/// One generated answer, kept for history navigation, the phone view and export.
struct AnswerRecord: Identifiable, Codable, Equatable {
    var id = UUID()
    var date = Date()
    var question: String
    var answer: String
    var model: String
    var seconds: Double?
    var firstTokenSeconds: Double?
    var automatic: Bool
    /// Screenshot that was sent with the question, relative to the session
    /// folder ("screenshots/…"). Nil when the answer had no screenshot.
    var screenshotPath: String?
    var webAnswer: String? = nil
    var webSources: [WebResult]? = nil
    var evidence: EvidenceBundle? = nil
}

struct CaptureTarget: Identifiable, Hashable {
    var id: String
    var title: String
}

struct ScreenshotWindow: Identifiable, Hashable, Sendable {
    var id: UInt32
    var title: String
    var owner: String
}

struct ScreenshotDisplay: Identifiable, Hashable, Sendable {
    var id: UInt32
    var title: String
    var width: Int
    var height: Int
}

enum AppSection: String, CaseIterable, Identifiable {
    case assistant = "助手"
    case materials = "场景"
    case history = "记录"
    case settings = "设置"

    var id: String { rawValue }
}

enum CaptureSource: String, Hashable {
    case system = "系统音频"
    case microphone = "麦克风"
}

enum AppFailure: LocalizedError {
    case codexUnavailable
    case codexNotSignedIn
    case modelMissing
    case capture(String)
    case transcription(String)
    case screenshot(String)

    var errorDescription: String? {
        switch self {
        case .codexUnavailable:
            return "没有找到 Codex CLI。请先安装并打开 ChatGPT/Codex 桌面客户端。"
        case .codexNotSignedIn:
            return "需要使用 ChatGPT 账号登录本机 Codex CLI。此应用不使用 API Key；请在 ChatGPT/Codex 客户端登录后重试。"
        case .modelMissing:
            return "Zipformer 中英双语模型尚未就绪。请在设置中下载完整模型文件组。"
        case let .capture(message), let .transcription(message), let .screenshot(message):
            return message
        }
    }
}
