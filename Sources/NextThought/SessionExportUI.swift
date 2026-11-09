import AppKit
import PDFKit
import SwiftUI
import UniformTypeIdentifiers
import NextThoughtCore

struct SessionExportRequest: Identifiable {
    let id = UUID()
    let detail: SessionDetail
}

/// Selects the same projection text that Save writes, then chooses its preview
/// presentation: rendered pages for document formats, source text for subtitles.
enum SessionExportPreviewContent: Equatable {
    case pages(markdown: String)
    case text(String)

    var sourceText: String {
        switch self {
        case let .pages(markdown): markdown
        case let .text(text): text
        }
    }

    var usesPageLayout: Bool {
        if case .pages = self { return true }
        return false
    }

    static func make(format: SessionExportFormat, meta: SessionMeta,
                     projection: SessionTextProjection) -> SessionExportPreviewContent {
        switch format {
        case .markdown, .docx, .pdf:
            .pages(markdown: SessionMarkdownExporter.export(meta: meta, projection: projection))
        case .srt:
            .text(SessionSubtitleExporter.export(meta: meta, projection: projection, format: .srt).text)
        case .vtt:
            .text(SessionSubtitleExporter.export(meta: meta, projection: projection, format: .vtt).text)
        }
    }
}

struct SessionExportSheet: View {
    @Environment(AppState.self) private var state
    @Environment(\.dismiss) private var dismiss

    let request: SessionExportRequest
    @State private var format: SessionExportFormat = .markdown
    @State private var transcriptVariant: SessionTranscriptVariant = .corrected
    @State private var languageView: SessionLanguageView = .bilingual
    @State private var status = ""
    @State private var pagePreviewDocument: PDFDocument?
    @State private var pagePreviewError = ""

    private var detail: SessionDetail { request.detail }
    private var projection: SessionTextProjection {
        SessionProjectionBuilder.build(meta: detail.meta, lines: detail.lines, answers: detail.answers,
                                       detected: detail.questions, summary: detail.summary, notes: detail.notes,
                                       options: SessionProjectionOptions(transcriptVariant: transcriptVariant,
                                                                         languageView: languageView))
    }
    private var previewContent: SessionExportPreviewContent {
        SessionExportPreviewContent.make(format: format, meta: detail.meta, projection: projection)
    }
    private var previewText: String { previewContent.sourceText }
    private var subtitleExport: SessionSubtitleExport? {
        switch format {
        case .srt: SessionSubtitleExporter.export(meta: detail.meta, projection: projection, format: .srt)
        case .vtt: SessionSubtitleExporter.export(meta: detail.meta, projection: projection, format: .vtt)
        default: nil
        }
    }
    private var baseName: String {
        (detail.summary?.title ?? detail.meta.displayTitle)
            .replacingOccurrences(of: "/", with: "-")
            .replacingOccurrences(of: ":", with: "-")
    }

    var body: some View {
        VStack(alignment: .leading, spacing: Space.s4) {
            HStack(alignment: .firstTextBaseline) {
                VStack(alignment: .leading, spacing: Space.s1) {
                    Text("导出会议").font(Typo.section)
                    Text(verbatim: detail.meta.displayTitle).font(Typo.caption).foregroundStyle(Ink.secondary)
                        .lineLimit(1)
                }
                Spacer()
                Button("关闭") { dismiss() }.buttonStyle(GhostButtonStyle())
            }

            HStack(alignment: .top, spacing: Space.s3) {
                Picker("格式", selection: $format) {
                    ForEach(SessionExportFormat.allCases) { item in Text(item.title).tag(item) }
                }
                .frame(width: 180)
                Picker("转写", selection: $transcriptVariant) {
                    Text("当前校正版本").tag(SessionTranscriptVariant.corrected)
                    Text("原始记录").tag(SessionTranscriptVariant.original)
                }
                .frame(width: 180)
                Picker("语言", selection: $languageView) {
                    Text("原文与译文").tag(SessionLanguageView.bilingual)
                    Text("仅原文").tag(SessionLanguageView.source)
                    Text("译文优先").tag(SessionLanguageView.translated)
                }
                .frame(width: 160)
            }

            HStack(spacing: Space.s2) {
                Image(systemName: "info.circle").foregroundStyle(Ink.secondary)
                Text(format == .srt || format == .vtt
                     ? String(localized: "预览使用同一版转写投影；字幕时间来自语句起止时间，缺少结束时间时会标明估算。")
                     : String(localized: "页面预览与导出使用同一版校正、译文和纪要内容。"))
                    .font(Typo.caption).foregroundStyle(Ink.secondary)
                    .fixedSize(horizontal: false, vertical: true)
            }

            if !detail.readIssues.isEmpty {
                VStack(alignment: .leading, spacing: Space.s1) {
                    Label("归档有缺失或读取错误，导出已暂停。", systemImage: "exclamationmark.triangle.fill")
                        .font(Typo.label).foregroundStyle(Ink.caution)
                    ForEach(Array(detail.readIssues.enumerated()), id: \.offset) { entry in
                        Text(verbatim: entry.element.message).font(Typo.caption).foregroundStyle(Ink.secondary)
                    }
                }
                .padding(Space.s3)
                .background(Ink.caution.opacity(0.08), in: RoundedRectangle(cornerRadius: Space.radiusSmall))
            }

            if isDocumentFormat, projection.derivedContentStatus != .current {
                Label(projection.derivedContentStatus == .stale
                      ? "纪要或手记可能已过期；导出预览已附上核对提示。"
                      : "旧纪要未记录来源版本；导出预览会说明无法确认是否过期。",
                      systemImage: projection.derivedContentStatus == .stale ? "exclamationmark.triangle.fill" : "questionmark.circle")
                    .font(Typo.caption)
                    .foregroundStyle(projection.derivedContentStatus == .stale ? Ink.caution : Ink.secondary)
                    .fixedSize(horizontal: false, vertical: true)
            }

            if let subtitleExport {
                HStack(spacing: Space.s3) {
                    if subtitleExport.estimatedTimingCount > 0 {
                        Label("\(subtitleExport.estimatedTimingCount) 段结束时间为估算值", systemImage: "clock")
                    }
                    if subtitleExport.staleTranslationCount > 0 {
                        Label("\(subtitleExport.staleTranslationCount) 条译文可能已过期", systemImage: "arrow.triangle.2.circlepath")
                    }
                }
                .font(Typo.caption).foregroundStyle(Ink.caution)
                .fixedSize(horizontal: false, vertical: true)
            }

            previewSurface

            HStack {
                Text(verbatim: status).font(Typo.caption).foregroundStyle(Ink.secondary).lineLimit(2)
                Spacer()
                Button("导出…", action: save)
                    .buttonStyle(PrimaryButtonStyle())
                    .disabled(!detail.readIssues.isEmpty || previewText.isEmpty)
                    .keyboardShortcut(.defaultAction)
            }
        }
        .padding(Space.s6)
        .frame(minWidth: 760, idealWidth: 860, minHeight: 520, idealHeight: 620)
        .task(id: previewContent) {
            await updatePagePreview(for: previewContent)
        }
    }

