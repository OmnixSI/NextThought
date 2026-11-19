using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace NextThought.Core;

/// <summary>What kind of name a glossary entry is.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<GlossaryKind>))]
public enum GlossaryKind
{
    Term,
    Person,
    Project,
    Acronym,
    Place,
    Product
}

public static class GlossaryKinds
{
    public static string Raw(this GlossaryKind kind) => kind switch
    {
        GlossaryKind.Person => "person",
        GlossaryKind.Project => "project",
        GlossaryKind.Acronym => "acronym",
        GlossaryKind.Place => "place",
        GlossaryKind.Product => "product",
        _ => "term"
    };

    public static GlossaryKind Parse(string? raw) => raw switch
    {
        "person" => GlossaryKind.Person,
        "project" => GlossaryKind.Project,
        "acronym" => GlossaryKind.Acronym,
        "place" => GlossaryKind.Place,
        "product" => GlossaryKind.Product,
        _ => GlossaryKind.Term
    };

    public static string Title(this GlossaryKind kind) => kind switch
    {
        GlossaryKind.Person => "人名",
        GlossaryKind.Project => "项目",
        GlossaryKind.Acronym => "缩写",
        GlossaryKind.Place => "地点",
        GlossaryKind.Product => "产品",
        _ => "术语"
    };
}

/// <summary>One name or term: its canonical spelling and the spellings speech recognition got wrong.</summary>
public sealed record GlossaryEntry
{
    public GlossaryEntry(string canonical, GlossaryKind kind = GlossaryKind.Term,
        IReadOnlyList<string>? variants = null, string? note = null, string? id = null)
    {
        Id = id ?? Guid.NewGuid().ToString("D");
        Canonical = canonical;
        Kind = kind;
        Variants = variants ?? [];
        Note = note;
    }

    public string Id { get; init; }
    /// <summary>The spelling every transcript, answer and note should use.</summary>
    public string Canonical { get; init; }
    public GlossaryKind Kind { get; init; }
    /// <summary>Wrong spellings the recognizer produced ("K8s"). Never used as hotwords.</summary>
    public IReadOnlyList<string> Variants { get; init; }
    public string? Note { get; init; }

    /// <summary>The canonical spelling followed by the wrong ones.</summary>
    public IEnumerable<string> Spellings => new[] { Canonical }.Concat(Variants);
}

/// <summary>The user's table of names and terms ("术语和人名"). Ported from Glossary.swift.</summary>
/// <summary>
/// Mac's glossary only has term/person. Extended Windows kinds are written as term/person
/// plus a sourceKind field Mac ignores, so the same file stays readable on both sides.
/// </summary>
public sealed class GlossaryEntryConverter : JsonConverter<GlossaryEntry>
{
    public override GlossaryEntry Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options)
    {
        using var document = JsonDocument.ParseValue(ref reader);
        var root = document.RootElement;
        var canonical = root.TryGetProperty("canonical", out var canonicalElement) ? canonicalElement.GetString() ?? "" : "";
        var rawKind = root.TryGetProperty("sourceKind", out var sourceKind) ? sourceKind.GetString()
            : root.TryGetProperty("kind", out var kind) ? kind.GetString() : null;
        var variants = root.TryGetProperty("variants", out var variantsElement) && variantsElement.ValueKind == JsonValueKind.Array
            ? variantsElement.EnumerateArray().Select(item => item.GetString() ?? "").Where(item => item.Length > 0).ToList()
            : [];
        var note = root.TryGetProperty("note", out var noteElement) ? noteElement.GetString() : null;
        var id = root.TryGetProperty("id", out var idElement) ? idElement.GetString() : null;
        return new GlossaryEntry(canonical, GlossaryKinds.Parse(rawKind), variants, note, id);
    }

    public override void Write(Utf8JsonWriter writer, GlossaryEntry value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteString("id", value.Id);
        writer.WriteString("canonical", value.Canonical);
        writer.WriteString("kind", value.Kind == GlossaryKind.Person ? "person" : "term");
        if (value.Kind is not (GlossaryKind.Term or GlossaryKind.Person)) writer.WriteString("sourceKind", value.Kind.Raw());
        writer.WriteStartArray("variants");
        foreach (var variant in value.Variants) writer.WriteStringValue(variant);
        writer.WriteEndArray();
        if (value.Note is { Length: > 0 } note) writer.WriteString("note", note);
        writer.WriteEndObject();
    }
}

