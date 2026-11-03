import AppKit
import SwiftUI
import InterviewHackerCore

/// Nine settings groups, arranged beneath five simple top-level categories.
enum SettingsGroup: String, CaseIterable, Identifiable {
    case essentials = "基础", recognition = "识别", answers = "回答", notes = "纪要", documents = "资料", overlay = "提词器", skills = "技能", recording = "数据", more = "更多"
    var id: String { rawValue }
    var displayTitle: String { self == .overlay ? "回答浮窗" : rawValue }
    var symbol: String {
        switch self {
        case .essentials: "checklist"
        case .recognition: "waveform"
        case .documents: "doc.text.magnifyingglass"
        case .answers: "sparkles"
        case .notes: "doc.text"
        case .skills: "wand.and.stars"
        case .overlay: "rectangle.topthird.inset.filled"
        case .recording: "tray.full"
        case .more: "ellipsis.circle"
        }
    }

    /// Accepts names stored by older versions (常规 / 更新 / 快捷键).
    init(stored: String) {
        switch stored {
        case "常规": self = .essentials
        case "更新", "快捷键": self = .more
        default: self = SettingsGroup(rawValue: stored) ?? .essentials
        }
    }
}

enum SettingsCategory: String, CaseIterable, Identifiable {
    case general = "常规"
    case meeting = "会议与识别"
    case answers = "回答与提词"
    case notesAndMaterials = "纪要与资料"
    case data = "数据与更多"

    var id: String { rawValue }
    var displayTitle: String { self == .answers ? "回答与浮窗" : rawValue }

    var groups: [SettingsGroup] {
        switch self {
        case .general: [.essentials]
        case .meeting: [.recognition]
        case .answers: [.answers, .overlay]
        case .notesAndMaterials: [.notes, .documents]
        case .data: [.recording, .skills, .more]
        }
    }

    var summary: String {
        switch self {
        case .general: "界面、语言与快速准备"
        case .meeting: "选择声音来源与实时识别方式"
        case .answers: "回答服务与会中回答浮窗"
        case .notesAndMaterials: "会后纪要与资料读取"
        case .data: "本机记录、技能、更新与维护"
        }
    }

    var symbol: String {
        switch self {
        case .general: "slider.horizontal.3"
        case .meeting: "waveform"
        case .answers: "sparkles"
        case .notesAndMaterials: "doc.text"
        case .data: "tray.full"
        }
    }
}

extension SettingsGroup {
    var category: SettingsCategory {
        switch self {
        case .essentials: .general
        case .recognition: .meeting
        case .answers, .overlay: .answers
        case .notes, .documents: .notesAndMaterials
        case .recording, .skills, .more: .data
        }
    }
}

/// A compact, category-first settings page. Existing settings remain grouped
/// below each category, without a second vertical navigation rail.
struct SettingsView: View {
    @AppStorage("settingsGroup", store: UserDefaults.standard) private var groupRaw = SettingsGroup.essentials.rawValue
    @State private var query = ""

    private var selectedGroup: SettingsGroup { SettingsGroup(stored: groupRaw) }
    private var selectedCategory: SettingsCategory { selectedGroup.category }

    var body: some View {
        GeometryReader { proxy in
            let effectiveWidth = min(max(0, proxy.size.width - 32), 980)
            let columnCount = effectiveWidth >= 780 ? 2 : 1
            VStack(alignment: .leading, spacing: 12) {
                SettingsPageHeader(query: $query)
                SettingsCategoryBar(selection: selectedCategory, select: selectCategory)

                if query.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty {
                    if selectedCategory.groups.count > 1 {
                        SettingsGroupBar(groups: selectedCategory.groups, selection: selectedGroup, select: selectGroup)
                    }
                } else {
                    SettingsSearchResults(query: query, select: selectGroup)
                }

                ScrollView {
                    VStack(alignment: .leading, spacing: 14) {
                        SettingsPaneHeading(category: selectedCategory, group: selectedGroup)
                        SettingsGroupContent(group: selectedGroup)
                    }
                    .frame(maxWidth: .infinity, alignment: .leading)
                    .padding(.bottom, 18)
                }
                .id(selectedGroup)
            }
            .frame(maxWidth: 980, alignment: .leading)
            .frame(maxWidth: .infinity, alignment: .center)
            .environment(\.settingsColumnCount, columnCount)
            .padding(.horizontal, 16).padding(.bottom, 14).padding(.top, 4)
        }
    }

    private func selectCategory(_ category: SettingsCategory) {
        guard let first = category.groups.first else { return }
        groupRaw = first.rawValue
        query = ""
    }

    private func selectGroup(_ group: SettingsGroup) {
        groupRaw = group.rawValue
        query = ""
    }
}

private struct SettingsPaneHeading: View {
    let category: SettingsCategory
    let group: SettingsGroup

    var body: some View {
        HStack(alignment: .firstTextBaseline, spacing: 12) {
            Text(LocalizedStringKey(category.displayTitle)).font(.system(size: 16, weight: .semibold))
            Spacer(minLength: 8)
            Text(LocalizedStringKey(category.summary)).font(.system(size: 10.5)).foregroundStyle(Ink.tertiary)
            if group == .answers {
                Note("联网搜索选项可在高级设置中查看和调整。", presentation: .info)
                    .accessibilityLabel("联网搜索设置说明")
            }
        }
    }
}

private struct SettingsCategoryBar: View {
    let selection: SettingsCategory
    let select: (SettingsCategory) -> Void

    var body: some View {
        ScrollView(.horizontal, showsIndicators: false) {
            HStack(spacing: 4) {
                ForEach(SettingsCategory.allCases) { category in
                    let isSelected = category == selection
                    Button { select(category) } label: {
                        HStack(spacing: 7) {
                            Image(systemName: category.symbol).font(.system(size: 12))
                            Text(LocalizedStringKey(category.displayTitle)).font(.system(size: 11.5, weight: isSelected ? .semibold : .medium))
                        }
                        .foregroundStyle(isSelected ? Ink.ink : Ink.secondary)
                        .padding(.horizontal, 12).padding(.top, 8).padding(.bottom, 10)
                        .overlay(alignment: .bottom) {
                            Capsule().fill(isSelected ? Ink.ink : Color.clear).frame(height: 2)
                        }
                    }
                    .buttonStyle(.plain)
                    .help(LocalizedStringKey(category.summary))
                    .accessibilityAddTraits(isSelected ? .isSelected : [])
                }
            }
            .animation(.easeInOut(duration: 0.18), value: selection)
        }
        .overlay(alignment: .bottom) { Rectangle().fill(Ink.hairline).frame(height: 1) }
    }
}

private struct SettingsGroupBar: View {
    let groups: [SettingsGroup]
    let selection: SettingsGroup
    let select: (SettingsGroup) -> Void

    var body: some View {
        HStack(spacing: 6) {
            ForEach(groups) { group in
                let isSelected = group == selection
                Button { select(group) } label: {
                    HStack(spacing: 6) {
                        Image(systemName: group.symbol).font(.system(size: 11))
                        Text(LocalizedStringKey(group.displayTitle)).font(.system(size: 11.5, weight: isSelected ? .semibold : .medium))
                    }
                    .foregroundStyle(isSelected ? Ink.ink : Ink.secondary)
                    .padding(.horizontal, 10).padding(.vertical, 6)
                    .background(isSelected ? Ink.hover : Color.clear, in: Capsule())
                }
                .buttonStyle(.plain)
                    .help(LocalizedStringKey(group.summary))
                .accessibilityAddTraits(isSelected ? .isSelected : [])
            }
            Spacer(minLength: 0)
        }
        .animation(.easeInOut(duration: 0.18), value: selection)
    }
}

private struct SettingsSearchResults: View {
    let query: String
    let select: (SettingsGroup) -> Void

    private var matches: [SettingsGroup] {
        let term = query.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !term.isEmpty else { return [] }
        return SettingsGroup.allCases.filter { group in
            let translatedName = Bundle.main.localizedString(forKey: group.displayTitle, value: group.displayTitle, table: nil)
            let translatedSummary = Bundle.main.localizedString(forKey: group.summary, value: group.summary, table: nil)
            return [group.rawValue, group.displayTitle, group.summary, translatedName, translatedSummary, group.englishSearchTerms]
                .contains { $0.localizedCaseInsensitiveContains(term) }
        }
    }

    var body: some View {
        ScrollView(.horizontal, showsIndicators: false) {
            HStack(spacing: 7) {
                if matches.isEmpty {
                    Text("没有匹配的设置分组").font(.system(size: 11.5)).foregroundStyle(.secondary)
                } else {
                    ForEach(matches) { group in
                        Button { select(group) } label: {
                            Label(LocalizedStringKey(group.displayTitle), systemImage: group.symbol)
                        }
                        .buttonStyle(ChipButtonStyle())
                        .help(LocalizedStringKey(group.summary))
                    }
                }
            }
        }
    }
}

private struct SettingsGroupContent: View {
    @Environment(AppState.self) private var state
    let group: SettingsGroup

    @ViewBuilder var body: some View {
        switch group {
        case .essentials: EssentialSettings()
        case .recognition: RecognitionSettings()
        case .documents: DocumentModelSettings()
        case .answers: AnswerSettings()
        case .notes: NotesSettingsView()
        case .skills:
            if state.simplifiedInterface {
                SettingsSection(title: "技能", icon: "wand.and.stars") {
                    Note("技能配置属于高级设置。已有技能仍会生效；打开右上角开关后可以管理。")
                }
            } else { SkillSettings() }
        case .overlay: OverlaySettings()
        case .recording: RecordingSettings()
        case .more: MoreSettings()
        }
    }
}

extension SettingsGroup {
    fileprivate var englishSearchTerms: String {
        switch self {
        case .essentials: "general appearance language setup"
        case .recognition: "meeting audio microphone speech recognition"
        case .documents: "documents materials OCR retrieval"
        case .answers: "answers model engine search"
        case .notes: "notes summary template language"
        case .skills: "skills instructions"
        case .overlay: "teleprompter overlay font display"
        case .recording: "data recording backup restore export import"
        case .more: "updates permissions shortcuts companion"
        }
    }

