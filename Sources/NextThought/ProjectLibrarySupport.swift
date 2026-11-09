import AppKit
import Quartz
import SwiftUI
import NextThoughtCore

struct WorkspaceRecordsTab: View {
    @Environment(AppState.self) private var state
    private var records: [SessionMeta] { state.archive.sessions.filter { $0.workspaceID == state.activeWorkspace.id }.sorted { $0.startedAt > $1.startedAt } }
    var body: some View {
        VStack(alignment: .leading, spacing: Space.s4) {
            Text("记录").font(Typo.section)
            if records.isEmpty {
                ContentUnavailableView("还没有记录", systemImage: "doc.text", description: Text("从这个项目开始一场研究讨论，记录和资料会保存在一起。"))
            }
            ForEach(records) { meta in
                Button {
                    state.historyWorkspaceFilter = state.activeWorkspace.id
                    state.historySelection = meta.id
                    state.select(.meetings)
                } label: {
                    SessionRow(meta: meta, selected: false, active: meta.id == state.archive.activeID)
                        .frame(maxWidth: .infinity, alignment: .leading)
                }.buttonStyle(InteractiveRowButtonStyle())
            }
        }
    }
}

/// Native Quick Look keeps large PDFs lazy while the extracted text can be checked beside them.
struct MaterialReviewSheet: View {
    let document: ContextDocument
    @Environment(\.dismiss) private var dismiss
    @State private var previewText: String?
    @State private var previewTruncated = false
    private var original: URL? {
        guard let hash = document.content?.sourceHash, hash.count == 64, hash.allSatisfy(\.isHexDigit),
              let format = document.sourceFormat, !format.contains("/"), !format.contains("..") else { return nil }
        let url = AppPaths.root.appendingPathComponent("Originals").appendingPathComponent(hash + "." + format)
        return FileManager.default.fileExists(atPath: url.path) ? url : nil
    }
    var body: some View {
        VStack(alignment: .leading, spacing: Space.s4) {
            HStack {
                Text(document.name).font(Typo.section).lineLimit(2)
                Spacer()
                Button("完成") { dismiss() }.keyboardShortcut(.cancelAction).buttonStyle(ChipButtonStyle())
            }
            if let warnings = document.content?.warnings, !warnings.isEmpty {
                VStack(alignment: .leading, spacing: 5) {
                    Label("这份资料需要核对", systemImage: "exclamationmark.triangle").font(Typo.label)
                    ForEach(Array(warnings.enumerated()), id: \.offset) { _, warning in Text(warning).font(Typo.body).textSelection(.enabled) }
                }.foregroundStyle(Ink.caution).padding(12).frame(maxWidth: .infinity, alignment: .leading).background(Ink.sunken, in: RoundedRectangle(cornerRadius: 8))
            }
            HSplitView {
                VStack(alignment: .leading, spacing: 10) {
                    HStack {
                        Text("原件").font(Typo.label)
                        Spacer()
                        if let original { Button("用系统应用打开") { NSWorkspace.shared.open(original) }.buttonStyle(ChipButtonStyle()) }
                    }
                    if let original { NativeMaterialPreview(url: original) }
                    else { ContentUnavailableView("原件暂不可用", systemImage: "doc.questionmark", description: Text("提取内容仍已保留。可重新导入原文件补齐原件。")) }
                }.frame(minWidth: 300, maxWidth: .infinity, maxHeight: .infinity).padding(8)
                VStack(alignment: .leading, spacing: 10) {
                    HStack {
                        Text("提取内容").font(Typo.label)
                        Spacer()
                        if let path = document.markdownPath, let file = MaterialArchive.resolve(path) {
                            Button("打开完整文本") { NSWorkspace.shared.open(file) }.buttonStyle(ChipButtonStyle())
                        }
                    }
                    ScrollView {
                        if let previewText {
                            Text(verbatim: previewText).font(Typo.body).textSelection(.enabled)
                                .frame(maxWidth: .infinity, alignment: .leading).padding(12)
                        } else { ProgressView("正在准备提取预览…").padding() }
                        if previewTruncated { Text("此处预览前 80,000 字符，完整提取已保存在本机。可打开完整文本核对。").font(Typo.caption).foregroundStyle(Ink.secondary) }
                    }.background(Ink.sunken, in: RoundedRectangle(cornerRadius: 8))
                }.frame(minWidth: 300, maxWidth: .infinity, maxHeight: .infinity).padding(8)
            }
        }.padding(20).frame(minWidth: 900, idealWidth: 1080, minHeight: 600, idealHeight: 720).background(Ink.paper)
        .task(id: document.id) {
            let result = await Task.detached(priority: .userInitiated) {
                let text = document.content?.markdown ?? document.pages.joined(separator: "\n\n")
                return (String(text.prefix(80_000)), text.count > 80_000)
            }.value
            guard !Task.isCancelled else { return }
            previewText = result.0; previewTruncated = result.1
        }
    }
}

private struct NativeMaterialPreview: NSViewRepresentable {
    let url: URL
    func makeNSView(context: Context) -> QLPreviewView { QLPreviewView(frame: .zero, style: .normal) }
    func updateNSView(_ view: QLPreviewView, context: Context) {
        if (view.previewItem as? NSURL) != url as NSURL { view.previewItem = url as NSURL }
    }
}
