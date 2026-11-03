import Foundation
import Testing
@testable import InterviewHackerCore

struct WorkspaceTests {
    let now = Date(timeIntervalSince1970: 1_900_000_000)

    @Test func modesHaveProfilesAndOldValuesStillDecode() throws {
        for mode in SessionMode.allCases {
            let profile = ModeProfile.for(mode)
            #expect(!profile.fields.isEmpty)
            #expect(!profile.documentCategories.isEmpty)
            #expect(!profile.templatePreferences.isEmpty)
        }
        let decoded = try JSONDecoder().decode([SessionMode].self, from: Data(#"["meeting","interview","lesson","futuremode"]"#.utf8))
        #expect(decoded == [.meeting, .interview, .lesson, .general])
        #expect(SessionMode.meeting.title == "工作会议")
    }

    @Test func legacyDefaultAnswerRequirementsFollowModeChanges() {
        let oldDefaults: [(SessionMode, String)] = [
            (.meeting, "我是会议参与者。回答聚焦决策、风险和下一步行动，给出可以直接说出口的简短发言；涉及数据时标注来源；守住资料中我方的立场与底线，不确定时建议会后确认。"),
            (.client, "我是我方代表，正在和客户沟通。回答聚焦客户的需求和顾虑，给出可以直接说出口的简短回应；涉及价格、时间和承诺时以资料为准，不确定的说“会后确认”，不替公司做资料里没有的承诺。"),
            (.general, "直接回答当前问题：先给一句可以说出口的结论，再给 2–3 个要点；涉及资料时标注来源，不确定时说明。"),
        ]
        for (mode, oldValue) in oldDefaults {
            let profile = ModeProfile.for(mode)
            #expect(profile.matchesDefaultTemplatePreferences(oldValue))
            #expect(profile.matchesDefaultTemplatePreferences(profile.templatePreferences))
            #expect(!profile.matchesDefaultTemplatePreferences("我自己的回答要求"))
        }
    }

    @Test func workspaceDecodesOlderFilesAndBuildsMaterial() throws {
        let id = UUID()
        let json = #"{"id":"\#(id.uuidString)","name":"字节二面","mode":"interview"}"#
        var workspace = try JSONDecoder().decode(Workspace.self, from: Data(json.utf8))
        #expect(workspace.name == "字节二面")
        #expect(workspace.useKnowledge)
        workspace.fields = ["company": "字节跳动", "position": "后端工程师", "legacy": "旧字段"]
        workspace.notes = "重点准备缓存"
        let text = workspace.materialText
        #expect(text.hasPrefix("应聘公司：字节跳动\n岗位：后端工程师"))
        #expect(text.contains("legacy：旧字段"))
        #expect(text.hasSuffix("重点准备缓存"))
    }

    @Test func questionBankIsSearchableAndCategoriesLabelSources() {
        var workspace = Workspace(name: "面试", mode: .interview)
        workspace.documents = [ContextDocument(name: "resume.pdf", pages: ["负责订单服务 Redis 缓存层"], category: "简历")]
        workspace.questionBank = [QuestionBankItem(question: "缓存和数据库怎么保证一致", answer: "先更新数据库再删除缓存，binlog 兜底")]
        let index = SessionContextIndex(documents: workspace.retrievalDocuments)
        let hits = index.retrieve(question: "你们缓存一致性怎么保证")
        #expect(hits.contains { $0.source == "[题库] 题库（历次复盘）" })
        #expect(index.retrieve(question: "Redis 缓存层").first?.source == "[简历] resume.pdf")
        workspace.useKnowledge = false
        #expect(workspace.retrievalDocuments.count == 1)
        #expect(workspace.experienceNotes().isEmpty)
    }

    @Test func knowledgeMergeDeduplicatesAndUpdates() {
        var workspace = Workspace(name: "面试", mode: .interview)
        let session = UUID()
        var result = KnowledgeMerger.merge(lessons: ["先说结论再展开", "先说结论再展开。"], bank: [("缓存一致性怎么保证？", "先更新库再删缓存")],
                                           into: &workspace, session: session, now: now)
        #expect(result.lessons == 1)
        #expect(workspace.questionBank.count == 1)
        result = KnowledgeMerger.merge(lessons: ["被问项目时报具体数字"], bank: [("缓存一致性如何保证", "先更新库再删缓存，并用 binlog 兜底")],
                                       into: &workspace, session: session, now: now)
        #expect(result == (1, 1))
        #expect(workspace.questionBank.count == 1)
        #expect(workspace.questionBank[0].timesAsked == 2)
        #expect(workspace.questionBank[0].answer.contains("binlog"))
        workspace.lessons[1].pinned = true
        #expect(workspace.experienceNotes().hasPrefix("- 被问项目时报具体数字"))
    }

    @Test func parsesLessonsAndBankFromReview() {
        let markdown = """
        # 标题
        二面
        ## 问题清单
        1. 介绍一下项目 —— 我的回答要点：订单服务
        ## 经验沉淀
        - 先画时序再讲方案
        - 报出 QPS 等具体数字
        ## 题库更新
        - 问：缓存和数据库怎么保证一致？
          答：先更新数据库再删除缓存。
          删除失败用重试队列兜底。
        - 问：为什么选 Kafka
          答：吞吐高，生态成熟。
        ## 复盘建议
        - 多练
        """
        let parsed = SessionSummaryParser.parseAll(markdown)
        #expect(parsed.title == "二面")
        #expect(parsed.questions.count == 1)
        #expect(parsed.lessons == ["先画时序再讲方案", "报出 QPS 等具体数字"])
        #expect(parsed.bank.count == 2)
        #expect(parsed.bank[0].answer == "先更新数据库再删除缓存。\n删除失败用重试队列兜底。")
        #expect(parsed.bank[1].question == "为什么选 Kafka")
        #expect(SessionSummaryPrompt.system(for: .academic).contains("报告人"))
    }

    @Test func backupMergeNeverOverwritesNewerWork() {
        let id = UUID()
        var mine = Workspace(id: id, name: "A", mode: .interview, updatedAt: now)
        mine.lessons = [WorkspaceLesson(text: "我的经验")]
        var old = Workspace(id: id, name: "A-old", mode: .interview, updatedAt: now.addingTimeInterval(-100))
        old.lessons = [WorkspaceLesson(text: "备份里的经验")]
        let other = Workspace(name: "A", mode: .academic)
        let result = BackupMerge.workspaces(existing: [mine], incoming: [old, other])
        #expect(result.merged.count == 2)
        #expect(result.merged[0].name == "A")
        #expect(result.merged[0].lessons.count == 2)
        #expect(result.merged[1].name == "A（导入）")
        #expect(result.added == 1 && result.updated == 1)
        let newer = Workspace(id: id, name: "A-new", mode: .interview, updatedAt: now.addingTimeInterval(100))
        let second = BackupMerge.workspaces(existing: result.merged, incoming: [newer])
        #expect(second.merged[0].name == "A-new")
        #expect(second.merged[0].lessons.count == 2)
    }

    @Test func manifestValidationAndSettingsFilter() throws {
        let manifest = BackupManifest(kind: .full, appVersion: "1.3.0", appBuild: "20", includesSecrets: false, workspaceCount: 1, sessionCount: 2)
        try manifest.validate()
        var future = manifest; future.version = 99
        #expect(throws: BackupManifest.ValidationError.newerFormat(99)) { try future.validate() }
        let filtered = SettingsSnapshot.filter(["apiModel": "x", "selectedTargetID": "window:1", "AppleLanguages": ["zh"]])
        #expect(filtered.keys.sorted() == ["apiModel"])
        #expect(SettingsSnapshot.secretAccounts.contains("stt.soniox"))
    }
}
