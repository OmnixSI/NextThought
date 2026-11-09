import Foundation
import InterviewHackerCore

@MainActor
extension AppState {
    var draftAvailableDocumentCount: Int {
        guard let series = draftSeries, let plan = draftMeetingPlan else { return 0 }
        return plan.preparedWorkspace(from: series).documents.filter(\.enabled).count
    }
    var draftDeferredDocumentCount: Int {
        draftJobs.filter { $0.documentID == nil && !$0.stage.terminal }.count
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
