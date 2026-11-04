import AppKit
import SwiftUI
import InterviewHackerCore

extension AnswerBackend {
    /// The backend at launch: a saved choice is kept; a fresh install uses an API
    /// key unless Codex is already installed on this Mac.
    static func initialChoice() -> AnswerBackend {
        let installed = DemoMode.isEnabled || CodexBridge.executableURL() != nil
        let raw = AnswerBackendDefault.rawValue(saved: UserDefaults.standard.string(forKey: "answerBackend"), codexInstalled: installed)
        return AnswerBackend(rawValue: raw) ?? .codexAppServer
    }
}

// MARK: - Plain-language failures

/// What a failed request to the answer service means, in words a non-technical
/// person can act on. The rules live in `APIFailureExplainer` (Core).
enum AnswerServiceError {
    static func explain(_ error: Error) -> String {
        switch error {
        case let AnswerEngineError.http(code, body):
            // A 2xx status with an error body: the service accepted the call and then reported a problem.
            if (200 ..< 300).contains(code) {
                let detail = String(body.prefix(120)).trimmingCharacters(in: .whitespacesAndNewlines)
                return detail.isEmpty ? String(localized: "服务商返回了错误。") : String(localized: "服务商返回了错误：\(detail)")
            }
            return text(for: APIFailureExplainer.classify(status: code, body: body))
        case AnswerEngineError.timeout:
            return text(for: .timeout)
        case AnswerEngineError.invalidEndpoint:
            return text(for: .badAddress)
        case AnswerEngineError.missingKey:
            return String(localized: "还没有填写 Key。")
        case AnswerEngineError.empty:
            return String(localized: "服务连上了，但没有返回内容。检查模型名是否正确。")
        default:
            let nsError = error as NSError
            if nsError.domain == NSURLErrorDomain {
                return text(for: APIFailureExplainer.classify(status: nil, urlErrorCode: nsError.code))
            }
            return error.localizedDescription
        }
    }

    static func text(for failure: APIFailure) -> String {
        if case let .rejected(code) = failure { return String(localized: "服务商拒绝了这次请求（HTTP \(code)）。") }
        return String(localized: String.LocalizationValue(APIFailureExplainer.message(failure)))
    }
}

extension View {
    /// A text field on a surface with a hairline, radius 6, height 32.
    func inkField() -> some View {
        textFieldStyle(.plain)
            .font(Typo.body)
            .padding(.horizontal, Space.s3)
            .frame(height: Space.control)
            .background(Ink.surface, in: RoundedRectangle(cornerRadius: Space.radiusSmall, style: .continuous))
            .overlay(RoundedRectangle(cornerRadius: Space.radiusSmall, style: .continuous).strokeBorder(Ink.hairline, lineWidth: 1))
    }
}

// MARK: - Provider choice

@MainActor
extension AppState {
    /// Providers for the picker, the ones Chinese users reach for first at the front.
    static var apiProviderChoices: [(name: String, url: String)] {
        APIPreset.templates.enumerated()
            .sorted { (APIProviders.order(forBaseURL: $0.element.url), $0.offset) < (APIProviders.order(forBaseURL: $1.element.url), $1.offset) }
            .map { (name: $0.element.name, url: $0.element.url) }
    }

    /// The active API setup has no key and no model of the person's own (a model the
    /// app pre-filled for the provider does not count), so it can be turned into another provider.
    var apiSetupIsPristine: Bool {
        let model = apiModel.trimmingCharacters(in: .whitespacesAndNewlines)
        let prefilled = APIProviders.guide(forBaseURL: apiBaseURL)?.suggestedModel ?? ""
        return apiKey.isEmpty && (model.isEmpty || model == prefilled)
    }

    /// Picks a provider: reuses a saved preset for it, turns an untouched preset into
    /// it, or adds a new preset. Fills in the address and, when it is certain, a model.
    func chooseAPIProvider(name: String, baseURL: String) {
        let refs = apiPresets.map { APIProviderSwitch.PresetRef(id: $0.id, baseURL: $0.baseURL) }
        let suggested = APIProviders.guide(forBaseURL: baseURL)?.suggestedModel ?? ""
        switch APIProviderSwitch.plan(target: baseURL, presets: refs, activeID: activeAPIPresetID, activeIsPristine: apiSetupIsPristine) {
        case let .select(id):
            selectAPIPreset(id)
            if apiModel.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty, !suggested.isEmpty { apiModel = suggested }
        case .reuseActive:
            apiBaseURL = baseURL
            apiModel = suggested
            apiModelChoices = []
            apiModelsStatus = ""
            if let id = activeAPIPresetID { renameAPIPreset(id, to: name) }
        case .addNew:
            addAPIPreset(name: name, baseURL: baseURL)
            apiModel = suggested
        }
        engineTest = .idle
        saveAnswerSettings()
    }
}

