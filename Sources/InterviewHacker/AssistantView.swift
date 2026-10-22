import AppKit
import SwiftUI
import InterviewHackerCore

/// The live workspace: the setup card, then the modules (对话 / 问题 / 回答 / 记录)
/// arranged by the user's layout: two columns or one, sized by dragging the gaps.
struct AssistantView: View {
    @Environment(AppState.self) private var state

    var body: some View {
        VStack(spacing: 12) {
            if !state.isRunning { SetupCard() }
            ModuleHost()
        }
        .padding(.horizontal, 16).padding(.top, 4).padding(.bottom, 8)
        .frame(maxWidth: .infinity, maxHeight: .infinity, alignment: .top)
        .safeAreaInset(edge: .bottom, spacing: 0) { ComposerDock() }
    }
}

/// One module's content, docked or in its own window.
struct AssistantModuleContent: View {
    @Environment(AppState.self) private var state
    let module: AssistantModule
    var detached = false

    var body: some View {
        switch module {
        case .conversation: ConversationPanel()
        case .question:
            if detached {
                DetectionStrip(showsEmpty: true).frame(maxHeight: .infinity, alignment: .top)
            } else {
                DetectionStrip()
            }
        case .answer:
            VStack(spacing: 12) {
                ScreenshotCard()
                AnswerPanel(fillHeight: true)
            }
        case .history: AnswerHistoryPanel()
        case .insights: InsightsPanel()
        }
    }
}

// MARK: - Module layout

/// Docked modules in one or two columns; the gaps between them are resize handles.
private struct ModuleHost: View {
    @Environment(AppState.self) private var state
    @State private var liveSplit: Double?
    @State private var splitStart: CGFloat?

    var body: some View {
        GeometryReader { proxy in
            let layout = state.assistantLayout
            let wide = proxy.size.width >= 760
            Group {
                if layout.slots.isEmpty {
                    EmptyLayoutHint()
                } else if wide && layout.usesTwoColumns {
                    let split = liveSplit ?? layout.split
                    let left = max(CGFloat(300), min(proxy.size.width - CGFloat(312), (proxy.size.width - CGFloat(12)) * CGFloat(split)))
                    HStack(spacing: 0) {
                        ModuleColumn(slots: layout.slots(in: 0), height: proxy.size.height).frame(width: left)
                        ResizeHandle(axis: .horizontal) {
                            splitStart = left
                        } onDrag: { delta in
                            let start = splitStart ?? left
                            liveSplit = Double(min(max((start + delta) / max(CGFloat(1), proxy.size.width - CGFloat(12)), 0.25), 0.75))
                        } onEnd: {
                            if let liveSplit { state.updateLayout { $0.split = liveSplit } }
                            liveSplit = nil
                            splitStart = nil
                        }
                        .frame(width: 12)
                        ModuleColumn(slots: layout.slots(in: 1), height: proxy.size.height)
                    }
                } else {
                    // One column (narrow window or single-column layout): answer side first.
                    ModuleColumn(slots: layout.slots(in: 1) + layout.slots(in: 0), height: proxy.size.height)
                }
            }
            .frame(width: proxy.size.width, height: proxy.size.height, alignment: .top)
        }
    }
}

private struct EmptyLayoutHint: View {
    @Environment(AppState.self) private var state
    var body: some View {
        VStack(spacing: 10) {
            Image(systemName: "rectangle.3.group").font(.system(size: 28)).foregroundStyle(.secondary)
            Text("所有模块都已隐藏或弹出为独立窗口，可以在下方“模块”菜单里恢复").foregroundStyle(.secondary)
            Button("恢复默认布局") { state.updateLayout { $0 = .standard } }.buttonStyle(SecondaryButtonStyle())
        }
        .frame(maxWidth: .infinity, maxHeight: .infinity)
        .card()
    }
}

/// A column: compact modules take their own height, the rest share what is left by weight.
private struct ModuleColumn: View {
    @Environment(AppState.self) private var state
    let slots: [AssistantLayout.Slot]
    let height: CGFloat
    @State private var compactHeights: [AssistantModule: CGFloat] = [:]
    @State private var liveWeights: [AssistantModule: Double] = [:]
    /// Heights and combined weight of the pair when a gap drag began.
    @State private var dragStart: (top: CGFloat, bottom: CGFloat, weights: Double)?

    private static let gap: CGFloat = 12

    /// Below this a module's own controls no longer fit and would spill over its neighbours.
    private static func minimum(_ module: AssistantModule) -> CGFloat {
        switch module {
        case .answer: 360
        case .insights: 180
        case .conversation: 150
        default: 120
        }
    }

    var body: some View {
        let heights = weightedHeights()
        let needed = heights.values.reduce(0, +) + slots.filter(\.module.isCompact).reduce(CGFloat(0)) { $0 + (compactHeights[$1.module] ?? 0) }
            + (0..<max(0, slots.count - 1)).reduce(CGFloat(0)) { $0 + gapHeight(after: $1) }
        // Too many modules for the window: scroll instead of squeezing them into each other.
        ScrollView(.vertical) { stack(heights) }
            .scrollDisabled(needed <= height + 1)
            .scrollIndicators(needed > height + 1 ? .automatic : .hidden)
    }

    private func stack(_ heights: [AssistantModule: CGFloat]) -> some View {
        VStack(spacing: 0) {
            ForEach(Array(slots.enumerated()), id: \.element.module) { index, slot in
                AssistantModuleContent(module: slot.module)
                    .frame(height: slot.module.isCompact ? nil : heights[slot.module], alignment: .top)
                    .background {
                        if slot.module.isCompact {
                            GeometryReader { geo in
                                Color.clear.onAppear { compactHeights[slot.module] = geo.size.height }
                                    .onChange(of: geo.size.height) { _, value in compactHeights[slot.module] = value }
                            }
                        }
                    }
                if index < slots.count - 1 {
                    gap(after: index, heights: heights)
                }
            }
        }
        .frame(maxHeight: .infinity, alignment: .top)
    }

    private func weight(_ slot: AssistantLayout.Slot) -> Double { liveWeights[slot.module] ?? slot.weight }

    /// No gap under an empty compact strip (no questions yet).
    private func gapHeight(after index: Int) -> CGFloat {
        let slot = slots[index]
        return slot.module.isCompact && (compactHeights[slot.module] ?? 0) < 1 ? 0 : Self.gap
    }

