import SwiftUI
import NextThoughtCore

/// Finder-style information sheet for one archived meeting.
struct SessionInfoView: View {
    @Environment(\.dismiss) private var dismiss
    let sessionID: UUID

    @State private var detail: SessionDetail?
    @State private var storage: SessionStorageInfo?

    private var archive: SessionArchive { SessionArchive.shared }
    private var meta: SessionMeta? {
        detail?.meta ?? archive.sessions.first { $0.id == sessionID }
    }

    var body: some View {
        VStack(spacing: 0) {
            HStack(spacing: Space.s2) {
                Image(systemName: "info.circle").foregroundStyle(Ink.secondary)
                Text("详细信息").font(Typo.section)
                Spacer()
                Button("完成") { dismiss() }.buttonStyle(SecondaryButtonStyle())
            }
            .padding(.horizontal, Space.s6)
            .frame(height: 54)
            .overlay(alignment: .bottom) { Rectangle().fill(Ink.hairline).frame(height: 1) }

            if let meta {
                ScrollView {
                    VStack(alignment: .leading, spacing: Space.s6) {
                        VStack(alignment: .leading, spacing: Space.s2) {
                            Text(verbatim: meta.displayTitle).font(Typo.display)
                            Text(String(localized: String.LocalizationValue(stringLiteral: meta.mode.title)))
                                .font(Typo.caption).foregroundStyle(Ink.secondary)
                        }
                        infoGroup("会议") {
                            infoRow("开始时间", meta.startedAt.formatted(date: .long, time: .shortened))
                            durationRow(meta)
                            infoRow("发言人", speakerDescription(meta))
                        }
                        infoGroup("纪要") {
                            infoRow("状态", notesDescription(meta))
                        }
                        infoGroup("存储") {
                            storageRows
                            Text("大小按本会议存储目录及其子目录中的普通文件总字节数统计。")
                                .font(.system(size: 11)).foregroundStyle(Ink.tertiary)
                        }
                        infoGroup("归档") {
                            infoRow("写入状态", persistenceDescription)
                            if let snapshot = archive.persistenceBySession[sessionID] {
                                if snapshot.unqueuedWriteCount > 0 {
                                    Text(snapshot.dataLossAcknowledged
                                         ? String(localized: "已确认：有 \(snapshot.unqueuedWriteCount) 项内容未能入队，因此未保存。")
                                         : String(localized: "有 \(snapshot.unqueuedWriteCount) 项内容未能入队，因此未保存。"))
                                        .font(Typo.caption).foregroundStyle(Ink.caution)
                                        .fixedSize(horizontal: false, vertical: true)
                                }
                                if snapshot.canAcknowledgeDataLoss {
                                    Button("确认缺失并继续保存") { archive.acknowledgeDataLossAndResume(sessionID) }
                                        .buttonStyle(SecondaryButtonStyle())
                                } else if snapshot.state == .blocked, snapshot.pendingEvents > 0 {
                                    Button("重试保存") { archive.retryPersistence(sessionID) }
                                        .buttonStyle(SecondaryButtonStyle())
                                }
                            }
                            if let detail, !detail.readIssues.isEmpty {
                                ForEach(Array(detail.readIssues.enumerated()), id: \.offset) { item in
                                    infoRow("读取提醒", item.element.message)
                                }
                            }
                        }
                    }
                    .frame(maxWidth: 520, alignment: .leading)
                    .padding(Space.s6)
                    .frame(maxWidth: .infinity, alignment: .topLeading)
                }
            } else {
                ContentUnavailableView("会议不可用", systemImage: "exclamationmark.circle",
                                       description: Text("这条会议记录已不存在或无法读取。"))
            }
        }
        .frame(width: 500, height: 540)
        .background(Ink.paper)
        .task(id: sessionID) {
            _ = await archive.flush(sessionID: sessionID)
            async let loadedDetail = archive.detail(sessionID)
            async let loadedStorage = archive.storageInfo(sessionID)
            detail = await loadedDetail
            storage = await loadedStorage
        }
    }

    @ViewBuilder
    private func durationRow(_ meta: SessionMeta) -> some View {
        if let endedAt = meta.endedAt {
            infoRow("时长", Self.durationText(endedAt.timeIntervalSince(meta.startedAt)))
        } else if sessionID == archive.activeID {
            TimelineView(.periodic(from: .now, by: 1)) { context in
                infoRow("时长", String(localized: "进行中 · \(Self.durationText(context.date.timeIntervalSince(meta.startedAt)))"))
            }
        } else {
            infoRow("时长", String(localized: "不可用（会议尚未结束）"))
        }
    }