    var summary: String {
        switch self {
        case .essentials: "核心准备 · 可选资料增强"
        case .recognition: "会议声音 · 麦克风 · 语音识别"
        case .documents: "读取资料 · 免下载或按需增强"
        case .answers: "回答服务 · 回答模型 · 联网"
        case .notes: String(localized: "纪要模型 · 模板 · 语言")
        case .skills: "加载 SKILL.md · 全局或按系列"
        case .overlay: "位置 · 字号 · 共享显示"
        case .recording: "本机会话记录 · 备份与恢复"
        case .more: "更新 · 权限 · 快捷键 · 手机伴侣"
        }
    }
}

private struct SettingsPageHeader: View {
    @Environment(AppState.self) private var state
    @Binding var query: String

    var body: some View {
        HStack(spacing: 14) {
            VStack(alignment: .leading, spacing: 2) {
                Text("设置").font(Typo.section)
                Text("常用选项放在前面；高级选项仍可随时展开。")
                    .font(.system(size: 11.5)).foregroundStyle(.secondary)
            }
            Spacer(minLength: 8)
            searchField.frame(maxWidth: 220)
            Toggle("高级设置", isOn: Binding(get: { !state.simplifiedInterface }, set: {
                state.simplifiedInterface = !$0
                state.saveAnswerSettings()
            }))
            .toggleStyle(.switch).controlSize(.small).fixedSize()
            .help("展开模型参数、联网与技能、备份细项、快捷键和维护工具。关闭只隐藏选项，不修改已有配置。")
        }
        .padding(.horizontal, 4).padding(.top, 2).padding(.bottom, 2)
    }

    private var searchField: some View {
        HStack(spacing: 7) {
            Image(systemName: "magnifyingglass").font(.system(size: 11)).foregroundStyle(.secondary)
            TextField("查找设置，例如：麦克风", text: $query)
                .textFieldStyle(.plain).font(.system(size: 11.5))
            if !query.isEmpty {
                Button { query = "" } label: {
                    Image(systemName: "xmark.circle.fill").font(.system(size: 11))
                }
                .buttonStyle(.borderless).foregroundStyle(.secondary)
            }
        }
        .padding(.horizontal, 9).padding(.vertical, 7)
        .background(Ink.sunken, in: RoundedRectangle(cornerRadius: 8, style: .continuous))
        .overlay(RoundedRectangle(cornerRadius: 8, style: .continuous).stroke(Ink.hairline, lineWidth: 1))
    }
}

private struct SettingsColumnCountKey: EnvironmentKey {
    static let defaultValue = 2
}

private struct SettingsRowAlignmentKey: EnvironmentKey {
    static let defaultValue = false
}

extension EnvironmentValues {
    var settingsColumnCount: Int {
        get { self[SettingsColumnCountKey.self] }
        set { self[SettingsColumnCountKey.self] = max(1, newValue) }
    }

    fileprivate var settingsRowAligned: Bool {
        get { self[SettingsRowAlignmentKey.self] }
        set { self[SettingsRowAlignmentKey.self] = newValue }
    }
}

/// Measures paired cards at the same column width and gives them a shared
/// row height so both card surfaces finish on the same baseline.
private struct BalancedSettingsRow: Layout {
    var spacing: CGFloat = 12
    var firstColumnFraction: CGFloat = 0.5

    func sizeThatFits(proposal: ProposedViewSize, subviews: Subviews, cache: inout ()) -> CGSize {
        guard !subviews.isEmpty else { return .zero }
        let width = proposal.width ?? subviews.reduce(0) { $0 + $1.sizeThatFits(.unspecified).width } + spacing
        let availableWidth = max(0, width - spacing)
        let firstWidth = availableWidth * min(max(firstColumnFraction, 0), 1)
        let widths = subviews.count == 1 ? [width] : [firstWidth, availableWidth - firstWidth]
        let rowHeight = subviews.enumerated().map { index, subview in
            subview.sizeThatFits(ProposedViewSize(width: widths[min(index, widths.count - 1)], height: nil)).height
        }.max() ?? 0
        return CGSize(width: width, height: rowHeight)
    }

    func placeSubviews(in bounds: CGRect, proposal: ProposedViewSize, subviews: Subviews, cache: inout ()) {
        guard !subviews.isEmpty else { return }
        let availableWidth = max(0, bounds.width - spacing)
        let firstWidth = availableWidth * min(max(firstColumnFraction, 0), 1)
        let widths = subviews.count == 1 ? [bounds.width] : [firstWidth, availableWidth - firstWidth]
        let rowHeight = subviews.enumerated().map { index, subview in
            subview.sizeThatFits(ProposedViewSize(width: widths[min(index, widths.count - 1)], height: nil)).height
        }.max() ?? bounds.height
        var x = bounds.minX
        for (index, subview) in subviews.enumerated() {
            let columnWidth = widths[min(index, widths.count - 1)]
            subview.place(
                at: CGPoint(x: x, y: bounds.minY),
                anchor: .topLeading,
                proposal: ProposedViewSize(width: columnWidth, height: rowHeight)
            )
            x += columnWidth + spacing
        }
    }
}

/// Explicit rows keep wide cards full width and paired card bottoms aligned.
struct SettingsCardRow<Left: View, Right: View>: View {
    @Environment(\.settingsColumnCount) private var columnCount
    private let leftFraction: CGFloat
    private let left: Left
    private let right: Right

    init(leftFraction: CGFloat = 0.5, @ViewBuilder left: () -> Left, @ViewBuilder right: () -> Right) {
        self.leftFraction = leftFraction
        self.left = left()
        self.right = right()
    }

    var body: some View {
        Group {
            if columnCount > 1 {
                BalancedSettingsRow(spacing: 12, firstColumnFraction: leftFraction) {
                    left.environment(\.settingsRowAligned, true)
                        .frame(maxWidth: .infinity, alignment: .topLeading)
                    right.environment(\.settingsRowAligned, true)
                        .frame(maxWidth: .infinity, alignment: .topLeading)
                }
            } else {
                VStack(alignment: .leading, spacing: 12) {
                    left.frame(maxWidth: .infinity, alignment: .topLeading)
                    right.frame(maxWidth: .infinity, alignment: .topLeading)
                }
            }
        }
        .frame(maxWidth: .infinity, alignment: .topLeading)
    }
}

/// Card with an icon title, shared by every settings group.
struct SettingsSection<Content: View>: View {
    let title: String
    let icon: String
    @Environment(\.settingsRowAligned) private var rowAligned
    @ViewBuilder var content: Content

    var body: some View {
        VStack(alignment: .leading, spacing: 12) {
            HStack(spacing: 10) {
                Image(systemName: icon)
                    .font(.system(size: 13, weight: .regular)).foregroundStyle(Ink.secondary)
                    .frame(width: 18)
                Text(LocalizedStringKey(title)).font(.system(size: 14, weight: .semibold))
            }
            content
        }
        .font(.system(size: 13))
        .frame(maxHeight: rowAligned ? .infinity : nil, alignment: .topLeading)
        .card()
    }
}

/// The single global switch hides expert controls without resetting their values.
struct AdvancedOptions<Content: View>: View {
    @Environment(AppState.self) private var state
    private let content: Content

    init(_ id: String, @ViewBuilder content: () -> Content) {
        self.content = content()
    }

    var body: some View {
        if !state.simplifiedInterface { content }
    }
}

enum NotePresentation: Equatable {
    case info
    case inline
}

struct Note: View {
    var text: String
    var color: Color = .secondary
    var presentation: NotePresentation
    @State private var showingInfo = false
    @State private var isHovering = false
    @FocusState private var infoFocused: Bool

    init(_ text: String, color: Color? = nil, presentation: NotePresentation? = nil) {
        self.text = text
        self.color = color ?? .secondary
        self.presentation = presentation ?? .inline
    }

    var body: some View {
        Group {
            if presentation == .inline {
                Text(LocalizedStringKey(text)).font(.system(size: 11.5)).foregroundStyle(color).fixedSize(horizontal: false, vertical: true)
            } else {
                Button { showingInfo.toggle() } label: {
                    Image(systemName: "info.circle")
                        .font(.system(size: 12))
                        .foregroundStyle(color)
                        .frame(width: 28, height: 28)
                        .background(isHovering || infoFocused ? Ink.hover : Color.clear, in: Circle())
                        .overlay(Circle().strokeBorder(infoFocused ? Ink.focus : Color.clear, lineWidth: 1.5))
                        .contentShape(Rectangle())
                }
                .buttonStyle(.plain)
                .focused($infoFocused)
                .onHover { isHovering = $0 }
                .help(LocalizedStringKey(text))
                .accessibilityLabel("查看说明")
                .accessibilityHint(Text(LocalizedStringKey(text)))
                .popover(isPresented: $showingInfo, arrowEdge: .trailing) {
                    Text(LocalizedStringKey(text))
                        .font(.system(size: 12))
                        .foregroundStyle(.primary)
                        .frame(width: 280, alignment: .leading)
                        .fixedSize(horizontal: false, vertical: true)
                        .padding(12)
                }
            }
        }
    }
}

// MARK: - Essentials

/// Sparse preferences share a compact row instead of occupying two half-width cards.
private struct GeneralPreferenceControls: View {
    let state: AppState

    var body: some View {
        ViewThatFits(in: .horizontal) {
            HStack(spacing: 20) {
                onboardingControl
                Rectangle()
                    .fill(Ink.hairline)
                    .frame(width: 1, height: 26)
                    .accessibilityHidden(true)
                LanguageSettingsSection(compact: true)
            }

            VStack(alignment: .leading, spacing: 12) {
                onboardingControl
                LanguageSettingsSection(compact: true)
            }
        }
        .padding(.horizontal, 4)
        .padding(.vertical, 4)
    }

    private var onboardingControl: some View {
        HStack(spacing: 8) {
            Image(systemName: "sparkles")
                .font(.system(size: 13, weight: .regular))
                .foregroundStyle(Ink.secondary)
                .frame(width: 18)
            Toggle("开启新手引导", isOn: Binding(
                get: { state.onboardingGuideEnabled },
                set: { state.setOnboardingGuideEnabled($0) }
            ))
            .toggleStyle(.switch)
            .controlSize(.small)
            .fixedSize()
            .accessibilityLabel("开启新手引导")
            .accessibilityHint("开启后可以进入或继续新手引导；关闭会暂停引导并保留进度")
        }
    }
}

