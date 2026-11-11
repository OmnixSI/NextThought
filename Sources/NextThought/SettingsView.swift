import AppKit
import AVFoundation
import SwiftUI
import NextThoughtCore

/// One sidebar page. Each is a single scroll of sections; nothing nests below it.
enum SettingsPage: String, CaseIterable, Identifiable {
    case scenes = "场景", sound = "声音", answers = "回答", materials = "资料"
    case presenter = "提词与教练", notes = "纪要", general = "通用", data = "数据与权限"
    var id: String { rawValue }
    var title: String { rawValue }
    var symbol: String {
        switch self {
        case .scenes: "square.grid.2x2"
        case .sound: "waveform"
        case .answers: "sparkles"
        case .materials: "doc.text"
        case .presenter: "rectangle.topthird.inset.filled"
        case .notes: "doc.plaintext"
        case .general: "gearshape"
        case .data: "lock.shield"
        }
    }
    var summary: String {
        switch self {
        case .scenes: "每种会议开始时怎么设置：会中、回答、纪要模板、提词"
        case .sound: "会议声音 · 麦克风 · 语音识别 · 术语和人名"
        case .answers: "回答服务 · 联网搜索 · 屏幕与讲稿 · 技能"
        case .materials: "怎样读取和查找你的资料"
        case .presenter: "提词岛、临场教练、屏幕共享和字幕条"
        case .notes: "会后纪要的模型、模板和收尾方式"
        case .general: "语言、外观、快捷键、手机伴侣和更新"
        case .data: "会议记录、备份、系统权限和维护"
        }
    }
    /// Accepts a page name or any name stored by older versions.
    init(stored: String) {
        self = SettingsPage(rawValue: stored) ?? SettingsGroup(stored: stored).page
    }
}

/// Destinations used by earlier versions (and by code that opens Settings). Each one
/// now opens a page, and a section on it where the old destination was one.
enum SettingsGroup: String, CaseIterable, Identifiable {
    case essentials = "基础", recognition = "识别", answers = "回答", notes = "纪要", documents = "资料", overlay = "提词器", skills = "技能", recording = "数据", more = "更多", shortcuts = "快捷键", companion = "手机伴侣", privacy = "权限与维护"
    case screen = "屏幕与讲稿"
    var id: String { rawValue }
    var page: SettingsPage {
        switch self {
        case .essentials, .more, .shortcuts, .companion: .general
        case .recognition: .sound
        case .answers, .skills, .screen: .answers
        case .notes: .notes
        case .documents: .materials
        case .overlay: .presenter
        case .recording, .privacy: .data
        }
    }
    /// The section the old destination pointed at, when it was not the top of its page.
    var anchor: String? {
        switch self {
        case .skills: "技能"
        case .screen: "屏幕与讲稿"
        case .more: "更新"
        case .shortcuts: "快捷键"
        case .companion: "手机伴侣"
        case .privacy: "权限"
        default: nil
        }
    }
    /// Accepts names stored by older versions (常规 / 更新 / 快捷键).
    init(stored: String) {
        switch stored {
        case "常规": self = .essentials
        case "更新": self = .more
        case "快捷键": self = .shortcuts
        default: self = SettingsGroup(rawValue: stored) ?? .essentials
        }
    }
}

/// Lets a row in one page send the reader to a section of another (“去打开”).
private struct SettingsNavigateKey: EnvironmentKey { static let defaultValue: (SettingsPage, String?) -> Void = { _, _ in } }
extension EnvironmentValues {
    var settingsNavigate: (SettingsPage, String?) -> Void {
        get { self[SettingsNavigateKey.self] }
        set { self[SettingsNavigateKey.self] = newValue }
    }
}

struct SettingsView: View {
    @Environment(AppState.self) private var state
    @AppStorage("settingsGroup", store: AppPreferences.standard) private var groupRaw = SettingsPage.scenes.rawValue
    @State private var query = ""
    @State private var searchTarget: SettingsSearchItem?
    @State private var searchNavigationRevision = UUID()
    @State private var highlightTarget: String?
    @FocusState private var searchFocused: Bool
    private var page: SettingsPage { SettingsPage(stored: groupRaw) }
    private var searching: Bool { !query.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty }
    private var searchItems: [SettingsSearchItem] { SettingsSearchItem.matching(query) }