// MARK: - API connection

/// Connect an OpenAI-compatible service: pick the provider, paste a key, press one
/// button. Shared by the first-run guide and Settings → 回答.
struct APIConnectView: View {
    @Environment(AppState.self) private var state
    /// nil until the person taps a provider; then the fields stay open.
    @State private var picked: Bool?
    @State private var keyDraft = ""
    @State private var replacingKey = false
    @State private var editingAddress = false
    @State private var note: String?
    @State private var awaitingModel = false
    @State private var fellBack = false
    @State private var checking = false

    private var isChosen: Bool { picked ?? !state.apiSetupIsPristine }
    private var isLocal: Bool { APIProviders.isLocal(baseURL: state.apiBaseURL) }
    private var guide: APIProviderGuide? { APIProviders.guide(forBaseURL: state.apiBaseURL) }
    private var hasStoredKey: Bool { !state.apiKey.isEmpty }
    private var showsKeyField: Bool { !hasStoredKey || replacingKey }
    private var selectedProviderURL: String? {
        let current = APIProviders.normalized(state.apiBaseURL)
        return AppState.apiProviderChoices.first { APIProviders.normalized($0.url) == current }?.url
    }

    var body: some View {
        VStack(alignment: .leading, spacing: Space.s4) {
            providerPicker
            if isChosen {
                addressRow
                if !isLocal { keyRow }
                modelRow
                actionRow
                footnote
                moreOptions
            }
        }
        .disabled(state.isAnswering)
    }

    // MARK: Provider

    private var providerPicker: some View {
        VStack(alignment: .leading, spacing: Space.s2) {
            Text("服务商").overline()
            FlowLayout(spacing: Space.s2, lineSpacing: Space.s2) {
                ForEach(AppState.apiProviderChoices, id: \.url) { provider in
                    Button {
                        pick(name: provider.name, url: provider.url)
                    } label: {
                        Text(LocalizedStringKey(provider.name))
                    }
                    .buttonStyle(ChipButtonStyle(prominent: isChosen && selectedProviderURL == provider.url))
                }
                Button {
                    pick(name: String(localized: "自定义"), url: "https://")
                } label: {
                    Text("其他")
                }
                .buttonStyle(ChipButtonStyle(prominent: isChosen && selectedProviderURL == nil))
            }
        }
    }

    private func pick(name: String, url: String) {
        picked = true
        keyDraft = ""
        replacingKey = false
        editingAddress = false
        note = nil
        awaitingModel = false
        fellBack = false
        state.chooseAPIProvider(name: name, baseURL: url)
    }

    @ViewBuilder private var addressRow: some View {
        @Bindable var state = state
        if selectedProviderURL == nil || editingAddress {
            TextField("API 地址，例如 https://api.openai.com/v1", text: $state.apiBaseURL)
                .inkField()
                .onChange(of: state.apiBaseURL) { _, _ in state.saveAnswerSettings(); note = nil }
        } else {
            HStack(spacing: Space.s2) {
                Text(verbatim: shortAddress).font(Typo.caption).foregroundStyle(Ink.secondary).lineLimit(1)
                Button("修改地址") { editingAddress = true }.buttonStyle(GhostButtonStyle(height: 24, horizontalPadding: 6))
                Spacer(minLength: 0)
            }
        }
    }

    private var shortAddress: String {
        state.apiBaseURL.replacingOccurrences(of: "https://", with: "").replacingOccurrences(of: "http://", with: "")
    }

    // MARK: Key

    private var keyRow: some View {
        VStack(alignment: .leading, spacing: Space.s2) {
            Text("API Key").overline()
            if showsKeyField {
                SecureField("粘贴 API Key", text: $keyDraft)
                    .inkField()
                    .onSubmit { run() }
            } else {
                HStack(spacing: Space.s2) {
                    Image(systemName: "key").font(.system(size: 12)).foregroundStyle(Ink.secondary)
                    Text(verbatim: APIKeyInput.masked(state.apiKey)).font(Typo.code).foregroundStyle(Ink.ink)
                    Spacer(minLength: 0)
                    Button("更换") { replacingKey = true; keyDraft = "" }.buttonStyle(GhostButtonStyle(height: 24, horizontalPadding: 6))
                }
                .padding(.horizontal, Space.s3)
                .frame(height: Space.control)
                .background(Ink.sunken, in: RoundedRectangle(cornerRadius: Space.radiusSmall, style: .continuous))
            }
        }
    }

