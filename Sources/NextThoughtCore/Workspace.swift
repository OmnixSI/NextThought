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

/// A lesson learned in a review (“结论先行”“缓存一致性要画时序”…).
public struct WorkspaceLesson: Codable, Sendable, Equatable, Identifiable {
    public var id: UUID
    public var text: String
    public var sourceSessionID: UUID?
    /// All meetings that independently support this lesson. Older workspaces may omit it.
    public var sourceSessionIDs: [UUID]?
    public var createdAt: Date
    public var pinned: Bool
    public init(id: UUID = UUID(), text: String, sourceSessionID: UUID? = nil, sourceSessionIDs: [UUID]? = nil,
                createdAt: Date = Date(), pinned: Bool = false) {
        self.id = id; self.text = text; self.sourceSessionID = sourceSessionID
        self.sourceSessionIDs = sourceSessionIDs; self.createdAt = createdAt; self.pinned = pinned
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
    public var revision: Int
    public var history: [WorkspaceSeriesItemRevision]
    public var confirmedAt: Date
    public var updatedAt: Date

    public init(id: UUID = UUID(), kind: Kind, text: String, owner: String? = nil, due: String? = nil,
                dueDate: Date? = nil, actionState: ActionState? = nil, sourceSessionID: UUID,
                evidenceID: String, claimRevision: String, sourceRevision: String,
                sourceQuote: String? = nil, location: String? = nil, revision: Int = 1,
                history: [WorkspaceSeriesItemRevision] = [], confirmedAt: Date = Date(), updatedAt: Date = Date()) {
        self.id = id; self.kind = kind; self.text = text; self.owner = owner; self.due = due; self.dueDate = dueDate
        self.actionState = kind == .action ? (actionState ?? .open) : nil
        self.sourceSessionID = sourceSessionID; self.evidenceID = evidenceID; self.claimRevision = claimRevision
        self.sourceRevision = sourceRevision; self.sourceQuote = sourceQuote; self.location = location
        self.revision = max(1, revision); self.history = history; self.confirmedAt = confirmedAt; self.updatedAt = updatedAt
    }

    public var currentRevision: WorkspaceSeriesItemRevision {
        WorkspaceSeriesItemRevision(id: revisionIdentifier(itemID: id, revision: revision),
                                    revision: revision, kind: kind, text: text, owner: owner, due: due,
                                    dueDate: dueDate, actionState: actionState, sourceSessionID: sourceSessionID,
                                    evidenceID: evidenceID, claimRevision: claimRevision, sourceRevision: sourceRevision,
                                    sourceQuote: sourceQuote, location: location, recordedAt: updatedAt)
    }

    private func revisionIdentifier(itemID: UUID, revision: Int) -> UUID {
        var bytes = Array(SHA256.hash(data: Data("series-item-revision|\(itemID.uuidString.lowercased())|\(revision)".utf8)).prefix(16))
        bytes[6] = (bytes[6] & 0x0F) | 0x50
        bytes[8] = (bytes[8] & 0x3F) | 0x80
        return UUID(uuid: (bytes[0], bytes[1], bytes[2], bytes[3], bytes[4], bytes[5], bytes[6], bytes[7],
                           bytes[8], bytes[9], bytes[10], bytes[11], bytes[12], bytes[13], bytes[14], bytes[15]))
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
    public var recordedAt: Date

    public init(id: UUID = UUID(), revision: Int, kind: WorkspaceSeriesItem.Kind, text: String,
                owner: String? = nil, due: String? = nil, dueDate: Date? = nil,
                actionState: WorkspaceSeriesItem.ActionState? = nil, sourceSessionID: UUID,
                evidenceID: String, claimRevision: String, sourceRevision: String,
                sourceQuote: String? = nil, location: String? = nil, recordedAt: Date = Date()) {
        self.id = id; self.revision = revision; self.kind = kind; self.text = text; self.owner = owner
        self.due = due; self.dueDate = dueDate; self.actionState = actionState; self.sourceSessionID = sourceSessionID
        self.evidenceID = evidenceID; self.claimRevision = claimRevision; self.sourceRevision = sourceRevision
        self.sourceQuote = sourceQuote; self.location = location; self.recordedAt = recordedAt
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
    public var status: Status
    public var receipt: WorkspaceSeriesChangeReceipt?
    public var createdAt: Date

    public init(id: String, kind: WorkspaceSeriesItem.Kind, text: String, owner: String? = nil,
                due: String? = nil, dueDate: Date? = nil, sourceSessionID: UUID,
                evidenceID: String, claimRevision: String, sourceRevision: String,
                sourceQuote: String? = nil, location: String? = nil, status: Status = .pending,
                receipt: WorkspaceSeriesChangeReceipt? = nil, createdAt: Date = Date()) {
        self.id = id; self.kind = kind; self.text = text; self.owner = owner; self.due = due; self.dueDate = dueDate
        self.sourceSessionID = sourceSessionID; self.evidenceID = evidenceID; self.claimRevision = claimRevision
        self.sourceRevision = sourceRevision; self.sourceQuote = sourceQuote; self.location = location
        self.status = status; self.receipt = receipt; self.createdAt = createdAt
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
    public var baseQuestionID: UUID?
    public var baseAnswerRevision: Int?
    public var status: Status
    public var receipt: WorkspaceKnowledgeReceipt?
    public var createdAt: Date

    public enum Status: String, Codable, Sendable { case pending, accepted, rejected }

    public init(id: String, logicalItemID: String, kind: WorkspaceKnowledgeKind, text: String,
                proposedAnswer: String? = nil, sourceSessionID: UUID? = nil,
                baseQuestionID: UUID? = nil, baseAnswerRevision: Int? = nil,
                status: Status = .pending, receipt: WorkspaceKnowledgeReceipt? = nil, createdAt: Date = Date()) {
        self.id = id; self.logicalItemID = logicalItemID; self.kind = kind; self.text = text
        self.proposedAnswer = proposedAnswer; self.sourceSessionID = sourceSessionID
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
                stageReview: String? = nil, stageReviewAt: Date? = nil, createdAt: Date = Date(), updatedAt: Date = Date(), archived: Bool = false, skillIDs: [UUID] = []) {
        self.id = id; self.name = name; self.mode = mode; self.style = style
        self.preferences = preferences ?? ModeProfile.for(mode).templatePreferences
        self.notes = notes; self.fields = fields; self.documents = documents; self.lessons = lessons
        self.questionBank = questionBank; self.knowledgeCandidates = knowledgeCandidates; self.useKnowledge = useKnowledge
        self.seriesItems = seriesItems; self.seriesChangeCandidates = seriesChangeCandidates
        self.stageReview = stageReview; self.stageReviewAt = stageReviewAt
        self.createdAt = createdAt; self.updatedAt = updatedAt; self.archived = archived
        self.skillIDs = skillIDs
        self.customTypeName = nil
        self.customBaseMode = nil
    }

    // Tolerant decoding: fields added in later versions get defaults.
    enum CodingKeys: String, CodingKey {
        case id, name, mode, style, preferences, notes, fields, documents, lessons, questionBank, knowledgeCandidates, useKnowledge
        case seriesItems, seriesChangeCandidates
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
        if useKnowledge, !questionBank.isEmpty {
            let pages = questionBank.map { "问题：\($0.question)\n准备的回答：\($0.answer)" }
            result.append(ContextDocument(id: id, name: "题库（历次复盘）", pages: pages, enabled: true, category: "题库"))
        }
        return result
    }
}

// MARK: - Merging review output into a workspace

public enum KnowledgeMerger {
    /// Turn generated notes into inert, deterministic suggestions. Re-running the
    /// same summary keeps the existing candidate and its review receipt.
    @discardableResult
    public static func collectCandidates(lessons newLessons: [String], bank newItems: [BankEntry],
                                         into workspace: inout Workspace, session: UUID?,
                                         originID: UUID? = nil, now: Date = Date()) -> [String] {
        let originID = originID ?? session ?? workspace.id
        var candidates = workspace.knowledgeCandidates ?? []
        var knownIDs = Set(candidates.map(\.id))
        var added: [String] = []

        for raw in newLessons {
            let text = raw.trimmingCharacters(in: .whitespacesAndNewlines)
            guard text.count >= 4 else { continue }
            let logicalID = logicalItemID(kind: .lesson, text: text)
            let id = candidateID(originID: originID, session: session, kind: .lesson,
                                 logicalID: logicalID, answer: "")
            guard knownIDs.insert(id).inserted else { continue }
            candidates.append(WorkspaceKnowledgeCandidate(id: id, logicalItemID: logicalID, kind: .lesson,
                                                           text: text, sourceSessionID: session, createdAt: now))
            added.append(id)
        }

        for item in newItems {
            let question = item.question.trimmingCharacters(in: .whitespacesAndNewlines)
            let answer = item.answer.trimmingCharacters(in: .whitespacesAndNewlines)
            guard question.count >= 3, !answer.isEmpty else { continue }
            let logicalID = logicalItemID(kind: .question, text: question)
            let id = candidateID(originID: originID, session: session, kind: .question,
                                 logicalID: logicalID, answer: answer)
            guard knownIDs.insert(id).inserted else { continue }
            let match = workspace.questionBank.first { similarQuestion($0.question, question) }
            candidates.append(WorkspaceKnowledgeCandidate(id: id, logicalItemID: logicalID, kind: .question,
                                                           text: question, proposedAnswer: answer, sourceSessionID: session,
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
                sourceCounted = addSource(candidate.sourceSessionID, to: &workspace.lessons[index].sourceSessionIDs,
                                          legacySource: workspace.lessons[index].sourceSessionID)
            } else {
                guard decision == .add else { return nil }
                let id = UUID()
                workspace.lessons.append(WorkspaceLesson(id: id, text: candidate.text,
                                                          sourceSessionID: candidate.sourceSessionID,
                                                          sourceSessionIDs: candidate.sourceSessionID.map { [$0] }, createdAt: now))
                resultItemID = id
                sourceCounted = candidate.sourceSessionID != nil
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
                        item.answerSourceSessionID = candidate.sourceSessionID
                        item.answerRevision = (item.answerRevision ?? 0) + 1
                        item.updatedAt = now
                    }
                }
                sourceCounted = recordQuestionSource(candidate.sourceSessionID, on: &item)
                workspace.questionBank[index] = item
                resultItemID = item.id
            } else {
                guard decision == .add else { return nil }
                let id = UUID()
                workspace.questionBank.append(QuestionBankItem(id: id, question: candidate.text, answer: proposed,
                                                                 timesAsked: candidate.sourceSessionID == nil ? 0 : 1,
                                                                 sourceSessionID: candidate.sourceSessionID,
                                                                 sourceSessionIDs: candidate.sourceSessionID.map { [$0] },
                                                                 answerSourceSessionID: candidate.sourceSessionID,
                                                                 answerRevision: 0, updatedAt: now))
                resultItemID = id
                sourceCounted = candidate.sourceSessionID != nil
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

    private static func recordQuestionSource(_ session: UUID?, on item: inout QuestionBankItem) -> Bool {
        guard let session else { return false }
        var sources = Set(item.sourceSessionIDs ?? [])
        if let legacy = item.sourceSessionID { sources.insert(legacy) }
        guard sources.insert(session).inserted else {
            item.sourceSessionIDs = sources.sorted { $0.uuidString < $1.uuidString }
            return false
        }
        item.sourceSessionIDs = sources.sorted { $0.uuidString < $1.uuidString }
        if item.sourceSessionID == nil { item.sourceSessionID = session }
        item.timesAsked += 1
        return true
    }

    private static func addSource(_ session: UUID?, to sourcesValue: inout [UUID]?, legacySource: UUID?) -> Bool {
        guard let session else { return false }
        var sources = Set(sourcesValue ?? [])
        if let legacySource { sources.insert(legacySource) }
        let inserted = sources.insert(session).inserted
        sourcesValue = sources.sorted { $0.uuidString < $1.uuidString }
        return inserted
    }

    private static func logicalItemID(kind: WorkspaceKnowledgeKind, text: String) -> String {
        // Identity keeps the original question intent (“why” versus “how”); only
        // display matching may use the intentionally looser question-key normalizer.
        return stableDigest("\(kind.rawValue)|\(normalized(text))")
    }

    private static func candidateID(originID: UUID, session: UUID?, kind: WorkspaceKnowledgeKind,
                                     logicalID: String, answer: String) -> String {
        stableDigest("\(originID.uuidString.lowercased())|\(session?.uuidString.lowercased() ?? "")|\(kind.rawValue)|\(logicalID)|\(normalized(answer))")
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