public sealed record Glossary
{
    public Glossary(IReadOnlyList<GlossaryEntry>? entries = null) => Entries = entries ?? [];

    public IReadOnlyList<GlossaryEntry> Entries { get; init; }

    public bool IsEmpty => Entries.Count == 0;

    public static readonly Glossary Empty = new();

    /// <summary>
    /// Trims, drops empty entries, merges entries whose canonical spellings share a loose key (the
    /// first keeps its id, kind and note; variants are united) and cleans each variant list: none
    /// empty, none equal to the canonical spelling, none shorter than two characters, duplicates
    /// (ignoring case and width) removed. Ported from <c>Glossary.normalized()</c>.
    /// </summary>
    public Glossary Normalized()
    {
        var result = new List<GlossaryEntry>();
        var indexByKey = new Dictionary<string, int>();
        foreach (var entry in Entries)
        {
            var canonical = Tidy(entry.Canonical);
            var key = LooseKey(canonical);
            if (canonical.Length == 0 || key.Length == 0) continue;
            var note = entry.Note is { } value ? Tidy(value) : null;
            note = note is { Length: > 0 } ? note : null;
            if (indexByKey.TryGetValue(key, out var index))
            {
                // The same name spelled differently ("OpenAI" / "Open AI"): the later spelling becomes a variant.
                var existing = result[index];
                result[index] = existing with
                {
                    Variants = [.. existing.Variants, canonical, .. entry.Variants],
                    Note = existing.Note ?? note
                };
            }
            else
            {
                indexByKey[key] = result.Count;
                result.Add(entry with { Canonical = canonical, Variants = entry.Variants.ToList(), Note = note });
            }
        }
        for (var index = 0; index < result.Count; index++)
            result[index] = result[index] with { Variants = CleanVariants(result[index].Variants, result[index].Canonical) };
        return new Glossary(result);
    }

    private static List<string> CleanVariants(IReadOnlyList<string> variants, string canonical)
    {
        var seen = new HashSet<string>();
        var cleaned = new List<string>();
        foreach (var variant in variants.Select(Tidy))
        {
            if (variant.Length < 2 || variant == canonical || LooseKey(variant).Length == 0) continue;
            if (seen.Add(StrictKey(variant))) cleaned.Add(variant);
        }
        return cleaned;
    }

    /// <summary>
    /// Equality key for comparison: width-folded, lowercased, whitespace and punctuation removed.
    /// "Open AI", "open-ai" and "ＯｐｅｎＡＩ" are the same key; "+" and "#" are kept so that
    /// "C++" and "C#" stay different from "C".
    /// </summary>
    public static string LooseKey(string text)
    {
        var builder = new StringBuilder();
        foreach (var character in Fold(text, lowercase: true))
            if (char.IsLetterOrDigit(character) || character is '+' or '#') builder.Append(character);
        return builder.ToString();
    }

    /// <summary>Case-sensitive comparison: only whitespace and punctuation are ignored.</summary>
    public static string StrictKey(string text)
    {
        var builder = new StringBuilder();
        foreach (var character in Fold(text, lowercase: false))
            if (char.IsLetterOrDigit(character) || character is '+' or '#') builder.Append(character);
        return builder.ToString();
    }

    /// <summary>
    /// <paramref name="text"/> with every recorded wrong spelling replaced by its canonical spelling.
    /// Longest spelling wins; a canonical spelling takes part in matching but is never rewritten;
    /// variants match ignoring case and full/half width; a spelling that starts or ends with a Latin
    /// letter or digit must not touch another Latin letter, digit or underscore on that side.
    /// </summary>
    public string Canonicalize(string text) => CanonicalizeDetailed(text).Text;

