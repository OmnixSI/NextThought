import AppKit
import SwiftUI
import UniformTypeIdentifiers
import PDFKit
import InterviewHackerCore

struct ContentView: View {
    @EnvironmentObject private var state: AppState
    @State private var showModelDownloadWarning = false
    @State private var showHistoryDeleteWarning = false
    @State private var profileImporting = false
    @State private var followTranscript = true

    var body: some View {
        VStack(spacing: 0) {
            header
            navigation
            if let message = state.errorMessage {
                HStack(alignment: .top) {
                    Image(systemName: "exclamationmark.triangle").foregroundStyle(.orange)
                    Text(message).font(.callout).textSelection(.enabled)
                    Spacer()
                    Button { state.closeError() } label: { Image(systemName: "xmark") }.buttonStyle(.borderless).help("关闭提示")
                }.padding(12).background(.orange.opacity(0.08)).padding(.horizontal, 18)
            }
            Group {
                switch state.section {
                case .assistant: assistant
                case .materials: MaterialsView()
                case .settings: settings
                case .history: history
                }
            }
            .frame(maxWidth: .infinity, maxHeight: .infinity)
        }
        .frame(minWidth: 520, minHeight: 600)
        .background(.ultraThinMaterial)
        .clipShape(RoundedRectangle(cornerRadius: 24, style: .continuous))
        .overlay(RoundedRectangle(cornerRadius: 24).stroke(.white.opacity(0.34), lineWidth: 1))
        .preferredColorScheme(state.darkMode ? .dark : .light)
        .confirmationDialog("下载本地语音识别模型？", isPresented: $showModelDownloadWarning, titleVisibility: .visible) {
            Button("下载约 547 MiB") { state.downloadModel() }
            Button("取消", role: .cancel) { }
        } message: {
            Text("模型会保存到本机应用支持目录。首次下载需要网络；权重不包含在应用安装包中。")
        }
        .confirmationDialog("删除全部本地记录？", isPresented: $showHistoryDeleteWarning, titleVisibility: .visible) {
            Button("删除记录", role: .destructive) { state.deleteHistory() }
            Button("取消", role: .cancel) { }
        } message: {
            Text("此操作会删除 InterviewHacker 保存的会话转录和回答。")
        }
        .sheet(isPresented: $state.isWindowPickerPresented) {
            NavigationStack {
                List(state.screenshotWindows) { window in
                    Button {
                        state.captureScreenshot(window)
                    } label: {
                        VStack(alignment: .leading, spacing: 3) {
                            Text(window.title).foregroundStyle(.primary).lineLimit(2)
                            Text(window.owner).font(.caption).foregroundStyle(.secondary)
                        }
                        .frame(maxWidth: .infinity, alignment: .leading)
                        .contentShape(Rectangle())
                    }
                    .buttonStyle(.plain)
                }
                .navigationTitle("选择要截图的窗口")
                .toolbar {
                    ToolbarItem(placement: .cancellationAction) {
                        Button("取消") { state.cancelWindowPicker() }
                    }
                }
            }
            .frame(width: 420, height: 520)
        }
        .sheet(isPresented: $state.isRegionCapturePresented) {
            if let image = state.regionCaptureImage {
                RegionScreenshotPicker(
                    image: image,
                    displays: state.screenshotDisplays,
                    selectedDisplayID: state.selectedScreenshotDisplayID,
                    onDisplaySelected: state.selectScreenshotDisplay,
                    onCancel: state.cancelRegionPicker,
                    onCapture: state.commitRegionScreenshot)
            } else {
                ProgressView("正在读取显示器…")
                    .frame(width: 420, height: 240)
            }
        }
        .fileImporter(isPresented: $profileImporting, allowedContentTypes: [.plainText, .pdf, .rtf, UTType(filenameExtension: "md") ?? .plainText], allowsMultipleSelection: true) { result in
            switch result {
            case .success(let urls): state.importDocuments(urls); state.section = .materials
            case .failure(let error): state.errorMessage = "读取资料失败：\(error.localizedDescription)"
            }
        }
    }

