import SwiftUI

/// Native component-state sample sheet used only by the deterministic acceptance renderer.
/// Every transcript, notice, and receipt on this page is synthetic.
struct LiveSessionAcceptanceComponentSamplesView: View {
    private let columns = [GridItem(.flexible(), spacing: Space.s3), GridItem(.flexible(), spacing: Space.s3)]

    var body: some View {
        ScrollView {
            VStack(alignment: .leading, spacing: Space.s4) {
                VStack(alignment: .leading, spacing: Space.s1) {
                    Text("会中组件状态样板").font(LiveSessionTypography.display).foregroundStyle(Ink.ink)
                    Text("原生 SwiftUI 样板 · 所有文本与状态均为合成演示，不访问麦克风、ASR、模型、网络或归档。")
                        .font(LiveSessionTypography.caption).foregroundStyle(Ink.secondary)
                }
                LazyVGrid(columns: columns, alignment: .leading, spacing: Space.s3) {
                    actionButtons
                    dataStates
                    sourceChips
                    transcriptRows
                    taskRows
                    revisionDiff
                    inlineNotices
                    newContentAndComposer
                }
            }
            .padding(Space.s6)
        }
        .frame(width: 1_200, height: 1_080)
        .background(Ink.paper)
        .environment(\.locale, Locale(identifier: "zh-Hans"))
    }

    private var actionButtons: some View {
        sampleCard("ActionButton · default / hover / pressed / focus / unavailable") {
            FlowLayout(spacing: Space.s2, lineSpacing: Space.s2) {
                Button("默认") { }
                    .buttonStyle(PrimaryButtonStyle(scalesWithDynamicType: true))
                Button("悬停") { }
                    .buttonStyle(SecondaryButtonStyle(scalesWithDynamicType: true))
                    .environment(\.liveSessionAcceptanceButtonInteraction, .hover)
                Button("按下") { }
                    .buttonStyle(GhostButtonStyle(scalesWithDynamicType: true))
                    .environment(\.liveSessionAcceptanceButtonInteraction, .pressed)
                Button("焦点") { }
                    .buttonStyle(SecondaryButtonStyle(scalesWithDynamicType: true))
                    .environment(\.liveSessionAcceptanceButtonInteraction, .focused)
                Button("不可用") { }
                    .buttonStyle(SecondaryButtonStyle(scalesWithDynamicType: true))
                    .disabled(true)
            }
            Text("预览态由演示环境注入，不代表鼠标事件录制。")
                .font(LiveSessionTypography.caption).foregroundStyle(Ink.secondary)
        }
    }

    private var dataStates: some View {
        sampleCard("DataStateView · loading / no-match") {
            HStack(spacing: Space.s2) {
                ProgressView().controlSize(.small)
                Text("正在加载本场资料…").font(LiveSessionTypography.body)
            }
            .foregroundStyle(Ink.secondary)
            HStack {
                VStack(alignment: .leading, spacing: Space.s1) {
                    Text("没有匹配到「数据库迁移」").font(LiveSessionTypography.label)
                    Text("保留查询与范围；可扩大范围或清除查询。")
                        .font(LiveSessionTypography.caption).foregroundStyle(Ink.secondary)
                }
                Spacer(minLength: 0)
                Button("清除查询") { }
                    .buttonStyle(GhostButtonStyle(height: 28, scalesWithDynamicType: true))
            }
            .padding(Space.s2)
            .background(Ink.sunken, in: RoundedRectangle(cornerRadius: Space.radiusSmall))
        }
    }

    private var sourceChips: some View {
        sampleCard("SourceChip · current / stale") {
            HStack(spacing: Space.s2) {
                Label("译文 · 当前", systemImage: "checkmark.circle.fill")
                    .font(LiveSessionTypography.caption).foregroundStyle(Ink.secondary)
                Label("译文待更新", systemImage: "exclamationmark.triangle.fill")
                    .font(LiveSessionTypography.caption).foregroundStyle(Ink.caution)
            }
            Text("过期状态来自版本比较；旧内容仍可读。")
                .font(LiveSessionTypography.caption).foregroundStyle(Ink.secondary)
        }
    }

    private var transcriptRows: some View {
        sampleCard("TranscriptRow · final / streaming / selected+focus") {
            transcriptLine("对方", "如果发布窗口需要延长，我们会先核对回滚指标。", color: Ink.ink)
            transcriptLine("对方 · 流式", "再决定是否调整……", color: Ink.secondary)
            HStack(spacing: Space.s2) {
                Text("已选择原话").padding(.horizontal, 6).padding(.vertical, 3).background(Ink.hover, in: Capsule())
                Button("新增 2 句 · 回到现场") { }
                    .buttonStyle(GhostButtonStyle(height: 28, scalesWithDynamicType: true))
                    .environment(\.liveSessionAcceptanceButtonInteraction, .focused)
            }
            .font(LiveSessionTypography.caption)
            Text("选中文字荧光为合成标记；原生 textSelection 高亮未伪造。")
                .font(LiveSessionTypography.caption).foregroundStyle(Ink.secondary)
        }
    }