    var body: some View {
        HStack(spacing: 0) {
            sidebar.frame(width: 214)
            Divider()
            VStack(alignment: .leading, spacing: 0) {
                header
                Divider().padding(.horizontal, 28)
                if let message = state.errorMessage {
                    HStack(alignment: .top, spacing: 10) {
                        Image(systemName: "exclamationmark.triangle").foregroundStyle(Ink.caution)
                        Text(message).font(Typo.caption).textSelection(.enabled)
                        Spacer(minLength: 0)
                        Button { state.closeError() } label: { Image(systemName: "xmark") }
                            .buttonStyle(.borderless).accessibilityLabel("关闭提示")
                    }.padding(16).background(Ink.sunken).padding(.horizontal, 28).padding(.top, 16)
                }
                detail
            }
            .frame(maxWidth: .infinity, maxHeight: .infinity, alignment: .topLeading)
        }
        .background(Ink.paper)
        .foregroundStyle(Ink.ink)
        .tint(Brand.accent)
        .environment(\.settingsNativeControls, true)
        .environment(\.settingsNavigate) { page, anchor in
            groupRaw = page.rawValue
            searchTarget = anchor.map { SettingsSearchItem(page, $0, "") }
            highlightTarget = anchor
            searchNavigationRevision = UUID()
        }
        .background {
            Button("") { searchFocused = true }
                .keyboardShortcut("f", modifiers: .command).hidden().accessibilityHidden(true)
        }
        .onChange(of: query) { _, next in
            if next.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty {
                searchTarget = nil; highlightTarget = nil; searchNavigationRevision = UUID()
            }
        }
        .onAppear { followLegacyDestination() }
        .onChange(of: groupRaw) { _, _ in
            // External deep links must not remain behind stale search results.
            if searchTarget?.page != page { query = ""; searchTarget = nil; highlightTarget = nil }
            followLegacyDestination()
        }
    }

    /// A deep link written with an old destination name lands on its section.
    private func followLegacyDestination() {
        guard let anchor = SettingsGroup(rawValue: groupRaw)?.anchor, searchTarget == nil else { return }
        searchTarget = SettingsSearchItem(page, anchor, "")
        highlightTarget = anchor
        searchNavigationRevision = UUID()
    }

    private var sidebar: some View {
        VStack(alignment: .leading, spacing: 16) {
            Text("设置").font(.system(size: 20, weight: .semibold)).padding(.horizontal, 20).padding(.top, 22)
            HStack(spacing: 7) {
                Image(systemName: "magnifyingglass").foregroundStyle(Ink.secondary).accessibilityHidden(true)
                TextField("搜索设置", text: $query).textFieldStyle(.plain).focused($searchFocused)
                    .onExitCommand { query = ""; searchTarget = nil; searchFocused = false }
                    .accessibilityIdentifier("settings.search")
                if !query.isEmpty {
                    Button { query = ""; searchTarget = nil; highlightTarget = nil; searchNavigationRevision = UUID(); searchFocused = true } label: { Image(systemName: "xmark.circle.fill") }
                        .buttonStyle(.plain).foregroundStyle(Ink.secondary).accessibilityLabel("清除搜索")
                }
            }
            .font(Typo.caption).padding(9)
            .background(Ink.surface, in: RoundedRectangle(cornerRadius: 8))
            .overlay(RoundedRectangle(cornerRadius: 8).strokeBorder(searchFocused ? Ink.focus : Ink.hairline, lineWidth: searchFocused ? 2 : 1))
            .padding(.horizontal, 12)
            if searching {
                ScrollView {
                    LazyVStack(alignment: .leading, spacing: 4) {
                        if searchItems.isEmpty {
                            VStack(alignment: .leading, spacing: 8) {
                                Text("没有匹配的设置项").font(Typo.label)
                                Text("试试麦克风、模板或备份").font(Typo.caption).foregroundStyle(Ink.secondary)
                            }.padding(12)
                        }
                        ForEach(searchItems) { item in
                            Button {
                                let target: SettingsSearchItem
                                if item.page == .sound, item.title == "流式模型与整句精修", state.transcriptionBackend != "zipformer" {
                                    target = SettingsSearchItem(item.page, item.title, anchor: "语音识别", item.words)
                                } else { target = item }
                                searchTarget = target
                                groupRaw = item.page.rawValue
                                highlightTarget = target.anchor
                                searchNavigationRevision = UUID()
                            } label: {
                                VStack(alignment: .leading, spacing: 4) {
                                    Text(LocalizedStringKey(item.title)).font(Typo.label)
                                    Text(LocalizedStringKey(item.page.title)).font(Typo.caption).foregroundStyle(Ink.secondary)
                                }.frame(maxWidth: .infinity, alignment: .leading).padding(10)
                                    .background(searchTarget?.id == item.id ? Ink.hover : Color.clear, in: RoundedRectangle(cornerRadius: 7))
                                    .contentShape(Rectangle())
                            }.buttonStyle(.plain).accessibilityIdentifier("settings.result." + item.id)
                        }
                    }.padding(.horizontal, 8)
                }.accessibilityLabel("设置搜索结果")
            } else {
                List(selection: Binding<SettingsPage?>(get: { page }, set: { next in
                    guard let next, next != page else { return }
                    groupRaw = next.rawValue
                    searchTarget = nil; highlightTarget = nil
                })) {
                    ForEach(SettingsPage.allCases) { item in
                        Label {
                            Text(LocalizedStringKey(item.title)).font(Typo.label)
                        } icon: {
                            Image(systemName: item.symbol).font(.system(size: 15)).frame(width: 20)
                        }
                        .padding(.vertical, 6)
                        .tag(item)
                        .accessibilityIdentifier("settings.page." + item.rawValue)
                    }
                }.listStyle(.sidebar).scrollContentBackground(.hidden)
            }
            Spacer(minLength: 0)
            Text("NextThought").font(Typo.caption).foregroundStyle(Ink.secondary)
                .padding(.horizontal, 20).padding(.bottom, 18)
        }
        .background(Ink.sunken)
    }

