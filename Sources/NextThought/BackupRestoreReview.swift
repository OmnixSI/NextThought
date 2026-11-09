import AppKit
import SwiftUI
import NextThoughtCore

/// A real review step shared by portable imports and local recovery points.
@MainActor
final class BackupRestoreReview: NSObject, NSWindowDelegate {
    private static var current: BackupRestoreReview?
    private let prepared: DataBackup.PreparedImport
    private var window: NSWindow?
    private var applying = false
    private init(_ prepared: DataBackup.PreparedImport) { self.prepared = prepared }

    static func present(_ prepared: DataBackup.PreparedImport, applySettings: Bool, state: AppState) {
        current?.finish()
        let review = BackupRestoreReview(prepared); current = review
        let parent = NSApp.keyWindow
        let window = NSWindow(contentRect: NSRect(x: 0, y: 0, width: 820, height: 640), styleMask: [.titled, .closable, .resizable], backing: .buffered, defer: false)
        window.title = String(localized: "导入与恢复预览"); window.minSize = NSSize(width: 700, height: 540)
        window.isReleasedWhenClosed = false; window.delegate = review
        review.window = window
        window.contentView = NSHostingView(rootView: BackupRestoreReviewView(prepared: prepared, applySettings: applySettings, onCancel: { review.finish() }) { sessions, templates, settings in
            review.applying = true
            state.isDataBusy = true
            defer { state.isDataBusy = false; review.applying = false }
            do {
                let summary = try await DataBackup.applyImport(prepared, useBackupSessions: sessions, useBackupTemplates: templates, applySettings: settings)
                state.rebuildContextIndex(); state.dataStatus = summary.description
                state.dataStatusTone = .success; state.dataStatusCanRetryBackup = false
                review.finish()
            } catch {
                state.archive.contentDidChange(); state.archive.load(); state.rebuildContextIndex()
                throw error
            }
        }.preferredColorScheme(state.colorScheme))
        if let parent { parent.beginSheet(window) }
        else { window.center(); window.makeKeyAndOrderFront(nil) }
    }
    func windowWillClose(_ notification: Notification) { prepared.discard(); window?.contentView = nil; window = nil; Self.current = nil }
    func windowShouldClose(_ sender: NSWindow) -> Bool { !applying }
    private func finish() {
        guard let window else { return }
        window.sheetParent?.endSheet(window); window.close()
    }
}

private struct BackupRestoreReviewView: View {
    let prepared: DataBackup.PreparedImport
    @State var applySettings: Bool
    let onCancel: () -> Void
    let onApply: (Set<UUID>, Set<String>, Bool) async throws -> Void
    @State private var selectedSessions = Set<UUID>()
    @State private var selectedTemplates = Set<String>()
    @State private var busy = false
    @State private var failure: String?

    var body: some View {
        VStack(alignment: .leading, spacing: Space.s4) {
            Text("导入与恢复预览").font(Typo.section)
            Text("新增内容会导入；相同内容会跳过。有冲突时默认保留本机，只有明确选择后才使用备份。")
                .font(Typo.body).foregroundStyle(Ink.secondary)
            HStack(spacing: Space.s4) {
                stat("新增记录", prepared.sessions.filter { $0.status == .new }.count)
                stat("相同记录", prepared.sessions.filter { $0.status == .identical }.count)
                stat("冲突记录", prepared.sessions.filter { $0.status == .conflict }.count)
                stat("备份模板", prepared.templates.count)
            }
            Text("项目与课程按修改时间合并；应用前会先创建本机恢复点。模板和个人笔记属于数据，始终参与恢复。")
                .font(Typo.caption).foregroundStyle(Ink.secondary)
            ScrollView {
                LazyVStack(alignment: .leading, spacing: Space.s3) {
                    ForEach(prepared.sessions) { item in
                        HStack(alignment: .top, spacing: 12) {
                            VStack(alignment: .leading, spacing: 4) {
                                Text(item.title).font(Typo.label).textSelection(.enabled)
                                Text(LocalizedStringKey(status(item.status))).font(Typo.caption).foregroundStyle(item.status == .conflict ? Ink.caution : Ink.secondary)
                            }
                            Spacer()
                            if item.status == .conflict {
                                Toggle("使用备份", isOn: Binding(get: { selectedSessions.contains(item.id) }, set: { value in
                                    if value { selectedSessions.insert(item.id) } else { selectedSessions.remove(item.id) }
                                })).toggleStyle(.checkbox).font(Typo.label)
                            }
                        }.padding(12).background(Ink.sunken, in: RoundedRectangle(cornerRadius: 8))
                    }
                    if !prepared.templates.isEmpty { Text("自定义纪要模板").font(Typo.label).padding(.top, 8) }
                    ForEach(prepared.templates) { item in
                        HStack {
                            VStack(alignment: .leading, spacing: 4) {
                                Text(item.title).font(Typo.label)
                                Text(LocalizedStringKey(status(item.status))).font(Typo.caption).foregroundStyle(Ink.secondary)
                            }
                            Spacer()
                            if item.status == .conflict {
                                Toggle("使用备份", isOn: Binding(get: { selectedTemplates.contains(item.id) }, set: { value in
                                    if value { selectedTemplates.insert(item.id) } else { selectedTemplates.remove(item.id) }
                                })).toggleStyle(.checkbox).font(Typo.label)
                            }
                        }.padding(12).background(Ink.sunken, in: RoundedRectangle(cornerRadius: 8))
                    }
                    if prepared.sessions.isEmpty && prepared.templates.isEmpty {
                        Text("此备份没有会议记录或自定义模板；项目资料仍会按规则合并。")
                            .font(Typo.body).foregroundStyle(Ink.secondary)
                    }
                }
            }
            Toggle("同时导入偏好设置", isOn: $applySettings).font(Typo.label)
            if let failure { Text(failure).font(Typo.body).foregroundStyle(Ink.caution).textSelection(.enabled) }
            HStack {
                if busy { ProgressView().controlSize(.small); Text("正在恢复，请保留此窗口…").font(Typo.caption) }
                Spacer()
                Button("取消", action: onCancel).buttonStyle(SecondaryButtonStyle()).disabled(busy)
                Button("应用所选内容") {
                    busy = true; failure = nil
                    Task {
                        do { try await onApply(selectedSessions, selectedTemplates, applySettings) }
                        catch { failure = String(localized: "恢复未全部完成：\(error.localizedDescription) 已有内容请以实际结果为准；如已开始应用，可从恢复前的恢复点找回。"); busy = false }
                    }
                }.buttonStyle(PrimaryButtonStyle()).disabled(busy)
            }
        }.padding(24).background(Ink.paper).disabled(busy)
    }
    private func stat(_ title: String, _ count: Int) -> some View {
        VStack(alignment: .leading, spacing: 4) { Text("\(count)").font(Typo.section); Text(LocalizedStringKey(title)).font(Typo.caption).foregroundStyle(Ink.secondary) }
    }
    private func status(_ status: BackupImportStatus) -> String {
        switch status { case .new: "新增 · 将导入"; case .identical: "内容相同 · 跳过"; case .conflict: "内容不同 · 默认保留本机" }
    }
}