    private var header: some View {
        HStack(spacing: 12) {
            ZStack {
                RoundedRectangle(cornerRadius: 12).fill(Color.accentColor.gradient)
                Image(systemName: "waveform.and.mic").font(.system(size: 16, weight: .semibold)).foregroundStyle(.white)
            }
            .frame(width: 36, height: 36)

            VStack(alignment: .leading, spacing: 2) {
                Text("InterviewHacker").font(.system(size: 15, weight: .semibold))
                HStack(spacing: 5) {
                    Circle().fill(state.isRunning && !state.isPaused ? .green : .secondary.opacity(0.55)).frame(width: 6, height: 6)
                    Text(state.statusText).font(.system(size: 12, weight: .medium)).foregroundStyle(.secondary).lineLimit(1)
                }
            }
            Spacer()
            Button {
                state.section = state.section == .settings ? .assistant : .settings
            } label: {
                Image(systemName: state.section == .settings ? "waveform" : "slider.horizontal.3")
                    .font(.system(size: 14, weight: .medium))
                    .frame(width: 32, height: 32)
                    .background(.primary.opacity(0.055), in: Circle())
            }
            .buttonStyle(.plain)
            .help("设置")
        }
        .padding(.horizontal, 20)
        .padding(.top, 18)
        .padding(.bottom, 14)
    }

    private var navigation: some View {
        HStack(spacing: 5) {
            ForEach(AppSection.allCases) { item in navigationItem(item) }
        }
        .padding(4)
        .background(.primary.opacity(0.045), in: RoundedRectangle(cornerRadius: 14))
        .padding(.horizontal, 20)
        .padding(.bottom, 14)
    }

    private var assistant: some View {
        VStack(spacing: 12) {
            ScrollView {
                VStack(spacing: 14) {
                    sessionCard
                    HStack {
                        Button { state.section = .materials } label: {
                            Label("资料 \(state.documents.filter(\.enabled).count) 份 · \(state.sessionMode.title)", systemImage: "doc.text")
                        }
                        Spacer()
                        Button("提词器", systemImage: "rectangle.topthird.inset.filled") { TeleprompterController.shared.toggle() }
                        Button("仅提词器") { TeleprompterController.shared.showOnlyTeleprompter() }
                    }.buttonStyle(.bordered).font(.callout)
                    if let preview = state.screenshotPreview { screenshotCard(preview) }
                    if !state.currentQuestion.isEmpty || state.isAnswering || !state.answer.isEmpty { answerCard }
                    transcriptCard
                }
            }
            HStack {
                Button { state.forceAnswerCurrent() } label: {
                    Label("回答最新转写", systemImage: "bolt.fill").frame(maxWidth: .infinity)
                }.buttonStyle(PrimaryButtonStyle()).help("不等待自动问题判断 · " + state.shortcutLabel(.forceAnswer))
                Button { Task { if state.isPaused { await state.resumeSession() } else { await state.pauseSession() } } } label: {
                    Image(systemName: state.isPaused ? "play" : "pause")
                }.buttonStyle(SecondaryButtonStyle()).disabled(!state.isRunning || state.isChangingSession).help("暂停或恢复聆听")
            }
            questionComposer
            privacyNote
        }
        .padding(.horizontal, 18).padding(.bottom, 14)
    }

