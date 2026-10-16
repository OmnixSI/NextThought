import AppKit
import Foundation
import InterviewHackerCore
import Observation
import UniformTypeIdentifiers

/// Imported skills (SKILL.md files), saved in `Application Support/InterviewHacker/Skills`.
/// Global skills apply to every scenario; others are attached per scenario.
@MainActor
@Observable
final class SkillLibrary {
    static let shared = SkillLibrary()

    private(set) var skills: [Skill] = []

    private init() { load() }

    func load() {
        guard !DemoMode.isEnabled,
              let data = try? Data(contentsOf: AppPaths.skills),
              let saved = try? JSONLines.decoder().decode([Skill].self, from: data) else { return }
        skills = saved
    }

    private func save() {
        guard !DemoMode.isEnabled else { return }
        let encoder = JSONLines.encoder()
        encoder.outputFormatting.insert(.prettyPrinted)
        guard let data = try? encoder.encode(skills) else { return }
        try? FileManager.default.createDirectory(at: AppPaths.skills.deletingLastPathComponent(), withIntermediateDirectories: true)
        try? data.write(to: AppPaths.skills, options: .atomic)
    }

    func skill(_ id: UUID) -> Skill? { skills.first { $0.id == id } }

    /// Open panel for .md / .txt files; each becomes one skill.
    @discardableResult
    func importFiles(global: Bool) -> [Skill] {
        let panel = NSOpenPanel()
        panel.allowsMultipleSelection = true
        panel.canChooseDirectories = false
        panel.allowedContentTypes = [UTType(filenameExtension: "md") ?? .plainText, .plainText]
        panel.message = "选择技能文件（SKILL.md 或任意 Markdown）"
        guard panel.runModal() == .OK else { return [] }
        var added: [Skill] = []
        for url in panel.urls {
            guard let text = try? String(contentsOf: url, encoding: .utf8) else { continue }
            var skill = Skill.parse(text, fileName: url.lastPathComponent)
            // A folder skill (…/my-skill/SKILL.md) is better named after its folder.
            if url.lastPathComponent.uppercased() == "SKILL.MD", skill.name == "未命名技能" { skill.name = url.deletingLastPathComponent().lastPathComponent }
            skill.global = global
            added.append(skill)
        }
        skills.append(contentsOf: added)
        save()
        Diagnostics.record("skills.imported", ["count": added.count])
        return added
    }

    @discardableResult
    func create(global: Bool) -> Skill {
        var skill = Skill.parse(Skill.starterTemplate, fileName: "新技能.md")
        skill.global = global
        skills.append(skill)
        save()
        return skill
    }

    func update(_ skill: Skill) {
        guard let index = skills.firstIndex(where: { $0.id == skill.id }), skills[index] != skill else { return }
        skills[index] = skill
        save()
    }

    func delete(_ id: UUID) {
        skills.removeAll { $0.id == id }
        save()
    }

    /// Backup import: adds skills that are not here yet; local edits are kept.
    func merge(_ incoming: [Skill]) -> Int {
        let fresh = incoming.filter { skill in !skills.contains { $0.id == skill.id } }
        skills.append(contentsOf: fresh)
        if !fresh.isEmpty { save() }
        return fresh.count
    }

    func installDemo(_ items: [Skill]) { skills = items }
}

@MainActor
extension AppState {
    var skillLibrary: SkillLibrary { SkillLibrary.shared }

    /// Skills for the current scenario, as prompt snippets.
    var activeSkillSnippets: [SkillSnippet] {
        Skill.active(SkillLibrary.shared.skills, attached: activeWorkspace.skillIDs).map { SkillSnippet(name: $0.name, content: $0.content) }
    }

    func setSkill(_ id: UUID, attached: Bool) {
        var workspace = activeWorkspace
        if attached { if !workspace.skillIDs.contains(id) { workspace.skillIDs.append(id) } } else { workspace.skillIDs.removeAll { $0 == id } }
        activeWorkspace = workspace
    }
}
