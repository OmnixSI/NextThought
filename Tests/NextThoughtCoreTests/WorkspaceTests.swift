import Foundation
import Testing
@testable import NextThoughtCore

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

    @Test func knowledgeCandidatesStayPendingUntilAcceptedAndRepeatedReviewsDoNotCountTwice() throws {
        var workspace = Workspace(name: "面试", mode: .interview)
        let earlierSession = UUID(), session = UUID()
        workspace.questionBank = [QuestionBankItem(question: "缓存一致性怎么保证？", answer: "旧答法保留", timesAsked: 2,
                                                    sourceSessionID: earlierSession, updatedAt: now)]
        let first = KnowledgeMerger.collectCandidates(lessons: ["先说结论再展开", "先说结论再展开。"],
                                                       bank: [BankEntry(question: "缓存一致性怎么保证？", answer: "先更新库再删缓存")],
                                                       into: &workspace, session: session, now: now)
        #expect(first.count == 2)
        #expect(workspace.questionBank.count == 1)
        #expect(workspace.questionBank[0].timesAsked == 2)
        #expect(workspace.questionBank[0].answer == "旧答法保留")
        let again = KnowledgeMerger.collectCandidates(lessons: ["先说结论再展开"],
                                                       bank: [BankEntry(question: "缓存一致性怎么保证？", answer: "先更新库再删缓存")],
                                                       into: &workspace, session: session, now: now)
        #expect(again.isEmpty)

        let candidate = try #require(workspace.knowledgeCandidates?.first { $0.kind == .question })
        let receipt = try #require(KnowledgeMerger.applyCandidate(candidate.id, decision: .keepExisting,
                                                                  operationID: UUID(), into: &workspace, now: now))
        #expect(workspace.questionBank[0].answer == "旧答法保留")
        #expect(workspace.questionBank[0].timesAsked == 3)
        #expect(workspace.questionBank[0].sourceSessionIDs == [earlierSession, session].sorted { $0.uuidString < $1.uuidString })
        let retry = KnowledgeMerger.applyCandidate(candidate.id, decision: .replaceAnswer,
                                                   operationID: UUID(), into: &workspace, now: now.addingTimeInterval(1))
        #expect(retry == receipt)
        #expect(workspace.questionBank[0].timesAsked == 3)
        let data = try JSONEncoder().encode(workspace)
        var restored = try JSONDecoder().decode(Workspace.self, from: data)
        let afterRestart = KnowledgeMerger.applyCandidate(candidate.id, decision: .replaceAnswer,
                                                           operationID: UUID(), into: &restored, now: now.addingTimeInterval(2))
        #expect(afterRestart == receipt)
        #expect(restored.questionBank[0].timesAsked == 3)
    }

    @Test func replacingAnAnswerRequiresFreshRevisionAndKeepsTheOldAnswer() throws {
        var workspace = Workspace(name: "面试", mode: .interview)
        workspace.questionBank = [QuestionBankItem(question: "Redis 缓存雪崩怎么处理", answer: "人工维护的答法", timesAsked: 4)]
        let session = UUID()
        let ids = KnowledgeMerger.collectCandidates(lessons: [],
                                                     bank: [BankEntry(question: "Redis 缓存雪崩如何处理", answer: "分批过期并加随机值")],
                                                     into: &workspace, session: session, now: now)
        let candidateID = try #require(ids.first)
        KnowledgeMerger.editQuestionBankItem(&workspace.questionBank[0],
                                             question: "Redis 缓存雪崩如何处理", answer: "用户后来手动调整的答法",
                                             now: now.addingTimeInterval(1))
        #expect(KnowledgeMerger.applyCandidate(candidateID, decision: .replaceAnswer,
                                               operationID: UUID(), into: &workspace, now: now) == nil)
        #expect(KnowledgeMerger.refreshCandidateBase(candidateID, in: &workspace))
        let receipt = try #require(KnowledgeMerger.applyCandidate(candidateID, decision: .replaceAnswer,
                                                                  operationID: UUID(), into: &workspace, now: now))
        #expect(receipt.decision == .replaceAnswer)
        #expect(workspace.questionBank[0].answer == "分批过期并加随机值")
        #expect(workspace.questionBank[0].answerSourceSessionID == session)
        #expect(workspace.questionBank[0].answerHistory?.map(\.answer) == ["人工维护的答法", "用户后来手动调整的答法"])
        #expect(workspace.questionBank[0].timesAsked == 5)
    }

    @Test func replacingAnAnswerPreservesItsSourceAttribution() throws {
        var workspace = Workspace(name: "面试", mode: .interview)
        let originalQuestionSession = UUID(), answerSession = UUID(), candidateSession = UUID()
        workspace.questionBank = [QuestionBankItem(question: "Redis 缓存雪崩怎么处理", answer: "原答法",
                                                    timesAsked: 2, sourceSessionID: originalQuestionSession,
                                                    sourceSessionIDs: [originalQuestionSession, answerSession],
                                                    answerSourceSessionID: answerSession, answerRevision: 2,
                                                    updatedAt: now)]
        let ids = KnowledgeMerger.collectCandidates(lessons: [],
                                                     bank: [BankEntry(question: "Redis 缓存雪崩如何处理", answer: "新答法")],
                                                     into: &workspace, session: candidateSession, now: now)
        let candidateID = try #require(ids.first)

        _ = try #require(KnowledgeMerger.applyCandidate(candidateID, decision: .replaceAnswer,
                                                         operationID: UUID(), into: &workspace,
                                                         now: now.addingTimeInterval(1)))

        #expect(workspace.questionBank[0].answer == "新答法")
        #expect(workspace.questionBank[0].answerSourceSessionID == candidateSession)
        #expect(workspace.questionBank[0].answerHistory?.count == 1)
        #expect(workspace.questionBank[0].answerHistory?.first?.answer == "原答法")
        #expect(workspace.questionBank[0].answerHistory?.first?.sourceSessionID == answerSession)
    }

    @Test func staleKeepExistingRequiresReviewOfTheCurrentQuestionRevision() throws {
        var workspace = Workspace(name: "面试", mode: .interview)
        let existingSession = UUID(), candidateSession = UUID()
        workspace.questionBank = [QuestionBankItem(question: "Redis 缓存雪崩怎么处理", answer: "原答法", timesAsked: 2,
                                                    sourceSessionID: existingSession, sourceSessionIDs: [existingSession])]
        let candidateIDs = KnowledgeMerger.collectCandidates(lessons: [],
                                                              bank: [BankEntry(question: "Redis 缓存雪崩如何处理", answer: "候选答法")],
                                                              into: &workspace, session: candidateSession, now: now)
        let candidateID = try #require(candidateIDs.first)

        KnowledgeMerger.editQuestionBankItem(&workspace.questionBank[0],
                                             question: "Redis 缓存雪崩怎么处理", answer: "刚刚手动更新的答法",
                                             now: now.addingTimeInterval(1))
        #expect(KnowledgeMerger.applyCandidate(candidateID, decision: .keepExisting,
                                               operationID: UUID(), into: &workspace, now: now) == nil)
        #expect(workspace.questionBank[0].answer == "刚刚手动更新的答法")
        #expect(workspace.questionBank[0].timesAsked == 2)
        #expect(workspace.questionBank[0].sourceSessionIDs == [existingSession])

        #expect(KnowledgeMerger.refreshCandidateBase(candidateID, in: &workspace))
        let receipt = try #require(KnowledgeMerger.applyCandidate(candidateID, decision: .keepExisting,
                                                                  operationID: UUID(), into: &workspace, now: now))
        #expect(receipt.sourceCounted)
        #expect(workspace.questionBank[0].answer == "刚刚手动更新的答法")
        #expect(workspace.questionBank[0].timesAsked == 3)
        #expect(Set(workspace.questionBank[0].sourceSessionIDs ?? []) == [existingSession, candidateSession])
    }

    @Test func keepingAnExistingLessonIsRejectedWhenNoSimilarLessonRemains() throws {
        var workspace = Workspace(name: "面试", mode: .interview)
        let ids = KnowledgeMerger.collectCandidates(lessons: ["面试回答先说结论，再按证据展开"], bank: [],
                                                     into: &workspace, session: UUID(), now: now)
        let candidateID = try #require(ids.first)

        #expect(KnowledgeMerger.applyCandidate(candidateID, decision: .keepExisting,
                                               operationID: UUID(), into: &workspace, now: now) == nil)
        #expect(workspace.lessons.isEmpty)
        #expect(workspace.knowledgeCandidates?.first?.status == .pending)
    }

    @Test func editingQuestionWordingInvalidatesOpenReplacementWithoutErasingProvenance() throws {
        var workspace = Workspace(name: "面试", mode: .interview)
        let sourceSession = UUID(), currentSession = UUID()
        let oldRevision = WorkspaceAnswerRevision(answer: "更早的答法", recordedAt: now.addingTimeInterval(-10))
        workspace.questionBank = [QuestionBankItem(question: "Redis 缓存雪崩怎么处理", answer: "人工维护的答法",
                                                    timesAsked: 4, sourceSessionID: sourceSession,
                                                    sourceSessionIDs: [sourceSession], answerHistory: [oldRevision],
                                                    answerRevision: 3, updatedAt: now)]
        let ids = KnowledgeMerger.collectCandidates(lessons: [],
                                                     bank: [BankEntry(question: "Redis 缓存雪崩如何处理", answer: "分批过期并加随机值")],
                                                     into: &workspace, session: currentSession, now: now)
        let candidateID = try #require(ids.first)

        KnowledgeMerger.editQuestionBankItem(&workspace.questionBank[0],
                                             question: "Redis 缓存雪崩如何处理", answer: "人工维护的答法",
                                             now: now.addingTimeInterval(1))
        #expect(workspace.questionBank[0].answerRevision == 4)
        #expect(workspace.questionBank[0].answerHistory == [oldRevision])
        #expect(workspace.questionBank[0].sourceSessionIDs == [sourceSession])
        #expect(KnowledgeMerger.applyCandidate(candidateID, decision: .replaceAnswer,
                                               operationID: UUID(), into: &workspace, now: now) == nil)
        #expect(KnowledgeMerger.refreshCandidateBase(candidateID, in: &workspace))

        let receipt = try #require(KnowledgeMerger.applyCandidate(candidateID, decision: .replaceAnswer,
                                                                  operationID: UUID(), into: &workspace, now: now))
        #expect(receipt.sourceCounted)
        #expect(workspace.questionBank[0].answer == "分批过期并加随机值")
        #expect(workspace.questionBank[0].answerHistory?.map(\.answer) == ["更早的答法", "人工维护的答法"])
        #expect(workspace.questionBank[0].sourceSessionIDs?.contains(currentSession) == true)
    }

    @Test func whyAndHowQuestionsRemainSeparateCandidates() {
        var workspace = Workspace(name: "面试", mode: .interview)
        let ids = KnowledgeMerger.collectCandidates(lessons: [], bank: [
            BankEntry(question: "为什么选择 Redis？", answer: "需要低延迟缓存"),
            BankEntry(question: "如何使用 Redis？", answer: "设置过期时间并处理并发")
        ], into: &workspace, session: UUID(), now: now)
        #expect(ids.count == 2)
        #expect(Set(ids).count == 2)
    }

    @Test func legacyKnowledgeFieldsRemainReadable() throws {
        let session = UUID()
        let workspaceID = UUID(), lessonID = UUID(), questionID = UUID()
        let json = """
        {"id":"\(workspaceID.uuidString)","name":"旧场景","mode":"interview","lessons":[{"id":"\(lessonID.uuidString)","text":"旧经验","sourceSessionID":"\(session.uuidString)","createdAt":0,"pinned":false}],"questionBank":[{"id":"\(questionID.uuidString)","question":"旧问题","answer":"旧答法","timesAsked":3,"sourceSessionID":"\(session.uuidString)","updatedAt":0}]}
        """
        let workspace = try JSONDecoder().decode(Workspace.self, from: Data(json.utf8))
        #expect(workspace.questionBank.first?.answer == "旧答法")
        #expect(workspace.questionBank.first?.answerHistory == nil)
        #expect(workspace.questionBank.first?.answerRevision == nil)
        #expect(workspace.knowledgeCandidates == nil)
    }

    @Test func legacyReaderCanReadNewWorkspaceFields() throws {
        let item = QuestionBankItem(question: "缓存问题", answer: "新答法", timesAsked: 2,
                                    answerHistory: [WorkspaceAnswerRevision(answer: "旧答法", recordedAt: now)],
                                    answerRevision: 1, updatedAt: now)
        var workspace = Workspace(name: "新场景", mode: .interview, questionBank: [item])
        workspace.knowledgeCandidates = [WorkspaceKnowledgeCandidate(id: "candidate", logicalItemID: "question",
                                                                      kind: .question, text: "缓存问题",
                                                                      proposedAnswer: "待审阅答法")]
        let data = try JSONEncoder().encode(workspace)
        let oldView = try JSONDecoder().decode(LegacyWorkspaceView.self, from: data)

        #expect(oldView.name == "新场景")
        #expect(oldView.questionBank.first?.answer == "新答法")
        #expect(oldView.questionBank.first?.timesAsked == 2)
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

    @Test func backupMergePreservesKnowledgeSourcesHistoryAndReviewReceipt() throws {
        let workspaceID = UUID(), lessonID = UUID(), questionID = UUID()
        let oldSession = UUID(), sharedSession = UUID(), currentSession = UUID()
        let sharedRevision = WorkspaceAnswerRevision(answer: "共同历史", recordedAt: now.addingTimeInterval(-300))
        let olderRevision = WorkspaceAnswerRevision(answer: "备份中的旧答法", recordedAt: now.addingTimeInterval(-200),
                                                    sourceSessionID: oldSession)
        let currentRevision = WorkspaceAnswerRevision(answer: "本机历史", recordedAt: now.addingTimeInterval(-100),
                                                      sourceSessionID: sharedSession)
        var current = Workspace(id: workspaceID, name: "A", mode: .interview, updatedAt: now)
        current.lessons = [WorkspaceLesson(id: lessonID, text: "当前经验", sourceSessionID: currentSession,
                                           sourceSessionIDs: [sharedSession, currentSession],
                                           createdAt: now.addingTimeInterval(-50), pinned: true)]
        current.questionBank = [QuestionBankItem(id: questionID, question: "缓存雪崩如何处理", answer: "当前答法",
                                                 timesAsked: 5, sourceSessionID: currentSession,
                                                 sourceSessionIDs: [sharedSession, currentSession],
                                                 answerSourceSessionID: currentSession,
                                                 answerHistory: [sharedRevision, currentRevision], answerRevision: 4,
                                                 updatedAt: now)]
        let acceptedReceipt = WorkspaceKnowledgeReceipt(operationID: UUID(), candidateID: "same-candidate",
                                                        decision: .keepExisting, reviewedAt: now.addingTimeInterval(-25),
                                                        resultItemID: questionID, sourceCounted: true)
        current.knowledgeCandidates = [WorkspaceKnowledgeCandidate(id: "same-candidate", logicalItemID: "question",
                                                                   kind: .question, text: "缓存雪崩如何处理",
                                                                   proposedAnswer: "备选答法")]

        var backup = Workspace(id: workspaceID, name: "A backup", mode: .interview,
                               updatedAt: now.addingTimeInterval(-20))
        backup.lessons = [WorkspaceLesson(id: lessonID, text: "较旧经验", sourceSessionID: oldSession,
                                          sourceSessionIDs: [oldSession, sharedSession],
                                          createdAt: now.addingTimeInterval(-150))]
        backup.questionBank = [QuestionBankItem(id: questionID, question: "缓存雪崩如何处理", answer: "旧答法",
                                                 timesAsked: 4, sourceSessionID: oldSession,
                                                 sourceSessionIDs: [oldSession, sharedSession],
                                                 answerSourceSessionID: oldSession,
                                                 answerHistory: [sharedRevision, olderRevision], answerRevision: 2,
                                                 updatedAt: now.addingTimeInterval(-10))]
        let acceptedCandidate = WorkspaceKnowledgeCandidate(id: "same-candidate", logicalItemID: "question",
                                                             kind: .question, text: "缓存雪崩如何处理",
                                                             proposedAnswer: "备选答法", status: .accepted,
                                                             receipt: acceptedReceipt, createdAt: now.addingTimeInterval(-80))
        backup.knowledgeCandidates = [acceptedCandidate]

        let merged = BackupMerge.workspaces(existing: [current], incoming: [backup])
        let workspace = try #require(merged.merged.first)
        let lesson = try #require(workspace.lessons.first)
        let question = try #require(workspace.questionBank.first)
        let candidate = try #require(workspace.knowledgeCandidates?.first)

        #expect(workspace.name == "A")
        #expect(lesson.text == "当前经验")
        #expect(lesson.pinned)
        #expect(lesson.createdAt == now.addingTimeInterval(-150))
        #expect(Set(lesson.sourceSessionIDs ?? []) == [oldSession, sharedSession, currentSession])
        #expect(question.answer == "当前答法")
        #expect(question.answerSourceSessionID == currentSession)
        #expect(question.answerRevision == 5)
        #expect(question.timesAsked == 5)
        #expect(Set(question.sourceSessionIDs ?? []) == [oldSession, sharedSession, currentSession])
        let preservedBackupAnswer = try #require(question.answerHistory?.first { $0.answer == "旧答法" })
        #expect(preservedBackupAnswer.recordedAt == backup.questionBank[0].updatedAt)
        #expect(preservedBackupAnswer.sourceSessionID == oldSession)
        #expect(Set(question.answerHistory?.map(\.id) ?? []) == Set([sharedRevision.id, olderRevision.id,
                                                                      currentRevision.id, preservedBackupAnswer.id]))
        #expect(candidate.status == .accepted)
        #expect(candidate.receipt == acceptedReceipt)
        #expect(candidate.receipt?.resultItemID == questionID)
        #expect(merged.updated == 1)
    }

    @Test func backupMergePreservesLosingManualAnswerOnceWithItsSourceAndTimestamp() throws {
        let workspaceID = UUID(), questionID = UUID(), sourceSessionID = UUID()
        let manualEditAt = now.addingTimeInterval(-60)
        let acceptedAt = now
        var manualItem = QuestionBankItem(id: questionID, question: "缓存雪崩怎么处理", answer: "初始答法",
                                           sourceSessionID: sourceSessionID, answerSourceSessionID: sourceSessionID,
                                           updatedAt: now.addingTimeInterval(-120))
        KnowledgeMerger.editQuestionBankItem(&manualItem, question: "缓存雪崩怎么处理",
                                             answer: "手动维护的答法", now: manualEditAt)
        #expect(manualItem.answerHistory?.map(\.answer) == ["初始答法"])
        #expect(manualItem.answerSourceSessionID == nil)
        #expect(manualItem.knownAnswerSourceSessionID == nil)

        var current = Workspace(id: workspaceID, name: "面试", mode: .interview, updatedAt: acceptedAt)
        current.questionBank = [QuestionBankItem(id: questionID, question: manualItem.question, answer: "已接受的建议",
                                                  sourceSessionID: sourceSessionID, answerSourceSessionID: UUID(),
                                                  answerRevision: 2, updatedAt: acceptedAt)]
        var backup = Workspace(id: workspaceID, name: "面试备份", mode: .interview,
                               updatedAt: acceptedAt.addingTimeInterval(-1))
        backup.questionBank = [manualItem]

        let firstMerge = try #require(BackupMerge.workspaces(existing: [current], incoming: [backup]).merged.first)
        let firstQuestion = try #require(firstMerge.questionBank.first)
        let preserved = try #require(firstQuestion.answerHistory?.first(where: { $0.answer == "手动维护的答法" }))
        #expect(firstQuestion.answer == "已接受的建议")
        #expect(preserved.recordedAt == manualEditAt)
        #expect(preserved.sourceSessionID == nil)

        let secondMerge = try #require(BackupMerge.workspaces(existing: [firstMerge], incoming: [backup]).merged.first)
        let secondQuestion = try #require(secondMerge.questionBank.first)
        let preservedRevisions = secondQuestion.answerHistory?.filter { $0.answer == "手动维护的答法" } ?? []
        #expect(preservedRevisions.count == 1)
        #expect(preservedRevisions.first?.recordedAt == manualEditAt)
        #expect(preservedRevisions.first?.sourceSessionID == nil)
    }

    @Test func manifestValidationAndSettingsFilter() throws {
        let manifest = BackupManifest(kind: .full, appVersion: "1.3.0", appBuild: "20", includesSecrets: false, workspaceCount: 1, sessionCount: 2)
        try manifest.validate()
        #expect(manifest.format == "nextthought-backup")
        var legacy = manifest; legacy.format = "interviewhacker-backup"
        try legacy.validate()
        var future = manifest; future.version = 99
        #expect(throws: BackupManifest.ValidationError.newerFormat(99)) { try future.validate() }
        let filtered = SettingsSnapshot.filter(["apiModel": "x", "selectedTargetID": "window:1", "AppleLanguages": ["zh"]])
        #expect(filtered.keys.sorted() == ["apiModel"])
        #expect(SettingsSnapshot.secretAccounts.contains("stt.soniox"))
    }

    @Test func restoreMapsLegacyAutoApplyLessonsPreferenceToCandidateCollection() {
        let legacy = SettingsSnapshot.valuesForRestore(["autoApplyLessons": false, "apiModel": "legacy-model",
                                                        "selectedTargetID": "window:1"])
        #expect(legacy["autoCollectKnowledgeCandidates"] as? Bool == false)
        #expect(legacy["autoApplyLessons"] == nil)
        #expect(legacy["apiModel"] as? String == "legacy-model")
        #expect(legacy["selectedTargetID"] == nil)

        let current = SettingsSnapshot.valuesForRestore(["autoApplyLessons": true,
                                                         "autoCollectKnowledgeCandidates": false])
        #expect(current["autoCollectKnowledgeCandidates"] as? Bool == false)
        #expect(current["autoApplyLessons"] == nil)

        let currentOnly = SettingsSnapshot.valuesForRestore(["autoCollectKnowledgeCandidates": true])
        #expect(currentOnly["autoCollectKnowledgeCandidates"] as? Bool == true)
    }
}

private struct LegacyWorkspaceView: Decodable {
    let name: String
    let questionBank: [LegacyQuestion]
}

private struct LegacyQuestion: Decodable {
    let answer: String
    let timesAsked: Int
}