    private var sessionCard: some View {
        VStack(alignment: .leading, spacing: 11) {
            HStack {
                Label("实时会议辅助", systemImage: "waveform")
                    .font(.system(size: 13, weight: .semibold))
                Spacer()
                Text(state.readinessText)
                    .font(.system(size: 12, weight: .medium))
                    .foregroundStyle(state.readinessText == "可以开始" ? .green : .secondary)
            }
            HStack(spacing: 8) {
                if !state.capturePermissionGranted {
                    Button("允许录屏与系统音频") {
                        Task { await state.requestCapturePermission() }
                    }
                    .buttonStyle(PrimaryButtonStyle())
                }
                if !state.isRunning {
                    Button {
                        Task { await state.startSession() }
                    } label: {
                        Label("开始辅助", systemImage: "play.fill").frame(maxWidth: .infinity)
                    }
                    .buttonStyle(PrimaryButtonStyle())
                    .disabled(state.readinessText != "可以开始" || state.isPreparingModel)
                } else if state.isPaused {
                    Button {
                        Task { await state.resumeSession() }
                    } label: {
                        Label("恢复", systemImage: "play.fill").frame(maxWidth: .infinity)
                    }
                    .buttonStyle(PrimaryButtonStyle())
                    Button {
                        Task { await state.stopSession() }
                    } label: {
                        Label("停止", systemImage: "stop.fill").frame(maxWidth: .infinity)
                    }
                    .buttonStyle(SecondaryButtonStyle())
                } else {
                    Button {
                        Task { await state.pauseSession() }
                    } label: {
                        Label("暂停", systemImage: "pause.fill").frame(maxWidth: .infinity)
                    }
                    .buttonStyle(SecondaryButtonStyle())
                    Button {
                        Task { await state.stopSession() }
                    } label: {
                        Label("停止", systemImage: "stop.fill").frame(maxWidth: .infinity)
                    }
                    .buttonStyle(SecondaryButtonStyle())
                }
                Button {
                    state.captureScreenshot(.region)
                } label: {
                    Image(systemName: "viewfinder").frame(width: 42, height: 36)
                }
                .buttonStyle(SecondaryButtonStyle())
                .help("截取屏幕区域 · ⌘⇧S")
            }
            if state.droppedAudioSegments > 0 {
                Label("已遗漏 \(state.droppedAudioSegments) 段音频，转录不完整", systemImage: "exclamationmark.triangle")
                    .font(.caption).foregroundStyle(.orange)
            }
            Picker("音频来源", selection: $state.selectedTargetID) {
                ForEach(state.captureTargets) { Text($0.title).tag($0.id) }
            }.onChange(of: state.selectedTargetID) { _, _ in Task { await state.changeCaptureTarget() } }
            HStack(spacing: 7) {
                Image(systemName: "speaker.wave.2.fill").font(.system(size: 12)).foregroundStyle(.secondary)
                Text(state.transcriptionBackend == "apple" ? "系统音频 → Apple Speech · 端侧流式" : "系统音频 → Whisper · 分段识别")
                    .font(.system(size: 12)).foregroundStyle(.secondary)
                Spacer()
                Text(state.shortcutLabel(.toggleSession)).font(.system(size: 11, design: .monospaced)).foregroundStyle(.tertiary)
            }
        }
        .padding(14)
        .background(.background.opacity(0.72), in: RoundedRectangle(cornerRadius: 17))
        .overlay(RoundedRectangle(cornerRadius: 17).stroke(.primary.opacity(0.06), lineWidth: 1))
        .disabled(state.isChangingSession)
    }


    private var transcriptCard: some View {
        VStack(alignment: .leading, spacing: 9) {
            HStack {
                Text(state.transcriptionBackend == "apple" ? "流式转录" : "分段转录").font(.system(size: 14, weight: .semibold))
                Spacer()
                Toggle("跟随", isOn: $followTranscript).toggleStyle(.checkbox).font(.caption)
                Text(state.transcriptionStatus).font(.system(size: 11)).foregroundStyle(.secondary)
                Text("\(state.entries.count) 条").font(.system(size: 12)).foregroundStyle(.tertiary)
            }
            ScrollViewReader { proxy in
                ScrollView {
                    VStack(alignment: .leading, spacing: 10) {
                        if state.entries.isEmpty && state.partialTranscript.isEmpty {
                            Text("开始辅助后，稳定识别的会议语音会出现在这里。原始音频不保存。")
                                .font(.system(size: 13)).foregroundStyle(.secondary)
                                .frame(maxWidth: .infinity, alignment: .leading)
                                .padding(.vertical, 14)
                        }
                        ForEach(state.entries.suffix(80)) { entry in
                            transcriptLine(entry.text, time: entry.date.formatted(date: .omitted, time: .shortened), partial: false)
                                .id(entry.id)
                        }
                        if !state.partialTranscript.isEmpty {
                            transcriptLine(state.partialTranscript, time: "识别中", partial: true)
                                .id("partial")
                        }
                    }
                }
                .frame(minHeight: 90, maxHeight: 210)
                .onChange(of: state.partialTranscript) { _, _ in if followTranscript { withAnimation(.easeOut(duration: 0.15)) { proxy.scrollTo("partial", anchor: .bottom) } } }
                .onChange(of: state.entries.count) { _, _ in if followTranscript { withAnimation(.easeOut(duration: 0.15)) { proxy.scrollTo(state.entries.last?.id, anchor: .bottom) } } }
            }
        }
        .padding(14)
        .background(.background.opacity(0.55), in: RoundedRectangle(cornerRadius: 17))
    }