    public GlossaryCanonicalization CanonicalizeDetailed(string text)
    {
        var chars = text.ToCharArray();
        var spans = Scan(chars, caseSensitiveCanonical: true);
        if (spans.Count == 0) return new GlossaryCanonicalization(text, []);
        var output = new StringBuilder();
        var cursor = 0;
        var replacements = new List<GlossaryReplacement>();
        foreach (var span in spans.Where(span => !span.Candidate.IsCanonical))
        {
            var canonical = Entries[span.Candidate.EntryIndex].Canonical;
            var original = new string(chars, span.Start, span.End - span.Start);
            if (original == canonical) continue;
            output.Append(chars, cursor, span.Start - cursor).Append(canonical);
            cursor = span.End;
            var existing = replacements.FindIndex(item => item.From == original && item.To == canonical);
            if (existing >= 0) replacements[existing] = replacements[existing] with { Count = replacements[existing].Count + 1 };
            else replacements.Add(new GlossaryReplacement(original, canonical, 1));
        }
        output.Append(chars, cursor, chars.Length - cursor);
        return new GlossaryCanonicalization(output.ToString(), replacements);
    }

    /// <summary>Spellings to give the recognizer: canonical forms only, longest first, one per line.</summary>
    public IReadOnlyList<string> Hotwords(int limit = 200) =>
        Entries.Select(entry => Tidy(entry.Canonical))
            .Where(word => word.Length > 0)
            .Distinct()
            .OrderByDescending(word => word.Length)
            .Take(limit)
            .ToList();

    /// <summary>The glossary as a prompt section for notes and answers (empty when there is nothing to say).</summary>
    public string PromptSection()
    {
        if (IsEmpty) return "";
        var builder = new StringBuilder("## 术语表\n在转写、回答与纪要中一律使用下面的写法：\n");
        foreach (var entry in Entries)
        {
            builder.Append("- ").Append(entry.Canonical);
            if (entry.Variants.Count > 0) builder.Append("（不写成：").Append(string.Join("、", entry.Variants)).Append('）');
            if (entry.Note is { Length: > 0 } note) builder.Append("：").Append(note);
            builder.Append('\n');
        }
        return builder.ToString().TrimEnd();
    }

