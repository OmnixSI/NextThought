import Foundation

enum AppEnvironment {
    static var values: [String: String] {
        var values = ProcessInfo.processInfo.environment
        let legacyValues = values.filter { $0.key.hasPrefix("INTERVIEWHACKER_") }
        for (key, value) in legacyValues {
            let suffix = String(key.dropFirst("INTERVIEWHACKER_".count))
            let nextKey = "NEXTTHOUGHT_" + suffix
            if values[nextKey] == nil { values[nextKey] = value }
        }
        return values
    }
}
