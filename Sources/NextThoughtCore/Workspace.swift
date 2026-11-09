import CryptoKit
import Foundation

// MARK: - Mode profiles

/// One structured input of a mode (“应聘公司”, “报告题目”…).
public struct ModeField: Sendable, Equatable, Identifiable {
    public var id: String
    public var title: String
    public var placeholder: String
    /// Multi-line editor instead of a single line.
    public var long: Bool
    public init(_ id: String, _ title: String, _ placeholder: String, long: Bool = false) {
        self.id = id; self.title = title; self.placeholder = placeholder; self.long = long
    }
}

/// What each mode asks for: fields, material slots, default answer requirements.
public struct ModeProfile: Sendable {
    public var mode: SessionMode
    public var role: String
    public var fields: [ModeField]
    /// File categories offered when adding materials.
    public var documentCategories: [String]
    public var templatePreferences: String
    /// Extra instructions for the after-session review.
    public var reviewFocus: String

    public func matchesDefaultTemplatePreferences(_ value: String) -> Bool {
        if value.isEmpty || value == templatePreferences { return true }
        switch mode {
        case .meeting:
            return value == "我是会议参与者。回答聚焦决策、风险和下一步行动，给出可以直接说出口的简短发言；涉及数据时标注来源；守住资料中我方的立场与底线，不确定时建议会后确认。"
        case .client:
            return value == "我是我方代表，正在和客户沟通。回答聚焦客户的需求和顾虑，给出可以直接说出口的简短回应；涉及价格、时间和承诺时以资料为准，不确定的说“会后确认”，不替公司做资料里没有的承诺。"
        case .general, .custom:
            return value == "直接回答当前问题：先给一句可以说出口的结论，再给 2–3 个要点；涉及资料时标注来源，不确定时说明。"
        default:
            return false
        }
    }

    public static func `for`(_ mode: SessionMode) -> ModeProfile {
        switch mode {
        case .interview:
            return ModeProfile(
                mode: mode, role: "候选人",
                fields: [
                    ModeField("company", "应聘公司", "公司名称、业务、近期新闻、团队情况"),
                    ModeField("position", "岗位", "岗位名称、级别、JD 要点"),
                    ModeField("interviewer", "面试官 / HR", "面试官姓名、背景、风格；HR 关注点"),
                    ModeField("highlights", "我的亮点", "最想让对方记住的 3 个经历或数字", long: true),
                    ModeField("salary", "期望与底线", "薪资期望、到岗时间、不能接受的条件"),
                ],
                documentCategories: ["简历", "岗位 JD", "公司信息", "HR / 面试官", "八股 / 面经", "项目资料", "其他"],
                templatePreferences: "我是候选人。用第一人称、口语化回答，先给一句结论，再给 2–3 个要点；行为题用 STAR 结构，只使用我资料中的真实经历，没有的用【待补充】标出；技术题给关键原理与取舍；面经里准备过的题优先按我准备的答案说。",
                reviewFocus: "按问题逐条评价我的回答：答到了什么、遗漏了什么、更好的说法是什么；标出面试官追问的薄弱点和下一轮需要准备的内容。")
        case .academic:
            return ModeProfile(
                mode: mode, role: "报告人",
                fields: [
                    ModeField("title", "报告题目", "论文 / 报告标题"),
                    ModeField("venue", "会议与场次", "会议名称、分会场、报告时长、主持人"),
                    ModeField("claims", "核心论点", "要讲清楚的贡献、方法和关键结果", long: true),
                    ModeField("limitations", "已知局限", "实验不足、假设、未来工作；被问到时怎么回应", long: true),
                    ModeField("expected", "预计会被问的问题", "审稿意见、同行常问的问题", long: true),
                ],
                documentCategories: ["论文", "PPT / 幻灯片", "讲稿", "补充材料 / 附录", "相关工作", "审稿意见", "其他"],
                templatePreferences: "我是报告人，正在回答听众或主持人的提问。先用一句话正面回答，再给依据（论文中的方法、实验、数据并标注页码）；遇到质疑先承认合理之处再说明设计理由；不知道的诚实说明并给出后续计划；中英文提问分别用对应语言回答。",
                reviewFocus: "列出听众和主持人提出的每个问题及我的回答质量；指出讲解中不清楚、被反复追问的部分；给出论文或 PPT 需要补充的内容。")
        case .meeting:
            return ModeProfile(
                mode: mode, role: "会议参与者",
                fields: [
                    ModeField("counterpart", "对方公司 / 参会人", "公司、职位、决策人、关注点"),
                    ModeField("goal", "会议目标", "这次会议要达成的结果"),
                    ModeField("position", "我方立场与底线", "可以让步的、不能让步的", long: true),
                    ModeField("numbers", "关键数字", "价格、预算、指标、时间节点", long: true),
                ],
                documentCategories: ["公司 / 产品资料", "对方信息", "议程", "报价 / 合同", "会议纪要", "其他"],
                templatePreferences: "我是会议参与者。回答聚焦决策、风险和下一步行动，给出简洁、自然的参考内容；涉及数据时标注来源；守住资料中我方的立场与底线，不确定时建议会后确认。",
                reviewFocus: "整理决定事项、每个人的承诺与截止时间、未解决的分歧；评价我方谈判表现和下次需要准备的数据。")
        case .lesson:
            return ModeProfile(
                mode: mode, role: "学生",
                fields: [
                    ModeField("course", "课程", "课程名称、章节、老师"),
                    ModeField("goals", "学习目标", "这节课需要掌握的内容", long: true),
                ],
                documentCategories: ["课件", "笔记", "作业 / 题目", "教材", "其他"],
                templatePreferences: "我是学生。先用一句话解释概念，再给推导或例子；公式用 LaTeX 行内格式；指出与课件对应的页码。",
                reviewFocus: "整理本节课的知识点、老师强调的重点和布置的任务；列出我还没弄懂的地方。")
        case .labMeeting:
            return ModeProfile(
                mode: mode, role: "组会汇报人",
                fields: [
                    ModeField("progress", "本周进展", "做了哪些实验、读了哪些论文、结果如何", long: true),
                    ModeField("problems", "卡住的地方", "想请导师或同学帮忙判断的问题", long: true),
                    ModeField("next", "下一步计划", "下周打算做什么"),
                ],
                documentCategories: ["进展 / 讲稿", "论文", "实验数据", "相关工作", "其他"],
                templatePreferences: "我在组会上汇报研究进展。回答导师或同学的追问时先给结论，再说依据（实验、数据、论文，标注来源）；结果不好时如实说明原因和下一步；不确定的地方说明还需要做什么实验来确认。",
                reviewFocus: "整理讨论过的问题、讨论得不够透彻的地方、导师的意见和要求，以及论文和实验的下一步。")
        case .client:
            return ModeProfile(
                mode: mode, role: "我方代表",
                fields: [
                    ModeField("counterpart", "客户 / 对方", "公司、参会人、决策人、关注点"),
                    ModeField("goal", "沟通目标", "这次沟通要达成的结果"),
                    ModeField("offer", "产品与报价", "我们能提供什么、价格和时间", long: true),
                    ModeField("concerns", "客户可能的顾虑", "预算、风险、竞品，以及怎么回应", long: true),
                ],
                documentCategories: ["产品资料", "报价 / 合同", "客户信息", "会议纪要", "其他"],
                templatePreferences: "我是我方代表，正在和客户沟通。回答聚焦客户的需求和顾虑，给出简洁、自然的参考回应；涉及价格、时间和承诺时以资料为准，不确定的说“会后确认”，不替公司做资料里没有的承诺。",
                reviewFocus: "整理客户的需求和痛点、顾虑与我们的回应、我们答应的事和时间、下一步和需要内部跟进的事项。")
        case .discussion:
            return ModeProfile(
                mode: mode, role: "讨论参与者",
                fields: [
                    ModeField("topic", "讨论题目", "要讨论的问题或案例"),
                    ModeField("position", "我的观点", "我想表达的看法和依据", long: true),
                ],
                documentCategories: ["题目材料", "参考资料", "其他"],
                templatePreferences: "我在参加小组讨论。给出有依据的观点，回应别人的观点时先肯定合理之处再补充；需要时帮我总结各方观点、推动形成结论。",
                reviewFocus: "整理议题、各方观点、结论与分歧和分工。")
        case .general, .custom:
            return ModeProfile(
                mode: mode, role: "参与者",
                fields: [ModeField("context", "场景说明", "这是什么场合、对方是谁、我希望达成什么", long: true)],
                documentCategories: ["资料", "其他"],
                templatePreferences: "直接回答当前问题：先给一句简洁结论，再给 2–3 个要点；涉及资料时标注来源，不确定时说明。",
                reviewFocus: "总结讨论要点和对方关心的问题，给出下次可以做得更好的地方。")
        }
    }
}

// MARK: - Knowledge (self-iteration)

/// An immutable version of an accepted lesson. It keeps the text and source IDs
/// needed for audit or restore without needing to reopen the meeting transcript.
public struct WorkspaceLessonRevision: Codable, Sendable, Equatable, Identifiable {
    public var id: UUID
    public var revision: Int
    public var text: String
    public var sourceSessionID: UUID?
    public var sourceSessionIDs: [UUID]?
    /// Meeting IDs whose source content was deleted while this version was retained.
    public var sourceMissingSessionIDs: [UUID]?
    public var recordedAt: Date

