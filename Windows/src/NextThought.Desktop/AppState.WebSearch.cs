using CommunityToolkit.Mvvm.Input;
using NextThought.Core;
using NextThought.Platform;

namespace NextThought.Desktop;

/// <summary>
/// E14 (决议 §3.3 web-search 2/3): the original answer appears first, the decision asks the model
/// with the Mac three-second deadline (WebSearchClassifier), and a needed lookup runs in the
/// background for that answer id (Mac WebLookup). When it finishes the live page can switch that
/// answer between the original and the web version only (`ShowsWebAnswer` / ToggleWebAnswerCommand);
/// the state is per answer id, and a superseded lookup cannot overwrite a newer answer.
/// </summary>
public partial class AppState
{
    private readonly WebLookupStore webLookups = new();
    private readonly HashSet<Guid> showOriginalAnswer = [];
    private readonly HashSet<Guid> acceptedWebAnswers = [];
    private CancellationTokenSource? webLookupCancellation;

    /// <summary>The background lookup for the answer on screen (Mac displayedWebLookup).</summary>
    public WebLookup? DisplayedWebLookup => DisplayedAnswerRecord is { } record ? webLookups.Find(record.Id) : null;

    public bool HasWebLookup => DisplayedWebLookup is not null;

    /// <summary>True while the answer on screen shows the web version; the record keeps both.</summary>
    public bool ShowsWebAnswer
    {
        get
        {
            if (DisplayedWebLookup is not { HasAnswer: true } lookup) return false;
            return acceptedWebAnswers.Contains(lookup.AnswerId) && !showOriginalAnswer.Contains(lookup.AnswerId);
        }
    }

    public bool CanToggleWebAnswer => DisplayedWebLookup is { HasAnswer: true };

    /// <summary>Mac showsWebAnswer drives the pill: 看原回答 / 看联网版.</summary>
    public string WebAnswerToggleLabel => ShowsWebAnswer ? UiText.T("看原回答") : UiText.T("看联网版");

    /// <summary>Searching/error line next to the pill; empty when there is nothing to say.</summary>
    public string WebLookupStatusText => DisplayedWebLookup switch
    {
        { Status: WebLookupStatus.Searching } => UiText.T("正在联网核查…"),
        { Status: WebLookupStatus.Failed, Error: { Length: > 0 } error } => error,
        _ => ""
    };

    [RelayCommand]
    private void ToggleWebAnswer()
    {
        if (DisplayedWebLookup is not { HasAnswer: true } lookup) return;
        if (ShowsWebAnswer) showOriginalAnswer.Add(lookup.AnswerId);
        else
        {
            acceptedWebAnswers.Add(lookup.AnswerId);
            showOriginalAnswer.Remove(lookup.AnswerId);
        }
        RefreshWebAnswerUi(lookup.AnswerId);
    }

    /// <summary>Mode, rules and the model review with its three-second deadline (Mac assessWebSearch).</summary>
    private async Task<SearchDecision> AssessWebSearchAsync(string question, CancellationToken ct)
    {
        var mode = WebSearchModes.Parse(Settings.WebSearchMode);
        var rules = WebSearchNeed.Assess(question);
        if (mode != WebSearchMode.Off && WebSearch() is null)
            return new SearchDecision(false, UiText.T("联网搜索未配置"), question, "setting");
        var context = TranscriptContextBuilder.Recent(
            currentLines.Select(line => (line.Date, line.Text)), DateTimeOffset.Now);
        Func<string, string, CancellationToken, Task<string>> ask = (system, payload, token) =>
        {
            var options = AnswerOptions();
            return CurrentClient().AnswerAsync(options, system, payload, null, token, timeoutSeconds: 3);
        };
        return await WebSearchClassifier.AssessAsync(mode, question, context,
            ScreenContextText.Length > 0 ? ScreenContextText : null,
            rules, AnswerGrounding.PureRewrite(question), AnswerGrounding.ExplicitSearch(question), ask, ct: ct);
    }

    /// <summary>Starts the background web answer for a finished answer when the decision asked for it.</summary>
    private void MaybeStartWebLookup(AnswerRecord record, SearchDecision? decision)
    {
        if (decision is not { Search: true } || WebSearch() is null) return;
        _ = StartWebLookupAsync(record, decision);
    }