    private var answerCard: some View {
        VStack(alignment: .leading, spacing: 10) {
            HStack(alignment: .top) {
                VStack(alignment: .leading, spacing: 4) {
                    Text("建议回答").font(.system(size: 14, weight: .semibold))
                    Text(state.displayedQuestion.isEmpty ? state.pendingQuestion : state.displayedQuestion).font(.system(size: 12)).foregroundStyle(.secondary).lineLimit(2)
                }
                Spacer()
                if state.isAnswering {
                    ProgressView().controlSize(.small)
                    Button { state.cancelAnswer() } label: { Image(systemName: "xmark.circle.fill") }
                        .buttonStyle(.plain).foregroundStyle(.secondary).help("取消回答")
                }
            }
            if state.isAnswering && !state.answer.isEmpty {
                Text("正在回答：\(state.pendingQuestion)").font(.system(size: 12)).foregroundStyle(.secondary).lineLimit(2)
            }
            ScrollView {
                Text(state.displayedAnswer.isEmpty ? "Codex 正在生成回答…" : state.displayedAnswer)
                    .font(.system(size: 16)).lineSpacing(4).textSelection(.enabled)
                    .frame(maxWidth: .infinity, alignment: .leading)
            }
            .frame(minHeight: 140, maxHeight: 320)
            HStack {
                Button { state.copyAnswer() } label: { Label("复制", systemImage: "doc.on.doc") }
                Button { state.regenerate() } label: { Label("重新生成", systemImage: "arrow.clockwise") }
                    .disabled(state.isAnswering)
                Spacer()
                Text(state.lastAnswerSeconds.map { String(format: "回答 %.1f s · Codex", $0) } ?? "Codex CLI").font(.system(size: 11)).foregroundStyle(.tertiary)
            }
            .buttonStyle(.plain)
            .font(.system(size: 12, weight: .medium))
        }
        .padding(14)
        .background(Color.accentColor.opacity(0.08), in: RoundedRectangle(cornerRadius: 17))
        .overlay(RoundedRectangle(cornerRadius: 17).stroke(Color.accentColor.opacity(0.12), lineWidth: 1))
    }

    private func screenshotCard(_ image: NSImage) -> some View {
        VStack(alignment: .leading, spacing: 9) {
            HStack {
                Label("截图预览", systemImage: "viewfinder").font(.system(size: 13, weight: .semibold))
                Spacer()
                Button("取消") { state.clearScreenshot() }.buttonStyle(.plain).font(.system(size: 12))
                Button("分析截图") { state.answerScreenshot() }.buttonStyle(PrimaryButtonStyle()).font(.system(size: 12))
            }
            Image(nsImage: image).resizable().scaledToFit().frame(maxHeight: 125).clipShape(RoundedRectangle(cornerRadius: 8))
        }
        .padding(12)
        .background(.background.opacity(0.68), in: RoundedRectangle(cornerRadius: 15))
    }

