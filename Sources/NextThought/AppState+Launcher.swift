import Foundation
import NextThoughtCore

@MainActor
extension AppState {
    /// Starts a meeting of `scene` from the notch launcher. The user is in another app: no window
    /// comes forward, and a draft left on the preparation page is replaced by a fresh one. Anything
    /// that needs the main window (an unsaved series edit, the one-time recording notice) takes
    /// the ordinary path instead, which asks there.
    func startFromIsland(scene: SessionMode) async {
        guard !isRunning, !isChangingSession else { return }
        let editor = WorkspaceEditController.shared
        if (editor.draft != nil && editor.hasChanges) || !UserDefaults.standard.bool(forKey: "recordingNoticeShown") {
            beginNewMeeting(scene: scene)
            guard meetingDraft != nil else { return }
            await startPreparedMeeting()
            return
        }
        cancelDraft()
        meetingDraft = MeetingDraft()
        setDraftType(scene)
        await startPreparedMeeting()
    }
}
