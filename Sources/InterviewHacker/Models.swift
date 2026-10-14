import Foundation

struct TranscriptEntry: Identifiable, Codable, Equatable {
    var id = UUID()
    var date = Date()
    var source: String
    var text: String
    var isFinal: Bool
    var startedAt: Date?
    var endedAt: Date?
}

struct SavedSession: Codable {
    var id = UUID()
    var startedAt: Date
    var endedAt: Date
    var entries: [TranscriptEntry]
    var answers: [String]
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
    case settings = "设置"
    case history = "记录"

    var id: String { rawValue }
}

enum CaptureSource: String {
    case system = "系统音频"
    case microphone = "麦克风"
}

enum AppFailure: LocalizedError {
    case codexUnavailable
    case codexNotSignedIn
    case noTranscriber
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
        case .noTranscriber:
            return "应用内 Whisper 运行时不可用。请重新构建或安装 InterviewHacker 应用包。"
        case .modelMissing:
            return "本地 Whisper 模型尚未下载。请在设置中确认并下载约 547 MiB 的模型。"
        case let .capture(message), let .transcription(message), let .screenshot(message):
            return message
        }
    }
}
