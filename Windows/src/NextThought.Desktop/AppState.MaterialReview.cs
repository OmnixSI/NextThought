using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NextThought.Core;

namespace NextThought.Desktop;

public partial class AppState
{
    [ObservableProperty] private string materialReviewNote = "";
    private static readonly JsonSerializerOptions ReviewJson = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    private string ReviewPath => Path.Combine(Paths.Root, "material-review.json");

    public void LoadMaterialReview()
    {
        MaterialReviewNote = "";
        if (Library.SelectedMaterial is not { } material) return;
        try
        {
            if (!File.Exists(ReviewPath)) return;
            var map = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(ReviewPath), ReviewJson);
            if (map?.TryGetValue(material.Id, out var note) == true) MaterialReviewNote = note;
        }
        catch { }
    }

    [RelayCommand]
    private void SaveMaterialReview()
    {
        if (Library.SelectedMaterial is not { } material) return;
        try
        {
            var map = File.Exists(ReviewPath)
                ? JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(ReviewPath), ReviewJson) ?? []
                : [];
            map[material.Id] = MaterialReviewNote;
            File.WriteAllText(ReviewPath, JsonSerializer.Serialize(map, ReviewJson));
            SetStatus("资料复核备注已保存。");
        }
        catch (Exception ex) { SetStatus("保存复核备注失败：" + ex.Message, true); }
    }
}
