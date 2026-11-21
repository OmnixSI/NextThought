using System.Collections.ObjectModel;
using System.Text.Json;
using NextThought.Core;
using NextThought.Desktop.Views;

namespace NextThought.Desktop;

/// <summary>One row of 回答记录 (Mac AnswerHistoryPanel.row).</summary>
public sealed record AnswerHistoryRow(int Index, AnswerRecord Record, bool Shown)
{
    public string TimeText => Record.Date.ToLocalTime().ToString("HH:mm");
    public string Question => AnswerRefinements.OriginalQuestion(Record.Question);
    public string AnswerPreview => Record.Answer.Replace("**", "").Replace("\n", " ");
    public bool HasWebLookup => Record.WebSources is { Count: > 0 };
    public bool IsManual => !Record.Automatic;
}


/// <summary>
/// E5: the answer area's modules (AssistantLayout.swift / DetachedModules.swift) — the docked
/// module layout with its presets, pop-out windows, and the answer history that reads every stored
/// answer of the open session (CurrentAnswerHistory while recording, the archived answers after).
/// </summary>
public partial class AppState
{
    private static readonly JsonSerializerOptions LayoutJson = new() { WriteIndented = true };

    private AssistantLayout? assistantLayout;
    private readonly Dictionary<AssistantModule, DetachedModuleWindow> detachedModuleWindows = [];

    /// <summary>The assistant module layout (Mac assistantLayout, saved as JSON next to the data).</summary>
    public AssistantLayout AssistantLayoutState
    {
        get => assistantLayout ??= LoadAssistantLayout();
        private set
        {
            assistantLayout = value;
            OnPropertyChanged(nameof(AssistantLayoutState));
        }
    }

    public string AssistantLayoutJsonPath => Path.Combine(Paths.Root, "assistant-layout.json");

    private AssistantLayout LoadAssistantLayout()
    {
        try
        {
            return File.Exists(AssistantLayoutJsonPath)
                ? AssistantLayout.FromJson(File.ReadAllText(AssistantLayoutJsonPath))
                : AssistantLayout.Standard;
        }
        catch
        {
            return AssistantLayout.Standard;
        }
    }

    private void SaveAssistantLayout()
    {
        try { File.WriteAllText(AssistantLayoutJsonPath, AssistantLayoutState.ToJson()); }
        catch { /* the layout is a preference; a failed write must not break the meeting */ }
    }

    /// <summary>Mac AssistantLayout.Preset menu: switch the whole layout.</summary>
    public void ApplyAssistantPreset(AssistantLayout.Preset preset)
    {
        var previous = AssistantLayoutState;
        AssistantLayoutState = AssistantLayout.For(preset);
        SyncDetachedModuleWindows(previous);
        SaveAssistantLayout();
    }

    public void OpenDetachedModule(AssistantModule module)
    {
        // Mac never detaches the insights module: it can only dock or hide (decisions §3.3 insights 5).
        if (module == AssistantModule.Insights) return;
        var previous = AssistantLayoutState;
        AssistantLayoutState = AssistantLayoutState.Detach(module);
        SyncDetachedModuleWindows(previous);
        SaveAssistantLayout();
    }

    public void DockDetachedModule(AssistantModule module)
    {
        var previous = AssistantLayoutState;
        AssistantLayoutState = AssistantLayoutState.Dock(module).Show(module);
        SyncDetachedModuleWindows(previous);
        SaveAssistantLayout();
    }

    public void MoveAssistantModule(AssistantModule module, int delta)
    {
        AssistantLayoutState = AssistantLayoutState.Move(module, delta);
        OnPropertyChanged(nameof(AssistantLayoutState));
        SaveAssistantLayout();
    }

    /// <summary>Opens/closes the module windows so they follow the layout (Mac DetachedModuleController).</summary>
    private void SyncDetachedModuleWindows(AssistantLayout previous)
    {
        foreach (var module in AssistantLayoutState.Detached.Except(previous.Detached)
                     .Where(module => module != AssistantModule.Insights).ToList())
        {
            var window = new DetachedModuleWindow { DataContext = this, Module = module };
            detachedModuleWindows[module] = window;
            window.Closed += (_, _) =>
            {
                detachedModuleWindows.Remove(module);
                if (AssistantLayoutState.IsDetached(module)) DockDetachedModule(module);
            };
            window.Show();
        }
        foreach (var module in previous.Detached.Except(AssistantLayoutState.Detached).ToList())
        {
            if (detachedModuleWindows.Remove(module, out var window)) window.Close();
        }
    }

    // ---- answer history (Mac answerHistory + historyCursor) -------------------------

