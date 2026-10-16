import AppKit
import InterviewHackerCore
import SwiftUI

/// Settings → 判断: the decision model (Laya or StartLux-Decision) that takes over the rule-based
/// judgments (is this a question for me, does it need the web, is this mic line mine,
/// are these the same question).
struct DecisionSettings: View {
    @Environment(AppState.self) private var state

    var body: some View {
        @Bindable var decisions = state.decisions
        SettingsSection(title: "决策模型", icon: "brain") {
            Toggle("用决策模型做判断", isOn: $decisions.enabled)
                .onChange(of: decisions.enabled) { _, on in
                    decisions.save()
                    if on { Task { await decisions.start() } } else { decisions.stop() }
                }
            Note("原来靠正则和关键词规则做的判断，交给专门做“选择题”的模型：一次读出每个选项的概率，不生成文字。规则仍是兜底：模型没开、没启动、超时（约 1 秒）或出错时，照常按规则处理，不会拖慢回答。")
            Picker("运行在", selection: $decisions.source) {
                ForEach(DecisionService.Source.allCases) { Text($0.title).tag($0) }
            }
            .pickerStyle(.segmented)
            .onChange(of: decisions.source) { _, _ in decisions.save(); decisions.stop() }
            if decisions.source == .local {
                DecisionModelPicker()
            } else {
                TextField("服务地址，例如 http://192.168.1.20:8790", text: $decisions.serverURL).textFieldStyle(.roundedBorder)
                    .onChange(of: decisions.serverURL) { _, _ in decisions.save() }
                SecureField("Key（可留空）", text: $decisions.serverKey).textFieldStyle(.roundedBorder)
                    .onChange(of: decisions.serverKey) { _, _ in decisions.save() }
                Note("任何运行 `startlux-decide serve` 的机器都可以（例如家里另一台 Mac）。应用调用 /health 和 /v1/systemone。")
            }
            DecisionStatusRow()
        }
        SettingsSection(title: "交给模型的判断", icon: "list.bullet.rectangle") {
            Toggle("这句是不是在问我（问题识别、补全问题、漏掉的问题）", isOn: $decisions.useForQuestions)
                .onChange(of: decisions.useForQuestions) { _, _ in decisions.save() }
            Toggle("这个问题要不要联网（联网搜索设为“自动”时）", isOn: $decisions.useForWebSearch)
                .onChange(of: decisions.useForWebSearch) { _, _ in decisions.save() }
            Toggle("麦克风这句是我说的，还是扬声器回声", isOn: $decisions.useForEcho)
                .onChange(of: decisions.useForEcho) { _, _ in decisions.save() }
            Toggle("复盘后合并题库里的重复问题", isOn: $decisions.useForKnowledge)
                .onChange(of: decisions.useForKnowledge) { _, _ in decisions.save() }
            Note("把握很大的问题（以问号结尾等）仍由规则立即回答；模型负责拿不准的句子，并在规则漏掉问题时补上。")
        }
        InsightSettings()
        DecisionEvaluationSection()
        SettingsSection(title: "许可与隐私", icon: "doc.text") {
            Note("Laya（Convai Innovations，Mac 版由社区移植为 laya-mlx）使用 Apache-2.0 许可，可商用。StartLux-Decision 权重使用 CC BY-NC 4.0，仅限非商业用途。两者的中文效果都请用上面的“评测”在这台 Mac 上实测。")
            Note("在这台 Mac 上运行时，对话文本只发给本机 127.0.0.1 的服务，不会上传。连接其他服务时，文本会发给那台机器。")
            HStack(spacing: 14) {
                Link("Laya", destination: URL(string: "https://github.com/NandhaKishorM/laya")!)
                Link("laya-mlx", destination: URL(string: "https://github.com/mizorewww/laya-mlx")!)
                Link("StartLux-Decision", destination: URL(string: "https://decision.startlux.com/")!)
            }
            .font(.system(size: 12))
        }
    }
}

