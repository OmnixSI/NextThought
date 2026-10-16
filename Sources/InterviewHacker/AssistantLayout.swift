import Foundation
import SwiftUI

/// The assistant page's building blocks; each can be shown, hidden, resized (drag the
/// gaps), moved between the two columns or popped out into its own window.
enum AssistantModule: String, CaseIterable, Codable, Identifiable {
    case conversation, question, answer, history, insights
    var id: String { rawValue }
    var title: String {
        switch self {
        case .conversation: "实时对话"
        case .question: "当前问题与候选"
        case .answer: "建议回答"
        case .history: "回答记录"
        case .insights: "屏幕与讲稿"
        }
    }
    var symbol: String {
        switch self {
        case .conversation: "text.bubble"
        case .question: "questionmark.bubble"
        case .answer: "sparkles"
        case .history: "clock.arrow.circlepath"
        case .insights: "rectangle.on.rectangle"
        }
    }
    /// Sizes to its content (a strip) instead of sharing the column height.
    var isCompact: Bool { self == .question }
}

/// Docked modules in two columns (order = array order), detached ones, and the
/// column split. Saved as JSON in UserDefaults (“assistantLayout”) and in backups.
struct AssistantLayout: Codable, Equatable {
    struct Slot: Codable, Equatable {
        var module: AssistantModule
        var column: Int
        var weight: Double
    }

    var slots: [Slot]
    var detached: [AssistantModule] = []
    /// Left column share of the width (two-column layout).
    var split: Double = 0.44

    static let standard = AssistantLayout(slots: [
        Slot(module: .conversation, column: 0, weight: 1), Slot(module: .history, column: 0, weight: 0.45),
        Slot(module: .question, column: 1, weight: 1), Slot(module: .answer, column: 1, weight: 1),
    ])

    enum Preset: String, CaseIterable, Identifiable {
        case standard, answerFirst, presenting, stacked, answerOnly
        var id: String { rawValue }
        var title: String {
            switch self {
            case .standard: "对话在左 · 回答在右（默认）"
            case .answerFirst: "回答在左 · 对话在右"
            case .presenting: "讲稿在左 · 回答在右（演讲 / 答辩）"
            case .stacked: "单栏上下排列（小屏）"
            case .answerOnly: "只看回答"
            }
        }
        var shortTitle: String {
            switch self {
            case .standard: "对话 | 回答"
            case .answerFirst: "回答 | 对话"
            case .presenting: "讲稿 | 回答"
            case .stacked: "单栏"
            case .answerOnly: "只看回答"
            }
        }
        var layout: AssistantLayout {
            switch self {
            case .standard: .standard
            case .answerFirst: AssistantLayout(slots: [
                Slot(module: .question, column: 0, weight: 1), Slot(module: .answer, column: 0, weight: 1),
                Slot(module: .conversation, column: 1, weight: 1), Slot(module: .history, column: 1, weight: 0.5)], split: 0.58)
            case .presenting: AssistantLayout(slots: [
                Slot(module: .insights, column: 0, weight: 1.4), Slot(module: .conversation, column: 0, weight: 0.6),
                Slot(module: .question, column: 1, weight: 1), Slot(module: .answer, column: 1, weight: 1)], split: 0.5)
            case .stacked: AssistantLayout(slots: [
                Slot(module: .question, column: 0, weight: 1), Slot(module: .answer, column: 0, weight: 1.3),
                Slot(module: .conversation, column: 0, weight: 1), Slot(module: .history, column: 0, weight: 0.45)])
            case .answerOnly: AssistantLayout(slots: [Slot(module: .question, column: 0, weight: 1), Slot(module: .answer, column: 0, weight: 1)])
            }
        }
    }

    func slots(in column: Int) -> [Slot] { slots.filter { $0.column == column } }
    func isDocked(_ module: AssistantModule) -> Bool { slots.contains { $0.module == module } }
    func isDetached(_ module: AssistantModule) -> Bool { detached.contains(module) }
    func isHidden(_ module: AssistantModule) -> Bool { !isDocked(module) && !isDetached(module) }
    var usesTwoColumns: Bool { !slots(in: 0).isEmpty && !slots(in: 1).isEmpty }