    /// <summary>"每行一个术语；别名用 | 分隔；# 开头是人名/项目等分类"。 Also accepts a JSON array of entries.</summary>
    public static Glossary ParseText(string text, GlossaryKind defaultKind = GlossaryKind.Term)
    {
        var trimmed = text.Trim();
        if (trimmed.StartsWith('[')) return ParseJson(trimmed);
        var entries = new List<GlossaryEntry>();
        foreach (var rawLine in text.Replace("\r\n", "\n").Split('\n'))
        {
            var line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith('#')) continue;
            // Mac format: canonical | variants (comma separated) | kind | note.
            var columns = line.Replace('｜', '|').Split('|').Select(Tidy).ToArray();
            if (columns.Length == 0 || columns[0].Length == 0) continue;
            var variants = columns.Length > 1
                ? columns[1].Split([',', '，', '、', ';', '；'], StringSplitOptions.RemoveEmptyEntries).Select(Tidy).Where(part => part.Length > 0).ToList()
                : [];
            var kind = defaultKind;
            string? note = null;
            if (columns.Length > 2 && columns[2].Length > 0)
            {
                var parsedKind = columns[2] switch
                {
                    "person" or "人名" or "姓名" or "name" or "people" => GlossaryKind.Person,
                    "project" or "项目" => GlossaryKind.Project,
                    "acronym" or "缩写" => GlossaryKind.Acronym,
                    "place" or "地点" => GlossaryKind.Place,
                    "product" or "产品" => GlossaryKind.Product,
                    "term" or "术语" or "词" or "专有名词" or "word" => GlossaryKind.Term,
                    _ => (GlossaryKind?)null
                };
                if (parsedKind is { } value) kind = value;
                else note = columns[2];
                if (columns.Length > 3 && columns[3].Length > 0) note = columns[3];
            }
            entries.Add(new GlossaryEntry(columns[0], kind, variants, note));
        }
        return new Glossary(entries).Normalized();
    }

    private static Glossary ParseJson(string text)
    {
        try
        {
            var entries = JsonSerializer.Deserialize<List<GlossaryEntry>>(text, GlossaryStore.Options);
            return new Glossary((entries ?? []).Where(entry => !string.IsNullOrWhiteSpace(entry.Canonical)).ToList()).Normalized();
        }
        catch (JsonException)
        {
            return Empty;
        }
    }

    /// <summary>The entry whose canonical spelling or a variant equals the text under LooseKey.</summary>
    public GlossaryEntry? EntryMatching(string text)
    {
        var key = LooseKey(text);
        if (key.Length == 0) return null;
        return Entries.FirstOrDefault(entry => entry.Spellings.Any(spelling => LooseKey(spelling) == key));
    }

    /// <summary>Every spelling to look for when the person searches for the query (query first).</summary>
    public IReadOnlyList<string> Expand(string query) => Search(query).Forms;

    /// <summary>
    /// Expansion plus which spellings are recorded misheard ones. The typed query comes first;
    /// when it equals an entry's spelling all spellings follow, otherwise a contained spelling is
    /// replaced by each other spelling of its entry. At most 32 forms.
    /// </summary>
    public GlossarySearch Search(string query)
    {
        var typed = query.Trim();
        if (typed.Length == 0) return new GlossarySearch("", [], new HashSet<string>());
        var forms = new List<string> { typed };
        var seen = new HashSet<string>(StringComparer.Ordinal) { StrictKey(typed) };
        var misheard = new HashSet<string>(StringComparer.Ordinal);

        void Add(string form)
        {
            var key = StrictKey(form);
            if (key.Length == 0 || forms.Count >= MaxExpansion || !seen.Add(key)) return;
            forms.Add(form);
        }

        var canonicalKeys = Entries.Select(entry => StrictKey(entry.Canonical)).ToHashSet(StringComparer.Ordinal);
        foreach (var entry in Entries)
            foreach (var variant in entry.Variants)
                if (!canonicalKeys.Contains(StrictKey(variant))) misheard.Add(StrictKey(variant));

        var loose = LooseKey(typed);
        var found = false;
        if (loose.Length > 0)
        {
            foreach (var entry in Entries.Where(entry => entry.Spellings.Any(spelling => LooseKey(spelling) == loose)))
            {
                found = true;
                foreach (var spelling in entry.Spellings) Add(spelling);
            }
        }
        if (!found)
        {
            var chars = typed.ToCharArray();
            foreach (var span in Scan(chars, caseSensitiveCanonical: false))
            {
                var entry = Entries[span.Candidate.EntryIndex];
                var before = new string(chars, 0, span.Start);
                var after = new string(chars, span.End, chars.Length - span.End);
                var original = StrictKey(new string(chars, span.Start, span.End - span.Start));
                foreach (var spelling in entry.Spellings.Where(spelling => StrictKey(spelling) != original))
                {
                    var composed = before + spelling + after;
                    if (entry.Variants.Contains(spelling) && !canonicalKeys.Contains(StrictKey(spelling)))
                        misheard.Add(StrictKey(composed));
                    Add(composed);
                }
            }
        }
        return new GlossarySearch(typed, forms, misheard);
    }

    /// <summary>This table followed by <paramref name="other"/>, normalized; variants are united.</summary>
    public Glossary Merging(Glossary other) => new Glossary(Entries.Concat(other.Entries).ToList()).Normalized();

    /// <summary>The table in force for a series: the global table plus the series' own.</summary>
    public static Glossary Layered(Glossary global, Glossary? series) =>
        series is null || series.IsEmpty ? global.Normalized() : global.Merging(series);

    private const int MaxExpansion = 32;

    private static bool ContainsFold(string text, string form)
    {
        var haystack = text.Select(character => Fold(character, lowercase: true)).ToArray();
        var needle = form.Select(character => Fold(character, lowercase: true)).ToArray();
        if (needle.Length == 0 || haystack.Length < needle.Length) return false;
        for (var index = 0; index <= haystack.Length - needle.Length; index++)
            if (haystack.AsSpan(index, needle.Length).SequenceEqual(needle)) return true;
        return false;
    }

    // ---- matching engine ----

    private sealed record Candidate(int EntryIndex, bool IsCanonical, int Order, char[] Lower, char[] Kept,
        bool NeedsLeftBoundary, bool NeedsRightBoundary);

    private sealed record Span(int Start, int End, Candidate Candidate);

    private Dictionary<char, List<Candidate>> CandidateIndex()
    {
        var index = new Dictionary<char, List<Candidate>>();
        var order = 0;
        for (var entryIndex = 0; entryIndex < Entries.Count; entryIndex++)
        {
            var entry = Entries[entryIndex];
            var spellings = new List<(string Text, bool IsCanonical)> { (Tidy(entry.Canonical), true) };
            spellings.AddRange(entry.Variants.Select(variant => (Tidy(variant), false)));
            foreach (var (spelling, isCanonical) in spellings)
            {
                var raw = spelling.ToCharArray();
                // Single-character variants are too noisy to rewrite.
                if (raw.Length == 0 || (!isCanonical && raw.Length < 2)) continue;
                var lower = raw.Select(character => Fold(character, lowercase: true)).ToArray();
                var kept = raw.Select(character => Fold(character, lowercase: false)).ToArray();
                order++;
                var candidate = new Candidate(entryIndex, isCanonical, order, lower, kept,
                    IsLatinWord(lower[0]), IsLatinWord(lower[^1]));
                if (!index.TryGetValue(lower[0], out var list)) index[lower[0]] = list = [];
                list.Add(candidate);
            }
        }
        foreach (var key in index.Keys.ToList())
        {
            index[key].Sort((lhs, rhs) =>
            {
                if (lhs.Lower.Length != rhs.Lower.Length) return rhs.Lower.Length.CompareTo(lhs.Lower.Length);
                if (lhs.IsCanonical != rhs.IsCanonical) return lhs.IsCanonical ? -1 : 1;
                return lhs.Order.CompareTo(rhs.Order);
            });
        }
        return index;
    }

    private List<Span> Scan(char[] chars, bool caseSensitiveCanonical)
    {
        var spans = new List<Span>();
        if (chars.Length == 0 || IsEmpty) return spans;
        var index = CandidateIndex();
        if (index.Count == 0) return spans;
        var lower = chars.Select(character => Fold(character, lowercase: true)).ToArray();
        var kept = chars.Select(character => Fold(character, lowercase: false)).ToArray();
        var position = 0;
        while (position < lower.Length)
        {
            Candidate? hit = null;
            if (index.TryGetValue(lower[position], out var candidates))
            {
                foreach (var candidate in candidates)
                {
                    var length = candidate.Lower.Length;
                    if (position + length > lower.Length) continue;
                    var same = candidate.IsCanonical && caseSensitiveCanonical
                        ? kept.AsSpan(position, length).SequenceEqual(candidate.Kept)
                        : lower.AsSpan(position, length).SequenceEqual(candidate.Lower);
                    if (!same) continue;
                    if (candidate.NeedsLeftBoundary && position > 0 && IsLatinWord(lower[position - 1])) continue;
                    if (candidate.NeedsRightBoundary && position + length < lower.Length && IsLatinWord(lower[position + length])) continue;
                    hit = candidate;
                    break;
                }
            }
            if (hit is null) { position++; continue; }
            spans.Add(new Span(position, position + hit.Lower.Length, hit));
            position += hit.Lower.Length;
        }
        return spans;
    }

    private static bool IsLatinWord(char character) =>
        character is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or >= '0' and <= '9' or '_';

    /// <summary>Width folding (full width → half width) and optional lower casing for one character.</summary>
    private static char Fold(char character, bool lowercase)
    {
        var value = character;
        if (value is >= '\uFF01' and <= '\uFF5E') value = (char)(value - 0xFEE0);
        else if (value == '\u3000') value = ' ';
        return lowercase ? char.ToLowerInvariant(value) : value;
    }

    private static IEnumerable<char> Fold(string text, bool lowercase) => text.Select(character => Fold(character, lowercase));

    private static string Tidy(string text) => text.Trim();
}

