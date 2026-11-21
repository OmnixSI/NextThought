namespace NextThought.Core;

/// <summary>
/// The other side's turn in a question round: everything they have said since I last spoke
/// (port of QuestionTurn.swift). After a talk an examiner's turn is often several sentences or a
/// doubt put as a statement, so the answer is for the whole turn and is asked for once they stop.
/// </summary>
public sealed class QuestionTurn
{
    public sealed class Config
    {
        /// <summary>Silence after their last sentence that ends the turn.</summary>
        public double Quiet { get; init; } = 2;

        /// <summary>A pause this long between two of their sentences starts a new turn.</summary>
        public double Gap { get; init; } = 25;

        /// <summary>An unanswered turn is dropped after this long.</summary>
        public double Patience { get; init; } = 30;

        /// <summary>Words the turn must hold before an answer is asked for.</summary>
        public int MinimumWords { get; init; } = 8;

        /// <summary>Words said after an answer was asked for that make it worth asking again.</summary>
        public int Growth { get; init; } = 8;

        /// <summary>Words of mine that end their turn; "yes" and "mm" do not.</summary>
        public int Reply { get; init; } = 3;

        public int Limit { get; init; } = 1600;
    }

    private readonly List<string> sentences = [];

    public Config Settings { get; init; } = new();
    public IReadOnlyList<string> Sentences => sentences;
    public DateTimeOffset? LastHeardAt { get; private set; }

    /// <summary>Words of the turn that the latest answer request covered.</summary>
    public int AnsweredWords { get; private set; }

    public int Requests { get; private set; }
    public bool IsEmpty => sentences.Count == 0;
    public int Words => sentences.Sum(CountWords);

    /// <summary>Words they have said that no answer covers yet.</summary>
    public int Unanswered => Math.Max(0, Words - AnsweredWords);

    /// <summary>The turn as one question, with the sentence still being said.</summary>
    public string Text(string partial = "")
    {
        var live = partial.Trim();
        var all = live.Length == 0 ? sentences : sentences.Append(live);
        var joined = string.Join(" ", all).Trim();
        return Settings.Limit > 0 && joined.Length > Settings.Limit ? joined[^Settings.Limit..].Trim() : joined;
    }

    /// <summary>A finished sentence of theirs.</summary>
    public void Heard(string sentence, DateTimeOffset date)
    {
        var text = sentence.Trim();
        if (text.Length == 0) return;
        if (LastHeardAt is { } last && (date - last).TotalSeconds > Settings.Gap) Reset();
        sentences.Add(text);
        if (LastHeardAt is null || date > LastHeardAt) LastHeardAt = date;
    }

    /// <summary>A finished sentence of mine: a real reply ends their turn.</summary>
    public void Spoke(string sentence)
    {
        if (CountWords(sentence) >= Settings.Reply) Reset();
    }

    public void Reset()
    {
        sentences.Clear();
        LastHeardAt = null;
        AnsweredWords = 0;
        Requests = 0;
    }

    /// <summary>Whether an answer is worth asking for: the first always, later ones only after growth.</summary>
    public bool WorthAsking(string partial = "")
    {
        var total = Words + CountWords(partial);
        return Requests == 0 ? total >= 2 : total - AnsweredWords >= Settings.Growth;
    }

    /// <summary>They have stopped (or I have started to reply) and what they said has no answer.</summary>
    public bool IsDue(DateTimeOffset now, string partial, bool replying)
    {
        if (LastHeardAt is not { } last || sentences.Count == 0 || partial.Trim().Length != 0) return false;
        var waited = (now - last).TotalSeconds;
        if (waited > Settings.Patience || (!replying && waited < Settings.Quiet)) return false;
        return Requests == 0 ? Words >= Settings.MinimumWords : Unanswered >= Settings.Growth;
    }

    /// <summary>An answer was asked for with everything said so far.</summary>
    public void Asked(string partial = "")
    {
        AnsweredWords = Words + CountWords(partial);
        Requests++;
    }

    /// <summary>Words in Latin script count one each, Chinese characters one each.</summary>
    public static int CountWords(string text)
    {
        var count = 0;
        var inWord = false;
        foreach (var character in text)
        {
            if (character is >= '\u3400' and <= '\u9FFF')
            {
                count++;
                inWord = false;
            }
            else if (char.IsLetterOrDigit(character))
            {
                if (!inWord) count++;
                inWord = true;
            }
            else if (character != '\'' && character != '-')
            {
                inWord = false;
            }
        }
        return count;
    }
}
