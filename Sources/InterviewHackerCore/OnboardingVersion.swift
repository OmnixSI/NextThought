import Foundation

public enum OnboardingVersion {
    /// Feature releases reintroduce the guide; patch releases do not.
    public static func guideVersion(_ appVersion: String) -> String {
        appVersion.split(separator: ".").prefix(2).joined(separator: ".")
    }
    public static func needsGuide(appVersion: String, completedVersion: String?) -> Bool {
        completedVersion != guideVersion(appVersion)
    }
}
