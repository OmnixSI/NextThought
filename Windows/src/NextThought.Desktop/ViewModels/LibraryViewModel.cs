using System.Collections.ObjectModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using NextThought.Core;
using NextThought.Platform;

namespace NextThought.Desktop;

/// <summary>资料库：导入、检索、查看引用出处。</summary>
public partial class LibraryViewModel(AppState state) : ObservableObject
{
    private readonly AppState state = state;
    private MaterialStore? store;

    private MaterialStore Store => store ??= new MaterialStore(state.Paths);

    public ObservableCollection<MaterialDocument> Materials { get; } = [];

    [ObservableProperty] private MaterialDocument? selectedMaterial;
    [ObservableProperty] private string selectedPreview = "选择一份资料，查看本机解析出的文本。";
    [ObservableProperty] private string importStatus = "";
    [ObservableProperty] private bool importing;
    [ObservableProperty] private string importProgress = "";
    [ObservableProperty] private string filter = "";

    public IReadOnlyList<MaterialDocument> DocumentsForRetrieval() => Materials.Count > 0 ? Materials.ToList() : Store.List().ToList();


    public string MaterialsInsight => Materials.Count == 0
        ? "还没有资料；导入 PDF、Word、PPT、Excel、Markdown 或图片后，这里显示页数与段落数。"
        : string.Join("  ·  ", Materials.Take(3).Select(document =>
            UiText.Format("%@ %lld 段", document.Name, document.Blocks.Count))) + (Materials.Count > 3 ? UiText.Format("  ·  另 %lld 份", Materials.Count - 3) : "");

    public bool HasMaterials => Materials.Count > 0;
    public void NotifyLanguageChanged() => OnPropertyChanged(nameof(MaterialCountText));

    public string MaterialCountText => NextThought.Core.UiText.EnglishUi
        ? $"{NextThought.Core.UiText.Materials(Materials.Count)} · parsed on device"
        : UiText.Format("%lld 份资料 · 本机解析", Materials.Count);

    partial void OnFilterChanged(string value) => ApplyFilter();

    private List<MaterialDocument> all = [];

    public void Refresh()
    {
        try
        {
            all = Store.List().ToList();
            ApplyFilter();
            OnPropertyChanged(nameof(MaterialsInsight));
            ImportStatus = all.Count == 0 ? UiText.T("还没有资料。导入 PDF、Word、PPT、Excel、Markdown 或图片。") : UiText.Format("共 %lld 份资料，均在本机解析，不上传。", all.Count);
        }
        catch (Exception ex)
        {
            ImportStatus = "资料库读取失败：" + ex.Message;
        }
    }

    private void ApplyFilter()
    {
        var query = Filter.Trim();
        var visible = query.Length == 0
            ? all
            : all.Where(x => x.Name.Contains(query, StringComparison.OrdinalIgnoreCase)).ToList();
        Materials.Clear();
        foreach (var material in visible) Materials.Add(material);
        if (SelectedMaterial is not null && !visible.Contains(SelectedMaterial)) SelectedMaterial = null;
        OnPropertyChanged(nameof(HasMaterials));
        OnPropertyChanged(nameof(MaterialCountText));
    }

    partial void OnSelectedMaterialChanged(MaterialDocument? value)
    {
        if (value is null) { SelectedPreview = "选择一份资料，查看本机解析出的文本。"; return; }
        var builder = new System.Text.StringBuilder();
        builder.AppendLine(UiText.Format("%@ · %@ · %lld 段 · 解析器 %@", value.Name, value.Extension, value.Blocks.Count, value.Reader));
        if (value.Warnings.Count > 0) builder.AppendLine("提示：" + string.Join("；", value.Warnings));
        builder.AppendLine();
        foreach (var block in value.Blocks.Take(120))
        {
            builder.AppendLine($"[{block.Location}] {block.Text}");
            builder.AppendLine();
        }
        if (value.Blocks.Count > 120) builder.AppendLine(UiText.Format("…… 其余 %lld 段未显示。", value.Blocks.Count - 120));
        SelectedPreview = builder.ToString();
    }

    public async Task ImportAsync(IEnumerable<string> files)
    {
        var list = files.ToList();
        if (list.Count == 0) return;
        Importing = true;
        var added = 0;
        try
        {
            foreach (var file in list)
            {
                ImportProgress = UiText.Format("正在解析 %@…", System.IO.Path.GetFileName(file));
                try
                {
                    await Store.ImportAsync(file, CancellationToken.None);
                    added++;
                }
                catch (Exception ex)
                {
                    ImportStatus = UiText.Format("导入 %@ 失败：%@", System.IO.Path.GetFileName(file), ex.Message);
                }
            }
            Refresh();
            state.RefreshMaterialTerms();
            ImportStatus = added > 0 ? UiText.Format("已导入 %lld 份资料。", added) : ImportStatus;
            ImportProgress = "";
        }
        finally
        {
            Importing = false;
        }
    }

    public void OpenCitation(SourceCitation citation)
    {
        var material = Materials.FirstOrDefault(x => x.Id == citation.DocumentId) ?? Store.List().FirstOrDefault(x => x.Id == citation.DocumentId);
        if (material is null) { ImportStatus = "引用来自已删除的资料：" + citation.DocumentName; return; }
        SelectedMaterial = material;
        state.Section = AppSection.Library;
    }

    public void RevealSourceFile()
    {
        if (SelectedMaterial is null) return;
        try
        {
            var path = Store.SourcePath(SelectedMaterial);
            state.OpenExternal(System.IO.Path.GetDirectoryName(path) ?? state.Paths.Root);
        }
        catch (Exception ex) { ImportStatus = ex.Message; }
    }
}
