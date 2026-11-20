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
}