    private async Task StartWebLookupAsync(AnswerRecord record, SearchDecision decision)
    {
        webLookupCancellation?.Cancel();
        var cancellation = new CancellationTokenSource();
        webLookupCancellation = cancellation;
        var provider = WebSearch();
        var lookup = webLookups.Begin(record.Id, record.Question, provider?.Provider ?? "", DateTimeOffset.UtcNow);
        RefreshWebAnswerUi(record.Id);
        var started = DateTimeOffset.UtcNow;
        try
        {
            if (provider is null) throw new InvalidOperationException(UiText.T("联网未配置：先在设置里保存 Tavily 密钥。"));
            var now = DateTimeOffset.UtcNow;
            IReadOnlyList<WebResult> results;
            // The automatic lookup may reuse a fresh verification; a manual one always searches.
            if (decision.Mechanism != "manual"
                && webVerificationCache.TryGet(record.Question, provider.Provider, now, out var cached))
            {
                results = cached.Results;
                LastWebSearchUsedCache = true;
            }
            else
            {
                SetStatus(UiText.T("正在联网核查…"));
                var response = await provider.SearchAsync(record.Question, 5, cancellation.Token);
                if (response.Error is not null) throw new InvalidOperationException(response.Error);
                results = response.Results;
                webVerificationCache.Store(new WebVerificationEntry(record.Question, provider.Provider,
                    record.Question, results, DateTimeOffset.UtcNow));
                LastWebSearchUsedCache = false;
            }
            if (results.Count == 0) throw new InvalidOperationException(UiText.T("联网查证失败：") + "no results");
            var parts = BuildAnswerPrompt(CurrentSession, record.Question, results, hasScreenshot: false);
            var options = AnswerOptions();
            var text = await CurrentClient().AnswerAsync(options, parts.System, parts.User,
                update =>
                {
                    if (webLookups.UpdateAnswer(record.Id, lookup.Generation, update))
                        RefreshWebAnswerUi(record.Id);
                }, cancellation.Token);
            if (!webLookups.Finish(record.Id, lookup.Generation, text, results,
                    (DateTimeOffset.UtcNow - started).TotalSeconds))
                return;
            ApplyWebAnswerToRecord(record.Id, text, results);
            SetStatus(UiText.Format("联网版已就绪 · %lld 个来源", results.Count));
        }
        catch (OperationCanceledException)
        {
            webLookups.Fail(record.Id, lookup.Generation, "");
        }
        catch (Exception error)
        {
            webLookups.Fail(record.Id, lookup.Generation, error.Message);
            SetStatus(error.Message, true);
        }
        finally
        {
            if (ReferenceEquals(webLookupCancellation, cancellation)) webLookupCancellation = null;
            cancellation.Dispose();
            RefreshWebAnswerUi(record.Id);
        }
    }

    private void ApplyWebAnswerToRecord(Guid id, string webAnswer, IReadOnlyList<WebResult> sources)
    {
        for (var index = 0; index < CurrentAnswerHistory.Count; index++)
        {
            var record = CurrentAnswerHistory[index];
            if (record.Id != id) continue;
            CurrentAnswerHistory[index] = record with { WebAnswer = webAnswer, WebSources = sources };
            break;
        }
        RefreshSessionAnswerHistory();
        RefreshHistory();
    }

    /// <summary>Refreshes the switch/status line and the sources of the answer on screen.</summary>
    private void RefreshWebAnswerUi(Guid? changedAnswerId = null)
    {
        var record = DisplayedAnswerRecord;
        if (record is null || (changedAnswerId is { } changed && changed != record.Id)) return;
        var lookup = webLookups.Find(record.Id);
        WebResults.Clear();
        if (lookup is { IsDone: true, Sources: { } sources })
            foreach (var result in sources) WebResults.Add(result);
        OnPropertyChanged(nameof(HasWebLookup));
        OnPropertyChanged(nameof(DisplayedWebLookup));
        OnPropertyChanged(nameof(ShowsWebAnswer));
        OnPropertyChanged(nameof(CanToggleWebAnswer));
        OnPropertyChanged(nameof(WebAnswerToggleLabel));
        OnPropertyChanged(nameof(WebLookupStatusText));
        OnPropertyChanged(nameof(WebStatusText));
        OnPropertyChanged(nameof(HasWebResults));
        OnPropertyChanged(nameof(HasWebLookupLine));
        OnPropertyChanged(nameof(LiveWebStatusLine));
        OnPropertyChanged(nameof(LiveWebSources));
        RaiseLiveAnswerUi();
    }
}
