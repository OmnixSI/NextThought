using System.Text;
using System.Text.Json;

namespace NextThought.Core;

/// <summary>
/// One JSON object per line (Mac JSONLines): a torn final line is ignored, not treated as
/// corrupting the earlier rows. Used by session files and their recovery code.
/// </summary>
public static class JSONLines
{
    private static readonly JsonSerializerOptions Options = new(JsonDefaults.Options)
    {
        WriteIndented = false
    };

    public static byte[] Line<T>(T value, JsonSerializerOptions? options = null)
    {
        var json = JsonSerializer.Serialize(value, options ?? Options);
        return Encoding.UTF8.GetBytes(json + "\n");
    }

    public static IReadOnlyList<T> Decode<T>(byte[] data, JsonSerializerOptions? options = null)
    {
        var text = Encoding.UTF8.GetString(data);
        if (text.Length == 0) return [];
        var lines = text.Split('\n');
        var result = new List<T>();
        for (var index = 0; index < lines.Length; index++)
        {
            var line = lines[index];
            if (string.IsNullOrWhiteSpace(line)) continue;
            try
            {
                var value = JsonSerializer.Deserialize<T>(line, options ?? Options);
                if (value is not null) result.Add(value);
            }
            catch (JsonException)
            {
                // Only the unterminated final line may be torn; earlier ones are still kept.
                if (index == lines.Length - 1) continue;
                continue;
            }
        }
        return result;
    }

    /// <summary>B22: the report form of <see cref="Decode{T}"/>; a torn last line is flagged.</summary>
    public static JSONLinesDecodeReport<T> DecodeReport<T>(byte[] data, JsonSerializerOptions? options = null)
    {
        var report = new JSONLinesDecodeReport<T>();
        if (data.Length == 0) return report;
        var terminated = data[^1] == (byte)'\n';
        var text = Encoding.UTF8.GetString(data);
        var rawLines = text.Split('\n');
        var lineCount = terminated ? Math.Max(0, rawLines.Length - 1) : rawLines.Length;
        for (var index = 0; index < lineCount; index++)
        {
            var line = rawLines[index];
            if (line.Length == 0) continue;
            try
            {
                var value = JsonSerializer.Deserialize<T>(line, options ?? Options);
                if (value is not null) report.Records.Add(value);
                else report.MalformedLineNumbers.Add(index + 1);
            }
            catch (JsonException)
            {
                report.MalformedLineNumbers.Add(index + 1);
            }
        }
        report.TruncatedFinalLine = !terminated && lineCount > 0
            && report.MalformedLineNumbers.Count > 0 && report.MalformedLineNumbers[^1] == lineCount;
        return report;
    }
}

/// <summary>B22: records decoded from a JSONL file plus where the file was malformed.</summary>
public sealed class JSONLinesDecodeReport<T>
{
    public List<T> Records { get; } = [];
    public List<int> MalformedLineNumbers { get; } = [];
    public bool TruncatedFinalLine { get; set; }
}