private struct EssentialSettings: View {
    @Environment(AppState.self) private var state
    var body: some View {
        VStack(alignment: .leading, spacing: 12) {
            SettingsCardRow(leftFraction: 0.42) {
                AppearanceSettingsSection()
            } right: {
                SettingsSection(title: "这台 Mac 现在能做什么", icon: "checklist") {
                    capability("实时语音（必需）", state.liveSetupReadiness.voiceReady ? "识别与音频权限已就绪；在会中页面选择会议窗口" : "需要语音识别与音频权限", ready: state.liveSetupReadiness.voiceReady, action: "设置语音") { state.openSettings(.recognition) }
                    Divider()
                    capability("回答服务（必需）", state.answerEngineReady ? "回答服务已配置，可以测试或开始使用" : "需要连接回答服务，才能在对话中生成建议回答", ready: state.answerEngineReady, action: "回答设置") { state.openSettings(.answers) }
                    Divider()
                    capability("资料增强（可选）", "系统识别与关键词直接可用；OCR 模型和向量模型按需安装", ready: true, action: "查看取舍") { state.openSettings(.documents) }
                }
            }

            HStack {
                Button("备份与恢复") { state.openSettings(.recording) }.buttonStyle(.plain)
                Text("·").foregroundStyle(.tertiary)
                Button("版本与更新") { state.openSettings(.more) }.buttonStyle(.plain)
            }
            .font(.system(size: 12)).foregroundStyle(.secondary).padding(.horizontal, 4)

            AdvancedOptions("essentials-tools") { AdvancedEssentialSettings() }
        }
    }
    private func capability(_ title: String, _ detail: String, ready: Bool, action: String, perform: @escaping () -> Void) -> some View {
        HStack(alignment: .top, spacing: 12) {
            Image(systemName: ready ? "checkmark.circle.fill" : "circle.dashed")
                .foregroundStyle(ready ? .green : .secondary)
            VStack(alignment: .leading, spacing: 4) {
                Text(LocalizedStringKey(title)).fontWeight(.semibold)
                Text(LocalizedStringKey(detail)).font(.system(size: 12)).foregroundStyle(.secondary).fixedSize(horizontal: false, vertical: true)
            }
            Spacer(minLength: 8)
            Button(LocalizedStringKey(action), action: perform).buttonStyle(SecondaryButtonStyle())
        }.padding(.vertical, 4)
    }
}

private struct AdvancedEssentialSettings: View {
    @Environment(AppState.self) private var state

    var body: some View {
        @Bindable var state = state
        SettingsSection(title: "环境与模型", icon: "wrench.and.screwdriver") {
            HStack(spacing: 8) {
                Button { Task { await state.runQuickEnvironmentSetup() } } label: {
                    Label("一键配置", systemImage: "wand.and.stars")
                }
                .buttonStyle(PrimaryButtonStyle())
                .disabled(state.isRunningEnvironmentSetup)
                .help("检查回答引擎、选择模型、准备语音识别、刷新音频来源并重建资料索引。缺少本地语音模型时会在后台下载，不影响使用")
                Note("一键检查并配好回答模型、语音模型、音频来源和资料索引；哪一步没成功会直接写在这里。", presentation: .info)
                    .accessibilityLabel("一键配置说明")
                Button { Task { await state.rebuildEnvironment() } } label: {
                    Label("一键重建环境", systemImage: "arrow.triangle.2.circlepath")
                }
                .buttonStyle(SecondaryButtonStyle())
                .disabled(state.isRunningEnvironmentSetup)
                .help("重新读取配置、重建资料索引与数据目录，不改变你的选择")
                if state.isRunningEnvironmentSetup {
                    ProgressView().controlSize(.small)
                }
                Spacer(minLength: 0)
            }
            if !state.environmentSetupSteps.isEmpty {
                VStack(alignment: .leading, spacing: 6) {
                    ForEach(state.environmentSetupSteps) { step in
                        HStack(alignment: .top, spacing: 8) {
                            Image(systemName: step.status.symbol)
                                .font(.system(size: 12))
                                .foregroundStyle(stepColor(step.status))
                                .frame(width: 16)
                            VStack(alignment: .leading, spacing: 1) {
                                Text(step.title).font(.system(size: 12.5, weight: .medium))
                                if !step.detail.isEmpty {
                                    Text(step.detail).font(.system(size: 11)).foregroundStyle(.secondary)
                                        .fixedSize(horizontal: false, vertical: true)
                                }
                            }
                            Spacer(minLength: 0)
                        }
                    }
                }
            }
            if !state.environmentSetupSummary.isEmpty {
                Note(state.environmentSetupSummary, color: state.environmentSetupSummary.hasPrefix("有 ") ? .orange : .green)
            }
        }
        SettingsSection(title: "使用前检查", icon: "checklist") {
            SetupChecklist()
            HStack {
                Spacer()
                Button("刷新") { Task { await state.refreshSetupStatus() } }.buttonStyle(ChipButtonStyle())
            }
        }
    }

    /// Status colours of the one-click setup steps: green for finished, orange
    /// for a step that needs attention, brand colour while it runs.
    @MainActor private func stepColor(_ status: EnvironmentSetup.Step.Status) -> Color {
        switch status {
        case .done: .green
        case .failed: .orange
        case .running: Brand.accent
        case .pending, .skipped: .secondary
        }
    }
}

private struct AppearanceSettingsSection: View {
    @Environment(AppState.self) private var state

    var body: some View {
        @Bindable var state = state
        SettingsSection(title: "界面与外观", icon: "paintbrush") {
            GeneralPreferenceControls(state: state)
            Divider()
            MenuPicker(title: "外观", selection: Binding(get: { state.appearance }, set: { state.setAppearance($0) }),
                       options: [("system", String(localized: "跟随系统")),
                                 ("light", String(localized: "浅色")), ("dark", String(localized: "深色"))])
            Toggle("工作台在本应用前台时置顶", isOn: $state.keepOnTop)
                .onChange(of: state.keepOnTop) { _, _ in state.updateSettings() }
            Note("此选项仅在 InterviewHacker 处于前台时提升工作台、设置和分离模块；切换到其他 App 后它们会降回普通层级。回答浮窗保持独立浮窗行为。", presentation: .inline)
        }
    }
}

/// Current code signature and the one-click “bind to this Mac”.
struct SignatureStatus: View {
    private var updater: Updater { Updater.shared }
    @State private var confirming = false

    private var matchesBoundIdentity: Bool {
        guard LocalSigning.isBound,
              let expected = LocalSigning.designatedRequirement,
              let actual = updater.signature?.requirement else { return false }
        return actual == expected
    }

    var body: some View {
        VStack(alignment: .leading, spacing: 6) {
            if let signature = updater.signature {
                Label(signature.summary, systemImage: signature.isAdHoc ? "exclamationmark.shield" : "checkmark.shield")
                    .foregroundStyle(signature.isAdHoc ? Color.orange : Color.green)
            }
            if updater.signature?.isAdHoc != false || !matchesBoundIdentity {
                HStack {
                    Button(LocalSigning.isBound ? "重新绑定本机签名" : "绑定本机签名（以后更新不再重新授权）") { confirming = true }
                        .buttonStyle(SecondaryButtonStyle())
                        .disabled(!updater.canInstall || updater.phase == .installing)
                }
            Note("macOS 把录屏 / 麦克风权限绑定在应用签名上。绑定后应用用本机证书重新签名，之后的自动更新沿用同一签名，权限一直有效。绑定后需要再授权一次。", presentation: .inline)
            }
        }
        .confirmationDialog("绑定本机签名并重新打开？", isPresented: $confirming, titleVisibility: .visible) {
            Button("绑定并重新打开") { updater.bindSignatureAndRelaunch() }
            Button("取消", role: .cancel) { }
        } message: {
            Text("会在本机生成签名证书（保存在 Application Support/InterviewHacker/Signing），重新签名后重启应用。重启后请再允许一次录屏与麦克风，以后更新不再需要。")
        }
    }
}

private struct CompanionSection: View {
    @Environment(AppState.self) private var state
    @State private var appPairing = false
    @State private var showsBackup = false
    @State private var copiedLink: String?