    private var header: some View {
        VStack(alignment: .leading, spacing: 6) {
            Text(LocalizedStringKey(page.title)).font(.system(size: 23, weight: .semibold))
                .accessibilityAddTraits(.isHeader)
            Text(LocalizedStringKey(page.summary)).font(Typo.caption).foregroundStyle(Ink.secondary)
                .fixedSize(horizontal: false, vertical: true)
        }
        .padding(.horizontal, 28).padding(.top, 24).padding(.bottom, 18)
        .frame(maxWidth: .infinity, alignment: .leading)
    }

    private var detail: some View {
        ScrollViewReader { scroll in
            ScrollView {
                SettingsPageContent(page: page)
                    .frame(maxWidth: 760, alignment: .leading)
                    .frame(maxWidth: .infinity, alignment: .center)
                    .padding(28)
            }
            .id(page)
            .environment(\.settingsSearchHighlight, highlightTarget)
            .environment(\.settingsSearchDestination, searchTarget?.anchor)
            .task(id: searchNavigationRevision) {
                guard let target = searchTarget else { return }
                try? await Task.sleep(for: .milliseconds(180))
                guard !Task.isCancelled else { return }
                withAnimation(Motion.expand) { scroll.scrollTo(target.anchor, anchor: .top) }
                try? await Task.sleep(for: .seconds(3))
                if !Task.isCancelled { highlightTarget = nil }
            }
        }
    }
}

private struct SettingsPageContent: View {
    let page: SettingsPage

    @ViewBuilder var body: some View {
        switch page {
        case .scenes: SceneSettingsPage()
        case .sound: SoundSettings()
        case .answers: AnswerSettings()
        case .materials: DocumentModelSettings()
        case .presenter: PresenterSettings()
        case .notes: NotesSettingsView()
        case .general: GeneralSettings()
        case .data: DataSettings()
        }
    }
}

/// Card with an icon title, shared by every settings page. The title is also the
/// scroll anchor that search results and deep links point at.
struct SettingsSection<Content: View>: View {
    let title: String
    let icon: String
    @Environment(\.settingsSearchHighlight) private var searchHighlight
    @ViewBuilder var content: Content

    var body: some View {
        VStack(alignment: .leading, spacing: 16) {
            HStack(spacing: 10) {
                Image(systemName: icon)
                    .font(.system(size: 13, weight: .regular)).foregroundStyle(Ink.secondary)
                    .frame(width: 18)
                Text(LocalizedStringKey(title)).font(.system(size: 15, weight: .semibold))
            }
            content
        }
        .font(.system(size: 13))
        .card()
        .id(title)
        .overlay(RoundedRectangle(cornerRadius: Space.radiusMedium).stroke(searchHighlight == title ? Ink.focus : Color.clear, lineWidth: 2))
        .animation(Motion.hover, value: searchHighlight == title)
    }
}

