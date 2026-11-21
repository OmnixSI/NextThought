using System.Globalization;
using System.Text;

namespace NextThought.Core;

/// <summary>
/// The Windows interface string table. Chinese is the source language: the catalogue key is the
/// Mac original sentence (G8), so Chinese mode returns the key and English mode looks the key up.
/// English translations come from two places: the generated Mac catalogue
/// (<see cref="UiTextMacEnglish"/>, 2,638 entries) and the hand-written Windows-only wording table
/// (<see cref="UiTextWindowsEnglish"/>, every sentence whose Windows wording differs from Mac — G10
/// records each of these in <c>Windows/WORDING.md</c>).
/// </summary>
public static class UiText
{
    public static bool IsEnglish(string? language) =>
        !string.IsNullOrWhiteSpace(language) && language.StartsWith("en", StringComparison.OrdinalIgnoreCase);

    private static readonly Dictionary<string, string> English = BuildEnglish();

    /// <summary>G7: how many Mac catalogue strings the generated table carries.</summary>
    public static int MacStringCount => UiTextMacEnglish.Count;

    /// <summary>Number of hand-written Windows-only entries (the G10 replacement table).</summary>
    public static int WindowsStringCount => UiTextWindowsEnglish.Count;

    private static Dictionary<string, string> BuildEnglish()
    {
        // Windows-specific wording wins for identical keys; Mac entries fill the remaining 2,607.
        var dictionary = new Dictionary<string, string>(UiTextWindowsEnglish.Strings, StringComparer.Ordinal);
        foreach (var pair in UiTextMacEnglish.Strings) dictionary.TryAdd(pair.Key, pair.Value);
        return dictionary;
    }

    private static readonly Dictionary<string, string> Chinese = English
        .GroupBy(pair => pair.Value, StringComparer.Ordinal)
        .ToDictionary(group => group.Key, group => group.First().Key, StringComparer.Ordinal);

    /// <summary>Legacy direction-agnostic lookup kept for call sites that store either language.</summary>
    public static string Translate(string text, bool english)
    {
        if (english) return English.TryGetValue(text, out var translated) ? translated : text;
        return Chinese.TryGetValue(text, out var original) ? original : text;
    }

    /// <summary>G8: the one lookup used by the markup extension and the helpers below.</summary>
    public static string Lookup(string key, bool english) =>
        english && English.TryGetValue(key, out var translated) ? translated : key;

    /// <summary>True when the key has an English translation (used by coverage tests/scans).</summary>
    public static bool HasTranslation(string key) => English.ContainsKey(key);

    /// <summary>G8: does the key exist in the Mac catalogue (i.e. is the wording shared with Mac)?</summary>
    public static bool IsMacString(string key) => UiTextMacEnglish.Strings.ContainsKey(key);

    /// <summary>All key → English pairs (Mac + Windows-only) for the localization scan.</summary>
    public static IReadOnlyDictionary<string, string> EnglishStrings => English;

    /// <summary>G8: the hand-written Windows-only replacement table (G10 source).</summary>
    public static IReadOnlyDictionary<string, string> WindowsStrings => UiTextWindowsEnglish.Strings;

    /// <summary>
    /// G8: resolves a stored language preference. Mac stores "system" (default), "zh-Hans" or "en";
    /// the Windows setting also accepts "zh" and the picker labels.
    /// </summary>
    public static bool ResolveEnglish(string? preference)
    {
        var normalized = UiTextSource.NormalizePreference(preference);
        return normalized switch
        {
            UiTextSource.EnglishPreference => true,
            UiTextSource.ChinesePreference => false,
            _ => SystemIsEnglish()
        };
    }

    /// <summary>G8: with the default "跟随系统" the first launch follows the Windows display language.</summary>
    public static bool SystemIsEnglish() => SystemIsEnglish(CultureInfo.CurrentUICulture);

