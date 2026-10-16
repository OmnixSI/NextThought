import AppKit
import InterviewHackerCore
import SwiftUI

/// Settings → 判断模型: the StartLux-Decision model that takes over the rule-based
/// judgments (is this a question for me, does it need the web, is this mic line mine,
/// are these the same question).
struct DecisionSettings: View {
    @Environment(AppState.self) private var state

    var body: some View {
        @Bindable var decisions = state.decisions
        SettingsSection(title: "StartLux 决策模型", icon: "brain") {
            Toggle("用决策模型做判断", isOn: $decisions.enabled)
                .onChange(of: decisions.enabled) { _, on in
                    decisions.save()
                    if on { Task { await decisions.start() } } else { decisions.stop() }
                }
            Note("原来靠正则和关键词规则做的判断，交给专门做“选择题”的小模型：一次读出每个选项的概率，几十毫秒出结果，不写长文。规则仍是兜底：模型没开、没启动、超时（约 1 秒）或出错时，照常按规则处理，不会拖慢回答。")
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
        DecisionEvaluationSection()
        SettingsSection(title: "许可与隐私", icon: "doc.text") {
            Note("模型权重使用 CC BY-NC 4.0 许可，仅限非商业用途。官方基准以英文为主，中文效果请用上面的“评测”在这台 Mac 上实测。")
            Note("在这台 Mac 上运行时，对话文本只发给本机 127.0.0.1 的服务，不会上传。连接其他服务时，文本会发给那台机器。")
            Link("项目主页与论文", destination: URL(string: "https://decision.startlux.com/")!).font(.system(size: 12))
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
                Text("模型大小").font(.system(size: 12, weight: .medium))
                Spacer()
                Picker("精度", selection: $decisions.bits) {
                    Text("8-bit（推荐）").tag(8)
                    Text("4-bit（更小）").tag(4)
                }
                .labelsHidden().pickerStyle(.segmented).frame(width: 220)
                .onChange(of: decisions.bits) { _, _ in decisions.save(); decisions.stop() }
            }
            ForEach(DecisionModelVariant.allCases) { variant in
                DecisionVariantRow(variant: variant, bits: decisions.bits, selected: decisions.variant == variant,
                                   recommended: recommended.variant == variant && recommended.bits == decisions.bits,
                                   installed: decisions.isInstalled(variant, bits: decisions.bits)) {
                    decisions.variant = variant
                    decisions.save()
                    decisions.stop()
                }
            }
            Note(String(format: "这台 Mac 有 %.0f GB 内存，推荐 %@ %d-bit。大小为估算值，以下载时显示的为准。首次下载还会安装约 1 GB 的 Python 运行环境（需要 uv 或 Python 3.10 以上），都放在应用数据目录里。", memory, recommended.variant.rawValue, recommended.bits))
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
                Text(variant.rawValue).font(.system(size: 13, weight: .semibold)).frame(width: 40, alignment: .leading)
                VStack(alignment: .leading, spacing: 2) {
                    HStack(spacing: 6) {
                        Text(String(format: "约 %.1f GB 磁盘 · 运行约 %.0f GB 内存 · 准确度 %.1f", disk, memory, variant.decisionIndex))
                            .font(.system(size: 12)).monospacedDigit()
                        if recommended { DecisionBadge("推荐", color: Brand.accent) }
                        if installed { DecisionBadge("已下载", color: .green) }
                    }
                    Text(variant.note).font(.system(size: 11)).foregroundStyle(.secondary)
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
                        Button { decisions.install() } label: { Label("下载并启动 \(decisions.variant.rawValue)", systemImage: "arrow.down.circle") }
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
                    Text("正在启动（首次加载模型约半分钟）…").font(.system(size: 12)).foregroundStyle(.secondary)
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
        case .notInstalled: decisions.source == .local ? "还没有下载 \(decisions.variant.rawValue) \(decisions.bits)-bit。" : "未连接"
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
