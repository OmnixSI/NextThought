using System.Collections.ObjectModel;
using NextThought.Core;

namespace NextThought.Desktop;

public partial class AppState
{
    public ObservableCollection<SceneSettingRow> SceneSettingsRows { get; } = [];

    private void LoadSceneSettings()
    {
        SceneSettingsRows.Clear();
        Dictionary<string, SceneDefaults> saved;
        try { saved = SettingsStore.Load().SceneDefaults; } catch { saved = []; }
        var templates = SceneTemplateOptions();
        foreach (var mode in Enum.GetValues<MeetingMode>())
        {
            var key = mode.ToString();
            var stored = saved.GetValueOrDefault(key);
            // Mac stores only overrides; an old full row is still accepted field by field.
            SceneSettingsRows.Add(new SceneSettingRow(mode, stored ?? SceneDefaults.For(mode), templates, SaveSceneSetting));
        }
    }

    /// <summary>E26: built-in selectable templates plus imported custom ones (Mac allNotesTemplates).</summary>
    public IReadOnlyList<SceneTemplateOption> SceneTemplateOptions()
    {
        var options = new List<SceneTemplateOption>();
        foreach (var choice in TemplateChoices)
            options.Add(new SceneTemplateOption(choice.Template.Id, choice.Title));
        return options;
    }

    public SceneDefaults SceneDefaultsFor(MeetingMode mode) =>
        SceneSettingsRows.FirstOrDefault(row => row.Mode == mode)?.Defaults
        ?? SceneDefaults.For(mode);

    public string DefaultTemplateIdFor(MeetingMode mode) => SceneDefaultsFor(mode).NotesTemplate;

    private void SaveSceneSetting(SceneSettingRow row)
    {
        try
        {
            var settings = SettingsStore.Load();
            var defaults = SceneDefaults.For(row.Mode);
            var value = row.Defaults;
            // Mac SceneSettings: a field is stored only while it differs from the built-in row.
            var stored = new SceneDefaults(
                value.LiveMode == defaults.LiveMode ? "" : value.LiveMode,
                value.AnswerStyle == defaults.AnswerStyle ? "" : value.AnswerStyle,
                value.NotesTemplate == defaults.NotesTemplate ? "" : value.NotesTemplate,
                value.CueStyle == defaults.CueStyle ? "" : value.CueStyle);
            if (stored.IsEmptyFor(row.Mode)) settings.SceneDefaults.Remove(row.Mode.ToString());
            else settings.SceneDefaults[row.Mode.ToString()] = stored;
            _ = SettingsStore.SaveAsync(settings);
        }
        catch { }
    }
}
