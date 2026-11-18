namespace NextThought.Desktop;

/// <summary>Command-line switches that make the interface inspectable without a live meeting.</summary>
public static class StartupOptions
{
    public static bool Demo { get; private set; }
    public static AppSection? Section { get; private set; }
    public static bool Dark { get; private set; }
    public static string? CheckReport { get; private set; }
    public static string? KnowledgeCheck { get; private set; }
    public static string? PresenterCheck { get; private set; }
    public static string? RelayCheck { get; private set; }
    public static string? UpdateInstallCheck { get; private set; }
    public static string? SubtitleExportCheck { get; private set; }
    public static string? HotwordsCheck { get; private set; }
    public static string? FormulaCheck { get; private set; }
    public static string? BackupCheck { get; private set; }
    public static string? PromptCheck { get; private set; }
    public static string? ApiCheck { get; private set; }
    public static string? RetrievalCheck { get; private set; }
    public static string? CloudCheck { get; private set; }
    public static string? StreamCheck { get; private set; }
    public static string? QuestionCheck { get; private set; }
    public static string? PdfCheck { get; private set; }
    public static string? GazeCheck { get; private set; }
    public static string? BpeCheck { get; private set; }
    public static string? GlossarySearchCheck { get; private set; }
    public static string? ProcessCheck { get; private set; }
    public static string? SessionSearchCheck { get; private set; }
    public static string? SeriesBriefingCheck { get; private set; }
    public static double? SettingsScroll { get; private set; }
    public static int? SeriesTab { get; private set; }
    public static bool Island { get; private set; }
    public static bool Subtitles { get; private set; }
    public static bool Companion { get; private set; }
    public static string? CaptureTest { get; private set; }
    public static bool Teleprompter { get; private set; }
    public static bool TeleprompterThrough { get; private set; }
    public static bool Presenter { get; private set; }
    public static string? ImportWave { get; private set; }
    public static string? HistoryFilter { get; private set; }
    public static string? HistoryQuery { get; private set; }
    public static NextThought.Core.MeetingMode? Scene { get; private set; }
    public static NextThought.Core.NotesFormat? NotesFormat { get; private set; }
    public static string? UpdateCheck { get; private set; }
    public static string? UpdateReport { get; private set; }