    var body: some View {
        SettingsSection(title: "手机伴侣", icon: "iphone") {
            Toggle("开启手机显示与遥控", isOn: Binding(get: { state.companionEnabled }, set: { state.setCompanion(enabled: $0) }))
            Text(state.companionStatus).font(.system(size: 12)).foregroundStyle(state.companionStatus.hasPrefix("启动失败") ? .orange : .secondary)
            if state.companionEnabled {
                MenuPicker(title: "连接方式", selection: $appPairing,
                           options: [(false, String(localized: "手机网页 · 无需安装")),
                                     (true, String(localized: "已安装手机 App"))])
                if appPairing {
                    if let url = try? CompanionServer.shared.pairing.url(base: URL(string: "interviewhacker-companion://pair")!) {
                        pairingCard(url.absoluteString, title: "用手机 App 扫描", detail: "打开 InterviewHacker 手机 App，选择“扫描二维码”。", copyTitle: "复制 App 配对链接")
                    }
                } else if showsBackup, let address = state.companionBackupAddress {
                    pairingCard(address, title: "互联网备用入口", detail: "用手机相机扫描。此入口使用加密中转；可在手机收藏，供重新打开时使用。", copyTitle: "复制备用入口")
                    Button("返回局域网二维码") { showsBackup = false }.buttonStyle(ChipButtonStyle())
                } else if let address = state.companionAddress {
                    pairingCard(address, title: "用手机相机扫描", detail: "手机与 Mac 连接同一可信网络，在浏览器中打开，无需安装 App。局域网使用明文 HTTP。", copyTitle: "复制网页链接")
                    if state.companionBackupAddress != nil {
                        Button("显示互联网备用二维码") { showsBackup = true }.buttonStyle(ChipButtonStyle())
                    }
                }
                if state.companionBluetoothEnabled {
                    Label("蓝牙备用（仅 App）：" + state.companionBluetoothStatus, systemImage: "antenna.radiowaves.left.and.right")
                        .font(.system(size: 12)).foregroundStyle(.secondary)
                }
                if state.companionRelayEnabled {
                    Label("加密中转：" + state.companionRelayStatus, systemImage: "network")
                        .font(.system(size: 12)).foregroundStyle(state.companionBackupAddress == nil ? .orange : .secondary)
                    if state.companionBackupAddress != nil {
                        Note("备用连接已开启。已配对的手机需要用新的二维码重新配对。", presentation: .inline)
                    }
                }
            }
            DisclosureGroup("备用连接与高级选项") { advancedControls.padding(.top, 8) }
            Note("手机端会显示问题、回答与来源标签，不同步完整转录、资料或截图。配对链接请勿转发；共享手机屏幕时，回答仍可能被看见。", presentation: .inline)
        }
    }
    private func pairingCard(_ address: String, title: String, detail: String, copyTitle: String) -> some View {
        HStack(alignment: .top, spacing: 16) {
            QRCodeView(text: address).frame(width: 168, height: 168).padding(8).background(.white, in: RoundedRectangle(cornerRadius: 12))
                .accessibilityLabel(title + "的配对二维码")
            VStack(alignment: .leading, spacing: 10) {
                Text(title).font(.system(size: 14, weight: .semibold))
                Text(detail).font(.system(size: 12)).foregroundStyle(.secondary).fixedSize(horizontal: false, vertical: true)
                Button(copiedLink == address ? "已复制" : copyTitle) {
                    NSPasteboard.general.clearContents(); NSPasteboard.general.setString(address, forType: .string); copiedLink = address
                }.buttonStyle(ChipButtonStyle())
            }.frame(maxWidth: .infinity, alignment: .leading)
        }.padding(.vertical, 8)
    }
    private var advancedControls: some View {
        @Bindable var state = state
        return VStack(alignment: .leading, spacing: 12) {
            Toggle("启用原生手机 App 的蓝牙备用", isOn: Binding(get: { state.companionBluetoothEnabled }, set: { state.setCompanionBluetooth(enabled: $0) })).disabled(!state.companionEnabled)
            if !state.companionBluetoothEnabled { Text(state.companionBluetoothStatus).font(.system(size: 12)).foregroundStyle(.secondary) }
            TextField("HTTPS 中转服务根地址", text: $state.companionRelayURL).textFieldStyle(.roundedBorder)
            Toggle("启用互联网加密备用连接", isOn: Binding(get: { state.companionRelayEnabled }, set: { state.setCompanionRelay(enabled: $0) })).disabled(!state.companionEnabled)
            if !state.companionRelayEnabled { Text(state.companionRelayStatus).font(.system(size: 12)).foregroundStyle(.secondary) }
            Divider()
            HStack(spacing: 8) {
                Text("首选端口").font(.system(size: 12))
                TextField("8765", value: $state.companionPort, format: .number.grouping(.never)).textFieldStyle(.roundedBorder).frame(width: 80)
                Button("重新生成链接") { state.setCompanion(enabled: true); copiedLink = nil }.buttonStyle(ChipButtonStyle()).disabled(!state.companionEnabled)
            }
            Note("重新生成链接后，手机需要重新配对。", presentation: .inline)
        }
    }
}

// MARK: - Recognition

private struct RecognitionSettings: View {
    @Environment(AppState.self) private var state
    @State private var confirmDelete = false
    @FocusState private var vocabularyFocused: Bool

    var body: some View {
        @Bindable var state = state
        VStack(alignment: .leading, spacing: 12) {
        SettingsCardRow {
        SettingsSection(title: "会议声音与麦克风", icon: "speaker.wave.2") {
            if !state.capturePermissionGranted {
                Note("需要音频权限，才能听到会议里的声音。", color: .orange)
                Button("允许录屏与系统音频") { Task { await state.requestCapturePermission() } }
            } else { Label("会议音频权限已允许", systemImage: "checkmark.circle.fill").foregroundStyle(.green) }
            HStack {
                MenuPicker(title: "会议声音来自", selection: $state.selectedTargetID,
                           options: state.captureTargets.map { ($0.id, $0.title) }, placeholder: "选择会议窗口")
                    .disabled(state.isRunning || state.isChangingSession)
                    .onChange(of: state.selectedTargetID) { _, _ in Task { await state.changeCaptureTarget() } }
                Button("刷新") { Task { await state.refreshTargets() } }
            }
            Toggle("同时识别我说的话（麦克风）", isOn: $state.microphoneEnabled)
                .disabled(state.isRunning || state.isChangingSession)
                .onChange(of: state.microphoneEnabled) { _, _ in state.updateSettings() }
            if state.microphoneEnabled && state.microphoneAccess != .granted {
                Button("允许麦克风") { Task { await state.requestMicrophoneAccess() } }
            }
        }
        } right: {
        SettingsSection(title: "语音识别", icon: "waveform") {
            if !state.simplifiedInterface {
            MenuPicker(title: "引擎", selection: $state.transcriptionBackend, options: [
                ("zipformer", "本地流式 · 离线（推荐）"),
                ("apple", "Apple Speech · 本地 / macOS 26"),
                ("cloud", "云端流式 · 需 API Key"),
            ], localizesOptions: true)
            .disabled(state.isRunning || state.isPreparingModel)
            .onChange(of: state.transcriptionBackend) { _, _ in state.updateSettings(); Task { await state.prewarmRecognizer() } }
            } else {
                Text(state.transcriptionBackend == "zipformer" ? "本机识别 · 会议音频不上传" : state.transcriptionBackend == "apple" ? "系统语音识别" : "云端语音识别 · 会议音频会上传")
                Note("需要更换引擎或调整参数时，打开右上角的高级设置。")
            }
            switch state.transcriptionBackend {
            case "zipformer": ModelDownloadRow()
            case "apple":
                LanguagePicker()
                Note("Apple 路径需明确选择中文或 English，首次使用可能下载语言资源。")
            default:
                LanguagePicker()
                CloudSettings()
            }
        }
        }
        AdvancedOptions("recognition") {
            VStack(alignment: .leading, spacing: 12) {
            SettingsSection(title: "专业词", icon: "textformat") {
                HStack(spacing: 8) {
                    TextField("专业词 / 人名，逗号分隔（提高识别准确度）", text: $state.recognitionVocabulary)
                        .textFieldStyle(.plain).padding(.horizontal, 10).padding(.vertical, 8)
                        .frame(maxWidth: 520, minHeight: 36)
                        .background(Ink.sunken, in: RoundedRectangle(cornerRadius: 8))
                        .overlay(RoundedRectangle(cornerRadius: 8).stroke(vocabularyFocused ? Ink.focus : Ink.hairline, lineWidth: vocabularyFocused ? 1.5 : 1))
                        .focused($vocabularyFocused)
                        .onChange(of: state.recognitionVocabulary) { _, _ in state.updateSettings() }
                    Note("填入人名、项目名和专有名词，识别时会更准；用逗号分隔。", presentation: .info)
                        .accessibilityLabel("专业词说明")
                }
            }
            if state.transcriptionBackend == "zipformer" {
                SettingsSection(title: "本地模型", icon: "cpu") {
                    HStack(spacing: 8) {
                        MenuPicker(title: "流式模型", selection: $state.localStreamingModel, options: LocalStreamingModel.allCases.map { ($0, $0.title) }, localizesOptions: true)
                            .disabled(state.modelControlsDisabled)
                            .onChange(of: state.localStreamingModel) { _, _ in state.updateSettings(); Task { await state.refreshModelStatus(); await state.prewarmRecognizer() } }
                        Note(state.localStreamingModel.detail, presentation: .info).accessibilityLabel("流式模型说明")
                    }
                    Toggle("整句精修：每句说完后用离线模型重新识别整句（推荐）", isOn: $state.secondPassEnabled)
                        .disabled(state.modelControlsDisabled || state.localStreamingModel == .legacy)
                        .onChange(of: state.secondPassEnabled) { _, _ in state.updateSettings(); Task { await state.prewarmRecognizer() } }
                    if let rtf = state.lastRTF, let seconds = state.lastASRSeconds {
                        Note(String(format: "最近处理 %.2f s · RTF %.2f", seconds, rtf) + (state.lastRefineSeconds.map { String(format: " · 精修 %.2f s", $0) } ?? ""), presentation: .inline)
                    }
                    HStack {
                        Link("模型来源：sherpa-onnx X-ASR（Apache-2.0）", destination: URL(string: "https://github.com/Gilgamesh-J/X-ASR")!)
                            .font(.system(size: 11)).foregroundStyle(Color.accentColor)
                        Spacer()
                        Button("删除全部模型") { confirmDelete = true }.buttonStyle(.borderless).foregroundStyle(.red).disabled(state.modelControlsDisabled)
                    }
                }
                .confirmationDialog("删除全部本地识别模型？", isPresented: $confirmDelete, titleVisibility: .visible) {
                    Button("删除", role: .destructive) { state.deleteModels() }
                    Button("取消", role: .cancel) { }
                }
            }
            SettingsSection(title: "启动与回声", icon: "speaker.wave.2.bubble.left") {
                Toggle("后台预加载识别模型（开始几乎瞬间启动）", isOn: $state.keepRecognizerWarm)
                    .onChange(of: state.keepRecognizerWarm) { _, _ in state.saveRecognizerSettings() }
                if let seconds = state.lastStartupSeconds { Note(String(format: "上次开始用时 %.1f 秒", seconds), presentation: .inline) }
                HStack(spacing: 8) {
                    Toggle("忽略麦克风里的扬声器回声（推荐）", isOn: $state.echoSuppression)
                        .onChange(of: state.echoSuppression) { _, _ in state.updateSettings() }
                    Note("不戴耳机时对方的声音会从扬声器传进麦克风；应用比对两路音频的节奏和文字，回声不记为“我”。戴耳机始终最干净。", presentation: .info)
                        .accessibilityLabel("回声处理说明")
                }
                Toggle("麦克风回声消除（Apple 语音处理，实验）", isOn: $state.microphoneVoiceProcessing)
                    .disabled(state.isRunning)
                    .onChange(of: state.microphoneVoiceProcessing) { _, _ in state.updateSettings() }
            }
            InsightSettings()
            }
        }
        }
    }
}

/// Model status with download / cancel, shared by settings, the setup checklist and onboarding.
struct ModelDownloadRow: View {
    @Environment(AppState.self) private var state

