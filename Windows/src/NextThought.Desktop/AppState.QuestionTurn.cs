using NextThought.Core;

namespace NextThought.Desktop;

/// <summary>
/// E6: the other side's question round (port of QuestionTurn.swift + the AppState+Live call sites).
/// After my own talk an examiner's turn is often several sentences or a doubt put as a statement,
/// so the answer is for the whole turn and is asked for once they stop. Only 汇报/答辩 meetings
/// answer whole turns, exactly like Mac answersWholeTurns.
/// </summary>
public partial class AppState
{
    private readonly QuestionTurn questionTurn = new();
    private int myTurnWords;

    /// <summary>Mac answersWholeTurns: 汇报 / 答辩, online, and I have been presenting.</summary>
    public bool AnswersWholeTurns =>
        SelectedMode == MeetingMode.Academic
        && CurrentSession is { InPerson: false }
        && (Settings.PresenterMode || myTurnWords >= 300);

    public string QuestionTurnText => questionTurn.Text();

    public int QuestionTurnWords => questionTurn.Words;

    public int QuestionTurnSentenceCount => questionTurn.Sentences.Count;

    public int QuestionTurnRequests => questionTurn.Requests;

    public bool HasQuestionTurn => !questionTurn.IsEmpty;

    /// <summary>The unfinished sentence, without the "系统 · " display prefix.</summary>
    public string QuestionTurnPartial
    {
        get
        {
            const string prefix = "系统 · ";
            var text = SystemPartial;
            return text.StartsWith(prefix, StringComparison.Ordinal) ? text[prefix.Length..] : text;
        }
    }

    /// <summary>Feeds one finished line into the turn (Mac questionTurn.heard / .spoke).</summary>
    private void NoteQuestionTurn(TranscriptLine line)
    {
        if (!Recording || string.IsNullOrWhiteSpace(line.Text)) return;
        if (line.Speaker == "remote")
        {
            questionTurn.Heard(line.Text, line.EndedAt ?? line.Date);
        }
        else
        {
            questionTurn.Spoke(line.Text);
            myTurnWords += QuestionTurn.CountWords(line.Text);
        }
        RaiseQuestionTurn();
    }

    /// <summary>Mac questionTurn.asked: this request covers everything heard so far.</summary>
    private void MarkQuestionTurnAsked(string partial = "")
    {
        if (questionTurn.IsEmpty) return;
        questionTurn.Asked(partial);
        RaiseQuestionTurn();
    }

    private void ResetQuestionTurn()
    {
        questionTurn.Reset();
        myTurnWords = 0;
        RaiseQuestionTurn();
    }

    /// <summary>
    /// Mac answerTurnIfDue: they stopped (or I started replying) and part of the turn has no
    /// answer yet. Called from the candidate timer.
    /// </summary>
    private bool TryAnswerQuestionTurn(DateTimeOffset now, bool replying)
    {
        if (!Recording || Stopping || Answering || !AnswersWholeTurns) return false;
        if (!questionTurn.IsDue(now, QuestionTurnPartial, replying)) return false;
        if (questionTurn.Words > 250 || questionTurn.Requests >= 4) return false;
        var question = questionTurn.Text();
        if (question.Length == 0) return false;
        DetectedQuestion = question;
        _ = AnswerAsync(question, automatic: true);
        return true;
    }

    /// <summary>The text the next automatic answer should cover: the whole turn or the trigger.</summary>
    private string QuestionTurnAnswerText(string trigger)
    {
        if (!AnswersWholeTurns || questionTurn.IsEmpty) return trigger;
        var partial = QuestionTurnPartial;
        if (!questionTurn.WorthAsking(partial)) return trigger;
        var text = questionTurn.Text(partial);
        return text.Length == 0 ? trigger : text;
    }

    public void RaiseQuestionTurn()
    {
        OnPropertyChanged(nameof(QuestionTurnText));
        OnPropertyChanged(nameof(QuestionTurnWords));
        OnPropertyChanged(nameof(QuestionTurnSentenceCount));
        OnPropertyChanged(nameof(QuestionTurnRequests));
        OnPropertyChanged(nameof(HasQuestionTurn));
        OnPropertyChanged(nameof(AnswersWholeTurns));
    }
}