    private func weightedHeights() -> [AssistantModule: CGFloat] {
        let flexible = slots.filter { !$0.module.isCompact }
        let fixed = slots.filter(\.module.isCompact).reduce(CGFloat(0)) { $0 + (compactHeights[$1.module] ?? 0) }
        let gaps = (0..<max(0, slots.count - 1)).reduce(CGFloat(0)) { $0 + gapHeight(after: $1) }
        let available = max(0, height - fixed - gaps)
        let total = flexible.reduce(0) { $0 + weight($1) }
        var result: [AssistantModule: CGFloat] = [:]
        for slot in flexible { result[slot.module] = max(Self.minimum(slot.module), available * CGFloat(weight(slot) / max(total, 0.01))) }
        return result
    }

    /// Between two flexible modules the gap is a handle that trades height between them.
    @ViewBuilder
    private func gap(after index: Int, heights: [AssistantModule: CGFloat]) -> some View {
        let above = slots[index], below = slots[index + 1]
        if !above.module.isCompact, !below.module.isCompact, let top = heights[above.module], let bottom = heights[below.module] {
            ResizeHandle(axis: .vertical) {
                dragStart = (top, bottom, weight(above) + weight(below))
            } onDrag: { delta in
                guard let start = dragStart else { return }
                let sum = start.top + start.bottom
                let share = Double(min(max(start.top + delta, Self.minimum(above.module)), sum - Self.minimum(below.module)) / max(sum, 1))
                liveWeights[above.module] = start.weights * share
                liveWeights[below.module] = start.weights * (1 - share)
            } onEnd: {
                let changes = liveWeights
                state.updateLayout { layout in for (module, weight) in changes { layout.setWeight(module, weight) } }
                liveWeights = [:]
                dragStart = nil
            }
            .frame(height: Self.gap)
        } else {
            Color.clear.frame(height: gapHeight(after: index))
        }
    }
}

/// Hands its window to `found`; invisible to clicks.
private struct WindowReader: NSViewRepresentable {
    let found: (NSWindow?) -> Void
    final class ReaderView: NSView {
        var found: ((NSWindow?) -> Void)?
        override func viewDidMoveToWindow() { DispatchQueue.main.async { [weak self] in self?.found?(self?.window) } }
        override func hitTest(_ point: NSPoint) -> NSView? { nil }
    }
    func makeNSView(context: Context) -> NSView {
        let view = ReaderView()
        view.found = found
        return view
    }
    func updateNSView(_ nsView: NSView, context: Context) {}
}

/// The gap between modules or columns: a faint grabber that darkens on hover; drag to
/// trade size between the two sides.
private struct ResizeHandle: View {
    enum Axis { case horizontal, vertical }
    let axis: Axis
    let onBegin: () -> Void
    let onDrag: (CGFloat) -> Void
    let onEnd: () -> Void
    @State private var hovering = false
    @State private var active = false
    @State private var cursorPushed = false
    @State private var window: NSWindow?

    var body: some View {
        ZStack {
            WindowReader { window = $0 }
            Color.clear.contentShape(Rectangle())
            Capsule().fill(active ? AnyShapeStyle(Brand.accent) : AnyShapeStyle(Color.primary.opacity(hovering ? 0.35 : 0.12)))
                .frame(width: axis == .horizontal ? 4 : 48, height: axis == .horizontal ? 48 : 4)
        }
        .onHover { inside in
            hovering = inside
            setCursor(inside || active)
        }
        .gesture(DragGesture(minimumDistance: 1, coordinateSpace: .global)
            .onChanged { value in
                if !active { active = true; onBegin() }
                onDrag(axis == .horizontal ? value.translation.width : value.translation.height)
            }
            .onEnded { _ in
                active = false
                onEnd()
                setCursor(hovering)
            })
        .onDisappear { setCursor(false) }
        .animation(.easeOut(duration: 0.12), value: hovering)
        .animation(.easeOut(duration: 0.12), value: active)
        .help("拖动调整大小")
    }

    /// Push and pop stay paired, so the resize cursor never sticks after a drag. While
    /// the pointer is on the handle the window stops moving with background drags:
    /// otherwise it follows the pointer too and the whole page shakes while resizing.
    private func setCursor(_ on: Bool) {
        guard on != cursorPushed else { return }
        cursorPushed = on
        window?.isMovableByWindowBackground = !on
        if on { (axis == .horizontal ? NSCursor.resizeLeftRight : NSCursor.resizeUpDown).push() } else { NSCursor.pop() }
    }
}

/// 布局 menu in the composer bar: modules, presets, reset.
/// The module list is deliberately flat: a menu of menus buried the most common
/// action (show / hide a module) two levels deep and made the app feel
/// confusing. Every module is one row now, and its state is written in the row.
private struct LayoutMenu: View {
    @Environment(AppState.self) private var state

    var body: some View {
        Menu {
            if !state.simplifiedInterface {
                Section("预设布局") {
                    ForEach(AssistantLayout.Preset.allCases) { preset in
                        Toggle(preset.title, isOn: Binding(get: { state.assistantLayout == preset.layout },
                                                           set: { _ in state.updateLayout { $0 = preset.layout } }))
                    }
                }
            }
            Section("模块") {
                ForEach(AssistantModule.allCases) { module in
                    Button { toggle(module) } label: {
                        let layout = state.assistantLayout
                        Label(module.title + stateSuffix(layout, module),
                              systemImage: layout.isHidden(module) ? "eye.slash" : module.symbol)
                    }
                }
            }
            if !state.simplifiedInterface {
                Section("独立窗口") {
                    ForEach(AssistantModule.allCases) { module in
                        Button { toggleDetached(module) } label: {
                            let detached = state.assistantLayout.isDetached(module)
                            Label(detached ? "合并回主窗口 · \(module.title)" : "弹出为独立窗口 · \(module.title)",
                                  systemImage: detached ? "arrow.down.right.and.arrow.up.left" : "macwindow")
                        }
                    }
                }
                Section("位置") {
                    ForEach(AssistantModule.allCases) { module in
                        if let slot = state.assistantLayout.slots.first(where: { $0.module == module }) {
                            Button { state.updateLayout { $0.place(module, column: 1 - slot.column, index: .max) } } label: {
                                Label("\(module.title) · 移到\(slot.column == 0 ? "右栏" : "左栏")",
                                      systemImage: slot.column == 0 ? "arrow.right" : "arrow.left")
                            }
                            Button { state.updateLayout { $0.move(module, by: -1) } } label: {
                                Label("\(module.title) · 上移", systemImage: "arrow.up")
                            }
                            Button { state.updateLayout { $0.move(module, by: 1) } } label: {
                                Label("\(module.title) · 下移", systemImage: "arrow.down")
                            }
                        }
                    }
                }
            }
            Divider()
            Button("恢复默认布局") { state.updateLayout { $0 = .standard } }
        } label: {
            Image(systemName: "rectangle.3.group")
        }
        .menuStyle(.button).menuIndicator(.hidden).fixedSize()
        .buttonStyle(GlassIconButtonStyle(size: 40))
        .help(state.simplifiedInterface
              ? "简化模式：这里只有模块，点一下就显示或隐藏；模块之间的空隙可以拖动调整大小。"
              : "模块与布局：点模块可显示 / 隐藏或弹出为独立窗口，也可选一个预设布局。模块之间的空隙可以拖动调整大小。")
    }

