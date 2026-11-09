import Testing
@testable import NextThoughtCore

struct MeetingCarryTests {
    @Test func customCarryFiltersOnlyTheSelectedBaselineContext() {
        let baseline = ContextDocument(name: "旧资料.pdf", pages: ["baseline material"])
        let addedDuringPreparation = ContextDocument(name: "新资料.pdf", pages: ["new material"])
        let workspace = Workspace(
            name: "项目系列", mode: .meeting,
            documents: [baseline, addedDuringPreparation],
            lessons: [WorkspaceLesson(text: "保留的经验")],
            questionBank: [QuestionBankItem(question: "旧问题？", answer: "旧答案")]
        )
        let carry = MeetingCarry(materials: false, review: false, lessons: false, transcript: true)

        let next = carry.workspaceSnapshot(from: workspace, baselineDocumentIDs: [baseline.id])

        #expect(next.documents.map(\.id) == [addedDuringPreparation.id])
        #expect(next.lessons.isEmpty)
        #expect(next.questionBank.isEmpty)
        #expect(!next.retrievalDocuments.contains { $0.name == "题库（历次复盘）" })
        #expect(carry.transcript)

        let archivedContext = SessionContextSnapshot(mode: next.mode, style: next.style,
                                                     preferences: next.preferences, profile: next.materialText,
                                                     documents: next.documents.filter(\.enabled))
        #expect(archivedContext.documents.map(\.id) == [addedDuringPreparation.id])
        #expect(!archivedContext.documents.contains { $0.name == baseline.name })
    }

    @Test func standardCarryKeepsTheWholeWorkspace() {
        let baseline = ContextDocument(name: "系列资料.pdf", pages: ["material"])
        let workspace = Workspace(
            name: "项目系列", mode: .meeting,
            documents: [baseline],
            lessons: [WorkspaceLesson(text: "经验")],
            questionBank: [QuestionBankItem(question: "问题？", answer: "答案")]
        )

        let next = MeetingCarry.standard.workspaceSnapshot(from: workspace, baselineDocumentIDs: [baseline.id])

        #expect(next == workspace)
    }
}