    public init(id: UUID = UUID(), revision: Int, text: String, sourceSessionID: UUID? = nil,
                sourceSessionIDs: [UUID]? = nil, sourceMissingSessionIDs: [UUID]? = nil,
                recordedAt: Date = Date()) {
        self.id = id; self.revision = max(1, revision); self.text = text
        self.sourceSessionID = sourceSessionID; self.sourceSessionIDs = sourceSessionIDs
        self.sourceMissingSessionIDs = sourceMissingSessionIDs; self.recordedAt = recordedAt
    }
}

/// A lesson learned in a review (“结论先行”“缓存一致性要画时序”…).
public struct WorkspaceLesson: Codable, Sendable, Equatable, Identifiable {
    public var id: UUID
    public var text: String
    public var sourceSessionID: UUID?
    /// All meetings that independently support this lesson. Older workspaces may omit it.
    public var sourceSessionIDs: [UUID]?
    /// Deleted source IDs are retained only as provenance tombstones, never as transcript links.
    public var sourceMissingSessionIDs: [UUID]?
    public var createdAt: Date
    public var updatedAt: Date
    public var pinned: Bool
    public var revision: Int
    public var history: [WorkspaceLessonRevision]
    /// True only when the user explicitly chose to keep this accepted text after deleting its source.
    public var independentlyRetained: Bool

    public init(id: UUID = UUID(), text: String, sourceSessionID: UUID? = nil, sourceSessionIDs: [UUID]? = nil,
                sourceMissingSessionIDs: [UUID]? = nil, createdAt: Date = Date(), updatedAt: Date? = nil,
                pinned: Bool = false, revision: Int = 1, history: [WorkspaceLessonRevision] = [],
                independentlyRetained: Bool = false) {
        self.id = id; self.text = text; self.sourceSessionID = sourceSessionID
        self.sourceSessionIDs = sourceSessionIDs; self.sourceMissingSessionIDs = sourceMissingSessionIDs
        self.createdAt = createdAt; self.updatedAt = updatedAt ?? createdAt; self.pinned = pinned
        self.revision = max(1, revision); self.history = history; self.independentlyRetained = independentlyRetained
    }

    public var currentRevision: WorkspaceLessonRevision {
        WorkspaceLessonRevision(id: revisionIdentifier(), revision: revision,
                                text: text, sourceSessionID: sourceSessionID,
                                sourceSessionIDs: sourceSessionIDs,
                                sourceMissingSessionIDs: sourceMissingSessionIDs, recordedAt: updatedAt)
    }

    private func revisionIdentifier() -> UUID {
        let components = [id.uuidString.lowercased(), String(revision), text,
                          sourceSessionID?.uuidString.lowercased() ?? "",
                          (sourceSessionIDs ?? []).map { $0.uuidString.lowercased() }.sorted().joined(separator: ","),
                          (sourceMissingSessionIDs ?? []).map { $0.uuidString.lowercased() }.sorted().joined(separator: ","),
                          String(updatedAt.timeIntervalSince1970.bitPattern, radix: 16)]
        let material = components.map { "\($0.utf8.count):\($0)" }.joined(separator: "|")
        var bytes = Array(SHA256.hash(data: Data("workspace-lesson-revision|\(material)".utf8)).prefix(16))
        bytes[6] = (bytes[6] & 0x0F) | 0x50
        bytes[8] = (bytes[8] & 0x3F) | 0x80
        return UUID(uuid: (bytes[0], bytes[1], bytes[2], bytes[3], bytes[4], bytes[5], bytes[6], bytes[7],
                           bytes[8], bytes[9], bytes[10], bytes[11], bytes[12], bytes[13], bytes[14], bytes[15]))
    }

    private enum CodingKeys: String, CodingKey {
        case id, text, sourceSessionID, sourceSessionIDs, sourceMissingSessionIDs
        case createdAt, updatedAt, pinned, revision, history, independentlyRetained
    }

    public init(from decoder: Decoder) throws {
        let c = try decoder.container(keyedBy: CodingKeys.self)
        let createdAt = try c.decodeIfPresent(Date.self, forKey: .createdAt) ?? Date(timeIntervalSince1970: 0)
        self.init(id: try c.decodeIfPresent(UUID.self, forKey: .id) ?? UUID(),
                  text: try c.decode(String.self, forKey: .text),
                  sourceSessionID: try c.decodeIfPresent(UUID.self, forKey: .sourceSessionID),
                  sourceSessionIDs: try c.decodeIfPresent([UUID].self, forKey: .sourceSessionIDs),
                  sourceMissingSessionIDs: try c.decodeIfPresent([UUID].self, forKey: .sourceMissingSessionIDs),
                  createdAt: createdAt,
                  updatedAt: try c.decodeIfPresent(Date.self, forKey: .updatedAt) ?? createdAt,
                  pinned: try c.decodeIfPresent(Bool.self, forKey: .pinned) ?? false,
                  revision: try c.decodeIfPresent(Int.self, forKey: .revision) ?? 1,
                  history: try c.decodeIfPresent([WorkspaceLessonRevision].self, forKey: .history) ?? [],
                  independentlyRetained: try c.decodeIfPresent(Bool.self, forKey: .independentlyRetained) ?? false)
    }
}

/// Applies lesson changes while keeping the complete accepted text and provenance history.
public enum WorkspaceLessonHistory {
    @discardableResult
    public static func editText(_ text: String, on lesson: inout WorkspaceLesson,
                                now: Date = Date()) -> Bool {
        guard lesson.text != text else { return false }
        appendCurrentRevision(to: &lesson)
        lesson.text = text
        lesson.revision += 1
        lesson.updatedAt = now
        lesson.independentlyRetained = false
        return true
    }

    @discardableResult
    public static func addSources(_ newSources: Set<UUID>, to lesson: inout WorkspaceLesson,
                                  now: Date = Date()) -> Bool {
        guard !newSources.isEmpty else { return false }
        let existing = allSourceIDs(lesson)
        let additions = newSources.subtracting(existing)
        guard !additions.isEmpty else { return false }
        appendCurrentRevision(to: &lesson)
        let sources = existing.union(additions)
        lesson.sourceSessionIDs = sorted(sources)
        lesson.sourceSessionID = lesson.sourceSessionID.flatMap { sources.contains($0) ? $0 : nil } ?? sorted(sources).first
        lesson.revision += 1
        lesson.updatedAt = now
        lesson.independentlyRetained = false
        return true
    }

    /// Restore an immutable old text/source snapshot, keeping the replaced version in history.
    @discardableResult
    public static func restore(_ revisionID: UUID, on lesson: inout WorkspaceLesson,
                               now: Date = Date()) -> Bool {
        guard let revision = lesson.history.first(where: { $0.id == revisionID }),
              lesson.text != revision.text || lesson.sourceSessionID != revision.sourceSessionID
                || lesson.sourceSessionIDs != revision.sourceSessionIDs
                || lesson.sourceMissingSessionIDs != revision.sourceMissingSessionIDs else { return false }
        appendCurrentRevision(to: &lesson)
        lesson.text = revision.text
        lesson.sourceSessionID = revision.sourceSessionID
        lesson.sourceSessionIDs = revision.sourceSessionIDs
        lesson.sourceMissingSessionIDs = revision.sourceMissingSessionIDs
        lesson.revision += 1
        lesson.updatedAt = now
        lesson.independentlyRetained = allSourceIDs(revision).isEmpty
            && !(revision.sourceMissingSessionIDs ?? []).isEmpty
        return true
    }

    static func appendCurrentRevision(to lesson: inout WorkspaceLesson) {
        let current = lesson.currentRevision
        guard !lesson.history.contains(where: { $0.id == current.id }) else { return }
        lesson.history.append(current)
    }

    static func allSourceIDs(_ lesson: WorkspaceLesson) -> Set<UUID> {
        Set(lesson.sourceSessionIDs ?? []).union(lesson.sourceSessionID.map { [$0] } ?? [])
    }

    static func allSourceIDs(_ revision: WorkspaceLessonRevision) -> Set<UUID> {
        Set(revision.sourceSessionIDs ?? []).union(revision.sourceSessionID.map { [$0] } ?? [])
    }

    static func sorted(_ ids: Set<UUID>) -> [UUID] {
        ids.sorted { $0.uuidString < $1.uuidString }
    }
}

public struct WorkspaceAnswerRevision: Codable, Sendable, Equatable, Identifiable {
    public var id: UUID
    public var answer: String
    public var recordedAt: Date
    public var sourceSessionID: UUID?
    public init(id: UUID = UUID(), answer: String, recordedAt: Date = Date(), sourceSessionID: UUID? = nil) {
        self.id = id; self.answer = answer; self.recordedAt = recordedAt; self.sourceSessionID = sourceSessionID
    }
}