    var body: some View {
        VStack(alignment: .leading, spacing: 6) {
            HStack {
                if state.isModelInstalled && state.missingPackages.isEmpty {
                    Label("\(state.localStreamingModel.shortTitle)已就绪 · 可离线使用", systemImage: "checkmark.circle.fill").foregroundStyle(.green)
                } else {
                    let size = state.missingPackages.reduce(0) { $0 + $1.approximateMegabytes }
                    Label(size > 0 ? "需要下载识别模型（约 \(size) MB，只需一次）" : "需要下载识别模型", systemImage: "arrow.down.circle").foregroundStyle(.orange)
                }
                Spacer()
                if state.isDownloadingModel {
                    Button("取消") { state.cancelModelDownload() }.buttonStyle(SecondaryButtonStyle())
                } else if !state.missingPackages.isEmpty || !state.isModelInstalled {
                    Button("下载") { state.downloadModel() }.buttonStyle(PrimaryButtonStyle()).disabled(state.modelControlsDisabled)
                }
            }
            if state.isDownloadingModel { ProgressView(value: state.modelDownloadProgress).controlSize(.small) }
            if !state.modelDownloadStatus.isEmpty { Note(state.modelDownloadStatus, presentation: .inline) }
        }
    }
}

private struct LanguagePicker: View {
    @Environment(AppState.self) private var state
    var body: some View {
        @Bindable var state = state
        MenuPicker(title: "识别语言", selection: $state.recognitionLanguage, options: [("auto", "自动 / 中英混说"), ("zh", "中文"), ("en", "English")], localizesOptions: true)
            .disabled(state.isRunning || state.isPreparingModel)
            .onChange(of: state.recognitionLanguage) { _, _ in state.updateSettings(); Task { await state.prewarmRecognizer() } }
    }
}

struct CloudSettings: View {
    @Environment(AppState.self) private var state
    var body: some View {
        @Bindable var state = state
        MenuPicker(title: "服务", selection: $state.cloudProvider, options: CloudSTTProvider.allCases.map { ($0, $0.title) })
            .disabled(state.isRunning)
            .onChange(of: state.cloudProvider) { _, _ in state.loadCloudKey(); state.saveAnswerSettings() }
        TextField("模型（留空使用 \(state.cloudProvider.defaultModel)）", text: $state.cloudModel)
            .textFieldStyle(.roundedBorder)
            .onChange(of: state.cloudModel) { _, _ in state.saveAnswerSettings() }
        SecureField("\(state.cloudProvider.rawValue.capitalized) API Key（保存在系统钥匙串）", text: $state.cloudKey)
            .textFieldStyle(.roundedBorder)
            .onChange(of: state.cloudKey) { _, _ in state.saveCloudKey() }
        Toggle("我了解：会议音频会实时上传到所选服务商并按其条款处理", isOn: $state.cloudConsent)
            .onChange(of: state.cloudConsent) { _, _ in state.saveAnswerSettings() }
        Note("云端识别会把会议音频发送给所选服务。请先确认参与者同意录音和转写；Soniox 支持中英混说，Deepgram 需指定单一语言。", presentation: .inline)
    }
}

// MARK: - Answers

private struct AnswerSettings: View {
    @Environment(AppState.self) private var state

    var body: some View {
        @Bindable var state = state
        VStack(alignment: .leading, spacing: 12) {
        SettingsSection(title: "回答引擎", icon: "sparkles") {
            VStack(alignment: .leading, spacing: 12) {
                AnswerEngineForm(foldSecondary: true)
                EngineTestRow()
                Divider()
                HStack(spacing: 8) {
                    Image(systemName: "text.bubble")
                        .font(.system(size: 13, weight: .regular))
                        .foregroundStyle(Ink.secondary)
                        .frame(width: 18)
                    VStack(alignment: .leading, spacing: 3) {
                        Text("自动回答").font(.system(size: 13, weight: .semibold))
                        Text("在会议顶栏选择“自动回答”或“只记录”。")
                            .font(.system(size: 12)).foregroundStyle(.secondary)
                            .fixedSize(horizontal: false, vertical: true)
                    }
                    Hint("“自动回答”会在识别到问题后生成参考回答；“只记录”不会自动生成，但仍可手动提问。默认模式由会议类型决定（汇报和客户沟通默认自动回答）。明确的问题通常会很快开始回答；不确定的句子会显示为可点击的“可能的问题”。", presentation: .info)
                        .accessibilityLabel("自动回答说明")
                }
            }
        }
        .frame(maxWidth: 1320, alignment: .leading)
        AdvancedOptions("answers") {
            VStack(alignment: .leading, spacing: 12) {
            WebSearchSettings()
            SettingsSection(title: "问题识别", icon: "text.magnifyingglass") {
                HStack(spacing: 8) {
                    Toggle("不确定的句子交给 API 模型二次判断", isOn: $state.verifyUncertainWithModel)
                        .disabled(state.answerBackend != .openAICompatible)
                        .onChange(of: state.verifyUncertainWithModel) { _, _ in state.saveAnswerSettings() }
                    Note("本地规则逐句判断：区分陈述、未说完、提问、请求（“说一下你对…的理解”“walk me through…”）和追问；去掉口头禅，忽略“能听到吗”这类设备确认。课程场景更严格，避免把讲师的自问自答当成提问。", presentation: .info)
                        .accessibilityLabel("问题识别说明")
                }
                if state.answerBackend != .openAICompatible { Note("需要使用 API Key 回答引擎才能启用二次判断。") }
            }
            if state.answerBackend != .openAICompatible {
                SettingsSection(title: "Codex 运行方式", icon: "terminal") {
                    HStack(spacing: 8) {
                        MenuPicker(title: "运行方式", selection: $state.answerBackend,
                                   options: [(.codexAppServer, String(localized: "常驻服务 · 流式（推荐）")),
                                             (.codexExec, String(localized: "codex exec · 兼容"))])
                            .onChange(of: state.answerBackend) { _, _ in state.saveAnswerSettings() }
                        Note("常驻 `codex app-server` 复用进程与登录，逐字流式显示；不可用时自动回退到 codex exec。exec 每次启动一个进程，等完整结果后显示。", presentation: .info)
                            .accessibilityLabel("Codex 运行方式说明")
                    }
                }
            }
            }
        }
        }
    }
}

/// Engine choice and its fields; shared by settings and onboarding.
struct AnswerEngineForm: View {
    @Environment(AppState.self) private var state
    /// Keep essential connection fields visible; the global switch controls extra parameters.
    var foldSecondary = false

    var body: some View {
        @Bindable var state = state
        MenuPicker(title: "引擎", selection: Binding(get: { state.answerBackend == .openAICompatible ? AnswerBackend.openAICompatible : .codexAppServer },
                                                       set: { state.selectAnswerBackend($0) }),
                   options: [(.codexAppServer, String(localized: "Codex（ChatGPT 账号）")),
                             (.openAICompatible, String(localized: "API Key（任意 OpenAI 兼容服务）"))])
        .disabled(state.isAnswering)
        if state.answerBackend == .openAICompatible {
            APIPresetForm(foldSecondary: foldSecondary)
        } else {
            HStack {
                Image(systemName: state.codexReady ? "checkmark.circle.fill" : "exclamationmark.circle")
                    .foregroundStyle(state.codexReady ? .green : .orange)
                VStack(alignment: .leading, spacing: 2) {
                    Text(LocalizedStringKey(state.checkingCodexStatus ? "正在检测 Codex…" : state.codexReady ? "Codex 已登录（ChatGPT 账号）" : state.codexInstalled ? "需要登录 Codex" : "未检测到 Codex")).font(.system(size: 13, weight: .medium)).lineLimit(2)
                    if !state.simplifiedInterface { Text("\(state.codexLoginStatus) · \(state.codexVersion)").font(.system(size: 11)).foregroundStyle(.secondary) }
                }
                Spacer()
                Button { Task { await state.refreshCodexStatus() } } label: { Image(systemName: "arrow.clockwise").frame(width: 28, height: 28) }
                    .buttonStyle(SecondaryButtonStyle()).help("重新检测 Codex 登录状态和可用模型")
            }
            if !state.checkingCodexStatus && !state.codexInstalled {
                Note("未检测到 Codex：安装 ChatGPT / Codex 桌面客户端，或在终端运行 `npm i -g @openai/codex`，然后 `codex login` 用 ChatGPT 账号登录，再点右侧刷新。", color: .orange)
            } else if !state.checkingCodexStatus && !state.codexReady {
                Note("在终端运行 codex login，用 ChatGPT 账号完成登录，再点右侧刷新。已有桌面登录仍显示未连接时，也可用这条命令连接本机 CLI。", color: .orange)
            }
            if foldSecondary {
                MenuPicker(title: "模型", selection: $state.codexModelID, options: codexModelOptions, placeholder: "请选择模型")
                    .disabled(state.isAnswering)
                    .onChange(of: state.codexModelID) { _, _ in state.normalizeCodexSelection() }
                if state.codexModels.isEmpty {
                    TextField("模型 ID（与 Codex 中一致）", text: $state.codexModelID)
                        .textFieldStyle(.roundedBorder).disabled(state.isAnswering)
                        .onChange(of: state.codexModelID) { _, _ in state.updateSettings() }
                }
                AdvancedOptions("answers-codex") {
                    CodexReasoningPicker()
                    CodexFastModeToggle()
                }
            } else {
                HStack(spacing: 10) {
                    MenuPicker(title: "模型", selection: $state.codexModelID, options: codexModelOptions, placeholder: "请选择模型")
                        .disabled(state.isAnswering)
                        .onChange(of: state.codexModelID) { _, _ in state.normalizeCodexSelection() }
                    CodexReasoningPicker()
                }
                if state.codexModels.isEmpty {
                    TextField("模型 ID（与 Codex 中一致）", text: $state.codexModelID)
                        .textFieldStyle(.roundedBorder).disabled(state.isAnswering)
                        .onChange(of: state.codexModelID) { _, _ in state.updateSettings() }
                }
                CodexFastModeToggle()
            }
        }
    }

    private var codexModelOptions: [(String, String)] {
        var options: [(String, String)] = state.codexModels.map { ($0.id, $0.name) }
        if !state.codexModelID.isEmpty, !state.codexModels.contains(where: { $0.id == state.codexModelID }) {
            options.insert((state.codexModelID, state.codexModelID), at: 0)
        }
        return options
    }
}

/// Reasoning strength of the Codex engine; rarely changed, so the settings page
/// folds it away.
private struct CodexReasoningPicker: View {
    @Environment(AppState.self) private var state
    var body: some View {
        @Bindable var state = state
        MenuPicker(title: "推理强度", selection: $state.codexReasoning, options: state.codexEfforts.map { ($0, $0) })
            .disabled(state.isAnswering).onChange(of: state.codexReasoning) { _, _ in state.updateSettings() }
    }
}