    /// One click per module: hidden → show, in its own window → back into the
    /// main window, visible → hide. The row text says which of the three it is.
    private func toggle(_ module: AssistantModule) {
        let layout = state.assistantLayout
        if layout.isHidden(module) {
            state.updateLayout { $0.show(module) }
        } else if layout.isDetached(module) {
            state.dockModule(module)
        } else {
            state.updateLayout { $0.hide(module) }
        }
    }

    /// Pops a module out into its own window, or merges it back.
    private func toggleDetached(_ module: AssistantModule) {
        if state.assistantLayout.isDetached(module) {
            state.dockModule(module)
        } else {
            state.detachModule(module)
        }
    }

    /// The module's current state, written into the row so the list explains itself:
    /// all three possible states are visible without opening anything.
    private func stateSuffix(_ layout: AssistantLayout, _ module: AssistantModule) -> String {
        if layout.isDetached(module) { return "（独立窗口）" }
        if layout.isHidden(module) { return "（已隐藏）" }
        return "（显示）"
    }
}

/// Every answer in this session; click one to show it in the answer module.
private struct AnswerHistoryPanel: View {
    @Environment(AppState.self) private var state

    var body: some View {
        VStack(alignment: .leading, spacing: 8) {
            HStack {
                Label("回答记录", systemImage: "clock.arrow.circlepath").font(.system(size: 13, weight: .semibold))
                Text("\(state.answerHistory.count)").font(.caption.monospacedDigit()).foregroundStyle(.tertiary)
                Spacer()
                if state.historyCursor != nil {
                    Button("回到最新") { state.historyCursor = nil }.buttonStyle(ChipButtonStyle())
                }
            }
            if state.answerHistory.isEmpty {
                Text("回答过的问题会按时间列在这里，点一下就能回看当时的回答。")
                    .font(.system(size: 12)).foregroundStyle(.secondary)
                    .frame(maxWidth: .infinity, maxHeight: .infinity, alignment: .topLeading)
            } else {
                ScrollView {
                    LazyVStack(alignment: .leading, spacing: 6) {
                        ForEach(Array(state.answerHistory.enumerated().reversed()), id: \.element.id) { index, record in
                            row(record, index: index)
                        }
                    }
                }
            }
        }
        .card()
        .frame(maxHeight: .infinity, alignment: .top)
    }

    private func row(_ record: AnswerRecord, index: Int) -> some View {
        let shown = (state.historyCursor ?? state.answerHistory.count - 1) == index
        let web = state.webLookups[record.id]?.status == .done
        return Button {
            state.historyCursor = index == state.answerHistory.count - 1 ? nil : index
        } label: {
            VStack(alignment: .leading, spacing: 3) {
                HStack(spacing: 6) {
                    Text(record.date.formatted(date: .omitted, time: .shortened)).font(.system(size: 10).monospacedDigit()).foregroundStyle(.tertiary)
                    if web { Image(systemName: "globe").font(.system(size: 9)).foregroundStyle(Brand.accent) }
                    if !record.automatic { Text("手动").font(.system(size: 9.5)).foregroundStyle(.tertiary) }
                }
                Text(AnswerRefinement.originalQuestion(of: record.question)).font(.system(size: 12.5, weight: .semibold)).lineLimit(2)
                Text(record.answer.replacingOccurrences(of: "**", with: "")).font(.system(size: 11.5)).foregroundStyle(.secondary).lineLimit(2)
            }
            .frame(maxWidth: .infinity, alignment: .leading)
            .padding(8)
            .background(shown ? Brand.accent.opacity(0.14) : Color.primary.opacity(0.04), in: RoundedRectangle(cornerRadius: 10, style: .continuous))
            .contentShape(Rectangle())
        }
        .buttonStyle(.plain)
    }
}

// MARK: - Setup (before a session)

private struct SetupCard: View {
    @Environment(AppState.self) private var state

    var body: some View {
        @Bindable var state = state
        VStack(alignment: .leading, spacing: 12) {
            HStack(alignment: .center, spacing: 12) {
                ZStack {
                    Circle().fill(Brand.gradient)
                    Image(systemName: "waveform.and.mic").font(.system(size: 17, weight: .semibold)).foregroundStyle(.white)
                }
                .frame(width: 40, height: 40)
                VStack(alignment: .leading, spacing: 3) {
                    HStack(spacing: 12) {
                        Text("实时语音辅助").font(.system(size: 16, weight: .semibold))
                        Button("新手引导") { state.openOnboarding() }.buttonStyle(ChipButtonStyle())
                    }
                    Text(state.readinessText)
                        .font(.system(size: 12))
                        .foregroundStyle(state.isReadyToStart ? Color.green : Color.secondary)
                        .lineLimit(1)
                }
                Spacer(minLength: 8)
                if !state.capturePermissionGranted {
                    Button("允许录屏与系统音频") { Task { await state.requestCapturePermission() } }
                        .buttonStyle(PrimaryButtonStyle())
                } else if state.transcriptionBackend == "zipformer", !state.isModelInstalled {
                    Button(state.isDownloadingModel ? "下载中 \(Int(state.modelDownloadProgress * 100))%" : "下载识别模型") { state.downloadModel() }
                        .buttonStyle(PrimaryButtonStyle()).disabled(state.isDownloadingModel)
                } else {
                    Button { Task { await state.startSession() } } label: {
                        HStack(spacing: 6) {
                            if state.isChangingSession { ProgressView().controlSize(.small).tint(.white) }
                            Label(state.isChangingSession ? "正在启动…" : "开始辅助", systemImage: "play.fill")
                        }
                        .frame(minWidth: 120)
                    }
                    .buttonStyle(PrimaryButtonStyle())
                    .keyboardShortcut(.defaultAction)
                    .disabled(!state.isReadyToStart || state.isPreparingModel || state.isChangingSession)
                }
            }
            if state.isDownloadingModel {
                ProgressView(value: state.modelDownloadProgress).controlSize(.small)
                Text(state.modelDownloadStatus).font(.caption).foregroundStyle(.secondary)
            }
            if !state.answerEngineReady {
                HStack(spacing: 8) {
                    Image(systemName: "exclamationmark.circle").foregroundStyle(.orange)
                    Text("回答引擎还没设置好：可以转写，但不会生成回答。").font(.system(size: 12))
                    Spacer()
                    Button("设置向导") { state.openOnboarding() }.buttonStyle(ChipButtonStyle(prominent: true))
                }
            }
            HStack(spacing: 6) {
                WorkspacePicker()
                Button { state.section = .materials } label: {
                    Label("资料 \(state.documents.filter(\.enabled).count) · 题库 \(state.activeWorkspace.questionBank.count)", systemImage: "doc.text")
                }.buttonStyle(ChipButtonStyle()).help("编辑这个场景的资料、回答要求、经验库与题库")
                Spacer(minLength: 8)
                // The speaker icon belongs to the picker, so both live in one
                // fixed-size unit: the picker used to reserve a 260pt frame, which
                // stranded the icon hundreds of points away from the visible title.
                HStack(spacing: 4) {
                    Image(systemName: "speaker.wave.2.fill").font(.system(size: 11)).foregroundStyle(.secondary)
                    MenuPicker(selection: $state.selectedTargetID,
                               options: state.captureTargets.map { ($0.id, SetupCard.shortTitle($0.title)) },
                               placeholder: "选择音频来源")
                        .onChange(of: state.selectedTargetID) { _, _ in Task { await state.changeCaptureTarget() } }
                }
                .fixedSize(horizontal: true, vertical: false)
                Button { Task { await state.refreshTargets() } } label: { Image(systemName: "arrow.clockwise") }
                    .buttonStyle(GlassIconButtonStyle(size: 28)).help("刷新窗口列表")
                Toggle(isOn: $state.microphoneEnabled) { Image(systemName: "mic.fill") }
                    .toggleStyle(.button).help("同时采集麦克风（我）")
                    .onChange(of: state.microphoneEnabled) { _, _ in state.updateSettings() }
            }
            Text("选会议 App 的窗口只采集它的声音；麦克风单独识别并标为“我”。没戴耳机时，扬声器回声会被自动识别并忽略。")
                .font(.system(size: 11)).foregroundStyle(.secondary).fixedSize(horizontal: false, vertical: true)
        }
        .card()
    }