/// A question that came up, with the best answer so far (改进后的回答).
public struct QuestionBankItem: Codable, Sendable, Equatable, Identifiable {
    public var id: UUID
    public var question: String
    public var answer: String
    public var timesAsked: Int
    public var sourceSessionID: UUID?
    /// Unique meeting sources keep regenerated or retried reviews from counting twice.
    public var sourceSessionIDs: [UUID]?
    /// Meeting that supplied the current answer; question sources above can include many meetings.
    public var answerSourceSessionID: UUID?
    /// Previous accepted answers remain available when a reviewed candidate replaces one.
    public var answerHistory: [WorkspaceAnswerRevision]?
    /// Bumped by manual edits and accepted replacements for stale-review detection.
    public var answerRevision: Int?
    public var updatedAt: Date
    /// Legacy records used the question's source as the answer source. Once the
    /// answer has been edited, a missing answer source means it is user-authored.
    public var knownAnswerSourceSessionID: UUID? {
        answerSourceSessionID ?? (answerRevision == nil ? sourceSessionID : nil)
    }
    public init(id: UUID = UUID(), question: String, answer: String, timesAsked: Int = 1, sourceSessionID: UUID? = nil,
                sourceSessionIDs: [UUID]? = nil, answerSourceSessionID: UUID? = nil,
                answerHistory: [WorkspaceAnswerRevision]? = nil,
                answerRevision: Int? = nil, updatedAt: Date = Date()) {
        self.id = id; self.question = question; self.answer = answer; self.timesAsked = timesAsked
        self.sourceSessionID = sourceSessionID; self.sourceSessionIDs = sourceSessionIDs
        self.answerSourceSessionID = answerSourceSessionID
        self.answerHistory = answerHistory; self.answerRevision = answerRevision; self.updatedAt = updatedAt
    }
}

/// A user-confirmed decision or commitment that remains current across meetings.
/// Source details point to the meeting and reviewed evidence used for this revision.
public struct WorkspaceSeriesItem: Codable, Sendable, Equatable, Identifiable {
    public enum Kind: String, Codable, Sendable { case decision, action }
    public enum ActionState: String, Codable, Sendable { case open, inProgress, done, cancelled, superseded }

    public var id: UUID
    public var kind: Kind
    public var text: String
    public var owner: String?
    public var due: String?
    public var dueDate: Date?
    /// Nil for decisions; actions begin open and can be updated independently of evidence review.
    public var actionState: ActionState?
    /// Evidence anchor for the current revision.
    public var sourceSessionID: UUID
    public var evidenceID: String
    public var claimRevision: String
    public var sourceRevision: String
    public var sourceQuote: String?
    public var location: String?
    /// The full transcript snapshot used to verify this accepted revision.
    public var transcriptSourceRevision: String?
    /// True when the meeting anchoring the current revision was deleted. The
    /// confirmed text stays visible for re-confirmation, but its quote is scrubbed.
    public var sourceMissing: Bool?
    /// True only when the user explicitly chose to keep this text as independent context.
    public var independentlyRetained: Bool?
    public var revision: Int
    public var history: [WorkspaceSeriesItemRevision]
    public var confirmedAt: Date
    public var updatedAt: Date

    public init(id: UUID = UUID(), kind: Kind, text: String, owner: String? = nil, due: String? = nil,
                dueDate: Date? = nil, actionState: ActionState? = nil, sourceSessionID: UUID,
                evidenceID: String, claimRevision: String, sourceRevision: String,
                sourceQuote: String? = nil, location: String? = nil,
                transcriptSourceRevision: String? = nil, sourceMissing: Bool? = false,
                independentlyRetained: Bool? = false, revision: Int = 1,
                history: [WorkspaceSeriesItemRevision] = [], confirmedAt: Date = Date(), updatedAt: Date = Date()) {
        self.id = id; self.kind = kind; self.text = text; self.owner = owner; self.due = due; self.dueDate = dueDate
        self.actionState = kind == .action ? (actionState ?? .open) : nil
        self.sourceSessionID = sourceSessionID; self.evidenceID = evidenceID; self.claimRevision = claimRevision
        self.sourceRevision = sourceRevision; self.sourceQuote = sourceQuote; self.location = location
        self.transcriptSourceRevision = transcriptSourceRevision; self.sourceMissing = sourceMissing
        self.independentlyRetained = independentlyRetained
        self.revision = max(1, revision); self.history = history; self.confirmedAt = confirmedAt; self.updatedAt = updatedAt
    }

    public var currentRevision: WorkspaceSeriesItemRevision {
        var snapshot = WorkspaceSeriesItemRevision(revision: revision, kind: kind, text: text, owner: owner,
                                                   due: due, dueDate: dueDate, actionState: actionState,
                                                   sourceSessionID: sourceSessionID, evidenceID: evidenceID,
                                                   claimRevision: claimRevision, sourceRevision: sourceRevision,
                                                   sourceQuote: sourceQuote, location: location,
                                                   transcriptSourceRevision: transcriptSourceRevision,
                                                   sourceMissing: sourceMissing, recordedAt: updatedAt)
        snapshot.id = snapshot.stableID(itemID: id)
        return snapshot
    }
}

/// Immutable snapshot retained when a confirmed decision or action changes.
public struct WorkspaceSeriesItemRevision: Codable, Sendable, Equatable, Identifiable {
    public var id: UUID
    public var revision: Int
    public var kind: WorkspaceSeriesItem.Kind
    public var text: String
    public var owner: String?
    public var due: String?
    public var dueDate: Date?
    public var actionState: WorkspaceSeriesItem.ActionState?
    public var sourceSessionID: UUID
    public var evidenceID: String
    public var claimRevision: String
    public var sourceRevision: String
    public var sourceQuote: String?
    public var location: String?
    public var transcriptSourceRevision: String?
    /// True when this historical revision's meeting was deleted. The accepted
    /// decision remains in history, while its private evidence is removed.
    public var sourceMissing: Bool?
    public var recordedAt: Date

    public init(id: UUID = UUID(), revision: Int, kind: WorkspaceSeriesItem.Kind, text: String,
                owner: String? = nil, due: String? = nil, dueDate: Date? = nil,
                actionState: WorkspaceSeriesItem.ActionState? = nil, sourceSessionID: UUID,
                evidenceID: String, claimRevision: String, sourceRevision: String,
                sourceQuote: String? = nil, location: String? = nil,
                transcriptSourceRevision: String? = nil, sourceMissing: Bool? = false,
                recordedAt: Date = Date()) {
        self.id = id; self.revision = revision; self.kind = kind; self.text = text; self.owner = owner
        self.due = due; self.dueDate = dueDate; self.actionState = actionState; self.sourceSessionID = sourceSessionID
        self.evidenceID = evidenceID; self.claimRevision = claimRevision; self.sourceRevision = sourceRevision
        self.sourceQuote = sourceQuote; self.location = location
        self.transcriptSourceRevision = transcriptSourceRevision; self.sourceMissing = sourceMissing
        self.recordedAt = recordedAt
    }

    func stableID(itemID: UUID) -> UUID {
        let values = [
            Optional(itemID.uuidString.lowercased()), Optional(String(revision)), Optional(kind.rawValue), Optional(text),
            owner, due, dueDate.map { String($0.timeIntervalSince1970.bitPattern, radix: 16) },
            actionState?.rawValue, Optional(sourceSessionID.uuidString.lowercased()), Optional(evidenceID),
            Optional(claimRevision), Optional(sourceRevision), sourceQuote, location, transcriptSourceRevision,
            sourceMissing.map { String($0) }, Optional(String(recordedAt.timeIntervalSince1970.bitPattern, radix: 16))
        ]
        let material = values.map { value in
            guard let value else { return "0:" }
            return "1\(value.utf8.count):\(value)"
        }.joined(separator: "|")
        var bytes = Array(SHA256.hash(data: Data("series-item-revision-v2|\(material)".utf8)).prefix(16))
        bytes[6] = (bytes[6] & 0x0F) | 0x50
        bytes[8] = (bytes[8] & 0x3F) | 0x80
        return UUID(uuid: (bytes[0], bytes[1], bytes[2], bytes[3], bytes[4], bytes[5], bytes[6], bytes[7],
                           bytes[8], bytes[9], bytes[10], bytes[11], bytes[12], bytes[13], bytes[14], bytes[15]))
    }
}

/// Minimal deletion metadata that prevents a backup from restoring source content
/// for a removed series item or historical revision. It contains no accepted text
/// or evidence details.
public struct WorkspaceSeriesItemSourceTombstone: Codable, Sendable, Equatable, Hashable {
    public var itemID: UUID
    public var sourceSessionID: UUID

    public init(itemID: UUID, sourceSessionID: UUID) {
        self.itemID = itemID
        self.sourceSessionID = sourceSessionID
    }
}

public enum WorkspaceSeriesChangeDecision: String, Codable, Sendable { case add, replace, reject }

/// Optional user edits confirmed alongside an evidence-backed series proposal.
/// A non-nil value can intentionally clear an extracted owner or deadline.
public struct WorkspaceSeriesChangeReviewDetails: Sendable, Equatable {
    public var owner: String?
    /// Preserve the deadline as spoken, including relative wording and any named time zone.
    public var due: String?

    public init(owner: String?, due: String?) {
        self.owner = owner
        self.due = due
    }
}

public struct WorkspaceSeriesChangeReceipt: Codable, Sendable, Equatable {
    public var operationID: UUID
    public var candidateID: String
    public var decision: WorkspaceSeriesChangeDecision
    public var reviewedAt: Date
    public var resultItemID: UUID?

    public init(operationID: UUID, candidateID: String, decision: WorkspaceSeriesChangeDecision,
                reviewedAt: Date, resultItemID: UUID?) {
        self.operationID = operationID; self.candidateID = candidateID; self.decision = decision
        self.reviewedAt = reviewedAt; self.resultItemID = resultItemID
    }
}

