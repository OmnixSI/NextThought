using Xunit;

namespace NextThought.Tests;

/// <summary>
/// E6: the answer language choice, the bilingual answer layout and the QuestionTurn wiring
/// (AnswerLanguage.swift, QuestionTurn.swift and the LiveSessionView call sites).
/// </summary>
public sealed class AnswerLanguageParityTests
{
    private static string Root()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Windows", "src", "NextThought.Desktop", "Views", "LiveView.axaml")))
                return directory.FullName;
            directory = directory.Parent;
        }
        throw new DirectoryNotFoundException("repository root not found");
    }

    private static string Desktop(params string[] parts) =>
        Path.Combine([Root(), "Windows", "src", "NextThought.Desktop", .. parts]);

    private static string Read(params string[] parts) => File.ReadAllText(Desktop(parts));

    [Fact]
    public void QuestionTurnIsWiredIntoTheAnswerPath()
    {
        Assert.True(File.Exists(Desktop("AppState.QuestionTurn.cs")));
        var turn = Read("AppState.QuestionTurn.cs");
        Assert.Contains("private readonly QuestionTurn questionTurn", turn);
        Assert.Contains("questionTurn.Heard(", turn);
        Assert.Contains("questionTurn.Spoke(", turn);
        Assert.Contains("questionTurn.WorthAsking(", turn);
        Assert.Contains("questionTurn.IsDue(", turn);
        Assert.Contains("questionTurn.Asked(", turn);
        Assert.Contains("public bool AnswersWholeTurns", turn);
        Assert.Contains("MeetingMode.Academic", turn);
        Assert.Contains("Settings.PresenterMode || myTurnWords >= 300", turn);
        Assert.Contains("TryAnswerQuestionTurn", turn);
        Assert.Contains("QuestionTurnAnswerText", turn);
        Assert.Contains("ResetQuestionTurn", turn);

        var live = Read("AppState.Live.cs");
        Assert.Contains("NoteQuestionTurn(line)", live);
        Assert.Contains("TryAnswerQuestionTurn(now, replying: speaking)", live);
        Assert.Contains("QuestionTurnAnswerText(question.FullText)", live);
        Assert.Contains("MarkQuestionTurnAsked(QuestionTurnPartial)", live);
        Assert.Contains("ResetQuestionTurn();", live);
    }

    [Fact]
    public void BilingualAnswersGetTheMacPairLayout()
    {
        Assert.True(File.Exists(Desktop("Views", "BilingualAnswerText.axaml")));
        var code = Read("Views", "BilingualAnswerText.axaml.cs");
        Assert.Contains("public partial class BilingualAnswerText", code);
        Assert.Contains("BilingualAnswer.IsBilingual", code);
        Assert.Contains("BilingualAnswer.GlossOf", code);
        Assert.Contains("MarkdownPlain.Strip", code);
        Assert.Contains("SelectableTextBlock", code);

        var live = Read("Views", "LiveView.axaml");
        Assert.Contains("<views:BilingualAnswerText Text=\"{Binding DisplayedAnswerText}\"", live);
        Assert.Contains("IsVisible=\"{Binding IsBilingualAnswer}\"", live);
        Assert.Contains("IsVisible=\"{Binding !IsBilingualAnswer}\"", live);

        var ui = Read("AppState.LiveUi.cs");
        Assert.Contains("public bool IsBilingualAnswer", ui);
        Assert.Contains("IslandHeadlineGloss", ui);
        Assert.Contains("IslandDetailGloss", ui);
    }

    [Fact]
    public void TheCueIslandShowsTheMeaningUnderEachLine()
    {
        // C8: the island renders the meaning under each line through CueRow.withGloss — the cue
        // row control draws Head/Rest/Gloss, the model feeds the gloss from CueScript.
        var island = Read("Views", "CueIslandWindow.axaml");
        Assert.Contains("island:IslandCueRow", island);
        var row = Read("Views", "Island", "IslandCueRow.cs");
        Assert.Contains("row.Gloss", row);
        var model = Read("Views", "Island", "CueIslandViewModel.cs");
        Assert.Contains("Gloss = line.Gloss", model);
        var ui = Read("AppState.LiveUi.cs");
        Assert.Contains("IslandLines", ui);
    }

    [Fact]
    public void TheAnswerSettingsMenuOffersTheLanguageAndGloss()
    {
        var live = Read("Views", "LiveView.axaml");
        Assert.Contains("{loc:Tr 回答语言}", live);
        Assert.Contains("AnswerLanguage_Click", live);
        Assert.Contains("{loc:Tr 英文回答附中文对照}", live);
        Assert.Contains("AnswerGloss_Click", live);
        var code = Read("Views", "LiveView.axaml.cs");
        Assert.Contains("state.Settings.AnswerLanguage = language", code);
        Assert.Contains("_ = state.Settings.SaveAsync()", code);
    }

    [Fact]
    public void TheLiveSelfTestCoversTheLanguageAndBilingualRendering()
    {
        var check = Read("SelfTest", "Checks", "RunLiveUiCheckAsync.cs");
        Assert.Contains("AnswerLanguage.Fields", check);
        Assert.Contains("IsBilingualAnswer", check);
        Assert.Contains("BilingualAnswer.Spoken", check);
        Assert.Contains("IslandHeadlineGloss", check);
        Assert.Contains("bilingual-answer.png", check);
    }
}
