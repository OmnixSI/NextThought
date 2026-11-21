using Avalonia.Controls;
using Avalonia.Interactivity;
using CommunityToolkit.Mvvm.ComponentModel;
using NextThought.Core;
using NextThought.Platform;

namespace NextThought.Desktop.Views;

/// <summary>
/// E30: the 导入与恢复预览 page (port of BackupRestoreReview.swift). It shows the prepared plan
/// and only applies the merge after the user confirms; conflicts default to this machine.
/// </summary>
public partial class BackupRestoreReviewView : UserControl
{
    private PreparedBackupImport? prepared;
    private AppState? state;

    /// <summary>True once the chosen content was applied (the caller closes the window).</summary>
    public bool Applied { get; private set; }

    public BackupRestoreReviewView() => InitializeComponent();

    public BackupRestoreReviewView(PreparedBackupImport prepared, AppState state) : this()
    {
        this.prepared = prepared;
        this.state = state;
        DataContext = new BackupReviewModel(prepared);
        NextThought.Core.UiTextSource.Instance.PropertyChanged += OnLanguageChanged;
        DetachedFromVisualTree += (_, _) => NextThought.Core.UiTextSource.Instance.PropertyChanged -= OnLanguageChanged;
    }

    /// <summary>G8: the planned rows re-read their status text on a live language switch.</summary>
    private void OnLanguageChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (DataContext is BackupReviewModel model) model.NotifyLanguageChanged();
    }

    private async void Apply_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not BackupReviewModel model || prepared is null || state is null) return;
        try
        {
            model.Busy = true;
            model.Failure = null;
            var result = await state.ApplyBackupImportAsync(prepared, model.SelectedSessions(), model.SelectedTemplates(), model.ApplySettings);
            if (result is null)
            {
                model.Failure = state.BackupStatusLine;
                model.Busy = false;
                return;
            }
            Applied = true;
            (TopLevel.GetTopLevel(this) as Window)?.Close();
        }
        catch (Exception ex)
        {
            model.Busy = false;
            model.Failure = NextThought.Core.UiText.Format(
                "恢复未全部完成：%@ 已有内容请以实际结果为准；如已开始应用，可从恢复前的恢复点找回。", ex.Message);
        }
    }

    private void Cancel_Click(object? sender, RoutedEventArgs e) => (TopLevel.GetTopLevel(this) as Window)?.Close();
}

/// <summary>BackupRestoreReviewView's state: the planned rows and the apply-settings switch.</summary>
public sealed partial class BackupReviewModel : ObservableObject
{
    public BackupReviewModel(PreparedBackupImport prepared)
    {
        foreach (var session in prepared.Sessions)
            Sessions.Add(new BackupReviewRow(session.Title, session.Status, session.Id, null));
        foreach (var template in prepared.Templates)
            Templates.Add(new BackupReviewRow(template.Title, template.Status, null, template.Id));
        NewCount = prepared.NewSessions;
        IdenticalCount = prepared.IdenticalSessions;
        ConflictCount = prepared.ConflictSessions;
        TemplateCount = prepared.Templates.Count;
    }

    public ObservableCollection<BackupReviewRow> Sessions { get; } = [];
    public ObservableCollection<BackupReviewRow> Templates { get; } = [];
    public int NewCount { get; }
    public int IdenticalCount { get; }
    public int ConflictCount { get; }
    public int TemplateCount { get; }
    public bool HasTemplates => Templates.Count > 0;
    public bool IsEmpty => Sessions.Count == 0 && Templates.Count == 0;

    [ObservableProperty] private bool applySettings = true;
    [ObservableProperty] private bool busy;
    [ObservableProperty] private string? failure;

    partial void OnFailureChanged(string? value) => OnPropertyChanged(nameof(HasFailure));
    public bool HasFailure => Failure is { Length: > 0 };

    public IReadOnlySet<Guid> SelectedSessions() =>
        Sessions.Where(row => row.UseBackup && row.SessionId is not null).Select(row => row.SessionId!.Value).ToHashSet();

    public IReadOnlySet<string> SelectedTemplates() =>
        Templates.Where(row => row.UseBackup && row.TemplateId is not null).Select(row => row.TemplateId!).ToHashSet();

    /// <summary>G8: the status text of every planned row is translated on demand.</summary>
    public void NotifyLanguageChanged()
    {
        foreach (var row in Sessions) row.NotifyLanguageChanged();
        foreach (var row in Templates) row.NotifyLanguageChanged();
    }
}

/// <summary>One session or template of the review plan.</summary>
public sealed partial class BackupReviewRow : ObservableObject
{
    public BackupReviewRow(string title, BackupImportStatus status, Guid? sessionId, string? templateId)
    {
        Title = title;
        Status = status;
        SessionId = sessionId;
        TemplateId = templateId;
    }

    public string Title { get; }
    public BackupImportStatus Status { get; }
    public Guid? SessionId { get; }
    public string? TemplateId { get; }
    public bool IsConflict => Status == BackupImportStatus.Conflict;
    [ObservableProperty] private bool useBackup;

    /// <summary>BackupRestoreReviewView.status(_:) — the Mac original sentences.</summary>
    public string StatusText => Status switch
    {
        BackupImportStatus.New => NextThought.Core.UiText.T("新增 · 将导入"),
        BackupImportStatus.Identical => NextThought.Core.UiText.T("内容相同 · 跳过"),
        _ => NextThought.Core.UiText.T("内容不同 · 默认保留本机")
    };

    public void NotifyLanguageChanged() => OnPropertyChanged(nameof(StatusText));
}
