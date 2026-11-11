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
    /// `academic` keeps its raw value for stored notes; it is the lab-meeting / research-discussion template.
    case interview, academic, work, client, lecture, discussion, general, groupProject, talk
    public var id: String { rawValue }
    /// Templates offered in settings and meeting notes.
    public static var selectable: [NotesTemplateKind] {
        [.general, .interview, .talk, .academic, .groupProject, .client, .work, .lecture, .discussion]
    }
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
        case .academic: MeetingTypeDefaults(liveMode: .prompting, style: .concise, template: .talk, followsSlides: true)
        case .labMeeting: MeetingTypeDefaults(liveMode: .notesOnly, style: .detailed, template: .academic)
        case .meeting: MeetingTypeDefaults(liveMode: .notesOnly, style: .concise, template: .work)
        case .client: MeetingTypeDefaults(liveMode: .prompting, style: .concise, template: .client)
        case .lesson: MeetingTypeDefaults(liveMode: .notesOnly, style: .detailed, template: .lecture)
        case .discussion: MeetingTypeDefaults(liveMode: .notesOnly, style: .concise, template: .discussion)
        case .groupProject: MeetingTypeDefaults(liveMode: .notesOnly, style: .concise, template: .groupProject)
        }
    }
}

// MARK: - Guessing the type from a title

/// Picks a type from words in a calendar or typed title; general when nothing fits.
public enum MeetingTypeGuesser {
    /// Checked in order: the first type with a matching keyword wins. The more specific
    /// scenes come first: "课程设计" is a group project before it is a course, and
    /// "client talk" is a client call before it is a talk.
    static let keywords: [(SessionMode, [String])] = [
        (.interview, ["面试", "一面", "二面", "三面", "四面", "终面", "hr面", "interview", "onsite", "phone screen"]),
        (.labMeeting, ["组会", "group meeting", "lab meeting"]),
        (.groupProject, ["小组作业", "大作业", "课程设计", "课设", "团队作业", "group project", "group assignment", "team project", "course project", "capstone"]),
        (.client, ["客户", "client", "customer"]),
        (.academic, ["答辩", "defense", "defence", "汇报", "报告会", "presentation", "talk"]),
        (.lesson, ["讲座", "课程", "上课", "lecture", "class", "course"]),
        (.discussion, ["小组讨论", "讨论", "discussion", "brainstorm", "头脑风暴"]),
        (.meeting, ["例会", "周会", "站会", "小组会", "项目组会", "项目会议", "sync", "standup", "stand-up", "1:1", "one on one", "review"]),
    ]

    public static func guess(_ title: String) -> SessionMode {
        let text = title.lowercased()
        guard !text.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty else { return .general }
        for (mode, words) in keywords where words.contains(where: { matches($0, in: text) }) {
            return mode
        }
        return .general
    }

    private static func matches(_ word: String, in text: String) -> Bool {
        if word == "组会" {
            // 小组会议 and 项目组会议 are ordinary meetings, not a lab's 组会.
            var rest = text[...]
            while let range = rest.range(of: word) {
                let before = text[..<range.lowerBound]
                if !before.hasSuffix("小") && !before.hasSuffix("项目") { return true }
                rest = text[range.upperBound...]
            }
            return false
        }
        if word.allSatisfy({ $0.isASCII && ($0.isLetter || $0 == " " || $0 == "-") }) {
            // English words match whole words only ("class" is not in "classic").
            let pattern = "(?<![a-z0-9])" + NSRegularExpression.escapedPattern(for: word) + "(?:s|es)?(?![a-z0-9])"
            return text.range(of: pattern, options: .regularExpression) != nil
        }
        return text.contains(word)
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
        #"第\s*[0-9一二三四五六七八九十]+\s*(讲|课|节|章|季度|季)"#,
        #"\bq[1-4]\b"#,
        #"\b(first|second|third|final)\s+round\b"#,
        #"(面试|interview)"#,
    ]

    /// Numbered units whose noun is part of the series name: "Lecture 4" → "lecture".
    static let numberedPatterns = [
        #"\b(lecture|lec|session|sprint|seminar|workshop|class|part)\s*#?[0-9]+\b"#,
    ]

    /// The title without round markers ("一面", "round 2", "第 3 周", "第 3 讲", "Lecture 4", "Q3").
    public static func baseTitle(_ title: String) -> String {
        var text = title.lowercased()
        for pattern in numberedPatterns {
            text = text.replacingOccurrences(of: pattern, with: "$1", options: .regularExpression)
        }
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
