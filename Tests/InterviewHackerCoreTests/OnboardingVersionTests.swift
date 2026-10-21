import Testing
@testable import InterviewHackerCore

struct OnboardingVersionTests {
    @Test func firstInstallAndFeatureUpgradeShowGuide() {
        #expect(OnboardingVersion.needsGuide(appVersion: "1.7.0", completedVersion: nil))
        #expect(OnboardingVersion.needsGuide(appVersion: "1.8.0", completedVersion: "1.7"))
        #expect(OnboardingVersion.needsGuide(appVersion: "2.0.0", completedVersion: "1.9"))
    }
    @Test func patchUpdateDoesNotRepeatCompletedGuide() {
        #expect(!OnboardingVersion.needsGuide(appVersion: "1.7.2", completedVersion: "1.7"))
    }
    @Test func backupKeepsExpertPreferenceButNeverSkipsNewInstallGuide() {
        let snapshot = SettingsSnapshot.filter(["advancedSettingsEnabled": true, "onboardingGuideCompletedVersion": "1.7", "onboardingCompleted": true])
        #expect(snapshot["advancedSettingsEnabled"] as? Bool == true)
        #expect(snapshot["onboardingGuideCompletedVersion"] == nil)
        #expect(snapshot["onboardingCompleted"] == nil)
    }
}
