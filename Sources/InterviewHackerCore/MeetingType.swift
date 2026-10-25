import Foundation

// MARK: - Meeting type defaults

/// What the live page does by default: suggest answers, or only take notes.
public enum LiveMode: String, Codable, CaseIterable, Sendable {
    /// 提词: detected questions are answered automatically.
    case prompting
    /// 只记录: the transcript only; ⌘⇧↩ still answers on demand.
    case notesOnly
}

/// Built-in notes templates (02-scenarios-and-flows.md, "按场景的模板").
public enum NotesTemplateKind: String, Codable, CaseIterable, Sendable, Identifiable {
    case interview, academic, work, client, lecture, discussion, general
    public var id: String { rawValue }
}

/// The defaults a meeting type sets, so nobody has to touch the switches.
public struct MeetingTypeDefaults: Sendable, Equatable {
    public var liveMode: LiveMode
    public var style: AnswerStyle
    public var template: NotesTemplateKind
    /// Presentations follow the slides on screen (left column shows the talk).
    public var followsSlides: Bool

    public init(liveMode: LiveMode, style: AnswerStyle, template: NotesTemplateKind, followsSlides: Bool = false) {
        self.liveMode = liveMode; self.style = style; self.template = template; self.followsSlides = followsSlides
    }

    /// The table of 02-scenarios-and-flows.md, "会议类型". A custom type uses its base
    /// type (see `Workspace.effectiveMode`); on its own it behaves like general.
    public static func `for`(_ mode: SessionMode) -> MeetingTypeDefaults {
        switch mode {
        case .general, .custom: MeetingTypeDefaults(liveMode: .notesOnly, style: .concise, template: .general)
        case .interview: MeetingTypeDefaults(liveMode: .prompting, style: .concise, template: .interview)
        case .academic: MeetingTypeDefaults(liveMode: .prompting, style: .concise, template: .academic, followsSlides: true)
        case .labMeeting: MeetingTypeDefaults(liveMode: .notesOnly, style: .detailed, template: .academic)
        case .meeting: MeetingTypeDefaults(liveMode: .notesOnly, style: .concise, template: .work)
        case .client: MeetingTypeDefaults(liveMode: .prompting, style: .concise, template: .client)
        case .lesson: MeetingTypeDefaults(liveMode: .notesOnly, style: .detailed, template: .lecture)
        case .discussion: MeetingTypeDefaults(liveMode: .notesOnly, style: .concise, template: .discussion)
        }
    }
}

// MARK: - Guessing the type from a title

/// Picks a type from words in a calendar or typed title; general when nothing fits.
public enum MeetingTypeGuesser {
    /// Checked in order: the first type with a matching keyword wins.
    static let keywords: [(SessionMode, [String])] = [
        (.interview, ["面试", "一面", "二面", "三面", "四面", "终面", "hr面", "interview", "onsite", "phone screen"]),
        (.labMeeting, ["组会", "group meeting", "lab meeting"]),
        (.academic, ["答辩", "defense", "defence", "汇报", "报告会", "presentation", "talk"]),
        (.client, ["客户", "client", "customer"]),
        (.lesson, ["讲座", "课程", "上课", "lecture", "class", "course"]),
        (.discussion, ["小组讨论", "讨论", "discussion", "brainstorm", "头脑风暴"]),
        (.meeting, ["例会", "周会", "站会", "sync", "standup", "stand-up", "1:1", "one on one", "review"]),
    ]

    public static func guess(_ title: String) -> SessionMode {
        let text = title.lowercased()
        guard !text.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty else { return .general }
        for (mode, words) in keywords where words.contains(where: { text.contains($0) }) {
            return mode
        }
        return .general
    }
}

// MARK: - Series ("系列")

/// Finds the series (workspace) a new meeting continues: "字节跳动 · 后端二面"
/// belongs to "字节跳动 · 后端" (03 / 04 docs, "接着上一场").
public enum SeriesMatcher {
    /// Round markers that differ between meetings of the same series.
    static let roundPatterns = [
        #"第\s*[0-9一二三四五六七八九十]+\s*(轮|场|次|期|周|面)"#,
        #"[0-9一二三四五六七八九十]+\s*面"#,
        #"(终面|hr\s*面|复试|初试)"#,
        #"\bround\s*[0-9]+\b"#,
        #"\b(r|#)[0-9]+\b"#,
        #"\bweek\s*[0-9]+\b"#,
        #"\b(first|second|third|final)\s+round\b"#,
        #"(面试|interview)"#,
    ]

    /// The title without round markers ("一面", "round 2", "第 3 周").
    public static func baseTitle(_ title: String) -> String {
        var text = title.lowercased()
        for pattern in roundPatterns {
            text = text.replacingOccurrences(of: pattern, with: " ", options: .regularExpression)
        }
        return text.trimmingCharacters(in: .whitespacesAndNewlines)
    }

    /// Similarity of two titles once round markers are gone (0…1).
    public static func similarity(_ title: String, _ series: String) -> Double {
        let left = baseTitle(title), right = baseTitle(series)
        let letters = { (text: String) in text.filter { $0.isLetter || $0.isNumber } }
        guard letters(left).count >= 2, letters(right).count >= 2 else { return 0 }
        return KnowledgeMerger.similarity(left, right)
    }

    public struct Candidate: Sendable, Equatable {
        public var id: UUID
        public var name: String
        public var mode: SessionMode
        /// When the series was last used (latest meeting or edit).
        public var lastUsed: Date
        public init(id: UUID, name: String, mode: SessionMode, lastUsed: Date) {
            self.id = id; self.name = name; self.mode = mode; self.lastUsed = lastUsed
        }
    }

    /// The best matching series, or nil. A series of the same type wins ties, then
    /// the one used most recently.
    public static func bestMatch(title: String, mode: SessionMode? = nil, candidates: [Candidate], minimum: Double = 0.5) -> UUID? {
        let scored = candidates.map { candidate -> (Candidate, Double) in
            var score = similarity(title, candidate.name)
            if let mode, mode != .general, candidate.mode == mode { score += 0.05 }
            return (candidate, score)
        }
        .filter { $0.1 >= minimum }
        return scored.max { lhs, rhs in
            lhs.1 == rhs.1 ? lhs.0.lastUsed < rhs.0.lastUsed : lhs.1 < rhs.1
        }?.0.id
    }
}