    // MARK: Model

    private var modelRow: some View {
        @Bindable var state = state
        return VStack(alignment: .leading, spacing: Space.s2) {
            Text("模型").overline()
            HStack(spacing: Space.s2) {
                TextField("模型名", text: $state.apiModel)
                    .inkField()
                    .onChange(of: state.apiModel) { _, _ in state.saveAnswerSettings() }
                    .onSubmit { run() }
                if !state.apiModelChoices.isEmpty {
                    Menu("选择") {
                        ForEach(state.apiModelChoices, id: \.self) { id in
                            Button(id) { chooseModel(id) }
                        }
                    }
                    .menuStyle(.button).buttonStyle(SecondaryButtonStyle()).fixedSize()
                }
                Button("获取列表") { Task { await state.fetchAPIModels() } }
                    .buttonStyle(SecondaryButtonStyle())
                    .help("从服务商读取可用的模型")
            }
            if let hint = guide?.modelHint, state.apiModel.trimmingCharacters(in: .whitespaces).isEmpty {
                Text(LocalizedStringKey(hint)).font(Typo.caption).foregroundStyle(Ink.secondary)
            } else if !state.apiModelsStatus.isEmpty {
                Text(LocalizedStringKey(state.apiModelsStatus)).font(Typo.caption).foregroundStyle(Ink.secondary)
            }
        }
    }

    private func chooseModel(_ id: String) {
        state.apiModel = id
        state.saveAnswerSettings()
        if awaitingModel { run() }
    }

    // MARK: Check

    private var primaryTitle: LocalizedStringKey {
        if isLocal { return "检查连接" }
        if showsKeyField { return keyDraft.isEmpty ? "粘贴并检查" : "保存并检查" }
        return "重新检查"
    }

    private var actionRow: some View {
        VStack(alignment: .leading, spacing: Space.s2) {
            HStack(spacing: Space.s3) {
                Button { run() } label: {
                    HStack(spacing: 6) {
                        if checking || state.engineTest == .running { ProgressView().controlSize(.small) }
                        Text(primaryTitle)
                    }
                }
                .buttonStyle(PrimaryButtonStyle())
                .disabled(checking || state.engineTest == .running)
                result
                Spacer(minLength: 0)
            }
        }
    }

    @ViewBuilder private var result: some View {
        if let note {
            statusLine(.caution) { Text(verbatim: note) }
        } else {
            switch state.engineTest {
            case .running:
                Text("正在检查…").font(Typo.caption).foregroundStyle(Ink.secondary)
            case let .passed(seconds, _):
                statusLine(.ready) { Text("已连接 · 用时 \(seconds, specifier: "%.1f") 秒") }
            case let .failed(message):
                statusLine(.caution) { Text(verbatim: message) }
            case .idle:
                if state.answerBackend == .openAICompatible, state.answerEngineReady {
                    statusLine(.idle) { Text("已保存 · 点“重新检查”确认可用") }
                }
            }
        }
    }

    private func statusLine<Line: View>(_ tone: StatusDot.Tone, @ViewBuilder _ line: () -> Line) -> some View {
        HStack(alignment: .firstTextBaseline, spacing: Space.s2) {
            StatusDot(tone: tone)
            line().font(Typo.caption).foregroundStyle(tone == .caution ? Ink.caution : Ink.secondary)
                .fixedSize(horizontal: false, vertical: true)
        }
    }

    private func run() {
        guard !checking else { return }
        Task { await saveAndCheck() }
    }