    private var questionComposer: some View {
        HStack(spacing: 8) {
            TextField("手动输入问题…", text: $state.questionInput, axis: .vertical)
                .lineLimit(1...3)
                .textFieldStyle(.plain)
                .font(.system(size: 14))
                .onSubmit { state.answerInput() }
            Button { state.answerInput() } label: {
                Image(systemName: "arrow.up.circle.fill").font(.system(size: 24)).foregroundStyle(Color.accentColor)
            }
            .buttonStyle(.plain)
            .disabled(state.questionInput.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty)
        }
        .padding(.horizontal, 13)
        .padding(.vertical, 9)
        .background(.background.opacity(0.76), in: RoundedRectangle(cornerRadius: 14))
        .overlay(RoundedRectangle(cornerRadius: 14).stroke(.primary.opacity(0.07), lineWidth: 1))
    }

    private var privacyNote: some View {
        HStack(spacing: 6) {
            Image(systemName: "lock.shield").foregroundStyle(.green)
            Text("音频仅在本机识别；回答所需文本与截图会交给 Codex CLI。")
            Spacer(minLength: 0)
            Button("截取窗口") { state.captureScreenshot(.window) }
                .buttonStyle(.plain)
                .foregroundStyle(Color.accentColor)
                .help("截取指定窗口 · ⌘⇧A")
        }
        .font(.system(size: 11))
        .foregroundStyle(.secondary)
    }

    private func transcriptLine(_ text: String, time: String, partial: Bool) -> some View {
        HStack(alignment: .top, spacing: 8) {
            Text(time).font(.system(size: 11, design: .rounded)).foregroundStyle(.tertiary).frame(width: 42, alignment: .trailing)
            Text(text).font(.system(size: 13)).lineSpacing(3).foregroundStyle(partial ? .secondary : .primary)
                .frame(maxWidth: .infinity, alignment: .leading).textSelection(.enabled)
        }
    }

    private func navigationItem(_ item: AppSection) -> some View {
        let isSelected = state.section == item
        return Button {
            state.section = item
        } label: {
            Text(item.rawValue)
                .font(.system(size: 14, weight: isSelected ? .semibold : .medium))
                .foregroundStyle(isSelected ? Color.primary : Color.secondary)
                .frame(maxWidth: .infinity)
                .padding(.vertical, 9)
                .background(isSelected ? Color(nsColor: .windowBackgroundColor) : .clear, in: RoundedRectangle(cornerRadius: 10))
        }
        .buttonStyle(.plain)
    }