/// A reviewed-evidence proposal stays separate from active series state until accepted.
public struct WorkspaceSeriesChangeCandidate: Codable, Sendable, Equatable, Identifiable {
    public enum Status: String, Codable, Sendable { case pending, accepted, rejected, superseded }

    public var id: String
    public var kind: WorkspaceSeriesItem.Kind
    public var text: String
    public var owner: String?
    public var due: String?
    public var dueDate: Date?
    public var sourceSessionID: UUID
    public var evidenceID: String
    public var claimRevision: String
    public var sourceRevision: String
    public var sourceQuote: String?
    public var location: String?
    /// The full transcript snapshot used to verify this proposal.
    public var transcriptSourceRevision: String?
    /// Decision receipts survive source deletion, while deleted-source content is scrubbed.
    public var sourceMissing: Bool?
    public var status: Status
    public var receipt: WorkspaceSeriesChangeReceipt?
    /// Receipts from concurrent devices that reviewed the same candidate. The
    /// current receipt is the deterministic winner; older receipts remain replayable.
    public var receiptHistory: [WorkspaceSeriesChangeReceipt]?
    public var createdAt: Date

    public init(id: String, kind: WorkspaceSeriesItem.Kind, text: String, owner: String? = nil,
                due: String? = nil, dueDate: Date? = nil, sourceSessionID: UUID,
                evidenceID: String, claimRevision: String, sourceRevision: String,
                sourceQuote: String? = nil, location: String? = nil,
                transcriptSourceRevision: String? = nil, sourceMissing: Bool? = false,
                status: Status = .pending, receiptHistory: [WorkspaceSeriesChangeReceipt]? = nil,
                receipt: WorkspaceSeriesChangeReceipt? = nil, createdAt: Date = Date()) {
        self.id = id; self.kind = kind; self.text = text; self.owner = owner; self.due = due; self.dueDate = dueDate
        self.sourceSessionID = sourceSessionID; self.evidenceID = evidenceID; self.claimRevision = claimRevision
        self.sourceRevision = sourceRevision; self.sourceQuote = sourceQuote; self.location = location
        self.transcriptSourceRevision = transcriptSourceRevision
        self.sourceMissing = sourceMissing
        self.status = status; self.receipt = receipt; self.receiptHistory = receiptHistory; self.createdAt = createdAt
    }
}

public enum WorkspaceKnowledgeKind: String, Codable, Sendable { case lesson, question }

public enum WorkspaceKnowledgeDecision: String, Codable, Sendable {
    case add, keepExisting, replaceAnswer, reject
}

public struct WorkspaceKnowledgeReceipt: Codable, Sendable, Equatable {
    public var operationID: UUID
    public var candidateID: String
    public var decision: WorkspaceKnowledgeDecision
    public var reviewedAt: Date
    public var resultItemID: UUID?
    public var sourceCounted: Bool

    public init(operationID: UUID, candidateID: String, decision: WorkspaceKnowledgeDecision,
                reviewedAt: Date, resultItemID: UUID?, sourceCounted: Bool) {
        self.operationID = operationID; self.candidateID = candidateID; self.decision = decision
        self.reviewedAt = reviewedAt; self.resultItemID = resultItemID; self.sourceCounted = sourceCounted
    }
}

/// The archived snapshots that contributed to a series-wide review suggestion.
/// The transcript revision ties the suggestion to the transcript, while the
/// projection revision also detects edits to the saved summary or meeting view.
public struct WorkspaceKnowledgeCandidateSource: Codable, Sendable, Equatable, Identifiable {
    public var sessionID: UUID
    public var startedAt: Date
    public var transcriptSourceRevision: String
    public var projectionRevision: String

    public var id: UUID { sessionID }

    public init(sessionID: UUID, startedAt: Date, transcriptSourceRevision: String,
                projectionRevision: String) {
        self.sessionID = sessionID
        self.startedAt = startedAt
        self.transcriptSourceRevision = transcriptSourceRevision
        self.projectionRevision = projectionRevision
    }
}

/// A generated suggestion remains inert until the user accepts, keeps, replaces, or rejects it.
public struct WorkspaceKnowledgeCandidate: Codable, Sendable, Equatable, Identifiable {
    /// Deterministic content ID: repeating the same summary cannot enqueue it twice.
    public var id: String
    /// Stable logical target, independent of the suggested answer revision.
    public var logicalItemID: String
    public var kind: WorkspaceKnowledgeKind
    /// Lesson text or question text, depending on `kind`.
    public var text: String
    public var proposedAnswer: String?
    public var sourceSessionID: UUID?
    /// Transcript snapshot from the archived summary that produced this candidate.
    public var transcriptSourceRevision: String?
    /// Full session snapshots behind a series-wide review candidate. Such a
    /// candidate remains pending only while every contributing meeting is intact.
    public var contributingSources: [WorkspaceKnowledgeCandidateSource]?
    public var baseQuestionID: UUID?
    public var baseAnswerRevision: Int?
    public var status: Status
    public var receipt: WorkspaceKnowledgeReceipt?
    public var createdAt: Date

    public enum Status: String, Codable, Sendable { case pending, accepted, rejected }

    public var contributingSessionIDs: [UUID] {
        var ids = Set(contributingSources?.map(\.sessionID) ?? [])
        if let sourceSessionID { ids.insert(sourceSessionID) }
        return ids.sorted { $0.uuidString < $1.uuidString }
    }

    public var primarySourceSessionID: UUID? {
        sourceSessionID ?? contributingSources?.map(\.sessionID).min { $0.uuidString < $1.uuidString }
    }

    public init(id: String, logicalItemID: String, kind: WorkspaceKnowledgeKind, text: String,
                proposedAnswer: String? = nil, sourceSessionID: UUID? = nil,
                transcriptSourceRevision: String? = nil,
                contributingSources: [WorkspaceKnowledgeCandidateSource]? = nil,
                baseQuestionID: UUID? = nil, baseAnswerRevision: Int? = nil,
                status: Status = .pending, receipt: WorkspaceKnowledgeReceipt? = nil, createdAt: Date = Date()) {
        self.id = id; self.logicalItemID = logicalItemID; self.kind = kind; self.text = text
        self.proposedAnswer = proposedAnswer; self.sourceSessionID = sourceSessionID
        self.transcriptSourceRevision = transcriptSourceRevision
        self.contributingSources = contributingSources
        self.baseQuestionID = baseQuestionID; self.baseAnswerRevision = baseAnswerRevision
        self.status = status; self.receipt = receipt; self.createdAt = createdAt
    }
}

// MARK: - Workspace

/// A named scenario (“字节后端二面”, “ACL 2026 口头报告”): mode, inputs, materials,
/// answer requirements and the knowledge accumulated from its reviews.
public struct Workspace: Codable, Sendable, Equatable, Identifiable {
    public var id: UUID
    public var name: String
    public var mode: SessionMode
    public var style: AnswerStyle
    public var preferences: String
    /// Free-form background notes.
    public var notes: String
    /// Values of the mode's structured fields, by field id.
    public var fields: [String: String]
    public var documents: [ContextDocument]
    public var lessons: [WorkspaceLesson]
    public var questionBank: [QuestionBankItem]
    /// Review queue and receipts live beside the knowledge they can change.
    public var knowledgeCandidates: [WorkspaceKnowledgeCandidate]?
    /// Confirmed decisions and action items used by the local next-meeting briefing.
    public var seriesItems: [WorkspaceSeriesItem]?
    /// Reviewed evidence proposals are inert until a user accepts or rejects them.
    public var seriesChangeCandidates: [WorkspaceSeriesChangeCandidate]?
    /// Redacted source-deletion markers retained so importing an older backup
    /// cannot restore deleted series text or evidence.
    public var seriesItemSourceTombstones: [WorkspaceSeriesItemSourceTombstone]?
    /// Session IDs deleted from this workspace. Kept so older backups cannot
    /// reintroduce lessons or other accepted content sourced only from them.
    public var sourceDeletionTombstones: [UUID]?
    /// Feed lessons and the question bank back into answers.
    public var useKnowledge: Bool
    /// Latest review across all sessions of this workspace.
    public var stageReview: String?
    public var stageReviewAt: Date?
    public var createdAt: Date
    public var updatedAt: Date
    public var archived: Bool
    /// Skills from the library attached to this scenario (global skills apply anyway).
    public var skillIDs: [UUID]
    /// Name of a custom meeting type (`mode == .custom`), e.g. "导师一对一".
    public var customTypeName: String?
    /// The built-in type a custom type takes its defaults from.
    public var customBaseMode: SessionMode?
    /// Names and terms of this series only; added to the global glossary (“术语和人名”).
    public var glossary: Glossary?