    private var taskRows: some View {
        sampleCard("TaskRow · busy / warning / saved receipt") {
            HStack {
                ProgressView().controlSize(.small)
                Text("回答正在生成…").font(LiveSessionTypography.body)
                Spacer()
                Button("停止生成") { }
                    .buttonStyle(GhostButtonStyle(height: 28, scalesWithDynamicType: true))
            }
            Label("保存受阻 · 有 3 项待保存 · 重试", systemImage: "exclamationmark.triangle.fill")
                .font(LiveSessionTypography.caption).foregroundStyle(Ink.caution)
            Label("已保存 · 修订 12 · 03:05", systemImage: "checkmark.circle.fill")
                .font(LiveSessionTypography.caption).foregroundStyle(Ink.secondary)
        }
    }

    private var revisionDiff: some View {
        sampleCard("RevisionDiff · stale+manual edit") {
            HStack(alignment: .top, spacing: Space.s2) {
                Text("旧原话：缓存几穿").strikethrough().foregroundStyle(Ink.secondary)
                Image(systemName: "arrow.right").foregroundStyle(Ink.tertiary)
                Text("手动校正：缓存击穿").foregroundStyle(Ink.ink)
            }
            .font(LiveSessionTypography.body)
            Label("旧译文基于修订前原话；手动校正仍保留。", systemImage: "exclamationmark.triangle.fill")
                .font(LiveSessionTypography.caption).foregroundStyle(Ink.caution)
            Text("合成版本差异样例；实际过期标记由归档投影版本比较产生。")
                .font(LiveSessionTypography.caption).foregroundStyle(Ink.secondary)
        }
    }

    private var inlineNotices: some View {
        sampleCard("InlineNotice · error+existing result / cancel+late callback") {
            Label("回答暂时不可用；记录仍在继续。", systemImage: "exclamationmark.triangle.fill")
                .font(LiveSessionTypography.caption).foregroundStyle(Ink.caution)
            Text("之前有效回答仍可读。取消后到达的旧请求结果会被丢弃。")
                .font(LiveSessionTypography.body).foregroundStyle(Ink.ink)
            Text("最近结果：先看命中率和 P95 延迟，再比较灰度前后的错误率。")
                .font(LiveSessionTypography.caption).foregroundStyle(Ink.secondary)
        }
    }

    private var newContentAndComposer: some View {
        sampleCard("NewContentPill / ComposerDock · keyboard contract") {
            Button("新增 6 句 · 回到现场") { }
                .buttonStyle(SecondaryButtonStyle(scalesWithDynamicType: true))
                .environment(\.liveSessionAcceptanceButtonInteraction, .focused)
            HStack(spacing: Space.s2) {
                TextField("输入问题；回车发送", text: .constant(""))
                    .textFieldStyle(.plain)
                    .padding(Space.s2)
                    .background(Ink.surface, in: RoundedRectangle(cornerRadius: Space.radiusSmall))
                ShortcutHint(text: "⌘L", scalesWithDynamicType: true)
                Button("立即回答") { }
                    .buttonStyle(PrimaryButtonStyle(scalesWithDynamicType: true))
            }
            Text("⌘L 聚焦输入 · Return 发送 · ⌘⇧↩ 强制回答 · Esc 取消当前回答 · ↑/PageUp 保持阅读位置")
                .font(LiveSessionTypography.caption).foregroundStyle(Ink.secondary)
        }
    }

    private func transcriptLine(_ speaker: String, _ text: String, color: Color) -> some View {
        HStack(alignment: .firstTextBaseline, spacing: Space.s2) {
            Text(speaker).font(LiveSessionTypography.caption).foregroundStyle(Ink.secondary).frame(width: 78, alignment: .leading)
            Text(text).font(LiveSessionTypography.body).foregroundStyle(color)
        }
    }

    private func sampleCard<Content: View>(_ title: String, @ViewBuilder content: () -> Content) -> some View {
        VStack(alignment: .leading, spacing: Space.s2) {
            Text(title).font(LiveSessionTypography.label).foregroundStyle(Ink.ink)
            content()
        }
        .padding(Space.s3)
        .frame(maxWidth: .infinity, alignment: .leading)
        .background(Ink.surface, in: RoundedRectangle(cornerRadius: Space.radiusMedium, style: .continuous))
        .overlay(RoundedRectangle(cornerRadius: Space.radiusMedium, style: .continuous).strokeBorder(Ink.hairline, lineWidth: 1))
    }
}