/// Fast mode of the Codex engine and the notes that explain it.
private struct CodexFastModeToggle: View {
    @Environment(AppState.self) private var state
    var body: some View {
        @Bindable var state = state
        Toggle(isOn: $state.codexFastMode) {
            HStack(spacing: 6) {
                Image(systemName: "bolt.fill").foregroundStyle(state.codexFastMode ? Brand.accent : .secondary)
                Text("Fast 模式")
                Text(LocalizedStringKey(state.codexFastLabel)).foregroundStyle(.secondary)
            }
        }
        .disabled(state.isAnswering || !state.codexFastAvailable)
        .onChange(of: state.codexFastMode) { _, _ in state.updateSettings() }
        Note(state.codexFastAvailable
             ? (state.currentCodexModel?.defaultsToFast == true
                ? "当前模型默认使用 Fast；关闭后速度较慢、用量较少。"
                : "Fast 回答更快，但会消耗更多 ChatGPT 套餐用量。")
             : "当前模型或 Codex 版本不支持 Fast。", presentation: .inline)
        if !state.appServerNote.isEmpty { Note(state.appServerNote, color: .orange) }
    }
}

struct WebSearchSettings: View {
    @Environment(AppState.self) private var state

    var body: some View {
        @Bindable var state = state
        SettingsSection(title: "联网搜索", icon: "globe") {
            HStack(spacing: 8) {
                MenuPicker(title: "联网", selection: $state.webSearchMode,
                           options: WebSearchMode.allCases.map { ($0, $0 == .off ? String(localized: "关闭联网") : $0.title) }, localizesOptions: true)
                    .onChange(of: state.webSearchMode) { _, _ in state.saveWebSearchSettings() }
                Note("先立刻给出快速回答；需要最新信息（价格、新闻、版本、现任人员、“今天 / 最近”…）时在后台联网，通常 5–20 秒后自动换成联网版，可随时切回原回答。“自动判断”用本地规则。", presentation: .info)
                    .accessibilityLabel("联网搜索说明")
            }
            if state.webSearchMode != .off {
                MenuPicker(title: "搜索方式", selection: $state.webSearchProvider, options: WebSearchProvider.allCases.map { ($0, $0.title) }, localizesOptions: true)
                    .onChange(of: state.webSearchProvider) { _, _ in state.saveWebSearchSettings() }
                switch state.webSearchProvider {
                case .codex:
                    if !state.codexReady {
                        Note("需要安装 Codex 并用 ChatGPT 账号登录（见上方回答引擎）。", color: .orange)
                    } else {
                        Note("联网搜索会把问题发送给 Codex；搜索结果会按当前场景整理成回答。", presentation: .inline)
                    }
                case .tavily:
                    HStack {
                        SecureField("Tavily API Key（保存在系统钥匙串）", text: $state.tavilyKey)
                            .textFieldStyle(.roundedBorder)
                            .onChange(of: state.tavilyKey) { _, _ in state.saveTavilyKey() }
                        Link("获取 Key", destination: URL(string: "https://app.tavily.com")!).font(.system(size: 12))
                    }
                    Note("问题文本会发送给 Tavily；它返回的网页摘要由当前回答引擎整理，并在回答中标注来源。", presentation: .inline)
                }
            }
        }
    }
}

/// Saved providers: pick one, or add another from a template; each keeps its own
/// address, key, model and thinking strength.
struct APIPresetForm: View {
    @Environment(AppState.self) private var state
    /// When true the model fields move into a collapsed “高级选项” block, so the
    /// settings page only asks for the fields most people actually fill in.
    var foldSecondary = false
    @State private var renaming = false
    @State private var newName = ""
    @State private var confirmDelete = false

    var body: some View {
        @Bindable var state = state
        if !state.simplifiedInterface || state.apiPresets.count > 1 {
            HStack(spacing: 8) {
                Text("预设").foregroundStyle(.secondary)
                Menu {
                    ForEach(state.apiPresets) { preset in
                        Button { state.selectAPIPreset(preset.id) } label: {
                            if preset.id == state.activeAPIPresetID { Label(preset.name, systemImage: "checkmark") } else { Text(preset.name) }
                        }
                    }
                    Divider()
                    if !state.simplifiedInterface {
                    Menu("新建预设") {
                        ForEach(APIPreset.templates, id: \.name) { template in
                            Button(template.name) { state.addAPIPreset(name: template.name, baseURL: template.url) }
                        }
                        Divider()
                        Button("自定义…") { state.addAPIPreset(name: "自定义", baseURL: "https://") }
                    }
                    }
                } label: {
                    Label(state.activeAPIPreset?.name ?? "选择预设", systemImage: "server.rack")
                }
                .fixedSize()
                if !state.simplifiedInterface {
                    Button("重命名") { newName = state.activeAPIPreset?.name ?? ""; renaming = true }.buttonStyle(ChipButtonStyle())
                    Button("删除") { confirmDelete = true }.buttonStyle(ChipButtonStyle()).disabled(state.apiPresets.count <= 1)
                }
                Spacer()
            }
            .disabled(state.isAnswering)
            .alert("重命名预设", isPresented: $renaming) {
                TextField("名称", text: $newName)
                Button("保存") { if let id = state.activeAPIPresetID { state.renameAPIPreset(id, to: newName) } }
                Button("取消", role: .cancel) { }
            }
            .confirmationDialog("删除预设“\(state.activeAPIPreset?.name ?? "")”？", isPresented: $confirmDelete, titleVisibility: .visible) {
                Button("删除", role: .destructive) { if let id = state.activeAPIPresetID { state.deleteAPIPreset(id) } }
                Button("取消", role: .cancel) { }
            } message: { Text("会同时删除保存在钥匙串里的这个 API Key。") }
        }

        TextField("API 地址，例如 https://api.openai.com/v1", text: $state.apiBaseURL)
            .textFieldStyle(.roundedBorder)
            .onChange(of: state.apiBaseURL) { _, _ in state.saveAnswerSettings() }
        SecureField("API Key（保存在系统钥匙串）", text: $state.apiKey)
            .textFieldStyle(.roundedBorder)
            .onSubmit { state.saveAPIKey() }
            .onChange(of: state.apiKey) { _, _ in state.saveAPIKey() }
        APIModelDetails(foldSecondary: foldSecondary)
        Note("转录片段、检索到的资料和截图会发送到这个服务。", presentation: .inline)
    }
}

/// Model id, thinking strength and the model list of an OpenAI compatible
/// service; the settings page folds them away, onboarding shows them.
private struct APIModelDetails: View {
    @Environment(AppState.self) private var state
    var foldSecondary = false

    var body: some View {
        @Bindable var state = state
        HStack(spacing: 8) {
            TextField("模型 ID（与服务商一致）", text: $state.apiModel)
                .textFieldStyle(.roundedBorder)
                .onChange(of: state.apiModel) { _, _ in state.saveAnswerSettings() }
            if !state.apiModelChoices.isEmpty {
                Menu("选择") {
                    ForEach(state.apiModelChoices, id: \.self) { id in
                        Button(id) { state.apiModel = id; state.saveAnswerSettings() }
                    }
                }
                .fixedSize()
            }
            if !state.simplifiedInterface {
                Button("获取模型列表") { Task { await state.fetchAPIModels() } }.buttonStyle(ChipButtonStyle())
            }
        }
        if !state.apiModelsStatus.isEmpty { Note(state.apiModelsStatus, presentation: .inline) }
        if !foldSecondary || !state.simplifiedInterface {
            HStack(spacing: 8) {
                MenuPicker(title: "思考强度", selection: $state.apiReasoningEffort, options: APIPreset.efforts)
                    .onChange(of: state.apiReasoningEffort) { _, _ in state.saveAnswerSettings() }
                Note("仅对服务商支持的推理模型有效；不确定时保持默认。", presentation: .info)
                    .accessibilityLabel("思考强度说明")
            }
        }
    }
}

/// Sends a one-line test question through the configured engine.
struct EngineTestRow: View {
    @Environment(AppState.self) private var state

    var body: some View {
        HStack(spacing: 10) {
            Button {
                Task { await state.testAnswerEngine() }
            } label: {
                HStack(spacing: 6) {
                    if state.engineTest == .running { ProgressView().controlSize(.small) }
                    Label("测试回答", systemImage: "play.circle")
                }
            }
            .buttonStyle(SecondaryButtonStyle())
            .disabled(state.engineTest == .running || state.isAnswering)
            switch state.engineTest {
            case .idle: Text("发一句测试问题，确认引擎能正常回答").font(.system(size: 11)).foregroundStyle(.secondary)
            case .running: Text("正在请求…").font(.system(size: 11)).foregroundStyle(.secondary)
            case let .passed(seconds, reply):
                Label(String(format: "可以使用 · %.1f 秒 · “%@”", seconds, String(reply.prefix(24))), systemImage: "checkmark.circle.fill")
                    .font(.system(size: 11)).foregroundStyle(.green).lineLimit(1)
            case let .failed(message):
                Label(message, systemImage: "xmark.octagon.fill").font(.system(size: 11)).foregroundStyle(.orange).lineLimit(2)
            }
            Spacer(minLength: 0)
        }
    }
}

// MARK: - Skills

private struct SkillSettings: View {
    @Environment(AppState.self) private var state
    @State private var editing: Skill?
    @State private var deleting: Skill?
    private var library: SkillLibrary { SkillLibrary.shared }

    var body: some View {
        SettingsSection(title: "技能库", icon: "wand.and.stars") {
            HStack(spacing: 6) {
                Text("技能可用于所有系列，或在资料夹中按系列选择。")
                    .font(.system(size: 11.5)).foregroundStyle(.secondary)
                Note("技能是一份 Markdown 说明（例如 SKILL.md）：你希望的说话方式、答题套路、领域知识。打开“所有系列”的技能每次回答都会用；其他技能在资料夹的“技能”页按系列勾选。", presentation: .info)
                    .accessibilityLabel("技能使用说明")
            }
            HStack {
                Button { library.importFiles(global: false) } label: { Label("导入 .md 文件…", systemImage: "square.and.arrow.down") }
                    .buttonStyle(PrimaryButtonStyle())
                Button { editing = library.create(global: false) } label: { Label("新建技能", systemImage: "plus") }
                    .buttonStyle(SecondaryButtonStyle())
                Spacer()
            }
            if library.skills.isEmpty {
                Note("还没有技能。可以导入现成的 SKILL.md，或点“新建技能”从模板开始。", presentation: .inline)
            } else {
                VStack(spacing: 6) {
                    ForEach(library.skills) { skill in row(skill) }
                }
            }
        }
        .sheet(item: $editing) { skill in SkillEditor(skill: skill) { library.update($0) } }
        .confirmationDialog("删除技能“\(deleting?.name ?? "")”？", isPresented: Binding(get: { deleting != nil }, set: { if !$0 { deleting = nil } }), titleVisibility: .visible) {
            Button("删除", role: .destructive) { if let deleting { library.delete(deleting.id) }; deleting = nil }
            Button("取消", role: .cancel) { deleting = nil }
        }
    }

