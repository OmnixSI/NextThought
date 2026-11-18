using NextThought.Core;
using Xunit;

namespace NextThought.Core.Tests;

/// <summary>Tests for QuestionTurn (turn building and answer timing).</summary>
public sealed class QuestionTurnTests
{
    private static readonly DateTimeOffset Start = new(2026, 11, 18, 10, 0, 0, TimeSpan.FromHours(8));

    [Theory]
    [InlineData("hello world", 2)]
    [InlineData("你好世界", 4)]
    [InlineData("hello你好", 3)]
    [InlineData("don't", 1)]
    [InlineData("state-of-the-art", 1)]
    [InlineData("3 apples", 2)]
    [InlineData("", 0)]
    public void WordsCountLatinRunsAndCjkCharacters(string text, int expected)
    {
        Assert.Equal(expected, QuestionTurn.CountWords(text));
    }

    [Fact]
    public void TurnTextJoinsFinishedAndLiveSentences()
    {
        var turn = new QuestionTurn();
        turn.Heard("First sentence.", Start);
        turn.Heard("Second one.", Start.AddSeconds(1));
        Assert.Equal("First sentence. Second one.", turn.Text());
        Assert.Equal("First sentence. Second one. And now", turn.Text("And now"));
        Assert.Equal(4, turn.Words);
    }

    [Fact]
    public void LongGapStartsANewTurn()
    {
        var turn = new QuestionTurn();
        turn.Heard("one two", Start);
        turn.Heard("three four", Start.AddSeconds(26));
        Assert.Equal("three four", turn.Text());
        Assert.Equal(Start.AddSeconds(26), turn.LastHeardAt);
    }

    [Fact]
    public void OnlyARealReplyEndsTheirTurn()
    {
        var turn = new QuestionTurn();
        turn.Heard("a b c d", Start);
        turn.Spoke("yes");
        Assert.False(turn.IsEmpty);
        turn.Spoke("I agree now");
        Assert.True(turn.IsEmpty);
    }

    [Fact]
    public void AskingAgainNeedsGrowth()
    {
        var turn = new QuestionTurn();
        Assert.False(turn.WorthAsking("hi"));
        Assert.True(turn.WorthAsking("hi there"));
        turn.Asked("hi there");
        turn.Heard("one two three four five six seven eight", Start);
        Assert.False(turn.WorthAsking());
        turn.Heard("nine ten", Start.AddSeconds(1));
        Assert.True(turn.WorthAsking());
    }

    [Fact]
    public void DueRespectsQuietPatienceAndPartialSpeech()
    {
        var turn = new QuestionTurn();
        turn.Heard("one two three four five six seven eight", Start);
        Assert.False(turn.IsDue(Start.AddSeconds(1), "", replying: false));
        Assert.True(turn.IsDue(Start.AddSeconds(2), "", replying: false));
        Assert.True(turn.IsDue(Start.AddSeconds(1), "", replying: true));
        Assert.False(turn.IsDue(Start.AddSeconds(2), "still talking", replying: false));
        Assert.False(turn.IsDue(Start.AddSeconds(31), "", replying: false));
    }

    [Fact]
    public void AskedCoversEverythingSaidSoFar()
    {
        var turn = new QuestionTurn();
        turn.Heard("one two three four five six seven eight", Start);
        turn.Asked();
        Assert.Equal(8, turn.AnsweredWords);
        Assert.Equal(1, turn.Requests);
        Assert.Equal(0, turn.Unanswered);
        Assert.False(turn.IsDue(Start.AddSeconds(5), "", replying: false));
        turn.Heard("nine ten eleven twelve thirteen fourteen fifteen sixteen", Start.AddSeconds(5));
        Assert.Equal(8, turn.Unanswered);
        Assert.True(turn.IsDue(Start.AddSeconds(7), "", replying: false));
    }

    [Fact]
    public void TextIsLimitedAndResetClearsEverything()
    {
        var turn = new QuestionTurn { Settings = new QuestionTurn.Config { Limit = 10 } };
        turn.Heard("a very long sentence that will be cut", Start);
        Assert.Equal(10, turn.Text().Length);
        turn.Asked();
        turn.Reset();
        Assert.True(turn.IsEmpty);
        Assert.Null(turn.LastHeardAt);
        Assert.Equal(0, turn.AnsweredWords);
        Assert.Equal(0, turn.Requests);
    }
}
