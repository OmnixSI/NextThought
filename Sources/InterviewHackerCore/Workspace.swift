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
                templatePreferences: "我是会议参与者。回答聚焦决策、风险和下一步行动，给出可以直接说出口的简短发言；涉及数据时标注来源；守住资料中我方的立场与底线，不确定时建议会后确认。",
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
        case .general:
            return ModeProfile(
                mode: mode, role: "参与者",
                fields: [ModeField("context", "场景说明", "这是什么场合、对方是谁、我希望达成什么", long: true)],
                documentCategories: ["资料", "其他"],
                templatePreferences: "直接回答当前问题：先给一句可以说出口的结论，再给 2–3 个要点；涉及资料时标注来源，不确定时说明。",
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
    public var createdAt: Date
    public var pinned: Bool
    public init(id: UUID = UUID(), text: String, sourceSessionID: UUID? = nil, createdAt: Date = Date(), pinned: Bool = false) {
        self.id = id; self.text = text; self.sourceSessionID = sourceSessionID; self.createdAt = createdAt; self.pinned = pinned
    }
}

/// A question that came up, with the best answer so far (改进后的回答).
public struct QuestionBankItem: Codable, Sendable, Equatable, Identifiable {
    public var id: UUID
    public var question: String
    public var answer: String
    public var timesAsked: Int
    public var sourceSessionID: UUID?
    public var updatedAt: Date
    public init(id: UUID = UUID(), question: String, answer: String, timesAsked: Int = 1, sourceSessionID: UUID? = nil, updatedAt: Date = Date()) {
        self.id = id; self.question = question; self.answer = answer; self.timesAsked = timesAsked
        self.sourceSessionID = sourceSessionID; self.updatedAt = updatedAt
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

    public init(id: UUID = UUID(), name: String, mode: SessionMode, style: AnswerStyle = .concise, preferences: String? = nil,
                notes: String = "", fields: [String: String] = [:], documents: [ContextDocument] = [],
                lessons: [WorkspaceLesson] = [], questionBank: [QuestionBankItem] = [], useKnowledge: Bool = true,
                stageReview: String? = nil, stageReviewAt: Date? = nil, createdAt: Date = Date(), updatedAt: Date = Date(), archived: Bool = false, skillIDs: [UUID] = []) {
        self.id = id; self.name = name; self.mode = mode; self.style = style
        self.preferences = preferences ?? ModeProfile.for(mode).templatePreferences
        self.notes = notes; self.fields = fields; self.documents = documents; self.lessons = lessons
        self.questionBank = questionBank; self.useKnowledge = useKnowledge
        self.stageReview = stageReview; self.stageReviewAt = stageReviewAt
        self.createdAt = createdAt; self.updatedAt = updatedAt; self.archived = archived
        self.skillIDs = skillIDs
    }

    // Tolerant decoding: fields added in later versions get defaults.
    enum CodingKeys: String, CodingKey {
        case id, name, mode, style, preferences, notes, fields, documents, lessons, questionBank, useKnowledge
        case stageReview, stageReviewAt, createdAt, updatedAt, archived, skillIDs
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
        useKnowledge = try c.decodeIfPresent(Bool.self, forKey: .useKnowledge) ?? true
        stageReview = try c.decodeIfPresent(String.self, forKey: .stageReview)
        stageReviewAt = try c.decodeIfPresent(Date.self, forKey: .stageReviewAt)
        createdAt = try c.decodeIfPresent(Date.self, forKey: .createdAt) ?? Date()
        updatedAt = try c.decodeIfPresent(Date.self, forKey: .updatedAt) ?? createdAt
        archived = try c.decodeIfPresent(Bool.self, forKey: .archived) ?? false
        skillIDs = try c.decodeIfPresent([UUID].self, forKey: .skillIDs) ?? []
    }

    public var profile: ModeProfile { ModeProfile.for(mode) }

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
    /// Adds lessons (skipping near-duplicates) and question-bank entries (updating the
    /// answer and count when a similar question already exists).
    public static func merge(lessons newLessons: [String], bank newItems: [(question: String, answer: String)],
                             into workspace: inout Workspace, session: UUID?, now: Date = Date()) -> (lessons: Int, questions: Int) {
        var addedLessons = 0, touchedQuestions = 0
        for text in newLessons.map({ $0.trimmingCharacters(in: .whitespacesAndNewlines) }) where text.count >= 4 {
            if workspace.lessons.contains(where: { similarity($0.text, text) >= 0.6 }) { continue }
            workspace.lessons.append(WorkspaceLesson(text: text, sourceSessionID: session, createdAt: now))
            addedLessons += 1
        }
        for item in newItems {
            let question = item.question.trimmingCharacters(in: .whitespacesAndNewlines)
            let answer = item.answer.trimmingCharacters(in: .whitespacesAndNewlines)
            guard question.count >= 3, !answer.isEmpty else { continue }
            if let index = workspace.questionBank.firstIndex(where: { similarity(questionKey($0.question), questionKey(question)) >= 0.55 }) {
                workspace.questionBank[index].answer = answer
                workspace.questionBank[index].timesAsked += 1
                workspace.questionBank[index].updatedAt = now
                workspace.questionBank[index].sourceSessionID = session
            } else {
                workspace.questionBank.append(QuestionBankItem(question: question, answer: answer, sourceSessionID: session, updatedAt: now))
            }
            touchedQuestions += 1
        }
        if addedLessons + touchedQuestions > 0 { workspace.updatedAt = now }
        return (addedLessons, touchedQuestions)
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