    /// <summary>Test seam: the app ships Chinese and English, so every other system language resolves to English.</summary>
    public static bool SystemIsEnglish(CultureInfo culture)
    {
        ArgumentNullException.ThrowIfNull(culture);
        var language = culture.TwoLetterISOLanguageName;
        return !string.Equals(language, "zh", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Global UI language flag for row/subtitle formatting outside the Avalonia shell.</summary>
    public static bool EnglishUi { get; set; }

    public static string T(string text) => Translate(text, EnglishUi);

    /// <summary>
    /// G8: formats a catalogue sentence. The source sentence carries the Mac placeholder style
    /// (%@ string, %lld integer, %.2f double, %1$@/%2$lld positional, %% literal).
    /// </summary>
    public static string Format(string key, params object?[] arguments) =>
        FormatTemplate(Lookup(key, EnglishUi), arguments);

    /// <summary>Same as <see cref="Format"/> for a template that is already resolved.</summary>
    public static string FormatTemplate(string template, params object?[] arguments)
    {
        if (string.IsNullOrEmpty(template)) return template;
        if (template.IndexOf('%') < 0 || arguments.Length == 0) return template;
        var builder = new StringBuilder(template.Length + 16);
        var next = 0;
        var index = 0;
        while (index < template.Length)
        {
            var current = template[index];
            if (current != '%')
            {
                builder.Append(current);
                index++;
                continue;
            }
            var parsed = ParsePlaceholder(template, index);
            if (parsed is null)
            {
                builder.Append(current);
                index++;
                continue;
            }
            var (conversion, argumentIndex, precision, length) = parsed.Value;
            if (conversion == '%')
            {
                builder.Append('%');
                index += length;
                continue;
            }
            var position = argumentIndex is { } explicitIndex ? explicitIndex - 1 : next++;
            if (position < 0 || position >= arguments.Length)
            {
                builder.Append(template, index, length);
                index += length;
                continue;
            }
            builder.Append(Render(arguments[position], conversion, precision));
            index += length;
        }
        return builder.ToString();
    }

    private static (char Conversion, int? ArgumentIndex, int? Precision, int Length)? ParsePlaceholder(string template, int start)
    {
        var index = start + 1;
        if (index >= template.Length) return null;
        if (template[index] == '%') return ('%', null, null, 2);
        int? argumentIndex = null;
        var digitsStart = index;
        while (index < template.Length && char.IsAsciiDigit(template[index])) index++;
        if (index < template.Length && template[index] == '$' && index > digitsStart)
        {
            if (!int.TryParse(template.AsSpan(digitsStart, index - digitsStart), NumberStyles.None, CultureInfo.InvariantCulture, out var explicitIndex))
                return null;
            argumentIndex = explicitIndex;
            index++;
        }
        else
        {
            index = digitsStart;
        }
        while (index < template.Length && "-+ #0'".Contains(template[index])) index++;
        while (index < template.Length && char.IsAsciiDigit(template[index])) index++;
        int? precision = null;
        if (index < template.Length && template[index] == '.')
        {
            index++;
            var precisionStart = index;
            while (index < template.Length && char.IsAsciiDigit(template[index])) index++;
            if (index > precisionStart &&
                int.TryParse(template.AsSpan(precisionStart, index - precisionStart), NumberStyles.None, CultureInfo.InvariantCulture, out var value))
                precision = value;
        }
        foreach (var modifier in new[] { "hh", "h", "ll", "l", "q", "L", "z", "t", "j" })
        {
            if (index + modifier.Length <= template.Length && string.CompareOrdinal(template, index, modifier, 0, modifier.Length) == 0)
            {
                index += modifier.Length;
                break;
            }
        }
        if (index >= template.Length) return null;
        var conversion = template[index];
        if (!"@diouxXeEfFgGcspaA".Contains(conversion)) return null;
        return (conversion, argumentIndex, precision, index - start + 1);
    }

    private static string Render(object? argument, char conversion, int? precision)
    {
        if (conversion == '%') return "%";
        if (argument is null) return string.Empty;
        var culture = CultureInfo.InvariantCulture;
        try
        {
            return conversion switch
            {
                '@' or 's' => argument as string ?? Convert.ToString(argument, culture) ?? string.Empty,
                'c' => Convert.ToChar(argument, culture).ToString(),
                'd' or 'i' => Convert.ToInt64(argument, culture).ToString(culture),
                'u' => ((ulong)Convert.ToInt64(argument, culture)).ToString(culture),
                'o' => Convert.ToString(Convert.ToInt64(argument, culture), 8),
                'x' => Convert.ToString(Convert.ToInt64(argument, culture), 16),
                'X' => Convert.ToString(Convert.ToInt64(argument, culture), 16).ToUpperInvariant(),
                'f' or 'F' => Convert.ToDouble(argument, culture).ToString("F" + (precision ?? 6), culture),
                'e' => Convert.ToDouble(argument, culture).ToString("e" + (precision ?? 6), culture),
                'E' => Convert.ToDouble(argument, culture).ToString("E" + (precision ?? 6), culture),
                'g' => Convert.ToDouble(argument, culture).ToString("g" + (precision ?? 6), culture),
                'G' => Convert.ToDouble(argument, culture).ToString("G" + (precision ?? 6), culture),
                _ => Convert.ToString(argument, culture) ?? string.Empty
            };
        }
        catch (Exception exception) when (exception is FormatException or InvalidCastException or OverflowException)
        {
            return Convert.ToString(argument, culture) ?? string.Empty;
        }
    }

    // Backwards-compatible count helpers (Mac plural rule: other branch only).
    public static string Meetings(int count) => EnglishUi
        ? $"{count} meeting{(count == 1 ? "" : "s")}" : $"{count} 场会议";

    public static string Transcripts(int count) => EnglishUi
        ? $"{count} line{(count == 1 ? "" : "s")}" : $"{count} 条转写";

    public static string Answers(int count) => EnglishUi
        ? $"{count} answer{(count == 1 ? "" : "s")}" : $"{count} 条回答";

    public static string Lessons(int count) => EnglishUi
        ? $"{count} lesson{(count == 1 ? "" : "s")}" : $"{count} 条经验";

    public static string Pending(int count) => EnglishUi
        ? $"{count} to confirm" : $"{count} 条待确认";

    public static string SeriesCount(int count) => EnglishUi
        ? $"{count} series" : $"{count} 个系列";

    public static string Asked(int count) => EnglishUi
        ? $"asked {count} time{(count == 1 ? "" : "s")}" : $"被问到 {count} 次";

    public static string Materials(int count) => EnglishUi
        ? $"{count} material{(count == 1 ? "" : "s")}" : $"{count} 份资料";
}