    private func speakerDescription(_ meta: SessionMeta) -> String {
        guard let detail else { return String(localized: "正在读取…") }
        var names: [String] = []
        for line in detail.lines where !names.contains(line.speaker.label) {
            names.append(line.speaker.label)
        }
        if !names.isEmpty { return names.joined(separator: "、") }
        if meta.transcriptCount > 0 { return String(localized: "不可用（转写数据无法读取）") }
        return String(localized: "暂无可识别发言人")
    }

    private func notesDescription(_ meta: SessionMeta) -> String {
        guard let detail else { return String(localized: "正在读取…") }
        if detail.notes != nil { return String(localized: "已有纪要（结构化内容）") }
        if detail.summary != nil { return String(localized: "已有纪要（Markdown 内容）") }
        switch meta.summaryState {
        case .none: return String(localized: "没有纪要")
        case .pending where archive.summarizing.contains(meta.id): return String(localized: "正在整理纪要")
        case .pending: return String(localized: "整理已中断，可重试")
        case .failed: return String(localized: "整理失败：\(meta.summaryError ?? String(localized: "原因不可用"))")
        case .ready: return String(localized: "不可用（状态显示已完成，但纪要内容无法读取）")
        }
    }

    @ViewBuilder
    private var storageRows: some View {
        switch storage {
        case .some(.available(let path, let bytes)):
            infoRow("存储位置", path)
            infoRow("存储大小", ByteCountFormatter.string(fromByteCount: bytes, countStyle: .file))
        case .some(.missing(let path)):
            infoRow("存储位置", String(localized: "\(path)（目录不存在）"))
            infoRow("存储大小", String(localized: "不可用（会议目录不存在）"))
        case .some(.unavailable(let path)):
            infoRow("存储位置", String(localized: "\(path)（无法读取）"))
            infoRow("存储大小", String(localized: "不可用（无权限或读取失败）"))
        case .some(.demo):
            infoRow("存储位置", String(localized: "演示数据没有实际存储目录"))
            infoRow("存储大小", String(localized: "不可用（演示数据没有实际文件）"))
        case .none:
            infoRow("存储位置", String(localized: "正在读取…"))
            infoRow("存储大小", String(localized: "正在统计…"))
        }
    }

    private var persistenceDescription: String {
        if case .some(.demo) = storage { return String(localized: "演示数据没有实际存储目录。") }
        guard let snapshot = archive.persistenceBySession[sessionID] else {
            return String(localized: "归档已读取；当前没有待写入项。")
        }
        switch snapshot.state {
        case .saved:
            if let date = snapshot.lastPersistedAt {
                return String(localized: "已保存 · 最近 \(date.formatted(date: .omitted, time: .shortened))")
            }
            return String(localized: "已保存")
        case .saving:
            return String(localized: "正在保存 · 还有 \(snapshot.pendingEvents) 项（\(ByteCountFormatter.string(fromByteCount: Int64(snapshot.pendingBytes), countStyle: .file))）")
        case .blocked:
            return snapshot.lastFailure ?? String(localized: "保存受阻")
        }
    }

    private func infoGroup<Content: View>(_ title: String, @ViewBuilder content: () -> Content) -> some View {
        VStack(alignment: .leading, spacing: Space.s2) {
            Text(String(localized: String.LocalizationValue(stringLiteral: title)))
                .font(Typo.label).foregroundStyle(Ink.secondary)
            VStack(alignment: .leading, spacing: Space.s2, content: content)
                .padding(12)
                .frame(maxWidth: .infinity, alignment: .leading)
                .cardSurface(cornerRadius: Space.radiusSmall)
        }
    }

    private func infoRow(_ title: String, _ value: String) -> some View {
        HStack(alignment: .firstTextBaseline, spacing: Space.s3) {
            Text(String(localized: String.LocalizationValue(stringLiteral: title)))
                .font(Typo.caption).foregroundStyle(Ink.secondary)
                .frame(width: 72, alignment: .leading)
            Text(verbatim: value).font(Typo.body).textSelection(.enabled)
                .frame(maxWidth: .infinity, alignment: .leading)
                .fixedSize(horizontal: false, vertical: true)
        }
    }

    private static func durationText(_ interval: TimeInterval) -> String {
        let seconds = max(0, Int(interval.rounded()))
        let hours = seconds / 3_600
        let minutes = (seconds % 3_600) / 60
        let remainder = seconds % 60
        if hours > 0 { return String(localized: "\(hours) 小时 \(minutes) 分钟") }
        if minutes > 0 { return String(localized: "\(minutes) 分钟 \(remainder) 秒") }
        return String(localized: "\(remainder) 秒")
    }
}
