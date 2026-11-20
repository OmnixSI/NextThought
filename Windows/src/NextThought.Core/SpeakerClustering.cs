namespace NextThought.Core;

/// <summary>
/// Groups voiceprints as lines arrive, never revising a line already labelled
/// (port of VoiceClusterer in SpeakerClustering.swift). A line joins the closest voice when
/// similar enough; a long line unlike every voice starts a new one; anything else stays
/// unlabelled.
/// </summary>
public sealed class VoiceClusterer
{
    /// <summary>A line must last this long to start a new voice; short ones are unreliable.</summary>
    public const double NewSeconds = 2.0;

    /// <summary>Cosine similarity at or above which a line joins the closest voice.</summary>
    public const float Join = 0.5f;

    /// <summary>A new voice starts only when the closest voice is less similar than this.</summary>
    public const float StartNew = 0.5f;

    private readonly List<float[]> sums = [];

    /// <summary>Per voice, the sum of unit-length voiceprints; a merged-away voice is all zeros.</summary>
    public IReadOnlyList<float[]> Sums => sums;

    /// <summary>How many voices exist, merged-away ones included (ids never change).</summary>
    public int VoiceCount => sums.Count;

    /// <summary>The user said two voices are one: later lines like either go to <paramref name="into"/>.</summary>
    public void Merge(int from, int into)
    {
        if (from < 0 || from >= sums.Count || into < 0 || into >= sums.Count || from == into) return;
        if (sums[into].Length != sums[from].Length) return;
        for (var index = 0; index < sums[into].Length; index++) sums[into][index] += sums[from][index];
        // Never the closest again: similarity to a zero vector is 0.
        sums[from] = new float[sums[from].Length];
    }

    /// <summary>The voice of a line, counting it towards that voice; null when unsure.</summary>
    public int? Label(float[] vector, double seconds)
    {
        var best = -1;
        var bestSimilarity = double.NegativeInfinity;
        for (var index = 0; index < sums.Count; index++)
        {
            var similarity = Similarity(vector, sums[index]);
            if (similarity > bestSimilarity) { bestSimilarity = similarity; best = index; }
        }
        if (best >= 0 && bestSimilarity >= Join)
        {
            var sum = sums[best];
            for (var index = 0; index < sum.Length && index < vector.Length; index++) sum[index] += vector[index];
            return best;
        }
        if (seconds >= NewSeconds && (sums.Count == 0 || bestSimilarity < StartNew))
        {
            sums.Add((float[])vector.Clone());
            return sums.Count - 1;
        }
        return null;
    }

    /// <summary>Cosine similarity of a unit-length voiceprint to a voice's sum; zero sum gives 0.</summary>
    public static double Similarity(float[] vector, float[] sum)
    {
        var count = Math.Min(vector.Length, sum.Length);
        float dot = 0, squares = 0;
        for (var index = 0; index < count; index++) dot += vector[index] * sum[index];
        foreach (var value in sum) squares += value * value;
        return dot / Math.Max(Math.Sqrt(squares), float.Epsilon);
    }
}

/// <summary>
/// What the transcript shows for lines from the other side or the room. A voice gets a number
/// only after <see cref="ShownAfter"/> substantive lines, and only when at least two voices
/// have, so a one-to-one call stays "对方". The user's own lines are never told apart.
/// </summary>
public static class SpeakerLabels
{
    public const int ShownAfter = 5;

    /// <summary>Three words, or four CJK characters, are enough to say whose voice it is.</summary>
    public static bool Substantive(string text)
    {
        var words = text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
            .Count(word => word.Any(char.IsLetter));
        if (words >= 3) return true;
        var wide = text.Count(character =>
            IsIdeographic(character) || character is >= '\u3040' and <= '\u30FF' || character is >= '\uAC00' and <= '\uD7A3');
        return wide >= 4;
    }

    /// <summary>Voice id → 1-based speaker number, for the voices on show.</summary>
    public static IReadOnlyDictionary<int, int> VoiceNumbers(IReadOnlyList<TranscriptLine> lines, ISet<int>? meVoices = null)
    {
        var count = new Dictionary<int, int>();
        var reached = new Dictionary<int, int>();
        for (var index = 0; index < lines.Count; index++)
        {
            var line = lines[index];
            if (line.Speaker == "me") continue;
            if (line.Voice is not { } voice || meVoices?.Contains(voice) == true || !Substantive(line.Text)) continue;
            count[voice] = count.GetValueOrDefault(voice) + 1;
            if (count[voice] == ShownAfter) reached[voice] = index;
        }
        var shown = reached.Keys.OrderBy(voice => reached[voice]).ToList();
        if (shown.Count < 2) return new Dictionary<int, int>();
        var numbers = new Dictionary<int, int>();
        for (var position = 0; position < shown.Count; position++) numbers[shown[position]] = position + 1;
        return numbers;
    }