    /// <summary>Every answer of the open session: live ones while recording, archived ones otherwise.</summary>
    public ObservableCollection<AnswerRecord> SessionAnswerHistory { get; } = [];

    [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty]
    private int? answerHistoryCursor;

    partial void OnAnswerHistoryCursorChanged(int? value)
    {
        RefreshAnswerHistoryRows();
        RaiseLiveAnswerUi();
    }

    /// <summary>The rows of 回答记录, newest first, with the shown one marked.</summary>
    public ObservableCollection<AnswerHistoryRow> AnswerHistoryRows { get; } = [];

    /// <summary>Rebuilds the panel's list from the live history or the archive of the open session.</summary>
    public void RefreshSessionAnswerHistory()
    {
        SessionAnswerHistory.Clear();
        var source = Recording
            ? CurrentAnswerHistory
            : (SelectedSessionAnswers.Count > 0 ? SelectedSessionAnswers : CurrentAnswerHistory);
        foreach (var record in source) SessionAnswerHistory.Add(record);
        if (AnswerHistoryCursor is { } cursor && cursor >= SessionAnswerHistory.Count) AnswerHistoryCursor = null;
        RefreshAnswerHistoryRows();
        OnPropertyChanged(nameof(SessionAnswerHistory));
        OnPropertyChanged(nameof(AnswerHistoryCountText));
        RaiseLiveAnswerUi();
    }

    private void RefreshAnswerHistoryRows()
    {
        AnswerHistoryRows.Clear();
        var shown = AnswerHistoryCursor ?? SessionAnswerHistory.Count - 1;
        for (var index = SessionAnswerHistory.Count - 1; index >= 0; index--)
            AnswerHistoryRows.Add(new AnswerHistoryRow(index, SessionAnswerHistory[index], index == shown));
        OnPropertyChanged(nameof(AnswerHistoryRows));
    }

    public bool HasSessionAnswerHistory => SessionAnswerHistory.Count > 0;

    public string AnswerHistoryCountText => SessionAnswerHistory.Count.ToString();

    public bool HistoryCursorActive => AnswerHistoryCursor is not null;

    /// <summary>True while there are records to step through (Mac historyPositionText).</summary>
    public bool AnswerHistoryHasMultiple => SessionAnswerHistory.Count > 1;

    public string HistoryPositionText => SessionAnswerHistory.Count == 0
        ? ""
        : (AnswerHistoryCursor ?? SessionAnswerHistory.Count - 1) + 1 + " / " + SessionAnswerHistory.Count;

    public void MoveAnswerHistory(int delta)
    {
        var count = SessionAnswerHistory.Count;
        if (count == 0) return;
        var current = AnswerHistoryCursor ?? count - 1;
        var next = Math.Clamp(current + delta, 0, count - 1);
        AnswerHistoryCursor = next >= count - 1 ? null : next;
        RaiseLiveAnswerUi();
    }

    public void ShowLatestAnswer()
    {
        AnswerHistoryCursor = null;
        RaiseLiveAnswerUi();
    }

    /// <summary>Clicks a row in 回答记录; the last row means "latest", which clears the cursor.</summary>
    public void SelectAnswerHistory(int index)
    {
        if (index < 0 || index >= SessionAnswerHistory.Count) return;
        AnswerHistoryCursor = index == SessionAnswerHistory.Count - 1 ? null : index;
        RaiseLiveAnswerUi();
    }

    /// <summary>The record the answer column is showing (cursor-aware, Mac displayedRecordID).</summary>
    public AnswerRecord? DisplayedAnswerRecord
    {
        get
        {
            if (SessionAnswerHistory.Count == 0) return CurrentAnswerHistory.Count == 0 ? null : CurrentAnswerHistory[^1];
            var index = AnswerHistoryCursor ?? SessionAnswerHistory.Count - 1;
            return index >= 0 && index < SessionAnswerHistory.Count ? SessionAnswerHistory[index] : null;
        }
    }

    /// <summary>The answer text on screen: the live draft, or the answer at the cursor. The web
    /// version replaces it only for the answers the user switched (E14 ShowsWebAnswer).</summary>
    public string DisplayedAnswerText
    {
        get
        {
            if (ShowsWebAnswer && DisplayedWebLookup is { HasAnswer: true } lookup) return lookup.Answer;
            return AnswerHistoryCursor is not null ? DisplayedAnswerRecord?.Answer ?? "" : AnswerText;
        }
    }

    public string DisplayedQuestionText => AnswerHistoryCursor is not null
        ? DisplayedAnswerRecord?.Question ?? ""
        : DetectedQuestion;

    public bool HasDisplayedAnswer => DisplayedAnswerText.Length > 0;
}