public sealed record GlossaryReplacement(string From, string To, int Count);

/// <summary>The spelling found in the text; misheard means a recorded wrong spelling the person did not type.</summary>
public sealed record GlossarySearchHit(string Form, bool IsMisheard);

/// <summary>A search term together with every spelling the glossary knows for it.</summary>
public sealed record GlossarySearch(string Query, IReadOnlyList<string> Forms, IReadOnlySet<string> MisheardKeys)
{
    /// <summary>The first spelling found in the text; the typed query wins over other spellings.</summary>
    public GlossarySearchHit? FirstMatch(string text)
    {
        for (var index = 0; index < Forms.Count; index++)
        {
            var form = Forms[index];
            if (!FormFold(text).Contains(form, StringComparison.OrdinalIgnoreCase)) continue;
            var misheard = index > 0 && MisheardKeys.Contains(Glossary.StrictKey(form));
            return new GlossarySearchHit(form, misheard);
        }
        return null;
    }

    public bool Matches(string text) => FirstMatch(text) is not null;

    private static string FormFold(string text) =>
        new(text.Select(character => character is >= '\uFF01' and <= '\uFF5E' ? (char)(character - 0xFEE0)
            : character == '\u3000' ? ' ' : character).ToArray());
}

public sealed record GlossaryCanonicalization(string Text, IReadOnlyList<GlossaryReplacement> Replacements)
{
    public int ReplacementCount => Replacements.Sum(item => item.Count);
}