    private var settings: some View {
        ScrollView {
            VStack(alignment: .leading, spacing: 12) {
                settingsSection("系统权限", icon: "lock.shield") {
                    Text(state.capturePermissionStatus).font(.system(size: 12)).foregroundStyle(state.capturePermissionGranted ? .green : .secondary)
                    HStack {
                        Button(state.capturePermissionGranted ? "打开系统设置" : "申请录屏与系统音频") {
                            if state.capturePermissionGranted { state.openCapturePermissionSettings() }
                            else { Task { await state.requestCapturePermission() } }
                        }
                        .buttonStyle(SecondaryButtonStyle())
                        Button("刷新状态") { Task { await state.refreshTargets() } }
                            .buttonStyle(.plain)
                    }
                    Text("授权后如系统要求退出并重新打开，请完成重启。后续更新使用固定安装路径与同一签名身份；不要同时运行多个同名副本。")
                        .font(.system(size: 11)).foregroundStyle(.secondary).fixedSize(horizontal: false, vertical: true)
                }
                settingsSection("Codex 助手", icon: "sparkles") {
                    HStack {
                        VStack(alignment: .leading, spacing: 4) {
                            Text(state.codexLoginStatus).font(.system(size: 14, weight: .medium))
                            Text("Codex CLI · \(state.codexVersion)").font(.system(size: 12)).foregroundStyle(.secondary)
                        }
                        Spacer()
                        Button { Task { await state.refreshCodexStatus() } } label: {
                            Image(systemName: "arrow.clockwise").frame(width: 30, height: 30)
                        }
                        .buttonStyle(SecondaryButtonStyle())
                    }
                    Text("不使用 API Key。每次调用通过本机 Codex CLI 指定 GPT-5.6 Luna 与推理强度 none；转录、资料和截图会交给 Codex 服务处理。")
                        .font(.system(size: 12)).foregroundStyle(.secondary).fixedSize(horizontal: false, vertical: true)
                    Button("打开 ChatGPT / Codex") {
                        if let url = NSWorkspace.shared.urlForApplication(withBundleIdentifier: "com.openai.chat") {
                            NSWorkspace.shared.open(url)
                        } else if let url = URL(string: "https://chatgpt.com") {
                            NSWorkspace.shared.open(url)
                        }
                    }
                    .buttonStyle(.plain).font(.system(size: 12, weight: .medium)).foregroundStyle(Color.accentColor)
                }

                settingsSection("转写引擎", icon: "waveform") {
                    Picker("引擎", selection: $state.transcriptionBackend) {
                        Text("Whisper · 本地分段 / 自动语言").tag("whisper")
                        Text("Apple Speech · 本地流式 / macOS 26").tag("apple")
                    }.disabled(state.isRunning || state.isPreparingModel)
                    .onChange(of: state.transcriptionBackend) { _, _ in state.updateSettings() }
                    Text("Apple 路径需明确选择中文或 English，首次使用可能下载语言资源。不支持时会提示切回 Whisper，不会自动把音频传到云端。")
                        .font(.caption).foregroundStyle(.secondary)
                    if let rtf = state.lastRTF, let seconds = state.lastASRSeconds {
                        Text(String(format: "上段 Whisper：%.2f s · RTF %.2f（>1 表示处理慢于音频输入）", seconds, rtf)).font(.caption)
                    }
                    if state.droppedAudioSegments > 0 {
                        Text("当前会话有 \(state.droppedAudioSegments) 段音频因积压或停止超时未完成识别；转录并不完整。").font(.caption).foregroundStyle(.orange)
                    }
                }
                settingsSection("本地语音识别", icon: "waveform") {
                    HStack(alignment: .top) {
                        VStack(alignment: .leading, spacing: 4) {
                            Text(LocalWhisperService.modelName).font(.system(size: 13, weight: .semibold))
                            Text(state.isModelInstalled ? "已安装并通过完整性校验" : "尚未就绪 · 约 547 MiB")
                                .font(.system(size: 12)).foregroundStyle(state.isModelInstalled ? .green : .secondary)
                        }
                        Spacer()
                        if state.isModelInstalled {
                            Button("删除") { state.deleteModel() }.disabled(state.isRunning || state.isChangingSession || state.isDownloadingModel).buttonStyle(.plain).font(.system(size: 12)).foregroundStyle(.red)
                        } else if state.isDownloadingModel {
                            Button("取消") { state.cancelModelDownload() }.buttonStyle(.plain).font(.system(size: 12)).foregroundStyle(.secondary)
                        } else {
                            Button("下载模型") { showModelDownloadWarning = true }
                                .buttonStyle(SecondaryButtonStyle())
                        }
                    }
                    if state.isDownloadingModel { ProgressView(value: state.modelDownloadProgress).controlSize(.small) }
                    if !state.modelDownloadStatus.isEmpty {
                        Text(state.modelDownloadStatus).font(.system(size: 12)).foregroundStyle(.secondary)
                    }
                    Picker("识别语言", selection: $state.recognitionLanguage) {
                        Text("自动识别中英文").tag("auto")
                        Text("中文").tag("zh")
                        Text("English").tag("en")
                    }
                    .disabled(state.isRunning || state.isPreparingModel)
                    .onChange(of: state.recognitionLanguage) { _, _ in state.updateSettings() }
                    Text("音频在本机用 Whisper.cpp 1.9.4 和 large-v3-turbo Q5_0 识别，不会翻译。模型为多语言版，来源 Hugging Face，文件约 547 MiB，MIT 许可。用户无需安装 Homebrew 或 Whisper。")
                        .font(.system(size: 11)).foregroundStyle(.secondary).fixedSize(horizontal: false, vertical: true)
                    Link("模型来源与开源说明", destination: URL(string: "https://huggingface.co/ggerganov/whisper.cpp")!)
                        .font(.system(size: 11)).foregroundStyle(Color.accentColor)
                    TextField("专业词 / 人名（仅用于 Whisper）", text: $state.recognitionVocabulary)
                        .textFieldStyle(.roundedBorder)
                        .onChange(of: state.recognitionVocabulary) { _, _ in state.updateSettings() }
                    Text("缓存位置：\(state.modelPath)").font(.system(size: 11, design: .monospaced)).foregroundStyle(.tertiary).textSelection(.enabled)
                    if let seconds = state.modelLoadSeconds, seconds > 0 {
                        Text(String(format: "本次模型加载 %.1f 秒", seconds)).font(.system(size: 11)).foregroundStyle(.tertiary)
                    }
                }

                settingsSection("捕获目标", icon: "rectangle.on.rectangle") {
                    Picker("系统音频来源", selection: $state.selectedTargetID) {
                        ForEach(state.captureTargets) { target in Text(target.title).tag(target.id) }
                    }
                    .labelsHidden()
                    .onChange(of: state.selectedTargetID) { _, _ in Task { await state.changeCaptureTarget() } }
                    HStack {
                        Text("音频通道").font(.system(size: 12, weight: .medium))
                        Spacer()
                        Text("系统输出").font(.system(size: 12)).foregroundStyle(.secondary)
                        Button { Task { await state.refreshTargets() } } label: { Image(systemName: "arrow.clockwise") }
                            .buttonStyle(.plain).foregroundStyle(Color.accentColor)
                    }
                    Text("本版捕获所选屏幕或窗口的系统音频。麦克风通道与多人说话人区分尚未实现。首次使用需允许屏幕与系统音频录制。")
                        .font(.system(size: 11)).foregroundStyle(.secondary).fixedSize(horizontal: false, vertical: true)
                }

                settingsSection("回答与资料", icon: "text.book.closed") {
                    Toggle("自动检测问题并回答", isOn: $state.automaticAnswers).font(.system(size: 13)).onChange(of: state.automaticAnswers) { _, _ in state.updateSettings() }
                    Toggle("保存会话转录和回答", isOn: $state.saveHistory).font(.system(size: 13)).onChange(of: state.saveHistory) { _, _ in state.updateSettings() }
                    HStack {
                        Text("背景说明；多文件与 Prompt 在“资料”页").font(.system(size: 12, weight: .medium))
                        Spacer()
                        Button("导入文件") { profileImporting = true }.buttonStyle(.plain).font(.system(size: 12)).foregroundStyle(Color.accentColor)
                    }
                    TextEditor(text: $state.profile)
                        .font(.system(size: 12))
                        .scrollContentBackground(.hidden)
                        .frame(height: 84)
                        .padding(6)
                        .background(.primary.opacity(0.045), in: RoundedRectangle(cornerRadius: 9))
                        .onChange(of: state.profile) { _, value in state.setProfile(value) }
                    Text("关闭保存时，转录只留在当前运行内存；原始音频从不保存。开启保存后，记录保存在本机 Application Support。")
                        .font(.system(size: 11)).foregroundStyle(.secondary).fixedSize(horizontal: false, vertical: true)
                }

                settingsSection("窗口与快捷键", icon: "keyboard") {
                    Toggle("窗口置顶", isOn: $state.keepOnTop).font(.system(size: 13)).onChange(of: state.keepOnTop) { _, _ in state.updateSettings() }
                    Toggle("深色外观", isOn: $state.darkMode).font(.system(size: 13)).onChange(of: state.darkMode) { _, _ in state.updateSettings() }
                    Toggle("尽力排除屏幕捕获（不保证）", isOn: $state.capturePrivacy).onChange(of: state.capturePrivacy) { _, _ in state.updateSettings() }
                    Text("此选项不能保证 Zoom、Teams、Meet、录屏或截图软件都不捕获。优先只共享需要展示的窗口，并在另一台设备检查实际共享画面。不要用于绕过录制同意或考试规则。")
                        .font(.caption).foregroundStyle(.secondary)
                    ShortcutSettingsView()
                    ForEach(state.shortcutErrors, id: \.self) { Text($0).font(.system(size: 11)).foregroundStyle(.orange) }
                }

                Text("Codex 请求由 Codex CLI 按当前账号与产品规则处理；InterviewHacker 不保存登录凭证。若 CLI 或账号不可用，请在 Codex 客户端完成登录后重试。")
                    .font(.system(size: 11)).foregroundStyle(.tertiary).padding(.horizontal, 4)
            }
            .padding(.horizontal, 18)
            .padding(.bottom, 18)
        }
    }

