namespace NextThought.Core;

/// <summary>
/// What a document reader left behind: things to check and things that are only said
/// (port of DocumentNotice.swift). Only warnings mark a file as needing a look.
/// </summary>
public static class DocumentNotice
{
    private static readonly string[] NotePhrases =
    [
        "页内图像中的文字", "inside page images have not been read",
        "的图片没有读取", "图片里的文字没有读取"
    ];

    /// <summary>The same line said twice says nothing more.</summary>
    public static IReadOnlyList<string> Unique(IReadOnlyList<string> warnings)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        return warnings.Where(seen.Add).ToList();
    }

    public static bool IsNote(string warning) => NotePhrases.Any(phrase => warning.Contains(phrase, StringComparison.Ordinal));

    /// <summary>Lines that need a look.</summary>
    public static IReadOnlyList<string> Warnings(IReadOnlyList<string> lines) =>
        Unique(lines).Where(line => !IsNote(line)).ToList();

    /// <summary>
    /// The notes as one quiet line ("p.7、15 的图片没有读取，正文已完整提取。"), or null without any.
    /// </summary>
    public static string? Note(IReadOnlyList<string> lines)
    {
        var notes = Unique(lines).Where(IsNote).ToList();
        if (notes.Count == 0) return null;
        var pages = new List<int>();
        var slides = new List<int>();
        var other = false;
        foreach (var line in notes)
        {
            if (NumberAfter("p.", line) is { } page) pages.Add(page);
            else if ((NumberAfter("幻灯片 ", line) ?? NumberAfter("Slide ", line)) is { } slide) slides.Add(slide);
            else other = true;
        }
        var places = new List<string>();
        if (pages.Count > 0) places.Add("p." + List(pages));
        if (slides.Count > 0) places.Add("幻灯片 " + List(slides));
        if (places.Count == 0 || other) return "部分图片里的文字没有读取，正文已完整提取。";
        return string.Join("、", places) + " 的图片没有读取，正文已完整提取。";
    }

    private static int? NumberAfter(string prefix, string line)
    {
        if (!line.StartsWith(prefix, StringComparison.Ordinal)) return null;
        var digits = line[prefix.Length..].TakeWhile(char.IsNumber).ToArray();
        return digits.Length == 0 ? null : int.Parse(new string(digits));
    }

    private static string List(IEnumerable<int> numbers) =>
        string.Join("、", numbers.Distinct().OrderBy(number => number));
}
