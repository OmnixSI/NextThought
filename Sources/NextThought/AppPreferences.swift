import Foundation

enum AppPreferences {
    private static let migration: Void = migrateLegacyDomain()

    static var standard: UserDefaults {
        _ = migration
        return UserDefaults.standard
    }

    private static func migrateLegacyDomain() {
        let identifier = AppIdentity.bundleIdentifier
        guard let legacyIdentifier = AppIdentity.legacyBundleIdentifier(for: identifier),
              let legacyValues = UserDefaults.standard.persistentDomain(forName: legacyIdentifier),
              !legacyValues.isEmpty else { return }

        let currentValues = UserDefaults.standard.persistentDomain(forName: identifier) ?? [:]
        var migratedValues: [String: Any] = [:]
        for (key, value) in legacyValues {
            let nextKey = key.replacingOccurrences(of: "InterviewHacker", with: "NextThought")
                .replacingOccurrences(of: "interviewhacker", with: "nextthought")
                .replacingOccurrences(of: "INTERVIEWHACKER", with: "NEXTTHOUGHT")
            migratedValues[nextKey] = value
        }
        let merged = currentValues.merging(migratedValues) { current, _ in current }
        UserDefaults.standard.setPersistentDomain(merged, forName: identifier)
    }
}