    private var history: some View {
        VStack(alignment: .leading, spacing: 12) {
            HStack {
                VStack(alignment: .leading, spacing: 3) {
                    Text("本地会话记录").font(.system(size: 15, weight: .semibold))
                    Text("记录保存在本机，不含原始音频。") .font(.system(size: 12)).foregroundStyle(.secondary)
                }
                Spacer()
                Button { showHistoryDeleteWarning = true } label: { Image(systemName: "trash") }
                    .buttonStyle(.plain).foregroundStyle(.red).disabled(state.savedSessions.isEmpty)
            }
            if state.savedSessions.isEmpty {
                ContentUnavailableView("没有保存的会话", systemImage: "clock", description: Text("在设置中开启会话保存。"))
                    .frame(maxHeight: .infinity)
            } else {
                ScrollView {
                    VStack(spacing: 9) {
                        ForEach(state.savedSessions, id: \.id) { session in
                            VStack(alignment: .leading, spacing: 7) {
                                HStack {
                                    Text(session.startedAt.formatted(date: .abbreviated, time: .shortened))
                                        .font(.system(size: 13, weight: .semibold))
                                    Spacer()
                                    Text("\(session.entries.count) 条转录 · \(session.answers.count) 个回答")
                                        .font(.system(size: 11)).foregroundStyle(.secondary)
                                }
                                Text(session.entries.map(\.text).joined(separator: " · "))
                                    .font(.system(size: 12)).lineLimit(3).foregroundStyle(.secondary)
                            }
                            .padding(12)
                            .frame(maxWidth: .infinity, alignment: .leading)
                            .background(.background.opacity(0.7), in: RoundedRectangle(cornerRadius: 13))
                        }
                    }
                }
            }
            HStack {
                Button("导出当前转录") { state.saveTranscriptExport() }.buttonStyle(SecondaryButtonStyle())
                Spacer()
                Text("\(state.savedSessions.count) 个会话").font(.system(size: 12)).foregroundStyle(.tertiary)
            }
        }
        .padding(18)
    }