    public init(id: UUID = UUID(), name: String, mode: SessionMode, style: AnswerStyle = .concise, preferences: String? = nil,
                notes: String = "", fields: [String: String] = [:], documents: [ContextDocument] = [],
                lessons: [WorkspaceLesson] = [], questionBank: [QuestionBankItem] = [], useKnowledge: Bool = true,
                knowledgeCandidates: [WorkspaceKnowledgeCandidate]? = nil,
                seriesItems: [WorkspaceSeriesItem]? = nil,
                seriesChangeCandidates: [WorkspaceSeriesChangeCandidate]? = nil,
                seriesItemSourceTombstones: [WorkspaceSeriesItemSourceTombstone]? = nil,
                sourceDeletionTombstones: [UUID]? = nil,
                stageReview: String? = nil, stageReviewAt: Date? = nil, createdAt: Date = Date(), updatedAt: Date = Date(), archived: Bool = false, skillIDs: [UUID] = []) {
        self.id = id; self.name = name; self.mode = mode; self.style = style
        self.preferences = preferences ?? ModeProfile.for(mode).templatePreferences
        self.notes = notes; self.fields = fields; self.documents = documents; self.lessons = lessons
        self.questionBank = questionBank; self.knowledgeCandidates = knowledgeCandidates; self.useKnowledge = useKnowledge
        self.seriesItems = seriesItems; self.seriesChangeCandidates = seriesChangeCandidates
        self.seriesItemSourceTombstones = seriesItemSourceTombstones
        self.sourceDeletionTombstones = sourceDeletionTombstones
        self.stageReview = stageReview; self.stageReviewAt = stageReviewAt
        self.createdAt = createdAt; self.updatedAt = updatedAt; self.archived = archived
        self.skillIDs = skillIDs
        self.customTypeName = nil
        self.customBaseMode = nil
    }

    // Tolerant decoding: fields added in later versions get defaults.
    enum CodingKeys: String, CodingKey {
        case id, name, mode, style, preferences, notes, fields, documents, lessons, questionBank, knowledgeCandidates, useKnowledge
        case seriesItems, seriesChangeCandidates, seriesItemSourceTombstones, sourceDeletionTombstones
        case stageReview, stageReviewAt, createdAt, updatedAt, archived, skillIDs
        case customTypeName, customBaseMode, glossary
    }

    public init(from decoder: Decoder) throws {
        let c = try decoder.container(keyedBy: CodingKeys.self)
        id = try c.decode(UUID.self, forKey: .id)
        name = try c.decodeIfPresent(String.self, forKey: .name) ?? "未命名场景"
        mode = try c.decodeIfPresent(SessionMode.self, forKey: .mode) ?? .general
        style = (try? c.decodeIfPresent(AnswerStyle.self, forKey: .style)) ?? .concise
        preferences = try c.decodeIfPresent(String.self, forKey: .preferences) ?? ""
        notes = try c.decodeIfPresent(String.self, forKey: .notes) ?? ""
        fields = try c.decodeIfPresent([String: String].self, forKey: .fields) ?? [:]
        documents = try c.decodeIfPresent([ContextDocument].self, forKey: .documents) ?? []
        lessons = try c.decodeIfPresent([WorkspaceLesson].self, forKey: .lessons) ?? []
        questionBank = try c.decodeIfPresent([QuestionBankItem].self, forKey: .questionBank) ?? []
        knowledgeCandidates = try c.decodeIfPresent([WorkspaceKnowledgeCandidate].self, forKey: .knowledgeCandidates)
        seriesItems = try c.decodeIfPresent([WorkspaceSeriesItem].self, forKey: .seriesItems)
        seriesChangeCandidates = try c.decodeIfPresent([WorkspaceSeriesChangeCandidate].self, forKey: .seriesChangeCandidates)
        seriesItemSourceTombstones = try c.decodeIfPresent([WorkspaceSeriesItemSourceTombstone].self, forKey: .seriesItemSourceTombstones)
        sourceDeletionTombstones = try c.decodeIfPresent([UUID].self, forKey: .sourceDeletionTombstones)
        useKnowledge = try c.decodeIfPresent(Bool.self, forKey: .useKnowledge) ?? true
        stageReview = try c.decodeIfPresent(String.self, forKey: .stageReview)
        stageReviewAt = try c.decodeIfPresent(Date.self, forKey: .stageReviewAt)
        createdAt = try c.decodeIfPresent(Date.self, forKey: .createdAt) ?? Date()
        updatedAt = try c.decodeIfPresent(Date.self, forKey: .updatedAt) ?? createdAt
        archived = try c.decodeIfPresent(Bool.self, forKey: .archived) ?? false
        skillIDs = try c.decodeIfPresent([UUID].self, forKey: .skillIDs) ?? []
        customTypeName = try c.decodeIfPresent(String.self, forKey: .customTypeName)
        customBaseMode = try c.decodeIfPresent(SessionMode.self, forKey: .customBaseMode)
        glossary = try? c.decodeIfPresent(Glossary.self, forKey: .glossary)
    }

    /// The type whose defaults apply: a custom type behaves like its base type.
    public var effectiveMode: SessionMode {
        guard mode == .custom else { return mode }
        let base = customBaseMode ?? .general
        return base == .custom ? .general : base
    }

    public var profile: ModeProfile { ModeProfile.for(effectiveMode) }

    /// Background text for the model: the structured fields followed by free notes.
    public var materialText: String {
        var lines: [String] = []
        for field in profile.fields {
            let value = (fields[field.id] ?? "").trimmingCharacters(in: .whitespacesAndNewlines)
            if !value.isEmpty { lines.append("\(field.title)：\(value)") }
        }
        // Values of fields from another mode (after switching) are kept but labeled.
        for (key, value) in fields.sorted(by: { $0.key < $1.key }) where !profile.fields.contains(where: { $0.id == key }) {
            let trimmed = value.trimmingCharacters(in: .whitespacesAndNewlines)
            if !trimmed.isEmpty { lines.append("\(key)：\(trimmed)") }
        }
        let trimmedNotes = notes.trimmingCharacters(in: .whitespacesAndNewlines)
        if !trimmedNotes.isEmpty { lines.append(trimmedNotes) }
        return lines.joined(separator: "\n")
    }

    /// Lessons for the answer prompt: pinned first, then newest.
    public func experienceNotes(limit: Int = 2_000) -> String {
        guard useKnowledge else { return "" }
        let ordered = lessons.filter(\.pinned) + lessons.filter { !$0.pinned }.sorted { $0.createdAt > $1.createdAt }
        var result: [String] = []
        var used = 0
        for lesson in ordered {
            let line = "- " + lesson.text
            if used + line.count > limit { break }
            result.append(line); used += line.count
        }
        return result.joined(separator: "\n")
    }

    /// Documents for retrieval, including the question bank as one searchable file.
    public var retrievalDocuments: [ContextDocument] {
        var result = documents
        let reviewedAnswers = questionBank.filter {
            !$0.question.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty
                && !$0.answer.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty
        }
        if useKnowledge, !reviewedAnswers.isEmpty {
            let pages = reviewedAnswers.map { "问题：\($0.question)\n准备的回答：\($0.answer)" }
            result.append(ContextDocument(id: id, name: "题库（历次复盘）", pages: pages, enabled: true, category: "题库"))
        }
        return result
    }
}

/// Removes knowledge that depends on deleted meetings while preserving the other
/// meeting sources of shared items. Confirmed lessons and series text whose only
/// source is being deleted are removed unless the user explicitly opts to keep it.
public enum WorkspaceSourceIntegrity {
    public static func hasRetainableAcceptedContent(in workspace: Workspace,
                                                    from deletedSessionIDs: Set<UUID>) -> Bool {
        guard !deletedSessionIDs.isEmpty else { return false }
        if workspace.lessons.contains(where: {
            !WorkspaceLessonHistory.allSourceIDs($0).isDisjoint(with: deletedSessionIDs)
                || $0.history.contains {
                    !WorkspaceLessonHistory.allSourceIDs($0).isDisjoint(with: deletedSessionIDs)
                }
        }) { return true }
        return (workspace.seriesItems ?? []).contains { item in
            deletedSessionIDs.contains(item.sourceSessionID)
                || item.history.contains(where: { deletedSessionIDs.contains($0.sourceSessionID) })
        }
    }

