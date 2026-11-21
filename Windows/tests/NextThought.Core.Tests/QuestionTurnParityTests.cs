using NextThought.Core;
using Xunit;

namespace NextThought.Core.Tests;

/// <summary>
/// Translated from QuestionTurnTests.swift: a turn is everything the other side said since I
/// last spoke, so a doubt put as a statement is answered once they stop, a second question in
/// the turn asks again for both, and my real reply ends their turn.
/// </summary>
public sealed class QuestionTurnParityTests
{
    private static readonly DateTimeOffset Start = DateTimeOffset.FromUnixTimeSeconds(1_000);

    [Fact]
    public void ADoubtPutAsAStatementIsAnsweredWhenTheyStop()
    {
        var turn = new QuestionTurn();
        turn.Heard("I am not fully convinced by the comparison in your results section.", Start);
        Assert.False(turn.IsDue(Start.AddSeconds(1), "", replying: false));
        Assert.True(turn.IsDue(Start.AddSeconds(2.1), "", replying: false));
        turn.Asked();
        Assert.False(turn.IsDue(Start.AddSeconds(4), "", replying: false));
    }

    [Fact]
    public void NothingIsAskedWhileTheyAreStillTalking()
    {
        var turn = new QuestionTurn();
        turn.Heard("Thank you for the presentation, I have two questions for you.", Start);
        Assert.False(turn.IsDue(Start.AddSeconds(3), "The first one is about", replying: false));
    }

    [Fact]
    public void ASecondQuestionInTheSameTurnAsksAgainForBoth()
    {
        var turn = new QuestionTurn();
        turn.Heard("First, how do you estimate the channel?", Start);
        Assert.True(turn.WorthAsking());
        turn.Asked();
        turn.Heard("Okay.", Start.AddSeconds(2));
        Assert.False(turn.WorthAsking());
        turn.Heard("And second, why did you choose this baseline instead of a stronger one?", Start.AddSeconds(6));
        Assert.True(turn.WorthAsking());
        Assert.StartsWith("First, how do you estimate the channel?", turn.Text());
        Assert.EndsWith("stronger one?", turn.Text());
        Assert.True(turn.IsDue(Start.AddSeconds(8.5), "", replying: false));
    }

    [Fact]
    public void MyReplyEndsTheirTurn()
    {
        var turn = new QuestionTurn();
        turn.Heard("Could you explain the loss function on slide twelve in more detail please?", Start);
        turn.Spoke("Mm.");
        Assert.False(turn.IsEmpty);
        turn.Spoke("Sure, the loss has two terms.");
        Assert.True(turn.IsEmpty);
        turn.Heard("And what about the cost?", Start.AddSeconds(20));
        Assert.Equal("And what about the cost?", turn.Text());
    }

    [Fact]
    public void StartingToReplyAsksAtOnce()
    {
        var turn = new QuestionTurn();
        turn.Heard("It seems to me the gain mostly comes from the larger dataset.", Start);
        Assert.True(turn.IsDue(Start.AddSeconds(0.4), "", replying: true));
    }

    [Fact]
    public void ShortRemarksAndOldTurnsAreLeftAlone()
    {
        var turn = new QuestionTurn();
        turn.Heard("Okay, thank you.", Start);
        Assert.False(turn.IsDue(Start.AddSeconds(3), "", replying: false));
        turn.Heard("That was a very clear talk and I enjoyed it a lot.", Start.AddSeconds(1));
        Assert.False(turn.IsDue(Start.AddSeconds(40), "", replying: false));
    }

    [Fact]
    public void ALongPauseStartsANewTurn()
    {
        var turn = new QuestionTurn();
        turn.Heard("How large is the array in your simulation?", Start);
        turn.Asked();
        turn.Heard("Why is the near field model needed here?", Start.AddSeconds(60));
        Assert.Equal(0, turn.Requests);
        Assert.Equal("Why is the near field model needed here?", turn.Text());
    }

    [Fact]
    public void WordsCountLatinWordsAndChineseCharacters()
    {
        Assert.Equal(4, QuestionTurn.CountWords("near-field XL-MIMO isn't new"));
        Assert.Equal(8, QuestionTurn.CountWords("为什么用这个基线"));
    }
}