/// One collapsed group per page (or section) for controls that are rarely changed.
/// Search opens it when a result points inside (`advanced:<id>`).
struct AdvancedOptions<Content: View>: View {
    @Environment(\.settingsSearchDestination) private var searchDestination
    @Environment(\.settingsSearchHighlight) private var searchHighlight
    private let content: Content
    private let id: String
    private let title: String
    private let carded: Bool
    @State private var searchExpanded = false
    @AppStorage private var expanded: Bool
    /// `card` wraps a page-level group in the same card the sections use.
    init(_ id: String, title: String = "更多选项", card: Bool = false, @ViewBuilder content: () -> Content) {
        self.content = content()
        self.id = id
        self.title = title
        self.carded = card
        self._expanded = AppStorage(wrappedValue: false, "settings.expanded." + id, store: AppPreferences.standard)
    }
    private var anchor: String { "advanced:" + id }

    private var disclosure: some View {
        DisclosureGroup(LocalizedStringKey(title), isExpanded: Binding(
            get: { searchDestination == nil ? expanded : searchExpanded },
            set: { if searchDestination != nil { searchExpanded = $0 } else { expanded = $0 } }
        )) {
            VStack(alignment: .leading, spacing: Space.s3) { content }.padding(.top, Space.s3)
        }
        .font(Typo.label)
    }

    var body: some View {
        Group {
            if carded { disclosure.card() } else { disclosure }
        }
        .id(anchor)
        .overlay(RoundedRectangle(cornerRadius: Space.radiusMedium).stroke(searchHighlight == anchor ? Ink.focus : Color.clear, lineWidth: 2))
        .onChange(of: searchDestination, initial: true) { _, target in
            searchExpanded = target.map { expanded || $0 == anchor } ?? false
        }
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
                Text(LocalizedStringKey(text)).font(.system(size: 12)).foregroundStyle(color).fixedSize(horizontal: false, vertical: true)
            } else {
                Button { showingInfo.toggle() } label: {
                    Image(systemName: "info.circle")
                        .font(.system(size: 12))
                        .foregroundStyle(color)
                        .frame(width: Hit.icon, height: Hit.icon)
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
            Text("会使用此 Mac 上绑定的本机签名身份，重新签名后重启应用。重启后请再允许一次录屏与麦克风，之后更新会继续使用该身份。")
        }
    }
}

struct CompanionSection: View {
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
                    if let url = try? CompanionServer.shared.pairing.url(base: URL(string: "nextthought-companion://pair")!) {
                        pairingCard(url.absoluteString, title: "用手机 App 扫描", detail: "打开 NextThought 手机 App，选择“扫描二维码”。", copyTitle: "复制 App 配对链接")
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
            AdvancedOptions("general-companion", title: "备用连接与高级选项") { advancedControls }
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
            TextField("HTTPS 中转服务根地址", text: $state.companionRelayURL).textFieldStyle(PaperTextFieldStyle())
            Toggle("启用互联网加密备用连接", isOn: Binding(get: { state.companionRelayEnabled }, set: { state.setCompanionRelay(enabled: $0) })).disabled(!state.companionEnabled)
            if !state.companionRelayEnabled { Text(state.companionRelayStatus).font(.system(size: 12)).foregroundStyle(.secondary) }
            Divider()
            HStack(spacing: 8) {
                Text("首选端口").font(.system(size: 12))
                TextField("8765", value: $state.companionPort, format: .number.grouping(.never)).textFieldStyle(PaperTextFieldStyle()).frame(width: 80)
                Button("重新生成链接") { state.setCompanion(enabled: true); copiedLink = nil }.buttonStyle(ChipButtonStyle()).disabled(!state.companionEnabled)
            }
            Note("重新生成链接后，手机需要重新配对。", presentation: .inline)
        }
    }
}


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

struct LanguagePicker: View {
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
            .textFieldStyle(PaperTextFieldStyle())
            .onChange(of: state.cloudModel) { _, _ in state.saveAnswerSettings() }
        SecureField("\(state.cloudProvider.rawValue.capitalized) API Key（保存在系统钥匙串）", text: $state.cloudKey)
            .textFieldStyle(PaperTextFieldStyle())
            .onChange(of: state.cloudKey) { _, _ in state.saveCloudKey() }
        Toggle("我了解：会议音频会实时上传到所选服务商并按其条款处理", isOn: $state.cloudConsent)
            .onChange(of: state.cloudConsent) { _, _ in state.saveAnswerSettings() }
        Note("云端识别会把会议音频发送给所选服务。请先确认参与者同意录音和转写；Soniox 支持中英混说，Deepgram 需指定单一语言。", presentation: .inline)
    }
}