    /// Window titles can be arbitrarily long; truncating them keeps the
    /// icon + picker unit compact instead of letting the row grow without bound.
    private static func shortTitle(_ title: String) -> String {
        title.count > 24 ? String(title.prefix(24)) + "…" : title
    }
}

/// Active scenario: mode icon and name; switching loads its materials and knowledge.
struct WorkspacePicker: View {
    @Environment(AppState.self) private var state
    var compact = false

    var body: some View {
        Menu {
            ForEach(SessionMode.allCases) { mode in
                let items = state.workspaces.visible.filter { $0.mode == mode }
                if !items.isEmpty {
                    Section(mode.title) {
                        ForEach(items) { workspace in
                            Button { state.switchWorkspace(workspace.id) } label: {
                                if workspace.id == state.activeWorkspace.id { Label(workspace.name, systemImage: "checkmark") } else { Text(workspace.name) }
                            }
                        }
                    }
                }
            }
            Divider()
            Menu("新建场景") {
                ForEach(SessionMode.allCases) { mode in
                    Button { state.createWorkspace(mode: mode); state.section = .materials } label: { Label(mode.title, systemImage: mode.symbol) }
                }
            }
            Button("管理场景…") { state.section = .materials }
        } label: {
            HStack(spacing: 6) {
                Image(systemName: state.sessionMode.symbol).foregroundStyle(Brand.accent)
                Text(state.activeWorkspace.name).lineLimit(1).foregroundStyle(Color.primary)
                if !compact { Text(state.sessionMode.title).foregroundStyle(.secondary) }
                Image(systemName: "chevron.up.chevron.down").font(.system(size: 9, weight: .semibold)).foregroundStyle(.secondary)
            }
            .font(.system(size: 12.5, weight: .medium))
            .padding(.horizontal, 12).padding(.vertical, 6)
            .glassCapsule()
            .contentShape(Capsule())
        }
        .menuStyle(.button)
        .buttonStyle(.plain)
        .menuIndicator(.hidden)
        .fixedSize()
        .help("当前场景：模式、资料、回答要求和经验库都来自这里")
    }
}

// MARK: - Live conversation

private struct ConversationPanel: View {
    @Environment(AppState.self) private var state
    @State private var follow = true

    var body: some View {
        @Bindable var state = state
        VStack(alignment: .leading, spacing: 10) {
            HStack(spacing: 8) {
                Label("实时对话", systemImage: "text.bubble").font(.system(size: 13, weight: .semibold)).lineLimit(1).fixedSize()
                Text("\(state.entries.count)").font(.caption.monospacedDigit()).foregroundStyle(.tertiary)
                Spacer()
                if state.isRunning { WorkspacePicker(compact: true) }
                if state.isRunning {
                    MenuPicker(selection: $state.selectedTargetID, options: state.captureTargets.map { ($0.id, $0.title) }, placeholder: "音频来源")
                        .frame(maxWidth: 170, alignment: .trailing)
                        .onChange(of: state.selectedTargetID) { _, _ in Task { await state.changeCaptureTarget() } }
                }
                Button { follow.toggle() } label: {
                    Image(systemName: follow ? "arrow.down.to.line.compact" : "pause.circle")
                }
                .buttonStyle(GlassIconButtonStyle(size: 26, tint: follow ? Brand.accent : nil))
                .help(follow ? "自动滚动到最新（点击暂停）" : "已暂停自动滚动（点击恢复）")
            }
            TranscriptStatusLine()
            ScrollViewReader { proxy in
                ScrollView {
                    LazyVStack(alignment: .leading, spacing: 8) {
                        if state.entries.isEmpty {
                            EmptyConversationHint()
                        }
                        ForEach(state.entries) { entry in
                            ConversationBubble(entry: entry).id(entry.id)
                        }
                        PartialBubbles()
                        Color.clear.frame(height: 1).id("bottom")
                    }
                    .padding(.vertical, 4)
                }
                .scrollIndicators(.automatic)
                .onChange(of: state.entries.count) { _, _ in if follow { proxy.scrollTo("bottom", anchor: .bottom) } }
                .onChange(of: state.partialTranscript.count / 8) { _, _ in if follow { proxy.scrollTo("bottom", anchor: .bottom) } }
                .onChange(of: follow) { _, value in if value { proxy.scrollTo("bottom", anchor: .bottom) } }
            }
        }
        .card()
        .frame(maxHeight: .infinity, alignment: .top)
    }
}

