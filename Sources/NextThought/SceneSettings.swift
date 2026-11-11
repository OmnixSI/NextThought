import Foundation
import NextThoughtCore

/// What each built-in scene sets up when a meeting starts: live mode, answer length
/// and cue layout. A field is stored only when it differs from the built-in default
/// (`MeetingTypeDefaults`, `ScenePreset`), so changing a default in a later version
/// still reaches everyone who never touched the row.
@MainActor
@Observable
final class SceneSettings {
    static let shared = SceneSettings()
    static let key = "sceneOverrides"

    private enum Field {
        static let liveMode = "liveMode", style = "style", cueStyle = "cueStyle"
    }

    /// `[SessionMode.rawValue: [field: value]]`.
    private(set) var overrides: [String: [String: String]]
    @ObservationIgnored private let defaults: UserDefaults

    init(defaults: UserDefaults = AppPreferences.standard) {
        self.defaults = defaults
        overrides = defaults.dictionary(forKey: Self.key) as? [String: [String: String]] ?? [:]
    }

    // MARK: Reading

    func liveMode(for mode: SessionMode) -> LiveMode {
        overrides[mode.rawValue]?[Field.liveMode].flatMap(LiveMode.init(rawValue:)) ?? MeetingTypeDefaults.for(mode).liveMode
    }

    func style(for mode: SessionMode) -> AnswerStyle {
        styleOverride(for: mode) ?? MeetingTypeDefaults.for(mode).style
    }

    func cueStyle(for mode: SessionMode) -> CueStyle {
        overrides[mode.rawValue]?[Field.cueStyle].flatMap(CueStyle.init(rawValue:)) ?? ScenePreset.for(mode).cueStyle
    }

    /// The answer length the person chose for this scene; nil while it follows the built-in default.
    func styleOverride(for mode: SessionMode) -> AnswerStyle? {
        overrides[mode.rawValue]?[Field.style].flatMap(AnswerStyle.init(rawValue:))
    }

    /// True when a field of the scene differs from the built-in table.
    func isCustomised(_ mode: SessionMode) -> Bool { !(overrides[mode.rawValue] ?? [:]).isEmpty }

    // MARK: Writing

    func setLiveMode(_ value: LiveMode, for mode: SessionMode) {
        store(Field.liveMode, value.rawValue, isDefault: value == MeetingTypeDefaults.for(mode).liveMode, for: mode)
    }

    func setStyle(_ value: AnswerStyle, for mode: SessionMode) {
        store(Field.style, value.rawValue, isDefault: value == MeetingTypeDefaults.for(mode).style, for: mode)
    }

    func setCueStyle(_ value: CueStyle, for mode: SessionMode) {
        store(Field.cueStyle, value.rawValue, isDefault: value == ScenePreset.for(mode).cueStyle, for: mode)
    }

    func reset(_ mode: SessionMode) {
        overrides[mode.rawValue] = nil
        save()
    }

    private func store(_ field: String, _ raw: String, isDefault: Bool, for mode: SessionMode) {
        var fields = overrides[mode.rawValue] ?? [:]
        if isDefault { fields[field] = nil } else { fields[field] = raw }
        overrides[mode.rawValue] = fields.isEmpty ? nil : fields
        save()
    }

    private func save() { defaults.set(overrides, forKey: Self.key) }

    // MARK: Meeting start

    /// The meeting's workspace copy with the scene's answer length applied, unless the
    /// series has an explicit length of its own (see `MeetingPlan.preparedWorkspace`).
    func prepared(_ plan: MeetingPlan, from series: Workspace, previousTranscript: ContextDocument? = nil) -> Workspace {
        plan.preparedWorkspace(from: series, previousTranscript: previousTranscript,
                               sceneStyle: style(for: plan.effectiveMode))
    }
}