    private func row(_ skill: Skill) -> some View {
        let attached = state.workspaces.workspaces.filter { $0.skillIDs.contains(skill.id) }.count
        return HStack(spacing: 10) {
            Image(systemName: skill.global ? "globe" : "doc.text").foregroundStyle(skill.enabled ? Brand.accent : .secondary).frame(width: 18)
            VStack(alignment: .leading, spacing: 2) {
                Text(skill.name).font(.system(size: 13, weight: .semibold)).foregroundStyle(skill.enabled ? .primary : .secondary)
                Text(skill.summary.isEmpty ? "\(skill.content.count) 字" : skill.summary).font(.system(size: 11)).foregroundStyle(.secondary).lineLimit(1)
                if !skill.global { Text(attached == 0 ? "还没有场景使用" : "\(attached) 个场景在用").font(.system(size: 10.5)).foregroundStyle(.tertiary) }
            }
            Spacer()
            Toggle("所有系列", isOn: Binding(get: { skill.global }, set: { var next = skill; next.global = $0; library.update(next) }))
                .toggleStyle(.checkbox)
            Toggle("启用", isOn: Binding(get: { skill.enabled }, set: { var next = skill; next.enabled = $0; library.update(next) }))
                .toggleStyle(.switch).controlSize(.small).labelsHidden()
            Button("编辑") { editing = skill }.buttonStyle(ChipButtonStyle())
            Button { deleting = skill } label: { Image(systemName: "trash") }.buttonStyle(.borderless).foregroundStyle(.red)
        }
        .padding(.horizontal, 10).padding(.vertical, 8)
        .background(Color.primary.opacity(0.04), in: RoundedRectangle(cornerRadius: 10, style: .continuous))
    }
}

struct SkillEditor: View {
    @State var skill: Skill
    let save: (Skill) -> Void
    @Environment(\.dismiss) private var dismiss

    var body: some View {
        VStack(alignment: .leading, spacing: 10) {
            Text("编辑技能").font(.headline)
            TextField("名称", text: $skill.name).textFieldStyle(.roundedBorder)
            TextField("一句话说明（可选）", text: $skill.summary).textFieldStyle(.roundedBorder)
            TextEditor(text: $skill.content)
                .font(.system(size: 13, design: .monospaced))
                .scrollContentBackground(.hidden)
                .padding(6)
                .background(Color.primary.opacity(0.05), in: RoundedRectangle(cornerRadius: 8))
                .frame(minHeight: 320)
            Toggle("所有系列都使用这个技能", isOn: $skill.global)
            HStack {
                Text("\(skill.content.count) 字 · 单个技能最多使用约 6000 字").font(.system(size: 11)).foregroundStyle(.secondary)
                Spacer()
                Button("取消") { dismiss() }
                Button("保存") { save(skill); dismiss() }.keyboardShortcut(.defaultAction)
            }
        }
        .padding(20)
        .frame(width: 620)
    }
}

// MARK: - Overlay

private struct OverlaySettings: View {
    @Environment(AppState.self) private var state
    var body: some View {
        @Bindable var state = state
        VStack(alignment: .leading, spacing: 12) {
        SettingsSection(title: "回答浮窗", icon: "rectangle.topthird.inset.filled") {
            Button("显示回答浮窗") { TeleprompterController.shared.show() }.buttonStyle(PrimaryButtonStyle())
            HStack {
                Text("移动到")
                ForEach(TeleprompterController.Placement.allCases) { placement in
                    Button(placement.title) { TeleprompterController.shared.move(to: placement) }.buttonStyle(ChipButtonStyle())
                        .help("显示回答浮窗并移动到\(placement.title)")
                }
            }
            HStack {
                Text("字号")
                Slider(value: $state.teleprompterFontSize, in: 14...44, step: 1)
                    .frame(maxWidth: 320)
                    .onChange(of: state.teleprompterFontSize) { _, _ in state.saveAnswerSettings() }
                Text("\(Int(state.teleprompterFontSize))").font(.caption.monospacedDigit()).frame(width: 36)
            }
            Toggle("尽力从屏幕共享中排除本应用窗口", isOn: $state.capturePrivacy)
                .onChange(of: state.capturePrivacy) { _, _ in state.saveAnswerSettings(); state.updateSettings() }
            Note(String(localized: "系统无法保证窗口不被录屏。共享会议窗口或标签页更稳妥；\(state.shortcutLabel(.panicHide)) 可隐藏全部窗口。"), presentation: .inline)
        }
        AdvancedOptions("overlay") {
            SettingsSection(title: "外观细节与共享测试", icon: "slider.horizontal.3") {
                HStack {
                    Text("不透明度")
                    Slider(value: $state.teleprompterOpacity, in: 0.35...1).disabled(state.teleprompterHideBackground)
                        .frame(maxWidth: 320)
                        .onChange(of: state.teleprompterOpacity) { _, _ in state.saveAnswerSettings() }
                    Text("\(Int(state.teleprompterOpacity * 100))%").font(.caption.monospacedDigit()).frame(width: 36)
                }
                Toggle("隐藏背景，只保留文字、边框与按钮", isOn: $state.teleprompterHideBackground)
                    .onChange(of: state.teleprompterHideBackground) { _, _ in state.saveAnswerSettings() }
                Toggle("鼠标穿透（点击穿过回答浮窗）", isOn: Binding(get: { state.clickThrough }, set: { _ in state.toggleClickThrough() }))
                HStack {
                    Button("共享可见性测试") { state.runShareVisibilityTest() }.buttonStyle(SecondaryButtonStyle())
                    Button("仅显示回答浮窗") { TeleprompterController.shared.showOnlyTeleprompter() }.buttonStyle(SecondaryButtonStyle())
                }
                Note("手机显示与遥控在高级设置 → 更多 → 手机伴侣。隐藏背景时，不透明度不会影响文字与按钮。")
            }
        }
        }
    }
}

// MARK: - Recording

private struct RecordingSettings: View {
    @Environment(AppState.self) private var state
    @State private var includeSecrets = false
    @State private var applySettings = true
    @State private var backups: [DataBackup.BackupFile] = []
    @State private var restoring: DataBackup.BackupFile?

    var body: some View {
        @Bindable var state = state
        VStack(alignment: .leading, spacing: 12) {
        SettingsCardRow {
        SettingsSection(title: "会话记录与复盘", icon: "tray.full") {
            Toggle("记录每次会话（转录、识别到的问题、会中回答与当时的系列资料）", isOn: $state.recordSessions)
                .disabled(state.isRunning)
                .onChange(of: state.recordSessions) { _, _ in state.updateSettings() }
            if !state.simplifiedInterface {
            Toggle("会议结束后自动整理纪要（设置 → 纪要）", isOn: $state.autoSummarize)
                .disabled(!state.recordSessions)
                .onChange(of: state.autoSummarize) { _, _ in state.updateSettings() }
            if !state.recordSessions { Note("请先在“数据”中开启会话记录。") }
            Toggle("把复盘得到的经验和改进回答自动加入场景的经验库与题库（自我迭代）", isOn: $state.autoApplyLessons)
                .disabled(!state.autoSummarize)
                .onChange(of: state.autoApplyLessons) { _, _ in state.updateSettings() }
            if !state.autoSummarize { Note("请先开启会后自动整理纪要。") }
            }
            if state.simplifiedInterface { Note(state.autoSummarize ? "自动复盘已开启，会使用当前回答服务与额度。" : "自动复盘未开启，可在高级设置调整。", presentation: .inline) }
            Note("转录边说边保存在本机，不保存原始音频。生成摘要与复盘会把本次转录发送给当前回答服务并使用其额度。", presentation: .inline)
        }
        } right: {
        SettingsSection(title: "导出与导入", icon: "externaldrive") {
            Note("备份包含系列资料、经验库、题库、阶段复盘、会议记录和设置。导入会合并，不删除现有数据；冲突时保留较新的修改。", presentation: .inline)
            HStack {
                Button("导出全部数据…") { state.exportAllData(includeSecrets: includeSecrets && !state.simplifiedInterface) }.buttonStyle(PrimaryButtonStyle()).disabled(state.isDataBusy)
                if !state.simplifiedInterface { Toggle("包含 API Key 与访问令牌", isOn: $includeSecrets).toggleStyle(.checkbox) }
            }
            HStack {
                Button("导入…") { state.importData(applySettings: applySettings) }.buttonStyle(SecondaryButtonStyle()).disabled(state.isDataBusy || state.isRunning)
                if !state.simplifiedInterface { Toggle("同时导入设置（回答引擎、识别、快捷键等）", isOn: $applySettings).toggleStyle(.checkbox) }
            }
            if state.simplifiedInterface { Note("导出不包含密钥。导入" + (applySettings ? "会一并恢复备份中的设置。" : "保留当前设置。"), presentation: .inline) }
            if includeSecrets && !state.simplifiedInterface { Note("导出文件里的密钥是明文，请妥善保管，不要发给别人。", color: .orange) }
            if state.isDataBusy { ProgressView().controlSize(.small) }
            if !state.dataStatus.isEmpty { Note(state.dataStatus, color: .green) }
        }
        }
        SettingsSection(title: "自动备份", icon: "clock.arrow.circlepath") {
            if !state.simplifiedInterface {
            Toggle("会议结束后自动创建恢复点", isOn: $state.autoBackup)
                .onChange(of: state.autoBackup) { _, _ in state.updateSettings() }
            }
            HStack {
                Button("立即备份") { state.backupNow(); refreshSoon() }.buttonStyle(SecondaryButtonStyle()).disabled(state.isDataBusy)
                Button("打开数据文件夹") {
                    try? FileManager.default.createDirectory(at: AppPaths.root, withIntermediateDirectories: true)
                    NSWorkspace.shared.open(AppPaths.root)
                }.buttonStyle(SecondaryButtonStyle())
            }
            if state.simplifiedInterface {
                Note(state.autoBackup ? "会议结束后自动备份；更新和导入前保留恢复点。" : "会议自动备份未开启，可在高级设置调整。", presentation: .inline)
                if let latest = backups.first { Note("最近备份：" + latest.date.formatted(date: .abbreviated, time: .shortened), presentation: .inline) }
            } else if backups.isEmpty {
                Note("还没有自动备份。", presentation: .inline)
            } else {
                ForEach(backups.prefix(10)) { backup in
                    HStack {
                        Image(systemName: "archivebox").foregroundStyle(.secondary)
                        Text(backup.date.formatted(date: .abbreviated, time: .shortened))
                        Text(reasonTitle(backup.reason)).foregroundStyle(.secondary)
                        Text(ByteCountFormatter.string(fromByteCount: Int64(backup.size), countStyle: .file)).foregroundStyle(.tertiary)
                        Spacer()
                        Button("恢复…") { restoring = backup }.buttonStyle(ChipButtonStyle()).disabled(state.isRunning)
                        Button { NSWorkspace.shared.activateFileViewerSelecting([backup.url]) } label: { Image(systemName: "folder") }.buttonStyle(.borderless)
                    }
                }
            }
            Note(String(localized: "数据保存在 \(AppPaths.root.path)。恢复点最多保留最近 10 份、软预算 2 GiB，不含模型、索引或密钥。"), presentation: .inline)
        }
        .onAppear { backups = DataBackup.listBackups() }
        .onChange(of: state.section) { _, section in if section == .settings { backups = DataBackup.listBackups() } }
        .confirmationDialog("从这份备份恢复？", isPresented: Binding(get: { restoring != nil }, set: { if !$0 { restoring = nil } }), titleVisibility: .visible) {
            Button("合并恢复") { if let restoring { state.importArchive(restoring.url, applySettings: false) }; restoring = nil; refreshSoon() }
            Button("取消", role: .cancel) { restoring = nil }
        } message: {
            Text("把备份里的场景和会话记录合并回来；现有数据不会被删除，较新的修改会保留。")
        }
        }
    }

