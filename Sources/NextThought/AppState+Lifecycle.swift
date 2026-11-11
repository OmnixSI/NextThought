import AppKit
import Foundation
import NextThoughtCore

/// Things that end or interrupt a meeting from outside the live page: quitting,
/// updating, the one-time recording notice, and the answer service coming or going.
@MainActor
extension AppState {
    // MARK: Ending a meeting for quit / update / language

    enum MeetingEndAction {
        case quit, update, language

        var confirmTitle: String {
            switch self {
            case .quit: String(localized: "结束并退出")
            case .update: String(localized: "结束并更新")
            case .language: String(localized: "结束并切换语言")
            }
        }

        var message: String {
            switch self {
            case .quit: String(localized: "退出会结束这场会议。纪要会在下次打开时自动整理。")
            case .update: String(localized: "更新会结束这场会议。纪要会在下次打开时自动整理。")
            case .language: String(localized: "切换语言会结束这场会议。纪要会在下次打开时自动整理。")
            }
        }
    }

    /// Asks before an action that ends a running meeting. True when nothing is running
    /// or the user agreed; the answer holds until the action finishes or fails.
    func confirmEndingMeeting(for action: MeetingEndAction) -> Bool {
        guard isRunning || isPaused, !DemoMode.isEnabled else { return true }
        if endMeetingConfirmed { return true }
        let alert = NSAlert()
        alert.messageText = String(localized: "会议还在进行")
        alert.informativeText = action.message
        alert.addButton(withTitle: action.confirmTitle)
        alert.addButton(withTitle: String(localized: "取消"))
        guard alert.runModal() == .alertFirstButtonReturn else { return false }
        endMeetingConfirmed = true
        return true
    }

    // MARK: First-recording notice

    /// The consent alert lives on the main window: bring it forward so a start from the
    /// notch launcher or a global hotkey shows it. With no window to host it, ask here.
    func presentRecordingNotice() {
        guard !DemoMode.isEnabled else { return }
        let hasMainWindow = NSApp.windows.contains { MainWindowLayout.isMainWindow($0) }
        if hasMainWindow {
            liveWindow.set(.notes)
            MainWindowLayout.bringToFront()
            return
        }
        Task { @MainActor in
            guard let pending = recordingNoticePending else { return }
            let alert = NSAlert()
            alert.messageText = String(localized: "开始记录之前")
            alert.informativeText = String(localized: "开始前，请确认已告知参会的人。") + "\n\n" + recordingDataRouteSummary
            alert.addButton(withTitle: String(localized: "继续"))
            alert.addButton(withTitle: String(localized: "取消"))
            if alert.runModal() == .alertFirstButtonReturn {
                await confirmRecordingNotice(pending)
            } else {
                recordingNoticePending = nil
            }
        }
    }

    // MARK: Crash notice

    func dismissInterruptedNotice() { interruptedNotice = nil }

    // MARK: Answer service during a meeting

    /// An answer service is connected, so detected questions can be answered.
    var canAnswerAutomatically: Bool { answerEngineReady }

    /// The meeting type's default: automatic answers, except in the room.
    var defaultsToAutomaticAnswers: Bool { typeDefaults.liveMode == .prompting && !isOfflineMeeting }

    /// "还没有连接回答服务" while automatic answers are on but cannot answer; nil otherwise.
    func refreshAnswerServiceStatus() {
        let missing = isRunning && automaticAnswers && !canAnswerAutomatically
        let text: String? = missing ? String(localized: "还没有连接回答服务") : nil
        if liveAnswerStatus != text { liveAnswerStatus = text }
    }

    /// Turns automatic answers on when a service is connected mid-meeting, unless the
    /// user chose record-only by hand; keeps the status line current.
    func startAnswerServiceWatch() {
        liveModeChosenByHand = false
        let token = UUID()
        answerServiceWatch = token
        refreshAnswerServiceStatus()
        trackAnswerService(token)
    }

    func stopAnswerServiceWatch() {
        answerServiceWatch = nil
        liveAnswerStatus = nil
    }

    private func trackAnswerService(_ token: UUID) {
        withObservationTracking {
            _ = canAnswerAutomatically
        } onChange: { [weak self] in
            Task { @MainActor in
                guard let self, self.answerServiceWatch == token, self.isRunning else { return }
                if self.canAnswerAutomatically, !self.automaticAnswers, !self.liveModeChosenByHand, self.defaultsToAutomaticAnswers {
                    self.applyingAutomaticAnswerMode = true
                    self.setLiveMode(prompting: true)
                    self.applyingAutomaticAnswerMode = false
                }
                self.refreshAnswerServiceStatus()
                self.trackAnswerService(token)
            }
        }
    }
}