    @discardableResult
    public static func removeMeetings(_ deletedSessionIDs: Set<UUID>, from workspace: inout Workspace,
                                     retainAcceptedContent: Bool = false,
                                     now: Date = Date()) -> Bool {
        guard !deletedSessionIDs.isEmpty else { return false }
        var changed = false

        let sourceDeletionTombstones = Set(workspace.sourceDeletionTombstones ?? []).union(deletedSessionIDs)
            .sorted { $0.uuidString.lowercased() < $1.uuidString.lowercased() }
        if sourceDeletionTombstones != (workspace.sourceDeletionTombstones ?? []) { changed = true }

        let lessons = workspace.lessons.compactMap { lesson -> WorkspaceLesson? in
            var updated = lesson
            let originalSources = WorkspaceLessonHistory.allSourceIDs(lesson)
            let removed = originalSources.intersection(deletedSessionIDs)
            let remaining = originalSources.subtracting(deletedSessionIDs)
            if !removed.isEmpty && remaining.isEmpty && !retainAcceptedContent {
                changed = true
                return nil
            }

            var historyChanged = false
            updated.history = lesson.history.compactMap { revision in
                guard !WorkspaceLessonHistory.allSourceIDs(revision).isDisjoint(with: deletedSessionIDs) else {
                    return revision
                }
                let revisionSources = WorkspaceLessonHistory.allSourceIDs(revision)
                let revisionRemaining = revisionSources.subtracting(deletedSessionIDs)
                if revisionRemaining.isEmpty && !retainAcceptedContent {
                    historyChanged = true
                    return nil
                }
                var cleaned = revision
                cleaned.sourceSessionIDs = revisionRemaining.isEmpty ? nil : sorted(revisionRemaining)
                cleaned.sourceSessionID = revision.sourceSessionID.flatMap {
                    revisionRemaining.contains($0) ? $0 : nil
                } ?? sorted(revisionRemaining).first
                cleaned.sourceMissingSessionIDs = sorted(Set(revision.sourceMissingSessionIDs ?? []).union(deletedSessionIDs.intersection(revisionSources)))
                historyChanged = historyChanged || cleaned != revision
                return cleaned
            }

            if !removed.isEmpty {
                // Keep the previous accepted version before recording the source-only change.
                WorkspaceLessonHistory.appendCurrentRevision(to: &updated)
                updated.history = updated.history.compactMap { revision in
                    guard !WorkspaceLessonHistory.allSourceIDs(revision).isDisjoint(with: deletedSessionIDs) else {
                        return revision
                    }
                    let revisionSources = WorkspaceLessonHistory.allSourceIDs(revision)
                    var cleaned = revision
                    let revisionRemaining = revisionSources.subtracting(deletedSessionIDs)
                    cleaned.sourceSessionIDs = revisionRemaining.isEmpty ? nil : sorted(revisionRemaining)
                    cleaned.sourceSessionID = revision.sourceSessionID.flatMap {
                        revisionRemaining.contains($0) ? $0 : nil
                    } ?? sorted(revisionRemaining).first
                    cleaned.sourceMissingSessionIDs = sorted(Set(revision.sourceMissingSessionIDs ?? []).union(deletedSessionIDs.intersection(revisionSources)))
                    return cleaned
                }
                updated.sourceSessionIDs = remaining.isEmpty ? nil : sorted(remaining)
                updated.sourceSessionID = lesson.sourceSessionID.flatMap { remaining.contains($0) ? $0 : nil } ?? sorted(remaining).first
                updated.sourceMissingSessionIDs = sorted(Set(lesson.sourceMissingSessionIDs ?? []).union(removed))
                updated.revision += 1
                updated.updatedAt = now
                updated.independentlyRetained = remaining.isEmpty && retainAcceptedContent
                changed = true
            } else if historyChanged {
                changed = true
            }
            return updated
        }

        let questionBank = workspace.questionBank.compactMap { item -> QuestionBankItem? in
            let originalSources = sourceIDs(legacy: item.sourceSessionID, all: item.sourceSessionIDs)
            let removedQuestionSources = originalSources.intersection(deletedSessionIDs)
            var updated = item

            if !removedQuestionSources.isEmpty {
                let remaining = originalSources.subtracting(deletedSessionIDs)
                changed = true
                // A question learned only from the deleted meeting is no longer reusable.
                guard !remaining.isEmpty else { return nil }
                updated.sourceSessionIDs = sorted(remaining)
                updated.sourceSessionID = item.sourceSessionID.flatMap { remaining.contains($0) ? $0 : nil } ?? sorted(remaining).first
                updated.timesAsked = max(remaining.count, max(0, item.timesAsked - removedQuestionSources.count))
                updated.updatedAt = now
            }

            if let answerSource = item.knownAnswerSourceSessionID, deletedSessionIDs.contains(answerSource) {
                // Keep a multiply-sourced question, but never retrieve an answer
                // whose only provenance was removed.
                updated.answer = ""
                updated.answerSourceSessionID = nil
                updated.answerRevision = (item.answerRevision ?? 0) + 1
                updated.updatedAt = now
                changed = true
            }

            if let history = item.answerHistory {
                let retained = history.filter { $0.sourceSessionID.map { !deletedSessionIDs.contains($0) } ?? true }
                if retained != history {
                    updated.answerHistory = retained
                    changed = true
                }
            }
            return updated
        }

        var seriesItemSourceTombstones = Set(workspace.seriesItemSourceTombstones ?? [])
        for item in workspace.seriesItems ?? [] {
            if deletedSessionIDs.contains(item.sourceSessionID) {
                seriesItemSourceTombstones.insert(.init(itemID: item.id, sourceSessionID: item.sourceSessionID))
            }
            for revision in item.history where deletedSessionIDs.contains(revision.sourceSessionID) {
                seriesItemSourceTombstones.insert(.init(itemID: item.id, sourceSessionID: revision.sourceSessionID))
            }
        }
        let sortedSeriesItemSourceTombstones = seriesItemSourceTombstones.sorted {
            let leftItem = $0.itemID.uuidString.lowercased()
            let rightItem = $1.itemID.uuidString.lowercased()
            if leftItem != rightItem { return leftItem < rightItem }
            return $0.sourceSessionID.uuidString.lowercased() < $1.sourceSessionID.uuidString.lowercased()
        }
        if sortedSeriesItemSourceTombstones != (workspace.seriesItemSourceTombstones ?? []) {
            changed = true
        }

        let seriesItems = (workspace.seriesItems ?? []).compactMap { item -> WorkspaceSeriesItem? in
            let alreadyRetained = item.sourceMissing == true && item.independentlyRetained == true
            let sourceIsDeleted = deletedSessionIDs.contains(item.sourceSessionID) && !alreadyRetained
            if sourceIsDeleted && !retainAcceptedContent {
                changed = true
                return nil
            }
            var updated = item
            if sourceIsDeleted {
                updated.sourceMissing = true
                updated.independentlyRetained = true
                updated.evidenceID = ""
                updated.claimRevision = ""
                updated.sourceRevision = ""
                updated.sourceQuote = nil
                updated.location = nil
                updated.transcriptSourceRevision = nil
                updated.updatedAt = now
                changed = true
            }
            let retainedHistory = updated.history.compactMap { revision -> WorkspaceSeriesItemRevision? in
                guard deletedSessionIDs.contains(revision.sourceSessionID), revision.sourceMissing != true else { return revision }
                changed = true
                guard retainAcceptedContent else { return nil }
                var tombstone = revision
                tombstone.sourceMissing = true
                tombstone.evidenceID = ""
                tombstone.claimRevision = ""
                tombstone.sourceRevision = ""
                tombstone.sourceQuote = nil
                tombstone.location = nil
                tombstone.transcriptSourceRevision = nil
                tombstone.id = tombstone.stableID(itemID: item.id)
                return tombstone
            }
            updated.history = retainedHistory
            return updated
        }

        let knowledgeCandidates = workspace.knowledgeCandidates?.compactMap { candidate -> WorkspaceKnowledgeCandidate? in
            let sources = Set(candidate.contributingSessionIDs)
            // Older stage-review candidates did not retain provenance. Once any
            // meeting is removed, discard unverifiable pending proposals.
            if sources.isEmpty { return candidate.status == .pending ? nil : candidate }
            guard !sources.isDisjoint(with: deletedSessionIDs) else { return candidate }
            changed = true

            // Keep the durable decision receipt for audit/idempotent replay, but
            // scrub the proposal text and deleted evidence. The accepted lesson or
            // answer above is independently retained, revised, or removed according
            // to its remaining source set.
            guard candidate.status != .pending else { return nil }
            var retained = candidate
            retained.text = ""
            retained.proposedAnswer = nil
            retained.sourceSessionID = candidate.sourceSessionID.flatMap {
                deletedSessionIDs.contains($0) ? nil : $0
            }
            retained.transcriptSourceRevision = nil
            retained.contributingSources = candidate.contributingSources?.filter {
                !deletedSessionIDs.contains($0.sessionID)
            }
            retained.baseQuestionID = nil
            retained.baseAnswerRevision = nil
            return retained
        }
        let seriesCandidates = workspace.seriesChangeCandidates?.compactMap { candidate -> WorkspaceSeriesChangeCandidate? in
            guard deletedSessionIDs.contains(candidate.sourceSessionID) else { return candidate }
            // Keep a redacted tombstone even for an unreviewed candidate so an older
            // backup cannot reintroduce its deleted quote or make it reviewable again.
            if candidate.status == .pending {
                changed = true
                var tombstone = candidate
                tombstone.text = ""
                tombstone.owner = nil
                tombstone.due = nil
                tombstone.dueDate = nil
                tombstone.evidenceID = ""
                tombstone.claimRevision = ""
                tombstone.sourceRevision = ""
                tombstone.sourceQuote = nil
                tombstone.location = nil
                tombstone.transcriptSourceRevision = nil
                tombstone.sourceMissing = true
                tombstone.status = .superseded
                return tombstone
            }
            let requiresScrub = candidate.sourceMissing != true || !candidate.text.isEmpty
                || candidate.owner != nil || candidate.due != nil || candidate.dueDate != nil
                || !candidate.evidenceID.isEmpty || !candidate.claimRevision.isEmpty
                || !candidate.sourceRevision.isEmpty || candidate.sourceQuote != nil
                || candidate.location != nil || candidate.transcriptSourceRevision != nil
            guard requiresScrub else { return candidate }
            changed = true
            var retained = candidate
            retained.text = ""
            retained.owner = nil
            retained.due = nil
            retained.dueDate = nil
            retained.evidenceID = ""
            retained.claimRevision = ""
            retained.sourceRevision = ""
            retained.sourceQuote = nil
            retained.location = nil
            retained.transcriptSourceRevision = nil
            retained.sourceMissing = true
            return retained
        }
        if knowledgeCandidates != workspace.knowledgeCandidates { changed = true }
        if seriesCandidates != workspace.seriesChangeCandidates { changed = true }

        guard changed else { return false }
        workspace.lessons = lessons
        workspace.questionBank = questionBank
        if workspace.seriesItems != nil { workspace.seriesItems = seriesItems }
        workspace.seriesItemSourceTombstones = sortedSeriesItemSourceTombstones.isEmpty ? nil : sortedSeriesItemSourceTombstones
        workspace.sourceDeletionTombstones = sourceDeletionTombstones
        workspace.knowledgeCandidates = knowledgeCandidates
        workspace.seriesChangeCandidates = seriesCandidates
        workspace.updatedAt = now
        return true
    }

