import Foundation
import Testing
@testable import NextThoughtCore

struct MeetingPreparationTests {
    @Test func singleMeetingTypeAppliesWithoutChangingSeriesDefaults() {
        let series = Workspace(name: "周组会", mode: .labMeeting, style: .detailed)
        let result = MeetingPlan(mode: .academic).preparedWorkspace(from: series)
        #expect(result.mode == .academic)
        #expect(result.style == .concise)
        #expect(result.preferences == ModeProfile.for(.academic).templatePreferences)
        #expect(series.mode == .labMeeting && series.style == .detailed)
    }

    @Test func sceneStyleAppliesToEveryMeetingUnlessTheSeriesChoseItsOwn() {
        // Same type as the series: a lab meeting answers in detail by default.
        let plain = Workspace(name: "组会", mode: .labMeeting)
        #expect(MeetingPlan(mode: .labMeeting).preparedWorkspace(from: plain).style == .detailed)
        // The scene setting wins over the type's default.
        #expect(MeetingPlan(mode: .labMeeting).preparedWorkspace(from: plain, sceneStyle: .concise).style == .concise)
        // A series whose length differs from its type's default keeps it.
        let explicit = Workspace(name: "组会", mode: .labMeeting, style: .concise)
        #expect(MeetingPlan(mode: .labMeeting).preparedWorkspace(from: explicit, sceneStyle: .detailed).style == .concise)
    }

    @Test func excludedCarryNeverReappearsAndNewFilesStayAvailable() {
        let old = ContextDocument(name: "旧论文", pages: ["prior evidence"])
        let new = ContextDocument(name: "新实验", pages: ["current evidence"])
        let transcript = ContextDocument(name: "指定的上一场", pages: ["selected meeting"])
        var series = Workspace(name: "研究讨论", mode: .labMeeting)
        series.documents = [old, new]
        series.lessons = [.init(text: "prior lesson")]
        series.questionBank = [.init(question: "prior question", answer: "prior answer")]
        let original = series
        let previous = UUID()
        let plan = MeetingPlan(mode: .discussion, carry: .init(materials: false, review: false, lessons: false, transcript: true),
            baselineDocumentIDs: [old.id], previousSessionID: previous)
        let preview = plan.preparedWorkspace(from: series, previousTranscript: transcript)
        let live = plan.preparedWorkspace(from: series, previousTranscript: transcript)
        #expect(preview == live)
        #expect(preview.documents.map(\.id) == [new.id, transcript.id])
        #expect(preview.lessons.isEmpty && preview.questionBank.isEmpty)
        #expect(plan.previousSessionID == previous)
        #expect(series == original)
    }

    @Test func customMeetingUsesItsOwnBaseAndKeepsExplicitAnswerRequirements() {
        var series = Workspace(name: "实验室", mode: .meeting)
        series.preferences = "只引用已验证的实验数据"
        let result = MeetingPlan(mode: .custom, customTypeName: "导师一对一", customBaseMode: .labMeeting)
            .preparedWorkspace(from: series)
        #expect(result.mode == .custom && result.effectiveMode == .labMeeting)
        #expect(result.customTypeName == "导师一对一")
        #expect(result.preferences == series.preferences)
        #expect(series.mode == .meeting && series.customTypeName == nil)
    }

    @Test func transcriptIsOnlyIncludedWhenExplicitlySelected() {
        let series = Workspace(name: "组会", mode: .labMeeting)
        let transcript = ContextDocument(name: "上一场", pages: ["private prior discussion"])
        #expect(MeetingPlan(mode: .labMeeting).preparedWorkspace(from: series, previousTranscript: transcript).documents.isEmpty)
    }
}