    private func refreshSoon() {
        Task { try? await Task.sleep(for: .seconds(2)); backups = DataBackup.listBackups() }
    }

    private func reasonTitle(_ reason: String) -> String {
        switch reason {
        case "daily": String(localized: "旧每日备份")
        case "session": String(localized: "会议结束")
        case "update": String(localized: "更新前")
        case "import": String(localized: "导入前")
        case "manual": String(localized: "手动")
        default: String(localized: String.LocalizationValue(reason))
        }
    }
}

// MARK: - More

private struct MoreSettings: View {
    @Environment(AppState.self) private var state

    var body: some View {
        @Bindable var state = state
        VStack(alignment: .leading, spacing: 12) {
            UpdateSettings()
            AdvancedOptions("maintenance") {
                VStack(alignment: .leading, spacing: 12) {
                    SettingsSection(title: "权限与签名", icon: "lock.shield") {
                        Text(state.capturePermissionStatus).foregroundStyle(state.capturePermissionGranted ? .green : .secondary)
                        HStack {
                            Button("录屏与系统音频…") { state.openCapturePermissionSettings() }.buttonStyle(SecondaryButtonStyle())
                            Button("麦克风…") { state.openMicrophoneSettings() }.buttonStyle(SecondaryButtonStyle())
                            Button("刷新") { Task { await state.refreshSetupStatus() } }.buttonStyle(ChipButtonStyle())
                        }
                        SignatureStatus()
                    }
                    ShortcutSection()
                    CompanionSection()
                    SettingsSection(title: "性能", icon: "gauge.with.dots.needle.33percent") {
                        Toggle("减少视觉特效（更流畅，适合轻薄本 / 省电）", isOn: $state.reducedEffects)
                            .onChange(of: state.reducedEffects) { _, value in UserDefaults.standard.set(value, forKey: "reducedEffects") }
                        Note("关闭液态玻璃的实时折射，改用平面半透明效果。系统“降低透明度”开启时会自动使用。")
                    }
                }
            }
        }
    }
}

// MARK: - Updates

private struct UpdateSettings: View {
    @Environment(AppState.self) private var state
    private var updater: Updater { Updater.shared }

    var body: some View {
        @Bindable var updater = Updater.shared
        SettingsSection(title: "自动更新", icon: "arrow.down.circle") {
            HStack {
                VStack(alignment: .leading, spacing: 2) {
                    Text("当前版本 \(updater.currentVersion)（#\(updater.currentBuild)）").font(.system(size: 13, weight: .medium))
                    Text(statusText).foregroundStyle(statusColor).lineLimit(3)
                }
                Spacer()
                switch updater.phase {
                case .ready:
                    Button("重启并安装") { updater.installAndRelaunch() }.buttonStyle(PrimaryButtonStyle()).disabled(state.isRunning)
                case .available:
                    Button("下载") { updater.downloadUpdate() }.buttonStyle(PrimaryButtonStyle())
                case .checking, .downloading, .installing:
                    ProgressView().controlSize(.small)
                default:
                    Button("检查更新") { Task { await updater.check(userInitiated: true) } }.buttonStyle(SecondaryButtonStyle())
                }
            }
            if case let .downloading(fraction) = updater.phase { ProgressView(value: fraction).controlSize(.small) }
            if let note = updater.stagedNote { Note(note, color: .orange) }
            if state.isRunning, updater.phase == .ready { Note("正在辅助中：停止后再安装，避免打断会议。", color: .orange) }
            if !state.simplifiedInterface {
            Picker("更新渠道", selection: $updater.channel) {
                ForEach(UpdateChannel.allCases) { Text($0.title).tag($0) }
            }
            .pickerStyle(.segmented)
            .onChange(of: updater.channel) { _, _ in updater.saveSettings(); Task { await updater.check(userInitiated: true) } }
            Toggle("自动检查更新（启动后与每 6 小时；有新版本时在后台下载）", isOn: $updater.automaticChecks)
                .onChange(of: updater.automaticChecks) { _, _ in updater.saveSettings() }
            }
            Note(state.simplifiedInterface ? "更新前会自动备份，并校验安装包。" : "从 GitHub Releases 下载，校验 SHA-256 和签名后再安装；已绑定本机签名时，更新后权限保持不变。")
        }
        // Show essential access help after an error; otherwise use the global expert switch.
        if updater.needsAccessHelp {
            GitHubAccessSection()
        } else {
            AdvancedOptions("updates") { GitHubAccessSection() }
        }
    }

    private var statusText: String {
        switch updater.phase {
        case .idle: return updater.lastChecked.map { "上次检查 \($0.formatted(date: .omitted, time: .shortened))" } ?? "尚未检查"
        case .checking: return "正在检查…"
        case .upToDate: return "已是最新" + (updater.lastChecked.map { " · \($0.formatted(date: .omitted, time: .shortened)) 检查" } ?? "")
        case .available: return "发现新版本 \(updater.available?.displayVersion ?? "")"
        case let .downloading(fraction): return "正在下载 \(updater.available?.displayVersion ?? "") · \(Int(fraction * 100))%"
        case .ready: return "\(updater.available?.displayVersion ?? "新版本") 已下载并校验，可以安装"
        case .installing: return "正在安装并重新打开…"
        case let .failed(message): return message
        }
    }

    private var statusColor: Color {
        switch updater.phase {
        case .failed: .orange
        case .ready, .available: Brand.accent
        default: .secondary
        }
    }
}

private struct GitHubAccessSection: View {
    @State private var token = ""

    var body: some View {
        @Bindable var updater = Updater.shared
        SettingsSection(title: "GitHub 访问", icon: "key") {
            HStack {
                TextField("仓库", text: $updater.repository).textFieldStyle(.roundedBorder).frame(maxWidth: 260)
                    .onSubmit { updater.saveSettings() }
                Spacer()
                Label(updater.hasToken ? "已保存访问令牌" : "未保存访问令牌", systemImage: updater.hasToken ? "checkmark.seal" : "key")
                    .foregroundStyle(updater.hasToken ? .green : .secondary)
            }
            HStack {
                SecureField("粘贴访问令牌（github_pat_…）", text: $token).textFieldStyle(.roundedBorder)
                Button("保存并检查") {
                    updater.saveToken(token); token = ""
                    Task { await updater.check(userInitiated: true) }
                }
                .buttonStyle(ChipButtonStyle(prominent: true)).disabled(token.isEmpty)
                if updater.hasToken { Button("清除") { updater.saveToken("") }.buttonStyle(ChipButtonStyle()) }
            }
            if let note = updater.accessNote { Note(note, color: .orange) }
            HStack(spacing: 8) {
                Button { NSWorkspace.shared.open(Updater.createTokenURL) } label: { Label("去 GitHub 创建令牌", systemImage: "arrow.up.right.square") }
                    .buttonStyle(SecondaryButtonStyle())
                Text("或在终端运行 gh auth login，就不用填令牌").font(.system(size: 11.5)).foregroundStyle(.secondary)
            }
            VStack(alignment: .leading, spacing: 3) {
                Text("创建令牌时这样填：").font(.system(size: 11.5, weight: .semibold))
                Note("1. Resource owner 选仓库所属的组织 \(updater.repository.split(separator: "/").first.map(String.init) ?? "")（不是个人账号）")
                Note("2. Repository access 选 Only select repositories → \(updater.repository.split(separator: "/").last.map(String.init) ?? "")")
                Note("3. Permissions → Contents 选 Read-only，其余不动；生成后复制 github_pat_ 开头的整串粘贴到上面")
                Note("令牌保存在系统钥匙串。组织要求审批时，需要管理员批准后才能用。")
            }
        }
    }
}

// MARK: - Shortcuts

private struct ShortcutSection: View {
    @Environment(AppState.self) private var state
    var body: some View {
        SettingsSection(title: "全局快捷键", icon: "keyboard") {
            ShortcutSettingsView()
            ForEach(state.shortcutErrors, id: \.self) { Note($0, color: .orange) }
        }
    }
}
