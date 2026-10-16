import Foundation
import Testing
@testable import InterviewHackerCore

struct SkillTests {
    @Test func readsFrontMatter() {
        let text = "---\nname: 面试口语化\ndescription: \"回答像真人说话\"\n---\n\n# 标题\n先说结论。\n"
        let skill = Skill.parse(text, fileName: "SKILL.md")
        #expect(skill.name == "面试口语化")
        #expect(skill.summary == "回答像真人说话")
        #expect(skill.content == "# 标题\n先说结论。")
    }

    @Test func fallsBackToHeadingThenFileName() {
        #expect(Skill.parse("# 学术答辩\n承认局限。", fileName: "x.md").name == "学术答辩")
        #expect(Skill.parse("只有正文", fileName: "商务谈判.md").name == "商务谈判")
        #expect(Skill.parse("只有正文", fileName: "SKILL.md").name == "未命名技能")
    }

    @Test func activeSkillsAreGlobalOrAttached() {
        let global = Skill(name: "g", content: "a", global: true)
        let attached = Skill(name: "a", content: "b")
        let other = Skill(name: "o", content: "c")
        let disabled = Skill(name: "d", content: "d", global: true, enabled: false)
        let active = Skill.active([global, attached, other, disabled], attached: [attached.id])
        #expect(active.map(\.name) == ["g", "a"])
    }

    @Test func workspaceKeepsSkillsAndOldFilesStillLoad() throws {
        var workspace = Workspace(name: "w", mode: .interview)
        let id = UUID()
        workspace.skillIDs = [id]
        let data = try JSONEncoder().encode(workspace)
        #expect(try JSONDecoder().decode(Workspace.self, from: data).skillIDs == [id])
        let old = #"{"id":"\#(UUID().uuidString)","name":"旧场景","mode":"interview"}"#
        #expect(try JSONDecoder().decode(Workspace.self, from: Data(old.utf8)).skillIDs.isEmpty)
    }
}