    public static void Parse(IReadOnlyList<string> args)
    {
        foreach (var argument in args)
        {
            if (argument is "--demo" or "-d") Demo = true;
            else if (argument is "--dark") Dark = true;
            else if (argument is "--island") Island = true;
            else if (argument is "--subtitles") Subtitles = true;
            else if (argument is "--companion") Companion = true;
            else if (argument is "--teleprompter") Teleprompter = true;
            else if (argument.StartsWith("--update-check=", StringComparison.OrdinalIgnoreCase)) UpdateCheck = argument["--update-check=".Length..];
            else if (argument.StartsWith("--update-report=", StringComparison.OrdinalIgnoreCase)) UpdateReport = argument["--update-report=".Length..];
            else if (argument.StartsWith("--notes-format=", StringComparison.OrdinalIgnoreCase))
                NotesFormat = argument["--notes-format=".Length..].ToLowerInvariant() switch {
                    "fishbone" => NextThought.Core.NotesFormat.Fishbone,
                    "outline" => NextThought.Core.NotesFormat.Outline,
                    "viewpoints" => NextThought.Core.NotesFormat.Viewpoints,
                    _ => NextThought.Core.NotesFormat.Standard
                };
            else if (argument.StartsWith("--scene=", StringComparison.OrdinalIgnoreCase)
                     && Enum.TryParse<NextThought.Core.MeetingMode>(argument["--scene=".Length..], true, out var scene)) Scene = scene;
            else if (argument.StartsWith("--history-filter=", StringComparison.OrdinalIgnoreCase)) HistoryFilter = argument["--history-filter=".Length..];
            else if (argument.StartsWith("--history-query=", StringComparison.OrdinalIgnoreCase)) HistoryQuery = argument["--history-query=".Length..];
            else if (argument is "--teleprompter-through") { Teleprompter = true; TeleprompterThrough = true; }
            else if (argument is "--presenter") Presenter = true;
            else if (argument.StartsWith("--import-wave=", StringComparison.OrdinalIgnoreCase)) ImportWave = argument["--import-wave=".Length..];
            else if (argument.StartsWith("--capture-test=", StringComparison.OrdinalIgnoreCase)) CaptureTest = argument["--capture-test=".Length..];
            else if (argument.StartsWith("--startup-check=", StringComparison.OrdinalIgnoreCase)) CheckReport = argument["--startup-check=".Length..];
            else if (argument.StartsWith("--knowledge-check=", StringComparison.OrdinalIgnoreCase)) KnowledgeCheck = argument["--knowledge-check=".Length..];
            else if (argument.StartsWith("--presenter-check=", StringComparison.OrdinalIgnoreCase)) PresenterCheck = argument["--presenter-check=".Length..];
            else if (argument.StartsWith("--relay-check=", StringComparison.OrdinalIgnoreCase)) RelayCheck = argument["--relay-check=".Length..];
            else if (argument.StartsWith("--update-install-check=", StringComparison.OrdinalIgnoreCase)) UpdateInstallCheck = argument["--update-install-check=".Length..];
            else if (argument.StartsWith("--subtitle-export-check=", StringComparison.OrdinalIgnoreCase)) SubtitleExportCheck = argument["--subtitle-export-check=".Length..];
            else if (argument.StartsWith("--hotwords-check=", StringComparison.OrdinalIgnoreCase)) HotwordsCheck = argument["--hotwords-check=".Length..];
            else if (argument.StartsWith("--formula-check=", StringComparison.OrdinalIgnoreCase)) FormulaCheck = argument["--formula-check=".Length..];
            else if (argument.StartsWith("--backup-check=", StringComparison.OrdinalIgnoreCase)) BackupCheck = argument["--backup-check=".Length..];
            else if (argument.StartsWith("--prompt-check=", StringComparison.OrdinalIgnoreCase)) PromptCheck = argument["--prompt-check=".Length..];
            else if (argument.StartsWith("--api-check=", StringComparison.OrdinalIgnoreCase)) ApiCheck = argument["--api-check=".Length..];
            else if (argument.StartsWith("--retrieval-check=", StringComparison.OrdinalIgnoreCase)) RetrievalCheck = argument["--retrieval-check=".Length..];
            else if (argument.StartsWith("--cloud-check=", StringComparison.OrdinalIgnoreCase)) CloudCheck = argument["--cloud-check=".Length..];
            else if (argument.StartsWith("--stream-check=", StringComparison.OrdinalIgnoreCase)) StreamCheck = argument["--stream-check=".Length..];
            else if (argument.StartsWith("--question-check=", StringComparison.OrdinalIgnoreCase)) QuestionCheck = argument["--question-check=".Length..];
            else if (argument.StartsWith("--pdf-check=", StringComparison.OrdinalIgnoreCase)) PdfCheck = argument["--pdf-check=".Length..];
            else if (argument.StartsWith("--gaze-check=", StringComparison.OrdinalIgnoreCase)) GazeCheck = argument["--gaze-check=".Length..];
            else if (argument.StartsWith("--bpe-check=", StringComparison.OrdinalIgnoreCase)) BpeCheck = argument["--bpe-check=".Length..];
            else if (argument.StartsWith("--glossary-search-check=", StringComparison.OrdinalIgnoreCase)) GlossarySearchCheck = argument["--glossary-search-check=".Length..];
            else if (argument.StartsWith("--process-check=", StringComparison.OrdinalIgnoreCase)) ProcessCheck = argument["--process-check=".Length..];
            else if (argument.StartsWith("--session-search-check=", StringComparison.OrdinalIgnoreCase)) SessionSearchCheck = argument["--session-search-check=".Length..];
            else if (argument.StartsWith("--series-briefing-check=", StringComparison.OrdinalIgnoreCase)) SeriesBriefingCheck = argument["--series-briefing-check=".Length..];
            else if (argument.StartsWith("--settings-scroll=", StringComparison.OrdinalIgnoreCase)
                     && double.TryParse(argument["--settings-scroll=".Length..], out var scroll)) SettingsScroll = scroll;
            else if (argument.StartsWith("--series-tab=", StringComparison.OrdinalIgnoreCase)
                     && int.TryParse(argument["--series-tab=".Length..], out var tab)) SeriesTab = tab;
            else if (argument.StartsWith("--section=", StringComparison.OrdinalIgnoreCase)
                     && Enum.TryParse<AppSection>(argument["--section=".Length..], true, out var section)) Section = section;
        }
    }
}