    /// <summary>Line id → 1-based speaker number; lines without one keep showing as 对方 / 现场.</summary>
    public static IReadOnlyDictionary<Guid, int> Numbers(IReadOnlyList<TranscriptLine> lines, ISet<int>? meVoices = null)
    {
        var number = VoiceNumbers(lines, meVoices);
        var output = new Dictionary<Guid, int>();
        foreach (var line in lines)
        {
            if (line.Speaker == "me") continue;
            if (line.Voice is { } voice && number.TryGetValue(voice, out var value)) output[line.Id] = value;
        }
        return output;
    }

    /// <summary>Speaker number → the name the user gave (empty names are ignored).</summary>
    public static IReadOnlyDictionary<int, string> Names(
        IReadOnlyDictionary<string, string>? voiceNames, IReadOnlyList<TranscriptLine> lines, ISet<int>? meVoices = null)
    {
        if (voiceNames is null || voiceNames.Count == 0) return new Dictionary<int, string>();
        var output = new Dictionary<int, string>();
        foreach (var (voice, number) in VoiceNumbers(lines, meVoices))
            if (voiceNames.TryGetValue(voice.ToString(), out var name) && name.Length > 0) output[number] = name;
        return output;
    }

    /// <summary>Lines of the other side or the room that are in a voice recognised as the user.</summary>
    public static IReadOnlySet<Guid> MeLines(IReadOnlyList<TranscriptLine> lines, ISet<int> meVoices)
    {
        var ids = new HashSet<Guid>();
        if (meVoices.Count == 0) return ids;
        foreach (var line in lines)
            if (line.Speaker != "me" && line.Voice is { } voice && meVoices.Contains(voice)) ids.Add(line.Id);
        return ids;
    }

    /// <summary>The user marked or unmarked a voice as themselves; <paramref name="into"/> keeps the mark.</summary>
    public static IReadOnlyList<int>? MergedMe(IReadOnlyList<int>? meVoices, int from, int into)
    {
        if (meVoices is null) return null;
        var set = new HashSet<int>(meVoices);
        if (set.Remove(from)) set.Add(into);
        return set.Count == 0 ? null : set.OrderBy(voice => voice).ToList();
    }

    /// <summary>Lines where the speaker changes: the name shows there only, as chat apps do.</summary>
    public static IReadOnlySet<Guid> RunStarts(
        IReadOnlyList<TranscriptLine> lines, IReadOnlyDictionary<Guid, int> numbers, ISet<Guid>? mine = null)
    {
        var output = new HashSet<Guid>();
        string? last = null;
        foreach (var line in lines)
        {
            var key = line.Speaker == "me" || mine?.Contains(line.Id) == true
                ? "me"
                : numbers.TryGetValue(line.Id, out var number) ? "n" + number : "?";
            if (key != last) output.Add(line.Id);
            last = key;
        }
        return output;
    }

    /// <summary>"George", else "说话人 2" / "Speaker 2".</summary>
    public static string Label(int number, IReadOnlyDictionary<int, string> names, bool english = false) =>
        names.TryGetValue(number, out var name) ? name : english ? $"Speaker {number}" : $"说话人 {number}";

    /// <summary>Distinct voices on show, for the people row.</summary>
    public static int ShownCount(IReadOnlyList<TranscriptLine> lines) =>
        Numbers(lines).Values.Distinct().Count();

    /// <summary>A name worth keeping: one line, trimmed, at most 24 characters; null when empty.</summary>
    /// <summary>B21: truncate by Unicode grapheme clusters, never inside one.</summary>
    private static string TruncateGraphemes(string text, int maximum)
    {
        var enumerator = System.Globalization.StringInfo.GetTextElementEnumerator(text);
        var count = 0;
        while (enumerator.MoveNext())
        {
            count++;
            if (count == maximum) return text[..(enumerator.ElementIndex + enumerator.GetTextElement().Length)];
        }
        return text;
    }

    public static string? CleanName(string raw)
    {
        var single = raw.ReplaceLineEndings(" ");
        var trimmed = single.Trim();
        trimmed = TruncateGraphemes(trimmed, 24).Trim();
        return trimmed.Length == 0 ? null : trimmed;
    }

    /// <summary>The part before a bracket: "Kai Wu（导师）" reads as "Kai Wu".</summary>
    public static string ShortName(string name)
    {
        var bracket = name.IndexOfAny(['（', '(']);
        if (bracket < 0) return name;
        var shortName = name[..bracket].Trim();
        return shortName.Length == 0 ? name : shortName;
    }