    /// Stores the key, switches to this service, and sends the shortest real request
    /// through it. A service that does not work never replaces a Codex setup that does.
    @MainActor private func saveAndCheck() async {
        checking = true
        defer { checking = false }
        note = nil
        fellBack = false
        awaitingModel = false
        if !isLocal, showsKeyField {
            let source = keyDraft.isEmpty ? (NSPasteboard.general.string(forType: .string) ?? "") : keyDraft
            guard let key = APIKeyInput.clean(source) else {
                note = keyDraft.isEmpty
                    ? String(localized: "剪贴板里没有看起来像 Key 的内容。先到服务商后台复制 Key。")
                    : String(localized: "这看起来不像一个 Key。检查是否复制完整，不要带空格或换行。")
                return
            }
            state.apiKey = key
            state.saveAPIKey()
            keyDraft = ""
            replacingKey = false
        }
        let previous = state.answerBackend
        let fallback: AnswerBackend? = previous != .openAICompatible && state.answerEngineReady ? previous : nil
        state.selectAnswerBackend(.openAICompatible)
        var passed = false
        if state.apiModel.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty {
            // No model yet: the list also proves the key works.
            await state.fetchAPIModels()
            if state.apiModelChoices.isEmpty {
                // The list failed: its status says why (wrong key, wrong address, no list at all).
                note = state.apiModelsStatus.isEmpty
                    ? String(localized: "还不知道该用哪个模型。请填写模型名。")
                    : String(localized: String.LocalizationValue(state.apiModelsStatus))
            } else {
                note = String(localized: "Key 可以用。再选一个模型就好。")
                awaitingModel = true
            }
        } else {
            await state.testAnswerEngine()
            if case .passed = state.engineTest {
                passed = true
            } else if state.apiModelChoices.isEmpty, case let .failed(message) = state.engineTest,
                      message == AnswerServiceError.text(for: .badModel) {
                await state.fetchAPIModels()
                awaitingModel = !state.apiModelChoices.isEmpty
            }
        }
        if !passed, let fallback {
            state.answerBackend = fallback
            state.saveAnswerSettings()
            fellBack = true
        }
    }

    // MARK: Footnote and more

    private var footnote: some View {
        VStack(alignment: .leading, spacing: Space.s2) {
            HStack(spacing: Space.s2) {
                Image(systemName: "lock").font(.system(size: 11))
                Text(LocalizedStringKey(isLocal ? "这是本机上的服务，数据不会离开这台 Mac。" : "Key 只存在这台 Mac 的钥匙串里，只发给你选的服务商。"))
            }
            .font(Typo.caption).foregroundStyle(Ink.secondary)
            if !isLocal {
                Text("回答时，转写片段、检索到的资料和截图会发送到这个服务。")
                    .font(Typo.caption).foregroundStyle(Ink.secondary)
            }
            if fellBack {
                Text("没有改动：回答仍然用 Codex。").font(Typo.caption).foregroundStyle(Ink.secondary)
            } else if state.answerBackend != .openAICompatible {
                Text("现在回答用的是 Codex。检查通过后会改用这个服务；没通过则保持不变。")
                    .font(Typo.caption).foregroundStyle(Ink.secondary)
            }
            if let page = guide?.keyPageURL, let url = URL(string: page), !isLocal {
                Link(destination: url) {
                    HStack(spacing: 4) {
                        Text("去哪里拿 Key")
                        Image(systemName: "arrow.up.right").font(.system(size: 10, weight: .semibold))
                    }
                    .font(Typo.label)
                }
                .foregroundStyle(Ink.ink)
            }
        }
        .fixedSize(horizontal: false, vertical: true)
    }

    private var moreOptions: some View {
        DisclosureGroup("更多选项") {
            VStack(alignment: .leading, spacing: Space.s3) {
                APIEffortRow()
                APIPresetManager()
            }
            .padding(.top, Space.s2)
        }
        .font(Typo.label)
    }
}

/// Thinking strength of the API service; rarely changed.
private struct APIEffortRow: View {
    @Environment(AppState.self) private var state

    var body: some View {
        @Bindable var state = state
        HStack(spacing: Space.s2) {
            MenuPicker(title: "思考强度", selection: $state.apiReasoningEffort, options: APIPreset.efforts, localizesOptions: true)
                .onChange(of: state.apiReasoningEffort) { _, _ in state.saveAnswerSettings() }
            Note("仅对服务商支持的推理模型有效；不确定时保持默认。", presentation: .info)
                .accessibilityLabel("思考强度说明")
            Spacer(minLength: 0)
        }
    }
}

/// Saved providers: switch between them, rename or delete one. Each keeps its own
/// address, key, model and thinking strength; the key is removed with the preset.
private struct APIPresetManager: View {
    @Environment(AppState.self) private var state
    @State private var renaming = false
    @State private var newName = ""
    @State private var confirmDelete = false

