import Foundation

public enum OnboardingVersion {
    /// Feature releases reintroduce the guide; patch releases do not.
    public static func guideVersion(_ appVersion: String) -> String {
        appVersion.split(separator: ".").prefix(2).joined(separator: ".")
    }
    public static func needsGuide(appVersion: String, completedVersion: String?) -> Bool {
        completedVersion != guideVersion(appVersion)
    }
    /// The first launch of a feature release (or of a new guide layout) re-arms a guide that was
    /// deferred or switched off by an earlier release, once.
    public static func isFeatureUpgrade(appVersion: String, startedVersion: String?, startedLayout: Int?, layout: Int) -> Bool {
        startedVersion != guideVersion(appVersion) || startedLayout != layout
    }
}