    /// Moves (or docks) `module` into `column` before the module at `index` in that column.
    mutating func place(_ module: AssistantModule, column: Int, index: Int) {
        let weight = slots.first { $0.module == module }?.weight ?? 1
        slots.removeAll { $0.module == module }
        detached.removeAll { $0 == module }
        let column = min(max(column, 0), 1)
        let inColumn = slots.enumerated().filter { $0.element.column == column }
        let position = index < inColumn.count ? inColumn[index].offset : (inColumn.last.map { $0.offset + 1 } ?? slots.count)
        slots.insert(Slot(module: module, column: column, weight: weight), at: position)
    }

    mutating func hide(_ module: AssistantModule) {
        slots.removeAll { $0.module == module }
        detached.removeAll { $0 == module }
    }

    /// Shows a hidden module at the end of the column that usually holds it.
    mutating func show(_ module: AssistantModule) {
        guard isHidden(module) else { return }
        let column = module == .conversation || module == .history ? 0 : (slots(in: 1).isEmpty && !slots(in: 0).isEmpty ? 0 : 1)
        place(module, column: column, index: .max)
    }

    mutating func detach(_ module: AssistantModule) {
        slots.removeAll { $0.module == module }
        if !detached.contains(module) { detached.append(module) }
    }

    mutating func move(_ module: AssistantModule, by delta: Int) {
        guard let slot = slots.first(where: { $0.module == module }) else { return }
        let column = slots(in: slot.column)
        guard let index = column.firstIndex(where: { $0.module == module }) else { return }
        let target = index + delta
        guard target >= 0, target < column.count else { return }
        // Indices below count the column without the module itself.
        place(module, column: slot.column, index: target)
    }

    mutating func setWeight(_ module: AssistantModule, _ weight: Double) {
        guard let index = slots.firstIndex(where: { $0.module == module }) else { return }
        slots[index].weight = min(max(weight, 0.15), 6)
    }

    static func load() -> AssistantLayout {
        guard let data = UserDefaults.standard.data(forKey: "assistantLayout"),
              var layout = try? JSONDecoder().decode(AssistantLayout.self, from: data) else { return .standard }
        // A module listed twice (hand-edited or older file) keeps its first place.
        var seen = Set<AssistantModule>()
        layout.slots = layout.slots.filter { seen.insert($0.module).inserted }
        layout.detached = layout.detached.filter { seen.insert($0).inserted }
        layout.split = min(max(layout.split, 0.25), 0.75)
        return layout
    }

    func save() {
        if let data = try? JSONEncoder().encode(self) { UserDefaults.standard.set(data, forKey: "assistantLayout") }
    }
}

@MainActor
extension AppState {
    /// Changes the layout and saves it.
    func updateLayout(_ change: (inout AssistantLayout) -> Void) {
        var next = assistantLayout
        change(&next)
        guard next != assistantLayout else { return }
        let wasDetached = Set(assistantLayout.detached)
        assistantLayout = next
        next.save()
        let nowDetached = Set(next.detached)
        // Windows follow the layout: open the newly detached, close the docked ones.
        for module in nowDetached.subtracting(wasDetached) { DetachedModuleController.shared.open(module) }
        for module in wasDetached.subtracting(nowDetached) { DetachedModuleController.shared.close(module) }
    }

    func detachModule(_ module: AssistantModule) {
        updateLayout { $0.detach(module) }
        Diagnostics.record("layout.detached", ["module": module.rawValue])
    }

    func dockModule(_ module: AssistantModule, column: Int? = nil, index: Int = .max) {
        updateLayout { layout in
            let column = column ?? (module == .conversation || module == .history ? 0 : (layout.slots(in: 1).isEmpty && !layout.slots(in: 0).isEmpty ? 0 : 1))
            layout.place(module, column: column, index: index)
        }
        Diagnostics.record("layout.docked", ["module": module.rawValue])
    }
}