private struct TranscriptStatusLine: View {
    @Environment(AppState.self) private var state
    var body: some View {
        HStack(spacing: 6) {
            Text(state.isRunning ? state.transcriptionStatus : "未在采集")
            if !state.microphoneEnabled {
                Text("· 麦克风未开启，不会显示你说的话").foregroundStyle(.orange)
                    .help("在“准备开始”卡片里点麦克风按钮，或在 设置 → 基础 → 音频来源 打开")
            } else if state.microphoneAccess == .denied {
                Button("· 麦克风未授权，点此打开系统设置") { state.openMicrophoneSettings() }
                    .buttonStyle(.plain).foregroundStyle(.orange)
            }
            if state.echoDroppedCount > 0 {
                Text("· \(state.echoDroppedCount) 句疑似回声").help("和对方刚说的话几乎一样的麦克风文字被当作扬声器回声，灰色显示、不参与回答；如果是你说的，点那一行的“是我说的”")
            }
            if state.droppedAudioSegments > 0 {
                Text("· 遗漏 \(state.droppedAudioSegments) 段").foregroundStyle(.orange)
            }
        }
        .font(.system(size: 11)).foregroundStyle(.secondary).lineLimit(1)
    }
}

private struct EmptyConversationHint: View {
    var body: some View {
        VStack(alignment: .leading, spacing: 6) {
            Text("开始辅助后，双方的话会实时出现在这里。").font(.system(size: 12))
            Text("· 左侧灰色为对方（系统声音），右侧彩色为我（麦克风）\n· 识别为问题的句子会标出；把鼠标移到任意一句上可以“回答这句”\n· 原始音频不保存；转录与回答保存在“记录”里")
                .font(.system(size: 11)).foregroundStyle(.secondary)
        }
        .frame(maxWidth: .infinity, alignment: .leading).padding(.vertical, 10)
    }
}

private struct ConversationBubble: View {
    @Environment(AppState.self) private var state
    let entry: TranscriptEntry
    @State private var hovering = false

    var body: some View {
        if entry.suspectedEcho == true { echoLine } else { bubble }
    }

    /// Microphone text that matched the other side: dimmed, one click to keep it.
    private var echoLine: some View {
        HStack(spacing: 6) {
            Spacer(minLength: 40)
            Image(systemName: "speaker.wave.2").font(.system(size: 10)).foregroundStyle(.tertiary)
            Text(entry.text).font(.system(size: 11.5)).foregroundStyle(.tertiary).lineLimit(1).strikethrough(color: .secondary.opacity(0.4))
            Button("是我说的") { state.markNotEcho(entry.id) }
                .buttonStyle(.plain).font(.system(size: 11, weight: .medium)).foregroundStyle(Brand.accent)
        }
        .help("这句和对方刚说的话很像，被当作扬声器回声忽略了（不参与回答）。如果是你说的，点“是我说的”。")
    }

    private var bubble: some View {
        let isMe = entry.isMe
        let isQuestion = !isMe && state.isQuestionText(entry.text)
        return HStack(alignment: .bottom, spacing: 6) {
            if isMe { Spacer(minLength: 40) }
            VStack(alignment: isMe ? .trailing : .leading, spacing: 3) {
                HStack(spacing: 5) {
                    if isQuestion {
                        Label("问题", systemImage: "questionmark.bubble.fill")
                            .font(.system(size: 9.5, weight: .semibold)).foregroundStyle(Brand.accent)
                    }
                    Text(entry.date.formatted(date: .omitted, time: .standard))
                        .font(.system(size: 9.5).monospacedDigit()).foregroundStyle(.tertiary)
                }
                Text(entry.text)
                    .font(.system(size: 13)).lineSpacing(2)
                    .textSelection(.enabled)
                    .padding(.horizontal, 11).padding(.vertical, 7)
                    .background(bubbleColor(isMe: isMe, isQuestion: isQuestion), in: RoundedRectangle(cornerRadius: 14, style: .continuous))
                    .overlay(alignment: isMe ? .bottomLeading : .bottomTrailing) {
                        if hovering && !isMe {
                            Button { state.answerEntry(entry) } label: { Label("回答这句", systemImage: "bolt.fill") }
                                .buttonStyle(ChipButtonStyle(prominent: true))
                                .offset(x: 8, y: 10)
                                .transition(.opacity)
                        }
                    }
            }
            if !isMe { Spacer(minLength: 40) }
        }
        .onHover { hovering = $0 }
        .animation(.easeOut(duration: 0.12), value: hovering)
    }

    private func bubbleColor(isMe: Bool, isQuestion: Bool) -> Color {
        if isMe { return Brand.accent.opacity(0.16) }
        return isQuestion ? Brand.accent.opacity(0.08) : Color.primary.opacity(0.06)
    }
}

/// The two in-progress utterances; only these redraw while someone is speaking.
private struct PartialBubbles: View {
    @Environment(AppState.self) private var state
    var body: some View {
        if !state.partialTranscript.isEmpty { partial(state.partialTranscript, isMe: false) }
        if !state.microphonePartialTranscript.isEmpty { partial(state.microphonePartialTranscript, isMe: true) }
    }

    private func partial(_ text: String, isMe: Bool) -> some View {
        HStack(alignment: .bottom, spacing: 6) {
            if isMe { Spacer(minLength: 40) }
            HStack(alignment: .firstTextBaseline, spacing: 6) {
                Image(systemName: "waveform").font(.system(size: 10)).foregroundStyle(.tertiary)
                Text(text).font(.system(size: 13)).foregroundStyle(.secondary).lineSpacing(2)
            }
            .padding(.horizontal, 11).padding(.vertical, 7)
            .background(RoundedRectangle(cornerRadius: 14, style: .continuous).strokeBorder(Color.primary.opacity(0.12), style: StrokeStyle(lineWidth: 1, dash: [4, 3])))
            if !isMe { Spacer(minLength: 40) }
        }
    }
}

// MARK: - Question detection

/// What the detector is tracking right now, plus uncertain questions to click.
struct DetectionStrip: View {
    @Environment(AppState.self) private var state
    /// In its own window the strip stays visible with a hint instead of collapsing.
    var showsEmpty = false