    @ViewBuilder
    private var previewSurface: some View {
        Group {
            if previewContent.usesPageLayout {
                if let pagePreviewDocument {
                    SessionPDFPagePreview(document: pagePreviewDocument)
                        .accessibilityLabel("按页排版的文档预览")
                } else if !pagePreviewError.isEmpty {
                    VStack(alignment: .leading, spacing: Space.s2) {
                        Label("分页预览暂不可用，以下显示导出源文本。", systemImage: "exclamationmark.triangle")
                            .font(Typo.caption).foregroundStyle(Ink.caution)
                        ScrollView {
                            Text(verbatim: previewText)
                                .font(Typo.caption)
                                .textSelection(.enabled)
                                .frame(maxWidth: .infinity, alignment: .leading)
                                .padding(Space.s4)
                        }
                    }
                } else {
                    ProgressView("正在生成分页预览…")
                        .frame(maxWidth: .infinity, maxHeight: .infinity)
                }
            } else {
                ScrollView {
                    Text(verbatim: previewText.isEmpty ? String(localized: "没有可导出的内容。") : previewText)
                        .font(.system(size: 11, design: .monospaced))
                        .textSelection(.enabled)
                        .frame(maxWidth: .infinity, alignment: .leading)
                        .padding(Space.s4)
                }
            }
        }
        .frame(minHeight: 260, maxHeight: 430)
        .background(Ink.sunken, in: RoundedRectangle(cornerRadius: Space.radiusSmall))
        .overlay(RoundedRectangle(cornerRadius: Space.radiusSmall).strokeBorder(Ink.hairline, lineWidth: 1))
    }

    @MainActor
    private func updatePagePreview(for content: SessionExportPreviewContent) async {
        guard case let .pages(markdown) = content else {
            pagePreviewDocument = nil
            pagePreviewError = ""
            return
        }
        guard !markdown.isEmpty else {
            pagePreviewDocument = nil
            pagePreviewError = ""
            return
        }
        pagePreviewDocument = nil
        pagePreviewError = ""
        do {
            let data = try await Task.detached(priority: .userInitiated) {
                try SessionDocumentRenderer.pdfData(for: markdown)
            }.value
            try Task.checkCancellation()
            guard let document = PDFDocument(data: data) else {
                throw CocoaError(.fileReadCorruptFile)
            }
            pagePreviewDocument = document
        } catch is CancellationError {
            return
        } catch {
            pagePreviewError = error.localizedDescription
        }
    }

    private func save() {
        let panel = NSSavePanel()
        panel.allowedContentTypes = [contentType(for: format)]
        panel.nameFieldStringValue = "\(baseName).\(format.fileExtension)"
        panel.isExtensionHidden = false
        panel.canCreateDirectories = true
        guard panel.runModal() == .OK, let selectedURL = panel.url else { return }
        let url = selectedURL.pathExtension.lowercased() == format.fileExtension
            ? selectedURL
            : selectedURL.deletingPathExtension().appendingPathExtension(format.fileExtension)
        do {
            try SessionFileExporter.write(content: previewText, format: format, to: url)
            status = String(localized: "已导出：\(url.lastPathComponent)")
        } catch {
            state.errorMessage = String(localized: "导出失败：\(error.localizedDescription)")
        }
    }

    private func contentType(for format: SessionExportFormat) -> UTType {
        switch format {
        case .pdf: .pdf
        case .docx: UTType(filenameExtension: "docx") ?? .data
        case .markdown: UTType(filenameExtension: "md") ?? .plainText
        case .srt: UTType(filenameExtension: "srt") ?? .plainText
        case .vtt: UTType(filenameExtension: "vtt") ?? .plainText
        }
    }

    private var isDocumentFormat: Bool {
        format == .markdown || format == .docx || format == .pdf
    }
}

private struct SessionPDFPagePreview: NSViewRepresentable {
    let document: PDFDocument

    func makeNSView(context: Context) -> PDFView {
        let view = PDFView()
        view.displayMode = .singlePageContinuous
        view.displayDirection = .vertical
        view.autoScales = true
        view.backgroundColor = NSColor.controlBackgroundColor
        view.document = document
        return view
    }

    func updateNSView(_ view: PDFView, context: Context) {
        if view.document !== document { view.document = document }
        view.displayMode = .singlePageContinuous
        view.displayDirection = .vertical
        view.autoScales = true
    }
}