/// Sizes, with the recommended one for this Mac's memory.
private struct DecisionModelPicker: View {
    @Environment(AppState.self) private var state

    var body: some View {
        @Bindable var decisions = state.decisions
        let recommended = DecisionModelVariant.recommended
        let memory = Double(ProcessInfo.processInfo.physicalMemory) / 1_073_741_824
        VStack(alignment: .leading, spacing: 6) {
            HStack {
                Text("模型").font(.system(size: 12, weight: .medium))
                Spacer()
                if !decisions.variant.isLaya {
                    Picker("StartLux 精度", selection: $decisions.bits) {
                        Text("8-bit（推荐）").tag(8)
                        Text("4-bit（更小）").tag(4)
                    }
                    .labelsHidden().pickerStyle(.segmented).frame(width: 220)
                    .onChange(of: decisions.bits) { _, _ in decisions.save(); decisions.stop() }
                }
            }
            ForEach(DecisionModelVariant.allCases) { variant in
                DecisionVariantRow(variant: variant, bits: decisions.bits, selected: decisions.variant == variant,
                                   recommended: recommended.variant == variant && (variant.isLaya || recommended.bits == decisions.bits),
                                   installed: decisions.isInstalled(variant, bits: decisions.bits)) {
                    decisions.variant = variant
                    decisions.save()
                    decisions.stop()
                }
            }
            Note(String(format: "这台 Mac 有 %.0f GB 内存。实时判断每句话都要跑，所以推荐又小又快的 Laya；StartLux 是大模型，常识更多，但占用和耗时大得多。大小为估算值，以下载时显示的为准。首次下载还会安装 Python 运行环境（Laya 约 300 MB，StartLux 约 1 GB；需要 uv 或 Python 3.11 以上），都放在应用数据目录里。", memory))
        }
    }
}

private struct DecisionVariantRow: View {
    let variant: DecisionModelVariant
    let bits: Int
    let selected: Bool
    let recommended: Bool
    let installed: Bool
    let select: () -> Void

    var body: some View {
        let disk = bits == 8 ? variant.estimatedGB.eight : variant.estimatedGB.four
        let memory = bits == 8 ? variant.estimatedRAM.eight : variant.estimatedRAM.four
        Button(action: select) {
            HStack(spacing: 10) {
                Image(systemName: selected ? "largecircle.fill.circle" : "circle")
                    .foregroundStyle(selected ? Brand.accent : .secondary)
                Text(variant.title).font(.system(size: 13, weight: .semibold)).frame(width: 130, alignment: .leading)
                VStack(alignment: .leading, spacing: 2) {
                    HStack(spacing: 6) {
                        Text(String(format: "约 %.1f GB 磁盘 · 运行约 %.0f GB 内存", disk, memory)
                             + (variant.decisionIndex.map { String(format: " · DI %.1f", $0) } ?? ""))
                            .font(.system(size: 12)).monospacedDigit()
                        if recommended { DecisionBadge("推荐", color: Brand.accent) }
                        if installed { DecisionBadge("已下载", color: .green) }
                    }
                    Text("\(variant.note) · \(variant.license)").font(.system(size: 11)).foregroundStyle(.secondary)
                }
                Spacer(minLength: 0)
            }
            .padding(.horizontal, 10).padding(.vertical, 7)
            .background(selected ? Brand.accent.opacity(0.10) : Color.primary.opacity(0.03), in: RoundedRectangle(cornerRadius: 8, style: .continuous))
            .contentShape(Rectangle())
        }
        .buttonStyle(.plain)
    }
}

private struct DecisionBadge: View {
    let text: String
    let color: Color
    init(_ text: String, color: Color) { self.text = text; self.color = color }
    var body: some View {
        Text(text).font(.system(size: 10, weight: .semibold)).foregroundStyle(color)
            .padding(.horizontal, 6).padding(.vertical, 1)
            .background(color.opacity(0.14), in: Capsule())
    }
}

/// Install / start / stop, with progress and the measured latency.
private struct DecisionStatusRow: View {
    @Environment(AppState.self) private var state