    var body: some View {
        let live = state.liveCandidate.flatMap { candidate -> QuestionCandidate? in
            candidate.state == .triggered && state.isAnswering ? nil : candidate
        }
        if live != nil || !state.suggestedQuestions.isEmpty {
            VStack(alignment: .leading, spacing: 6) {
                if let candidate = live {
                    HStack(spacing: 8) {
                        Image(systemName: candidate.isFinal ? "text.bubble" : "waveform").foregroundStyle(Brand.accent)
                        Text(candidate.assessment.intent.title).font(.system(size: 11, weight: .semibold)).foregroundStyle(Brand.accent)
                        ConfidenceBar(score: candidate.assessment.score)
                        Text(candidate.fullText).font(.system(size: 12)).foregroundStyle(.secondary).lineLimit(1)
                        Spacer(minLength: 0)
                        if candidate.state != .triggered, candidate.assessment.intent != .statement {
                            Button("回答") { state.answerCandidate(candidate) }.buttonStyle(ChipButtonStyle(prominent: true))
                        }
                    }
                }
                ForEach(state.suggestedQuestions.reversed()) { candidate in
                    HStack(spacing: 8) {
                        Image(systemName: "questionmark.bubble").foregroundStyle(.orange)
                        Text(candidate.fullText).font(.system(size: 12)).lineLimit(1)
                        Spacer(minLength: 0)
                        Button("回答") { state.answerCandidate(candidate) }.buttonStyle(ChipButtonStyle())
                        Button { state.dismissCandidate(candidate) } label: { Image(systemName: "xmark") }
                            .buttonStyle(.borderless).foregroundStyle(.secondary)
                    }
                }
            }
            .card()
            .transition(.opacity)
        } else if showsEmpty {
            Text("对方提问时，识别到的问题和“可能的问题”会出现在这里。")
                .font(.system(size: 12)).foregroundStyle(.secondary)
                .frame(maxWidth: .infinity, alignment: .leading)
                .card()
        }
    }
}

private struct ConfidenceBar: View {
    var score: Double
    var body: some View {
        ZStack(alignment: .leading) {
            Capsule().fill(Color.primary.opacity(0.1))
            Capsule().fill(score >= 0.6 ? Color.green : score >= 0.4 ? Color.orange : Color.secondary)
                .frame(width: 36 * max(0.05, min(1, score)))
        }
        .frame(width: 36, height: 4)
        .help(String(format: "问题置信度 %.0f%%", score * 100))
    }
}

// MARK: - Answer

private struct AnswerPanel: View {
    @Environment(AppState.self) private var state
    var fillHeight: Bool

    var body: some View {
        VStack(alignment: .leading, spacing: 12) {
            AnswerHeader()
            WebLookupStrip()
            AnswerProgressStrip()
            AnswerBody()
                .frame(minHeight: fillHeight ? 160 : 120, maxHeight: fillHeight ? .infinity : 340)
            WebSourcesList()
            RefinementBar()
            AnswerFooter()
        }
        .card(tint: Brand.accent)
        .frame(maxHeight: fillHeight ? .infinity : nil, alignment: .top)
    }
}

private struct AnswerHeader: View {
    @Environment(AppState.self) private var state
    var body: some View {
        HStack(alignment: .top, spacing: 8) {
            VStack(alignment: .leading, spacing: 4) {
                Label(state.isAnswering && state.historyCursor == nil ? "正在回答" : "建议回答", systemImage: "sparkles")
                    .font(.system(size: 12, weight: .semibold)).foregroundStyle(Brand.accent)
                let question = state.displayedQuestion
                if !question.isEmpty {
                    Text(question)
                        .font(.system(size: 13, weight: .medium)).foregroundStyle(.secondary).lineLimit(3).textSelection(.enabled)
                }
            }
            Spacer()
            if let position = state.historyPositionText {
                HStack(spacing: 4) {
                    Button { state.showPreviousAnswer() } label: { Image(systemName: "chevron.left") }
                        .buttonStyle(GlassIconButtonStyle(size: 26)).help("上一条 · \(state.shortcutLabel(.previousAnswer))")
                    Text(position).font(.caption.monospacedDigit()).foregroundStyle(.secondary)
                    Button { state.showNextAnswer() } label: { Image(systemName: "chevron.right") }
                        .buttonStyle(GlassIconButtonStyle(size: 26)).help("下一条 · \(state.shortcutLabel(.nextAnswer))")
                }
            }
            if state.isAnswering {
                ProgressView().controlSize(.small)
                Button { state.cancelAnswer() } label: { Image(systemName: "xmark") }
                    .buttonStyle(GlassIconButtonStyle(size: 26)).help("取消回答 · \(state.shortcutLabel(.cancelAnswer))")
            }
        }
    }
}

/// The streaming text: the only part of the panel that redraws per delta.
private struct AnswerBody: View {
    @Environment(AppState.self) private var state
    var body: some View {
        ScrollViewReader { proxy in
            ScrollView {
                VStack(alignment: .leading, spacing: 0) {
                    let text = state.displayedAnswer
                    if text.isEmpty {
                        Text(state.isAnswering ? "正在生成…" : "对方提问时会自动给出回答；也可以在下方输入问题，或按 \(state.shortcutLabel(.forceAnswer)) 立即回答最新的话。")
                            .font(.system(size: 14)).foregroundStyle(.secondary)
                            .frame(maxWidth: .infinity, alignment: .leading)
                    } else {
                        MarkdownText(text: text, size: 16, lineSpacing: 5).equatable()
                    }
                    Color.clear.frame(height: 1).id("answer-end")
                }
            }
            .onChange(of: state.answerDraft.count / 40) { _, _ in
                if state.isAnswering, state.historyCursor == nil { proxy.scrollTo("answer-end", anchor: .bottom) }
            }
        }
    }
}

/// Background web lookup for the answer on screen: searching, updated (switch
/// back and forth), failed — or a button to look it up by hand.
private struct WebLookupStrip: View {
    @Environment(AppState.self) private var state

    var body: some View {
        if let lookup = state.displayedWebLookup {
            HStack(spacing: 8) {
                switch lookup.status {
                case .searching:
                    ProgressView().controlSize(.mini)
                    Text(lookup.answer.isEmpty ? "正在联网查最新信息，先看上面的快速回答…" : "正在接收联网回答…").foregroundStyle(.secondary)
                    TimelineView(.periodic(from: lookup.startedAt, by: 1)) { context in
                        Text("\(Int(context.date.timeIntervalSince(lookup.startedAt))) 秒").monospacedDigit().foregroundStyle(.tertiary)
                    }
                case .done:
                    Image(systemName: "globe").foregroundStyle(Brand.accent)
                    Text(state.showsWebAnswer ? "已显示联网核查结果" : "有核查更新，可切换查看")
                        .foregroundStyle(state.showsWebAnswer ? Brand.accent : .secondary)
                    if let seconds = lookup.seconds { Text(String(format: "%.0f 秒", seconds)).foregroundStyle(.tertiary) }
                    Spacer(minLength: 0)
                    Button(state.showsWebAnswer ? "看原回答" : "看联网版") { state.toggleWebAnswer() }.buttonStyle(ChipButtonStyle())
                case let .failed(message):
                    Image(systemName: "globe").foregroundStyle(.secondary)
                    Text("联网没有完成：\(message)").foregroundStyle(.secondary).lineLimit(1)
                    Spacer(minLength: 0)
                    Button("重试") { state.lookUpDisplayedAnswer() }.buttonStyle(ChipButtonStyle())
                }
                if lookup.status == .searching { Spacer(minLength: 0) }
            }
            .font(.system(size: 12))
            .padding(.horizontal, 10).padding(.vertical, 6)
            .background(Brand.accent.opacity(0.08), in: RoundedRectangle(cornerRadius: 10, style: .continuous))
        }
    }
}