    private static func sourceIDs(legacy: UUID?, all: [UUID]?) -> Set<UUID> {
        Set(all ?? []).union(legacy.map { [$0] } ?? [])
    }

    private static func sorted(_ ids: Set<UUID>) -> [UUID] {
        ids.sorted { $0.uuidString < $1.uuidString }
    }
}

// MARK: - Merging review output into a workspace

public enum KnowledgeMerger {
    /// Turn generated notes into inert, deterministic suggestions. Re-running the
    /// same summary keeps the existing candidate and its review receipt.
    @discardableResult
    public static func collectCandidates(lessons newLessons: [String], bank newItems: [BankEntry],
                                         into workspace: inout Workspace, session: UUID?,
                                         originID: UUID? = nil, transcriptSourceRevision: String? = nil,
                                         contributingSources: [WorkspaceKnowledgeCandidateSource]? = nil,
                                         now: Date = Date()) -> [String] {
        let originID = originID ?? session ?? workspace.id
        let sourceSnapshots = canonicalSources(contributingSources)
        let sourceManifest = sourceManifest(for: sourceSnapshots)
        var candidates = workspace.knowledgeCandidates ?? []
        var knownIDs = Set(candidates.map(\.id))
        var added: [String] = []

        for raw in newLessons {
            let text = raw.trimmingCharacters(in: .whitespacesAndNewlines)
            guard text.count >= 4 else { continue }
            let logicalID = logicalItemID(kind: .lesson, text: text)
            let id = candidateID(originID: originID, session: session, kind: .lesson,
                                 logicalID: logicalID, answer: "", transcriptSourceRevision: transcriptSourceRevision,
                                 sourceManifest: sourceManifest)
            guard knownIDs.insert(id).inserted else { continue }
            candidates.append(WorkspaceKnowledgeCandidate(id: id, logicalItemID: logicalID, kind: .lesson,
                                                           text: text, sourceSessionID: session,
                                                           transcriptSourceRevision: transcriptSourceRevision,
                                                           contributingSources: sourceSnapshots.isEmpty ? nil : sourceSnapshots,
                                                           createdAt: now))
            added.append(id)
        }

        for item in newItems {
            let question = item.question.trimmingCharacters(in: .whitespacesAndNewlines)
            let answer = item.answer.trimmingCharacters(in: .whitespacesAndNewlines)
            guard question.count >= 3, !answer.isEmpty else { continue }
            let logicalID = logicalItemID(kind: .question, text: question)
            let id = candidateID(originID: originID, session: session, kind: .question,
                                 logicalID: logicalID, answer: answer, transcriptSourceRevision: transcriptSourceRevision,
                                 sourceManifest: sourceManifest)
            guard knownIDs.insert(id).inserted else { continue }
            let match = workspace.questionBank.first { similarQuestion($0.question, question) }
            candidates.append(WorkspaceKnowledgeCandidate(id: id, logicalItemID: logicalID, kind: .question,
                                                           text: question, proposedAnswer: answer, sourceSessionID: session,
                                                           transcriptSourceRevision: transcriptSourceRevision,
                                                           contributingSources: sourceSnapshots.isEmpty ? nil : sourceSnapshots,
                                                           baseQuestionID: match?.id, baseAnswerRevision: match.map { $0.answerRevision ?? 0 },
                                                           createdAt: now))
            added.append(id)
        }

        if !added.isEmpty {
            workspace.knowledgeCandidates = candidates
            workspace.updatedAt = now
        }
        return added
    }

    /// Verify that a queued proposal still exists in the current archived summary
    /// and that the transcript snapshot used to derive it is unchanged.
    public static func matchesCurrentSummary(_ candidate: WorkspaceKnowledgeCandidate,
                                             summary: SessionSummary, sessionID: UUID,
                                             liveTranscriptSourceRevision: String) -> Bool {
        guard candidate.sourceSessionID == sessionID,
              let revision = candidate.transcriptSourceRevision,
              !revision.isEmpty,
              revision == summary.transcriptSourceRevision,
              revision == liveTranscriptSourceRevision else { return false }
        var generated = Workspace(name: "候选核验", mode: .general)
        let ids = collectCandidates(lessons: summary.lessons ?? [], bank: summary.bank ?? [], into: &generated,
                                    session: sessionID, originID: sessionID,
                                    transcriptSourceRevision: revision)
        return ids.contains(candidate.id)
    }

    /// Apply one explicitly reviewed candidate. Knowledge and its receipt are committed
    /// together in the same Workspace file by WorkspaceStore.commit.
    @discardableResult
    public static func applyCandidate(_ candidateID: String, decision: WorkspaceKnowledgeDecision,
                                      operationID: UUID, into workspace: inout Workspace,
                                      now: Date = Date()) -> WorkspaceKnowledgeReceipt? {
        guard var candidates = workspace.knowledgeCandidates,
              let candidateIndex = candidates.firstIndex(where: { $0.id == candidateID }) else { return nil }
        let candidate = candidates[candidateIndex]
        if let receipt = candidate.receipt { return receipt }
        guard candidate.status == .pending else { return nil }

        let sourceSessionIDs = candidate.contributingSessionIDs
        guard decision == .reject || !sourceSessionIDs.isEmpty else { return nil }
        let primarySourceSessionID = candidate.primarySourceSessionID

        if decision == .reject {
            let receipt = WorkspaceKnowledgeReceipt(operationID: operationID, candidateID: candidate.id,
                                                     decision: decision, reviewedAt: now, resultItemID: nil, sourceCounted: false)
            candidates[candidateIndex].status = .rejected
            candidates[candidateIndex].receipt = receipt
            workspace.knowledgeCandidates = candidates
            workspace.updatedAt = now
            return receipt
        }

        var resultItemID: UUID?
        var sourceCounted = false
        switch candidate.kind {
        case .lesson:
            if let index = workspace.lessons.firstIndex(where: { similarity($0.text, candidate.text) >= 0.6 }) {
                guard decision == .add || decision == .keepExisting else { return nil }
                resultItemID = workspace.lessons[index].id
                sourceCounted = WorkspaceLessonHistory.addSources(Set(sourceSessionIDs), to: &workspace.lessons[index], now: now)
            } else {
                guard decision == .add else { return nil }
                let id = UUID()
                workspace.lessons.append(WorkspaceLesson(id: id, text: candidate.text,
                                                          sourceSessionID: primarySourceSessionID,
                                                          sourceSessionIDs: sourceSessionIDs.isEmpty ? nil : sourceSessionIDs,
                                                          createdAt: now, updatedAt: now))
                resultItemID = id
                sourceCounted = !sourceSessionIDs.isEmpty
            }

        case .question:
            guard let proposed = candidate.proposedAnswer else { return nil }
            if let index = workspace.questionBank.firstIndex(where: { similarQuestion($0.question, candidate.text) }) {
                var item = workspace.questionBank[index]
                guard decision == .keepExisting || decision == .replaceAnswer else { return nil }
                guard candidate.baseQuestionID == item.id,
                      candidate.baseAnswerRevision == (item.answerRevision ?? 0) else { return nil }
                if decision == .replaceAnswer {
                    if item.answer != proposed {
                        var history = item.answerHistory ?? []
                        history.append(WorkspaceAnswerRevision(answer: item.answer, recordedAt: item.updatedAt,
                                                               sourceSessionID: item.knownAnswerSourceSessionID))
                        item.answerHistory = history
                        item.answer = proposed
                        item.answerSourceSessionID = primarySourceSessionID
                        item.answerRevision = (item.answerRevision ?? 0) + 1
                        item.updatedAt = now
                    }
                }
                sourceCounted = recordQuestionSources(sourceSessionIDs, on: &item)
                workspace.questionBank[index] = item
                resultItemID = item.id
            } else {
                guard decision == .add else { return nil }
                let id = UUID()
                workspace.questionBank.append(QuestionBankItem(id: id, question: candidate.text, answer: proposed,
                                                                 timesAsked: sourceSessionIDs.count,
                                                                 sourceSessionID: primarySourceSessionID,
                                                                 sourceSessionIDs: sourceSessionIDs.isEmpty ? nil : sourceSessionIDs,
                                                                 answerSourceSessionID: primarySourceSessionID,
                                                                 answerRevision: 0, updatedAt: now))
                resultItemID = id
                sourceCounted = !sourceSessionIDs.isEmpty
            }
        }

        let receipt = WorkspaceKnowledgeReceipt(operationID: operationID, candidateID: candidate.id,
                                                 decision: decision, reviewedAt: now, resultItemID: resultItemID,
                                                 sourceCounted: sourceCounted)
        candidates[candidateIndex].status = .accepted
        candidates[candidateIndex].receipt = receipt
        workspace.knowledgeCandidates = candidates
        workspace.updatedAt = now
        return receipt
    }

