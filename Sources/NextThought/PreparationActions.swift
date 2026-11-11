import Foundation
import NextThoughtCore

@MainActor
extension AppState {
    var draftAvailableDocumentCount: Int {
        guard let series = draftSeries, let plan = draftMeetingPlan else { return 0 }
        return plan.preparedWorkspace(from: series).documents.filter(\.enabled).count
    }
    var draftDeferredDocumentCount: Int {
        draftJobs.filter { $0.documentID == nil && !$0.stage.terminal }.count
    }
    /// Megabytes the local recognition model still needs (at least 1).
    var recognitionModelDownloadMB: Int {
        max(1, missingPackages.reduce(0) { $0 + $1.approximateMegabytes })
    }

    /// The download status in the interface language. The stored text stays Chinese because callers test its prefix.
    var modelDownloadStatusText: String {
        let status = modelDownloadStatus
        if status.hasPrefix("下载失败：") { return String(localized: "下载失败：\(String(status.dropFirst("下载失败：".count)))") }
        if status.hasPrefix("删除模型失败：") { return String(localized: "删除模型失败：\(String(status.dropFirst("删除模型失败：".count)))") }
        return String(localized: String.LocalizationValue(status))
    }

    /// Recognition is the first thing missing and the fix is a model download.
    var needsRecognitionModelDownload: Bool {
        transcriptionBackend == "zipformer" && recordingReadiness.issues.first == .recognition
    }

    /// What the repair button will do, in words: a download is hundreds of megabytes.
    var recordingRepairTitle: String {
        guard let issue = recordingReadiness.issues.first else { return String(localized: "先准备声音") }
        switch issue {
        case .recognition:
            guard transcriptionBackend == "zipformer" else { return String(localized: "设置语音识别") }
            if isDownloadingModel { return String(localized: "正在下载 \(Int(modelDownloadProgress * 100))%") }
            return String(localized: "下载语音识别模型（约 \(recognitionModelDownloadMB) MB）")
        case .microphonePermission:
            return microphoneAccess == .undetermined ? String(localized: "允许麦克风") : String(localized: "打开系统设置")
        case .systemAudioPermission: return String(localized: "允许系统音频")
        case .audioSource: return String(localized: "刷新声音来源")
        }
    }

    /// An online meeting may leave the microphone out: the other side alone can be recorded.
    var canMeetWithoutMicrophone: Bool {
        meetingVenue == .online && microphoneEnabled
    }

    /// "只记录对方": same as the switch in settings, and persisted the same way.
    func continueWithoutMicrophone() {
        microphoneEnabled = false
        updateSettings()
    }

    func useMicrophoneAgain() {
        microphoneEnabled = true
        updateSettings()
    }

    func repairRecordingSetup() {
        guard let issue = recordingReadiness.issues.first else { return }
        switch issue {
        case .recognition:
            if transcriptionBackend == "zipformer" { if !isDownloadingModel { downloadModel() } }
            else { openSettings(.recognition) }
        case .systemAudioPermission: Task { await requestCapturePermission() }
        case .audioSource: Task { await refreshTargets() }
        case .microphonePermission: Task { await requestMicrophoneAccess() }
        }
    }
    func preparationRepairTitle(_ job: DocumentJob) -> String {
        switch job.waitingReason {
        case .runtime, .ocrModels, .semantic: String(localized: "准备所需组件")
        case .codexConnection: String(localized: "连接 Codex")
        case .legacyOffice: String(localized: "查看转换方式")
        case nil: String(localized: "查看资料设置")
        }
    }
    func repairPreparation(_ job: DocumentJob) {
        switch job.waitingReason {
        case .runtime:
            if job.provider == "paddle" {
                if let model = job.localModelID { localOCRModelID = model }
                prepareDocumentFeature(.paddle)
            } else { prepareDocumentFeature(.office) }
        case .ocrModels:
            if let model = job.localModelID { localOCRModelID = model }
            prepareDocumentFeature(.paddle)
        case .semantic: prepareDocumentFeature(.semantic)
        case .codexConnection: openSettings(.answers)
        case .legacyOffice, nil: openSettings(.documents)
        }
    }
}