/// Tavily sources behind the web answer, numbered like the [1] marks in the text.
private struct WebSourcesList: View {
    @Environment(AppState.self) private var state

    var body: some View {
        if state.showsWebAnswer, let sources = state.displayedWebLookup?.sources, !sources.isEmpty {
            VStack(alignment: .leading, spacing: 3) {
                ForEach(Array(sources.prefix(6).enumerated()), id: \.offset) { index, source in
                    if let url = URL(string: source.url) {
                        Link(destination: url) {
                            Text("[\(index + 1)] \(source.title)").lineLimit(1)
                        }
                        .font(.system(size: 11)).foregroundStyle(.secondary)
                    }
                }
            }
        }
    }
}

/// Lays its subviews left to right and wraps onto a new line when the row is full.
/// The answer card uses it so a chip label is never truncated and the buttons
/// never get squeezed into an unreadable sliver.
private struct WrappingHStack: Layout {
    var spacing: CGFloat = 8
    var lineSpacing: CGFloat = 6

    func sizeThatFits(proposal: ProposedViewSize, subviews: Subviews, cache: inout ()) -> CGSize {
        let maxWidth = proposal.width ?? .infinity
        let lines = lines(subviews: subviews, maxWidth: maxWidth)
        let width = lines.map(\.width).max() ?? 0
        let height = lines.reduce(0) { $0 + $1.height } + lineSpacing * CGFloat(max(0, lines.count - 1))
        return CGSize(width: min(width, maxWidth), height: height)
    }

    func placeSubviews(in bounds: CGRect, proposal: ProposedViewSize, subviews: Subviews, cache: inout ()) {
        let lines = lines(subviews: subviews, maxWidth: bounds.width)
        var y = bounds.minY
        for line in lines {
            var x = bounds.minX
            for item in line.items {
                subviews[item.index].place(
                    at: CGPoint(x: x, y: y + (line.height - item.size.height) / 2),
                    anchor: .topLeading,
                    proposal: ProposedViewSize(item.size)
                )
                x += item.size.width + spacing
            }
            y += line.height + lineSpacing
        }
    }

    private struct Item { let index: Int; let size: CGSize }
    private struct Line { var items: [Item] = []; var width: CGFloat = 0; var height: CGFloat = 0 }

    /// Greedy line breaking: a subview that no longer fits starts a new line.
    private func lines(subviews: Subviews, maxWidth: CGFloat) -> [Line] {
        var result: [Line] = []
        var current = Line()
        for index in subviews.indices {
            let size = subviews[index].sizeThatFits(.unspecified)
            let needed = current.items.isEmpty ? size.width : current.width + spacing + size.width
            if needed > maxWidth, !current.items.isEmpty {
                result.append(current)
                current = Line(items: [Item(index: index, size: size)], width: size.width, height: size.height)
            } else {
                current.items.append(Item(index: index, size: size))
                current.width = needed
                current.height = max(current.height, size.height)
            }
        }
        if !current.items.isEmpty { result.append(current) }
        return result
    }
}

private struct AnswerFooter: View {
    @Environment(AppState.self) private var state
    var body: some View {
        VStack(alignment: .leading, spacing: 6) {
            // The action buttons wrap instead of squeezing each other, and the
            // meta line moved to its own row: that way "重新生成" / "联网查一下"
            // are never clipped and never overlap the timing text.
            WrappingHStack(spacing: 8, lineSpacing: 6) {
                Button { state.copyAnswer() } label: { Label("复制", systemImage: "doc.on.doc") }
                    .buttonStyle(ChipButtonStyle()).disabled(state.displayedAnswer.isEmpty)
                    .fixedSize(horizontal: true, vertical: false)
                Button { state.regenerate() } label: { Label("重新生成", systemImage: "arrow.clockwise") }
                    .buttonStyle(ChipButtonStyle()).disabled(state.isAnswering || state.currentQuestion.isEmpty)
                    .fixedSize(horizontal: true, vertical: false)
                if state.webSearchMode != .off, state.displayedWebLookup == nil {
                    Button { state.lookUpDisplayedAnswer() } label: { Label("联网查一下", systemImage: "globe") }
                        .buttonStyle(ChipButtonStyle()).disabled(state.displayedAnswer.isEmpty || state.displayedQuestion.isEmpty)
                        .help("用联网搜索的最新信息重新回答这个问题；快速回答保留，可以切换")
                        .fixedSize(horizontal: true, vertical: false)
                }
            }
            if let id = state.displayedRecordID, let evidence = state.answerCoordinator.evidenceHistory[id] ?? state.answerHistory.first(where: { $0.id == id })?.evidence {
                AnswerEvidenceDetails(evidence: evidence).id(id)
            }
            Text(meta)
                .font(.system(size: 11)).foregroundStyle(.tertiary)
                .lineLimit(1).truncationMode(.tail)
                .frame(maxWidth: .infinity, alignment: .trailing)
            if !state.appServerNote.isEmpty {
                Text(state.appServerNote).font(.caption).foregroundStyle(.orange)
            }
        }
    }

    private var meta: String {
        var parts: [String] = []
        if let first = state.lastFirstTokenSeconds { parts.append(String(format: "首字 %.1fs", first)) }
        if let total = state.lastAnswerSeconds { parts.append(String(format: "完成 %.1fs", total)) }
        let model = state.lastAnswerModel ?? state.currentAnswerModel
        if !model.isEmpty { parts.append(model) }
        if state.activeServiceTier == "fast" { parts.append("Fast") }
        return parts.joined(separator: " · ")
    }
}

private struct ScreenshotCard: View {
    @Environment(AppState.self) private var state
    var body: some View {
        if let image = state.screenshotPreview {
            VStack(alignment: .leading, spacing: 10) {
                HStack {
                    Label("截图预览", systemImage: "viewfinder").font(.system(size: 13, weight: .semibold))
                    Spacer()
                    Button("取消") { state.clearScreenshot() }.buttonStyle(ChipButtonStyle())
                    Button("分析截图") { state.answerScreenshot() }.buttonStyle(ChipButtonStyle(prominent: true))
                }
                Image(nsImage: image).resizable().scaledToFit().frame(maxHeight: 140)
                    .clipShape(RoundedRectangle(cornerRadius: 12, style: .continuous))
                Text("可在下方输入框补充要求，例如“只指出 bug”。").font(.caption).foregroundStyle(.secondary)
            }
            .card()
        }
    }
}

// MARK: - Composer

private struct ComposerDock: View {
    @Environment(AppState.self) private var state