    /// <summary>Notes written before a voice had a name say "说话人 2"; rewrite them once named.</summary>
    public static string Naming(string text, IReadOnlyDictionary<int, string> names)
    {
        if (names.Count == 0 || (!text.Contains("说话人") && !text.Contains("Speaker"))) return text;
        var result = text;
        // Longest numbers first, so "说话人 12" is not read as "说话人 1" and a "2".
        foreach (var (number, name) in names.OrderByDescending(pair => pair.Key))
        {
            var shortName = ShortName(name);
            foreach (var label in new[] { $"说话人 {number}", $"说话人{number}", $"Speaker {number}" })
            {
                var searchFrom = 0;
                while ((searchFrom = result.IndexOf(label, searchFrom, StringComparison.Ordinal)) >= 0)
                {
                    var after = searchFrom + label.Length;
                    if (after < result.Length && char.IsNumber(result[after])) { searchFrom = after; continue; }
                    result = result[..searchFrom] + shortName + result[after..];
                    searchFrom += shortName.Length;
                }
            }
        }
        return result;
    }

    /// <summary>The user merged voice <paramref name="from"/> into <paramref name="into"/>: move the name.</summary>
    public static IReadOnlyDictionary<string, string>? Merged(
        IReadOnlyDictionary<string, string>? voiceNames, int from, int into)
    {
        if (voiceNames is null) return null;
        var names = new Dictionary<string, string>(voiceNames);
        if (names.Remove(from.ToString(), out var moved) && !names.ContainsKey(into.ToString()))
            names[into.ToString()] = moved;
        return names.Count == 0 ? null : names;
    }

    /// <summary>Line id → the speaker as the notes model reads it.</summary>
    public static IReadOnlyDictionary<Guid, string> PromptLabels(
        IReadOnlyList<TranscriptLine> lines, IReadOnlyDictionary<string, string>? voiceNames,
        ISet<int>? meVoices, bool english)
    {
        var shown = Numbers(lines, meVoices);
        var named = Names(voiceNames, lines, meVoices)
            .Where(pair => CleanName(pair.Value) is not null)
            .ToDictionary(pair => pair.Key, pair => CleanName(pair.Value)!);
        var output = shown.ToDictionary(pair => pair.Key, pair => Label(pair.Value, named, english));
        if (meVoices is not null)
            foreach (var id in MeLines(lines, meVoices)) output[id] = english ? "Me" : "我";
        return output;
    }

    /// <summary>A paragraph for the notes prompt when voices are on show; null otherwise.</summary>
    public static string? PromptNote(
        IReadOnlyList<TranscriptLine> lines, IReadOnlyDictionary<string, string>? voiceNames,
        ISet<int>? meVoices, bool english, bool inPerson)
    {
        var labels = PromptLabels(lines, voiceNames, meVoices, english);
        if (labels.Count == 0) return null;
        var shown = Numbers(lines, meVoices);
        var hasMe = meVoices is not null && MeLines(lines, meVoices).Count > 0;
        if (shown.Count == 0)
            return english
                ? "Lines labelled \"Me\" are the user's own words, recognised by voice (occasionally wrong)."
                : "转写里标着“我”的行是用户自己的发言，按声音认出，偶尔会有误判。";
        var seen = new SortedDictionary<int, string>();
        foreach (var (id, text) in labels)
            if (shown.TryGetValue(id, out var number)) seen[number] = text;
        var people = string.Join(english ? ", " : "、", seen.Values);
        var unknown = inPerson ? (english ? "Room" : "现场") : (english ? "Them" : "对方");
        var meNote = hasMe ? (english ? " Lines labelled \"Me\" are the user's own words." : "标着“我”的行是用户自己的发言。") : "";
        if (english)
            return "Speakers (this paragraph overrides what the system instructions say about not telling people apart): lines labelled with a person's name " +
                   $"or \"Speaker N\" were told apart by voice, which is occasionally wrong. Lines labelled \"{unknown}\" are speech that could not be matched to a person. " +
                   $"People in this meeting: {people}. Where the transcript makes it clear, say who is responsible on action items (owner) and who made each point. " +
                   "\"Speaker N\" is not a real name: never guess a name from it; use a name only when the words themselves give it. " +
                   "Words in brackets after a name say who the person is (\"Kai Wu (advisor)\"): read what they say accordingly (an advisor, examiner " +
                   $"or client states requirements; a peer suggests), and write the name without the brackets in the text.{meNote}";
        return "说话人区分（本段优先于系统说明里“无法区分说话人”的描述）：转写里标着人名或“说话人 N”的行，是按声音区分出的不同的人，偶尔会有误判；" +
               $"标着“{unknown}”的行是没能认出是谁的发言。本场出现的人：{people}。在转写能看清的情况下，待办的 owner 和各条要点都写明是谁；" +
               "“说话人 N”不是真名，不要凭它猜名字，只有原话里点了名才用名字。名字后面括号里的字是这个人的身份（例如“Kai Wu（导师）”）：" +
               $"按身份理解每句话的分量（导师、评委、客户说的是要求或意见，同学说的是建议），正文里称呼时只写名字，不带括号。{meNote}";
    }

    private static bool IsIdeographic(char character) =>
        character is >= '\u3400' and <= '\u4DBF'
        || character is >= '\u4E00' and <= '\u9FFF'
        || character is >= '\uF900' and <= '\uFAFF';
}