    var body: some View {
        let decisions = state.decisions
        VStack(alignment: .leading, spacing: 8) {
            HStack(spacing: 10) {
                switch decisions.phase {
                case .notInstalled:
                    if decisions.source == .local {
                        Button { decisions.install() } label: { Label("下载并启动 \(decisions.variant.title)", systemImage: "arrow.down.circle") }
                            .buttonStyle(PrimaryButtonStyle())
                    }
                case .installing:
                    ProgressView().controlSize(.small)
                    Button("取消") { decisions.cancelInstall() }.buttonStyle(SecondaryButtonStyle())
                case .stopped, .failed:
                    Button { Task { await decisions.start() } } label: { Label(decisions.source == .local ? "启动" : "连接", systemImage: "play.fill") }
                        .buttonStyle(PrimaryButtonStyle())
                        .disabled(decisions.source == .server && decisions.serverURL.isEmpty)
                    if decisions.source == .local, case .failed = decisions.phase, !decisions.isInstalled(decisions.variant, bits: decisions.bits) {
                        Button("重新下载") { decisions.install() }.buttonStyle(SecondaryButtonStyle())
                    }
                case .starting:
                    ProgressView().controlSize(.small)
                    Text(decisions.variant.isLaya ? "正在启动…" : "正在启动（首次加载模型约半分钟）…").font(.system(size: 12)).foregroundStyle(.secondary)
                case .running:
                    Button { decisions.stop() } label: { Label("停止", systemImage: "stop.fill") }.buttonStyle(SecondaryButtonStyle())
                }
                Spacer()
                if decisions.source == .local {
                    if decisions.isInstalled(decisions.variant, bits: decisions.bits), decisions.phase != .running {
                        Button("删除这个模型") { decisions.delete(decisions.variant, bits: decisions.bits) }.buttonStyle(ChipButtonStyle())
                    }
                    Button("日志") { NSWorkspace.shared.open(decisions.logFile) }.buttonStyle(ChipButtonStyle())
                        .disabled(!FileManager.default.fileExists(atPath: decisions.logFile.path))
                }
            }
            if case let .installing(message, progress) = decisions.phase {
                if let progress { ProgressView(value: progress).controlSize(.small) }
                Note(message)
            }
            Note(statusText(decisions), color: statusColor(decisions))
        }
        .onAppear { decisions.refreshPhase() }
    }

    private func statusText(_ decisions: DecisionService) -> String {
        switch decisions.phase {
        case .notInstalled: decisions.source == .local ? "还没有下载 \(decisions.variant.displayName(bits: decisions.bits))。" : "未连接"
        case .installing: "下载可以随时取消，已下载的部分会保留。"
        case .stopped: decisions.enabled ? "已停止：现在按规则判断。" : "已下载；打开上面的开关后使用。"
        case .starting: "启动期间按规则判断。"
        case .running:
            "运行中" + (decisions.latencyMs.map { "，每次判断约 \(Int($0)) ms" } ?? "") + (decisions.enabled ? "" : "（开关关闭，未使用）")
        case let .failed(message): message
        }
    }

    private func statusColor(_ decisions: DecisionService) -> Color {
        switch decisions.phase {
        case .running: .green
        case .failed: .red
        default: .secondary
        }
    }
}

/// Rules vs model on the built-in labeled samples.
private struct DecisionEvaluationSection: View {
    @Environment(AppState.self) private var state

    var body: some View {
        let decisions = state.decisions
        SettingsSection(title: "评测", icon: "gauge.with.dots.needle.67percent") {
            Note("用内置的 \(QuestionEvalCorpus.all.count) 句中英文样本（已标注是不是提问）和 \(WebSearchNeed.evalSamples.count) 个联网样本，分别对比本地规则和模型的准确率与耗时，按当前场景的模式判断。")
            HStack {
                Button { Task { await decisions.evaluate(mode: state.activeWorkspace.mode) } } label: { Label("开始评测", systemImage: "play.circle") }
                    .buttonStyle(SecondaryButtonStyle())
                    .disabled(!decisions.isReady || decisions.isEvaluating)
                if decisions.isEvaluating { ProgressView().controlSize(.small) }
                if !decisions.isReady { Note("模型运行后可评测") }
            }
            if !decisions.evaluation.isEmpty {
                Text(decisions.evaluation).font(.system(size: 12)).monospacedDigit().textSelection(.enabled)
            }
        }
    }
}