    var body: some View {
        VStack(spacing: 6) {
            HStack(spacing: 8) {
                // Outside the glass container: a GlassEffectContainer composites merged
                // glass above non-glass content, which would hide the text field.
                QuestionComposer()
                GlassGroup(spacing: 8) { HStack(spacing: 8) {
                    LayoutMenu()
                    // Simplified mode folds the secondary switches away, so the
                    // composer only shows the controls people actually need.
                    if state.answerBackend != .openAICompatible && state.codexFastAvailable && !state.simplifiedInterface {
                        FastModeToggle()
                    }
                    Button { state.captureScreenshot(.region) } label: { Image(systemName: "viewfinder") }
                        .buttonStyle(GlassIconButtonStyle(size: 40))
                        .help("区域截图提问 · \(state.shortcutLabel(.captureRegion))")
                    Button { state.captureScreenshot(.window) } label: { Image(systemName: "macwindow") }
                        .buttonStyle(GlassIconButtonStyle(size: 40))
                        .help("窗口截图提问 · \(state.shortcutLabel(.captureWindow))")
                    Button { state.forceAnswerCurrent() } label: {
                        Label("立即回答", systemImage: "bolt.fill")
                    }
                    .buttonStyle(PrimaryButtonStyle())
                    .help("回答最新的问题或转写，不等待自动判断 · \(state.shortcutLabel(.forceAnswer))")
                } }
            }
            PrivacyNote()
        }
        .padding(.horizontal, 14).padding(.top, 12).padding(.bottom, 10)
        .background(alignment: .bottom) { ScrollEdgeFade(edge: .bottom, height: 100).ignoresSafeArea() }
    }
}

/// Codex Fast mode, switchable mid-session (applies from the next answer).
private struct FastModeToggle: View {
    @Environment(AppState.self) private var state

    var body: some View {
        Button {
            state.codexFastMode.toggle()
            state.updateSettings()
        } label: {
            Label("Fast", systemImage: state.codexFastMode ? "hare.fill" : "hare")
        }
        .buttonStyle(ChipButtonStyle(prominent: state.codexFastMode))
        .help(state.codexFastMode ? "Codex Fast 模式已开启（\(state.codexFastLabel)）· 点按关闭" : "开启 Codex Fast 模式：\(state.codexFastLabel)")
    }
}

private struct QuestionComposer: View {
    @Environment(AppState.self) private var state

    var body: some View {
        @Bindable var state = state
        let placeholder = state.screenshotPreview == nil ? "输入问题，回车发送…" : "对截图的补充要求（可留空）…"
        HStack(spacing: 8) {
            TextField("", text: $state.questionInput, prompt: Text(placeholder).foregroundStyle(Color.secondary), axis: .vertical)
                .lineLimit(1...4)
                .textFieldStyle(.plain)
                .font(.system(size: 14))
                .foregroundStyle(Color.primary)
                .onSubmit { submit() }
            Button { submit() } label: {
                Image(systemName: "arrow.up").font(.system(size: 13, weight: .bold))
            }
            .buttonStyle(GlassIconButtonStyle(size: 28, tint: Brand.accent))
            .disabled(state.screenshotPreview == nil && state.questionInput.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty)
        }
        .padding(.leading, 16).padding(.trailing, 6).padding(.vertical, 6)
        .frame(minHeight: 40)
        .glassBackground(cornerRadius: 20)
    }

    private func submit() {
        if state.screenshotPreview == nil { state.answerInput() } else { state.answerScreenshot() }
    }
}

private struct PrivacyNote: View {
    @Environment(AppState.self) private var state
    var body: some View {
        HStack(spacing: 5) {
            Image(systemName: "lock.shield.fill").foregroundStyle(.green)
            Text(text).lineLimit(1)
        }
        .font(.system(size: 10.5))
        .foregroundStyle(.secondary)
    }

    private var text: String {
        let audio = state.transcriptionBackend == "cloud" ? "音频发送到 \(state.cloudProvider.title)" : "音频仅在本机识别，不保存"
        let answer = state.answerBackend == .openAICompatible ? "回答所需文本发送到所配置的 API" : "回答所需文本与截图交给 Codex"
        let record = state.recordSessions ? "转录与回答保存在本机记录" : "不保存记录"
        return [audio, answer, record].joined(separator: "；") + "。"
    }
}

extension AppState {
    /// Whether a transcript line was (part of) a detected question.
    func isQuestionText(_ text: String) -> Bool {
        questionTexts.contains { $0 == text || (text.count >= 4 && $0.count >= 4 && ($0.hasSuffix(text) || text.hasSuffix($0))) }
    }
}

private struct AnswerEvidenceDetails: View {
    let evidence: EvidenceBundle
    @State private var expanded = false
    private var summary: String {
        var parts = ["本次依据", "资料 \(evidence.materials.count)"]
        if evidence.screen != nil { parts.append(evidence.screenImageAttached ? "屏幕已附图" : "屏幕文字") }
        if !evidence.webSources.isEmpty { parts.append("联网 \(evidence.webSources.count)") }
        return parts.joined(separator: " · ")
    }
    var body: some View {
        VStack(alignment: .leading, spacing: 6) {
            if !evidence.notes.isEmpty {
                Button { expanded = true } label: {
                    Label("本次回答有 \(evidence.notes.count) 项限制 · 查看说明", systemImage: "exclamationmark.triangle")
                        .font(.system(size: 12)).foregroundStyle(.orange)
                        .fixedSize(horizontal: false, vertical: true)
                }.buttonStyle(.plain)
            }
            DisclosureGroup(summary, isExpanded: $expanded) {
                VStack(alignment: .leading, spacing: 8) {
                    Text(evidence.retrieval).foregroundStyle(.secondary)
                    ForEach(Array(evidence.materials.enumerated()), id: \.offset) { _, item in
                        DisclosureGroup(item.citation) {
                            Text(item.text).textSelection(.enabled).fixedSize(horizontal: false, vertical: true)
                                .frame(maxWidth: .infinity, alignment: .leading).padding(.vertical, 4)
                        }
                    }
                    if let screen = evidence.screen {
                        Label(screen.source + (evidence.screenImageAttached ? " · 已附图" : " · OCR 文字"), systemImage: "display")
                    }
                    if let search = evidence.search { Label(search.reason, systemImage: "globe") }
                    ForEach(evidence.webSources, id: \.url) { source in
                        if let url = URL(string: source.url) { Link(source.title, destination: url) }
                    }
                    ForEach(evidence.notes, id: \.self) { note in
                        Label(note, systemImage: "exclamationmark.triangle").foregroundStyle(.orange)
                    }
                }.frame(maxWidth: .infinity, alignment: .leading).padding(.top, 4)
            }.font(.system(size: 12))
        }
    }
}