    private func settingsSection<Content: View>(_ title: String, icon: String, @ViewBuilder content: () -> Content) -> some View {
        VStack(alignment: .leading, spacing: 10) {
            Label(title, systemImage: icon).font(.system(size: 14, weight: .semibold))
            content()
        }
        .padding(13)
        .frame(maxWidth: .infinity, alignment: .leading)
        .background(.background.opacity(0.68), in: RoundedRectangle(cornerRadius: 15))
    }

    private func shortcutLine(_ key: String, detail: String) -> some View {
        HStack {
            Text(key).font(.system(size: 12, weight: .medium, design: .monospaced))
            Spacer()
            Text(detail).font(.system(size: 12)).foregroundStyle(.secondary)
        }
    }

}

private struct PrimaryButtonStyle: ButtonStyle {
    func makeBody(configuration: Configuration) -> some View {
        configuration.label
            .font(.system(size: 13, weight: .semibold))
            .foregroundStyle(.white)
            .padding(.horizontal, 12)
            .frame(height: 36)
            .background(Color.accentColor.opacity(configuration.isPressed ? 0.78 : 1), in: RoundedRectangle(cornerRadius: 10))
    }
}

private struct SecondaryButtonStyle: ButtonStyle {
    func makeBody(configuration: Configuration) -> some View {
        configuration.label
            .font(.system(size: 13, weight: .medium))
            .foregroundStyle(.primary)
            .padding(.horizontal, 12)
            .frame(height: 36)
            .background(.primary.opacity(configuration.isPressed ? 0.1 : 0.055), in: RoundedRectangle(cornerRadius: 10))
    }
}