/// The model's other uses: the screen, slides, answer progress, notes, wrap-up, web check.
private struct InsightSettings: View {
    @Environment(AppState.self) private var state

    var body: some View {
        @Bindable var insights = state.insights
        let modelReady = state.decisions.isReady
        SettingsSection(title: "屏幕感知", icon: "eye") {
            Toggle("会话中每隔几秒读一次屏幕", isOn: $insights.screenWatch)
                .onChange(of: insights.screenWatch) { _, on in
                    insights.save()
                    state.startScreenWatch()
                    if on { state.updateLayout { $0.show(.insights) } }
                }
            if insights.screenWatch {
                HStack {
                    Text("间隔")
                    Picker("间隔", selection: $insights.screenInterval) {
                        Text("2 秒").tag(2.0)
                        Text("4 秒").tag(4.0)
                        Text("8 秒").tag(8.0)
                        Text("15 秒").tag(15.0)
                    }
                    .labelsHidden().pickerStyle(.segmented).frame(width: 260)
                    .onChange(of: insights.screenInterval) { _, _ in insights.save() }
                }
                Toggle("跟随演示文稿：认出正在讲第几页，给出这一页的讲稿提示", isOn: $insights.followSlides)
                    .onChange(of: insights.followSlides) { _, _ in insights.save() }
            }
            Note("在本机用系统文字识别读取主显示器（不含 InterviewHacker 自己的窗口），文字不上传。模型判断屏幕内容是否和对话相关：相关的才交给回答引擎（例如共享屏幕上的题目、代码、文档）；屏幕上出现题目时，助手页“屏幕与讲稿”里会出现“回答屏幕上的题目”。")
            Note("讲稿跟随：把 PPT / Keynote / PDF 导入当前场景的资料，放映时自动认出当前页，提前写好这一页的要点、过渡句和听众可能问的问题，并勾掉你已经讲过的要点；回答提问时会关联当前页。需要“屏幕与系统音频录制”权限。")
            if let error = insights.screenError { Note(error, color: .orange) }
        }
        SettingsSection(title: "更多交给模型的判断", icon: "sparkle.magnifyingglass") {
            Toggle("回答进度：边听你说，勾掉已经讲过的句子，提示下一句", isOn: $insights.answerProgress)
                .onChange(of: insights.answerProgress) { _, _ in insights.save() }
            Toggle("按问题类型调整回答（行为题用 STAR、编程题给代码和复杂度…），只带用得上的技能", isOn: $insights.shapeAnswers)
                .onChange(of: insights.shapeAnswers) { _, _ in insights.save() }
            Toggle("记下待办、决定、时间节点和课堂重点", isOn: $insights.liveNotesOn)
                .onChange(of: insights.liveNotesOn) { _, _ in insights.save() }
            Toggle("对方收尾时提醒停止并复盘", isOn: $insights.wrapUpHint)
                .onChange(of: insights.wrapUpHint) { _, _ in insights.save() }
            Toggle("联网核对：联网结果没有新内容时保留快速回答", isOn: $insights.webCheck)
                .onChange(of: insights.webCheck) { _, _ in insights.save() }
            Note(modelReady
                 ? "这些判断每次只要几十毫秒；问题类型判断最多等 0.35 秒，模型较慢（每次超过 0.3 秒）时自动跳过，不拖慢回答。"
                 : "需要上面的决策模型运行。没有模型时，讲稿跟随和回答进度按文字重合度判断，其余几项不生效。")
        }
    }
}
