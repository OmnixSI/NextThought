namespace NextThought.Core;

public sealed record ScreenTextRegion(string Text, IReadOnlyList<double> Bounds, double Confidence);

public sealed record ScreenContext
{
    public string FrameId { get; init; } = "";
    public string TargetId { get; init; } = "";
    public string Source { get; init; } = "";
    public DateTimeOffset CapturedAt { get; init; }
    public DateTimeOffset RecognizedAt { get; init; } = DateTimeOffset.UtcNow;
    public IReadOnlyList<ScreenTextRegion> Regions { get; init; } = [];
    public IReadOnlyList<double>? CaptureRegion { get; init; }
    public string? EnhancedText { get; init; }
    public IReadOnlyList<double>? EnhancedRegion { get; init; }
    public string Recognizer { get; init; } = "system";

    /// <summary>Recognized text, enhanced when available.</summary>
    public string Text => EnhancedText ?? string.Join("\n", Regions.Select(region => region.Text));

    public bool LowConfidence => Regions.Count == 0 || Regions.Any(region => region.Confidence < 0.8);

    public bool IsFresh(DateTimeOffset now, double maximumAge = 2)
    {
        var age = (now - CapturedAt).TotalSeconds;
        return age >= 0 && age <= maximumAge;
    }
}

/// <summary>Questions that point at the screen (port of ScreenReference in ScreenContext.swift).</summary>
public static class ScreenReference
{
    private static readonly string[] References =
    [
        "这一页", "这页", "这个图", "这张图", "这个表", "这张表", "这个公式", "这个地方", "这里", "这部分",
        "屏幕", "左边", "右边", "上面这个", "下面这个", "这段代码", "这个文件",
        "this slide", "this page", "this figure", "this chart", "this table", "this equation",
        "on screen", "on the screen", "left side", "right side", "this code", "this part"
    ];

    private static readonly string[] ImageNeeds =
    [
        "图", "表", "公式", "这里", "这个地方", "左边", "右边", "位置", "箭头", "颜色",
        "figure", "chart", "table", "equation", "diagram", "left", "right", "here"
    ];

    public static bool IsReference(string question) =>
        References.Any(reference => question.ToLowerInvariant().Contains(reference, StringComparison.Ordinal));

    public static bool NeedsImage(string question) =>
        ImageNeeds.Any(need => question.ToLowerInvariant().Contains(need, StringComparison.Ordinal));

    public static bool IsRelated(string question, ScreenContext context) =>
        IsReference(question) || TextSimilarity.Relevant(screen: context.Text, recentSpeech: question);
}

/// <summary>Character-bigram similarity for OCR and speech text (port of TextSimilarity).</summary>
public static class TextSimilarity
{
    /// <summary>Lowercased letters, digits and CJK characters only.</summary>
    public static List<char> Normalized(string text) =>
        text.ToLowerInvariant().Where(char.IsLetterOrDigit).ToList();

    public static HashSet<string> Bigrams(string text)
    {
        var characters = Normalized(text);
        var result = new HashSet<string>(StringComparer.Ordinal);
        if (characters.Count < 2)
        {
            if (characters.Count == 1) result.Add(characters[0].ToString());
            return result;
        }
        for (var index = 0; index < characters.Count - 1; index++)
            result.Add(new string([characters[index], characters[index + 1]]));
        return result;
    }

    public static double Jaccard(HashSet<string> a, HashSet<string> b)
    {
        if (a.Count == 0 && b.Count == 0) return 1;
        var union = new HashSet<string>(a, StringComparer.Ordinal);
        union.UnionWith(b);
        return union.Count == 0 ? 1 : (double)a.Count(b.Contains) / union.Count;
    }

    /// <summary>Share of <paramref name="part"/> found in <paramref name="whole"/>.</summary>
    public static double Containment(HashSet<string> part, HashSet<string> whole) =>
        part.Count == 0 ? 0 : (double)part.Count(whole.Contains) / part.Count;

    public static double Jaccard(string a, string b) => Jaccard(Bigrams(a), Bigrams(b));

    public static double Containment(string part, string whole) => Containment(Bigrams(part), Bigrams(whole));

    /// <summary>Whole words where the script has them, character pairs for Chinese/Japanese.</summary>
    public static HashSet<string> Terms(string text)
    {
        var result = new HashSet<string>(StringComparer.Ordinal);
        var word = new System.Text.StringBuilder();
        char? previousCjk = null;
        void Flush()
        {
            if (word.Length >= 2) result.Add(word.ToString());
            word.Clear();
        }
        foreach (var character in text.ToLowerInvariant())
        {
            var isCjk = char.ConvertToUtf32(character.ToString(), 0) >= 0x2E80;
            if (isCjk && char.IsLetterOrDigit(character))
            {
                Flush();
                if (previousCjk is { } previous) result.Add(new string([previous, character]));
                previousCjk = character;
            }
            else if (char.IsLetterOrDigit(character))
            {
                previousCjk = null;
                word.Append(character);
            }
            else
            {
                Flush();
                previousCjk = null;
            }
        }
        Flush();
        return result;
    }

    /// <summary>Without the model: the screen matters when a fair share of recent speech appears on it.</summary>
    public static bool Relevant(string screen, string recentSpeech)
    {
        var speech = Bigrams(recentSpeech);
        return speech.Count >= 8 && Containment(speech, Bigrams(screen)) >= 0.25;
    }
}