    /// Apply an explicit manual edit without letting a stale editor snapshot erase
    /// provenance or answer history. The revision also advances when the question
    /// wording changes, so an already-open suggestion cannot replace an answer for
    /// a question the user has since changed.
    public static func editQuestionBankItem(_ item: inout QuestionBankItem,
                                            question: String, answer: String,
                                            now: Date = Date()) {
        let questionChanged = item.question != question
        let answerChanged = item.answer != answer
        if questionChanged && !answerChanged {
            // A legacy item used sourceSessionID as its only answer source. Make that
            // source explicit before advancing the revision disables the legacy fallback.
            item.answerSourceSessionID = item.knownAnswerSourceSessionID
        }
        if answerChanged {
            var history = item.answerHistory ?? []
            history.append(WorkspaceAnswerRevision(answer: item.answer, recordedAt: item.updatedAt,
                                                   sourceSessionID: item.knownAnswerSourceSessionID))
            item.answerHistory = history
            item.answerSourceSessionID = nil
        }
        if questionChanged || answerChanged {
            item.answerRevision = (item.answerRevision ?? 0) + 1
        }
        item.question = question
        item.answer = answer
        item.updatedAt = now
    }

    /// Restore a saved answer while keeping the answer it replaces and its source.
    public static func restoreAnswerRevision(_ revisionID: UUID, on item: inout QuestionBankItem,
                                             now: Date = Date()) -> Bool {
        guard let revision = item.answerHistory?.first(where: { $0.id == revisionID }),
              item.answer != revision.answer || item.answerSourceSessionID != revision.sourceSessionID else { return false }
        var history = item.answerHistory ?? []
        history.append(WorkspaceAnswerRevision(answer: item.answer, recordedAt: item.updatedAt,
                                               sourceSessionID: item.knownAnswerSourceSessionID))
        item.answerHistory = history
        item.answer = revision.answer
        item.answerSourceSessionID = revision.sourceSessionID
        item.answerRevision = (item.answerRevision ?? 0) + 1
        item.updatedAt = now
        return true
    }

    /// Refresh the snapshot after a concurrent edit so the UI can show the current answer
    /// and require a second, informed choice before replacing it.
    public static func refreshCandidateBase(_ candidateID: String, in workspace: inout Workspace) -> Bool {
        guard var candidates = workspace.knowledgeCandidates,
              let candidateIndex = candidates.firstIndex(where: { $0.id == candidateID }),
              candidates[candidateIndex].kind == .question,
              let match = workspace.questionBank.first(where: { similarQuestion($0.question, candidates[candidateIndex].text) }) else {
            return false
        }
        candidates[candidateIndex].baseQuestionID = match.id
        candidates[candidateIndex].baseAnswerRevision = match.answerRevision ?? 0
        workspace.knowledgeCandidates = candidates
        return true
    }

    public static func similarQuestion(_ lhs: String, _ rhs: String) -> Bool {
        if let leftIntent = questionIntent(lhs), let rightIntent = questionIntent(rhs), leftIntent != rightIntent {
            return false
        }
        return similarity(questionKey(lhs), questionKey(rhs)) >= 0.55
    }

    /// Preserve the difference between question intents before removing their wording
    /// for approximate matching (for example, “why use Redis?” vs “how use Redis?”).
    private static func questionIntent(_ question: String) -> String? {
        let text = question.lowercased()
        let groups: [(intent: String, markers: [String], regularExpressions: Bool)] = [
            ("why", ["为什么", "为啥", "(?<![a-z])why(?![a-z])"], true),
            ("how", ["怎么样", "怎么", "如何", "怎样", "(?<![a-z])how(?![a-z])"], true),
            ("what", ["是什么", "什么", "(?<![a-z])what(?![a-z])"], true),
            ("which", ["哪一个", "哪些", "哪个", "(?<![a-z])which(?![a-z])"], true),
            ("when", ["什么时候", "何时", "(?<![a-z])when(?![a-z])"], true),
            ("where", ["在哪里", "在哪", "哪里", "(?<![a-z])where(?![a-z])"], true),
            ("who", ["谁", "(?<![a-z])who(?![a-z])"], true),
        ]
        var earliest: (offset: Int, intent: String)?
        for group in groups {
            for marker in group.markers {
                let options: String.CompareOptions = group.regularExpressions ? .regularExpression : []
                guard let range = text.range(of: marker, options: options) else { continue }
                let offset = text.distance(from: text.startIndex, to: range.lowerBound)
                if earliest == nil || offset < earliest!.offset { earliest = (offset, group.intent) }
            }
        }
        return earliest?.intent
    }

    private static func recordQuestionSources(_ sessions: [UUID], on item: inout QuestionBankItem) -> Bool {
        guard !sessions.isEmpty else { return false }
        var sources = Set(item.sourceSessionIDs ?? [])
        if let legacy = item.sourceSessionID { sources.insert(legacy) }
        let oldCount = sources.count
        sources.formUnion(sessions)
        let addedCount = sources.count - oldCount
        guard addedCount > 0 else {
            item.sourceSessionIDs = sources.sorted { $0.uuidString < $1.uuidString }
            return false
        }
        item.sourceSessionIDs = sources.sorted { $0.uuidString < $1.uuidString }
        if item.sourceSessionID == nil { item.sourceSessionID = sessions.first }
        item.timesAsked += addedCount
        return true
    }

    private static func addSources(_ sessions: [UUID], to sourcesValue: inout [UUID]?, legacySource: UUID?) -> Bool {
        guard !sessions.isEmpty else { return false }
        var sources = Set(sourcesValue ?? [])
        if let legacySource { sources.insert(legacySource) }
        let oldCount = sources.count
        sources.formUnion(sessions)
        let inserted = sources.count > oldCount
        sourcesValue = sources.sorted { $0.uuidString < $1.uuidString }
        return inserted
    }

    private static func logicalItemID(kind: WorkspaceKnowledgeKind, text: String) -> String {
        // Identity keeps the original question intent (“why” versus “how”); only
        // display matching may use the intentionally looser question-key normalizer.
        return stableDigest("\(kind.rawValue)|\(normalized(text))")
    }

    private static func candidateID(originID: UUID, session: UUID?, kind: WorkspaceKnowledgeKind,
                                     logicalID: String, answer: String, transcriptSourceRevision: String?,
                                     sourceManifest: String) -> String {
        let base = "\(originID.uuidString.lowercased())|\(session?.uuidString.lowercased() ?? "")|\(kind.rawValue)|\(logicalID)|\(normalized(answer))|\(transcriptSourceRevision ?? "")"
        return stableDigest(sourceManifest.isEmpty ? base : "\(base)|sources:\(sourceManifest)")
    }

    private static func canonicalSources(_ sources: [WorkspaceKnowledgeCandidateSource]?) -> [WorkspaceKnowledgeCandidateSource] {
        var byID: [UUID: WorkspaceKnowledgeCandidateSource] = [:]
        for source in sources ?? [] { byID[source.sessionID] = source }
        return byID.values.sorted { $0.sessionID.uuidString < $1.sessionID.uuidString }
    }

    private static func sourceManifest(for sources: [WorkspaceKnowledgeCandidateSource]) -> String {
        sources.map { source in
            let dateBits = source.startedAt.timeIntervalSince1970.bitPattern
            return "\(source.sessionID.uuidString.lowercased()):\(String(dateBits, radix: 16)):\(source.transcriptSourceRevision):\(source.projectionRevision)"
        }.joined(separator: "|")
    }

    private static func normalized(_ text: String) -> String {
        String(text.lowercased().filter { $0.isLetter || $0.isNumber })
    }

    private static func stableDigest(_ text: String) -> String {
        let bytes = Array(text.utf8)
        var left: UInt64 = 0xcbf29ce484222325
        var right: UInt64 = 0x84222325cbf29ce4
        for byte in bytes { left = (left ^ UInt64(byte)) &* 0x100000001b3 }
        for byte in bytes.reversed() { right = (right ^ (UInt64(byte) &+ 31)) &* 0x100000001b3 }
        let a = String(left, radix: 16), b = String(right, radix: 16)
        return String(repeating: "0", count: max(0, 16 - a.count)) + a
            + String(repeating: "0", count: max(0, 16 - b.count)) + b
    }

    /// Drops wording that varies between askings of the same question
    /// (“怎么 / 如何”, “你们”, “请问”, “can you”…).
    public static func questionKey(_ question: String) -> String {
        var text = question.lowercased()
        for word in ["请问", "请你", "请", "麻烦", "你们", "您", "你", "一下", "怎么样", "怎么", "如何", "怎样", "为什么", "为啥", "是什么", "什么", "哪些",
                     "有没有", "能不能", "可不可以", "是否", "吗", "呢", "吧", "啊", "can you", "could you", "please", "how do you", "how would you", "what is", "what's", "tell me about"] {
            text = text.replacingOccurrences(of: word, with: " ")
        }
        return text
    }

    /// Character-bigram Jaccard similarity (works for Chinese, English and mixed text).
    public static func similarity(_ a: String, _ b: String) -> Double {
        func grams(_ text: String) -> Set<String> {
            let chars = Array(text.lowercased().filter { $0.isLetter || $0.isNumber })
            guard chars.count > 1 else { return Set(chars.map(String.init)) }
            return Set((0..<(chars.count - 1)).map { String(chars[$0...$0 + 1]) })
        }
        let left = grams(a), right = grams(b)
        guard !left.isEmpty || !right.isEmpty else { return 1 }
        return Double(left.intersection(right).count) / Double(max(1, left.union(right).count))
    }
}
