import Foundation
import InterviewHackerCore
import SwiftUI

struct TranscriptEntry: Identifiable, Codable, Equatable {
    var id = UUID()
    var date = Date()
    var source: String
    var text: String
    var isFinal: Bool
    var startedAt: Date?
    var endedAt: Date?
    var translatedText: String? = nil
    /// Microphone line that looked like speaker echo: shown dimmed, kept out of answers
    /// and records until the user marks it as theirs.
    var suspectedEcho: Bool?
    /// In-person meeting: the microphone heard everyone in the room ("现场").
    var onSite: Bool?
    /// Which voice said it (the other side or the room), when 区分对方的每个人 is on; a
    /// meeting-local id. Never set for the user's own lines.
    var voice: Int?
    var roleLabel: String {
        if onSite == true { return "现场 · 麦克风" }
        return source == CaptureSource.microphone.rawValue ? "我 · 麦克风" : "对方 · 系统声音"
    }
    var contextualText: String { "[\(roleLabel)] \(text)" }
    var isMe: Bool { source == CaptureSource.microphone.rawValue && onSite != true }
    var speaker: SessionSpeaker { onSite == true ? .room : (isMe ? .me : .remote) }
    /// Someone other than the user may be speaking (the other side, or the room).
    var isOtherSide: Bool { source == CaptureSource.system.rawValue || onSite == true }
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
    var completion: AnswerCompletion? = nil
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

/// Sidebar sections. The live meeting is not a section: while a meeting runs the
/// window switches to focus mode and the sidebar is hidden.
enum AppSection: String, CaseIterable, Identifiable {
    case home, meetings, library, settings

    var id: String { rawValue }

    /// The three everyday sections; settings sits at the bottom of the sidebar.
    static let primary: [AppSection] = [.home, .meetings, .library]

    var title: LocalizedStringKey {
        switch self {
        case .home: "首页"
        case .meetings: "所有记录"
        case .library: "项目与课程"
        case .settings: "设置"
        }
    }

    var symbol: String {
        switch self {
        case .home: "house"
        case .meetings: "list.bullet"
        case .library: "folder"
        case .settings: "gearshape"
        }
    }

    /// Accepts the section names of older demo scripts (assistant, materials, history).
    init?(demoName: String) {
        switch demoName {
        case "assistant", "live", "prepare": self = .home
        case "materials": self = .library
        case "history": self = .meetings
        default:
            guard let section = AppSection(rawValue: demoName) else { return nil }
            self = section
        }
    }
}

enum CaptureSource: String, Hashable, Sendable {
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