struct CodexReasoningPicker: View {
    @Environment(AppState.self) private var state
    var body: some View {
        @Bindable var state = state
        MenuPicker(title: "推理强度", selection: $state.codexReasoning, options: state.codexEfforts.map { ($0, $0) })
            .disabled(state.isAnswering).onChange(of: state.codexReasoning) { _, _ in state.updateSettings() }
    }
}

/// Fast mode of the Codex engine and the notes that explain it.
struct CodexFastModeToggle: View {
    @Environment(AppState.self) private var state
    var body: some View {
        @Bindable var state = state
        Toggle(isOn: $state.codexFastMode) {
            HStack(spacing: 6) {
                Image(systemName: "bolt.fill").foregroundStyle(state.codexFastMode ? Brand.accent : .secondary)
                Text("快速模式")
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
                        Note("这种搜索方式需要 Codex 并用 ChatGPT 账号登录。只用 API Key 时，请把“搜索方式”改成 Tavily。", color: .orange)
                    } else {
                        Note("联网搜索会把问题发送给 Codex；搜索结果会按当前场景整理成回答。", presentation: .inline)
                    }
                case .tavily:
                    HStack {
                        SecureField("Tavily API Key（保存在系统钥匙串）", text: $state.tavilyKey)
                            .textFieldStyle(PaperTextFieldStyle())
                            .onChange(of: state.tavilyKey) { _, _ in state.saveTavilyKey() }
                        Link("获取 Key", destination: URL(string: "https://app.tavily.com")!).font(.system(size: 12))
                    }
                    Note("问题文本会发送给 Tavily；它返回的网页摘要由当前回答服务整理，并在回答中标注来源。", presentation: .inline)
                }
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
                BusyActionLabel(title: "测试回答", busyTitle: "正在测试…", busy: state.engineTest == .running)
            }
            .buttonStyle(SecondaryButtonStyle())
            .controlBusy(state.engineTest == .running)
            .disabled(state.engineTest == .running || state.isAnswering)
            switch state.engineTest {
            case .idle: Text("发一句测试问题，确认回答服务能正常回答").font(.system(size: 11)).foregroundStyle(.secondary)
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

struct SkillSettings: View {
    @Environment(AppState.self) private var state
    @State private var editing: Skill?
    @State private var deleting: Skill?
    private var library: SkillLibrary { SkillLibrary.shared }

    var body: some View {
        SettingsSection(title: "技能", icon: "wand.and.stars") {
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
                if !skill.global { Text(attached == 0 ? "还没有系列使用" : "\(attached) 个系列在用").font(.system(size: 10.5)).foregroundStyle(.tertiary) }
            }
            Spacer()
            Toggle("所有系列", isOn: Binding(get: { skill.global }, set: { var next = skill; next.global = $0; library.update(next) }))
                .toggleStyle(.inline)
            Toggle("启用", isOn: Binding(get: { skill.enabled }, set: { var next = skill; next.enabled = $0; library.update(next) }))
                .toggleStyle(.inline)
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
            TextField("名称", text: $skill.name).textFieldStyle(PaperTextFieldStyle())
            TextField("一句话说明（可选）", text: $skill.summary).textFieldStyle(PaperTextFieldStyle())
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


// MARK: - Updates

struct UpdateSettings: View {
    @Environment(AppState.self) private var state
    private var updater: Updater { Updater.shared }

    var body: some View {
        @Bindable var updater = Updater.shared
        SettingsSection(title: "更新", icon: "arrow.triangle.2.circlepath") {
            HStack(alignment: .center, spacing: 20) {
                VStack(alignment: .leading, spacing: 6) {
                    HStack(alignment: .firstTextBaseline, spacing: 10) {
                        Text(verbatim: "NextThought " + updater.currentVersion).font(.system(size: 15, weight: .medium))
                        Text("构建 \(String(updater.currentBuild))").font(Typo.caption).foregroundStyle(Ink.secondary)
                    }
                    Text(statusText).font(Typo.caption).foregroundStyle(statusColor)
                        .fixedSize(horizontal: false, vertical: true)
                }
                Spacer(minLength: 8)
                updateAction
            }
            if case let .downloading(fraction) = updater.phase {
                ProgressView(value: fraction).controlSize(.small).accessibilityLabel("更新下载进度")
            }
            if let note = updater.stagedNote { Note(note, color: Ink.caution) }
            if state.isRunning, updater.phase == .ready { Note("记录结束后即可安装更新。", color: Ink.caution) }
            Divider()
            MenuPicker(title: "更新渠道", selection: $updater.channel,
                       options: [(.stable, String(localized: "正式版本")), (.main, String(localized: "预览版本"))])
                .onChange(of: updater.channel) { _, _ in
                    updater.saveSettings()
                    Task { await updater.check(userInitiated: true) }
                }
            if updater.channel == .main { Note("预览版本可能包含尚未正式发布的功能。", color: Ink.caution) }
            Divider()
            Toggle("自动检查更新", isOn: $updater.automaticChecks)
                .onChange(of: updater.automaticChecks) { _, _ in updater.saveSettings() }
            Note("启动后及每 6 小时检查；有新版本时在后台下载。")
            Note("更新前会自动备份，并校验安装包。")
        }
        // Show essential access help after an error; otherwise use the global expert switch.
        if updater.needsAccessHelp {
            GitHubAccessSection()
        } else {
            AdvancedOptions("general-updates", title: "GitHub 访问") { GitHubAccessSection() }
        }
    }

    @ViewBuilder private var updateAction: some View {
        switch updater.phase {
        case .ready:
            Button("重启并安装") { updater.installAndRelaunch() }
                .buttonStyle(PrimaryButtonStyle()).disabled(state.isRunning || !updater.canInstall)
        case .available:
            Button("下载更新") { updater.downloadUpdate() }.buttonStyle(PrimaryButtonStyle())
        case .checking, .downloading, .installing:
            Button {} label: {
                BusyActionLabel(title: "检查更新", busyTitle: "正在处理…", busy: true)
            }.buttonStyle(SecondaryButtonStyle()).controlBusy(true).disabled(true)
        default:
            Button { Task { await updater.check(userInitiated: true) } } label: {
                BusyActionLabel(title: "检查更新", busyTitle: "正在处理…", busy: false)
            }.buttonStyle(SecondaryButtonStyle())
        }
    }

    private var statusText: String {
        switch updater.phase {
        case .idle: return updater.lastChecked.map { String(localized: "上次检查 \($0.formatted(date: .omitted, time: .shortened))") } ?? String(localized: "尚未检查")
        case .checking: return String(localized: "正在检查…")
        case .upToDate: return String(localized: "已是最新") + (updater.lastChecked.map { " · " + $0.formatted(date: .omitted, time: .shortened) } ?? "")
        case .available: return String(localized: "发现新版本 \(updater.available?.displayVersion ?? "")")
        case let .downloading(fraction): return String(localized: "正在下载") + " " + (updater.available?.displayVersion ?? "") + " · " + String(Int(fraction * 100)) + "%"
        case .ready: return String(localized: "\(updater.available?.displayVersion ?? String(localized: "新版本")) 已下载并校验，可以安装")
        case .installing: return String(localized: "正在安装并重新打开…")
        case let .failed(message): return message
        }
    }

    private var statusColor: Color {
        switch updater.phase {
        case .failed: Ink.live
        case .ready, .available: Ink.ready
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
                TextField("仓库", text: $updater.repository).textFieldStyle(PaperTextFieldStyle()).frame(maxWidth: 260)
                    .onSubmit { updater.saveSettings() }
                Spacer()
                Label(updater.hasToken ? "已保存访问令牌" : "未保存访问令牌", systemImage: updater.hasToken ? "checkmark.seal" : "key")
                    .foregroundStyle(updater.hasToken ? .green : .secondary)
            }
            HStack {
                SecureField("粘贴访问令牌（github_pat_…）", text: $token).textFieldStyle(PaperTextFieldStyle())
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


private struct SettingsNativeControlsKey: EnvironmentKey { static let defaultValue = false }
extension EnvironmentValues {
    var settingsNativeControls: Bool {
        get { self[SettingsNativeControlsKey.self] }
        set { self[SettingsNativeControlsKey.self] = newValue }
    }
}

