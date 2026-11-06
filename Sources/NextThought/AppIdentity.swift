import Foundation

enum AppIdentity {
    static let bundleIdentifier = Bundle.main.bundleIdentifier ?? "com.nextthought.app"

    static func legacyBundleIdentifier(for identifier: String) -> String? {
        switch identifier {
        case "com.nextthought.app": "com.interviewhacker.app"
        default: nil
        }
    }
}
