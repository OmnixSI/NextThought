import Testing
@testable import NextThoughtCore

struct OnboardingVersionTests {
    @Test func firstInstallAndFeatureUpgradeShowGuide() {
        #expect(OnboardingVersion.needsGuide(appVersion: "1.7.0", completedVersion: nil))
        #expect(OnboardingVersion.needsGuide(appVersion: "1.8.0", completedVersion: "1.7"))
        #expect(OnboardingVersion.needsGuide(appVersion: "2.0.0", completedVersion: "1.9"))
        // The redesigned guide reaches existing users with the next feature release.
        #expect(OnboardingVersion.needsGuide(appVersion: "2.2.0", completedVersion: "2.1"))
    }
    @Test func patchUpdateDoesNotRepeatCompletedGuide() {
        #expect(!OnboardingVersion.needsGuide(appVersion: "1.7.2", completedVersion: "1.7"))
        #expect(!OnboardingVersion.needsGuide(appVersion: "2.1.4", completedVersion: "2.1"))
    }
    @Test func featureReleaseOrNewLayoutRearmsTheGuideOnce() {
        #expect(OnboardingVersion.isFeatureUpgrade(appVersion: "3.0.0", startedVersion: nil, startedLayout: nil, layout: 2))
        #expect(OnboardingVersion.isFeatureUpgrade(appVersion: "3.1.0", startedVersion: "3.0", startedLayout: 2, layout: 2))
        #expect(OnboardingVersion.isFeatureUpgrade(appVersion: "3.0.0", startedVersion: "3.0", startedLayout: 1, layout: 2))
        #expect(!OnboardingVersion.isFeatureUpgrade(appVersion: "3.0.2", startedVersion: "3.0", startedLayout: 2, layout: 2))
    }
    @Test func backupKeepsExpertPreferenceButNeverSkipsNewInstallGuide() {
        let snapshot = SettingsSnapshot.filter(["advancedSettingsEnabled": true, "onboardingGuideCompletedVersion": "1.7", "onboardingCompleted": true])
        #expect(snapshot["advancedSettingsEnabled"] as? Bool == true)
        #expect(snapshot["onboardingGuideCompletedVersion"] == nil)
        #expect(snapshot["onboardingCompleted"] == nil)
    }
}
