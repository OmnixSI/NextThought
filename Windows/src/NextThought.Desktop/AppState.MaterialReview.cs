using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NextThought.Core;
using NextThought.Platform;

namespace NextThought.Desktop;

/// <summary>
/// E21: the library material review sheet (ProjectLibrarySupport.swift MaterialReviewSheet).
/// Warnings, the original file and the extracted text are shown together; the review note is kept
/// in material-review.json.
/// </summary>
public partial class AppState
{
    [ObservableProperty] private string materialReviewNote = "";
    [ObservableProperty] private string materialReviewPreview = "";
    [ObservableProperty] private bool materialReviewPreviewTruncated;

    public System.Collections.ObjectModel.ObservableCollection<string> MaterialReviewWarnings { get; } = [];
    public bool HasMaterialReviewWarnings => MaterialReviewWarnings.Count > 0;
    public string MaterialReviewWarningsTitle => UiText.T("这份资料需要核对");
    public string MaterialReviewNoteLine => DocumentNotice.Note(Library.SelectedMaterial?.Warnings ?? []) ?? "";
    public bool HasMaterialReviewNoteLine => MaterialReviewNoteLine.Length > 0;
    public string MaterialReviewPreviewTruncatedText => UiText.T("此处预览前 80,000 字符，完整提取已保存在本机。可打开完整文本核对。");
    public string MaterialReviewUnavailableText => UiText.T("原件暂不可用");
    public string MaterialReviewUnavailableHint => UiText.T("提取内容仍已保留。可重新导入原文件补齐原件。");
    public string MaterialReviewNoteHeading => UiText.T("复核备注");

    public string MaterialReviewSourcePath => Library.SelectedMaterial is { } material
        ? new MaterialStore(Paths).SourcePath(material)
        : "";
    public string MaterialReviewExtractedPath => Library.SelectedMaterial is { } material
        ? Path.Combine(Paths.Root, "Materials", material.Id, "extracted.txt")
        : "";
    public bool MaterialReviewHasOriginal => MaterialReviewSourcePath.Length > 0 && File.Exists(MaterialReviewSourcePath);

    public void LoadMaterialReview()
    {
        MaterialReviewWarnings.Clear();
        MaterialReviewPreview = "";
        MaterialReviewPreviewTruncated = false;
        MaterialReviewNote = "";
        if (Library.SelectedMaterial is not { } material)
        {
            RaiseMaterialReview();
            return;
        }
        foreach (var warning in DocumentNotice.Warnings(material.Warnings)) MaterialReviewWarnings.Add(warning);
        var (text, truncated) = NextThought.Core.MaterialReviewPreview.Build(material.Blocks);
        MaterialReviewPreview = text;
        MaterialReviewPreviewTruncated = truncated;
        MaterialReviewNote = MaterialReviewNotes.NoteFor(Paths.Root, material.Id);
        RaiseMaterialReview();
    }

    private void RaiseMaterialReview()
    {
        OnPropertyChanged(nameof(HasMaterialReviewWarnings));
        OnPropertyChanged(nameof(MaterialReviewWarningsTitle));
        OnPropertyChanged(nameof(MaterialReviewNoteLine));
        OnPropertyChanged(nameof(HasMaterialReviewNoteLine));
        OnPropertyChanged(nameof(MaterialReviewPreviewTruncatedText));
        OnPropertyChanged(nameof(MaterialReviewUnavailableText));
        OnPropertyChanged(nameof(MaterialReviewUnavailableHint));
        OnPropertyChanged(nameof(MaterialReviewNoteHeading));
        OnPropertyChanged(nameof(MaterialReviewSourcePath));
        OnPropertyChanged(nameof(MaterialReviewExtractedPath));
        OnPropertyChanged(nameof(MaterialReviewHasOriginal));
    }

    /// <summary>MaterialReviewSheet: 用系统应用打开 the original file.</summary>
    [RelayCommand]
    private void OpenMaterialOriginal()
    {
        if (!MaterialReviewHasOriginal) return;
        try { OpenExternal(MaterialReviewSourcePath); }
        catch (Exception ex) { SetStatus(ex.Message, true); }
    }

    /// <summary>MaterialReviewSheet: 打开完整文本 — a readable .txt with the whole extraction.</summary>
    [RelayCommand]
    private void OpenMaterialExtracted()
    {
        if (Library.SelectedMaterial is not { } material) return;
        try
        {
            var path = MaterialReviewExtractedPath;
            if (!File.Exists(path))
            {
                var (text, _) = NextThought.Core.MaterialReviewPreview.Build(material.Blocks, int.MaxValue);
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.WriteAllText(path, text);
            }
            OpenExternal(path);
        }
        catch (Exception ex) { SetStatus(ex.Message, true); }
    }

    [RelayCommand]
    private void SaveMaterialReview()
    {
        if (Library.SelectedMaterial is not { } material) return;
        try
        {
            MaterialReviewNotes.Save(Paths.Root, material.Id, MaterialReviewNote);
            SetStatus(UiText.T("资料复核备注已保存。"));
        }
        catch (Exception ex) { SetStatus(UiText.T("保存复核备注失败：") + ex.Message, true); }
    }
}
