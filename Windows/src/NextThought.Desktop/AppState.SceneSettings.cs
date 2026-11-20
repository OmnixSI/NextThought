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
        foreach (var mode in Enum.GetValues<MeetingMode>())
        {
            var key = mode.ToString();
            SceneSettingsRows.Add(new SceneSettingRow(mode, saved.GetValueOrDefault(key) ?? new SceneDefaults(), SaveSceneSetting));
        }
    }

    private void SaveSceneSetting(SceneSettingRow row)
    {
        try
        {
            var settings = SettingsStore.Load();
            settings.SceneDefaults[row.Mode.ToString()] = row.Defaults;
            _ = SettingsStore.SaveAsync(settings);
        }
        catch { }
    }
}