/// <summary>Reads and writes <c>Glossary/glossary.json</c>.</summary>
public sealed class GlossaryStore
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase), new GlossaryEntryConverter() }
    };

    private readonly string path;

    public GlossaryStore(string dataDirectory) => path = Path.Combine(dataDirectory, "Glossary", "glossary.json");

    public string FilePath => path;

    public Glossary Load()
    {
        try
        {
            if (!File.Exists(path)) return Glossary.Empty;
            var text = File.ReadAllText(path);
            using var document = System.Text.Json.JsonDocument.Parse(text);
            // Mac writes {"entries":[...]}; older Windows builds wrote a bare array.
            if (document.RootElement.ValueKind == System.Text.Json.JsonValueKind.Object
                && document.RootElement.TryGetProperty("entries", out var entries)
                && entries.ValueKind == System.Text.Json.JsonValueKind.Array)
                return new Glossary(entries.Deserialize<List<GlossaryEntry>>(Options)).Normalized();
            if (document.RootElement.ValueKind == System.Text.Json.JsonValueKind.Array)
                return new Glossary(document.RootElement.Deserialize<List<GlossaryEntry>>(Options)).Normalized();
            return Glossary.Empty;
        }
        catch (Exception)
        {
            // A damaged table must not stop the app; saving is blocked by the caller until the user confirms.
            return Glossary.Empty;
        }
    }

    public void Save(Glossary glossary)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var document = new Dictionary<string, object> { ["entries"] = glossary.Entries };
        File.WriteAllText(path, JsonSerializer.Serialize(document, Options));
    }

    /// <summary>The table as the text box shows it: canonical | variant | variant.</summary>
    public static string ToText(Glossary glossary) =>
        string.Join('\n', glossary.Entries.Select(entry =>
            string.Join(" | ", new[] { entry.Canonical }.Concat(entry.Variants))));
}