    var body: some View {
        if !state.simplifiedInterface || state.apiPresets.count > 1 {
            HStack(spacing: Space.s2) {
                Text("预设").font(Typo.label).foregroundStyle(Ink.secondary)
                Menu {
                    ForEach(state.apiPresets) { preset in
                        Button { state.selectAPIPreset(preset.id) } label: {
                            if preset.id == state.activeAPIPresetID { Label(preset.name, systemImage: "checkmark") } else { Text(verbatim: preset.name) }
                        }
                    }
                } label: {
                    Label { Text(verbatim: state.activeAPIPreset?.name ?? String(localized: "选择预设")) } icon: { Image(systemName: "server.rack") }
                }
                .menuStyle(.button).buttonStyle(SecondaryButtonStyle()).fixedSize()
                if !state.simplifiedInterface {
                    Button("重命名") { newName = state.activeAPIPreset?.name ?? ""; renaming = true }.buttonStyle(ChipButtonStyle())
                    Button("删除") { confirmDelete = true }.buttonStyle(ChipButtonStyle()).disabled(state.apiPresets.count <= 1)
                }
                Spacer(minLength: 0)
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
    }
}

// MARK: - Codex (secondary)

/// Codex for people who already have it: status, model, thinking strength.
struct CodexEngineSection: View {
    @Environment(AppState.self) private var state

    var body: some View {
        @Bindable var state = state
        VStack(alignment: .leading, spacing: Space.s3) {
            HStack(spacing: Space.s3) {
                StatusDot(tone: state.codexReady ? .ready : .caution)
                VStack(alignment: .leading, spacing: 2) {
                    Text(LocalizedStringKey(state.checkingCodexStatus ? "正在检测 Codex…" : state.codexReady ? "Codex 已登录（ChatGPT 账号）" : state.codexInstalled ? "需要登录 Codex" : "未检测到 Codex"))
                        .font(Typo.label).lineLimit(2)
                    if !state.simplifiedInterface {
                        Text("\(state.codexLoginStatus) · \(state.codexVersion)").font(Typo.caption).foregroundStyle(Ink.secondary)
                    }
                }
                Spacer()
                Button { Task { await state.refreshCodexStatus() } } label: { Image(systemName: "arrow.clockwise") }
                    .buttonStyle(GlassIconButtonStyle()).help("重新检测 Codex 登录状态和可用模型")
            }
            if !state.checkingCodexStatus && !state.codexInstalled {
                Note("未检测到 Codex：安装 ChatGPT / Codex 桌面客户端，或在终端运行 `npm i -g @openai/codex`，然后 `codex login` 用 ChatGPT 账号登录，再点右侧刷新。", color: Ink.caution)
            } else if !state.checkingCodexStatus && !state.codexReady {
                Note("在终端运行 codex login，用 ChatGPT 账号完成登录，再点右侧刷新。已有桌面登录仍显示未连接时，也可用这条命令连接本机 CLI。", color: Ink.caution)
            }
            MenuPicker(title: "模型", selection: $state.codexModelID, options: codexModelOptions, placeholder: "请选择模型")
                .disabled(state.isAnswering)
                .onChange(of: state.codexModelID) { _, _ in state.normalizeCodexSelection() }
            if state.codexModels.isEmpty {
                TextField("模型 ID（与 Codex 中一致）", text: $state.codexModelID)
                    .inkField().disabled(state.isAnswering)
                    .onChange(of: state.codexModelID) { _, _ in state.updateSettings() }
            }
            AdvancedOptions("answers-codex") {
                CodexReasoningPicker()
                CodexFastModeToggle()
            }
            if state.answerBackend == .openAICompatible {
                Button("改用 Codex") { state.selectAnswerBackend(.codexAppServer) }
                    .buttonStyle(SecondaryButtonStyle())
                    .disabled(!state.codexReady || state.isAnswering)
            } else {
                EngineTestRow()
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

// MARK: - The form

/// How answers are produced: an API key first, Codex tucked away for people who
/// already use it. Shared by settings and the first-run guide.
struct AnswerEngineForm: View {
    @Environment(AppState.self) private var state
    /// Kept for source compatibility: the extra options are always folded away now.
    var foldSecondary = false
    @State private var codexExpanded: Bool?

    var body: some View {
        VStack(alignment: .leading, spacing: Space.s4) {
            APIConnectView()
            Rectangle().fill(Ink.hairline).frame(height: 1)
            DisclosureGroup(isExpanded: Binding(get: { codexExpanded ?? (state.answerBackend != .openAICompatible) },
                                                set: { codexExpanded = $0 })) {
                CodexEngineSection().padding(.top, Space.s3)
            } label: {
                HStack(spacing: Space.s2) {
                    Text("使用 Codex（已有 ChatGPT 账号）")
                    if state.answerBackend != .openAICompatible {
                        Text("当前使用").font(Typo.caption).foregroundStyle(Ink.secondary)
                    }
                }
                .font(Typo.label)
            }
        }
    }
}
